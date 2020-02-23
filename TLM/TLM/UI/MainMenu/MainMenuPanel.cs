// #define QUEUEDSTATS

namespace TrafficManager.UI.MainMenu {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.API.Util;
    using TrafficManager.State.Keybinds;
    using TrafficManager.State;
    using UnityEngine;

    public class MainMenuPanel
        : U.Panel.BaseUWindowPanel,
          IObserver<GlobalConfig> {
        private struct MenuButtonDef {
            public ToolMode Mode;
            public Type ButtonType;
        }

        private static readonly MenuButtonDef[] MENU_BUTTON_TYPES
            = {
                  new MenuButtonDef {
                                        ButtonType = typeof(ToggleTrafficLightsButton),
                                        Mode = ToolMode.ToggleTrafficLight,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(TimedTrafficLightsButton),
                                        Mode = ToolMode.TimedLightsButton,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(ManualTrafficLightsButton),
                                        Mode = ToolMode.ManualSwitch,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(LaneConnectorButton),
                                        Mode = ToolMode.LaneConnector,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(LaneArrowsButton),
                                        Mode = ToolMode.LaneArrows,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(DespawnButton),
                                        Mode = ToolMode.DespawnButton,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(PrioritySignsButton),
                                        Mode = ToolMode.AddPrioritySigns,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(JunctionRestrictionsButton),
                                        Mode = ToolMode.JunctionRestrictions,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(SpeedLimitsButton),
                                        Mode = ToolMode.SpeedLimits,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(VehicleRestrictionsButton),
                                        Mode = ToolMode.VehicleRestrictions,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(ParkingRestrictionsButton),
                                        Mode = ToolMode.ParkingRestrictions,
                                    },
                  new MenuButtonDef {
                                        ButtonType = typeof(ClearTrafficButton),
                                        Mode = ToolMode.ClearTrafficButton,
                                    },
              };

        public const int DEFAULT_MENU_X = 85;
        public const int DEFAULT_MENU_Y = 60;

        /// <summary>List of buttons stores created UIButtons in order. </summary>
        public List<BaseMenuButton> Buttons { get; private set; }

        /// <summary>Dict of buttons allows quick search by toolmode.</summary>
        private Dictionary<ToolMode, BaseMenuButton> QuickFindButtons;

        public UILabel VersionLabel { get; private set; }

        public UILabel StatsLabel { get; private set; }

        public UIDragHandle Drag { get; private set; }

        IDisposable confDisposable;

        private bool isStarted_;

        private UITextureAtlas allButtonsAtlas_;

        /// <summary>Defines button placement on the main menu since last layout reset.</summary>
        private MainMenuLayout menuLayout_;

        public override void Start() {
            GlobalConfig conf = GlobalConfig.Instance;

            OnUpdate(conf);

            confDisposable = conf.Subscribe(this);

            isVisible = false;

            backgroundSprite = "GenericPanel";
            color = new Color32(64, 64, 64, 240);

            VersionLabel = AddUIComponent<VersionLabel>();
            StatsLabel = AddUIComponent<StatsLabel>();

            // Create and populate list of background atlas keys, used by all buttons
            // And also each button will have a chance to add their own atlas keys for loading.
            var tmpSkin = new U.Button.ButtonSkin {
                                                      Prefix = "MainMenuPanel",
                                                      BackgroundPrefix = "RoundButton",
                                                      ForegroundNormal = false,
                                                      BackgroundHovered = true,
                                                      BackgroundActive = true,
                                                  };
            // By default the atlas will include backgrounds: DefaultRound-bg-normal
            HashSet<string> atlasKeysSet = tmpSkin.CreateAtlasKeyset();

            QuickFindButtons = new Dictionary<ToolMode, BaseMenuButton>();
            Buttons = new List<BaseMenuButton>();
            foreach (MenuButtonDef buttonDef in MENU_BUTTON_TYPES) {
                // Create and populate the panel with buttons
                var newButton = AddUIComponent(buttonDef.ButtonType) as BaseMenuButton;

                // Also ask each button what sprites they need
                newButton.SetupButtonSkin(atlasKeysSet);

                QuickFindButtons.Add(buttonDef.Mode, newButton);
                Buttons.Add(newButton);
            }

            // Create atlas and give it to all buttons
            allButtonsAtlas_ = tmpSkin.CreateAtlas(
                                   "MainMenu.Tool",
                                   50,
                                   50,
                                   512,
                                   atlasKeysSet);

            foreach (BaseMenuButton b in Buttons) {
                b.atlas = allButtonsAtlas_;
            }

            var dragHandler = new GameObject("TMPE_Menu_DragHandler");
            dragHandler.transform.parent = transform;
            dragHandler.transform.localPosition = Vector3.zero;
            Drag = dragHandler.AddComponent<UIDragHandle>();
            Drag.enabled = !GlobalConfig.Instance.Main.MainMenuPosLocked;

            this.OnRescaleRequested();
            eventVisibilityChanged += OnVisibilityChanged;
            isStarted_ = true;
        }

        private void OnVisibilityChanged(UIComponent component, bool value) {
            VersionLabel.enabled = value;
            StatsLabel.enabled = Options.showPathFindStats && value;
        }

        public override void OnDestroy() {
            eventVisibilityChanged -= OnVisibilityChanged;
            confDisposable?.Dispose();
        }

        internal void SetPosLock(bool lck) {
            Drag.enabled = !lck;
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

        public void OnUpdate(GlobalConfig config) {
            UpdatePosition(new Vector2(config.Main.MainMenuX, config.Main.MainMenuY));

            if (isStarted_) {
                this.OnRescaleRequested();
                this.Invalidate();
            }
        }

        public override void OnRescaleRequested() {
            // Update size
            //--------------
            menuLayout_ = RepositionToolButtons();
            this.width = ScaledSize.GetWidth(menuLayout_.MaxCols);
            this.height = ScaledSize.GetHeight(menuLayout_.Rows);

            // Update drag size
            //-------------------
            this.Drag.width = this.width;
            this.Drag.height = ScaledSize.GetTitlebarHeight();
        }

        /// <summary>Calculates button and panel sizes based on the screen resolution.</summary>
        internal class ScaledSize {
            internal const int NUM_ROWS = 2;
            internal const int NUM_COLS = 6;

            /// <summary>Calculate width of main menu panel, based on button width and spacings.</summary>
            /// <returns>Width of the panel.</returns>
            internal static float GetWidth(int cols) {
                // 6 buttons + spacings (each 1/8 of a button)
                float allSpacings = (cols + 1) * 0.125f;
                return GetButtonSize() * (cols + allSpacings);
            }

            internal static float GetHeight() => GetHeight(NUM_ROWS);

            internal static float GetHeight(int rows) {
                // Count height for `Rows` button rows + `Rows` spacings (1/8th) + titlebar
                return (GetButtonSize() * (rows + (rows * 0.125f)))
                       + GetTitlebarHeight();
            }

            /// <summary>Define size as smallest of 2.08% of width or 3.7% of height (40 px at 1080p).
            /// The button cannot be smaller than 40px.</summary>
            /// <returns>Button size for current screen resolution.</returns>
            internal static float GetButtonSize() {
                var scaledSize
                    = U.UIScaler.ScreenSizeSmallestFraction(0.0208f, 0.037f) *
                      U.UIScaler.GetUIScale();
                return Mathf.Max(scaledSize, 40f);
            }

            internal static float GetTitlebarHeight() {
                return GetButtonSize() * 0.66f;
            }
        }

        /// <summary>Reset sizes and positions for UI buttons.</summary>
        /// <returns>Visible buttons count.</returns>
        private MainMenuLayout RepositionToolButtons() {
            MainMenuLayout layout = new MainMenuLayout();

            // Recreate tool buttons
            float y = ScaledSize.GetTitlebarHeight();
            float buttonSize = ScaledSize.GetButtonSize();
            float spacing = buttonSize / 8f;

            layout.CountEnabledButtons(Buttons);

            int placedInARow = 0;
            float x = spacing;

            foreach (BaseMenuButton button in Buttons) {
                if (button.IsVisible()) {
                    button.Show();
                    button.relativePosition = new Vector3(x, y);

                    x += buttonSize + spacing;

                    placedInARow++;
                    layout.MaxCols = Math.Max(layout.MaxCols, placedInARow);

                    if (layout.IsRowBreak(placedInARow, ScaledSize.NUM_COLS)) {
                        y += buttonSize + spacing;
                        x = spacing; // reset to the left side of the button area
                        placedInARow = 0;
                        layout.Rows++;
                    }
                } else {
                    button.Hide();
                    // to avoid window upsizing to fit an invisible button
                    button.relativePosition = Vector3.zero;
                }

                button.width = buttonSize;
                button.height = buttonSize;
                button.Invalidate();
            } // foreach button

            // Special case when new row was broken but no buttons placed on it, reduce Rows count
            // happens when button count is even
            if (x <= 2 * spacing) {
                layout.Rows--;
            }

            return layout;
        }

        public void UpdatePosition(Vector2 pos) {
            Rect rect = new Rect(
                pos.x,
                pos.y,
                ModUI.Instance.MainMenu.width,
                ScaledSize.GetHeight());
            Vector2 resolution = UIView.GetAView().GetScreenResolution();
            VectorUtil.ClampRectToScreen(ref rect, resolution);
            Log.Info($"Setting main menu position to [{pos.x},{pos.y}]");
            absolutePosition = rect.position;
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
            if (QuickFindButtons.TryGetValue(toolMode, out var b)) {
                b.SimulateClick();
            }
        }
    } // end class
}