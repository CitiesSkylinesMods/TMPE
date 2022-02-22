namespace TrafficManager.UI.Helpers {
    using ICities;
    using ColossalFramework.UI;
    using TrafficManager.State;
    using CSUtil.Commons;
    using UnityEngine;

    public class SliderOption : SerializableUIOptionBase<float, UISlider, SliderOption> {

        private float _min = 0;
        private float _max = 255;
        private float _step = 5;
        private UILabel _sliderLabel;

        public SliderOption(string fieldName, Options.PersistTo scope = Options.PersistTo.Savegame)
        : base(fieldName, scope) {
            OnValueChanged = DefaultOnValueChanged;
        }

        /* Data */

        public delegate bool SliderValidatorDelegate(float desired, out float result);

        public event OnValueChanged OnValueChanged;

        public OnValueChanged Handler {
            set {
                OnValueChanged -= value;
                OnValueChanged += value;
            }
        }

        /// <summary>
        /// Optional custom validator which intercepts value changes and can inhibit event propagation.
        /// </summary>
        public SliderValidatorDelegate Validator { get; set; }

        public float Min {
            get => _min;
            set {
                if (_min == value) return;
                _min = Mathf.Clamp(value, 0, 255);
                if (HasUI) _ui.minValue = _min;
            }
        }

        public float Max {
            get => _max;
            set {
                if (_max == value) return;
                _max = Mathf.Clamp(value, 0, 255);
                if (HasUI) _ui.maxValue = _max;
            }
        }

        public float Step {
            get => _step;
            set {
                if (_step == value) return;
                _step = value;
                if (HasUI) _ui.stepSize = value;
            }
        }

        public override void Load(byte data) => Value = data;

        public override byte Save() => (byte)Mathf.RoundToInt(Value);

        /* UI */

        public string Label {
            get => _label ?? $"Slider:{FieldName}";
            set {
                _label = value;
                UpdateLabel();
            }
        }

        public string Tooltip {
            get => _tooltip;
            set {
                _tooltip = value;
                UpdateTooltip();
            }
        }

        public override float Value {
            get => base.Value;
            set {
                value = Mathf.Clamp(value, Min, Max);

                if (Validator != null) {
                    if (Validator(value, out float result)) {
                        value = result;
                    } else {
                        Log.Info($"SliderOption.Value: `{FieldName}` validator rejected value: {value}");
                        return;
                    }
                }

                if (value == base.Value) return;

                base.Value = value;

                Log.Info($"SliderOption.Value: `{FieldName}` changed to {value}");

                if (HasUI) _ui.value = value;
            }
        }

        public bool ReadOnly {
            get => _readOnly;
            set {
                _readOnly = !IsInScope || value;
                UpdateReadOnly();
            }
        }

        public override SliderOption AddUI(UIHelperBase container) {
            _ui = container.AddSlider(
                text: T(Label) + ":",
                min: Min,
                max: Max,
                step: Step,
                defaultValue: Mathf.Clamp(Value, Min, Max),
                eventCallback: OnValueChanged) as UISlider;

            _sliderLabel = _ui.parent.Find<UILabel>("Label");
            _sliderLabel.width = 500;

            UpdateTooltip();
            UpdateReadOnly();

            return this;
        }

        private void UpdateLabel() {
            if (!HasUI) return;

            _sliderLabel.text = T(Label);
        }

        private void UpdateTooltip() {
            if (!HasUI) return;

            _ui.tooltip = IsInScope
                ? string.IsNullOrEmpty(_tooltip)
                    ? string.Empty // avoid invalidating UI if already no tooltip
                    : T(_tooltip)
                : T(INGAME_ONLY_SETTING);

            _ui.RefreshTooltip();
        }

        private void UpdateReadOnly() {
            if (!HasUI) return;

            var readOnly = !IsInScope || _readOnly;

            Log._Debug($"SliderOption.UpdateReadOnly() - `{FieldName}` is {(readOnly ? "read-only" : "writeable")}");

            _ui.isInteractive = !readOnly;
            _ui.opacity = readOnly ? 0.3f : 1f;
        }
    }
}