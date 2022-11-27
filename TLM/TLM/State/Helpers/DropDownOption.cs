namespace TrafficManager.State.Helpers {
    using ICities;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using System.Linq;
    using TrafficManager.API.Traffic.Enums;

    public class DropDownOption<TEnum> : UIOptionBase<TEnum>
        where TEnum : struct, Enum, IConvertible {
        private UILabel _dropdownLabel;
        private UIDropDown _ui;
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

        public virtual DropDownOption<TEnum> AddUI(UIHelperBase container) {
            _ui = container.AddDropdown(
                text: Translate(Label) + ":",
                options: GetTranslatedItems(),
                defaultSelection: IndexOf(Option.Value),
                eventCallback: OnValueChanged) as UIDropDown;
            _ui.width = 350;
            _ui.parent.width = 350; //UIDropDown is added to the UIPanel which also require resize for correct tooltip interactions
            _dropdownLabel = _ui.parent.Find<UILabel>("Label");
            InitUI(_ui);
            return this;
        }

        public override void SetValue(TEnum value) {
            if (values_.Contains(value)) {
                if (_ui != null) {
                    _ui.selectedIndex = IndexOf(value);
                }
            } else {
                Log.Error($"unrecognised value:{value} for enum:{typeof(TEnum).Name}");
            }
        }

        protected void OnValueChanged(int index) => OnValueChanged(values_[index]);

        protected override void UpdateLabel() {
            if (_ui == null) return;
            _ui.text = Translate(Label) + ":";
            _ui.items = GetTranslatedItems();
        }

        protected override void UpdateTooltip() {
            if (_ui == null) return;

            //UIDropDown parent(UIPanel) handles tooltip
            _ui.parent.tooltip = IsInScope
                ? $"{_tooltip}"
                : Translate(INGAME_ONLY_SETTING);
        }

        protected override void UpdateReadOnly() {
            if (_ui == null) return;

            var readOnly = !IsInScope || _readOnly;

            Log._Debug($"DropDownOption.UpdateReadOnly() - `Name` is {(readOnly ? "read-only" : "writeable")}");

            _ui.isInteractive = !readOnly;
            _ui.opacity = readOnly ? 0.3f : 1f;
            // parent is UIPanel containing text label and dropdown
            _dropdownLabel.opacity = readOnly ? 0.3f : 1f;
        }
    }
}