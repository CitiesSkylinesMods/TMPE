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
                typeof(ManualTrafficLightsButton),
                typeof(LaneArrowsButton),
                typeof(LaneConnectorButton),
                typeof(DespawnButton),
                typeof(ClearTrafficButton),
                // second row
                typeof(PrioritySignsButton),
                typeof(TimedTrafficLightsButton),
                typeof(JunctionRestrictionsButton),
                typeof(SpeedLimitsButton),
                typeof(VehicleRestrictionsButton),
                typeof(ParkingRestrictionsButton),
            };

        // public class SizeProfile {
        //     public int NUM_BUTTONS_PER_ROW { get; set; }
        //
        //     public int NUM_ROWS { get; set; }
        //
        //     public int VSPACING { get; set; }
        //
        //     public int HSPACING { get; set; }
        //
        //     public int TOP_BORDER { get; set; }
        //
        //     public int BUTTON_SIZE { get; set; }
        //
        //     public int MENU_WIDTH { get; set; }
        //
        //     public int MENU_HEIGHT { get; set; }
        // }

        // public static readonly SizeProfile[] SIZE_PROFILES
        //     = {
        //         new SizeProfile {
        //             NUM_BUTTONS_PER_ROW = 6,
        //             NUM_ROWS = 2,
        //
        //             VSPACING = 5,
        //             HSPACING = 5,
        //             TOP_BORDER = 25,
        //             BUTTON_SIZE = 30,
        //
        //             MENU_WIDTH = 215,
        //             MENU_HEIGHT = 95
        //         },
        //         new SizeProfile {
        //             NUM_BUTTONS_PER_ROW = 6,
        //             NUM_ROWS = 2,
        //
        //             VSPACING = 5,
        //             HSPACING = 5,
        //             TOP_BORDER = 25,
        //             BUTTON_SIZE = 50,
        //
        //             MENU_WIDTH = 335,
        //             MENU_HEIGHT = 135,
        //         },
        //     };

        public const int DEFAULT_MENU_X = 85;
        public const int DEFAULT_MENU_Y = 60;

        public BaseMenuButton[] Buttons { get; private set; }

        public UILabel VersionLabel { get; private set; }

        public UILabel StatsLabel { get; private set; }

        public UIDragHandle Drag { get; private set; }

        IDisposable confDisposable;

        private bool started;

        public override void Start() {
            GlobalConfig conf = GlobalConfig.Instance;

            OnUpdate(conf);

            confDisposable = conf.Subscribe(this);

            isVisible = false;

            backgroundSprite = "GenericPanel";
            color = new Color32(64, 64, 64, 240);

            VersionLabel = AddUIComponent<VersionLabel>();
            StatsLabel = AddUIComponent<StatsLabel>();

            Buttons = new BaseMenuButton[MENU_BUTTON_TYPES.Length];
            for (int i = 0; i < MENU_BUTTON_TYPES.Length; ++i) {
                Buttons[i] = AddUIComponent(MENU_BUTTON_TYPES[i]) as BaseMenuButton;
            }

            var dragHandler = new GameObject("TMPE_Menu_DragHandler");
            dragHandler.transform.parent = transform;
            dragHandler.transform.localPosition = Vector3.zero;
            Drag = dragHandler.AddComponent<UIDragHandle>();
            Drag.enabled = !GlobalConfig.Instance.Main.MainMenuPosLocked;

            UpdateAllSizes();
            eventVisibilityChanged += OnVisibilityChanged;
            started = true;
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

            if (started) {
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
            float buttonSize = GetScaledButtonSize();
            // 6 buttons horizontal row, 2 rows + titlebar
            width = GetScaledMenuWidth();
            height = GetScaledMenuHeight();
        }

        internal static float GetScaledMenuWidth() {
            // 6 buttons + 7 button spacings (each 1/8 of a button)
            return GetScaledButtonSize() * (6f + (7 * 0.125f));
        }

        internal static float GetScaledMenuHeight() {
            // 2 button rows + spacing (1/8th) + titlebar
            return (GetScaledButtonSize() * 2.125f) + GetScaledTitlebarHeight();
        }

        /// <summary>Define size as smallest of 2.08% of width or 3.7% of height (40 px at 1080p).</summary>
        /// <returns>Button size for current screen resolution.</returns>
        internal static float GetScaledButtonSize() {
            return U.UIScaler.ScreenSizeSmallestFraction(0.0208f, 0.037f);
        }

        internal static float GetScaledTitlebarHeight() {
            return GetScaledButtonSize() * 0.66f;
        }

        private void UpdateDragSize() {
            Drag.width = width;
            Drag.height = GetScaledTitlebarHeight();
        }

        private void UpdateButtons() {
            int i = 0;
            int y = (int)GetScaledTitlebarHeight();
            float buttonSize = GetScaledButtonSize();
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
            Rect rect = new Rect(pos.x, pos.y, GetScaledMenuWidth(), GetScaledMenuHeight());
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