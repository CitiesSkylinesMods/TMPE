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

        /* Data: */
        public SerializableUIOptionBase(string fieldName, Options.PersistTo scope) {

            ValueField = typeof(Options).GetField(fieldName, BindingFlags.Static | BindingFlags.Public);
            if (ValueField == null) {
                throw new Exception($"{typeof(Options)}.{fieldName} does not exist");
            }

            Scope = scope;
        }

        public TranslatorDelegate Translator;
        private FieldInfo ValueField;
        public Options.PersistTo Scope { get; private set; }

        /// <summary>Gets or sets the value of the field this option represents.</summary>
        public virtual TVal Value {
            get => (TVal)ValueField.GetValue(null);
            set => ValueField.SetValue(null, value);
        }

        /// <summary>Custom translation delegate.</summary>
        /// <param name="key">The locale key to translate.</param>
        /// <returns>Returns the translation for <paramref name="key"/>.</returns>
        public delegate string TranslatorDelegate(string key);

        public static implicit operator TVal(SerializableUIOptionBase<TVal, TUI> a) => a.Value;

        // Legacy serialisation in `OptionsManager.cs`
        public abstract void Load(byte data);
        public abstract byte Save();

        // Unknown purpose.
        private Options OptionInstance => Singleton<Options>.instance;

        /* UI: */
        public string Label;
        public string Tooltip;
        public bool Indent = false;
        protected TUI _ui;
        public abstract void AddUI(UIHelperBase container);

        public virtual bool ReadOnlyUI { get; set; }

        /// <summary>
        /// Returns <c>true</c> if user can change the setting in the current scope.
        ///
        /// When not in scope, the UI component should be made read-only.
        /// </summary>
        protected bool IsInScope =>
            Scope.IsFlagSet(Options.PersistTo.Global) ||
            (Scope.IsFlagSet(Options.PersistTo.Savegame) && Options.IsGameLoaded(false));

        public void DefaultOnValueChanged(TVal newVal) {
            Log._Debug($"{Label} changed to {newVal}");
            Value = newVal;
        }

        /// <summary>
        /// Translate a locale key via <see cref="Translation.Options"/>
        /// or, if defined, via custom <see cref="Translator"/>.
        /// </summary>
        /// <param name="key">The locale key to translate.</param>
        /// <returns>Returns the translation for <paramref name="key"/>.</returns>
        protected string T(string key) {
            return Translator == null
                ? Translation.Options.Get(key)
                : Translator(key);
        }

    }
}
