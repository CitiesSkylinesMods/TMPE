namespace TrafficManager.UI {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using TrafficManager.U;
    using TrafficManager.UI.MainMenu;
    using UnityEngine;

    /// <summary>
    /// Globally available UI manager class which contains the main menu button and the panel.
    /// Access via ThreadingExtension.ModUi.
    /// </summary>
    public class ModUI : UICustomControl {
        /// <summary>
        /// List of main menu button functions, used for loading textures.
        /// </summary>
        public List<string> ButtonList = new List<string> {
                                                              "LaneConnector",
                                                              "ClearTraffic",
                                                              "DespawnDisabled",
                                                              "DespawnEnabled",
                                                              "JunctionRestrictions",
                                                              "LaneArrows",
                                                              "ManualTrafficLights",
                                                              "PrioritySigns",
                                                              "SpeedLimits",
                                                              "TimedTrafficLights",
                                                              "ToggleTrafficLights",
                                                              "VehicleRestrictions",
                                                              "ParkingRestrictions",
                                                          };

        /// <summary>
        /// Gets the floating draggable button which shows and hides TM:PE UI.
        /// </summary>
        public UIMainMenuButton MainMenuButton { get; }

        /// <summary>
        /// Gets the floating tool panel with TM:PE tool buttons.
        /// </summary>
        public MainMenuPanel MainMenu { get; private set; }

#if DEBUG
        public DebugMenuPanel DebugMenu { get; private set; }
#endif

        public static TrafficManagerTool GetTrafficManagerTool(bool createIfRequired = true) {
            if (tool == null && createIfRequired) {
                Log.Info("Initializing traffic manager tool...");
                GameObject toolModControl = ToolsModifierControl.toolController.gameObject;
                tool = toolModControl.GetComponent<TrafficManagerTool>()
                       ?? toolModControl.AddComponent<TrafficManagerTool>();
                tool.Initialize();
            }

            return tool;
        }

        private static TrafficManagerTool tool;

        public static TrafficManagerMode ToolMode { get; set; }

        private bool _uiShown;

        public ModUI() {
            Log._Debug("##### Initializing UIBase.");

            // Get the UIView object. This seems to be the top-level object for most
            // of the UI.
            UIView uiView = UIView.GetAView();

            // Add a new button to the view.
            MainMenuButton = (UIMainMenuButton)uiView.AddUIComponent(typeof(UIMainMenuButton));

            // add the menu
            MainMenu = (MainMenuPanel)uiView.AddUIComponent(typeof(MainMenuPanel));
            MainMenu.gameObject.AddComponent<CustomKeyHandler>();
#if DEBUG
            DebugMenu = (DebugMenuPanel)uiView.AddUIComponent(typeof(DebugMenuPanel));
#endif

            ToolMode = TrafficManagerMode.None;
        }

        ~ModUI() {
            Log._Debug("UIBase destructor is called.");
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
            Close();

            if (MainMenu != null) {
                CustomKeyHandler keyHandler = MainMenu.GetComponent<CustomKeyHandler>();
                if(keyHandler != null) {
                    UnityEngine.Object.Destroy(keyHandler);
                }

                UnityEngine.Object.Destroy(MainMenu);
#if DEBUG
                UnityEngine.Object.Destroy(DebugMenu);
#endif
            }

            UIView uiView = UIView.GetAView();
            MainMenu = (MainMenuPanel)uiView.AddUIComponent(typeof(MainMenuPanel));
            MainMenu.gameObject.AddComponent<CustomKeyHandler>();
#if DEBUG
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
                button.UpdateProperties();
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
            MainMenuButton.UpdateSprites();
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
            MainMenuButton.UpdateSprites();
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
            Log._Debug("LoadingExtension.EnableTool: called");
            TrafficManagerTool tmTool = GetTrafficManagerTool(true);

            ToolsModifierControl.toolController.CurrentTool = tmTool;
            ToolsModifierControl.SetTool<TrafficManagerTool>();
        }

        public static void DisableTool() {
            Log._Debug("LoadingExtension.DisableTool: called");
            if (ToolsModifierControl.toolController != null) {
                ToolsModifierControl.toolController.CurrentTool = ToolsModifierControl.GetTool<DefaultTool>();
                ToolsModifierControl.SetTool<DefaultTool>();
            } else {
                Log.Warning("LoadingExtensions.DisableTool: ToolsModifierControl.toolController is null!");
            }
    }


        internal static void ReleaseTool() {
            ToolMode = TrafficManagerMode.None;
            DestroyTool();
        }

        private static void DestroyTool() {
            DisableTool();
            if (tool != null) {
                Log.Info("Removing Traffic Manager Tool.");
                UnityEngine.Object.Destroy(tool);
                tool = null;
            } // end if
        } // end DestroyTool()
    }
}