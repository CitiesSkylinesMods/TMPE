namespace TrafficManager.UI.WhatsNew {
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using CSUtil.Commons;
    using UnityEngine;

    [SuppressMessage("Usage", "RAS0002:Readonly field for a non-readonly struct", Justification = "Not performance critical.")]
    public static class WhatsNewMarkup {
        public static MarkupKeyword ToKeyword(this string token) {
            if (MarkupKeywords.TryGetValue(token, out MarkupKeyword keyword)) {
                return keyword;
            }
            return MarkupKeyword.Unknown;
        }

        public static Color32 ToColor(this MarkupKeyword keyword) {
            return GetColor(keyword);
        }

        private static readonly Dictionary<string, MarkupKeyword> MarkupKeywords = new() {
            { "[Version]", MarkupKeyword.VersionStart },
            { "[/Version]", MarkupKeyword.VersionEnd },
            { "[Stable]", MarkupKeyword.Stable },
            { "[Link]", MarkupKeyword.Link },
            { "[Released]", MarkupKeyword.Released },
            { "[Meta]", MarkupKeyword.Meta },
            { "[New]", MarkupKeyword.New },
            { "[Mod]", MarkupKeyword.Mod },
            { "[Fixed]", MarkupKeyword.Fixed },
            { "[Updated]", MarkupKeyword.Updated },
            { "[Removed]", MarkupKeyword.Removed },
        };

        private static readonly Color32 Red = new (224, 61, 76, 255);
        private static readonly Color32 Amber = new (255, 196, 0, 255);
        private static readonly Color32 Green = new (40, 178, 72, 255);
        private static readonly Color32 Blue = new (3, 106, 225, 255);
        private static readonly Color32 Purple = new (119, 69, 204, 255);

        private static Color32 GetColor(MarkupKeyword keyword) {
            switch (keyword) {
                case MarkupKeyword.Fixed:
                    return Blue;
                case MarkupKeyword.New:
                    return Green;
                case MarkupKeyword.Mod:
                    return Amber;
                case MarkupKeyword.Removed:
                    return Red;
                case MarkupKeyword.Updated:
                    return Blue;
                case MarkupKeyword.VersionStart:
                case MarkupKeyword.VersionEnd:
                case MarkupKeyword.Stable:
                case MarkupKeyword.Meta:
                    return Purple;
                default:
                    Log.Warning($"No custom color for markup keyword: {keyword}");
                    return Color.white;
            }
        }
    }
}