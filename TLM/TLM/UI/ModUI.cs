namespace TrafficManager.UI {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using TrafficManager.UI.MainMenu;
    using UnityEngine;

    /// <summary>
    /// Globally available UI manager class which contains the main menu button and the panel.
    /// Access via ThreadingExtension.ModUi.
    /// </summary>
    public class ModUI : UICustomControl {
        /// <summary>Singleton storage.</summary>
        public static ModUI instance_;

        /// <summary>Singleton accessor.</summary>
        public static ModUI Instance {
            get => instance_;
        }

        public static void SetSingletonInstance(ModUI newInstance) {
            instance_ = newInstance;
        }

        /// <summary>Gets the floating draggable button which shows and hides TM:PE UI.</summary>
        public UI.MainMenu.MainMenuButton MainMenuButton { get; }

        /// <summary>Gets the floating tool panel with TM:PE tool buttons.</summary>
        public UI.MainMenu.MainMenuPanel MainMenu { get; private set; }

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

        public ModUI() {
            Log._Debug("##### Initializing ModUI.");

            // Get the UIView object. This seems to be the top-level object for most
            // of the UI.
            UIView uiView = UIView.GetAView();

            // Add a new button to the view.
            MainMenuButton = (MainMenuButton)uiView.AddUIComponent(typeof(MainMenuButton));

            // add the menu
            MainMenu = (MainMenuPanel)uiView.AddUIComponent(typeof(MainMenuPanel));
            MainMenu.gameObject.AddComponent<CustomKeyHandler>();
#if DEBUG
            DebugMenu = (DebugMenuPanel)uiView.AddUIComponent(typeof(DebugMenuPanel));
#endif

            ToolMode = TrafficManagerMode.None;
        }

        ~ModUI() {
            Log._Debug("ModUI destructor is called.");
            Destroy(MainMenuButton);
            Destroy(MainMenu);
            ReleaseTool();
        }

        public bool IsVisible() {
            return _uiShown;
        }

        public void ToggleMainMenu() {
            if (IsVisible()) {
                Close();
            } else {
                Show();
            }
        }

        internal void RebuildMenu() {
            // Close();

            if (MainMenu != null) {
//                 CustomKeyHandler keyHandler = MainMenu.GetComponent<CustomKeyHandler>();
//                 if (keyHandler != null) {
//                     UnityEngine.Object.Destroy(keyHandler);
//                 }
//
//                 UnityEngine.Object.Destroy(MainMenu);
#if DEBUG
                 UnityEngine.Object.Destroy(DebugMenu);
#endif
                 MainMenu.OnRescaleRequested();
            }

            // UIView uiView = UIView.GetAView();
            // MainMenu = (MainMenuPanel)uiView.AddUIComponent(typeof(MainMenuPanel));
            // MainMenu.gameObject.AddComponent<CustomKeyHandler>();

#if DEBUG
            UIView uiView = UIView.GetAView();
            DebugMenu = (DebugMenuPanel)uiView.AddUIComponent(typeof(DebugMenuPanel));
#endif
        }

        public void Show() {
            try {
                ToolsModifierControl.mainToolbar.CloseEverything();
            } catch (Exception e) {
                Log.Error("Error on Show(): " + e);
            }

            foreach (BaseMenuButton button in GetMenu().Buttons) {
                button.UpdateButtonImageAndTooltip();
            }

            GetMenu().Show();
            LoadingExtension.TranslationDatabase.ReloadTutorialTranslations();
            LoadingExtension.TranslationDatabase.ReloadGuideTranslations();
            TrafficManagerTool.ShowAdvisor("MainMenu");
#if DEBUG
            GetDebugMenu().Show();
#endif
            SetToolMode(TrafficManagerMode.Activated);
            _uiShown = true;
            MainMenuButton.UpdateButtonImageAndTooltip();
            UIView.SetFocus(MainMenu);
        }

        public void Close() {
            GetMenu().Hide();
#if DEBUG
            GetDebugMenu().Hide();
#endif
            TrafficManagerTool tmTool = GetTrafficManagerTool(false);
            if (tmTool != null) {
                tmTool.SetToolMode(UI.ToolMode.None);
            }

            SetToolMode(TrafficManagerMode.None);
            _uiShown = false;
            MainMenuButton.UpdateButtonImageAndTooltip();
        }

        internal MainMenuPanel GetMenu() {
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

        public static void DisableTool() {
            Log._Debug("ModUI.DisableTool: called");
            if (ToolsModifierControl.toolController == null) {
                Log.Warning("ModUI.DisableTool: ToolsModifierControl.toolController is null!");
            } else if (tool == null) {
                Log.Warning("ModUI.DisableTool: tool is null!");
            } else if (ToolsModifierControl.toolController.CurrentTool != tool) {
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

        /// <summary>Called from settings window, when windows need rescaling because GUI scale
        /// slider has changed.</summary>
        public void NotifyGuiScaleChanged() {
            MainMenu.OnRescaleRequested();
        }
    }
}
