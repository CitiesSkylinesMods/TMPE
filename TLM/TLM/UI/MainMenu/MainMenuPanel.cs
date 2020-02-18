// #define QUEUEDSTATS

namespace TrafficManager.UI.MainMenu {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using TrafficManager.API.Util;
    using TrafficManager.State.Keybinds;
    using TrafficManager.State;
    using UnityEngine;

    public class MainMenuPanel
        : UIPanel,
          IObserver<GlobalConfig>
    {
        private static readonly Type[] MENU_BUTTON_TYPES
            = {
                // first row
                typeof(ToggleTrafficLightsButton),
                typeof(TimedTrafficLightsButton),
                typeof(ManualTrafficLightsButton),
                typeof(LaneConnectorButton),
                typeof(LaneArrowsButton),
                typeof(DespawnButton),
                // second row
                typeof(PrioritySignsButton),
                typeof(JunctionRestrictionsButton),
                typeof(SpeedLimitsButton),
                typeof(VehicleRestrictionsButton),
                typeof(ParkingRestrictionsButton),
                typeof(ClearTrafficButton),
            };

        public const int DEFAULT_MENU_X = 85;
        public const int DEFAULT_MENU_Y = 60;

        public BaseMenuButton[] Buttons { get; private set; }

        public UILabel VersionLabel { get; private set; }

        public UILabel StatsLabel { get; private set; }

        public UIDragHandle Drag { get; private set; }

        IDisposable confDisposable;

        private bool isStarted_;

        private UITextureAtlas allButtonsAtlas_;

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
            var tmpSkin = new U.Button.ButtonSkin() {
                                                        Prefix = "MainMenuPanel",
                                                        BackgroundPrefix = "RoundButton",
                                                        ForegroundNormal = false,
                                                        BackgroundHovered = true,
                                                        BackgroundActive = true,
                                                    };
            // By default the atlas will include backgrounds: DefaultRound-bg-normal
            var atlasKeysList = tmpSkin.CreateAtlasKeysList();

            Buttons = new BaseMenuButton[MENU_BUTTON_TYPES.Length];
            for (int i = 0; i < MENU_BUTTON_TYPES.Length; ++i) {
                // Create and populate the panel with buttons
                Buttons[i] = AddUIComponent(MENU_BUTTON_TYPES[i]) as BaseMenuButton;

                // Also ask each button what sprites they need
                Buttons[i].SetupButtonSkin(atlasKeysList);
            }

            // Create atlas and give it to all buttons
            allButtonsAtlas_ = tmpSkin.CreateAtlas(
                                   "MainMenu.Tool",
                                   50,
                                   50,
                                   512,
                                   atlasKeysList);

            for (int i = 0; i < MENU_BUTTON_TYPES.Length; ++i) {
                Buttons[i].atlas = allButtonsAtlas_;
            }

            var dragHandler = new GameObject("TMPE_Menu_DragHandler");
            dragHandler.transform.parent = transform;
            dragHandler.transform.localPosition = Vector3.zero;
            Drag = dragHandler.AddComponent<UIDragHandle>();
            Drag.enabled = !GlobalConfig.Instance.Main.MainMenuPosLocked;

            UpdateAllSizes();
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
                UpdateAllSizes();
                Invalidate();
            }
        }

        public void UpdateAllSizes() {
            UpdateSize();
            UpdateDragSize();
            UpdateButtons();
        }

        private void UpdateSize() {
            float buttonSize = ScaledSize.GetButtonSize();
            // 6 buttons horizontal row, 2 rows + titlebar
            width = ScaledSize.GetWidth();
            height = ScaledSize.GetHeight();
        }

        /// <summary>Calculates button and panel sizes based on the screen resolution.</summary>
        internal class ScaledSize {
            internal static float GetWidth() {
                // 6 buttons + 7 button spacings (each 1/8 of a button)
                return GetButtonSize() * (6f + (7 * 0.125f));
            }

            internal static float GetHeight() {
                // 2 button rows + spacing (1/8th) + titlebar
                return (GetButtonSize() * 2.125f) + GetTitlebarHeight();
            }

            /// <summary>Define size as smallest of 2.08% of width or 3.7% of height (40 px at 1080p).</summary>
            /// <returns>Button size for current screen resolution.</returns>
            internal static float GetButtonSize() {
                return U.UIScaler.ScreenSizeSmallestFraction(0.0208f, 0.037f);
            }

            internal static float GetTitlebarHeight() {
                return GetButtonSize() * 0.66f;
            }
        }

        private void UpdateDragSize() {
            Drag.width = width;
            Drag.height = ScaledSize.GetTitlebarHeight();
        }

        private void UpdateButtons() {
            int i = 0;
            int y = (int)ScaledSize.GetTitlebarHeight();
            float buttonSize = ScaledSize.GetButtonSize();
            const int numRows = 2;
            const int numCols = 6;
            float spacing = buttonSize / 8f;

            for (int row = 0; row < numRows; ++row) {
                int x = (int)spacing;

                for (int col = 0; col < numCols; ++col) {
                    if (i >= Buttons.Length) {
                        break;
                    }

                    BaseMenuButton button = Buttons[i];
                    button.relativePosition = new Vector3(x, y);
                    button.width = buttonSize;
                    button.height = buttonSize;
                    button.Invalidate();
                    Buttons[i++] = button;
                    x += (int)(buttonSize + spacing);
                }

                y += (int)(buttonSize + spacing);
            }
        }

        public void UpdatePosition(Vector2 pos) {
            Rect rect = new Rect(pos.x, pos.y, ScaledSize.GetWidth(), ScaledSize.GetHeight());
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
                ClickToolButton(typeof(ToggleTrafficLightsButton));
            } else if (KeybindSettingsBase.LaneArrowTool.IsPressed(Event.current)) {
                ClickToolButton(typeof(LaneArrowsButton));
            } else if (KeybindSettingsBase.LaneConnectionsTool.IsPressed(Event.current)) {
                ClickToolButton(typeof(LaneConnectorButton));
            } else if (KeybindSettingsBase.PrioritySignsTool.IsPressed(Event.current)) {
                ClickToolButton(typeof(PrioritySignsButton));
            } else if (KeybindSettingsBase.JunctionRestrictionsTool.IsPressed(Event.current)) {
                ClickToolButton(typeof(JunctionRestrictionsButton));
            } else if (KeybindSettingsBase.SpeedLimitsTool.IsPressed(Event.current)) {
                ClickToolButton(typeof(SpeedLimitsButton));
            }
        }

        /// <summary>For given button class type, find it in the tool palette and send click</summary>
        /// <param name="t">Something like typeof(ToggleTrafficLightsButton)</param>
        void ClickToolButton(Type t) {
            for (var i = 0; i < MENU_BUTTON_TYPES.Length; i++) {
                if (MENU_BUTTON_TYPES[i] == t) {
                    Buttons[i].SimulateClick();
                    return;
                }
            }
        }

        // TODO: Scale with screen size in a smart way
        public static float GetButtonWidth() => 50f;
        public static float GetButtonHeight() => 50f;
    }
}