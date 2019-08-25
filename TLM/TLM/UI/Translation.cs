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

        /// <summary>
        /// Stores all languages (first key), and for each language stores translations
        /// (indexed by the second key in the value)
        /// </summary>
        private Dictionary<string, Dictionary<string, string>> translations_;

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

        public void LoadAllTranslations() {
            // Load all languages together
            translations_ = new Dictionary<string, Dictionary<string, string>>();
            foreach (string lang in AvailableLanguageCodes) {
                translations_[lang] = LoadLanguage(lang);
            }
        }

        public static string GetString(string key) {
            Translation self = LoadingExtension.Translator;
            string lang = GetCurrentLanguage();

            // Try find translation in the current language first
            if (self.translations_[lang].TryGetValue(key, out string ret)) {
                return ret;
            }

            // If not found, try also get translation in the default English
            return self.translations_[DEFAULT_LANGUAGE_CODE].TryGetValue(key, out string ret2)
                       ? ret2
                       : key;
        }

        public static bool HasString(string key) {
            Translation self = LoadingExtension.Translator;
            string lang = GetCurrentLanguage();

            // Assume the language always exists in self.translations, so only check the string
            return self.translations_[lang].ContainsKey(key);
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

        internal static string GetCurrentLanguage() {
            string lang = GlobalConfig.Instance.LanguageCode;

            // Having language code null means use the game language
            return lang ?? LocaleManager.instance.language;
        }

        // Not used
        [UsedImplicitly]
        public void SetCurrentLanguage(string lang) {
            if (lang != null && !LanguageLabels.ContainsKey(lang)) {
                Log.Warning($"Translation.SetCurrentLanguage: Invalid language code: {lang}");
                return;
            }

            GlobalConfig.Instance.LanguageCode = lang;
        }

        /// <summary>
        /// Load all translations available one by one. Each call loads one language.
        /// </summary>
        /// <param name="lang">Another language to load</param>
        /// <returns>A section of the translation dictionary, translations loaded for the
        ///     selected language</returns>
        private static Dictionary<string, string> LoadLanguage(string lang) {
            var result = new Dictionary<string, string>();
            try {
                string filename = RESOURCES_PREFIX
                                  + GetTranslatedFileName(DEFAULT_TRANSLATION_FILENAME, lang);
                Log._Debug($"Loading translations from file '{filename}'. Language={lang}");

                string[] lines;
                using (Stream st = Assembly.GetExecutingAssembly()
                                           .GetManifestResourceStream(filename)) {
                    using (var sr = new StreamReader(st)) {
                        lines = sr.ReadToEnd()
                                  .Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
                    }
                }

                foreach (string line in lines) {
                    if (line.Trim().Length == 0) {
                        continue;
                    }

                    // Try break the line on space, if no space - skip that line
                    int delimiterIndex = line.Trim().IndexOf(' ');
                    if (delimiterIndex <= 0) {
                        continue;
                    }

                    try {
                        // Try split the line into key and value
                        string translationKey = line.Substring(0, delimiterIndex);
                        string translationValue = line.Substring(delimiterIndex + 1)
                                                      .Trim()
                                                      .Replace("\\n", "\n");
                        result.Add(translationKey, translationValue);
                    }
                    catch (Exception) {
                        Log.WarningFormat(
                            "Failed to add translation for key {0}, lang={1}. Possible duplicate?",
                            line.Substring(0, delimiterIndex),
                            lang);
                    }
                }
            } catch (Exception e) {
                Log.Error($"Error while loading translations for lang={lang}: {e}");
            }

            return result;
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

            foreach (KeyValuePair<string, string> entry in translations_[lang]) {
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

                Log._Debug($"Adding tutorial translation for id {identifier}, key={tutorialKey} value={entry.Value}");

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