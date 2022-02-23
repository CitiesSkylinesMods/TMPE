namespace TrafficManager.State {
    using ICities;
    using TrafficManager.Lifecycle;
    using TrafficManager.U;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    public static class GeneralTab_InterfaceGroup {

        public static CheckboxOption MainMenuButtonPosLocked =
            new(nameof(Main.MainMenuButtonPosLocked), Options.PersistTo.Global) {
                Label = "General.Checkbox:Lock main menu button position",
                Handler = OnMainMenuButtonPosLockedChanged,
            };

        public static CheckboxOption MainMenuPosLocked =
            new(nameof(Main.MainMenuPosLocked), Options.PersistTo.Global) {
                Label = "General.Checkbox:Lock main menu window position",
                Handler = OnMainMenuPosLockedChanged,
            };

        public static CheckboxOption UseUUI =
            new(nameof(Main.UseUUI), Options.PersistTo.Global) {
                Label = "General.Checkbox:Use UnifiedUI",
                Handler = OnUseUUIChanged,
            };

        public static SliderOption GuiScale =
            new(nameof(Main.GuiScale), Options.PersistTo.Global) {
                Label = "General.Slider:GUI scale",
                Tooltip = "%",
                Min = 50,
                Max = 200,
                Handler = OnGuiScaleChanged,
            };

        public static SliderOption GuiOpacity =
            new(nameof(Main.GuiOpacity), Options.PersistTo.Global) {
                Label = "General.Slider:Window opacity",
                Tooltip = "%",
                Min = TrafficManagerTool.MINIMUM_OPACITY,
                Max = TrafficManagerTool.MAXIMUM_OPACITY,
                Handler = OnGuiOpacityChanged,
            };

        public static SliderOption OverlayOpacity =
            new(nameof(Main.OverlayOpacity), Options.PersistTo.Global) {
                Label = "General.Slider:Overlay opacity",
                Tooltip = "%",
                Min = TrafficManagerTool.MINIMUM_OPACITY,
                Max = TrafficManagerTool.MAXIMUM_OPACITY,
                Handler = OnOverlayOpacityChanged,
            };

        public static CheckboxOption EnableTutorial =
            new(nameof(Main.EnableTutorial), Options.PersistTo.Global) {
                Label = "General.Checkbox:Enable tutorials",
                Handler = OnEnableTutorialChanged,
            };

        public static CheckboxOption OpenUrlsInSteamOverlay =
            new(nameof(Main.OpenUrlsInSteamOverlay), Options.PersistTo.Global) {
                Label = "Checkbox:Use Steam Overlay to show TM:PE website links",
                Tooltip = "Checkbox.Tooltip:When disabled, website links will open in your default web browser",
                Handler = OnOpenUrlsInSteamOverlayChanged,
            };

        private static ConfigData.Main Main => GlobalConfig.Instance.Main;

        internal static void AddUI(UIHelperBase tab) {
            var group = tab.AddGroup(T("General.Group:Interface"));

            MainMenuButtonPosLocked.AddUI(group)
                .Value = Main.MainMenuButtonPosLocked;
            MainMenuPosLocked.AddUI(group)
                .Value = Main.MainMenuPosLocked;
            UseUUI.AddUI(group)
                .Value = Main.UseUUI;
            GuiScale.AddUI(group)
                .Value = Main.GuiScale;
            GuiOpacity.AddUI(group)
                .Value = Main.GuiOpacity;
            OverlayOpacity.AddUI(group)
                .Value = Main.OverlayOpacity;
            EnableTutorial.AddUI(group)
                .Value = Main.EnableTutorial;
            OpenUrlsInSteamOverlay.AddUI(group)
                .Value = Main.OpenUrlsInSteamOverlay;
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static void OnMainMenuButtonPosLockedChanged(bool val) {
            if (Main.MainMenuButtonPosLocked == val) return;

            Main.MainMenuButtonPosLocked = val;
            GlobalConfig.WriteConfig();

            if (TMPELifecycle.InGameOrEditor()) {
                ModUI.Instance.MainMenuButton.SetPosLock(val);
            }
        }

        private static void OnMainMenuPosLockedChanged(bool val) {
            if (Main.MainMenuPosLocked == val) return;

            Main.MainMenuPosLocked = val;
            GlobalConfig.WriteConfig();

            if (TMPELifecycle.InGameOrEditor()) {
                ModUI.Instance.MainMenu.SetPosLock(val);
            }
        }

        private static void OnUseUUIChanged(bool val) {
            if (Main.UseUUI == val) return;

            Main.UseUUI = val;
            GlobalConfig.WriteConfig();

            if (TMPELifecycle.InGameOrEditor()) {
                var button = ModUI.GetTrafficManagerTool()?.UUIButton;
                if (button) button.isVisible = val;
                ModUI.Instance?.MainMenuButton?.UpdateButtonSkinAndTooltip();
            }
        }

        private static void OnGuiScaleChanged(float val) {
            if (Main.GuiScale == val) return;

            Main.GuiScale = val;
            GlobalConfig.WriteConfig();

            if (TMPELifecycle.InGameOrEditor())
                ModUI.Instance?.Events.UiScaleChanged();
        }

        private static void OnGuiOpacityChanged(float val) {
            if (Main.GuiOpacity == val) return;

            Main.GuiOpacity = GuiOpacity.Save();
            GlobalConfig.WriteConfig();

            if (TMPELifecycle.InGameOrEditor()) {
                var opacity = UOpacityValue.FromOpacityPercent(Main.GuiOpacity);
                ModUI.Instance?.Events.OpacityChanged(opacity);
            }
        }

        private static void OnOverlayOpacityChanged(float val) {
            if (Main.OverlayOpacity == val) return;

            Main.OverlayOpacity = OverlayOpacity.Save();
            GlobalConfig.WriteConfig();
        }

        private static void OnEnableTutorialChanged(bool val) {
            if (Main.EnableTutorial == val) return;

            Main.EnableTutorial = val;
            GlobalConfig.WriteConfig();
        }

        private static void OnOpenUrlsInSteamOverlayChanged(bool val) {
            if (Main.OpenUrlsInSteamOverlay == val) return;

            Main.OpenUrlsInSteamOverlay = val;
            GlobalConfig.WriteConfig();
        }
    }
}