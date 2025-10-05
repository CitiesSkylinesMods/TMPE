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
        // bump and update what's new changelogs when new features added
        public static readonly Version CurrentVersion = new Version(11, 9, 3, 0);

        private const string WHATS_NEW_FILE = "whats_new.txt";
        private const string RESOURCES_PREFIX = "TrafficManager.Resources.";

        public WhatsNew() {
            LoadChangelogs();
        }

        [CanBeNull]
        public static Version PreviouslySeenVersion {
            get => GlobalConfig.Instance.Main.LastWhatsNewPanelVersion;
            set {
                if (value > GlobalConfig.Instance.Main.LastWhatsNewPanelVersion) {
                    Log.Info($"What's New: LastWhatsNewPanelVersion = {value}");
                    GlobalConfig.Instance.Main.LastWhatsNewPanelVersion = value;
                    GlobalConfig.WriteConfig();
                }
            }
        }

        /// <summary>
        /// Return info if What's new panel was shown in current version
        /// </summary>
        public bool Shown => CurrentVersion <= PreviouslySeenVersion;

        public List<Changelog> Changelogs { get; private set; }

        public static void OpenModal() {
            UIView uiView = UIView.GetAView();
            if (uiView) {
                MarkAsShown();

                WhatsNewPanel panel = uiView.AddUIComponent(typeof(WhatsNewPanel)) as WhatsNewPanel;
                if (panel) {
                    Log.Info("Opened What's New panel!");
                    UIView.PushModal(panel);
                    panel.CenterToParent();
                    panel.BringToFront();
                }
            }
        }

        public static void MarkAsShown() {
            Log.Info($"What's New - mark as shown. Version {CurrentVersion}");
            PreviouslySeenVersion = CurrentVersion;
        }

        private void LoadChangelogs() {
            Log.Info("Loading What's New changelogs...");
            string[] lines;
            using (Stream st = Assembly.GetExecutingAssembly()
                                       .GetManifestResourceStream(RESOURCES_PREFIX + WHATS_NEW_FILE))
            {
                using (var sr = new StreamReader(st)) {
                    lines = sr.ReadToEnd().Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
                }

                Changelogs = ParseChangelogs(lines);
            }
            Log.Info($"Loaded {Changelogs.Count} What's New changelogs");
        }

        /// <summary>
        /// Parses the changelogs in <c>whats_new.txt</c>.
        /// </summary>
        /// <param name="lines">The contents of <c>whats_new.txt</c>.</param>
        /// <returns>A list of <see cref="Changelog"/>.</returns>
        /// <exception cref="FormatException">
        /// Version blocks must contain only one <c>[Version]</c> tag
        /// and must end with a <c>[/Version]</c> tag.
        /// </exception>
        /// <exception cref="IndexOutOfRangeException">
        /// Ensure each version block ends with <c>[/Version]</c>.
        /// </exception>
        /// <remarks><seealso cref="https://github.com/CitiesSkylinesMods/TMPE/wiki/Changelogs#whats-new-panel"/> .</remarks>
        private static List<Changelog> ParseChangelogs(string[] lines) {
            var changelogs = new List<Changelog>();
            int i = 0;

            while (i < lines.Length) {
                string line = lines[i];

                if (TryParseKeyword(line, out MarkupKeyword lineKeyword, out string text)
                    && lineKeyword == MarkupKeyword.VersionStart) {

                    var changelog = new Changelog() {
                        // version text can be 1.2.3.4 or 1.2.3.4-hotfix-<number>
                        Version = new Version(text.Split('-')[0]),
                    };
                    var items = new List<Changelog.Item>();

                    // Parse contents of [Version]..[/Version] block
                    TryParseKeyword(lines[++i], out lineKeyword, out text);
                    while (lineKeyword != MarkupKeyword.VersionEnd) {

                        // Log._Debug($"Keyword {lineKeyword}, Text: {text}");
                        switch (lineKeyword) {
                            case MarkupKeyword.VersionStart:
                                throw new FormatException($"whats_new.txt line {i}: Unexpected '[Version]' tag.");
                            case MarkupKeyword.Stable:
                                changelog.Stable = true;
                                break;
                            case MarkupKeyword.Link:
                                changelog.Link = text;
                                break;
                            case MarkupKeyword.Released:
                                changelog.Released = text;
                                break;
                            case MarkupKeyword.Unknown:
                                // skip unknown entries
                                Log.Warning($"whats_new.txt line {i}: Unrecognised entry '{line}'");
                                break;
                            default:
                                items.Add(
                                    new Changelog.Item() {
                                        Keyword = lineKeyword,
                                        Text = text,
                                    });
                                break;
                        }

                        TryParseKeyword(lines[++i], out lineKeyword, out text);
                    }

                    changelog.Items = items.ToArray();
                    Array.Sort(changelog.Items, Changelog.Item.KeywordComparer);
                    changelogs.Add(changelog);

                    // If user already seen this version, don't bother parsing remainder of file
                    if (changelog.Version <= PreviouslySeenVersion) {
                        break;
                    }
                }

                i++;
            }

            return changelogs;
        }

        private static bool TryParseKeyword(string line, out MarkupKeyword keyword, out string text) {
            if ((!string.IsNullOrEmpty(line)) && line.StartsWith("[")) {
                int pos = line.IndexOf("]") + 1;
                string tag = line.Substring(0, pos);

                keyword = tag.ToKeyword();
                text = line.Substring(pos).Trim();

                return keyword != MarkupKeyword.Unknown;
            }

            keyword = MarkupKeyword.Unknown;
            text = line;
            return false;
        }
    }

    /// <summary>
    /// Represents the changelog for a release (ie. <c>[Version]..[/Version]</c> block).
    /// </summary>
    public class Changelog {
        public Version Version { get; set; }
        public bool Stable { get; set; }
        [CanBeNull]
        public string Link { get; set; }
        [CanBeNull]
        public string Released { get; set; }
        public Item[] Items { get; set; }

        public struct Item {
            public MarkupKeyword Keyword;
            public string Text;

            public static IComparer<Item> KeywordComparer { get; } = new KeywordRelationalComparer();

            private sealed class KeywordRelationalComparer : IComparer<Item> {
                public int Compare(Item x, Item y) {
                    return x.Keyword.CompareTo(y.Keyword);
                }
            }
        }
    }
}