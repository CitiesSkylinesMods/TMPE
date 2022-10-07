namespace TrafficManager.UI.MainMenu.OSD {
    using System.Collections.Generic;
    using JetBrains.Annotations;
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
            Hide();
        }

        private static void Hide() {
            MainMenuWindow mainMenu = ModUI.Instance.MainMenu;
            if (mainMenu.OnscreenDisplayPanel.GetUIView() != null) { // safety
                mainMenu.OnscreenDisplayPanel.opacity = 0f; // invisible
            }
        }

        /// <summary>
        /// On Screen Display feature:
        /// Clear, and hide the keybind panel.
        /// Populate with items, which can be keyboard shortcuts or hardcoded mouse clicks.
        /// </summary>
        /// <param name="items">List of <see cref="OsdItem"/> to display.</param>
        public static void Display(List<OsdItem> items) {
            MainMenuWindow mainMenu = ModUI.Instance.MainMenu;
            Hide();

            // Deactivate old items, and destroy them. Also remove them from the panel till Unity
            // is happy to delete them.
            foreach (Transform c in mainMenu.OnscreenDisplayPanel.transform) {
                c.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(c.gameObject);
            }

            // mainMenu.KeybindsPanel.transform.DetachChildren();

            // Populate the panel with the items
            var builder = new UBuilder();
            foreach (OsdItem item in items) {
                item.Build(
                    parent: mainMenu.OnscreenDisplayPanel,
                    builder);
            }

            if (items.Count > 0
                && mainMenu.OnscreenDisplayPanel.GetUIView() != null)
            {
                mainMenu.OnscreenDisplayPanel.opacity = 1f; // fully visible, opaque
            }

            // Recalculate now
            UResizer.UpdateControl(mainMenu);
        }

        /// <summary>Create OsdItem with generic "Esc Cancel and hide TMPE" text.</summary>
        /// <returns>New OsdItem to pass to the <see cref="Display"/>.</returns>
        [UsedImplicitly]
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

        /// <summary>Create OsdItem with generic "RightClick Leave lane" text.</summary>
        /// <returns>New OsdItem to pass to the <see cref="Display"/>.</returns>
        public static Shortcut RightClick_LeaveLane() {
            return new Shortcut(
                keybindSetting: KeybindSettingsBase.RightClick,
                localizedText: Translation.Options.Get("Keybind.RightClick:Leave lane"));
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