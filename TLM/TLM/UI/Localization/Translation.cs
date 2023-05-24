// ReSharper disable once CheckNamespace
namespace TrafficManager.UI {
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using ColossalFramework;
    using ColossalFramework.Globalization;
    using CSUtil.Commons;
    using TrafficManager.State;
    using TrafficManager.Lifecycle;

    /// <summary>
    /// Adding a new language step by step:
    /// 1. In Crowdin: Go to languages and add a new language
    /// 2. Visit the Files menu and click "Edit Schema" on each file
    /// 3. In the file selection dialog provide the recently exported CSV file
    /// 4. The columns selection window appears, scroll right and select the new language in some column
    /// 5. In this file: <see cref="AvailableLanguageCodes"/> and <see cref="CsvColumnsToLocales"/>
    /// </summary>
    public class Translation {
        internal const string DEFAULT_LANGUAGE_CODE = "en";
        internal const string RESOURCES_PREFIX = "TrafficManager.Resources.";

        public const string TUTORIAL_KEY_PREFIX = "TMPE_TUTORIAL_";
        private const string TUTORIAL_HEAD_KEY_PREFIX = TUTORIAL_KEY_PREFIX + "HEAD_";
        public const string TUTORIAL_BODY_KEY_PREFIX = TUTORIAL_KEY_PREFIX + "BODY_";

        public const string GUIDE_KEY_PREFIX = "TMPE_GUIDE_";
        private const string GUIDE_HEAD_KEY_PREFIX = GUIDE_KEY_PREFIX + "HEAD_";
        public const string GUIDE_BODY_KEY_PREFIX = GUIDE_KEY_PREFIX + "BODY_";

        private const string TMPE_TITLE_PREFIX = "TM:PE ";

        /// <summary>
        /// Defines column order in CSV, but not the dropdown order, the dropdown is just sorted.
        /// Mapping from translation languages (first row of CSV export) to language locales used in
        /// <see cref="AvailableLanguageCodes"/> above.
        /// </summary>
        internal static Dictionary<string, string> CsvColumnsToLocales;

        /// <summary>
        /// Defines the order of language selector dropdown.
        /// </summary>
        public static List<string> AvailableLanguageCodes;

        private Localization.LookupTable menuLookup_;
        public static Localization.LookupTable Menu =>
            TMPELifecycle.Instance.TranslationDatabase.menuLookup_;

        private Localization.LookupTable optionsLookup_;
        public static Localization.LookupTable Options =>
            TMPELifecycle.Instance.TranslationDatabase.optionsLookup_;

        private Localization.LookupTable junctionRestrictionsLookup_;
        public static Localization.LookupTable JunctionRestrictions =>
            TMPELifecycle.Instance.TranslationDatabase.junctionRestrictionsLookup_;

        private Localization.LookupTable laneRoutingLookup_;
        public static Localization.LookupTable LaneRouting =>
            TMPELifecycle.Instance.TranslationDatabase.laneRoutingLookup_;

        private Localization.LookupTable modConflictsLookup_;
        public static Localization.LookupTable ModConflicts =>
            TMPELifecycle.Instance.TranslationDatabase.modConflictsLookup_;

        private Localization.LookupTable parkingRestrictionsLookup_;
        public static Localization.LookupTable ParkingRestrictions =>
            TMPELifecycle.Instance.TranslationDatabase.parkingRestrictionsLookup_;

        private Localization.LookupTable prioritySignsLookup_;
        public static Localization.LookupTable PrioritySigns =>
            TMPELifecycle.Instance.TranslationDatabase.prioritySignsLookup_;

        private Localization.LookupTable speedLimitsLookup_;
        public static Localization.LookupTable SpeedLimits =>
            TMPELifecycle.Instance.TranslationDatabase.speedLimitsLookup_;

        private Localization.LookupTable trafficLightsLookup_;
        public static Localization.LookupTable TrafficLights =>
            TMPELifecycle.Instance.TranslationDatabase.trafficLightsLookup_;

        private Localization.LookupTable vehicleRestrictionsLookup_;
        public static Localization.LookupTable VehicleRestrictions =>
            TMPELifecycle.Instance.TranslationDatabase.vehicleRestrictionsLookup_;

