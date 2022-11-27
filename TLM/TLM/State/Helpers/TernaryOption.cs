namespace TrafficManager.State.Helpers {
    using System.Xml;
    using CSUtil.Commons;

    public class TernaryOption : SerializableOptionBase<bool?>, IValuePropagator {
        public TernaryOption(Options.Scope scope) : base(scope) { }

        public override byte Save() => (byte)TernaryBoolUtil.ToTernaryBool(Value);
        public override void Load(byte data) => Value = TernaryBoolUtil.ToOptBool((TernaryBool)data);

        public override void WriteXml(XmlWriter writer) {
            writer.WriteValue(Value?.ToString() ?? "Null");
        }

        public override void ReadXml(XmlReader reader) {
            string strVal = reader.ReadString();
            switch (strVal.ToLower()) {
                case "true":
                    Value = true;
                    return;
                case "false":
                    Value = false;
                    return;
                case "null" or "":
                    Value = null;
                    return;
                default:
                    Log.Error("unknown value:" + strVal);
                    ResetValue();
                    return;
            }
        }
    }
}
