// ReSharper disable once CheckNamespace
namespace TrafficManager.UI {
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using ColossalFramework;
    using ColossalFramework.Globalization;
    using CSUtil.Commons;
    using State;

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
            LoadingExtension.TranslationDatabase.menuLookup_;

        private Localization.LookupTable optionsLookup_;
        public static Localization.LookupTable Options =>
            LoadingExtension.TranslationDatabase.optionsLookup_;

        private Localization.LookupTable junctionRestrictionsLookup_;
        public static Localization.LookupTable JunctionRestrictions =>
            LoadingExtension.TranslationDatabase.junctionRestrictionsLookup_;

        private Localization.LookupTable laneRoutingLookup_;
        public static Localization.LookupTable LaneRouting =>
            LoadingExtension.TranslationDatabase.laneRoutingLookup_;

        private Localization.LookupTable modConflictsLookup_;
        public static Localization.LookupTable ModConflicts =>
            LoadingExtension.TranslationDatabase.modConflictsLookup_;

        private Localization.LookupTable parkingRestrictionsLookup_;
        public static Localization.LookupTable ParkingRestrictions =>
            LoadingExtension.TranslationDatabase.parkingRestrictionsLookup_;

        private Localization.LookupTable prioritySignsLookup_;
        public static Localization.LookupTable PrioritySigns =>
            LoadingExtension.TranslationDatabase.prioritySignsLookup_;

        private Localization.LookupTable speedLimitsLookup_;
        public static Localization.LookupTable SpeedLimits =>
            LoadingExtension.TranslationDatabase.speedLimitsLookup_;

        private Localization.LookupTable trafficLightsLookup_;
        public static Localization.LookupTable TrafficLights =>
            LoadingExtension.TranslationDatabase.trafficLightsLookup_;

        private Localization.LookupTable vehicleRestrictionsLookup_;
        public static Localization.LookupTable VehicleRestrictions =>
            LoadingExtension.TranslationDatabase.vehicleRestrictionsLookup_;

        private Localization.LookupTable tutorialsLookup_;
        public static Localization.LookupTable Tutorials =>
            LoadingExtension.TranslationDatabase.tutorialsLookup_;

        private Localization.LookupTable aiCitizenLookup_;
        public static Localization.LookupTable AICitizen =>
            LoadingExtension.TranslationDatabase.aiCitizenLookup_;

        private Localization.LookupTable aiCarLookup_;
        public static Localization.LookupTable AICar =>
            LoadingExtension.TranslationDatabase.aiCarLookup_;

        public void LoadAllTranslations() {
            CsvColumnsToLocales
                = new Dictionary<string, string> {
                                                     { "English", "en" }, // DEFAULT_LANGUAGE_CODE
                                                     { "Chinese Simplified", "zh" },
                                                     { "Chinese Traditional", "zh-tw" },
                                                     { "Dutch", "nl" },
                                                     { "English, United Kingdom", "en-gb" },
                                                     { "French", "fr" },
                                                     { "German", "de" },
                                                     { "Hungarian", "hu" },
                                                     { "Italian", "it" },
                                                     { "Japanese", "ja" },
                                                     { "Korean", "ko" },
                                                     { "Polish", "pl" },
                                                     { "Portuguese", "pt" },
                                                     { "Russian", "ru" },
                                                     { "Spanish", "es" }
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
            language = FixLanguageNameIfLanguageMod(language);

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
                Log.Warning($"Translated file {translatedFilename} not found!");
            }

            return filename;
        }

        private static string FixLanguageNameIfLanguageMod(string language) {
            if (AvailableLanguageCodes.Contains(language))
                return language;

            switch (language) {
                case "jaex": {
                    language = "ja";
                    break;
                }

                case "zh-cn": {
                    language = "zh";
                    break;
                }

                case "kr": {
                    language = "ko";
                    break;
                }

                default:
                    language = "en";
                    break;
            }

            return language;
        }

        internal static string GetCurrentLanguage() {
            string lang = GlobalConfig.Instance.LanguageCode;

            // Having language code null means use the game language
            return lang ?? FixLanguageNameIfLanguageMod(LocaleManager.instance.language);
        }

        public void ReloadTutorialTranslations() {
            var locale = (Locale)typeof(LocaleManager)
                                 .GetField(
                                     "m_Locale",
                                     BindingFlags.Instance | BindingFlags.NonPublic)
                                 ?.GetValue(SingletonLite<LocaleManager>.instance);
            if (locale == null) {
                Log.Warning("Can't update tutorials because locale object is null");
                return;
            }

            string lang = GetCurrentLanguage();

            // Reset is private method used to delete the key before re-adding it
            MethodInfo resetFun = typeof(Locale)
                .GetMethod(
                    "ResetOverriddenLocalizedStrings",
                    BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (KeyValuePair<string, string> entry in tutorialsLookup_.AllLanguages[lang]) {
                if (!entry.Key.StartsWith(TUTORIAL_KEY_PREFIX)) {
                    continue;
                }

                string identifier;
                string tutorialKey;

                if (entry.Key.StartsWith(TUTORIAL_HEAD_KEY_PREFIX)) {
                    identifier = "TUTORIAL_ADVISER_TITLE";
                    tutorialKey = TUTORIAL_KEY_PREFIX +
                                  entry.Key.Substring(TUTORIAL_HEAD_KEY_PREFIX.Length);
                } else if (entry.Key.StartsWith(TUTORIAL_BODY_KEY_PREFIX)) {
                    identifier = "TUTORIAL_ADVISER";
                    tutorialKey = TUTORIAL_KEY_PREFIX +
                                  entry.Key.Substring(TUTORIAL_BODY_KEY_PREFIX.Length);
                } else {
                    continue;
                }

                var key = new Locale.Key() {
                    m_Identifier = identifier,
                    m_Key = tutorialKey
                };

                resetFun?.Invoke(locale, new object[] { key });
                locale.AddLocalizedString(key, entry.Value);
            }
        }

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
    }
}