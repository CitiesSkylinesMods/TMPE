namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State.Keybinds;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using UnityEngine;

    public static class OnScreenDisplay {
        /// <summary>On Screen Display feature: Clear and maybe hide the keybind panel.</summary>
        public static void Clear() {
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
        public static void Click(bool shift, bool ctrl, bool alt, string localizedText) {
            MainMenuWindow mainMenu = ModUI.Instance.MainMenu;
            mainMenu.KeybindsPanel.isVisible = true;

            var builder = new UiBuilder<U.Panel.UPanel>(mainMenu.KeybindsPanel);

            using (var modifierLabel = builder.ModifierLabel(shift, ctrl, alt)) {
                modifierLabel.ResizeFunction(r => { r.Stack(UStackMode.NewRowBelow); });
            }

            using (var plusLabel = builder.Label<U.Label.ULabel>("+ ")) {
                plusLabel.ResizeFunction(r => { r.Stack(UStackMode.ToTheRight); });
            }

            string clickText = Translation.Options.Get("Shortcut:Click");
            using (var clickLabel = builder.Label<U.Label.ULabel>(clickText)) {
                clickLabel.Control.textColor = UConst.SHORTCUT_KEYBIND_TEXT;
                clickLabel.ResizeFunction(r => { r.Stack(UStackMode.ToTheRight); });
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

        /// <summary>Resize everything in MainMenu to fit the new panel contents.</summary>
        public static void Done() {
            MainMenuWindow mainMenu = ModUI.Instance.MainMenu;

            // Recalculate now
            UResizer.UpdateControl(mainMenu);
        }
    }
}