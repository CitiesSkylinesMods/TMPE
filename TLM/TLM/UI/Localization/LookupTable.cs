// Enable DUMP_TRANSLATIONS to write all translations available in the game into a lang.csv file
// #define DUMP_TRANSLATIONS

namespace TrafficManager.UI.Localization {
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using CSUtil.Commons;
    using TrafficManager.U;
    using TrafficManager.Util;

    public class LookupTable {
        public LookupTable(string lookupTableName) {
            Name = lookupTableName;
            Load();
        }

        public string Get(string key) {
            string lang = Translation.GetCurrentLanguage();
            return Get(lang, key);
        }

        /// <summary>Get a translation string and paint [[Key]] fragments with orange.</summary>
        /// <param name="key">Translation key.</param>
        /// <returns>Translated and colorized string.</returns>
        public string ColorizeKeybind(string key) {
            string lang = Translation.GetCurrentLanguage();
            return UIUtil.ColorizeKeybind(this.Get(lang, key));
        }

        /// <summary>Get a translation string and dynamically find, replace and paint [[Key]] fragments with orange.</summary>
        /// <param name="key">Translation key.</param>
        /// <returns>Translated and colorized string.</returns>
        public string ColorizeDynamicKeybinds(string key, string[] replacements) {
            string lang = Translation.GetCurrentLanguage();
            return UIUtil.ColorizeDynamicKeybinds(this.Get(lang, key), replacements);
        }

        public string Get(string lang, string key) {
#if DEBUG
            if (!AllLanguages.ContainsKey(lang)) {
                Log.Error($"Translation: Unknown language {lang}");
                return key;
            }
#endif

            // Current language
            if (AllLanguages[lang].TryGetValue(key, out string ret)) {
                return ret;
            }

            // Default language
            if (AllLanguages[Translation.DEFAULT_LANGUAGE_CODE].TryGetValue(key, out string ret2)) {
                return ret2;
            }

#if DEBUG
            // Prefixed locale key
            return "¶" + key;
#else
            // Trimmed locale key
            int pos = key.IndexOf(":");
            return pos > 0 ? key.Substring(pos + 1) : key;
#endif
        }

        public bool HasString(string key) {
            string lang = Translation.GetCurrentLanguage();

            // Assume the language always exists in self.translations, so only check the string
            return AllLanguages[lang].ContainsKey(key);
        }

        private string Name { get; }

        /// <summary>
        /// Stores all languages (first key), and for each language stores translations
        /// (indexed by the second key in the value)
        /// </summary>
        internal Dictionary<string, Dictionary<string, string>> AllLanguages;

        private void Load() {
            // Load all languages together
            AllLanguages = new Dictionary<string, Dictionary<string, string>>();

            // Load all translations CSV file with UTF8
            string filename = $"{Translation.RESOURCES_PREFIX}Translations.{Name}.csv";
            Log.Info($"Loading translations: {filename}");

            string firstLine;
            string dataBlock;
            using (Stream st = Assembly.GetExecutingAssembly()
                                       .GetManifestResourceStream(filename)) {
                using (var sr = new StreamReader(st, Encoding.UTF8)) {
                    ReadLines(sr, out firstLine, out dataBlock);
                }
            }

            // Read each line as CSV line
            // Read language order in the first line
            var languageCodes = new List<string>();
            using (var sr = new StringReader(firstLine)) {
                ReadCsvCell(sr); // skip
                while (true) {
                    string langName = ReadCsvCell(sr);
                    if (langName.Length == 0) {
                        break;
                    }

                    // Language might be a full name, or might be a ready-to-use locale code
                    string langCode = Translation.CsvColumnsToLocales.ContainsKey(langName)
                                          ? Translation.CsvColumnsToLocales[langName]
                                          : langName.ToLower();
                    languageCodes.Add(langCode);
                }
            }

            CollectTranslations(dataBlock, languageCodes, out AllLanguages);

#if DUMP_TRANSLATIONS
            DumpTranslationsToCsv();
#endif
            Log._Debug($"Loaded {AllLanguages.Count} different languages for {Name}");
        }

