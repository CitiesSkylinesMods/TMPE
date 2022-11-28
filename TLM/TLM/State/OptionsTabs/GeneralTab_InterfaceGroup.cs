namespace TrafficManager.State {
    using ICities;
    using TrafficManager.Lifecycle;
    using TrafficManager.U;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    public static class GeneralTab_InterfaceGroup {

        public static CheckboxOption MainMenuButtonPosLocked =
            new(nameof(Main.MainMenuButtonPosLocked), Scope.Global) {
                Label = "General.Checkbox:Lock main menu button position",
                Handler = OnMainMenuButtonPosLockedChanged,
            };

        public static CheckboxOption MainMenuPosLocked =
            new(nameof(Main.MainMenuPosLocked), Scope.Global) {
                Label = "General.Checkbox:Lock main menu window position",
                Handler = OnMainMenuPosLockedChanged,
            };

        public static CheckboxOption UseUUI =
            new(nameof(Main.UseUUI), Scope.Global) {
                Label = "General.Checkbox:Use UnifiedUI",
                Handler = OnUseUUIChanged,
            };

        public static SliderOption GuiScale =
            new(nameof(Main.GuiScale), Scope.Global) {
                Label = "General.Slider:GUI scale",
                Tooltip = "%",
                Min = 50,
                Max = 200,
                Handler = OnGuiScaleChanged,
            };

        public static SliderOption GuiOpacity =
            new(nameof(Main.GuiOpacity), Scope.Global) {
                Label = "General.Slider:Window opacity",
                Tooltip = "%",
                Min = TrafficManagerTool.MINIMUM_OPACITY,
                Max = TrafficManagerTool.MAXIMUM_OPACITY,
                Handler = OnGuiOpacityChanged,
            };

        public static SliderOption OverlayOpacity =
            new(nameof(Main.OverlayOpacity), Scope.Global) {
                Label = "General.Slider:Overlay opacity",
                Tooltip = "%",
                Min = TrafficManagerTool.MINIMUM_OPACITY,
                Max = TrafficManagerTool.MAXIMUM_OPACITY,
                Handler = OnOverlayOpacityChanged,
            };

        public static CheckboxOption EnableTutorial =
            new(nameof(Main.EnableTutorial), Scope.Global) {
                Label = "General.Checkbox:Enable tutorials",
                Handler = OnEnableTutorialChanged,
            };

        public static CheckboxOption OpenUrlsInSteamOverlay =
            new(nameof(Main.OpenUrlsInSteamOverlay), Scope.Global) {
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

        private static void OnMainMenuButtonPosLockedChanged(bool value) {
            if (Main.MainMenuButtonPosLocked == value) return;

            Main.MainMenuButtonPosLocked = value;
            GlobalConfig.WriteConfig();

            if (TMPELifecycle.InGameOrEditor()) {
                ModUI.Instance.MainMenuButton.SetPosLock(value);
            }
        }

        private static void OnMainMenuPosLockedChanged(bool value) {
            if (Main.MainMenuPosLocked == value) return;

            Main.MainMenuPosLocked = value;
            GlobalConfig.WriteConfig();

            if (TMPELifecycle.InGameOrEditor()) {
                ModUI.Instance.MainMenu.SetPosLock(value);
            }
        }

        private static void OnUseUUIChanged(bool value) {
            if (Main.UseUUI == value) return;

            Main.UseUUI = value;
            GlobalConfig.WriteConfig();

            if (TMPELifecycle.InGameOrEditor()) {
                var button = ModUI.GetTrafficManagerTool()?.UUIButton;
                if (button) button.isVisible = value;
                ModUI.Instance?.MainMenuButton?.UpdateButtonSkinAndTooltip();
            }
        }

        private static void OnGuiScaleChanged(float value) {
            if (Main.GuiScale == value) return;

            Main.GuiScale = value;
            GlobalConfig.WriteConfig();

            if (TMPELifecycle.InGameOrEditor())
                ModUI.Instance?.Events.UiScaleChanged();
        }

        private static void OnGuiOpacityChanged(float value) {
            if (Main.GuiOpacity == value) return;

            Main.GuiOpacity = GuiOpacity.Save();
            GlobalConfig.WriteConfig();

            if (TMPELifecycle.InGameOrEditor()) {
                var opacity = UOpacityValue.FromOpacityPercent(Main.GuiOpacity);
                ModUI.Instance?.Events.OpacityChanged(opacity);
            }
        }

        private static void OnOverlayOpacityChanged(float value) {
            if (Main.OverlayOpacity == value) return;

            Main.OverlayOpacity = OverlayOpacity.Save();
            GlobalConfig.WriteConfig();
        }

        private static void OnEnableTutorialChanged(bool value) {
            if (Main.EnableTutorial == value) return;

            Main.EnableTutorial = value;
            GlobalConfig.WriteConfig();
        }

        private static void OnOpenUrlsInSteamOverlayChanged(bool value)
        {
            if (Main.OpenUrlsInSteamOverlay == value)
                return;
            Main.OpenUrlsInSteamOverlay = value;
            GlobalConfig.WriteConfig();
        }
    }
}