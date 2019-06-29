using ColossalFramework.UI;
using CSUtil.Commons;
using System;
using ColossalFramework;
using TrafficManager.State.Keybinds;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.UI.MainMenu {
    public abstract class LinearSpriteButton : UIButton {
        public enum ButtonMouseState {
            Base,
            Hovered,
            MouseDown
        }

        public const string MENU_BUTTON_BACKGROUND = "Bg";
        public const string MENU_BUTTON_FOREGROUND = "Fg";

        public const string MENU_BUTTON_BASE = "Base";
        public const string MENU_BUTTON_HOVERED = "Hovered";
        public const string MENU_BUTTON_MOUSEDOWN = "MouseDown";

        public const string MENU_BUTTON_DEFAULT = "Default";
        public const string MENU_BUTTON_ACTIVE = "Active";

        protected static string GetButtonBackgroundTextureId(string prefix, ButtonMouseState state, bool active) {
            string ret = prefix + MENU_BUTTON_BACKGROUND;

            switch (state) {
                case ButtonMouseState.Base:
                    ret += MENU_BUTTON_BASE;
                    break;
                case ButtonMouseState.Hovered:
                    ret += MENU_BUTTON_HOVERED;
                    break;
                case ButtonMouseState.MouseDown:
                    ret += MENU_BUTTON_MOUSEDOWN;
                    break;
            }

            ret += active ? MENU_BUTTON_ACTIVE : MENU_BUTTON_DEFAULT;
            return ret;
        }

        protected static string GetButtonForegroundTextureId(string prefix, string function, bool active) {
            string ret = prefix + MENU_BUTTON_FOREGROUND + function;
            ret += active ? MENU_BUTTON_ACTIVE : MENU_BUTTON_DEFAULT;
            return ret;
        }

        public abstract bool CanActivate();

        public abstract string ButtonName { get; }

        public abstract string FunctionName { get; }

        public abstract string[] FunctionNames { get; }

        public abstract Texture2D AtlasTexture { get; }

        public abstract int Width { get; }
        public abstract int Height { get; }

        public override void Start() {
            var textureCount = Enum.GetValues(typeof(ButtonMouseState)).Length * (CanActivate() ? 2 : 1)
                               + FunctionNames.Length * 2;
            string[] textureIds = new string[textureCount];

            int i = 0;
            foreach (ButtonMouseState mouseState in EnumUtil.GetValues<ButtonMouseState>()) {
                if (CanActivate()) {
                    textureIds[i++] = GetButtonBackgroundTextureId(ButtonName, mouseState, true);
                }
                textureIds[i++] = GetButtonBackgroundTextureId(ButtonName, mouseState, false);
            }

            foreach (string function in FunctionNames) {
                textureIds[i++] = GetButtonForegroundTextureId(ButtonName, function, false);
            }

            foreach (string function in FunctionNames) {
                textureIds[i++] = GetButtonForegroundTextureId(ButtonName, function, true);
            }

            // Set the atlases for background/foreground
            atlas = TextureUtil.GenerateLinearAtlas("TMPE_" + ButtonName + "Atlas", AtlasTexture, textureIds.Length, textureIds);

            m_ForegroundSpriteMode = UIForegroundSpriteMode.Scale;
            UpdateProperties();

            // Enable button sounds.
            playAudioEvents = true;
        }

        public abstract bool Active { get; }

        public abstract string Tooltip { get; }

        public abstract bool Visible { get; }

        public abstract void HandleClick(UIMouseEventParameter p);

        public virtual KeybindSetting ShortcutKey {
            get { return null; }
        }

        protected override void OnClick(UIMouseEventParameter p) {
            HandleClick(p);
            UpdateProperties();
        }

        internal void UpdateProperties() {
            bool active = CanActivate() ? Active : false;

            m_BackgroundSprites.m_Normal =
                m_BackgroundSprites.m_Disabled =
                    m_BackgroundSprites.m_Focused =
                        GetButtonBackgroundTextureId(ButtonName, ButtonMouseState.Base, active);

            m_BackgroundSprites.m_Hovered =
                GetButtonBackgroundTextureId(ButtonName, ButtonMouseState.Hovered, active);

            m_PressedBgSprite =
                GetButtonBackgroundTextureId(ButtonName, ButtonMouseState.MouseDown, active);

            m_ForegroundSprites.m_Normal =
                m_ForegroundSprites.m_Disabled =
                    m_ForegroundSprites.m_Focused =
                        GetButtonForegroundTextureId(ButtonName, FunctionName, active);

            m_ForegroundSprites.m_Hovered =
                m_PressedFgSprite =
                    GetButtonForegroundTextureId(ButtonName, FunctionName, true);

            var shortcutText = GetShortcutTooltip();
            tooltip = Translation.GetString(Tooltip) + shortcutText;

            isVisible = Visible;
            this.Invalidate();
        }


        /// <summary>
        /// If shortcut key was set to a non-empty something, then form a text tooltip,
        /// otherwise an empty string is returned.
        /// </summary>
        /// <returns>Tooltip to append to the main tooltip text, or an empty string</returns>
        private string GetShortcutTooltip() {
            return ShortcutKey.Str("\n");
        }
    }
}