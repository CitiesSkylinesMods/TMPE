namespace TrafficManager.UI.MainMenu {
    using System;
    using ColossalFramework.UI;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using UnityEngine;

    /// <summary>
    /// Static class provides code for setting up onscreen display hints for keyboard and mouse.
    /// </summary>
    public static class OnScreenDisplay {
        public static void Clear() {
            Begin();
            Done();
        }

        /// <summary>On Screen Display feature: Clear and maybe hide the keybind panel.</summary>
        public static void Begin() {
            MainMenuWindow mainMenu = ModUI.Instance.MainMenu;

            foreach (Transform c in mainMenu.KeybindsPanel.transform) {
                c.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(c.gameObject);
            }

            mainMenu.KeybindsPanel.transform.DetachChildren();
            mainMenu.KeybindsPanel.isVisible = false;
        }

        /// <summary>
        /// On Screen Display feature: Add another keybind to the panel.
        /// </summary>
        /// <param name="kbSetting">KeybindSetting to show.</param>
        /// <param name="localizedText">Text from Translation.Get() or English if doesn't exist.</param>
        public static void Shortcut(KeybindSetting kbSetting, string localizedText) {
            MainMenuWindow mainMenu = ModUI.Instance.MainMenu;
            mainMenu.KeybindsPanel.isVisible = true;

            var builder = new UiBuilder<U.Panel.UPanel>(mainMenu.KeybindsPanel);

            using (var shortcutLabel = builder.ShortcutLabel(kbSetting)) {
                shortcutLabel.ResizeFunction(r => { r.Stack(UStackMode.NewRowBelow); });
            }

            using (var descriptionLabel = builder.Label<U.Label.ULabel>(localizedText)) {
                descriptionLabel.ResizeFunction(
                    r => {
                        r.Stack(
                            mode: UStackMode.ToTheRight,
                            spacing: UConst.UIPADDING * 2f); // double space
                    });
            }
        }

        /// <summary>
        /// On Screen Display feature: Add a click info to the panel.
        /// </summary>
        /// <param name="localizedText">Text from Translation.Get() or English if doesn't exist.</param>
        public static void Click(UIMouseButton button, bool shift, bool ctrl, bool alt, string localizedText) {
            MainMenuWindow mainMenu = ModUI.Instance.MainMenu;
            mainMenu.KeybindsPanel.isVisible = true;

            var builder = new UiBuilder<U.Panel.UPanel>(mainMenu.KeybindsPanel);
            string clickText = TranslationForMouseButton(button);

            if (shift | ctrl | alt) {
                using (var modifierLabel = builder.ModifierLabel(shift, ctrl, alt)) {
                    modifierLabel.ResizeFunction(r => { r.Stack(UStackMode.NewRowBelow); });
                }

                using (var plusLabel = builder.Label<U.Label.ULabel>("+")) {
                    plusLabel.ResizeFunction(r => { r.Stack(UStackMode.ToTheRight); });
                }

                // Click label attached to the right
                using (var clickLabel = builder.Label<U.Label.ULabel>(clickText)) {
                    clickLabel.Control.backgroundSprite = "GenericPanelDark";
                    clickLabel.Control.textColor = UConst.SHORTCUT_KEYBIND_TEXT;
                    clickLabel.ResizeFunction(r => { r.Stack(UStackMode.ToTheRight); });
                }
            } else {
                // Start new row. TODO: Allow new row somehow to be a call to the UI builder?
                using (var clickLabel = builder.Label<U.Label.ULabel>(clickText)) {
                    clickLabel.Control.backgroundSprite = "GenericPanelDark";
                    clickLabel.Control.textColor = UConst.SHORTCUT_KEYBIND_TEXT;
                    clickLabel.ResizeFunction(r => { r.Stack(UStackMode.NewRowBelow); });
                }
            }

            using (var descriptionLabel = builder.Label<U.Label.ULabel>(localizedText)) {
                descriptionLabel.ResizeFunction(
                    r => {
                        r.Stack(
                            mode: UStackMode.ToTheRight,
                            spacing: UConst.UIPADDING * 2f); // double space
                    });
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

        /// <summary>Resize everything in MainMenu to fit the new panel contents.</summary>
        public static void Done() {
            MainMenuWindow mainMenu = ModUI.Instance.MainMenu;

            // Recalculate now
            UResizer.UpdateControl(mainMenu);
        }

        public static void RightClickCancel() {
            // Click(
            //     button: UIMouseButton.Right,
            //     shift: false,
            //     ctrl: false,
            //     alt: false,
            //     localizedText: Translation.Options.Get("Keybind.RightClick:Back/cancel"));
            MainMenuWindow mainMenu = ModUI.Instance.MainMenu;
            var builder = new UiBuilder<U.Panel.UPanel>(mainMenu.KeybindsPanel);

            using (var clickLabel =
                builder.ShortcutLabel(KeybindSettingsBase.ToolCancelRightClick))
            {
                clickLabel.ResizeFunction(r => { r.Stack(UStackMode.NewRowBelow); });
            }

            string localizedText = Translation.Options.Get("Keybind.RightClick:Back/cancel");
            using (var descriptionLabel = builder.Label<U.Label.ULabel>(localizedText)) {
                descriptionLabel.ResizeFunction(
                    r => {
                        r.Stack(
                            mode: UStackMode.ToTheRight,
                            spacing: UConst.UIPADDING * 2f); // double space
                    });
            }
        }
    }
}