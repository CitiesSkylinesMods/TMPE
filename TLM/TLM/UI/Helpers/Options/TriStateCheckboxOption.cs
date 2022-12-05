namespace TrafficManager.UI.Helpers {
    using ICities;
    using ColossalFramework.UI;
    using TrafficManager.State;
    using CSUtil.Commons;
    using UnityEngine;
    using TrafficManager.Util.Extensions;
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using TrafficManager.Util;
    using System;

    public class TriStateCheckboxOption : PropagatorOptionBase<bool?, UITriStateCheckbox> {
        private const int LABEL_MAX_WIDTH = 615;
        private const int LABEL_MAX_WIDTH_INDENTED = 600;

        public TriStateCheckboxOption(string fieldName, Scope scope = Scope.Savegame) : base(fieldName, scope) { }

        public override void Propagate(bool value) {
            if (!value) {
                // this tristate button depends on another option that has been disabled.
                Value = null;
            } else {
                // Don't know to set to true or false because both are none-null.
                // I don't think it makes sense for other options to depend on tristate checkbox anyway.
                throw new NotImplementedException("other options cannot depend on tristate checkbox");
            }
        }
        protected override void OnPropagateAll(bool? val) => PropagateAll(val.HasValue);

        public override string ToString() => Name;

        public override void Load(byte data) =>
            Value = TernaryBoolUtil.ToOptBool((TernaryBool)data);

        public override byte Save() =>
            (byte)TernaryBoolUtil.ToTernaryBool(Value);

        public override void SetUIValue(bool? value) => _ui.Value = value;

        public override void AddUI(UIHelperBase container) {
            _ui = container.AddUIComponent<UITriStateCheckbox>();
            _ui.EventValueChanged += (_,val) => InvokeOnValueChanged(val);
            InitUI(_ui);
            if (Indent) ApplyIndent(_ui);
            ApplyTextWrap(_ui, Indent);
        }

        protected override void UpdateLabel() {
            if (!HasUI) return;

            _ui.label.text = Translate(Label);
        }

        protected override void UpdateTooltip() {
            if (HasUI) {
                _ui.tooltip = Tooltip;
            }
        }

        protected override void UpdateReadOnly() {
            if (!HasUI) return;

            Log._Debug($"TriStateCheckboxOption.UpdateReadOnly() - `{Name}` is {(ReadOnly ? "read-only" : "writeable")}");

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
            int maxWidth = indented ? LABEL_MAX_WIDTH_INDENTED : LABEL_MAX_WIDTH;
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