namespace TrafficManager.State.Helpers; 
using CSUtil.Commons;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using System.Xml.Schema;
using System.Xml;

public abstract class SerializableOptionBase : ILegacySerializableOption, IXmlSerializable {
    /// <summary>Use as tooltip for readonly UI components.</summary>
    public delegate string TranslatorDelegate(string key);

    protected const string INGAME_ONLY_SETTING = "This setting can only be changed in-game.";

    public string Name { get; set; }

    public abstract void ResetValue();

    public abstract void Load(byte data);
    public abstract byte Save();
    public XmlSchema GetSchema() => null;
    public abstract void ReadXml(XmlReader reader);
    public abstract void WriteXml(XmlWriter writer);
}

public abstract class SerializableOptionBase<TVal> : SerializableOptionBase {
    public delegate bool ValidatorDelegate(TVal desired, out TVal result);

    public delegate void OnChanged(TVal value);

    // used as internal store of value if _fieldInfo is null
    private TVal _value = default;

    protected TVal _defaultValue = default;

    public event OnChanged OnValueChanged;

    public SerializableOptionBase() {
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
        set {
            if (Validator != null) {
                if (Validator(value, out TVal result)) {
                    value = result;
                } else {
                    Log.Info($"`{Name}`: validator rejected value: {value}");
                    return;
                }
            }

            if (!_value.Equals(value)) {
                _value = value;
                Log.Info($"`{Name}`: value changed to {value}");
                OnValueChanged(value);
            }
        }
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
        Log._Debug($"SerializableOptionBase.DefaultOnValueChanged: {Name} value changed to {newVal}");
        Value = newVal;
    }

    public override void ResetValue() => Value = DefaultValue;

    public void InvokeOnValueChanged(TVal value) => OnValueChanged?.Invoke(value);
    public override void WriteXml(XmlWriter writer) => writer.WriteString(Value.ToString());

}