namespace TrafficManager.UI.Helpers {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using State;

    public abstract class SerializableUIOptionBase<TVal, TUI> : SerializableOptionBase
        where TUI : UIComponent {
        protected TVal _value;
        public readonly TVal DefaultValue;
        public TVal Value { get => _value; }

        public abstract void AddUI(UIHelperBase container);

        public abstract void SetValue(TVal newVal);

        protected TUI _ui;
        protected readonly bool _tooltip;
        public string Key;
        public string GroupName;
        public string Label { get => $"{GroupName}.CheckBox: {Key}"; }
        public string Tooltip { get => $"{GroupName}.Tooltip: {Key}"; }

        public void DefaultOnValueChanged(TVal newVal) {
            Options.IsGameLoaded();
            Log._Debug($"{GroupName}.{Label} changed to {newVal}");
            _value = newVal;
        }
        public SerializableUIOptionBase(
        string key,
        TVal default_value,
        string group_name,
        bool tooltip = false) {
            Key = key;
            DefaultValue = _value = default_value;
            GroupName = group_name;
            _tooltip = tooltip;
        }
    }
}
