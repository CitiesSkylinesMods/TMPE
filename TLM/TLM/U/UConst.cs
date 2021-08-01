namespace TrafficManager.U {
    public static class UConst {
        /// <summary>Orange color for shortcut text.</summary>
        public static readonly string SHORTCUT_TEXT_HEX = "#cc994c";

        public static string KEYBOARD_SHORTCUT_CLOSING_TAG = "</color>";

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
        public static string MAINMENU_ROUND_BUTTON_BG = "/MainMenu.Tool.RoundButton";
    }
}