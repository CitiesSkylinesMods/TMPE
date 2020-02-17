namespace TrafficManager.U.Button {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.State.Keybinds;
    using TrafficManager.UI.Textures;
    using UnityEngine;

    /// <summary>
    /// A smart button which can change its foreground and background textures based on its state,
    /// also has a localised tooltip.
    /// This is an abstract base class for buttons.
    /// </summary>
    public abstract class BaseUButton : UIButton {
        public abstract bool CanActivate();

        public abstract string ButtonName { get; }

        public abstract string FunctionName { get; }

        // public abstract string[] FunctionNames { get; }

        public abstract int GetWidth();

        public abstract int GetHeight();

        public override void Start() {
            m_ForegroundSpriteMode = UIForegroundSpriteMode.Scale;
            UpdateProperties();

            // Enable button sounds.
            playAudioEvents = true;
        }

        /// <summary>
        /// Override this to return true when the button is activated and should be highlighted.
        /// </summary>
        public abstract bool IsActive();

        /// <summary>
        /// Override this to return localized string for the tooltip.
        /// </summary>
        public abstract string GetTooltip();

        /// <summary>
        /// Override this to define whether the button should be visible on tool panel.
        /// </summary>
        /// <returns>Is the button visible?</returns>
        public abstract bool IsVisible();

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
            bool active = CanActivate() && IsActive();

            m_BackgroundSprites.m_Normal =
                m_BackgroundSprites.m_Disabled =
                    m_BackgroundSprites.m_Focused =
                        U.ButtonTexture.GetBackgroundTextureId(
                            ButtonName,
                            ButtonMouseState.Base,
                            active);

            m_BackgroundSprites.m_Hovered =
                U.ButtonTexture.GetBackgroundTextureId(
                    ButtonName,
                    ButtonMouseState.Hovered,
                    active);

            m_PressedBgSprite =
                U.ButtonTexture.GetBackgroundTextureId(
                    ButtonName,
                    ButtonMouseState.MouseDown,
                    active);

            m_ForegroundSprites.m_Normal =
                m_ForegroundSprites.m_Disabled =
                    m_ForegroundSprites.m_Focused =
                        U.ButtonTexture.GetForegroundTextureId(ButtonName, FunctionName, active);

            m_ForegroundSprites.m_Hovered =
                m_PressedFgSprite =
                    U.ButtonTexture.GetForegroundTextureId(ButtonName, FunctionName, true);

            string shortcutText = GetShortcutTooltip();
            tooltip = GetTooltip() + shortcutText;

            this.isVisible = IsVisible();
            this.Invalidate();
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