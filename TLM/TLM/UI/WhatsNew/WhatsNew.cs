namespace TrafficManager.UI.WhatsNew {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using ColossalFramework.UI;
    using CSUtil.Commons;

    public class WhatsNew {
#if TEST || DEBUG
        private const string INCOMPATIBLE_MODS_FILE = "whats_new_development.txt";
#else
        private const string INCOMPATIBLE_MODS_FILE = "whats_new_stable.txt";
#endif
        private const string RESOURCES_PREFIX = "TrafficManager.Resources.";
        internal const string VERSION_START = "[version]";
        internal const string VERSION_NUMBER = "[number]";
        internal const string VERSION_TITLE = "[title]";
        internal const string BULLET_POINT = "[*]";

        // bump and update what's new changelogs when new features added
        internal static readonly Version CurrentVersion = new Version(11,6,2, 0);

        internal bool Shown { get; private set; }
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
                }
            }
        }

        public void MarkAsShown() {
            Log.Info("What's New - mark as shown");
            Shown = true;
        }


        private void LoadChangelog() {
            Log.Info("Loading What's New changelogs...");
            string[] lines;
            using (Stream st = Assembly.GetExecutingAssembly()
                                       .GetManifestResourceStream(RESOURCES_PREFIX + INCOMPATIBLE_MODS_FILE))
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
        public string Title { get; private set; }
        public string[] BulletPoints { get; private set; }

        public static List<ChangelogEntry> ParseChangelogs(string[] lines) {
            List<ChangelogEntry> entries = new List<ChangelogEntry>();
            List<string> points = new List<string>();
            int i = 0;
            while (i < lines.Length) {
                string line = lines[i];
                if (line.StartsWith(WhatsNew.VERSION_START)) {
                    ChangelogEntry entry = new ChangelogEntry();
                    entry.Version = new Version(lines[++i].Substring(WhatsNew.VERSION_NUMBER.Length));
                    entry.Title = lines[++i].Substring(WhatsNew.VERSION_TITLE.Length);
                    i++;
                    points.Clear();
                    while (lines[i].StartsWith(WhatsNew.BULLET_POINT)) {
                        points.Add(lines[i++].Substring(WhatsNew.BULLET_POINT.Length));
                    }

                    entry.BulletPoints = points.ToArray();
                    entries.Add(entry);
                }

                i++;
            }

            return entries;
        }

    }
}