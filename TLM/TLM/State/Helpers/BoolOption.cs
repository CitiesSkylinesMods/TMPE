namespace TrafficManager.State.Helpers {
    using CSUtil.Commons;
    using System.Xml;

    public class BoolOption : SerializableOptionBase<bool>, IValuePropagator {
        public BoolOption(Options.Scope scope) : base(scope) { }

        public override void Load(byte data) => Value = data != 0;
        public override byte Save() => Value ? (byte)1 : (byte)0;

        public override void ReadXml(XmlReader reader) {
            string strVal = reader.ReadString();
            if (bool.TryParse(strVal, out bool value)) {
                Value = value;
            } else {
                Log.Error("unknown value:" + strVal);
                ResetValue();
            }
        }
    }
}