        private Localization.LookupTable tutorialsLookup_;
        public static Localization.LookupTable Tutorials =>
            TMPELifecycle.Instance.TranslationDatabase.tutorialsLookup_;

        private Localization.LookupTable guideLookup_;
        public static Localization.LookupTable Guide =>
            TMPELifecycle.Instance.TranslationDatabase.guideLookup_;

        private Localization.LookupTable aiCitizenLookup_;
        public static Localization.LookupTable AICitizen =>
            TMPELifecycle.Instance.TranslationDatabase.aiCitizenLookup_;

        private Localization.LookupTable aiCarLookup_;
        public static Localization.LookupTable AICar =>
            TMPELifecycle.Instance.TranslationDatabase.aiCarLookup_;

        /// <summary>
        /// Gets or sets a value indicating the current language to use for translations.
        /// Note: Don't access directly, instead use <see cref="GetCurrentLanguage()"/>.
        /// </summary>
        private static string CurrentLanguage { get; set; } = string.Empty;

        public void LoadAllTranslations() {
            CsvColumnsToLocales
                = new Dictionary<string, string> {
                    { "Arabic", "ar"},
                    { "English", "en" }, // DEFAULT_LANGUAGE_CODE
                    { "Chinese Simplified", "zh" },
                    { "Chinese Traditional", "zh-tw" },
                    { "Czech", "cz" },
                    { "Dutch", "nl" },
                    { "English, United Kingdom", "en-gb" },
                    { "Finnish", "fi" },
                    { "French", "fr" },
                    { "German", "de" },
                    { "Hungarian", "hu" },
                    { "Indonesian", "id" },
                    { "Italian", "it" },
                    { "Japanese", "ja" },
                    { "Korean", "ko" },
                    { "Occitan", "oc"},
                    { "Polish", "pl" },
                    { "Portuguese", "pt" },
                    { "Romanian", "ro" },
                    { "Russian", "ru" },
                    { "Slovak", "sk"},
                    { "Spanish", "es" },
                    { "Thai", "th"},
                    { "Turkish", "tr"},
                    { "Ukrainian", "uk"},
                    { "Vietnamese", "vi" },
                };
            AvailableLanguageCodes = CsvColumnsToLocales.Values.ToList();
            AvailableLanguageCodes.Sort();

            menuLookup_ = new Localization.LookupTable("Menu");
            optionsLookup_ = new Localization.LookupTable("Options");
            junctionRestrictionsLookup_ = new Localization.LookupTable("JunctionRestrictions");
            laneRoutingLookup_ = new Localization.LookupTable("LaneRouting");
            modConflictsLookup_ = new Localization.LookupTable("ModConflicts");
            parkingRestrictionsLookup_ = new Localization.LookupTable("ParkingRestrictions");
            prioritySignsLookup_ = new Localization.LookupTable("PrioritySigns");
            speedLimitsLookup_ = new Localization.LookupTable("SpeedLimits");
            trafficLightsLookup_ = new Localization.LookupTable("TrafficLights");
            vehicleRestrictionsLookup_ = new Localization.LookupTable("VehicleRestrictions");
            tutorialsLookup_ = new Localization.LookupTable("Tutorials");
            guideLookup_ = new Localization.LookupTable("Guide");
            aiCitizenLookup_ = new Localization.LookupTable("AI_Citizen");
            aiCarLookup_ = new Localization.LookupTable("AI_Car");
        }

        public static string GetTranslatedFileName(string filename) {
            return GetTranslatedFileName(filename, GetCurrentLanguage());
        }

