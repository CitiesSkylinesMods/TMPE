namespace TrafficManager.UI.Helpers {
    using ICities;
    using ColossalFramework.UI;
    using TrafficManager.State;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using ColossalFramework.Threading;
    using JetBrains.Annotations;
    using TrafficManager.Util;
    using UnityEngine;

    public class CheckboxOption : PropagatorOptionBase<bool, UICheckBox> {
        private const int CHECKBOX_LABEL_MAX_WIDTH = 695;
        private const int CHECKBOX_LABEL_MAX_WIDTH_INDENTED = 680;

        public CheckboxOption(string fieldName, Scope scope = Scope.Savegame)
        : base(fieldName, scope) { }

        public override void Propagate(bool value) => Value = value;

        protected override void OnPropagateAll(bool val) => PropagateAll(val);

        public override string ToString() => Name;

        public override void Load(byte data) => Value = data != 0;

        public override byte Save() => (byte)(Value ? 1 : 0);

        public override void SetUIValue(bool value) => _ui.isChecked = value;

        public override void AddUI(UIHelperBase container) {
            _ui = container.AddCheckbox(Translate(Label), Value, InvokeOnValueChanged) as UICheckBox;
            InitUI(_ui);
            if (Indent) ApplyIndent(_ui);
            ApplyTextWrap(_ui, Indent);
        }

        protected override void UpdateLabel() {
            if (!HasUI) return;

            _ui.label.text = Translate(Label);
        }

        protected override void UpdateTooltip() {
            if (!HasUI) return;

            _ui.tooltip = Tooltip;
        }

        protected override void UpdateReadOnly() {
            if (!HasUI) return;
            Log._Debug($"CheckboxOption.UpdateReadOnly() - `{Name}` is {(ReadOnly ? "read-only" : "writeable")}");

            _ui.readOnly = ReadOnly;
            _ui.opacity = ReadOnly ? 0.3f : 1f;
        }

        /* UI helper methods */

        internal static void ApplyIndent(UIComponent component) {
            UILabel label = component.Find<UILabel>("Label");

            if (label != null) {
                label.padding = new RectOffset(22, 0, 0, 0);
            }

            UISprite check = component.Find<UISprite>("Unchecked");

            if (check != null) {
                check.relativePosition += new Vector3(22.0f, 0);
            }
        }

        internal static void ApplyTextWrap(UICheckBox checkBox, bool indented = false) {
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