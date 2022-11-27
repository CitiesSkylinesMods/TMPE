namespace TrafficManager.State.Helpers {
    using System;
    using System.Xml;
    using CSUtil.Commons;
    using JetBrains.Annotations;

    public class TernaryOption : PropagatorOptionBase<bool?> {
        public TernaryOption(string name, Scope scope) : base(name, scope) { }

        public new TernaryOption PropagateTrueTo([NotNull] IValuePropagator target) {
            base.PropagateTrueTo(target);
            return this;
        }

        public override void Propagate(bool value) {
            if (!value) {
                // this tristate button depends on another option that has been disabled.
                Value = null;
            } else {
                // Don't know to set to true or false because both are none-null.
                // I don't think it makes sense for other options to depend on tristate checkbox anyway.
                throw new NotImplementedException("other options cannot depend on tristate checkbox");
            }
        }

        protected override void OnPropagateAll(bool? val) => PropagateAll(val.HasValue);

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
