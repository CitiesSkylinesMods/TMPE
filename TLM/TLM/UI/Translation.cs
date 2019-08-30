// Enable DUMP_TRANSLATIONS to write all translations available in the game into a lang.csv file
// #define DUMP_TRANSLATIONS

namespace TrafficManager.UI {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using ColossalFramework;
    using ColossalFramework.Globalization;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using State;
    using Util;

    /// <summary>
    /// Adding a new language step by step:
    /// 1. In Crowdin: Go to languages and add a new language
    /// 2. Visit the Files menu and click "Edit Schema" on each file
    /// 3. In the file selection dialog provide the recently exported CSV file
    /// 4. The columns selection window appears, scroll right and select the new language in some column
    /// 5. In this file: <see cref="AvailableLanguageCodes"/> and <see cref="csv_columns_to_locales_"/>
    /// </summary>
    public class Translation {
        private const string DEFAULT_LANGUAGE_CODE = "en";

        public const string TUTORIAL_KEY_PREFIX = "TMPE_TUTORIAL_";
        private const string TUTORIAL_HEAD_KEY_PREFIX = TUTORIAL_KEY_PREFIX + "HEAD_";
        public const string TUTORIAL_BODY_KEY_PREFIX = TUTORIAL_KEY_PREFIX + "BODY_";
        private const string RESOURCES_PREFIX = "TrafficManager.Resources.";

