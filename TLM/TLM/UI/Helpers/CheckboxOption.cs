namespace TrafficManager.UI.Helpers {
    using ICities;
    using ColossalFramework.UI;
    using State;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using JetBrains.Annotations;

    public class CheckboxOption : SerializableUIOptionBase<bool, UICheckBox> {
        [CanBeNull]
        private List<CheckboxOption> _requires;

        public CheckboxOption(string fieldName, Options.PersistTo scope = Options.PersistTo.Savegame)
        : base(fieldName, scope) {
            OnValueChanged = DefaultOnValueChanged;
        }

        /* Data */

        public event OnCheckChanged OnValueChanged;
        public OnCheckChanged Handler {
            set => OnValueChanged += value;
        }

        /// <summary>Optional list of checkboxes wich must be checked for this option to work.</summary>
        /// <remarks>If specified, setting this option <c>true</c> will also set those other checkboxes <c>true</c>.</remarks>
        [CanBeNull]
        public List<CheckboxOption> Requires {
            get => _requires;
            set {
                _requires = value;
                Log.Info($"CheckboxOption.Requires: {nameof(Options)}.{FieldName} marked as requiring:");

                foreach (var requirement in _requires) {
                    Log.Info($"- {nameof(Options)}.{requirement.FieldName}");

                    if (requirement.Dependents == null) {
                        requirement.Dependents = new();
                    }

                    requirement.Dependents.Add(this);
                }
            }
        }

        /// <summary>Opional list of options that are depending on this option.</summary>
        /// <remarks>Don't set directly; it's automatically managed by <see cref="Requires"/> property.</remarks>
        [CanBeNull]
        public List<CheckboxOption> Dependents { get; set; }

        public override void Load(byte data) {
            Log.Info($"CheckboxOption.Load: {data} -> {data != 0} -> {nameof(Options)}.{FieldName}");
            Value = data != 0;
        }
        public override byte Save() {
            Log.Info($"CheckboxOption.Save: {nameof(Options)}.{FieldName} -> {Value} -> {(Value ? (byte)1 : (byte)0)}");
            return Value ? (byte)1 : (byte)0;
        }

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
                Log.Info($"CheckboxOption.Value: {nameof(Options)}.{FieldName} changed to {value}");

                // auto-enable requirements if applicable
                if (value && _requires != null) {
                    foreach (var requirement in _requires) {
                        requirement.Value = true;
                    }
                }

                // auto-disable dependents if applicable
                if (!value && Dependents != null) {
                    foreach (var dependent in Dependents) {
                        dependent.Value = false;
                    }
                }

                base.Value = value;
                if (HasUI) {
                    _ui.isChecked = value;
                }
            }
        }

        public bool ReadOnlyUI {
            get => _readOnlyUI;
            set {
                // _readOnlyUI = !IsInScope || value;
                if (HasUI) {
                    _ui.readOnly = _readOnlyUI;
                    _ui.opacity = _readOnlyUI ? 0.3f : 1f;
                }
            }
        }

        public bool Indent { get; set; }

        public override void AddUI(UIHelperBase container) {
            _ui = container.AddCheckbox(T(Label), Value, OnValueChanged) as UICheckBox;
            if (HasUI && Indent) {
                Options.Indent(_ui);
            }
            Options.AllowTextWrap(_ui, Indent);
            Tooltip = _tooltip;
            ReadOnlyUI = _readOnlyUI;
        }

    }
}
