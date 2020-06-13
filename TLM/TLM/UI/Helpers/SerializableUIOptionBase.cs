namespace TrafficManager.UI.Helpers {
    using ColossalFramework.UI;
    using ColossalFramework;
    using CSUtil.Commons;
    using ICities;
    using System.Reflection;
    using System;
    using TrafficManager.State;

    public abstract class SerializableUIOptionBase<TVal, TUI> : ILegacySerializableOption
        where TUI : UIComponent {
        //Data:
        public SerializableUIOptionBase(string fieldName) {
            ValueField = typeof(Options).GetField(fieldName, BindingFlags.Static | BindingFlags.Public);
            if (ValueField == null) {
                throw new Exception($"{typeof(Options)}.{fieldName} does not exists");
            }
        }
        private FieldInfo ValueField;
        private Options OptionInstance => Singleton<Options>.instance;
        public virtual TVal Value {
            get => (TVal)ValueField.GetValue(null);
            set => ValueField.SetValue(null, value);
        }
        public static implicit operator TVal(SerializableUIOptionBase<TVal, TUI> a) => a.Value;
        public abstract void Load(byte data);
        public abstract byte Save();

        //UI:
        public abstract void AddUI(UIHelperBase container);
        protected TUI _ui;
        public string Label;
        public string Tooltip;
        public bool Indent = false;

        public void DefaultOnValueChanged(TVal newVal) {
            Options.IsGameLoaded();
            Log._Debug($"{Label} changed to {newVal}");
            Value = newVal;
        }
    }
}
