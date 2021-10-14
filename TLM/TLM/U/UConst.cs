namespace TrafficManager.U {
    using UnityEngine;

    public static class UConst {
        internal static Color UI_ORANGE = new Color(0xcc / 255f, 0x99 / 255f,0x4c / 255f);

        /// <summary>Orange color for shortcut text.</summary>
        private const string SHORTCUT_TEXT_HEX = "#cc994c";

        public const string KEYBOARD_SHORTCUT_CLOSING_TAG = "</color>";

        /// <summary>
        /// Default padding value does not scale with screen resolution, and is used in new U GUI
        /// forms for spacing between elements and around container borders.
        /// </summary>
        public const float UIPADDING = 4f;

        /// <summary>
        /// Opening color tag for keyboard shortcuts. Use <see cref="KEYBOARD_SHORTCUT_CLOSING_TAG"/> to close.
        /// </summary>
        public static string GetKeyboardShortcutColorTagOpener() {
            return $"<color {SHORTCUT_TEXT_HEX}>";
        }

        /// <summary>
        /// Use this as backgroundPrefix for button skins to have that nice round background with
        /// blue/ hover, light blue active and light grey disabled circle, like the
        /// main menu is using.
        /// </summary>
        public const string MAINMENU_ROUND_BUTTON_BG = "/MainMenu.Tool.RoundButton";
    }
}