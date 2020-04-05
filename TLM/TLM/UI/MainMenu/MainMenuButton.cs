namespace TrafficManager.UI.MainMenu {
    using System;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.API.Util;
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U;
    using TrafficManager.U.Button;
    using UnityEngine;

    public class MainMenuButton
        : BaseUButton,
          IObserver<GlobalConfig>
    {
        private UIDragHandle Drag { get; set; }

        private IDisposable confDisposable_;

        public override bool CanActivate() => true;

        public override string ButtonName => "TMPE_MainMenu";

        public override void Start() {
            U.UIUtil.MakeUniqueAndSetName(this.gameObject, "TMPE_MainMenuButton");

            // Place the button.
            OnUpdate(GlobalConfig.Instance);

            confDisposable_ = GlobalConfig.Instance.Subscribe(this);

            // Let the mainmenu atlas know we need this texture and assign it to self.atlas.
            this.Skin = new ButtonSkin {
                                           BackgroundPrefix = "MainMenuButton",
                                           Prefix = "MainMenuButton",
                                           BackgroundHovered = true,
                                           BackgroundActive = true,
                                           ForegroundHovered = true,
                                           ForegroundActive = true,
                                       };
            this.atlas = this.Skin.CreateAtlas(
                "MainMenu",
                50,
                50,
                256,
                this.Skin.CreateAtlasKeyset());
            UpdateButtonImageAndTooltip();

            // Set the button dimensions to smallest of 2.6% of screen width or 4.6% of screen height
            // Which approximately equals to 50 pixels in 1080p.
            width = height = GetButtonDimensions();

            // Enable button sounds.
            playAudioEvents = true;

            var dragHandler = new GameObject("TMPE_MainButton_DragHandler");
            dragHandler.transform.parent = transform;
            dragHandler.transform.localPosition = Vector3.zero;
            Drag = dragHandler.AddComponent<UIDragHandle>();

            Drag.width = width;
            Drag.height = height;
            Drag.enabled = !GlobalConfig.Instance.Main.MainMenuButtonPosLocked;

            // Set up the tooltip
            var uiView = GetUIView();
            if (uiView != null) {
                m_TooltipBox = uiView.defaultTooltipBox;
            }
        }

        private static float GetButtonDimensions() {
            // The new behaviour, scales with screen size but never less than 50px
            // var scaledSize = U.UIScaler.ScreenSizeSmallestFraction(0.026f, 0.046f);
            // return Mathf.Max(scaledSize, 50f);

            return 50f; // always 50px the original behaviour
        }

        public override bool IsActive() {
            return ModUI.Instance.IsVisible();
        }

        public override void OnDestroy() {
            confDisposable_?.Dispose();
        }

        internal void SetPosLock(bool lck) {
            Drag.enabled = !lck;
        }

        protected override void OnPositionChanged() {
            GlobalConfig config = GlobalConfig.Instance;

            bool posChanged = config.Main.MainMenuButtonX != (int)absolutePosition.x
                              || config.Main.MainMenuButtonY != (int)absolutePosition.y;

            if (posChanged) {
                Log._Debug($"Button position changed to {absolutePosition.x}|{absolutePosition.y}");

                config.Main.MainMenuButtonX = (int)absolutePosition.x;
                config.Main.MainMenuButtonY = (int)absolutePosition.y;

                GlobalConfig.WriteConfig();
            }

            base.OnPositionChanged();
        }

        // internal void UpdateSprites() {
        //     if (!ModUI.Instance.IsVisible()) {
        //         m_BackgroundSprites.m_Normal = m_BackgroundSprites.m_Disabled =
        //                                            m_BackgroundSprites.m_Focused =
        //                                                ATLASKEY_BG_NORMAL;
        //         m_BackgroundSprites.m_Hovered = ATLASKEY_BG_HOVERED;
        //         m_PressedBgSprite = ATLASKEY_BG_ACTIVE;
        //
        //         m_ForegroundSprites.m_Normal = m_ForegroundSprites.m_Disabled =
        //                                            m_ForegroundSprites.m_Focused =
        //                                                ATLASKEY_FG_NORMAL;
        //         m_ForegroundSprites.m_Hovered = ATLASKEY_FG_HOVERED;
        //         m_PressedFgSprite = ATLASKEY_FG_ACTIVE;
        //     } else {
        //         m_BackgroundSprites.m_Normal = m_BackgroundSprites.m_Disabled =
        //                                            m_BackgroundSprites.m_Focused =
        //                                                m_BackgroundSprites.m_Hovered =
        //                                                    ATLASKEY_BG_ACTIVE;
        //         m_PressedBgSprite = ATLASKEY_BG_HOVERED;
        //
        //         m_ForegroundSprites.m_Normal = m_ForegroundSprites.m_Disabled =
        //                                            m_ForegroundSprites.m_Focused =
        //                                                m_ForegroundSprites.m_Hovered =
        //                                                    ATLASKEY_FG_ACTIVE;
        //         m_PressedFgSprite = ATLASKEY_FG_HOVERED;
        //     }
        //
        //     this.Invalidate();
        // }

        public void OnUpdate(GlobalConfig config) {
            UpdatePosition(new Vector2(config.Main.MainMenuButtonX, config.Main.MainMenuButtonY));
        }

        public void UpdatePosition(Vector2 pos) {
            float size = GetButtonDimensions();
            Rect rect = new Rect(pos.x, pos.y, size, size);
            Vector2 resolution = UIView.GetAView().GetScreenResolution();

            VectorUtil.ClampRectToScreen(ref rect, resolution);
            Log.Info($"Setting main menu button position to [{pos.x},{pos.y}]");
            absolutePosition = rect.position;

            Invalidate();
        }

        public void OnGUI() {
            if (!UIView.HasModalInput()
                && !UIView.HasInputFocus()
                && KeybindSettingsBase.ToggleMainMenu.IsPressed(Event.current)) {
                if (ModUI.Instance != null) {
                    ModUI.Instance.ToggleMainMenu();
                }
            }
        }

        public override string GetTooltip() {
            return Translation.Menu.Get("Tooltip:Toggle Main Menu");
            // return KeybindSettingsBase.ToggleMainMenu.ToLocalizedString("\n");
        }

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.ToggleMainMenu;

        public override bool IsVisible() => true;

        public override void HandleClick(UIMouseEventParameter p) {
            try {
                Log._Debug($"Current tool: {ToolManager.instance.m_properties.CurrentTool}");
                ModUI.Instance.ToggleMainMenu();
            }
            catch (Exception e) {
                Log.Error($"Toggle mainmenu failed {e}");
            }
        }
    }
}
