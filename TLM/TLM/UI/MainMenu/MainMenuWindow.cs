// #define QUEUEDSTATS
namespace TrafficManager.UI.MainMenu {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using TrafficManager.API.Util;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State.Keybinds;
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using TrafficManager.Util;
    using UnityEngine;

    public class MainMenuWindow
        : U.Panel.BaseUWindowPanel,
          IObserver<GlobalConfig>
    {
        public const int DEFAULT_MENU_X = 85;
        public const int DEFAULT_MENU_Y = 60;
        public const string WINDOW_CONTROL_NAME = "TMPE_MainMenu_Window";

        /// <summary>
        /// Panel floating below the main menu and shows keybinds and mouse shortcuts.
        /// Panel is hidden if it contains no controls.
        /// </summary>
        public UPanel OnscreenDisplayPanel { get; set; }

        /// <summary>
        /// Button [?] in the corner which toggles keybinds
        /// </summary>
        private UButton toggleOsdButton_;

        /// <summary>Tool buttons occupy the left and bigger side of the main menu.</summary>
        private static readonly MenuButtonDef[] TOOL_BUTTON_DEFS
            = {
                  new MenuButtonDef {
                                        ButtonType = typeof(ToggleTrafficLightsButton),
                                        Mode = ToolMode.ToggleTrafficLight,
                                        IsEnabledFunc = () => true, // always ON
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(TimedTrafficLightsButton),
                                        Mode = ToolMode.TimedTrafficLights,
                                        IsEnabledFunc = TimedTrafficLightsButton.IsButtonEnabled,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(ManualTrafficLightsButton),
                                        Mode = ToolMode.ManualSwitch,
                                        IsEnabledFunc = ManualTrafficLightsButton.IsButtonEnabled,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(LaneConnectorButton),
                                        Mode = ToolMode.LaneConnector,
                                        IsEnabledFunc = LaneConnectorButton.IsButtonEnabled,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(LaneArrowsMenuButton),
                                        Mode = ToolMode.LaneArrows,
                                        IsEnabledFunc = () => true, // always ON
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(PrioritySignsButton),
                                        Mode = ToolMode.AddPrioritySigns,
                                        IsEnabledFunc = PrioritySignsButton.IsButtonEnabled,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(JunctionRestrictionsButton),
                                        Mode = ToolMode.JunctionRestrictions,
                                        IsEnabledFunc = JunctionRestrictionsButton.IsButtonEnabled,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(SpeedLimitsButton),
                                        Mode = ToolMode.SpeedLimits,
                                        IsEnabledFunc = SpeedLimitsButton.IsButtonEnabled,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(VehicleRestrictionsButton),
                                        Mode = ToolMode.VehicleRestrictions,
                                        IsEnabledFunc = VehicleRestrictionsButton.IsButtonEnabled,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(ParkingRestrictionsButton),
                                        Mode = ToolMode.ParkingRestrictions,
                                        IsEnabledFunc = ParkingRestrictionsButton.IsButtonEnabled,
                                    },
              };

        /// <summary>Extra buttons occupy the right side of the main menu.</summary>
        private static readonly MenuButtonDef[] EXTRA_BUTTON_DEFS
            = {
                  new MenuButtonDef {
                                        ButtonType = typeof(DespawnButton),
                                        Mode = ToolMode.DespawnButton,
                                        IsEnabledFunc = () => true,
                                    },
                  new MenuButtonDef {
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

        private bool isStarted_;

        private UITextureAtlas allButtonsAtlas_;

        /// <summary>Defines button placement on the main menu since last layout reset.</summary>
        private MainMenuLayout menuLayout_;

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
            UIView parent = UIView.GetAView();
            MainMenuWindow window = (MainMenuWindow)parent.AddUIComponent(typeof(MainMenuWindow));

            window.gameObject.AddComponent<CustomKeyHandler>();

            using (var builder = new U.UiBuilder<MainMenuWindow>(window)) {
                builder.ResizeFunction(r => { r.FitToChildren(); });
                builder.SetPadding(UConst.UIPADDING);

                window.SetupControls(builder);
                // window.SetTransparency(GlobalConfig.Instance.Main.GuiTransparency);

                // Resize everything correctly
                builder.Done();
            }

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
        public void SetupControls(UiBuilder<MainMenuWindow> builder) {
            // Create and populate list of background atlas keys, used by all buttons
            // And also each button will have a chance to add their own atlas keys for loading.
            var tmpSkin = new U.ButtonSkin {
                Prefix = "MainMenuPanel",
                BackgroundPrefix = "RoundButton",
                ForegroundNormal = false,
                BackgroundHovered = true,
                BackgroundActive = true,
            };
            // By default the atlas will include backgrounds: DefaultRound-bg-normal
            var atlasBuilder = new U.AtlasBuilder();
            tmpSkin.UpdateAtlasBuilder(
                atlasBuilder: atlasBuilder,
                spriteSize: new IntVector2(50));

            // Create Version Label and Help button:
            // [ TM:PE 11.x ] [?]
            UILabel versionLabel = SetupControls_TopRow(builder, atlasBuilder);

            // Main menu contains 2 panels side by side, one for tool buttons and another for
            // despawn & clear buttons.
            ButtonsDict = new Dictionary<ToolMode, BaseMenuButton>();
            // U.UPanel leftPanel;

            using (var innerPanelB = builder.ChildPanel<U.UPanel>(setupFn: p => {
                p.name = "TMPE_MainMenu_InnerPanel";
            })) {
                innerPanelB.ResizeFunction(r => {
                    r.Stack(mode: UStackMode.Below, spacing: 0f, stackRef: versionLabel);
                    r.FitToChildren();
                });

                AddButtonsResult toolButtonsResult
                    = SetupControls_ToolPanel(innerPanelB, atlasBuilder);

                SetupControls_ExtraPanel(innerPanelB, atlasBuilder, toolButtonsResult);
            }

            // Create atlas and give it to all buttons
            allButtonsAtlas_ = atlasBuilder.CreateAtlas(
                atlasName: "MainMenu_Atlas",
                loadingPath: "MainMenu.Tool",
                atlasSizeHint: new IntVector2(512));

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

        private void SetupControls_OnscreenDisplayPanel(UiBuilder<MainMenuWindow> builder) {
            using (var osdBuilder = builder.ChildPanel<U.UPanel>(
                p => {
                    p.name = "TMPE_MainMenu_KeybindsPanel";
                    // the GenericPanel sprite is Light Silver, make it dark
                    p.atlas = TextureUtil.FindAtlas("Ingame");
                    p.backgroundSprite = "GenericPanel";
                    p.color = new Color32(64, 64, 64, 240);
                    p.opacity = GlobalConfig.Instance.Main.KeybindsPanelVisible
                                    ? 1f
                                    : 0f;
                }))
            {
                osdBuilder.SetPadding(UConst.UIPADDING);

                // The keybinds panel belongs to main menu but does not expand it to fit
                UResizerConfig.From(osdBuilder.Control).ContributeToBoundingBox = false;
                this.OnscreenDisplayPanel = osdBuilder.Control;

                // bool keybindsVisible = GlobalConfig.Instance.Main.KeybindsPanelVisible;
                // this.OnscreenDisplayPanel.gameObject.SetActive(keybindsVisible);

                osdBuilder.ResizeFunction(
                    r => {
                        r.Stack(mode: UStackMode.Below, spacing: UConst.UIPADDING * 2);

                        // As the control technically belongs inside the mainmenu, it will respect
                        // the 4px padding, we want to shift it slightly left to line up with the
                        // main menu panel.
                        r.MoveBy(new Vector2(-UConst.UIPADDING, 0f));
                        r.FitToChildren();
                    });
            }
        }

        private UILabel SetupControls_TopRow(UiBuilder<MainMenuWindow> builder,
                                             U.AtlasBuilder atlasBuilder) {
            UILabel versionLabel;

            using (var versionLabelB = builder.Label<U.ULabel>(TrafficManagerMod.ModName)) {
                versionLabelB.ResizeFunction(r => r.Stack(UStackMode.Below));
                this.VersionLabel = versionLabel = versionLabelB.Control;
            }

            using (var btnBuilder = builder.Button<U.UButton>()) {
                UButton control = btnBuilder.Control;
                this.toggleOsdButton_ = control;
                control.atlas = this.allButtonsAtlas_;
                control.name = "TMPE_MainMenu_HelpButton";

                // Texture for Help will be included in the `allButtonsAtlas_`
                ButtonSkin skin = new ButtonSkin {
                    BackgroundPrefix = "RoundButton",
                    Prefix = "Help",
                    BackgroundHovered = true,
                    BackgroundActive = true,
                    ForegroundActive = true,
                };
                skin.UpdateAtlasBuilder(
                    atlasBuilder,
                    spriteSize: new IntVector2(50));

                control.Skin = skin;
                control.UpdateButtonImage();

                // This has to be done later when form setup is done:
                // helpB.Control.atlas = allButtonsAtlas_;

                btnBuilder.ResizeFunction(
                    resizeFn: r => {
                        r.Control.isVisible = true; // not sure why its hidden on create? TODO
                        r.Stack(mode: UStackMode.ToTheRight,
                                spacing: UConst.UIPADDING * 3f,
                                stackRef: versionLabel);
                        r.Width(UValue.FixedSize(18f)); // assume Version label is 18pt high
                        r.Height(UValue.FixedSize(18f));
                    });

                control.uCanActivate = c => true;
                control.uTooltip = Translation.Menu.Get("Tooltip:Toggle onscreen display panel");

                control.uIsActive =
                    c => GlobalConfig.Instance.Main.KeybindsPanelVisible;

                control.uOnClick += (component, eventParam) => {
                    ModUI.Instance.MainMenu.OnToggleOsdButtonClicked(component as U.UButton);
                };
            }

            return versionLabel;
        }

        private void OnToggleOsdButtonClicked(U.UButton button) {
            bool value = !GlobalConfig.Instance.Main.KeybindsPanelVisible;
            GlobalConfig.Instance.Main.KeybindsPanelVisible = value;
            GlobalConfig.WriteConfig();

            Log._Debug($"Toggle value of KeybindsPanelVisible to {value}");

            // Refer to the TrafficManager tool asking it to request help from the current tool
            ModUI.GetTrafficManagerTool().RequestOnscreenDisplayUpdate();
        }

        private void SetupControls_DebugLabels(UiBuilder<MainMenuWindow> builder,
                                               UIComponent stackUnder) {
            // Pathfinder stats label (debug only)
            if (Options.showPathFindStats) {
                using (var statsLabelB = builder.Label<StatsLabel>(string.Empty)) {
                    // Allow the label to hang outside the parent box
                    UResizerConfig.From(statsLabelB.Control).ContributeToBoundingBox = false;

                    UIComponent under = stackUnder; // copy for the closure to work
                    statsLabelB.ResizeFunction(
                        r => {
                            // Extra 2x spacing because of form's inner padding
                            r.Stack(
                                mode: UStackMode.Below,
                                spacing: 2f * UConst.UIPADDING,
                                stackRef: under);
                        });
                    stackUnder = this.StatsLabel = statsLabelB.Control;
                }
            }

            // Hot Reload version label (debug only)
            if (TrafficManagerMod.Instance.InGameHotReload) {
                // Hot Reload version label (debug only)
                string text = $"HOT RELOAD {Assembly.GetExecutingAssembly().GetName().Version}";
                using (var hotReloadB = builder.Label<U.ULabel>(text)) {
                    // Allow the label to hang outside the parent box
                    UResizerConfig.From(hotReloadB.Control).ContributeToBoundingBox = false;

                    hotReloadB.ResizeFunction(
                        r => {
                            // If pathFind stats label above was not visible, we need extra spacing
                            float extraSpacing = Options.showPathFindStats ? UConst.UIPADDING : 0f;
                            r.Stack(
                                mode: UStackMode.Below,
                                spacing: extraSpacing + UConst.UIPADDING,
                                stackRef: stackUnder);
                        });
                }
            }
        }

        private AddButtonsResult SetupControls_ToolPanel(UiBuilder<UPanel> innerPanelB,
                                                         U.AtlasBuilder atlasBuilder) {
            // This is tool buttons panel
            using (UiBuilder<UPanel> leftPanelB = innerPanelB.ChildPanel<U.UPanel>(
                setupFn: panel => {
                    panel.name = "TMPE_MainMenu_ToolPanel";
                }))
            {
                leftPanelB.ResizeFunction(r => {
                    r.Stack(mode: UStackMode.Below);
                    r.FitToChildren();
                });

                // Create 1 or 2 rows of button objects
                var toolButtonsResult = AddButtonsFromButtonDefinitions(
                    builder: leftPanelB,
                    atlasBuilder: atlasBuilder,
                    buttonDefs: TOOL_BUTTON_DEFS,
                    minRowLength: 4);
                ToolButtonsList = toolButtonsResult.Buttons;

                return toolButtonsResult;
            }
        }

        private void SetupControls_ExtraPanel(UiBuilder<UPanel> innerPanelB,
                                              U.AtlasBuilder atlasBuilder,
                                              AddButtonsResult toolButtonsResult) {
            // This is toggle despawn and clear traffic panel
            using (UiBuilder<UPanel> rightPanelB = innerPanelB.ChildPanel<U.UPanel>(
                setupFn: p => {
                    p.name = "TMPE_MainMenu_ExtraPanel";
                    // Silver background panel
                    p.atlas = TextureUtil.FindAtlas("Ingame");
                    p.backgroundSprite = "GenericPanel";
                    // The panel will be Dark Silver at 50% dark 100% alpha
                    p.color = new Color32(128, 128, 128, 255);
                }))
            {
                rightPanelB.ResizeFunction(r => {
                    // Step to the right by 4px
                    r.Stack(mode: UStackMode.ToTheRight,
                            spacing: UConst.UIPADDING);
                    r.FitToChildren();
                });

                // Place two extra buttons (despawn & clear traffic).
                // Use as many rows as in the other panel.
                var extraButtonsResult = AddButtonsFromButtonDefinitions(
                    builder: rightPanelB,
                    atlasBuilder: atlasBuilder,
                    buttonDefs: EXTRA_BUTTON_DEFS,
                    minRowLength: toolButtonsResult.Layout.Rows == 2 ? 1 : 2);
                ExtraButtonsList = extraButtonsResult.Buttons;
            }
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
                U.UIUtil.ClampToScreen(window: this,
                                       alwaysVisible: VersionLabel);
            }
        }

        private struct AddButtonsResult {
            public List<BaseMenuButton> Buttons;
            public MainMenuLayout Layout;
        }

        /// <summary>Create buttons and add them to the given panel UIBuilder.</summary>
        /// <param name="builder">UI builder to use.</param>
        /// <param name="atlasKeysSet">Atlas keys to update for button images.</param>
        /// <param name="buttonDefs">Button defs collection to create from it.</param>
        /// <param name="minRowLength">Longest the row can be without breaking.</param>
        /// <returns>A list of created buttons.</returns>
        private AddButtonsResult AddButtonsFromButtonDefinitions(UiBuilder<UPanel> builder,
                                                                 U.AtlasBuilder atlasBuilder,
                                                                 MenuButtonDef[] buttonDefs,
                                                                 int minRowLength)
        {
            AddButtonsResult result;
            result.Buttons = new List<BaseMenuButton>();

            // Count the button objects and set their layout
            result.Layout = new MainMenuLayout();
            result.Layout.CountEnabledButtons(buttonDefs);
            int placedInARow = 0;

            foreach (MenuButtonDef buttonDef in buttonDefs) {
                if (!buttonDef.IsEnabledFunc()) {
                    // Skip buttons which are not enabled
                    continue;
                }

                // Create and populate the panel with buttons
                var buttonBuilder = builder.Button<BaseMenuButton>(buttonDef.ButtonType);

                // Count buttons in a row and break the line
                bool doRowBreak = result.Layout.IsRowBreak(placedInARow, minRowLength);

                buttonBuilder.ResizeFunction(r => {
                    r.Stack(doRowBreak ? UStackMode.NewRowBelow : UStackMode.ToTheRight);
                    r.Width(UValue.FixedSize(40f));
                    r.Height(UValue.FixedSize(40f));
                });

                if (doRowBreak) {
                    placedInARow = 0;
                    result.Layout.Rows++;
                } else {
                    placedInARow++;
                }

                // Also ask each button what sprites they need
                buttonBuilder.Control.SetupButtonSkin(atlasBuilder);

                string buttonName = buttonDef.ButtonType.ToString().Split('.').Last();
                buttonBuilder.Control.name = $"TMPE_MainMenuButton_{buttonName}";

                ButtonsDict.Add(buttonDef.Mode, buttonBuilder.Control);
                result.Buttons.Add(buttonBuilder.Control);
            }

            return result;
        }

        private void OnVisibilityChanged(UIComponent component, bool value) {
            VersionLabel.enabled = value;

            if (StatsLabel != null) {
                // might not exist
                StatsLabel.enabled = Options.showPathFindStats && value;
            }
            UResizer.UpdateControl(this);
        }

        public override void OnDestroy() {
            eventVisibilityChanged -= OnVisibilityChanged;
            confDisposable?.Dispose();
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
                ClickToolButton(ToolMode.JunctionRestrictions);
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
                button.UpdateButtonImageAndTooltip();
            }
            foreach (BaseMenuButton button in this.ExtraButtonsList) {
                button.UpdateButtonImageAndTooltip();
            }
        }
    } // end class
}