namespace TrafficManager.UI.MainMenu.OSD {
    using System;
    using System.Collections.Generic;
    using System.Text;
    using ColossalFramework;
    using ColossalFramework.UI;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using TrafficManager.U.Label;

    /// <summary>
    /// Displays a mouse click shortcut in OSD panel.
    /// This is used for hardcoded mouse shortcuts. To display customizable shortcuts use the other
    /// class: <see cref="Shortcut"/>.
    /// </summary>
    public class HardcodedMouseShortcut : OsdItem {
        private readonly UIMouseButton button_;
        private readonly bool shift_;
        private readonly bool ctrl_;
        private readonly bool alt_;
        private readonly string localizedText_;
        private InputKey inputKey_;

        public HardcodedMouseShortcut(UIMouseButton button,
                            bool shift,
                            bool ctrl,
                            bool alt,
                            string localizedText) {
            button_ = button;
            shift_ = shift;
            ctrl_ = ctrl;
            alt_ = alt;
            localizedText_ = localizedText;
        }

        public override void Build(U.UiBuilder<U.Panel.UPanel> builder) {
            StringBuilder text = new StringBuilder();
            bool needSeparator = false;

            text.Append($"<color {UConst.SHORTCUT_TEXT_HEX}>");

            if (this.shift_) {
                text.Append(Translation.Options.Get("Shortcut.Modifier:Shift"));
                needSeparator = true;
            }
            if (this.ctrl_) {
                if (needSeparator) {
                    text.Append("+"); // separator required if shift is pressed
                } else {
                    needSeparator = true;
                }
                text.Append(Translation.Options.Get("Shortcut.Modifier:Ctrl"));
            }
            if (this.alt_) {
                if (needSeparator) {
                    text.Append("+");
                } else {
                    needSeparator = true;
                }
                text.Append(Translation.Options.Get("Shortcut.Modifier:Alt"));
            }

            if (needSeparator) {
                text.Append("+");
            }

            text.Append(TranslationForMouseButton(this.button_));

            text.Append("</color> ");
            text.Append(this.localizedText_);

            using (UiBuilder<ULabel> labelB = builder.Label<U.Label.ULabel>(text.ToString())) {
                labelB.Control.processMarkup = true;
                labelB.ResizeFunction(
                    r => { r.Stack(mode: UStackMode.NewRowBelow); });
            }
        }

        private static string TranslationForMouseButton(UIMouseButton button) {
            switch (button) {
                case UIMouseButton.Left:
                    return Translation.Options.Get("Shortcut:Click");
                case UIMouseButton.Right:
                    return Translation.Options.Get("Shortcut:RightClick");
                case UIMouseButton.Middle:
                    return Translation.Options.Get("Shortcut:MiddleClick");
                case UIMouseButton.Special0:
                    return "Special0";
                case UIMouseButton.Special1:
                    return "Special1";
                case UIMouseButton.Special2:
                    return "Special2";
                case UIMouseButton.Special3:
                    return "Special3";
                default:
                    throw new ArgumentOutOfRangeException(
                        paramName: nameof(button),
                        actualValue: button,
                        message: "Not supported click type for localization");
            }
        }
    }
}