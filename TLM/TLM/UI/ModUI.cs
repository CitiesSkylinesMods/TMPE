namespace TrafficManager.UI {
    using System;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.Lifecycle;
    using TrafficManager.UI.MainMenu;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>
    /// Globally available UI manager class which contains the main menu button and the panel.
    /// Access via ThreadingExtension.ModUi.
    /// </summary>
    public class ModUI : UICustomControl {
        /// <summary>Gets singleton.</summary>
        public static ModUI Instance { get; private set; }

        /// <summary>Gets or sets the floating draggable button which shows and hides TM:PE UI.</summary>
        public UI.MainMenu.MainMenuButton MainMenuButton { get; set; }

        /// <summary>Gets or sets the floating tool panel with TM:PE tool buttons.</summary>
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

        private bool isUiVisible_;

        /// <summary>Event to be sent when UI scale changes in the General Options tab.</summary>
        public struct UIScaleNotification {
            public float NewScale;
        }

        public class UIScaleObservable : GenericObservable<UIScaleNotification> { }

        /// <summary>
        /// Subscribe to this to get notifications in your UI about UI scale changes (slider in
        /// General options tab).
        /// </summary>
        [NonSerialized]
        public UIScaleObservable UiScaleObservable;

        /// <summary>Event to be sent when UI transparency slider changes in the General Options tab.</summary>
        public struct UIOpacityNotification {
            public U.UOpacityValue Opacity;
        }

        public class UIOpacityObservable : GenericObservable<UIOpacityNotification> { }

        /// <summary>
        /// Subscribe to this to get notifications in your UI about UI transparency changes
        /// (slider in General options tab).
        /// </summary>
        [NonSerialized]
        public UIOpacityObservable UiOpacityObservable;

        private const string TMPE_DEBUGMENU_NAME = "DebugMenu";

        public void Awake() {
            this.UiScaleObservable = new UIScaleObservable();
            this.UiOpacityObservable = new UIOpacityObservable();

            Log._Debug("##### Initializing ModUI.");

            this.CreateMainMenuButtonAndWindow();
#if DEBUG
            UIView uiView = UIView.GetAView();
            this.DebugMenu = (DebugMenuPanel)uiView.AddUIComponent(typeof(DebugMenuPanel));

            U.UIUtil.MakeUniqueAndSetName(
                toMakeUnique: this.DebugMenu.gameObject,
                name: TMPE_DEBUGMENU_NAME);
#endif

            ToolMode = TrafficManagerMode.None;

            // One time load
            TMPELifecycle.Instance.TranslationDatabase.ReloadTutorialTranslations();
            TMPELifecycle.Instance.TranslationDatabase.ReloadGuideTranslations();
        }

        private void CreateMainMenuButtonAndWindow() {
            UIView uiView = UIView.GetAView();
            try {
                this.MainMenu = MainMenuWindow.CreateMainMenuWindow();
            }
            catch (Exception e) {
                Log.Error($"While creating MainMenu: {e}");
            }

            try {
                this.MainMenuButton = (MainMenuButton)uiView.AddUIComponent(typeof(MainMenuButton));
            }
            catch (Exception e) {
                Log.Error($"While creating MainButton: {e}");
            }
        }

        public void Destroy() {
            Log._Debug("ModUI destructor is called.");
            DestroyImmediate(this.MainMenuButton);
            DestroyImmediate(this.MainMenu);
            ReleaseTool();
            Instance = null;
            DestroyImmediate(this);
        }

        public bool IsVisible() {
            return this.isUiVisible_;
        }

        public void ToggleMainMenu() {
            if (this.IsVisible()) {
                this.CloseMainMenu();
            } else {
                this.ShowMainMenu();
                GetTrafficManagerTool().RequestOnscreenDisplayUpdate();
            }
        }

        /// <summary>
        /// Called from Options and Options-Maintenance tab, when features and options changed,
        /// which might require rebuilding the main menu buttons.
        /// </summary>
        internal void RebuildMenu() {
            this.CloseMainMenu();

            if (this.MainMenu != null) {
                CustomKeyHandler keyHandler = this.MainMenu.GetComponent<CustomKeyHandler>();
                if (keyHandler != null) {
                    UnityEngine.Object.Destroy(keyHandler);
                }

                UnityEngine.Object.Destroy(this.MainMenu);
                UnityEngine.Object.Destroy(this.MainMenuButton);
                this.MainMenu = null;
                this.MainMenuButton = null;
#if DEBUG
                UnityEngine.Object.Destroy(this.DebugMenu);
                this.DebugMenu = null;
#endif
            }

            this.CreateMainMenuButtonAndWindow();
#if DEBUG
            UIView uiView = UIView.GetAView();
            this.DebugMenu = (DebugMenuPanel)uiView.AddUIComponent(typeof(DebugMenuPanel));

            U.UIUtil.MakeUniqueAndSetName(
                toMakeUnique: this.DebugMenu.gameObject,
                name: TMPE_DEBUGMENU_NAME);
#endif
        }

        public void ShowMainMenu() {
            try {
                ToolsModifierControl.mainToolbar.CloseEverything();
            }
            catch (Exception e) {
                Log.Error("Error on Show(): " + e);
            }

            MainMenuWindow menuWindow = this.GetMenu();
            menuWindow.UpdateButtons();
            menuWindow.Show();

            TrafficManagerTool.ShowAdvisor("MainMenu");
#if DEBUG
            this.GetDebugMenu().Show();
#endif
            this.isUiVisible_ = true;
            SetToolMode(TrafficManagerMode.Activated);
            this.MainMenuButton.UpdateButtonSkinAndTooltip();
            UIView.SetFocus(this.MainMenu);
        }

        public void CloseMainMenu() {
            // Before hiding the menu, shut down the active tool
            TrafficManagerTool tool = GetTrafficManagerTool(false);
            if (tool != null) {
                tool.SetToolMode(UI.ToolMode.None);
            }

            // Main menu is going invisible
            this.GetMenu()?.Hide();
#if DEBUG
            this.GetDebugMenu()?.Hide();
#endif

            this.isUiVisible_ = false;
            SetToolMode(TrafficManagerMode.None);
            this.MainMenuButton.UpdateButtonSkinAndTooltip();
        }

        internal MainMenuWindow GetMenu() {
            return this.MainMenu;
        }

#if DEBUG
        internal DebugMenuPanel GetDebugMenu() {
            return this.DebugMenu;
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
            TrafficManagerTool tmTool = GetTrafficManagerTool(createIfRequired: true);

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

            TMPELifecycle.Instance.TranslationDatabase.ReloadTutorialTranslations();
            TMPELifecycle.Instance.TranslationDatabase.ReloadGuideTranslations();
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
                ToolsModifierControl.toolController.CurrentTool =
                    ToolsModifierControl.GetTool<DefaultTool>();
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