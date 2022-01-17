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
            { "[Mod]", MarkupKeyword.Mod },
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
            { MarkupKeyword.Mod, "[Mod]" },
            { MarkupKeyword.Fixed, "[Fixed]" },
            { MarkupKeyword.Updated, "[Updated]" },
            { MarkupKeyword.Removed, "[Removed]" },
        };

        public static readonly Color32 ModColor = new Color32(255, 196, 0, 255);
        public static readonly Color32 FixedOrUpdatedColor = new Color32(3, 106, 225, 255);
        public static readonly Color32 NewOrAddedColor = new Color32(40, 178, 72, 255);
        public static readonly Color32 RemovedColor = new Color32(224, 61, 76, 255);
        public static readonly Color32 VersionColor = new Color32(119, 69, 204, 255);

        public static Color32 GetColor(MarkupKeyword keyword) {
            switch (keyword) {
                case MarkupKeyword.Fixed:
                    return FixedOrUpdatedColor;
                case MarkupKeyword.New:
                    return NewOrAddedColor;
                case MarkupKeyword.Mod:
                    return ModColor;
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