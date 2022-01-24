namespace TrafficManager.UI.Helpers {
    using ICities;
    using ColossalFramework.UI;
    using State;

    public class CheckboxOption : SerializableUIOptionBase<bool, UICheckBox> {
        public CheckboxOption(string fieldName, Options.PersistTo scope = Options.PersistTo.Savegame)
        : base(fieldName, scope) {
            OnValueChanged = DefaultOnValueChanged;
        }

        public event OnCheckChanged OnValueChanged;

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

        public override bool ReadOnlyUI {
            get => base.ReadOnlyUI;
            set {
                base.ReadOnlyUI = !IsInScope || value;
                if (_ui != null) {
                    _ui.readOnly = base.ReadOnlyUI;
                    _ui.opacity = base.ReadOnlyUI ? 0.3f : 1f;
                }
            }
        }

        public override void Load(byte data) => Value = data != 0;
        public override byte Save() => Value ? (byte)1 : (byte)0;

        public override void AddUI(UIHelperBase container) {
            _ui = container.AddCheckbox(
                T(Label),
                Value,
                this.OnValueChanged) as UICheckBox;
            if (Indent) {
                Options.Indent(_ui);
            }
            Options.AllowTextWrap(_ui, Indent);
            if (!IsInScope) {
                ReadOnlyUI = true;
                _ui.tooltip = "This setting can only be changed in-game.";
            } else if (Tooltip != null) {
                _ui.tooltip = T(Tooltip);
                if (base.ReadOnlyUI) {
                    ReadOnlyUI = true;
                }
            }
        }

    }
}
