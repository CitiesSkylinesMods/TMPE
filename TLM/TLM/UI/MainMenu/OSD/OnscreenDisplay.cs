namespace TrafficManager.UI.MainMenu.OSD {
    using System.Collections.Generic;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using UnityEngine;

    /// <summary>
    /// Static class provides code for setting up onscreen display hints for keyboard and mouse.
    /// </summary>
    public static class OnscreenDisplay {
        public static void Clear() {
            Display(new List<OsdItem>());
        }

        /// <summary>
        /// On Screen Display feature:
        /// Clear, and hide the keybind panel.
        /// Populate with items, which can be keyboard shortcuts or hardcoded mouse clicks.
        /// </summary>
        /// <param name="items">List of <see cref="OsdItem"/> to display.</param>
        public static void Display(List<OsdItem> items) {
            MainMenuWindow mainMenu = ModUI.Instance.MainMenu;
            mainMenu.KeybindsPanel.isVisible = false;

            // Deactivate old items, and destroy them. Also remove them from the panel till Unity
            // is happy to delete them.
            foreach (Transform c in mainMenu.KeybindsPanel.transform) {
                c.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(c.gameObject);
            }

            mainMenu.KeybindsPanel.transform.DetachChildren();

            // Populate the panel with the items
            using (var builder = new UiBuilder<U.Panel.UPanel>(mainMenu.KeybindsPanel)) {
                foreach (var item in items) {
                    item.Build(builder);
                }
            }


            // Show the panel now
            if (items.Count > 0) {
                mainMenu.KeybindsPanel.isVisible = true;
            }

            // Recalculate now
            UResizer.UpdateControl(mainMenu);
        }

        /// <summary>Create OsdItem with generic "Esc Cancel and hide TMPE" text.</summary>
        /// <returns>New OsdItem to pass to the <see cref="Display"/>.</returns>
        public static Shortcut Esc_CancelAndHideTMPE() {
            return new Shortcut(
                keybindSetting: KeybindSettingsBase.Esc,
                localizedText: Translation.Options.Get("Keybind.Esc:Cancel tool"));
        }

        /// <summary>Create OsdItem with generic "RightClick Leave node" text.</summary>
        /// <returns>New OsdItem to pass to the <see cref="Display"/>.</returns>
        public static Shortcut RightClick_LeaveNode() {
            return new Shortcut(
                keybindSetting: KeybindSettingsBase.RightClick,
                localizedText: Translation.Options.Get("Keybind.RightClick:Leave node"));
        }

        /// <summary>Create OsdItem with generic "RightClick Leave segment" text.</summary>
        /// <returns>New OsdItem to pass to the <see cref="Display"/>.</returns>
        public static Shortcut RightClick_LeaveSegment() {
            return new Shortcut(
                keybindSetting: KeybindSettingsBase.RightClick,
                localizedText: Translation.Options.Get("Keybind.RightClick:Leave segment"));
        }
    }
}