        /// <summary>
        /// Defines column order in CSV, but not the dropdown order, the dropdown is just sorted.
        /// Mapping from translation languages (first row of CSV export) to language locales used in
        /// <see cref="AvailableLanguageCodes"/> above.
        /// </summary>
        private static readonly Dictionary<string, string> csv_columns_to_locales_
            = new Dictionary<string, string>() {
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

        /// <summary>
        /// Defines the order of language selector dropdown.
        /// </summary>
        public static readonly List<string> AvailableLanguageCodes;

        static Translation() {
            AvailableLanguageCodes = csv_columns_to_locales_.Values.ToList();
            AvailableLanguageCodes.Sort();
        }

        /// <summary>
        /// Stores all languages (first key), and for each language stores translations
        /// (indexed by the second key in the value)
        /// </summary>
        private Dictionary<string, Dictionary<string, string>> translations_;

        public void LoadAllTranslations() {
            // Load all languages together
            translations_ = new Dictionary<string, Dictionary<string, string>>();

            // Load all translations CSV file with UTF8
            const string FILENAME = RESOURCES_PREFIX + "Translations.all_translations.csv";
            string[] lines;
            using (Stream st = Assembly.GetExecutingAssembly()
                                       .GetManifestResourceStream(FILENAME)) {
                using (var sr = new StreamReader(st, Encoding.UTF8)) {
                    lines = sr.ReadToEnd()
                              .Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
                }
            }

            // Read each line as CSV line
            // Read language order in the first line
            string firstLine = lines[0];
            var languageCodes = new List<string>();
            using (var sr = new StringReader(firstLine)) {
                ReadCsvCell(sr); // skip
                while (true) {
                    string langName = ReadCsvCell(sr);
                    if (langName.Length == 0 || !csv_columns_to_locales_.ContainsKey(langName)) {
                        break;
                    }
                    languageCodes.Add(csv_columns_to_locales_[langName]);
                }
            }

            // Initialize empty dicts for each language
            foreach (string lang in languageCodes) {
                translations_[lang] = new Dictionary<string, string>();
            }

            // first column is the translation key
            // Following columns are languages, following the order in AvailableLanguageCodes
            foreach (string line in lines.Skip(1)) {
                using (var sr = new StringReader(line)) {
                    string key = ReadCsvCell(sr);
                    if (key.Length == 0) {
                        break; // last line is empty
                    }
                    foreach (string lang in languageCodes) {
                        string cell = ReadCsvCell(sr);
                        translations_[lang][key] = cell;
                    }
                }
            }

#if DUMP_TRANSLATIONS
            DumpTranslationsToCsv();
#endif
        }

        /// <summary>
        /// Given a stringReader, read a CSV cell which can be a string until next comma, or quoted
        /// string (in this case double quotes are decoded to a quote character) and respects
        /// newlines \n too.
        /// </summary>
        /// <param name="sr">Source for reading CSV</param>
        /// <returns>Cell contents</returns>
        private static string ReadCsvCell(StringReader sr) {
            var sb = new StringBuilder();
            if (sr.Peek() == '"') {
                sr.Read(); // skip the leading \"

                // The cell begins with a \" character, special reading rules apply
                while (true) {
                    int next = sr.Read();
                    if (next == -1) {
                        break; // end of the line
                    }

                    switch (next) {
                        case '\\': {
                            int special = sr.Read();
                            if (special == 'n') {
                                // Recognized a new line
                                sb.Append("\n");
                            } else {
                                // Not recognized, append as is
                                sb.Append("\\");
                                sb.Append((char)special, 1);
                            }

                            break;
                        }
                        case '\"': {
                            // Found a '""', or a '",'
                            int peek = sr.Peek();
                            switch (peek) {
                                case '\"': {
                                    sr.Read(); // consume the double quote
                                    sb.Append("\"");
                                    break;
                                }
                                case ',':
                                case -1: {
                                    // Followed by a comma or end-of-string
                                    sr.Read(); // Consume the comma
                                    return sb.ToString();
                                }
                                default: {
                                    // Followed by a non-comma, non-end-of-string
                                    sb.Append("\"");
                                    break;
                                }
                            }
                            break;
                        }
                        default: {
                            sb.Append((char)next, 1);
                            break;
                        }
                    }
                }
            } else {
                // Simple reading rules apply, read to the next comma or end-of-string
                while (true) {
                    int next = sr.Read();
                    if (next == -1 || next == ',') {
                        break; // end-of-string or a comma
                    }

                    sb.Append((char)next, 1);
                }
            }

            return sb.ToString();
        }

        // Write translations to a CSV
        [Conditional("DUMP_TRANSLATIONS")]
        private void DumpTranslationsToCsv() {
            string Quote(string s) {
                return s.Replace("\"", "\"\"")
                        .Replace("\n", "\\n")
                        .Replace("\t", "\\t");
            }

            var sb = new StringBuilder();
            sb.Append("Original,");
            foreach (string lang in AvailableLanguageCodes) {
                sb.Append($"{lang},");
            }

            sb.Append("\n");

            foreach (KeyValuePair<string, string> englishStr in translations_[DEFAULT_LANGUAGE_CODE]) {
                sb.Append($"\"{Quote(englishStr.Key)}\",");
                foreach (string lang in AvailableLanguageCodes) {
                    sb.Append(
                        translations_[lang].TryGetValue(englishStr.Key, out string localizedStr)
                            ? $"\"{Quote(localizedStr)}\","
                            : ",");
                }

                sb.Append("\n");
            }

            File.WriteAllText("lang.csv", sb.ToString(), Encoding.UTF8);
        }

        public static string GetString(string key) {
            Translation self = LoadingExtension.Translator;
            string lang = GetCurrentLanguage();
            return self.Get_(lang, key);
        }

        public static string Get(string lang, string key) {
            Translation self = LoadingExtension.Translator;
            return self.Get_(lang, key);
        }

        private string Get_(string lang, string key) {
            if (!translations_.ContainsKey(lang)) {
                Log.Error($"Translation: Unknown language {lang}");
                return key;
            }

            // Try find translation in the current language first
            if (translations_[lang].TryGetValue(key, out string ret))
            {
                return ret;
            }

            // If not found, try also get translation in the default English
            return translations_[DEFAULT_LANGUAGE_CODE].TryGetValue(key, out string ret2)
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
            if (lang != null && !AvailableLanguageCodes.Contains(lang)) {
                Log.Warning($"Translation.SetCurrentLanguage: Invalid language code: {lang}");
                return;
            }

            GlobalConfig.Instance.LanguageCode = lang;
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

                // Log._Debug($"Adding tutorial translation for id {identifier}, key={tutorialKey} value={entry.Value}");

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