namespace TrafficManager.State.Helpers {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System.Security.Policy;
    using System.Xml;
    using TrafficManager.State.ConfigData;
    using UnityEngine;

    public class FloatOption : SerializableOptionBase<float> {
        public override void Load(byte data) => Value = data;
        public override byte Save() => FloatToByte(Value);

        private byte _min = 0;
        private byte _max = 100;

        public byte Min {
            get => _min;
            set {
                if (_min == value) return;
                _min = value;
                Value = Value;
            }
        }

        public byte Max {
            get => _max;
            set {
                if (_max == value) return;
                _max = value;
                Value = Value;
            }
        }

        public override float Value {
            get => base.Value;
            set => base.Value = Mathf.Clamp(value, Min, Max);
        }

        public byte FloatToByte(float val) =>
            (byte)Mathf.RoundToInt(Mathf.Clamp(val, Min, Max).Quantize(Step));

        public override void ReadXml(XmlReader reader) {
            string strVal = reader.ReadString();
            if (float.TryParse(strVal, out float value)) {
                Value = value;
            } else {
                Log.Error("unknown value:" + strVal);
                ResetValue();
            }
        }
    }
}