        /// <summary>
        /// Translates a filename to current language. Also use this if LoadingExtension does not
        /// have Translator field set (during construction initial loading)
        /// </summary>
        /// <param name="filename">Filename to translate</param>
        /// <returns>Filename with language inserted before extension</returns>
        public static string GetTranslatedFileName(string filename, string language) {
            language = GetValidLanguageCode(language);

            string translatedFilename = filename;
            if (language != DEFAULT_LANGUAGE_CODE) {
                int delimiterIndex = filename.Trim().LastIndexOf('.'); // file extension

                translatedFilename = string.Empty;

                if (delimiterIndex >= 0) {
                    translatedFilename = filename.Substring(0, delimiterIndex);
                }

                translatedFilename += $"_{language.Trim().ToLower()}";

                if (delimiterIndex >= 0) {
                    translatedFilename += filename.Substring(delimiterIndex);
                }
            }

            if (Assembly.GetExecutingAssembly()
                        .GetManifestResourceNames()
                        .Contains(RESOURCES_PREFIX + translatedFilename)) {
                Log._Debug($"Translated file {translatedFilename} found");
                return translatedFilename;
            }

            if (!DEFAULT_LANGUAGE_CODE.Equals(language)) {
                Log.Info($"Translated file {translatedFilename} not found; using base language");
            }

            return filename;
        }

