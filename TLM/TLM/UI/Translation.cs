using ColossalFramework.Globalization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TrafficManager.UI {
	public class Translation {
		const string RESOURCES_PREFIX = "TrafficManager.Resources.";
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
			string language = LocaleManager.instance.language;
			switch (language) {
				case "jaex":
					language = "ja";
					break;
				case "zh-cn":
					language = "zh";
					break;
			}

			string translatedFilename = filename;
			if (language != null) {
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
				if (language != null && !"en".Equals(language))
					Log.Warning($"Translated file {translatedFilename} not found!");
				return filename;
			}
		}

		private static void loadTranslations() {
			if (translations == null || loadedLanguage == null || ! loadedLanguage.Equals(LocaleManager.instance.language)) {
				try {
					string filename = RESOURCES_PREFIX + GetTranslatedFileName(DEFAULT_TRANSLATION_FILENAME);
					Log._Debug($"Loading translations from file '{filename}'. Language={LocaleManager.instance.language}");
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
								Log.Warning($"Failed to add translation for key {line.Substring(0, delimiterIndex)}, language {LocaleManager.instance.language}. Possible duplicate?");
							}
						}
					}
					loadedLanguage = LocaleManager.instance.language;
				} catch (Exception e) {
					Log.Error($"Error while loading translations: {e.ToString()}");
				}
			}
		}

		internal static int getMenuWidth() {
			switch (LocaleManager.instance.language) {
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
