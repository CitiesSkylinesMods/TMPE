using ColossalFramework.Globalization;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TrafficManager.State;
using TrafficManager.Util;

namespace TrafficManager.UI {
	public class Translation {
		public static readonly IDictionary<string, string> LANGUAGE_LABELS = new TinyDictionary<string, string>();
		public static readonly IList<string> AVAILABLE_LANGUAGE_CODES = new List<string>();
		public const string DEFAULT_LANGUAGE_CODE = "en";

		static Translation() {
			AVAILABLE_LANGUAGE_CODES.Clear();
			AVAILABLE_LANGUAGE_CODES.Add("de");
			AVAILABLE_LANGUAGE_CODES.Add("en");
			AVAILABLE_LANGUAGE_CODES.Add("es");
			AVAILABLE_LANGUAGE_CODES.Add("fr");
			AVAILABLE_LANGUAGE_CODES.Add("it");
			AVAILABLE_LANGUAGE_CODES.Add("ja");
			AVAILABLE_LANGUAGE_CODES.Add("ko");
			AVAILABLE_LANGUAGE_CODES.Add("nl");
			AVAILABLE_LANGUAGE_CODES.Add("pl");
			AVAILABLE_LANGUAGE_CODES.Add("pt");
			AVAILABLE_LANGUAGE_CODES.Add("ru");
			AVAILABLE_LANGUAGE_CODES.Add("zh-tw");
			AVAILABLE_LANGUAGE_CODES.Add("zh");

			LANGUAGE_LABELS.Clear();
			LANGUAGE_LABELS["de"] = "Deutsch";
			LANGUAGE_LABELS["en"] = "English";
			LANGUAGE_LABELS["es"] = "Español";
			LANGUAGE_LABELS["fr"] = "Français";
			LANGUAGE_LABELS["it"] = "Italiano";
			LANGUAGE_LABELS["ja"] = "日本語";
			LANGUAGE_LABELS["ko"] = "한국의";
			LANGUAGE_LABELS["nl"] = "Nederlands";
			LANGUAGE_LABELS["pl"] = "Polski";
			LANGUAGE_LABELS["pt"] = "Português";
			LANGUAGE_LABELS["ru"] = "русский язык";
			LANGUAGE_LABELS["zh-tw"] = "中文 (繁體)";
			LANGUAGE_LABELS["zh"] = "中文 (简体)";
		}

		private const string RESOURCES_PREFIX = "TrafficManager.Resources.";
		private static readonly string DEFAULT_TRANSLATION_FILENAME = "lang.txt";

		private static Dictionary<string, string> translations;
		private static string loadedLanguage = null;

		public static string GetString(string key) {
			loadTranslations();

			string ret = null;
			try {
				translations.TryGetValue(key, out ret);
			} catch (Exception e) {
				Log.Error($"Error fetching the key {key} from the translation dictionary: {e.ToString()}");
                return key;
			}
			if (ret == null)
				return key;
			return ret;
		}

		public static string GetTranslatedFileName(string filename) {
			string language = GetCurrentLanguage();
			switch (language) {
				case "jaex":
					language = "ja";
					break;
				case "zh-cn":
					language = "zh";
					break;
				case "kr":
					language = "ko";
					break;
			}

			string translatedFilename = filename;
			if (language != DEFAULT_LANGUAGE_CODE) {
				int delimiterIndex = filename.Trim().LastIndexOf('.'); // file extension

				translatedFilename = "";
				if (delimiterIndex >= 0)
					translatedFilename = filename.Substring(0, delimiterIndex);
				translatedFilename += "_" + language.Trim().ToLower();
				if (delimiterIndex >= 0)
					translatedFilename += filename.Substring(delimiterIndex);
			}

			if (Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains(RESOURCES_PREFIX + translatedFilename)) {
				Log._Debug($"Translated file {translatedFilename} found");
                return translatedFilename;
			} else {
				if (language != null && !DEFAULT_LANGUAGE_CODE.Equals(language))
					Log.Warning($"Translated file {translatedFilename} not found!");
				return filename;
			}
		}

		public static string GetCurrentLanguage() {
			string lang = GlobalConfig.Instance.LanguageCode;
			if (lang != null) {
				return lang;
			}

			return LocaleManager.instance.language;
		}

		public static void SetCurrentLanguage(string lang) {
			if (lang != null && !LANGUAGE_LABELS.ContainsKey(lang)) {
				Log.Warning($"Translation.SetCurrentLanguage: Invalid language code: {lang}");
				return;
			}

			GlobalConfig.Instance.LanguageCode = lang;
		}

		private static void loadTranslations() {
			string currentLang = GetCurrentLanguage();
			if (translations == null || loadedLanguage == null || ! loadedLanguage.Equals(currentLang)) {
				try {
					string filename = RESOURCES_PREFIX + GetTranslatedFileName(DEFAULT_TRANSLATION_FILENAME);
					Log._Debug($"Loading translations from file '{filename}'. Language={currentLang}");
					string[] lines;
					using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream(filename)) {
						using (StreamReader sr = new StreamReader(st)) {
							lines = sr.ReadToEnd().Split(new string[] { "\n", "\r\n" }, StringSplitOptions.None);
						}
					}
					translations = new Dictionary<string, string>();
					foreach (string line in lines) {
						if (line == null || line.Trim().Length == 0) {
							continue;
						}
						int delimiterIndex = line.Trim().IndexOf(' ');
						if (delimiterIndex > 0) {
							try {
								translations.Add(line.Substring(0, delimiterIndex), line.Substring(delimiterIndex + 1).Trim().Replace("\\n", "\n"));
							} catch (Exception) {
								Log.Warning($"Failed to add translation for key {line.Substring(0, delimiterIndex)}, language {currentLang}. Possible duplicate?");
							}
						}
					}
					loadedLanguage = currentLang;
				} catch (Exception e) {
					Log.Error($"Error while loading translations: {e.ToString()}");
				}
			}
		}

		internal static int getMenuWidth() {
			switch (GetCurrentLanguage()) {
				case null:
				case "en":
				case "de":
				default:
					return 220;
				case "ru":
				case "pl":
					return 260;
				case "es":
				case "fr":
				case "it":
					return 240;
				case "nl":
					return 270;
			}
		}

		internal static void OnLevelUnloading() {
			translations = null;
		}
	}
}
