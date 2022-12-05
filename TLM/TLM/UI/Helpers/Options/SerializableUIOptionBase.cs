namespace TrafficManager.UI.Helpers {
    using ColossalFramework.UI;
    using ColossalFramework;
    using CSUtil.Commons;
    using ICities;
    using System.Reflection;
    using System;
    using System.Threading;
    using TrafficManager.State;
    using JetBrains.Annotations;
    using TrafficManager.Lifecycle;
    using TrafficManager.Util;
    using System.Runtime.InteropServices;
    using UnityEngine;
    using System.Runtime.Serialization.Formatters;
    using System.Collections.Generic;

    public abstract class SerializableUIOptionBase : ILegacySerializableOption {
        [CanBeNull]
        private string _label;
        private string _tooltip;
        private bool _readOnly;

        public SerializableUIOptionBase(string name) => Name = name;

        public string Name { get; private set; }

        public bool Indent { get; set; }

        public delegate string TranslatorDelegate(string key);

        public TranslatorDelegate Translator { get; private set; }

        public string Label {
            get => _label ?? $"{GetType()}:{Name}";
            set {
                _label = value;
                UpdateLabel();
            }
        }

        public string Tooltip {
            get => _tooltip ?? string.Empty;
            set {
                _tooltip = value;
                UpdateTooltip();
            }
        }

        public bool ReadOnly {
            get => _readOnly;
            set {
                _readOnly = value;
                UpdateReadOnly();
            }
        }

        public abstract bool HasUI { get; }

        /// <summary>Terse shortcut for <c>Translator(key)</c>.</summary>
        /// <param name="key">The locale key to translate.</param>
        /// <returns>Returns localised string for <paramref name="key"/>.</returns>
        protected string Translate(string key) => Translator?.Invoke(key) ?? key;

        protected abstract void UpdateTooltip();

        protected abstract void UpdateReadOnly();

        protected abstract void UpdateLabel();

        public abstract void Load(byte data);

        public abstract byte Save();
    }

    public abstract class SerializableUIOptionBase<TVal> : SerializableUIOptionBase {
        public delegate TVal ValidatorDelegate(TVal desired, out TVal result);

        public delegate void OnChanged(TVal value);

        public event OnChanged OnValueChanged;

        // used as internal store of value if _fieldInfo is null
        private TVal _value = default;

        private FieldInfo _fieldInfo;

        public SerializableUIOptionBase(string fieldName, Scope scope) : base(fieldName) {
            OnValueChanged = DefaultOnValueChanged;
            if (scope.IsFlagSet(Scope.Savegame)) {
                _fieldInfo = typeof(SavedGameOptions).GetField(fieldName) ??
                    throw new Exception($"SerializableUIOptionBase.ctor: `{fieldName}` does not exist");
            }
        }

        public OnChanged Handler {
            set {
                OnValueChanged -= value;
                OnValueChanged += value;
            }
        }

        /// <summary>
        /// Optional custom validator which intercepts value changes and can inhibit event propagation.
        /// </summary>
        public ValidatorDelegate Validator { get; set; }

        /// <summary>Gets or sets the value of the field this option represents.</summary>
        public virtual TVal Value {
            get {
                if (_fieldInfo == null) {
                    return _value;
                }

                Shortcuts.AssertNotNull(SavedGameOptions.Instance, "SavedGameOptions.Instance");
                var value = _fieldInfo.GetValue(SavedGameOptions.Instance);
                if (value is IConvertible convertibleValue) {
                    return (TVal)ChangeType(convertibleValue, typeof(TVal));
                } else {
                    return (TVal)value;
                }
            }

            set {
                if (_fieldInfo == null) {
                    _value = value;
                } else if (value is IConvertible convertibleValue) {
                    IConvertible val = ChangeType(convertibleValue, _fieldInfo.FieldType);
                    Shortcuts.AssertNotNull(SavedGameOptions.Instance, "SavedGameOptions.Instance");
                    _fieldInfo.SetValue(SavedGameOptions.Instance, val);
                } else {
                    Shortcuts.AssertNotNull(SavedGameOptions.Instance, "SavedGameOptions.Instance");
                    _fieldInfo.SetValue(SavedGameOptions.Instance, value);
                }

                if (HasUI) {
                    SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(() => SetUIValue(value));
                }
            }
        }

        public static implicit operator TVal(SerializableUIOptionBase<TVal> a) => a.Value;

        public abstract void SetUIValue(TVal value);

        public void DefaultOnValueChanged(TVal newVal) {
            if (Value.Equals(newVal)) {
                return;
            }
            Log._Debug($"SerializableUIOptionBase.DefaultOnValueChanged: `{Name}` changed to {newVal}");
            Value = newVal;
        }

        public void InvokeOnValueChanged(TVal value) => OnValueChanged?.Invoke(value);

        /// <summary>type safe version of <c>Convert.ChangeType()</c>.</summary>
        private static IConvertible ChangeType(IConvertible value, Type type) => Convert.ChangeType(value, type) as IConvertible;
    }

    public abstract class SerializableUIOptionBase<TVal, TUI> : SerializableUIOptionBase<TVal>
        where TUI : UIComponent
    {
        protected TUI _ui;

        public SerializableUIOptionBase(string fieldName, Scope scope) : base(fieldName, scope) { }

        public override bool HasUI => _ui != null;

        public abstract void AddUI(UIHelperBase container);
        protected virtual void InitUI(UIComponent ui) {
            UpdateLabel();
            UpdateTooltip();
            UpdateReadOnly();
        }

    }

    public abstract class PropagatorOptionBase<TVal, TUI> : SerializableUIOptionBase<TVal, TUI>, IValuePropagator
        where TUI : UIComponent {
        private HashSet<IValuePropagator> _propagatesTrueTo = new();
        private HashSet<IValuePropagator> _propagatesFalseTo = new();

        public PropagatorOptionBase(string fieldName, Scope scope) : base(fieldName, scope) { }

        /// <summary>
        /// If this checkbox is set <c>true</c>, it will propagate that to the <paramref name="target"/>.
        /// </summary>
        /// <param name="target">The checkbox to propagate <c>true</c> value to.</param>
        /// <remarks>
        /// If target is set <c>false</c>, it will propagate that back to this checkbox.
        /// </remarks>
        public void PropagateTrueTo([NotNull] IValuePropagator target) {
            Log.Info($"TriStateCheckboxOption.PropagateTrueTo: `{Name}` will propagate to `{target}`");
            this.AddPropagate(target, true);
            target.AddPropagate(this, false);
        }

        private HashSet<IValuePropagator> GetTargetPropagates(bool value) =>
            value ? _propagatesTrueTo : _propagatesFalseTo;

        public void AddPropagate(IValuePropagator target, bool value) =>
            GetTargetPropagates(value).Add(target);

        public abstract void Propagate(bool value);

        protected void PropagateAll(bool value) {
            foreach (var target in GetTargetPropagates(value))
                target.Propagate(value);
        }

        protected abstract void OnPropagateAll(TVal val);
    }
}