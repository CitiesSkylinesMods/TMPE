namespace TrafficManager.UI.Helpers {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using State;

    //TODO issue #562: inherit ISerializable Interface
    //[Serializable()]
    public abstract class SerializableUIOptionBase<TVal, TUI> : ISerializableOptionBase
        where TUI : UIComponent {
        //Data:
        protected TVal _value;
        public readonly TVal DefaultValue;
        public virtual TVal Value {
            get => _value;
            set => _value = value;
        }
        public abstract void Load(byte data);
        public abstract byte Save();

        //UI:
        public abstract void AddUI(UIHelperBase container);
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
            bool tooltip = false)
        {
            _value = DefaultValue = default_value;
            Key = key;
            GroupName = group_name;
            _tooltip = tooltip;
        }
    }
}
