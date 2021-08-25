namespace TrafficManager.UI.MainMenu.OSD {
    using System;
    using System.Collections.Generic;
    using System.Text;
    using ColossalFramework;
    using ColossalFramework.UI;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;

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
                                      string localizedText,
                                      bool shift = false,
                                      bool ctrl = false,
                                      bool alt = false) {
            button_ = button;
            shift_ = shift;
            ctrl_ = ctrl;
            alt_ = alt;
            localizedText_ = localizedText;
        }

        public override void Build(UIComponent parent,
                                   U.UBuilder builder) {
            // Capacity 9 will fit all modifiers and separators and the text
            StringBuilder text = new StringBuilder(capacity: 9);

            text.Append(UConst.GetKeyboardShortcutColorTagOpener());

            if (this.shift_) {
                text.Append(Translation.Options.Get("Shortcut.Modifier:Shift"));
                text.Append("+");
            }

            if (this.ctrl_) {
                text.Append(Translation.Options.Get("Shortcut.Modifier:Ctrl"));
                text.Append("+");
            }

            if (this.alt_) {
                text.Append(Translation.Options.Get("Shortcut.Modifier:Alt"));
                text.Append("+");
            }

            text.Append(TranslationForMouseButton(this.button_));
            text.Append(UConst.KEYBOARD_SHORTCUT_CLOSING_TAG + " ");
            text.Append(this.localizedText_);

            builder.Label<U.ULabel>(
                parent,
                t: text.ToString(),
                stack: UStackMode.NewRowBelow,
                processMarkup: true);
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