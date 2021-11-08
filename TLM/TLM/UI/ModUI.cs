namespace TrafficManager.UI {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using TrafficManager.UI.MainMenu;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.Lifecycle;
    using JetBrains.Annotations;
    using System.Collections;

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

        /// <returns>returns TMPE tool if it is alive, null otherwise</returns>
        public static TrafficManagerTool GetTrafficManagerTool() =>
            trafficManagerTool_ ?? null; // ?? is overloaded.

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
        public UIOpacityObservable UiOpacityObservable;

        [UsedImplicitly]
        public void Awake() {
            UiScaleObservable = new UIScaleObservable();
            UiOpacityObservable = new UIOpacityObservable();

            Log._Debug("##### Initializing ModUI.");

            CreateMainMenuButtonAndWindow();
#if DEBUG
            UIView uiView = UIView.GetAView();
            DebugMenu = (DebugMenuPanel)uiView.AddUIComponent(typeof(DebugMenuPanel));
#endif

            ToolMode = TrafficManagerMode.None;

            // One time load
            TMPELifecycle.Instance.TranslationDatabase.ReloadTutorialTranslations();
            TMPELifecycle.Instance.TranslationDatabase.ReloadGuideTranslations();

            StartCoroutine(StartTMPEToolCoroutine());
        }

        private static IEnumerator StartTMPEToolCoroutine() {
            // delay this to make sure everything else is ready.
            yield return 0;
            try {
                EnableTool();
                DisableTool();
            } catch(Exception ex) {
                ex.LogException();
            }
            yield break;
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
            if (ToolsModifierControl.toolController is null) {
                Log.Warning("ModUI.DisableTool: ToolsModifierControl.toolController is null!");
            } else if (!trafficManagerTool_) {
                Log.Warning("ModUI.DisableTool: trafficManagerTool_ does not exist!");
            } else if (ToolsModifierControl.toolController.CurrentTool != trafficManagerTool_) {
                Log.Info("ModUI.DisableTool: CurrentTool is not traffic manager tool!");
            } else {
                trafficManagerTool_.enabled = false;
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
