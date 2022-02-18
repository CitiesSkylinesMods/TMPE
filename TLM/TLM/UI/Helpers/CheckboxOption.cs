namespace TrafficManager.UI.Helpers {
    using ICities;
    using ColossalFramework.UI;
    using TrafficManager.State;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using JetBrains.Annotations;
    using UnityEngine;

    public class CheckboxOption : SerializableUIOptionBase<bool, UICheckBox> {
        private const int CHECKBOX_LABEL_MAX_WIDTH = 695;
        private const int CHECKBOX_LABEL_MAX_WIDTH_INDENTED = 680;

        private List<CheckboxOption> _propagatesTrueTo;
        private List<CheckboxOption> _propagatesFalseTo;

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
        /// If this checkbox is set <c>true</c>, it will propagate that to the <paramref name="target"/>.
        /// Chainable.
        /// </summary>
        /// <param name="target">The checkox to propagate <c>true</c> value to.</param>
        /// <remarks>
        /// If target is set <c>false</c>, it will proapagate that back to this checkbox.
        /// </remarks>
        public CheckboxOption PropagateTrueTo([NotNull] CheckboxOption target) {
            Log.Info($"CheckboxOption.PropagateTrueTo: `{FieldName}` will proagate to `{target.FieldName}`");

            if (_propagatesTrueTo == null) _propagatesTrueTo = new();
            _propagatesTrueTo.Add(target);

            if (target._propagatesFalseTo == null) target._propagatesFalseTo = new();
            target._propagatesFalseTo.Add(this);

            return this;
        }

        public override void Load(byte data) => Value = data != 0;

        public override byte Save() => (byte)(Value ? 1 : 0);

        /* UI */

        public string Label {
            get => _label ?? $"Checkbox:{FieldName}";
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

        public bool Indent { get; set; }

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

                if (value == base.Value)
                    return;

                base.Value = value;

                Log.Info($"CheckboxOption.Value: `{FieldName}` changed to {value}");

                if (value && _propagatesTrueTo != null)
                    PropagateTo(_propagatesTrueTo, true);

                if (!value && _propagatesFalseTo != null)
                    PropagateTo(_propagatesFalseTo, false);

                if (HasUI) _ui.isChecked = value;
            }
        }

        public bool ReadOnly {
            get => _readOnly;
            set {
                _readOnly = !IsInScope || value;
                UpdateReadOnly();
            }
        }

        public override void AddUI(UIHelperBase container) {
            _ui = container.AddCheckbox(T(Label), Value, OnValueChanged) as UICheckBox;

            if (Indent) IndentUI(_ui);

            AllowTextWrap(_ui, Indent);

            UpdateTooltip();
            UpdateReadOnly();
        }

        private void PropagateTo(IList<CheckboxOption> targets, bool value) {
            foreach (var target in targets)
                target.Value = value;
        }

        private void UpdateLabel() {
            if (!HasUI) return;

            _ui.label.text = T(Label);
        }

        private void UpdateTooltip() {
            if (!HasUI) return;

            _ui.tooltip = IsInScope
                ? string.IsNullOrEmpty(_tooltip)
                    ? string.Empty // avoid invalidating UI if already no tooltip
                    : T(_tooltip)
                : T(INGAME_ONLY_SETTING);
        }

        private void UpdateReadOnly() {
            if (!HasUI) return;

            Log.Info($"CheckboxOption.Value: `{FieldName}` is readonly");

            var readOnly = !IsInScope || _readOnly;

            _ui.readOnly = readOnly;
            _ui.opacity = readOnly ? 0.3f : 1f;
        }

        /* UI helper methods */

        internal static void IndentUI(UIComponent component) {
            UILabel label = component.Find<UILabel>("Label");

            if (label != null) {
                label.padding = new RectOffset(22, 0, 0, 0);
            }

            UISprite check = component.Find<UISprite>("Unchecked");

            if (check != null) {
                check.relativePosition += new Vector3(22.0f, 0);
            }
        }

        internal static void AllowTextWrap(UICheckBox checkBox, bool indented = false) {
            UILabel label = checkBox.label;
            bool requireTextWrap;
            int maxWidth = indented ? CHECKBOX_LABEL_MAX_WIDTH_INDENTED : CHECKBOX_LABEL_MAX_WIDTH;
            using (UIFontRenderer renderer = label.ObtainRenderer()) {
                Vector2 size = renderer.MeasureString(label.text);
                requireTextWrap = size.x > maxWidth;
            }
            label.autoSize = false;
            label.wordWrap = true;
            label.verticalAlignment = UIVerticalAlignment.Middle;
            label.textAlignment = UIHorizontalAlignment.Left;
            label.size = new Vector2(maxWidth, requireTextWrap ? 40 : 20);
            if (requireTextWrap) {
                checkBox.height = 42; // set new height + top/bottom 1px padding
            }
        }
    }
}