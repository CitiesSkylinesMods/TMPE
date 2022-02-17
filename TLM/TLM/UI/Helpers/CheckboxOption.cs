namespace TrafficManager.UI.Helpers {
    using ICities;
    using ColossalFramework.UI;
    using TrafficManager.State;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using JetBrains.Annotations;

    public class CheckboxOption : SerializableUIOptionBase<bool, UICheckBox> {
        [CanBeNull]
        private List<CheckboxOption> _propagatesTrueTo;

        public CheckboxOption(string fieldName, Options.PersistTo scope = Options.PersistTo.Savegame)
        : base(fieldName, scope) {
            OnValueChanged = DefaultOnValueChanged;
        }

        /* Data */

        public delegate bool ValidatorDelegate(bool desired, out bool result);

        public event OnCheckChanged OnValueChanged;

        public OnCheckChanged Handler {
            set => OnValueChanged += value;
        }

        /// <summary>
        /// Optional custom validator which intercepts value changes and can inhibit event propagation.
        /// </summary>
        public ValidatorDelegate Validator { get; set; }

        /// <summary>
        /// Optional: If specified, when <c>Value</c> is set <c>true</c> it will propagate that to listed checkboxes.
        /// </summary>
        [CanBeNull]
        public List<CheckboxOption> PropagatesTrueTo {
            get => _propagatesTrueTo;
            set {
                _propagatesTrueTo = value;
                Log.Info($"CheckboxOption.PropagatesTrueTo: `{FieldName}` will proagate to:");

                foreach (var target in _propagatesTrueTo) {
                    Log.Info($"- `{target.FieldName}`");

                    if (target.PropagatesFalseTo == null) {
                        target.PropagatesFalseTo = new ();
                    }

                    target.PropagatesFalseTo.Add(this);
                }
            }
        }

        /// <summary>
        /// An alternate way to add targets to the <see cref="_propagatesTrueTo"/> list.
        /// Useful for keeping code concise when there's only single target (most common use case).
        /// </summary>
        /// <param name="target">The checkox to propagate <c>true</c> value to.</param>
        public void PropagateTrueTo([NotNull] CheckboxOption target) {
            Log.Info($"CheckboxOption.PropagateTrueTo: `{FieldName}` will proagate to `{target.FieldName}`");
            if (_propagatesTrueTo != null) {
                _propagatesTrueTo.Add(target);
            } else {
                _propagatesTrueTo = new() { { target } };
            }
        }

        /// <summary>
        /// Optional: If specified, when <c>Value</c> is set <c>false</c> it will propagate that to listed checkboxes.
        /// </summary>
        /// <remarks>Don't need to set directly; it's automatically managed by <see cref="PropagatesTrueTo"/> property.</remarks>
        [CanBeNull]
        public List<CheckboxOption> PropagatesFalseTo { get; set; }

        public override void Load(byte data) {
            Log.Info($"CheckboxOption.Load({data}): `{FieldName}` = {data != 0}");
            Value = data != 0;
        }

        public override byte Save() {
            Log.Info($"CheckboxOption.Save(): `{FieldName}` is {Value} -> {(Value ? (byte)1 : (byte)0)}");
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
                if (Validator != null) {
                    if (Validator(value, out bool result)) {
                        value = result;
                    } else {
                        Log.Info($"CheckboxOption.Value: `{FieldName}` validator rejected value: {value}");
                        return;
                    }
                }

                Log.Info($"CheckboxOption.Value: `{FieldName}` changed to {value}");

                if (value && _propagatesTrueTo != null) {
                    foreach (var target in _propagatesTrueTo) {
                        target.Value = true;
                    }
                }

                if (!value && PropagatesFalseTo != null) {
                    foreach (var target in PropagatesFalseTo) {
                        target.Value = false;
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
                _readOnlyUI = !IsInScope || value;
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
