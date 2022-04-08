namespace TrafficManager.UI {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using TrafficManager.UI.MainMenu;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.Lifecycle;
    using JetBrains.Annotations;
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.UI.Textures;

    /// <summary>
    /// Globally available UI manager class which contains the main menu button and the panel.
    /// Access via ThreadingExtension.ModUi.
    /// </summary>
    public partial class ModUI : UICustomControl {
        /// <summary>Singleton accessor.</summary>
        public static ModUI Instance { get; private set; }

        /// <summary>Gets the floating draggable button which shows and hides TM:PE UI.</summary>
        public UI.MainMenu.MainMenuButton MainMenuButton { get; set; }

        /// <summary>Gets the floating tool panel with TM:PE tool buttons.</summary>
        public UI.MainMenu.MainMenuWindow MainMenu { get; set; }

#if DEBUG
        public DebugMenuPanel DebugMenu { get; private set; }
#endif

        /// <returns>returns TMPE tool if it is alive, null otherwise</returns>
        public static TrafficManagerTool GetTrafficManagerTool() =>
            trafficManagerTool_ ?? null; // ?? is overloaded.

        /// <summary>
        /// must only be called from EnableTool to avoid recursion.
        /// </summary>
        private static void EnsureTrafficManagerTool() {
            try {
                if(!trafficManagerTool_) {
                    Log.Info("Initializing traffic manager tool...");
                    GameObject toolModControl = ToolsModifierControl.toolController.gameObject;
                    trafficManagerTool_ =
                        toolModControl.GetComponent<TrafficManagerTool>()
                        ?? toolModControl.AddComponent<TrafficManagerTool>();
                }
            } catch (Exception ex) {
                ex.LogException(showInPanel: true);
            }
        }

        private static TrafficManagerTool trafficManagerTool_;

        public static TrafficManagerMode ToolMode { get; set; }

        private bool _uiShown;

        /// <summary>Subscribe to UI events here.</summary>
        public EventPublishers Events = new();

        [UsedImplicitly]
        public void Awake() {
            try {
                Instance = this;

                Log._Debug("##### Initializing ModUI.");

                CreateMainMenuButtonAndWindow();
#if DEBUG
                UIView uiView = UIView.GetAView();
                const string DEBUG_MENU_GAMEOBJECT_NAME = "TMPE_DebugMenu";
                DebugMenu = (DebugMenuPanel)uiView.AddUIComponent(typeof(DebugMenuPanel));
                UIUtil.MakeUniqueAndSetName(DebugMenu.gameObject, DEBUG_MENU_GAMEOBJECT_NAME);
#endif

                ToolMode = TrafficManagerMode.None;

                // One time load
                TMPELifecycle.Instance.TranslationDatabase.ReloadTutorialTranslations();
                TMPELifecycle.Instance.TranslationDatabase.ReloadGuideTranslations();

            } catch(Exception ex) {
                ex.LogException(true);
            }
        }

        public void Start() {
            // Tool must only be created from EnableTool to avoid recursion.
            EnableTool();
            DisableTool();
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
            DestroyImmediate(MainMenuButton);
            DestroyImmediate(MainMenu);
            ReleaseTool();
            Instance = null;
            DestroyImmediate(this);
        }

        public bool IsVisible() {
            return _uiShown;
        }

        public void ToggleMainMenu() {
            if (IsVisible()) {
                CloseMainMenu();
            } else {
                ShowMainMenu();
                GetTrafficManagerTool()?.RequestOnscreenDisplayUpdate();

                if (!TMPELifecycle.Instance.WhatsNew.Shown) {
                    WhatsNew.WhatsNew.OpenModal();
                }
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

                // intentionally DestroyImmediate() - normal Destroy() is delayed
                UnityEngine.Object.DestroyImmediate(MainMenu);
                UnityEngine.Object.DestroyImmediate(MainMenuButton);
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
            _uiShown = true;
            SetToolMode(TrafficManagerMode.Activated);
            MainMenuButton.UpdateButtonSkinAndTooltip();
            UIView.SetFocus(MainMenu);
        }

        public void CloseMainMenu() {
            // Before hiding the menu, shut down the active tool
            GetTrafficManagerTool()?.SetToolMode(UI.ToolMode.None);

            // Main menu is going invisible
            GetMenu().Hide();
#if DEBUG
            GetDebugMenu().Hide();
#endif

            _uiShown = false;
            SetToolMode(TrafficManagerMode.None);
            MainMenuButton.UpdateButtonSkinAndTooltip();
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
            EnsureTrafficManagerTool();

            ToolsModifierControl.toolController.CurrentTool = GetTrafficManagerTool();
        }

        public static void OnLevelLoaded() {
            Log._Debug("ModUI.OnLevelLoaded: called");
            if (ModUI.Instance == null) {
                Log._Debug("Adding UIBase instance.");
                ToolsModifierControl.toolController.gameObject.AddComponent<ModUI>();
            }
            TMPELifecycle.Instance.TranslationDatabase.ReloadTutorialTranslations();
            TMPELifecycle.Instance.TranslationDatabase.ReloadGuideTranslations();

            // Do not handle ChangeTheme result assuming that savegame always has the selected theme
            // and MPH display in a consistent state
            RoadSignThemeManager.Instance.ChangeTheme(
                newTheme: GlobalConfig.Instance.Main.RoadSignTheme,
                mphEnabled: GlobalConfig.Instance.Main.DisplaySpeedLimitsMph);
        }

        public static void DisableTool() {
            Log._Debug("ModUI.DisableTool: called");
            if (ToolsModifierControl.toolController is null) {
                Log.Warning("ModUI.DisableTool: ToolsModifierControl.toolController is null!");
            } else if (!trafficManagerTool_) {
                Log.Warning("ModUI.DisableTool: trafficManagerTool_ does not exist!");
            } else if (ToolsModifierControl.toolController.CurrentTool != trafficManagerTool_) {
                Log.Info("ModUI.DisableTool: CurrentTool is not traffic manager tool!");
            } else {
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
