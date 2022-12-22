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

    public class CheckboxOption :
        SerializableUIOptionBase<bool, UICheckBox, CheckboxOption>, IValuePropagator {
        private const int CHECKBOX_LABEL_MAX_WIDTH = 695;
        private const int CHECKBOX_LABEL_MAX_WIDTH_INDENTED = 680;

        private HashSet<IValuePropagator> _propagatesTrueTo = new();
        private HashSet<IValuePropagator> _propagatesFalseTo = new();

        public CheckboxOption(string fieldName, Scope scope = Scope.Savegame)
        : base(fieldName, scope) { }

        /// <summary>
        /// If this checkbox is set <c>true</c>, it will propagate that to the <paramref name="target"/>.
        /// Chainable.
        /// </summary>
        /// <param name="target">The checkbox to propagate <c>true</c> value to.</param>
        /// <remarks>
        /// If target is set <c>false</c>, it will propagate that back to this checkbox.
        /// </remarks>
        public CheckboxOption PropagateTrueTo([NotNull] IValuePropagator target) {
            Log.Info($"CheckboxOption.PropagateTrueTo: `{FieldName}` will propagate to `{target}`");
            this.AddPropagate(target,true);
            target.AddPropagate(this, false);
            return this;
        }

        private HashSet<IValuePropagator> GetTargetPropagates(bool value) =>
            value ? _propagatesTrueTo : _propagatesFalseTo;

        public void AddPropagate(IValuePropagator target, bool value) =>
            GetTargetPropagates(value).Add(target);

        public void Propagate(bool value) => Value = value;

        private void PropagateAll(bool value) {
            foreach (var target in GetTargetPropagates(value))
                target.Propagate(value);
        }

        public override string ToString() => FieldName;

        public override void Load(byte data) => Value = data != 0;

        public override byte Save() => (byte)(Value ? 1 : 0);

        /* UI */
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

                if (value != base.Value) {
                    base.Value = value;
                    Log.Info($"CheckboxOption.Value: `{FieldName}` changed to {value}");
                    PropagateAll(value);
                }

                if (Shortcuts.IsMainThread()) {
                    if (HasUI) {
                        _ui.isChecked = value;
                    }
                } else {
                    SimulationManager.instance
                                     .m_ThreadingWrapper
                                     .QueueMainThread(() => {
                                         if (HasUI) {
                                             _ui.isChecked = value;
                                         }
                                     });
                }
            }
        }

        public override CheckboxOption AddUI(UIHelperBase container) {
            _ui = container.AddCheckbox(Translate(Label), Value, InvokeOnValueChanged) as UICheckBox;

            if (Indent) ApplyIndent(_ui);

            ApplyTextWrap(_ui, Indent);

            UpdateTooltip();
            UpdateReadOnly();

            return this;
        }

        protected override void UpdateLabel() {
            if (!HasUI) return;

            _ui.label.text = Translate(Label);
        }

        protected override void UpdateTooltip() {
            if (!HasUI) return;

            _ui.tooltip = IsInScope
                ? string.IsNullOrEmpty(_tooltip)
                    ? string.Empty // avoid invalidating UI if already no tooltip
                    : Translate(_tooltip)
                : Translate(INGAME_ONLY_SETTING);
        }

        protected override void UpdateReadOnly() {
            if (!HasUI) return;

            var readOnly = !IsInScope || _readOnly;

            Log._Debug($"CheckboxOption.UpdateReadOnly() - `{FieldName}` is {(readOnly ? "read-only" : "writeable")}");

            _ui.readOnly = readOnly;
            _ui.opacity = readOnly ? 0.3f : 1f;
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