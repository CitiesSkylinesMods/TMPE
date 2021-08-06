namespace TrafficManager.UI.MainMenu.OSD {
    using System.Collections.Generic;
    using System.Text;
    using ColossalFramework.UI;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;

    /// <summary>
    /// Displays a keybind or dual keybind in the OSD panel.
    /// This is for customizable shortcuts or dual shortcuts.
    /// To display a hardcoded mouse click, use <see cref="HardcodedMouseShortcut"/>.
    /// </summary>
    public class Shortcut : OsdItem {
        private readonly KeybindSetting keybindSetting_;
        private readonly string localizedText_;

        public Shortcut(KeybindSetting keybindSetting, string localizedText) {
            keybindSetting_ = keybindSetting;
            localizedText_ = localizedText;
        }

        public override void Build(UIComponent parent,
                                   U.UBuilder builder) {
            StringBuilder text = new StringBuilder();
            List<string> keybindStrings = this.keybindSetting_.ToLocalizedStringList();
            bool firstShortcut = true; // tracking | separators between multiple keybinds

            foreach (string keybindStr in keybindStrings) {
                if (!firstShortcut) {
                    text.Append("| ");
                } else {
                    firstShortcut = false;
                }

                text.Append(UConst.GetKeyboardShortcutColorTagOpener());
                text.Append(keybindStr);
                text.Append(UConst.KEYBOARD_SHORTCUT_CLOSING_TAG);
            }

            text.Append(" ");
            text.Append(this.localizedText_);

            builder.Label<U.ULabel>(
                parent,
                t: text.ToString(),
                stack: UStackMode.NewRowBelow,
                processMarkup: true);
        }
    }
}