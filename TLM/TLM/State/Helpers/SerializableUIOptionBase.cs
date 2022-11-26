namespace TrafficManager.UI.Helpers {
    using ColossalFramework.UI;
    using ColossalFramework;
    using CSUtil.Commons;
    using ICities;
    using System.Reflection;
    using System;
    using TrafficManager.State;
    using JetBrains.Annotations;
    using TrafficManager.Lifecycle;

    public abstract class SerializableUIOptionBase : ILegacySerializableOption {
        /// <summary>Use as tooltip for readonly UI components.</summary>
        public delegate string TranslatorDelegate(string key);

        protected const string INGAME_ONLY_SETTING = "This setting can only be changed in-game.";

        [CanBeNull]
        protected readonly FieldInfo _fieldInfo;
        private readonly string _fieldName;
        protected Options.PersistTo _scope;
        private TranslatorDelegate _translator;
        protected string _label;
        protected string _tooltip;
        protected bool _readOnly;

        public SerializableUIOptionBase(string fieldName, Options.PersistTo scope) {
            _fieldName = fieldName;
            _scope = scope;
            if (scope.IsFlagSet(Options.PersistTo.Savegame)) {
                _fieldInfo = typeof(Options).GetField(fieldName, BindingFlags.Static | BindingFlags.Public);

                if (_fieldInfo == null) {
                    throw new Exception($"SerializableUIOptionBase.ctor: `{fieldName}` does not exist");
                }
            }
        }

        public string FieldName => _fieldInfo?.Name ?? _fieldName;

        public bool Indent { get; set; }

        /// <summary>Returns <c>true</c> if setting can persist in current <see cref="_scope"/>.</summary>
        /// <remarks>
        /// When <c>false</c>, UI component should be <see cref="_readOnly"/>
        /// and <see cref="_tooltip"/> should be set to <see cref="INGAME_ONLY_SETTING"/>.
        /// </remarks>
        protected bool IsInScope =>
            _scope.IsFlagSet(Options.PersistTo.Global) ||
            (_scope.IsFlagSet(Options.PersistTo.Savegame) && TMPELifecycle.AppMode != null) ||
            _scope == Options.PersistTo.None;

        public TranslatorDelegate Translator {
            get => _translator ?? Translation.Options.Get;
            set => _translator = value;
        }

        public string Label {
            get => _label ?? $"{GetType()}:{FieldName}";
            set {
                _label = value;
                UpdateLabel();
            }
        }

        public string Tooltip {
            get => _tooltip;
            set {
                _tooltip = value;
                UpdateTooltip();
            }
        }

        public bool ReadOnly {
            get => _readOnly;
            set {
                _readOnly = !IsInScope || value;
                UpdateReadOnly();
            }
        }

        public abstract void ResetValue();

        public abstract void Load(byte data);

        public abstract byte Save();

        protected abstract void UpdateTooltip();

        protected abstract void UpdateReadOnly();

        protected abstract void UpdateLabel();

        /// <summary>Terse shortcut for <c>Translator(key)</c>.</summary>
        /// <param name="key">The locale key to translate.</param>
        /// <returns>Returns localised string for <paramref name="key"/>.</returns>
        protected string Translate(string key) => Translator(key);
    }

    public abstract class SerializableUIOptionBase<TVal, TUI, TComponent> : SerializableUIOptionBase
        where TUI : UIComponent
    {
        public delegate TVal ValidatorDelegate(TVal desired, out TVal result);

        public delegate void OnChanged(TVal value);

        protected TUI _ui;

        // used as internal store of value if _fieldInfo is null
        private TVal _value = default;

        protected TVal _defaultValue = default;

        public event OnChanged OnValueChanged;

        public SerializableUIOptionBase(string fieldName, Options.PersistTo scope)
            : base(fieldName, scope) {
            OnValueChanged = DefaultOnValueChanged;
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
                if(_fieldInfo == null) {
                    return _value;
                }

                var value = _fieldInfo.GetValue(null);
                if(value is IConvertible convertibleValue) {
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
                    _fieldInfo.SetValue(null, val);
                } else {
                    _fieldInfo.SetValue(null, value);

                }
            }
        }

        /// <summary>set only during initialization</summary>
        public TVal DefaultValue {
            get => _defaultValue;
            set => _value = _defaultValue = value;
        }

        public static implicit operator TVal(SerializableUIOptionBase<TVal, TUI, TComponent> a) => a.Value;

        public bool HasUI => _ui != null;

        /// <summary>type safe version of <c>Convert.ChangeType()</c>.</summary>
        private static IConvertible ChangeType(IConvertible value, Type type) => Convert.ChangeType(value, type) as IConvertible;

        public void DefaultOnValueChanged(TVal newVal) {
            if (Value.Equals(newVal)) {
                return;
            }
            Log._Debug($"SerializableUIOptionBase.DefaultOnValueChanged: `{FieldName}` changed to {newVal}");
            Value = newVal;
        }

        public override void ResetValue() => Value = DefaultValue;

        public void InvokeOnValueChanged(TVal value) => OnValueChanged?.Invoke(value);

        public abstract TComponent AddUI(UIHelperBase container);
    }
}