namespace TrafficManager.UI.Helpers {
    using ColossalFramework.UI;
    using ColossalFramework;
    using CSUtil.Commons;
    using ICities;
    using System;
    using TrafficManager.State;
    using TrafficManager.Lifecycle;
    using System.Runtime.CompilerServices;

    public abstract class SerializableOptionBase : ILegacySerializableOption {
        /// <summary>Use as tooltip for readonly UI components.</summary>
        public delegate string TranslatorDelegate(string key);

        protected const string INGAME_ONLY_SETTING = "This setting can only be changed in-game.";

        public Options.Scope Scope { get; private set; }

        public SerializableOptionBase(Options.Scope scope) {
            Scope = scope;
        }

        public abstract void ResetValue();

        public abstract void Load(byte data);

        public abstract byte Save();
    }

    public abstract class SerializableOptionBase<TVal> : SerializableOptionBase {
        public delegate TVal ValidatorDelegate(TVal desired, out TVal result);

        public delegate void OnChanged(TVal value);

        // used as internal store of value if _fieldInfo is null
        private TVal _value = default;

        protected TVal _defaultValue = default;

        public event OnChanged OnValueChanged;

        public SerializableOptionBase(Options.Scope scope)
            : base(scope) {
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

        public virtual TVal Value {
            get => _value;
            set => _value = value;
        }

        /// <summary>set only during initialization</summary>
        public TVal DefaultValue {
            get => _defaultValue;
            set => _value = _defaultValue = value;
        }

        public TVal FastValue {
            [MethodImpl(256)]
            get => _value;
        }

        [MethodImpl(256)]

        public static implicit operator TVal(SerializableOptionBase<TVal> a) => a._value;

        public void DefaultOnValueChanged(TVal newVal) {
            if (Value.Equals(newVal)) {
                return;
            }
            Log._Debug($"SerializableOptionBase.DefaultOnValueChanged: value changed to {newVal}");
            Value = newVal;
        }

        public override void ResetValue() => Value = DefaultValue;

        public void InvokeOnValueChanged(TVal value) => OnValueChanged?.Invoke(value);
    }
}