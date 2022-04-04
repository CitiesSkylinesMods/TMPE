namespace TrafficManager.UI.Helpers {
    using ICities;
    using ColossalFramework.UI;
    using TrafficManager.State;
    using CSUtil.Commons;
    using System;
    using System.Linq;
    using TrafficManager.API.Traffic.Enums;

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
            Select(KeyAttribute.GetKey<TEnum>).
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
            unchecked {
                Value = (TEnum)(IConvertible)(int)data;
            }
        }

        public override byte Save() => Value.ToByte(null);

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

        public bool ReadOnly {
            get => _readOnly;
            set {
                _readOnly = !IsInScope || value;
                UpdateReadOnly();
            }
        }

        public override DropDownOption<TEnum> AddUI(UIHelperBase container) {
            _ui = container.AddDropdown(
                text: T(Label) + ":",
                options: GetTranslatedItems(),
                defaultSelection: IndexOf(Value),
                eventCallback: InvokeOnValueChanged) as UIDropDown;

            _ui.width = 350;

            UpdateTooltip();
            UpdateReadOnly();

            return this;
        }

        protected override void UpdateLabel() {
            if (!HasUI) return;
            _ui.text = T(Label) + ":";
            _ui.items = GetTranslatedItems();
        }

        protected override void UpdateTooltip() {
            if (!HasUI) return;

            _ui.tooltip = IsInScope
                ? $"{Value}{_tooltip}"
                : T(INGAME_ONLY_SETTING);
        }

        protected override void UpdateReadOnly() {
            if (!HasUI) return;

            var readOnly = !IsInScope || _readOnly;

            Log._Debug($"DropDownOption.UpdateReadOnly() - `{FieldName}` is {(readOnly ? "read-only" : "writeable")}");

            _ui.isEnabled = !readOnly;
        }
    }

    public class DropDownOptionSimulationAccuracy : DropDownOption<SimulationAccuracy> {
        public DropDownOptionSimulationAccuracy(string fieldName, Options.PersistTo scope = Options.PersistTo.Savegame)
            : base(fieldName, scope) { }
        public override void Load(byte data) {
            int val = SimulationAccuracy.MaxValue - (SimulationAccuracy)data;
            Value = (SimulationAccuracy)val;
        }
        public override byte Save() {
            return (byte)(SimulationAccuracy.MaxValue - Value);
        }
    }

}