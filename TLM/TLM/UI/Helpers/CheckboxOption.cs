namespace TrafficManager.UI.Helpers {
    using ICities;
    using ColossalFramework.UI;
    using State;

    public class CheckboxOption : SerializableUIOptionBase<bool, UICheckBox> {
        public CheckboxOption(string fieldName, bool isGlobalOption = false)
        : base(fieldName, isGlobalOption) {
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

        public override bool Enabled {
            get => base.Enabled;
            set {
                base.Enabled = value;
                if (_ui != null) {
                    _ui.readOnly = !value;
                    _ui.opacity = value ? 1f : 0.3f;
                }
            }
        }

        public override void Load(byte data) => Value = data != 0;
        public override byte Save() => Value ? (byte)1 : (byte)0;

        public override void AddUI(UIHelperBase container) {
            bool unalterable = !GlobalOption && !Options.IsGameLoaded(false);
            _ui = container.AddCheckbox(
                T(Label),
                Value,
                this.OnValueChanged) as UICheckBox;
            if (Indent) {
                Options.Indent(_ui);
            }
            Options.AllowTextWrap(_ui, Indent);
            if (unalterable) {
                Enabled = false;
                _ui.tooltip = "This setting can only be changed in-game.";
            } else if (Tooltip != null) {
                _ui.tooltip = T(Tooltip);
                if (!base.Enabled) {
                    Enabled = false;
                }
            }
        }

    }
}
