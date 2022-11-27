namespace TrafficManager.State.Helpers; 
using System;
using System.Xml;
using TrafficManager.Util;

public class EnumOption<TEnum> : SerializableOptionBase<TEnum>
    where TEnum : Enum {
    public EnumOption(Scope scope) : base(scope) { }

    public override byte Save() => ChangeType<byte>(Value);

    public override void Load(byte data) => Value = ChangeType<TEnum>(Value);

    private static TResult ChangeType<TResult>(IConvertible value)
        where TResult : IConvertible {
        return (TResult)Convert.ChangeType(value, typeof(TResult));
    }

    public override void ReadXml(XmlReader reader) {
        string strVal = reader.ReadString();
        try {
            Enum.Parse(typeof(TEnum), strVal, ignoreCase: true);
        } catch(Exception ex) {
            ex.LogException();
        }
    }
}
