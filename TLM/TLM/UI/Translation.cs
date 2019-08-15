namespace TrafficManager.UI {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using ColossalFramework;
    using ColossalFramework.Globalization;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using State;
    using Util;

    public class Translation {
        public static readonly IDictionary<string, string> LanguageLabels;
        public static readonly IList<string> AvailableLanguageCodes;
        private const string DEFAULT_LANGUAGE_CODE = "en";

        public const string TUTORIAL_KEY_PREFIX = "TMPE_TUTORIAL_";
        private const string TUTORIAL_HEAD_KEY_PREFIX = TUTORIAL_KEY_PREFIX + "HEAD_";
        public const string TUTORIAL_BODY_KEY_PREFIX = TUTORIAL_KEY_PREFIX + "BODY_";
        private const string RESOURCES_PREFIX = "TrafficManager.Resources.";
        private const string DEFAULT_TRANSLATION_FILENAME = "lang.txt";
        private static Dictionary<string, string> _translations;
        private static string _loadedLanguage;

        static Translation() {
            AvailableLanguageCodes = new List<string> {
                "de",
                "en",
                "es",
                "fr",
                "hu",
                "it",
                "ja",
                "ko",
                "nl",
                "pl",
                "pt",
                "ru",
                "zh-tw",
                "zh"
            };

            LanguageLabels = new TinyDictionary<string, string> {
                ["de"] = "Deutsch",
                ["en"] = "English",
                ["es"] = "Español",
                ["fr"] = "Français",
                ["hu"] = "Magyar",
                ["it"] = "Italiano",
                ["ja"] = "日本語",
                ["ko"] = "한국의",
                ["nl"] = "Nederlands",
                ["pl"] = "Polski",
                ["pt"] = "Português",
                ["ru"] = "Русский язык",
                ["zh-tw"] = "中文 (繁體)",
                ["zh"] = "中文 (简体)"
            };
        }

        public static string GetString(string key) {
            LoadTranslations();
            return _translations.TryGetValue(key, out string ret) ? ret : key;
        }

        public static bool HasString(string key) {
            LoadTranslations();
            return _translations.ContainsKey(key);
        }

        public static string GetTranslatedFileName(string filename) {
            string language = GetCurrentLanguage();
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
            }

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

            if (Assembly.GetExecutingAssembly().GetManifestResourceNames()
                        .Contains(RESOURCES_PREFIX + translatedFilename)) {
                Log._Debug($"Translated file {translatedFilename} found");
                return translatedFilename;
            }

            if (!DEFAULT_LANGUAGE_CODE.Equals(language)) {
                Log.Warning($"Translated file {translatedFilename} not found!");
            }

            return filename;
        }

        private static string GetCurrentLanguage() {
            string lang = GlobalConfig.Instance.LanguageCode;
            return lang ?? LocaleManager.instance.language;
        }

        // Not used
        [UsedImplicitly]
        public static void SetCurrentLanguage(string lang) {
            if (lang != null && !LanguageLabels.ContainsKey(lang)) {
                Log.Warning($"Translation.SetCurrentLanguage: Invalid language code: {lang}");
                return;
            }

            GlobalConfig.Instance.LanguageCode = lang;
        }

        private static void LoadTranslations() {
            string currentLang = GetCurrentLanguage();

            if (_translations == null || _loadedLanguage == null || ! _loadedLanguage.Equals(currentLang)) {
                try {
                    string filename = RESOURCES_PREFIX + GetTranslatedFileName(DEFAULT_TRANSLATION_FILENAME);
                    Log._Debug($"Loading translations from file '{filename}'. Language={currentLang}");
                    string[] lines;

                    using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream(filename)) {
                        using (var sr = new StreamReader(st)) {
                            lines = sr.ReadToEnd().Split(new string[] { "\n", "\r\n" }, StringSplitOptions.None);
                        }
                    }

                    _translations = new Dictionary<string, string>();
                    foreach (string line in lines) {
                        if (line == null || line.Trim().Length == 0) {
                            continue;
                        }

                        int delimiterIndex = line.Trim().IndexOf(' ');

                        if (delimiterIndex > 0) {
                            try {
                                string translationKey = line.Substring(0, delimiterIndex);
                                string translationValue =
                                    line.Substring(delimiterIndex + 1).Trim().Replace("\\n", "\n");
                                _translations.Add(translationKey, translationValue);
                            }
                            catch (Exception) {
                                Log.WarningFormat(
                                    "Failed to add translation for key {0}, language {1}. Possible duplicate?",
                                    line.Substring(0, delimiterIndex),
                                    currentLang);
                            }
                        }
                    }

                    _loadedLanguage = currentLang;
                } catch (Exception e) {
                    Log.Error($"Error while loading translations: {e}");
                }
            }
        }

        public static void ReloadTutorialTranslations() {
            Locale locale = (Locale)typeof(LocaleManager)
                                    .GetField(
                                        "m_Locale",
                                        BindingFlags.Instance | BindingFlags.NonPublic)
                                    ?.GetValue(SingletonLite<LocaleManager>.instance);

            foreach (KeyValuePair<string, string> entry in _translations) {
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

                // Log._Debug($"Adding tutorial translation for id {identifier}, key={tutorialKey}
                //     value={entry.Value}");
                Locale.Key key = new Locale.Key() {
                    m_Identifier = identifier,
                    m_Key = tutorialKey
                };

                if (locale != null && !locale.Exists(key)) {
                    locale.AddLocalizedString(key, entry.Value);
                }
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

        internal static void OnLevelUnloading() {
            _translations = null;
        }
    }
}