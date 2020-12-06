namespace TrafficManager.UI.MainMenu.OSD {
    using System.Collections.Generic;
    using System.Text;
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

        public override void Build(U.UiBuilder<U.UPanel> builder) {
            StringBuilder text = new StringBuilder();
            List<string> keybindStrings = this.keybindSetting_.ToLocalizedStringList();
            bool firstShortcut = true; // tracking | separators between multiple keybinds

            using (UiBuilder<ULabel> labelB = builder.Label<U.ULabel>(string.Empty)) {
                labelB.Control.processMarkup = true;
                labelB.ResizeFunction(
                    r => {
                        r.Stack(mode: UStackMode.NewRowBelow);
                    });

                foreach (string keybindStr in keybindStrings) {
                    if (!firstShortcut) {
                        text.Append("| ");
                    } else {
                        firstShortcut = false;
                    }

                    text.Append($"<color {UConst.SHORTCUT_TEXT_HEX}>{keybindStr}</color>");
                }

                text.Append(" ");
                text.Append(this.localizedText_);
                labelB.Control.text = text.ToString();
            }
        }
    }
}