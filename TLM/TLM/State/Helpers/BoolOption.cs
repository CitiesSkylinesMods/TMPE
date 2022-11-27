namespace TrafficManager.State.Helpers {
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Xml;

    public class BoolOption : PropagatorOptionBase<bool> {
        public BoolOption(string name, Scope scope) : base(name, scope) { }

        public new BoolOption PropagateTrueTo([NotNull] IValuePropagator target) {
            base.PropagateTrueTo(target);
            return this;
        }

        public override void Propagate(bool value) => Value = value;

        protected override void OnPropagateAll(bool val) => PropagateAll(val);

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
