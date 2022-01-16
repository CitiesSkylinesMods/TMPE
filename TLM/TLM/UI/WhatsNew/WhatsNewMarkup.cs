namespace TrafficManager.UI.WhatsNew {
    using System.Collections.Generic;
    using CSUtil.Commons;
    using UnityEngine;

    public static class WhatsNewMarkup {
        public static readonly Dictionary<string, MarkupKeyword> MarkupKeywords = new() {
            { "[Version]", MarkupKeyword.VersionStart },
            { "[/Version]", MarkupKeyword.VersionEnd },
            { "[Link]", MarkupKeyword.Link },
            { "[Released]", MarkupKeyword.Released },
            { "[New]", MarkupKeyword.New },
            { "[Fixed]", MarkupKeyword.Fixed },
            { "[Updated]", MarkupKeyword.Updated },
            { "[Removed]", MarkupKeyword.Removed },
        };

        public static readonly Dictionary<MarkupKeyword, string> MarkupKeywordsString = new() {
            { MarkupKeyword.VersionStart, "[Version]" },
            { MarkupKeyword.VersionEnd, "[/Version]" },
            { MarkupKeyword.Link, "[Link]" },
            { MarkupKeyword.Released, "[Released]" },
            { MarkupKeyword.New, "[New]" },
            { MarkupKeyword.Fixed, "[Fixed]" },
            { MarkupKeyword.Updated, "[Updated]" },
            { MarkupKeyword.Removed, "[Removed]" },
        };

        public static readonly Color32 FixedOrUpdatedColor = new Color32(3,102,214, 255);
        public static readonly Color32 NewOrAddedColor = new Color32(40,167,69, 255);
        public static readonly Color32 RemovedColor = new Color32(215,58,73, 255);
        public static readonly Color32 VersionColor = new Color32(111,66,193, 255);

        public static Color32 GetColor(MarkupKeyword keyword) {
            switch (keyword) {
                case MarkupKeyword.Fixed:
                    return FixedOrUpdatedColor;
                case MarkupKeyword.New:
                    return NewOrAddedColor;
                case MarkupKeyword.Removed:
                    return RemovedColor;
                case MarkupKeyword.Updated:
                    return FixedOrUpdatedColor;
                case MarkupKeyword.VersionStart:
                case MarkupKeyword.VersionEnd:
                    return VersionColor;
                default:
                    Log.Warning($"No custom color for markup keyword: {keyword}");
                    return Color.white;
            }
        }


    }
}