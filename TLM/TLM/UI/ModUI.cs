namespace TrafficManager.UI {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using TrafficManager.UI.MainMenu;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>
    /// Globally available UI manager class which contains the main menu button and the panel.
    /// Access via ThreadingExtension.ModUi.
    /// </summary>
    public class ModUI : UICustomControl {
        /// <summary>Singleton accessor.</summary>
        public static ModUI Instance { get; private set; }

        /// <summary>Gets the floating draggable button which shows and hides TM:PE UI.</summary>
        public UI.MainMenu.MainMenuButton MainMenuButton { get; set; }

        /// <summary>Gets the floating tool panel with TM:PE tool buttons.</summary>
        public UI.MainMenu.MainMenuWindow MainMenu { get; set; }

#if DEBUG
        public DebugMenuPanel DebugMenu { get; private set; }
#endif

        public static TrafficManagerTool GetTrafficManagerTool(bool createIfRequired = true) {
            if (trafficManagerTool_ == null && createIfRequired) {
                Log.Info("Initializing traffic manager tool...");
                GameObject toolModControl = ToolsModifierControl.toolController.gameObject;
                trafficManagerTool_ =
                    toolModControl.GetComponent<TrafficManagerTool>()
                    ?? toolModControl.AddComponent<TrafficManagerTool>();
                trafficManagerTool_.Initialize();
            }

            return trafficManagerTool_;
        }

        private static TrafficManagerTool trafficManagerTool_;

        public static TrafficManagerMode ToolMode { get; set; }

        private bool _uiShown;

        /// <summary>Event to be sent when UI scale changes in the General Options tab.</summary>
        public struct UIScaleNotification { public float NewScale; }

        public class UIScaleObservable : GenericObservable<UIScaleNotification> {
        }

        /// <summary>
        /// Subscribe to this to get notifications in your UI about UI scale changes (slider in
        /// General options tab).
        /// </summary>
        [NonSerialized]
        public UIScaleObservable UiScaleObservable;

        /// <summary>Event to be sent when UI transparency slider changes in the General Options tab.</summary>
        public struct UIOpacityNotification { public U.UOpacityValue Opacity; }

        public class UIOpacityObservable : GenericObservable<UIOpacityNotification> {
        }

        /// <summary>
        /// Subscribe to this to get notifications in your UI about UI transparency changes
        /// (slider in General options tab).
        /// </summary>
        [NonSerialized]
        public UIOpacityObservable uiOpacityObservable;

        public ModUI() {
            UiScaleObservable = new UIScaleObservable();
            uiOpacityObservable = new UIOpacityObservable();

            Log._Debug("##### Initializing ModUI.");

            CreateMainMenuButtonAndWindow();
#if DEBUG
            UIView uiView = UIView.GetAView();
            DebugMenu = (DebugMenuPanel)uiView.AddUIComponent(typeof(DebugMenuPanel));
#endif

            ToolMode = TrafficManagerMode.None;

            // One time load
            LoadingExtension.TranslationDatabase.ReloadTutorialTranslations();
            LoadingExtension.TranslationDatabase.ReloadGuideTranslations();
        }

        private void CreateMainMenuButtonAndWindow() {
            UIView uiView = UIView.GetAView();
            try {
                MainMenu = MainMenuWindow.CreateMainMenuWindow();
            }
            catch (Exception e) {
                Log.Error($"While creating MainMenu: {e}");
            }
            try {
                MainMenuButton = (MainMenuButton)uiView.AddUIComponent(typeof(MainMenuButton));
            }
            catch (Exception e) {
                Log.Error($"While creating MainButton: {e}");
            }
        }

        public void Destroy() {
            Log._Debug("ModUI destructor is called.");
            Destroy(MainMenuButton);
            Destroy(MainMenu);
            ReleaseTool();
            Instance = null;
            Destroy(this);
        }

        public bool IsVisible() {
            return _uiShown;
        }

        public void ToggleMainMenu() {
            if (IsVisible()) {
                CloseMainMenu();
            } else {
                ShowMainMenu();
                GetTrafficManagerTool().RequestOnscreenDisplayUpdate();
            }
        }

        /// <summary>
        /// Called from Options and Options-Maintenance tab, when features and options changed,
        /// which might require rebuilding the main menu buttons.
        /// </summary>
        internal void RebuildMenu() {
            CloseMainMenu();

            if (MainMenu != null) {
                CustomKeyHandler keyHandler = MainMenu.GetComponent<CustomKeyHandler>();
                if (keyHandler != null) {
                    UnityEngine.Object.Destroy(keyHandler);
                }

                UnityEngine.Object.Destroy(MainMenu);
                UnityEngine.Object.Destroy(MainMenuButton);
                MainMenu = null;
                MainMenuButton = null;
#if DEBUG
                UnityEngine.Object.Destroy(DebugMenu);
                DebugMenu = null;
#endif
            }

            CreateMainMenuButtonAndWindow();
#if DEBUG
            UIView uiView = UIView.GetAView();
            DebugMenu = (DebugMenuPanel)uiView.AddUIComponent(typeof(DebugMenuPanel));
#endif
        }

        public void ShowMainMenu() {
            try {
                ToolsModifierControl.mainToolbar.CloseEverything();
            } catch (Exception e) {
                Log.Error("Error on Show(): " + e);
            }

            MainMenuWindow menuWindow = GetMenu();
            menuWindow.UpdateButtons();
            menuWindow.Show();

            TrafficManagerTool.ShowAdvisor("MainMenu");
#if DEBUG
            GetDebugMenu().Show();
#endif
            SetToolMode(TrafficManagerMode.Activated);
            _uiShown = true;
            MainMenuButton.UpdateButtonImageAndTooltip();
            UIView.SetFocus(MainMenu);
        }

        public void CloseMainMenu() {
            // Before hiding the menu, shut down the active tool
            GetTrafficManagerTool(false)?.SetToolMode(UI.ToolMode.None);

            // Main menu is going invisible
            GetMenu().Hide();
#if DEBUG
            GetDebugMenu().Hide();
#endif

            SetToolMode(TrafficManagerMode.None);
            _uiShown = false;
            MainMenuButton.UpdateButtonImageAndTooltip();
        }

        internal MainMenuWindow GetMenu() {
            return MainMenu;
        }

#if DEBUG
        internal DebugMenuPanel GetDebugMenu() {
            return DebugMenu;
        }
#endif

        public static void SetToolMode(TrafficManagerMode mode) {
            if (mode == ToolMode) {
                return;
            }

            ToolMode = mode;

            if (mode != TrafficManagerMode.None) {
                EnableTool();
            } else {
                DisableTool();
            }
        }

        public static void EnableTool() {
            Log._Debug("ModUI.EnableTool: called");
            TrafficManagerTool tmTool = GetTrafficManagerTool(true);

            ToolsModifierControl.toolController.CurrentTool = tmTool;
            ToolsModifierControl.SetTool<TrafficManagerTool>();
        }

        public static void OnLevelLoaded() {
            Log._Debug("ModUI.OnLevelLoaded: called");
            if (ModUI.Instance == null) {
                Log._Debug("Adding UIBase instance.");
                ModUI.Instance = ToolsModifierControl.toolController
                    .gameObject
                    .AddComponent<ModUI>();
            }
            LoadingExtension.TranslationDatabase.ReloadTutorialTranslations();
            LoadingExtension.TranslationDatabase.ReloadGuideTranslations();
        }

        public static void DisableTool() {
            Log._Debug("ModUI.DisableTool: called");
            if (ToolsModifierControl.toolController == null) {
                Log.Warning("ModUI.DisableTool: ToolsModifierControl.toolController is null!");
            } else if (trafficManagerTool_ == null) {
                Log.Warning("ModUI.DisableTool: tool is null!");
            } else if (ToolsModifierControl.toolController.CurrentTool != trafficManagerTool_) {
                Log.Info("ModUI.DisableTool: CurrentTool is not traffic manager tool!");
            } else {
                ToolsModifierControl.toolController.CurrentTool = ToolsModifierControl.GetTool<DefaultTool>();
                ToolsModifierControl.SetTool<DefaultTool>();
            }
        }

        internal static void ReleaseTool() {
            ToolMode = TrafficManagerMode.None;
            DestroyTool();
        }

        private static void DestroyTool() {
            DisableTool();
            if (trafficManagerTool_ != null) {
                Log.Info("Removing Traffic Manager Tool.");
                UnityEngine.Object.Destroy(trafficManagerTool_);
                trafficManagerTool_ = null;
            } // end if
        } // end DestroyTool()
    }
}
