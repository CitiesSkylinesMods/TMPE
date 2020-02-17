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

    public class UIMainMenuButton
        : BaseUButton,
          IObserver<GlobalConfig>
    {
        // private const string MAIN_MENU_BUTTON_BG_BASE = "TMPE_MainMenuButtonBgBase";
        // private const string MAIN_MENU_BUTTON_BG_HOVERED = "TMPE_MainMenuButtonBgHovered";
        // private const string MAIN_MENU_BUTTON_BG_ACTIVE = "TMPE_MainMenuButtonBgActive";
        // private const string MAIN_MENU_BUTTON_FG_BASE = "TMPE_MainMenuButtonFgBase";
        // private const string MAIN_MENU_BUTTON_FG_HOVERED = "TMPE_MainMenuButtonFgHovered";
        // private const string MAIN_MENU_BUTTON_FG_ACTIVE = "TMPE_MainMenuButtonFgActive";

        const string ATLASKEY_BG_NORMAL = "MainMenuButton-bg-normal";
        const string ATLASKEY_BG_HOVERED = "MainMenuButton-bg-hovered";
        const string ATLASKEY_BG_ACTIVE = "MainMenuButton-bg-active";
        const string ATLASKEY_FG_NORMAL = "MainMenuButton-fg-normal";
        const string ATLASKEY_FG_HOVERED = "MainMenuButton-fg-hovered";
        const string ATLASKEY_FG_ACTIVE = "MainMenuButton-fg-active";

        private const int BUTTON_WIDTH = 50;
        private const int BUTTON_HEIGHT = 50;

        private UIDragHandle Drag { get; set; }

        private IDisposable confDisposable_;

        public override bool CanActivate() => true;

        public override string ButtonName => "TMPE_MainMenu";

        public override void Start() {
            // Place the button.
            OnUpdate(GlobalConfig.Instance);

            confDisposable_ = GlobalConfig.Instance.Subscribe(this);

            // Set the atlas and background/foreground
            // var spriteNames = new[] {
            //     MAIN_MENU_BUTTON_BG_BASE,
            //     MAIN_MENU_BUTTON_BG_HOVERED,
            //     MAIN_MENU_BUTTON_BG_ACTIVE,
            //     MAIN_MENU_BUTTON_FG_BASE,
            //     MAIN_MENU_BUTTON_FG_HOVERED,
            //     MAIN_MENU_BUTTON_FG_ACTIVE,
            // };
            // atlas = TextureUtil.GenerateLinearAtlas(
            //     "TMPE_MainMenuButtonAtlas",
            //     Textures.MainMenu.MainMenuButton,
            //     6,
            //     spriteNames);

            // Let the mainmenu atlas know we need this texture and assign it to self.atlas
            this.Skin = new ButtonSkin {
                                           Prefix = "MainMenuButton",
                                           BackgroundHovered = true,
                                           BackgroundActive = true,
                                           ForegroundHovered = true,
                                           ForegroundActive = true,
                                       };
            this.atlas = this.Skin.CreateAtlas("MainMenu", 50, 50, 256);
            UpdateButtonImageAndTooltip();

            // Set the button dimensions.
            width = BUTTON_WIDTH;
            height = BUTTON_HEIGHT;

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

        public override bool IsActive() {
            return LoadingExtension.ModUi.IsVisible();
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
        //     if (!LoadingExtension.ModUi.IsVisible()) {
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
            Rect rect = new Rect(pos.x, pos.y, BUTTON_WIDTH, BUTTON_HEIGHT);
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
                if (LoadingExtension.ModUi != null) {
                    LoadingExtension.ModUi.ToggleMainMenu();
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
                LoadingExtension.ModUi.ToggleMainMenu();
            }
            catch (Exception e) {
                Log.Error($"Toggle mainmenu failed {e}");
            }
        }
    }
}