        Locale locale => (Locale)typeof(LocaleManager)
            .GetField(
                "m_Locale",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(SingletonLite<LocaleManager>.instance);

        // Reset is private method used to delete the key before re-adding it
        MethodInfo resetFun = typeof(Locale)
            .GetMethod(
            "ResetOverriddenLocalizedStrings",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public void ReloadTutorialTranslations() {
            var locale = this.locale;
            if (locale == null) {
                Log.Warning("Can't update tutorials because locale object is null");
                return;
            }

            string lang = GetCurrentLanguage();

            foreach (KeyValuePair<string, string> entry in tutorialsLookup_.AllLanguages[lang]) {
                if (!entry.Key.StartsWith(TUTORIAL_KEY_PREFIX)) {
                    continue;
                }

                string identifier;
                string tutorialKey;
                string value = entry.Value;
                if (entry.Key.StartsWith(TUTORIAL_HEAD_KEY_PREFIX)) {
                    identifier = "TUTORIAL_ADVISER_TITLE";
                    tutorialKey = TUTORIAL_KEY_PREFIX +
                                  entry.Key.Substring(TUTORIAL_HEAD_KEY_PREFIX.Length);
                    value = TMPE_TITLE_PREFIX + value;
                } else if (entry.Key.StartsWith(TUTORIAL_BODY_KEY_PREFIX)) {
                    identifier = "TUTORIAL_ADVISER";
                    tutorialKey = TUTORIAL_KEY_PREFIX +
                                  entry.Key.Substring(TUTORIAL_BODY_KEY_PREFIX.Length);
                } else {
                    continue;
                }

                var key = new Locale.Key() {
                    m_Identifier = identifier,
                    m_Key = tutorialKey,
                };

                resetFun?.Invoke(locale, new object[] { key });
                locale.AddLocalizedString(key, value);
            }
        }

        public IEnumerable<string> GetGuides() {
            string lang = GetCurrentLanguage();
            foreach (KeyValuePair<string, string> entry in guideLookup_.AllLanguages[lang]) {
                if (entry.Key.StartsWith(GUIDE_HEAD_KEY_PREFIX)) {
                    yield return entry.Key.Substring(GUIDE_HEAD_KEY_PREFIX.Length);
                }
            }
        }

        public void ReloadGuideTranslations() {
            var locale = this.locale;
            if (locale == null) {
                Log.Warning("Can't update guides because locale object is null");
                return;
            }

            string lang = GetCurrentLanguage();

            foreach (KeyValuePair<string, string> entry in guideLookup_.AllLanguages[lang]) {
                if (!entry.Key.StartsWith(GUIDE_KEY_PREFIX)) {
                    continue;
                }

                string identifier;
                string guideKey;
                string value = entry.Value;
                if (entry.Key.StartsWith(GUIDE_HEAD_KEY_PREFIX)) {
                    identifier = "TUTORIAL_TITLE";
                    guideKey = GUIDE_KEY_PREFIX +
                                  entry.Key.Substring(GUIDE_HEAD_KEY_PREFIX.Length);
                    value = TMPE_TITLE_PREFIX + value;
                } else if (entry.Key.StartsWith(GUIDE_BODY_KEY_PREFIX)) {
                    identifier = "TUTORIAL_TEXT";
                    guideKey = GUIDE_KEY_PREFIX +
                                  entry.Key.Substring(GUIDE_BODY_KEY_PREFIX.Length);
                } else {
                    continue;
                }

                var key = new Locale.Key() {
                    m_Identifier = identifier,
                    m_Key = guideKey,
                };

                resetFun?.Invoke(locale, new object[] { key });
                locale.AddLocalizedString(key, value);
            }
        }

        public void AddMissingGuideString(string localeKey) {
            var locale = this.locale;
            if (locale == null) {
                Log.Warning("Can't update guides because locale object is null");
                return;
            }

            {
                var key1 = new Locale.Key() {
                    m_Identifier = "TUTORIAL_TITLE",
                    m_Key = GUIDE_KEY_PREFIX + localeKey,
                };
                string value1 = TMPE_TITLE_PREFIX + "¶" + GUIDE_HEAD_KEY_PREFIX + localeKey;
                resetFun?.Invoke(locale, new object[] { key1 });
                locale.AddLocalizedString(key1,  value1);
            }
            {
                var key2 = new Locale.Key() {
                    m_Identifier = "TUTORIAL_TEXT",
                    m_Key = GUIDE_KEY_PREFIX + localeKey,
                };
                string value2 = "¶" + GUIDE_BODY_KEY_PREFIX + localeKey;
                resetFun?.Invoke(locale, new object[] { key2 });
                locale.AddLocalizedString(key2, value2);
            }
        }

        /// <summary>
        /// Triggered when user changes base game language.
        /// </summary>
        public static void HandleGameLocaleChange() {
            // only do something if TM:PE language is set to use game language
            if (string.IsNullOrEmpty(GlobalConfig.Instance.LanguageCode)) {
                SetCurrentLanguageToGameLanguage();
            }
        }

        /// <summary>
        /// Sets <see cref="CurrentLanguage"/> based on the game language.
        /// Note: This does not check to see if user has chosen a specific language in TM:PE settings.
        /// </summary>
        public static void SetCurrentLanguageToGameLanguage() {
            string code = LocaleManager.instance.language;

            // language mods can add their own codes, so deal with those if necessary
            switch (code) {
                case "jaex": {
                    code = "ja";
                    break;
                }

                case "zh-cn": {
                    code = "zh";
                    break;
                }

                case "kr": {
                    code = "ko";
                    break;
                }
            }

            CurrentLanguage = GetValidLanguageCode(code);
        }

        /// <summary>
        /// Sets <see cref="CurrentLanguage"/> based on TM:PE language setting.
        /// </summary>
        public static void SetCurrentLanguageToTMPELanguage() {
            string code = GlobalConfig.Instance.LanguageCode;

            if (string.IsNullOrEmpty(code)) {
                SetCurrentLanguageToGameLanguage();
            } else {
                CurrentLanguage = GetValidLanguageCode(code);
            }
        }

        /// <summary>
        /// Returns a valid language code based on supplied code.
        /// Note: Does not check for codes added by game language mods.
        /// </summary>
        /// <param name="code">The code we want to use.</param>
        /// <returns>Either the code requested, or the mod default code.</returns>
        private static string GetValidLanguageCode(string code) {
            return AvailableLanguageCodes.Contains(code)
                ? code
                : DEFAULT_LANGUAGE_CODE;
        }

        /// <summary>
        /// Get the currently selected language.
        /// </summary>
        /// <returns>A valid language code</returns>
        internal static string GetCurrentLanguage() {
            if (string.IsNullOrEmpty(CurrentLanguage)) {
                SetCurrentLanguageToTMPELanguage();
            }
            return CurrentLanguage;
        }

#if DEBUG
        /// <summary>
        /// Used to size the in-game debug menu based on chosen langauge.
        /// </summary>
        /// <returns>Width to use for the menu.</returns>
        internal static int GetMenuWidth() {
            switch (GetCurrentLanguage()) {
                // also: case null:
                // also: case "en":
                // also: case "de":
                default: {
                    return 220;
                }

                case "ru":
                case "pl": {
                    return 260;
                }

                case "es":
                case "fr":
                case "it": {
                    return 240;
                }

                case "nl": {
                    return 270;
                }
            }
        }
#endif
    }
}