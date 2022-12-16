// #define QUEUEDSTATS

namespace TrafficManager.UI.MainMenu {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using TrafficManager.API.Util;
    using TrafficManager.State.Keybinds;
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.Lifecycle;

    public partial class MainMenuWindow
        : U.Panel.BaseUWindowPanel,
          IObserver<GlobalConfig> {
        public const int DEFAULT_MENU_X = 85;
        public const int DEFAULT_MENU_Y = 60;
        public const string WINDOW_CONTROL_NAME = "TMPE_MainMenu_Window";

        /// <summary>
        /// Panel floating below the main menu and shows keybinds and mouse shortcuts.
        /// Panel is hidden if it contains no controls.
        /// </summary>
        internal OsdPanel OnscreenDisplayPanel;

        /// <summary>
        /// Button [?] in the corner which toggles keybinds
        /// </summary>
        private UButton toggleOsdButton_;

        /// <summary>Tool buttons occupy the left and bigger side of the main menu.</summary>
        private static readonly MenuButtonDef[] TOOL_BUTTON_DEFS
            = {
                new() {
                    ButtonType = typeof(ToggleTrafficLightsButton),
                    Mode = ToolMode.ToggleTrafficLight,
                    IsEnabledFunc = () => true, // always ON
                },
                new() {
                    ButtonType = typeof(TimedTrafficLightsButton),
                    Mode = ToolMode.TimedTrafficLights,
                    IsEnabledFunc = TimedTrafficLightsButton.IsButtonEnabled,
                },
                new() {
                    ButtonType = typeof(ManualTrafficLightsButton),
                    Mode = ToolMode.ManualSwitch,
                    IsEnabledFunc = ManualTrafficLightsButton.IsButtonEnabled,
                },
                new() {
                    ButtonType = typeof(LaneConnectorButton),
                    Mode = ToolMode.LaneConnector,
                    IsEnabledFunc = LaneConnectorButton.IsButtonEnabled,
                },
                new() {
                    ButtonType = typeof(LaneArrowsMenuButton),
                    Mode = ToolMode.LaneArrows,
                    IsEnabledFunc = () => true, // always ON
                },
                new() {
                    ButtonType = typeof(PrioritySignsButton),
                    Mode = ToolMode.AddPrioritySigns,
                    IsEnabledFunc = PrioritySignsButton.IsButtonEnabled,
                },
                new() {
                    ButtonType = typeof(JunctionRestrictionsButton),
                    Mode = ToolMode.JunctionRestrictions,
                    IsEnabledFunc = JunctionRestrictionsButton.IsButtonEnabled,
                },
                new() {
                    ButtonType = typeof(SpeedLimitsButton),
                    Mode = ToolMode.SpeedLimits,
                    IsEnabledFunc = SpeedLimitsButton.IsButtonEnabled,
                },
                new() {
                    ButtonType = typeof(VehicleRestrictionsButton),
                    Mode = ToolMode.VehicleRestrictions,
                    IsEnabledFunc = VehicleRestrictionsButton.IsButtonEnabled,
                },
                new() {
                    ButtonType = typeof(ParkingRestrictionsButton),
                    Mode = ToolMode.ParkingRestrictions,
                    IsEnabledFunc = ParkingRestrictionsButton.IsButtonEnabled,
                },
#if DEBUG
                new() {
                    ButtonType = typeof(RoutingDetectorButton),
                    Mode = ToolMode.RoutingDetector,
                    IsEnabledFunc = () => true, // always ON in debug mode
                },
#endif
        };

        /// <summary>Extra buttons occupy the right side of the main menu.</summary>
        private static readonly MenuButtonDef[] EXTRA_BUTTON_DEFS
            = {
                new() {
                    ButtonType = typeof(DespawnButton),
                    Mode = ToolMode.DespawnButton,
                    IsEnabledFunc = () => true,
                },
                new() {
                    ButtonType = typeof(ClearTrafficButton),
                    Mode = ToolMode.ClearTrafficButton,
                    IsEnabledFunc = () => true,
                },
            };

        /// <summary>List of buttons stores created UIButtons in order. </summary>
        public List<BaseMenuButton> ToolButtonsList;

        public List<BaseMenuButton> ExtraButtonsList;

        /// <summary>Dict of buttons allows quick search by toolmode.</summary>
        private Dictionary<ToolMode, BaseMenuButton> ButtonsDict;

        /// <summary>Used to determine drag box height.</summary>
        public UILabel VersionLabel { get; private set; }

        public UILabel StatsLabel { get; private set; }

        public UIDragHandle DragHandle { get; private set; }

        IDisposable confDisposable;

        private UITextureAtlas allButtonsAtlas_;

        public override void Start() {
            base.Start();

            U.UIUtil.MakeUniqueAndSetName(
                toMakeUnique: this.gameObject,
                name: WINDOW_CONTROL_NAME);

            GlobalConfig conf = GlobalConfig.Instance;

            OnUpdate(conf);

            confDisposable = conf.Subscribe(this);
            SetupWindow();
        }

        /// <summary>
        /// Called from ModUI when need to create or re-create the MainMenu panel.
        /// </summary>
        /// <returns>The created panel.</returns>
        internal static MainMenuWindow CreateMainMenuWindow() {
            // UIView parent = UIView.GetAView();
            // MainMenuWindow window = (MainMenuWindow)parent.AddUIComponent(typeof(MainMenuWindow));

            var builder = U.UBuilder.Create(
                abAtlasName: "MainMenu_Atlas",
                abLoadingPath: "MainMenu.Tool",
                abSizeHint: new IntVector2(512));
            var window = builder.CreateWindow<MainMenuWindow>();
            window.gameObject.AddComponent<CustomKeyHandler>();

            window.ResizeFunction((UResizer r) => { r.FitToChildren(); });
            window.SetPadding(UPadding.Default);
            window.SetupControls(builder);

            // Resize everything correctly
            window.ForceUpdateLayout();
            return window;
        }

        /// <summary>Called from constructor to setup own properties and events.</summary>
        private void SetupWindow() {
            this.name = WINDOW_CONTROL_NAME;
            this.isVisible = false;
            this.backgroundSprite = "GenericPanel";
            this.color = new Color32(64, 64, 64, 240);
            this.SetOpacity(
                U.UOpacityValue.FromOpacity(0.01f * GlobalConfig.Instance.Main.GuiOpacity));

            var dragHandler = new GameObject("TMPE_Menu_DragHandler");
            dragHandler.transform.parent = transform;
            dragHandler.transform.localPosition = Vector3.zero;
            this.DragHandle = dragHandler.AddComponent<UIDragHandle>();
            this.DragHandle.enabled = !GlobalConfig.Instance.Main.MainMenuPosLocked;

            this.eventVisibilityChanged += OnVisibilityChanged;
        }

        /// <summary>Called from ModUI to setup children for the window.</summary>
        /// <param name="builder">The UI Builder.</param>
        public void SetupControls(UBuilder builder) {
            // Create and populate list of background atlas keys, used by all buttons
            // And also each button will have a chance to add their own atlas keys for loading.
            var tmpSkin = ButtonSkin.CreateSimple(
                                        foregroundPrefix: "MainMenuPanel",
                                        backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG)
                                    .NormalForeground(false)
                                    .CanHover(foreground: false)
                                    .CanActivate(foreground: false);

            // By default the atlas will include backgrounds: DefaultRound-bg-normal
            tmpSkin.UpdateAtlasBuilder(
                atlasBuilder: builder.AtlasBuilder,
                spriteSize: new IntVector2(50));

            // Create Version Label and Help button:
            // [ TM:PE 11.x ] [?]
            UILabel versionLabel = SetupControls_TopRow(builder);

            // Main menu contains 2 panels side by side, one for tool buttons and another for
            // despawn & clear buttons.
            ButtonsDict = new Dictionary<ToolMode, BaseMenuButton>();
            // U.UPanel leftPanel;

            UPanel innerPanel = builder.Panel_(parent: this);
            innerPanel.name = "TMPE_MainMenu_InnerPanel";

            innerPanel.ResizeFunction(
                r => {
                    r.Stack(mode: UStackMode.Below, spacing: 0f, stackRef: versionLabel);
                    r.FitToChildren();
                });

            ToolPanel.AddButtonsResult toolButtonsResult =
                SetupControls_ToolPanel(innerPanel, builder);
            SetupControls_ExtraPanel(innerPanel, builder, toolButtonsResult);

            // Create atlas and give it to all buttons
            allButtonsAtlas_ = builder.AtlasBuilder.CreateAtlas();

            foreach (BaseMenuButton b in ToolButtonsList) {
                b.atlas = allButtonsAtlas_;
            }

            foreach (BaseMenuButton b in ExtraButtonsList) {
                b.atlas = allButtonsAtlas_;
            }

            this.toggleOsdButton_.atlas = allButtonsAtlas_;

            //-------------------------------------------------------------------------
            // Foldable panel with keybinds, starts hidden below or above the main menu
            //-------------------------------------------------------------------------
            SetupControls_OnscreenDisplayPanel(builder);

            // Floating labels under TM:PE window
            SetupControls_DebugLabels(builder, this.OnscreenDisplayPanel);
        }

        private void SetupControls_OnscreenDisplayPanel(UBuilder builder) {
            this.OnscreenDisplayPanel = builder.Panel<OsdPanel>(
                parent: this,
                stack: UStackMode.NewRowBelow);
            this.OnscreenDisplayPanel.SetupControls(this, builder);
        }

        private UILabel SetupControls_TopRow(UBuilder builder) {
            //-------------------------------------------------------
            // Mod name/version label (also serves as a drag handle)
            //-------------------------------------------------------
            UILabel versionLabel = builder.Label<U.ULabel>(
                parent: this,
                t: TrafficManagerMod.ModName,
                stack: UStackMode.Below);
            this.VersionLabel = versionLabel;

            //-------------------------------------------------------
            // (?) button which toggles On-Screen Display help panel
            //-------------------------------------------------------
            var osdToggle = builder.Button<U.UButton>(
                parent: this,
                text: string.Empty,
                tooltip: Translation.Menu.Get("Tooltip:Toggle onscreen display panel"),
                size: new Vector2(18f, 18f),
                stack: UStackMode.None);

            this.toggleOsdButton_ = osdToggle;
            osdToggle.atlas = this.allButtonsAtlas_;
            osdToggle.name = "TMPE_MainMenu_HelpButton";

            // Texture for Help will be included in the `allButtonsAtlas_`
            ButtonSkin skin = ButtonSkin.CreateSimple(
                                            backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG,
                                            foregroundPrefix: "Help")
                                        .CanHover(foreground: false)
                                        .CanActivate();
            skin.UpdateAtlasBuilder(
                atlasBuilder: builder.AtlasBuilder,
                spriteSize: new IntVector2(50));

            osdToggle.Skin = skin;
            osdToggle.ApplyButtonSkin();

            osdToggle.ResizeFunction(
                resizeFn: r => {
                    r.Control.isVisible = true; // not sure why its hidden on create? TODO
                    r.Stack(
                        mode: UStackMode.ToTheRight,
                        spacing: UConst.UIPADDING * 3f,
                        stackRef: versionLabel);
                });

            osdToggle.uCanActivate = _ => true;
            osdToggle.uIsActive = _ => GlobalConfig.Instance.Main.KeybindsPanelVisible;
            osdToggle.uOnClick += (component, _) => {
                ModUI.Instance.MainMenu.OnToggleOsdButtonClicked(component as U.UButton);
            };

            return versionLabel;
        }

        private void OnToggleOsdButtonClicked(U.UButton button) {
            bool value = !GlobalConfig.Instance.Main.KeybindsPanelVisible;
            GlobalConfig.Instance.Main.KeybindsPanelVisible = value;
            GlobalConfig.WriteConfig();

            Log._Debug($"Toggle value of KeybindsPanelVisible to {value}");

            // Refer to the TrafficManager tool asking it to request help from the current tool
            ModUI.GetTrafficManagerTool()?.RequestOnscreenDisplayUpdate();
        }

        private void SetupControls_DebugLabels(UBuilder builder,
                                               UIComponent stackUnder) {
            // Pathfinder stats label (debug only)
            if (SavedGameOptions.Instance.showPathFindStats) {
                var statsLabel = builder.Label<StatsLabel>(
                    parent: this,
                    t: string.Empty);

                // Allow the label to hang outside the parent box
                statsLabel.ContributeToBoundingBox(false);

                UIComponent under = stackUnder; // copy for the closure to work
                statsLabel.ResizeFunction(
                    resizeFn: (UResizer r) => {
                        // Extra 2x spacing because of form's inner padding
                        r.Stack(
                            mode: UStackMode.Below,
                            spacing: 2f * UConst.UIPADDING,
                            stackRef: under);
                    });
                stackUnder = this.StatsLabel = statsLabel;
            }

            if (TMPELifecycle.Instance.InGameHotReload) {
                // Hot Reload version label (debug only)
                var text = $"HOT RELOAD {Assembly.GetExecutingAssembly().GetName().Version}";
                ULabel hotReloadLabel = builder.Label<U.ULabel>(parent: this, t: text);

                // Allow the label to hang outside the parent box
                hotReloadLabel.ContributeToBoundingBox(false);

                hotReloadLabel.ResizeFunction(
                    (UResizer r) => {
                        // If pathFind stats label above was not visible, we need extra spacing
                        float extraSpacing = SavedGameOptions.Instance.showPathFindStats ? UConst.UIPADDING : 0f;
                        r.Stack(
                            mode: UStackMode.Below,
                            spacing: extraSpacing + UConst.UIPADDING,
                            stackRef: stackUnder);
                    });
            }
        }

        /// <summary>Left side panel with 1 or 2 rows of all tool buttons.</summary>
        internal ToolPanel toolPanel;

        /// <summary>Right side grey panel with extra buttons.</summary>
        internal ToolPanel rightPanel;

        private ToolPanel.AddButtonsResult
            SetupControls_ToolPanel(UPanel parent, UBuilder builder) {
            // This is tool buttons panel
            toolPanel = builder.Panel<ToolPanel>(parent: this);
            return toolPanel.SetupToolButtons(this, builder);
        }

        private void SetupControls_ExtraPanel(UPanel parent,
                                              UBuilder builder,
                                              ToolPanel.AddButtonsResult toolButtonsResult) {
            // This is toggle despawn and clear traffic panel
            rightPanel = builder.Panel<ToolPanel>(parent: this);
            rightPanel.SetupExtraButtons(this, builder, toolButtonsResult);
        }

        public override void OnBeforeResizerUpdate() {
            if (this.DragHandle != null) {
                // Drag handle is manually resized to the form width, but when the form is large,
                // the handle prevents it from shrinking. So shrink now, size properly after.
                this.DragHandle.size = Vector2.one;
            }
        }

        /// <summary>Called by UResizer for every control to be 'resized'.</summary>
        public override void OnAfterResizerUpdate() {
            if (this.DragHandle != null) {
                this.DragHandle.size = this.VersionLabel.size;

                // Push the window back into screen if the label/draghandle are partially offscreen
                U.UIUtil.ClampToScreen(
                    window: this,
                    alwaysVisible: VersionLabel);
            }
        }

        private void OnVisibilityChanged(UIComponent component, bool value) {
            VersionLabel.enabled = value;

            if (StatsLabel != null) {
                // might not exist
                StatsLabel.enabled = SavedGameOptions.Instance.showPathFindStats && value;
            }

            UResizer.UpdateControl(this);
        }

        public override void OnDestroy() {
            eventVisibilityChanged -= OnVisibilityChanged;
            confDisposable?.Dispose();

            if (allButtonsAtlas_) {
                TextureUtil.DestroyTextureAtlasAndContents(allButtonsAtlas_);
                allButtonsAtlas_ = null;
            }
            base.OnDestroy();
        }

        internal void SetPosLock(bool lck) {
            DragHandle.enabled = !lck;
        }

        protected override void OnPositionChanged() {
            GlobalConfig config = GlobalConfig.Instance;

            bool posChanged = config.Main.MainMenuX != (int)absolutePosition.x ||
                              config.Main.MainMenuY != (int)absolutePosition.y;

            if (posChanged) {
                Log._Debug($"Menu position changed to {absolutePosition.x}|{absolutePosition.y}");

                config.Main.MainMenuX = (int)absolutePosition.x;
                config.Main.MainMenuY = (int)absolutePosition.y;

                GlobalConfig.WriteConfig();
            }

            base.OnPositionChanged();
        }

        private int lastUpdatePositionFrame_ = 0;

        public void OnUpdate(GlobalConfig config) {
            int nowFrame = Time.frameCount;
            int diff = nowFrame - this.lastUpdatePositionFrame_;

            // Do not call UpdatePosition more than once every 60 frames
            if (diff > 60) {
                UpdatePosition(new Vector2(config.Main.MainMenuX, config.Main.MainMenuY));
                lastUpdatePositionFrame_ = nowFrame;
            }
        }

        /// <summary>Always invalidates the main menu, do not call too often!</summary>
        /// <param name="pos">Config main menu position.</param>
        public void UpdatePosition(Vector2 pos) {
            this.absolutePosition = new Vector2(pos.x, pos.y);
            if (U.UIUtil.ClampToScreen(window: this, alwaysVisible: this.VersionLabel)) {
                Log.Info($"Moving main menu pos={this.absolutePosition}");
            }

            Invalidate();
        }

        public void OnGUI() {
            // Return if modal window is active or the main menu is hidden
            if (!isVisible || UIView.HasModalInput() || UIView.HasInputFocus()) {
                return;
            }

            // Some safety checks to not trigger while full screen/modals are open
            // Check the key and then click the corresponding button
            if (KeybindSettingsBase.ToggleTrafficLightTool.IsPressed(Event.current)) {
                ClickToolButton(ToolMode.ToggleTrafficLight);
            } else if (KeybindSettingsBase.LaneArrowTool.IsPressed(Event.current)) {
                ClickToolButton(ToolMode.LaneArrows);
            } else if (KeybindSettingsBase.LaneConnectionsTool.IsPressed(Event.current)) {
                ClickToolButton(ToolMode.LaneConnector);
            } else if (KeybindSettingsBase.PrioritySignsTool.IsPressed(Event.current)) {
                ClickToolButton(ToolMode.AddPrioritySigns);
            } else if (KeybindSettingsBase.JunctionRestrictionsTool.IsPressed(Event.current)) {
                ClickToolButton(ToolMode.JunctionRestrictions);
            } else if (KeybindSettingsBase.SpeedLimitsTool.IsPressed(Event.current)) {
                ClickToolButton(ToolMode.SpeedLimits);
            }
        }

        /// <summary>For given button mode, send click.</summary>
        internal void ClickToolButton(ToolMode toolMode) {
            if (ButtonsDict.TryGetValue(toolMode, out var b)) {
                b.SimulateClick();
            }
        }

        public void UpdateButtons() {
            foreach (BaseMenuButton button in this.ToolButtonsList) {
                button.UpdateButtonSkinAndTooltip();
            }

            foreach (BaseMenuButton button in this.ExtraButtonsList) {
                button.UpdateButtonSkinAndTooltip();
            }
        }

        public override void Awake() {
            UIScaler.Reset();
            base.Awake();
        }

        protected override void OnResolutionChanged(Vector2 previousResolution, Vector2 currentResolution) {
            UIScaler.Reset();
            base.OnResolutionChanged(previousResolution, currentResolution);
        }

    } // end class
}