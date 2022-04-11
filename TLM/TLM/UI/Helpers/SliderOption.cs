namespace TrafficManager.UI.Helpers {
    using ICities;
    using ColossalFramework.UI;
    using TrafficManager.State;
    using CSUtil.Commons;
    using UnityEngine;
    using System;

    public class SliderOption : SerializableUIOptionBase<float, UISlider, SliderOption> {

        private const int SLIDER_LABEL_MAX_WIDTH = 695;

        private byte _min = 0;
        private byte _max = 255;
        private byte _step = 5;
        private UILabel _sliderLabel;

        public SliderOption(string fieldName, Options.PersistTo scope = Options.PersistTo.Savegame)
        : base(fieldName, scope) { }

        /* Data */

        public byte Min {
            get => _min;
            set {
                if (_min == value) return;

                _min = value;
                if (HasUI) _ui.minValue = _min;
            }
        }

        public byte Max {
            get => _max;
            set {
                if (_max == value) return;

                _max = value;
                if (HasUI) _ui.maxValue = _max;
            }
        }

        public byte Step {
            get => _step;
            set {
                if (_step == value) return;

                _step = value;
                if (HasUI) _ui.stepSize = value;
            }
        }

        public byte FloatToByte(float val)
            => (byte)Mathf.RoundToInt(Mathf.Clamp(val, Min, Max).Quantize(Step));

        public override void Load(byte data) => Value = data;

        public override byte Save() => FloatToByte(Value);

        /* UI */
        public override float Value {
            get => base.Value;
            set {
                value = FloatToByte(value);

                if (Mathf.Approximately(value, base.Value)) return;

                base.Value = value;

                Log.Info($"SliderOption.Value: `{FieldName}` changed to {value}");

                if (HasUI) {
                    _ui.value = value;
                    UpdateTooltip();
                }
            }
        }

        public override SliderOption AddUI(UIHelperBase container) {
            _ui = container.AddSlider(
                text: Translate(Label) + ":",
                min: Min,
                max: Max,
                step: Step,
                defaultValue: Value,
                eventCallback: InvokeOnValueChanged) as UISlider;

            _sliderLabel = _ui.parent.Find<UILabel>("Label");
            _sliderLabel.width = SLIDER_LABEL_MAX_WIDTH;

            UpdateTooltip();
            UpdateReadOnly();

            return this;
        }

        protected override void UpdateLabel() {
            if (!HasUI) return;

            _sliderLabel.text = Translate(Label);
        }

        protected override void UpdateTooltip() {
            if (!HasUI) return;

            _ui.tooltip = IsInScope
                ? $"{Value}{_tooltip}"
                : Translate(INGAME_ONLY_SETTING);

            if (_ui.thumbObject.hasFocus) {
                try { _ui.RefreshTooltip(); }
                catch (Exception _) { }
            }
        }

        protected override void UpdateReadOnly() {
            if (!HasUI) return;

            var readOnly = !IsInScope || _readOnly;

            Log._Debug($"SliderOption.UpdateReadOnly() - `{FieldName}` is {(readOnly ? "read-only" : "writeable")}");

            _ui.thumbObject.isInteractive = !readOnly;
            _ui.thumbObject.opacity = readOnly ? 0.3f : 1f;
        }
    }
}