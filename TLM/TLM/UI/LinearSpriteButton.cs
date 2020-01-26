namespace TrafficManager.UI {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using State.Keybinds;
    using System;
    using System.Collections.Generic;
    using TrafficManager.State.ConfigData;
    using UnityEngine;
    using Util;

    public abstract class LinearSpriteButton : UIButton {
        protected enum ButtonMouseState {
            Base,
            Hovered,
            MouseDown
        }

        private const string MENU_BUTTON_BACKGROUND = "Bg";
        private const string MENU_BUTTON_FOREGROUND = "Fg";

        private const string MENU_BUTTON_BASE = "Base";
        private const string MENU_BUTTON_HOVERED = "Hovered";
        private const string MENU_BUTTON_MOUSEDOWN = "MouseDown";

        private const string MENU_BUTTON_DEFAULT = "Default";
        private const string MENU_BUTTON_ACTIVE = "Active";
        private const string MENU_BUTTON_Disabled = "Disabled";

        protected static string GetButtonBackgroundDisabledTextureId(string prefix) =>
            GetButtonBackgroundTextureId(prefix, 0, false, true);

        protected static string GetButtonBackgroundTextureId(
            string prefix,
            ButtonMouseState state,
            bool active,
            bool disabled = false) {
            string ret = prefix + MENU_BUTTON_BACKGROUND;

            if (disabled)
                return ret + MENU_BUTTON_Disabled;


            switch (state) {
                case ButtonMouseState.Base: {
                        ret += MENU_BUTTON_BASE;
                        break;
                    }

                case ButtonMouseState.Hovered: {
                        ret += MENU_BUTTON_HOVERED;
                        break;
                    }

                case ButtonMouseState.MouseDown: {
                        ret += MENU_BUTTON_MOUSEDOWN;
                        break;
                    }
            }

            ret += active ? MENU_BUTTON_ACTIVE : MENU_BUTTON_DEFAULT;
            return ret;
        }

        private static string GetButtonForegroundDisabledTextureId(string prefix, string function) =>
            GetButtonForegroundTextureId(prefix, function, false, true);

        private static string GetButtonForegroundTextureId(
            string prefix,
            string function,
            bool active,
            bool disabled = false) {
            string ret = prefix + MENU_BUTTON_FOREGROUND + function;
            if (disabled) {
                ret += MENU_BUTTON_Disabled;
            }else if (active) {
                ret += MENU_BUTTON_ACTIVE;
            } else {
                ret += MENU_BUTTON_DEFAULT;
            }
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

            List<string> textureIds = new List<string>();
            foreach (ButtonMouseState mouseState in EnumUtil.GetValues<ButtonMouseState>()) {
                if (CanActivate()) {
                    textureIds.Add(GetButtonBackgroundTextureId(ButtonName, mouseState, true));
                }

                textureIds.Add(GetButtonBackgroundTextureId(ButtonName, mouseState, false));
            }

            if (CanDisable) {
                textureIds.Add(GetButtonBackgroundDisabledTextureId(ButtonName));
            }

            foreach (string function in FunctionNames) {
                textureIds.Add(GetButtonForegroundTextureId(ButtonName, function, false));
            }

            foreach (string function in FunctionNames) {
                textureIds.Add(GetButtonForegroundTextureId(ButtonName, function, true));
            }

            if (CanDisable) {
                foreach (string function in FunctionNames) {
                    textureIds.Add(GetButtonForegroundDisabledTextureId(ButtonName, function));
                }
            }

            // Set the atlases for background/foreground
            atlas = TextureUtil.GenerateLinearAtlas(
                "TMPE_" + ButtonName + "Atlas",
                AtlasTexture,
                textureIds.Count,
                textureIds.ToArray());

            m_ForegroundSpriteMode = UIForegroundSpriteMode.Scale;
            UpdateProperties();

            // Enable button sounds.
            playAudioEvents = true;
        }

        public virtual bool CanDisable => false;

        public virtual bool ShouldDisable => false;

        public abstract bool Active { get; }

        public abstract string Tooltip { get; }

        public abstract bool Visible { get; }

        public abstract void HandleClick(UIMouseEventParameter p);

        /// <summary>
        /// Override this to return non-null, and it will display a keybind tooltip
        /// </summary>
        public virtual KeybindSetting ShortcutKey => null;

        protected override void OnClick(UIMouseEventParameter p) {
            HandleClick(p);
            UpdateProperties();
        }

        internal void UpdateProperties() {
            bool active = CanActivate() && Active;
            bool disabled = CanDisable && ShouldDisable;

            Log._DebugIf(
                DebugSwitch.ResourceLoading.Get(),
                ()=>$"UpdateProperties: button={this.name} active={active} disabled={disabled}");

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

            if (CanDisable) {
                m_BackgroundSprites.m_Disabled =
                    GetButtonBackgroundDisabledTextureId(ButtonName);

                m_ForegroundSprites.m_Disabled =
                   GetButtonForegroundDisabledTextureId(ButtonName, FunctionName);

                isEnabled = !disabled;
            }

            string shortcutText = GetShortcutTooltip();
            tooltip = Tooltip + shortcutText;
            isVisible = Visible;
            Invalidate();
        }

        /// <summary>
        /// If shortcut key was set to a non-empty something, then form a text tooltip,
        /// otherwise an empty string is returned.
        /// </summary>
        /// <returns>Tooltip to append to the main tooltip text, or an empty string</returns>
        private string GetShortcutTooltip() {
            return ShortcutKey != null ? ShortcutKey.ToLocalizedString("\n") : string.Empty;
        }
    }
}