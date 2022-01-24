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

        /// <summary>Use as tooltip for readonly UI components.</summary>
        protected const string INGAME_ONLY_SETTING = "This setting can only be changed in-game.";

        /* Data: */

        protected Options.PersistTo _scope;
        private FieldInfo _fieldInfo;

        public SerializableUIOptionBase(string fieldName, Options.PersistTo scope) {

            _fieldInfo = typeof(Options).GetField(fieldName, BindingFlags.Static | BindingFlags.Public);
            if (_fieldInfo == null) {
                throw new Exception($"{typeof(Options)}.{fieldName} does not exist");
            }

            _scope = scope;
        }

        public TranslatorDelegate Translator {
            get => _translator ?? Translation.Options.Get;
            set => _translator = value;
        }
        private TranslatorDelegate _translator;
        /// <summary>Gets or sets the value of the field this option represents.</summary>
        public virtual TVal Value {
            get => (TVal)_fieldInfo.GetValue(null);
            set => _fieldInfo.SetValue(null, value);
        }

        /// <summary>Translate a locale key in to a localised string.</summary>
        /// <param name="key">The locale key to translate.</param>
        /// <returns>Returns the translation for <paramref name="key"/>.</returns>
        public delegate string TranslatorDelegate(string key);
        public string FieldName => _fieldInfo.Name;

        /// <summary>Returns <c>true</c> if user can change the setting in the current <see cref="_scope"/>.</summary>
        /// <remarks>When not in scope, the UI component should be made read-only (<seealso cref="ReadOnlyUI"/>).</remarks>
        protected bool IsInScope =>
            _scope.IsFlagSet(Options.PersistTo.Global) ||
            (_scope.IsFlagSet(Options.PersistTo.Savegame) && Options.IsGameLoaded(false)) ||
            _scope == Options.PersistTo.None;

        public static implicit operator TVal(SerializableUIOptionBase<TVal, TUI> a) => a.Value;

        // Legacy serialisation in `OptionsManager.cs`
        public abstract void Load(byte data);
        public abstract byte Save();

        // Unknown purpose.
        private Options OptionInstance => Singleton<Options>.instance;

        /* UI: */
        public bool HasUI => _ui != null;
        protected TUI _ui;
        public abstract void AddUI(UIHelperBase container);

        protected string _label;
        protected string _tooltip;

        protected bool _indent;
        protected bool _readOnlyUI;

        public void DefaultOnValueChanged(TVal newVal) {
            Log._Debug($"{Label} changed to {newVal}");
            Value = newVal;
        }

        /// <summary>Terse shortcut for <c>Translator(key)</c>.</summary>
        /// <param name="key">The locale key to translate.</param>
        /// <returns>Returns localised string for <paramref name="key"/>.</returns>
        protected string T(string key) => Translator(key);

    }
}
