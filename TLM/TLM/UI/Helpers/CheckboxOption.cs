namespace TrafficManager.UI.Helpers {
    using ICities;
    using ColossalFramework.UI;

    public class CheckboxOption : SerializableUIOptionBase<bool, UICheckBox> {
        public CheckboxOption(string fieldName) : base(fieldName) {
            OnValueChanged = DefaultOnValueChanged;
        }

        public event ICities.OnCheckChanged OnValueChanged;

        public OnCheckChanged Handler {
            set => OnValueChanged += value;
        }

        public override bool Value {
            get => base.Value;
            set {
                base.Value = value;
                if (_ui != null) {
                    _ui.isChecked = value;
                }
            }
        }

        public override void Load(byte data) => Value = (data != 0);
        public override byte Save() => Value ? (byte)1 : (byte)0;

        public override void AddUI(UIHelperBase container) {
            string T(string key) => Translation.Options.Get(key);
            _ui = container.AddCheckbox(
                T(Label),
                Value,
                this.OnValueChanged) as UICheckBox;
            if (Tooltip != null) {
                _ui.tooltip = T(Tooltip);
            }
            if (Indent) {
                State.Options.Indent(_ui);
            }
        }
    }
}