        /// <summary>
        /// Collects translations to map - collection of keyValue pairs for each language code
        /// </summary>
        /// <param name="dataBlock">block of data (all rows excluding first)</param>
        /// <param name="languageCodes">list of language codes</param>
        /// <param name="allLanguages">result dictionary where all translation string will be collected</param>
        private static void CollectTranslations(string dataBlock,
                                                List<string> languageCodes,
                                                out Dictionary<string, Dictionary<string, string>> allLanguages) {
            allLanguages = new Dictionary<string, Dictionary<string, string>>();
            // Initialize empty dicts for each language
            foreach (string lang in languageCodes) {
                allLanguages[lang] = new Dictionary<string, string>();
            }

            // first column is the translation key
            // Following columns are languages, following the order in AvailableLanguageCodes
            using (var sr = new StringReader(dataBlock)) {
                while (true) {
                    string key = ReadCsvCell(sr);
                    if (key.Length == 0) {
                        break; // last line is empty
                    }

                    foreach (string lang in languageCodes) {
                        string cell = ReadCsvCell(sr);
                        // Empty translations are not accepted for all languages other than English
                        // We don't load those keys
                        if (string.IsNullOrEmpty(cell) &&
                            lang != Translation.DEFAULT_LANGUAGE_CODE) {
                            continue;
                        }

                        allLanguages[lang][key] = cell;
                    }
                }
            }
        }

        /// <summary>
        /// Split stream of data on first row and remaining dataBlock
        /// </summary>
        /// <param name="sr">stream to read from</param>
        /// <param name="firstLine">first line of translation - row with language code names</param>
        /// <param name="dataBlock">string block of data (all remaining lines)</param>
        private static void ReadLines(StreamReader sr, out string firstLine, out string dataBlock) {
            firstLine = sr.ReadLine();
            dataBlock = sr.ReadToEnd();
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
                            // Found a '""', or a '",', or a '"/r', or a '"/r'
                            int peek = sr.Peek();
                            switch (peek) {
                                case '\"': {
                                    sr.Read(); // consume the double quote
                                    sb.Append("\"");
                                    break;
                                }
                                case '\r':
                                    //Followed by a \r then \n or just \n - end-of-string
                                    sr.Read(); // consume double quote
                                    sr.Read(); // consume \r
                                    if (sr.Peek() == '\n') {
                                        sr.Read(); // consume \n
                                    }
                                    return sb.ToString();
                                case '\n':
                                case ',':
                                case -1: {
                                    // Followed by a comma or end-of-string
                                    sr.Read(); // Consume the comma or newLine(LF)
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
                // Simple reading rules apply, read to the next comma, LF sequence or end-of-string
                while (true) {
                    int next = sr.Read();
                    if (next == -1 || next == ',' || next == '\n') {
                        break; // end-of-string, a newLine or a comma
                    }
                    if (next == '\r' && sr.Peek() == '\n') {
                        sr.Read(); //consume LF(\n) to complete CRLF(\r\n) newLine escape sequence
                        break;
                    }

                    sb.Append((char)next, 1);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Used only once to write out existing translations
        /// </summary>
        [Conditional("DUMP_TRANSLATIONS")]
        private void DumpTranslationsToCsv() {
            string Quote(string s) {
                return s.Replace("\"", "\"\"")
                        .Replace("\n", "\\n")
                        .Replace("\t", "\\t");
            }

            var sb = new StringBuilder();
            sb.Append("Original,");
            foreach (string lang in Translation.AvailableLanguageCodes) {
                sb.Append($"{lang},");
            }

            sb.Append("\n");

            foreach (KeyValuePair<string, string> englishStr
                in AllLanguages[Translation.DEFAULT_LANGUAGE_CODE])
            {
                sb.Append($"\"{Quote(englishStr.Key)}\",");
                foreach (string lang in Translation.AvailableLanguageCodes) {
                    sb.Append(
                        AllLanguages[lang].TryGetValue(englishStr.Key, out string localizedStr)
                            ? $"\"{Quote(localizedStr)}\","
                            : ",");
                }

                sb.Append("\n");
            }

            File.WriteAllText("lang.csv", sb.ToString(), Encoding.UTF8);
        }
    } // end class
}
