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

    public abstract class SerializableUIOptionBase<TVal, TUI, TComponent> : ILegacySerializableOption
        where TUI : UIComponent {

        /// <summary>Use as tooltip for readonly UI components.</summary>
        protected const string INGAME_ONLY_SETTING = "This setting can only be changed in-game.";

        /* Data: */
        public delegate TVal ValidatorDelegate(TVal desired, out TVal result);

        public delegate void OnChanged(TVal value);

        public event OnChanged OnValueChanged;

        public void InvokeOnValueChanged(TVal value) => OnValueChanged?.Invoke(value);

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

        protected Options.PersistTo _scope;

        [CanBeNull]
        private FieldInfo _fieldInfo;

        private string _fieldName;

        // used as internal store of value if _fieldInfo is null
        private TVal _value = default;

        public SerializableUIOptionBase(string fieldName, Options.PersistTo scope) {

            _fieldName = fieldName;
            _scope = scope;

            if (scope.IsFlagSet(Options.PersistTo.Savegame)) {
                _fieldInfo = typeof(Options).GetField(fieldName, BindingFlags.Static | BindingFlags.Public);

                if (_fieldInfo == null) {
                    throw new Exception($"SerializableUIOptionBase.ctor: `{fieldName}` does not exist");
                }
            }

            OnValueChanged = DefaultOnValueChanged;
        }

        /// <summary>Gets or sets the value of the field this option represents.</summary>
        public virtual TVal Value {
            get => _fieldInfo != null ? (TVal)_fieldInfo.GetValue(null) : _value;
            set {
                if (_fieldInfo != null) {
                    _fieldInfo.SetValue(null, value);
                } else {
                    _value = value;
                }
            }
        }

        public string FieldName => _fieldInfo?.Name ?? _fieldName;

        /// <summary>Returns <c>true</c> if setting can persist in current <see cref="_scope"/>.</summary>
        /// <remarks>
        /// When <c>false</c>, UI component should be <see cref="_readOnly"/>
        /// and <see cref="_tooltip"/> should be set to <see cref="INGAME_ONLY_SETTING"/>.
        /// </remarks>
        protected bool IsInScope =>
            _scope.IsFlagSet(Options.PersistTo.Global) ||
            (_scope.IsFlagSet(Options.PersistTo.Savegame) && TMPELifecycle.AppMode != null) ||
            _scope == Options.PersistTo.None;

        public static implicit operator TVal(SerializableUIOptionBase<TVal, TUI, TComponent> a) => a.Value;

        public void DefaultOnValueChanged(TVal newVal) {
            if (Value.Equals(newVal)) {
                return;
            }
            Log._Debug($"SerializableUIOptionBase.DefaultOnValueChanged: `{FieldName}` changed to {newVal}");
            Value = newVal;
        }

        public abstract void Load(byte data);
        public abstract byte Save();

        /* UI: */

        public bool HasUI => _ui != null;
        protected TUI _ui;

        protected string _label;
        protected string _tooltip;

        protected bool _readOnly;

        private TranslatorDelegate _translator;
        public delegate string TranslatorDelegate(string key);

        public TranslatorDelegate Translator {
            get => _translator ?? Translation.Options.Get;
            set => _translator = value;
        }

        public abstract TComponent AddUI(UIHelperBase container);

        /// <summary>Terse shortcut for <c>Translator(key)</c>.</summary>
        /// <param name="key">The locale key to translate.</param>
        /// <returns>Returns localised string for <paramref name="key"/>.</returns>
        protected string Translate(string key) => Translator(key);

        protected abstract void UpdateTooltip();

        protected abstract void UpdateReadOnly();

        protected abstract void UpdateLabel();

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

        public bool Indent { get; set; }
    }
}