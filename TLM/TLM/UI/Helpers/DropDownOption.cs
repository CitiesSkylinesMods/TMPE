namespace TrafficManager.UI.Helpers {
    using ICities;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using System.Linq;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;

    public class DropDownOption<TEnum> : SerializableUIOptionBase<TEnum, UIDropDown, DropDownOption<TEnum>>
        where TEnum : Enum, IConvertible {
        private UILabel _dropdownLabel;

        public DropDownOption(string fieldName, Scope scope = Scope.Savegame)
        : base(fieldName, scope) { }

        /* Data */
        private static TEnum[] values_ = Enum.GetValues(typeof(TEnum)) as TEnum[];

        private static string[] names_ = Enum.GetNames(typeof(TEnum));

        private static string[] keys_ =
            names_.
            Where(item => item != "MaxValue").
            Select(LocaleKeyAttribute.GetKey<TEnum>).
            ToArray();

        private string[] GetTranslatedItems() =>
            keys_.Select(Translate).ToArray();

        private static int IndexOf(TEnum value) =>
            Array.FindIndex(values_, item => item.Equals(value));

        protected void InvokeOnValueChanged(int index) {
            TEnum val = values_[index];
            InvokeOnValueChanged(val);
        }

        public override void Load(byte data) {
            Value = (TEnum)((int)data as IConvertible);
        }

        public override byte Save() => Value.ToByte(null);

        /* UI */
        public override TEnum Value {
            get => base.Value;
            set {
                if (values_.Contains(value)) {
                    base.Value = value;
                    if (Shortcuts.IsMainThread()) {
                        if (HasUI) {
                            _ui.selectedIndex = IndexOf(value);
                        }
                    } else {
                        SimulationManager.instance
                                         .m_ThreadingWrapper
                                         .QueueMainThread(() => {
                                             if (HasUI) {
                                                 _ui.selectedIndex = IndexOf(value);
                                             }
                                         });
                    }
                } else {
                    Log.Error($"unrecognised value:{value} for enum:{typeof(TEnum).Name}");
                }
            }
        }

        public override DropDownOption<TEnum> AddUI(UIHelperBase container) {
            _ui = container.AddCustomDropDown(
                text: Translate(Label) + ":",
                options: GetTranslatedItems(),
                defaultSelection: IndexOf(Value),
                eventCallback: InvokeOnValueChanged) as UIDropDown;

            _ui.width = 350;
            _ui.parent.width = 350; //UIDropDown is added to the UIPanel which also require resize for correct tooltip interactions
            _dropdownLabel = _ui.parent.Find<UILabel>("Label");

            UpdateTooltip();
            UpdateReadOnly();

            return this;
        }

        protected override void UpdateLabel() {
            if (!HasUI) return;
            _ui.text = Translate(Label) + ":";
            _ui.items = GetTranslatedItems();
        }

        protected override void UpdateTooltip() {
            if (!HasUI) return;

            //UIDropDown parent(UIPanel) handles tooltip
            _ui.parent.tooltip = IsInScope
                ? $"{_tooltip}"
                : Translate(INGAME_ONLY_SETTING);
        }

        protected override void UpdateReadOnly() {
            if (!HasUI) return;

            var readOnly = !IsInScope || _readOnly;

            Log._Debug($"DropDownOption.UpdateReadOnly() - `{FieldName}` is {(readOnly ? "read-only" : "writeable")}");

            _ui.isInteractive = !readOnly;
            _ui.opacity = readOnly ? 0.3f : 1f;
            // parent is UIPanel containing text label and dropdown
            _dropdownLabel.opacity = readOnly ? 0.3f : 1f;
        }
    }
}