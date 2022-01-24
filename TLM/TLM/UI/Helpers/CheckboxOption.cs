namespace TrafficManager.UI.Helpers {
    using ICities;
    using ColossalFramework.UI;
    using State;

    public class CheckboxOption : SerializableUIOptionBase<bool, UICheckBox> {
        public CheckboxOption(string fieldName, Options.PersistTo scope = Options.PersistTo.Savegame)
        : base(fieldName, scope) {
            OnValueChanged = DefaultOnValueChanged;
        }

        /* Data */

        public event OnCheckChanged OnValueChanged;
        public OnCheckChanged Handler {
            set => OnValueChanged += value;
        }
        public override void Load(byte data) => Value = data != 0;
        public override byte Save() => Value ? (byte)1 : (byte)0;

        /* UI */

        public string Label {
            get => _label ?? FieldName;
            set {
                _label = value;
                if (HasUI) {
                    _ui.label.text = string.IsNullOrEmpty(value)
                        ? string.Empty // avoid invalidating UI if already no label
                        : T(value);
                }
            }
        }

        public string Tooltip {
            get => _tooltip;
            set {
                _tooltip = value;
                if (HasUI) {
                    _ui.tooltip = IsInScope
                        ? string.IsNullOrEmpty(value)
                            ? string.Empty // avoid invalidating UI if already no tooltip
                            : T(value)
                        : T(INGAME_ONLY_SETTING);
                }
            }
        }

        public override bool Value {
            get => base.Value;
            set {
                base.Value = value;
                if (HasUI) {
                    _ui.isChecked = value;
                }
            }
        }

        public bool ReadOnlyUI {
            get => _readOnlyUI;
            set {
                _readOnlyUI = !IsInScope || value;
                if (HasUI) {
                    _ui.readOnly = _readOnlyUI;
                    _ui.opacity = _readOnlyUI ? 0.3f : 1f;
                }
            }
        }

        public bool Indent {
            get => _indent;
            set {
                _indent = value;
                if (HasUI && _indent) {
                    Options.Indent(_ui);
                }
            }
        }

        public override void AddUI(UIHelperBase container) {
            _ui = container.AddCheckbox(T(Label), Value, OnValueChanged) as UICheckBox;
            Indent = _indent;
            Options.AllowTextWrap(_ui, Indent);
            Tooltip = _tooltip;
            ReadOnlyUI = _readOnlyUI;
        }

    }
}
