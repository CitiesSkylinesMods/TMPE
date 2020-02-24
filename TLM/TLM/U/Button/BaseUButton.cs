namespace TrafficManager.U.Button {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using CSUtil.Commons;
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
        /// <summary>Defines how the button looks, hovers and activates.</summary>
        public U.Button.ButtonSkin Skin;

        /// <summary>Checks whether a button can ever be "activated", i.e. stays highlighted.</summary>
        /// <returns>Whether a button can toggle-activate.</returns>
        public abstract bool CanActivate();

        public abstract string ButtonName { get; }

        public override void Start() {
            m_ForegroundSpriteMode = UIForegroundSpriteMode.Scale;
            UpdateButtonImageAndTooltip();

            // Enable button sounds.
            playAudioEvents = true;
        }

        /// <summary>
        /// Override this to return true when the button is activated and should be highlighted.
        /// </summary>
        public abstract bool IsActive();

        /// <summary>Override this to return localized string for the tooltip.</summary>
        public abstract string GetTooltip();

        /// <summary>Override this to define whether the button should be visible on tool panel.</summary>
        /// <returns>Whether the button visible.</returns>
        public abstract bool IsVisible();

        public abstract void HandleClick(UIMouseEventParameter p);

        /// <summary>
        /// Override this to return non-null, and it will display a keybind tooltip
        /// </summary>
        public virtual KeybindSetting ShortcutKey => null;

        protected override void OnClick(UIMouseEventParameter p) {
            HandleClick(p);
            UpdateButtonImageAndTooltip();
        }

        internal void UpdateButtonImageAndTooltip() {
            if (this.Skin == null) {
                // No skin, no textures, nothing to be updated
                return;
            }
            ControlActiveState activeState = CanActivate() && IsActive() ?
                ControlActiveState.Active : ControlActiveState.Normal;
            ControlEnabledState enabledState =
                this.isEnabled ? ControlEnabledState.Enabled : ControlEnabledState.Disabled;

            m_BackgroundSprites.m_Normal
                = m_BackgroundSprites.m_Disabled =
                      m_BackgroundSprites.m_Focused =
                          Skin.GetBackgroundTextureId(
                              enabledState,
                              ControlHoveredState.Normal,
                              activeState);
            m_BackgroundSprites.m_Hovered
                = Skin.GetBackgroundTextureId(
                    enabledState,
                    ControlHoveredState.Hovered,
                    activeState);
            m_PressedBgSprite = Skin.GetBackgroundTextureId(
                enabledState,
                ControlHoveredState.Normal,
                ControlActiveState.Active);

            m_ForegroundSprites.m_Normal =
                m_ForegroundSprites.m_Disabled =
                    m_ForegroundSprites.m_Focused =
                        Skin.GetForegroundTextureId(
                            enabledState,
                            ControlHoveredState.Normal,
                            activeState);
            m_ForegroundSprites.m_Hovered
                = m_PressedFgSprite
                      = Skin.GetForegroundTextureId(
                          enabledState,
                          ControlHoveredState.Hovered,
                          activeState);

            // Update localized tooltip with shortcut key if available
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