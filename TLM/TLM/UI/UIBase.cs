namespace TrafficManager.UI {
    using System;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using UI.MainMenu;
    using UnityEngine;

    public class UIBase : UICustomControl {
        public UIMainMenuButton MainMenuButton { get; }

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

        public UIBase() {
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

        ~UIBase() {
            UnityEngine.Object.Destroy(MainMenuButton);
            UnityEngine.Object.Destroy(MainMenu);
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

            foreach (MenuButton button in GetMenu().Buttons) {
                button.UpdateProperties();
            }

            GetMenu().Show();
            LoadingExtension.TranslationDatabase.ReloadTutorialTranslations();
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
            if (mode == ToolMode) return;

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
            ToolsModifierControl.toolController.CurrentTool = ToolsModifierControl.GetTool<DefaultTool>();
            ToolsModifierControl.SetTool<DefaultTool>();
        }

        internal static void ReleaseTool() {
            if (ToolMode != TrafficManagerMode.None) {
                ToolMode = TrafficManagerMode.None;
                DestroyTool();
            }
        }

        private static void DestroyTool() {
            if (ToolsModifierControl.toolController != null) {
                ToolsModifierControl.toolController.CurrentTool = ToolsModifierControl.GetTool<DefaultTool>();
                ToolsModifierControl.SetTool<DefaultTool>();

                if (tool != null) {
                    UnityEngine.Object.Destroy(tool);
                    tool = null;
                }
            } else {
                Log.Warning("LoadingExtensions.DestroyTool: ToolsModifierControl.toolController is null!");
            }
        }
    }
}