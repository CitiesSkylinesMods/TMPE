namespace TrafficManager.UI.MainMenu.OSD {
    using System;
    using System.Collections.Generic;
    using System.Text;
    using ColossalFramework;
    using ColossalFramework.UI;
    using HarmonyLib;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;

    /// <summary>
    /// Displays Modifier key combination in OSD panel.
    /// This is used to tell the user to hold some Alt, Ctrl, Shift key or combination of thereof.
    /// </summary>
    public class HoldModifier : OsdItem {
        private readonly bool shift_;
        private readonly bool ctrl_;
        private readonly bool alt_;
        private readonly string localizedText_;

        public HoldModifier(string localizedText,
                            bool shift = false,
                            bool ctrl = false,
                            bool alt = false) {
            shift_ = shift;
            ctrl_ = ctrl;
            alt_ = alt;
            localizedText_ = localizedText;
        }

        public override void Build(U.UiBuilder<U.UPanel> builder) {
            // Capacity 4 will fit color tags and modifier string and localised text
            var text = new StringBuilder(capacity: 4);
            var modifierStrings = new List<string>(capacity: 3);

            text.Append($"<color {UConst.SHORTCUT_TEXT_HEX}>");

            if (this.shift_) {
                modifierStrings.Add(Translation.Options.Get("Shortcut.Modifier:Shift"));
            }

            if (this.ctrl_) {
                modifierStrings.Add(Translation.Options.Get("Shortcut.Modifier:Ctrl"));
            }

            if (this.alt_) {
                modifierStrings.Add(Translation.Options.Get("Shortcut.Modifier:Alt"));
            }

            text.Append(string.Join("+", modifierStrings.ToArray()));
            text.Append("</color> ");
            text.Append(this.localizedText_);

            builder.Label(t: text.ToString(), stack: UStackMode.NewRowBelow, processMarkup: true);
        }
    }
}