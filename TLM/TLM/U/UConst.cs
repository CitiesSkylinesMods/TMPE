namespace TrafficManager.U {
    using UnityEngine;

    public static class UConst {
        // private static readonly Color SHORTCUT_DESCR_TEXT = new Color(.75f, .75f, .75f, 1f);
        // public static readonly Color SHORTCUT_KEYBIND_TEXT = new Color(.8f, .6f, .3f, 1f);

        /// <summary>Orange color for shortcut text.</summary>
        public static readonly string SHORTCUT_TEXT_HEX = "#cc994c";

        /// <summary>
        /// Default padding value does not scale with screen resolution, and is used in new U GUI
        /// forms for spacing between elements and around container borders.
        /// </summary>
        public const float UIPADDING = 4f;
    }
}