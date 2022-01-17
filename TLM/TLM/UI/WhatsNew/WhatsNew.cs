namespace TrafficManager.UI.WhatsNew {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using Lifecycle;
    using State;

    public class WhatsNew {
#if TEST || DEBUG
        private const string WHATS_NEW_FILE = "whats_new_development.txt";
#else
        private const string WHATS_NEW_FILE = "whats_new_stable.txt";
#endif
        private const string RESOURCES_PREFIX = "TrafficManager.Resources.";

        // bump and update what's new changelogs when new features added
        internal static readonly Version CurrentVersion = new Version(11,6,2, 0);

        internal bool Shown => CurrentVersion == GlobalConfig.Instance.Main.LastWhatsNewPanelVersion;
        public List<ChangelogEntry> Changelogs { get; private set; }

        public WhatsNew() {
            LoadChangelog();
        }

        public static void OpenModal() {
            UIView uiView = UIView.GetAView();
            if (uiView) {
                WhatsNewPanel panel = uiView.AddUIComponent(typeof(WhatsNewPanel)) as WhatsNewPanel;
                if (panel) {
                    Log.Info("Opened What's New panel!");
                    UIView.PushModal(panel);
                    panel.CenterToParent();
                    panel.BringToFront();

                    if (TMPELifecycle.PlayMode) {
                        SimulationManager.instance.SimulationPaused = true;
                    }
                }
            }
        }

        public void MarkAsShown() {
            Log.Info($"What's New - mark as shown. Version {CurrentVersion}");
            GlobalConfig.Instance.Main.LastWhatsNewPanelVersion = CurrentVersion;
            GlobalConfig.WriteConfig();
        }


        private void LoadChangelog() {
            Log.Info("Loading What's New changelogs...");
            string[] lines;
            using (Stream st = Assembly.GetExecutingAssembly()
                                       .GetManifestResourceStream(RESOURCES_PREFIX + WHATS_NEW_FILE))
            {
                using (var sr = new StreamReader(st)) {
                    lines = sr.ReadToEnd().Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
                }

                Changelogs = ChangelogEntry.ParseChangelogs(lines);
            }
            Log.Info($"Loaded {Changelogs.Count} What's New changelogs");
        }
    }

    public class ChangelogEntry {
        public Version Version { get; private set; }
        [CanBeNull]
        public string Link { get; private set; }
        [CanBeNull]
        public string Released { get; private set; }
        public ChangeEntry[] ChangeEntries { get; private set; }

        public static List<ChangelogEntry> ParseChangelogs(string[] lines) {
            List<ChangelogEntry> entries = new List<ChangelogEntry>();
            int i = 0;
            var keywordStrings = WhatsNewMarkup.MarkupKeywordsString;
            while (i < lines.Length) {
                string line = lines[i];

                if (TryParseKeyword(line, out MarkupKeyword lineKeyword) && lineKeyword == MarkupKeyword.VersionStart) {
                    ChangelogEntry changelog = new ChangelogEntry();
                    // read version
                    changelog.Version = new Version(lines[i++].Substring(keywordStrings[MarkupKeyword.VersionStart].Length).Trim());

                    //get next line keyword
                    TryParseKeyword(lines[i], out lineKeyword);
                    // parse to the end of version section
                    List<ChangeEntry> changeEntries = new List<ChangeEntry>();
                    while (lineKeyword != MarkupKeyword.VersionEnd) {
                        string text = lines[i].Substring(keywordStrings[lineKeyword].Length).Trim();
                        Log.Info($"Keyword {lineKeyword}, text: {text}");
                        switch (lineKeyword) {
                            case MarkupKeyword.Link:
                                changelog.Link = text;
                                break;
                            case MarkupKeyword.Released:
                                changelog.Released = text;
                                break;
                            case MarkupKeyword.Unknown:
                                //skip unknown entries
                                break;
                            default:
                                changeEntries.Add(
                                    new ChangeEntry() {
                                        Keyword = lineKeyword,
                                        Text = text
                                    });
                                break;
                        }

                        i++;
                        TryParseKeyword(lines[i], out lineKeyword);
                    }


                    changelog.ChangeEntries = changeEntries.ToArray();
                    Array.Sort(changelog.ChangeEntries, ChangeEntry.KeywordComparer);
                    entries.Add(changelog);
                }

                i++;
            }

            return entries;
        }

        private static bool TryParseKeyword(string line, out MarkupKeyword keyword) {
            if (!string.IsNullOrEmpty(line)) {
                if(line.StartsWith("[") &&
                    WhatsNewMarkup.MarkupKeywords.TryGetValue(
                        line.Substring(0, line.IndexOf("]") + 1),
                        out keyword)) {
                    return true;
                }
                Log.Warning($"Couldn't parse line \"{line}\"");
            }

            keyword = MarkupKeyword.Unknown;
            return false;
        }

        public struct ChangeEntry {
            public MarkupKeyword Keyword;
            public string Text;

            private sealed class KeywordRelationalComparer : IComparer<ChangeEntry> {
                public int Compare(ChangeEntry x, ChangeEntry y) {
                    return x.Keyword.CompareTo(y.Keyword);
                }
            }

            public static IComparer<ChangeEntry> KeywordComparer { get; } = new KeywordRelationalComparer();
        }
    }
}