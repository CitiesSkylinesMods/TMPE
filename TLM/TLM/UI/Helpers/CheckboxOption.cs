namespace TrafficManager.UI.Helpers {
    using ICities;
    using ColossalFramework.UI;

    public sealed class CheckboxOption : SerializableUIOptionBase<bool, UICheckBox> {
        public event ICities.OnCheckChanged OnValueChanged;
        public CheckboxOption(
            string key,
            bool default_value,
            string group_name,
            bool tooltip = false)
            : base(key, default_value, group_name, tooltip) {
            OnValueChanged = DefaultOnValueChanged;
        }

        public override void Load(byte data) => SetValue(data != 0);
        public override byte Save() => Value ? (byte)1 : (byte)0;
        public override void SetValue(bool newVal) {
            if (_ui != null) {
                _ui.isChecked = newVal;
            }
            _value = newVal;
        }

        public override void AddUI(UIHelperBase container) {
            _ui = container.AddCheckbox(
                Translation.Options.Get(Label),
                DefaultValue,
                this.OnValueChanged) as UICheckBox;
            if (_tooltip) {
                _ui.tooltip = Translation.Options.Get(Tooltip);
            }
        }
    }
}
