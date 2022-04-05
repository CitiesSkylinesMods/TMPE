namespace TrafficManager.UI.Helpers {
    using ICities;
    using ColossalFramework.UI;
    using TrafficManager.State;
    using CSUtil.Commons;
    using System;
    using System.Linq;
    using TrafficManager.API.Traffic.Enums;

    public class DropDownOption : SerializableUIOptionBase<int, UIDropDown, DropDownOption> {
        public DropDownOption(string fieldName, Options.PersistTo scope = Options.PersistTo.Savegame)
        : base(fieldName, scope) { }

        protected string[] Labels;

        /* Data */
        public override void Load(byte data) => Value = data;
        public override byte Save() => (byte)Value;

        public override int Value {
            get => base.Value;
            set {
                if (0 <= value && value < Labels.Length) {
                    _ui.selectedIndex = base.Value = value;
                } else {
                    Log.Error($"index:{value} out of range:[0,{Labels.Length - 1}]");
                }
            }
        }

        /* UI */
        public override DropDownOption AddUI(UIHelperBase container) {
            _ui = container.AddDropdown(
                text: Translate(Label) + ":",
                options: Labels.Select(Translate).ToArray(),
                defaultSelection: Value,
                eventCallback: InvokeOnValueChanged) as UIDropDown;

            _ui.width = 350;

            UpdateTooltip();
            UpdateReadOnly();

            return this;
        }

        protected override void UpdateLabel() {
            if (!HasUI) return;
            _ui.text = Translate(Label) + ":";
            _ui.items = Labels.Select(Translate).ToArray();
        }

        protected override void UpdateTooltip() {
            if (!HasUI) return;

            _ui.tooltip = IsInScope
                ? $"{_tooltip}"
                : Translate(INGAME_ONLY_SETTING);
        }

        protected override void UpdateReadOnly() {
            if (!HasUI) return;

            var readOnly = !IsInScope || _readOnly;

            Log._Debug($"DropDownOption.UpdateReadOnly() - `{FieldName}` is {(readOnly ? "read-only" : "writeable")}");

            _ui.isEnabled = !readOnly;
        }
    }

    public class DropDownOption<TEnum> : SerializableUIOptionBase<TEnum, UIDropDown, DropDownOption<TEnum>>
        where TEnum : struct, Enum, IConvertible {

        public DropDownOption(string fieldName, Options.PersistTo scope = Options.PersistTo.Savegame)
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

        protected void InvokeOnIndexChanged(int index) {
            TEnum val = values_[index];
            InvokeOnValueChanged(val);
        }

        public override void Load(byte data) {
            unchecked {
                Value = (TEnum)(IConvertible)(int)data;
            }
        }

        public override byte Save() => Value.ToByte(null);

        public override TEnum Value {
            get => base.Value;
            set {
                if (values_.Contains(value)) {
                    base.Value = value;
                    _ui.selectedIndex = IndexOf(value);
                } else {
                    Log.Error($"unrecognised value:{value} for enum:{typeof(TEnum).Name}");
                }
            }
        }

        /* UI */
        public override DropDownOption<TEnum> AddUI(UIHelperBase container) {
            _ui = container.AddDropdown(
                text: Translate(Label) + ":",
                options: GetTranslatedItems(),
                defaultSelection: IndexOf(Value),
                eventCallback: InvokeOnIndexChanged) as UIDropDown;

            _ui.width = 350;

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

            _ui.tooltip = IsInScope
                ? $"{_tooltip}"
                : Translate(INGAME_ONLY_SETTING);
        }

        protected override void UpdateReadOnly() {
            if (!HasUI) return;

            var readOnly = !IsInScope || _readOnly;

            Log._Debug($"DropDownOption.UpdateReadOnly() - `{FieldName}` is {(readOnly ? "read-only" : "writeable")}");

            _ui.isEnabled = !readOnly;
        }
    }
}