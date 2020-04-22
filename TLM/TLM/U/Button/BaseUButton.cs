namespace TrafficManager.U.Button {
    using System;
    using ColossalFramework.UI;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Autosize;

    /// <summary>
    /// A smart button which can change its foreground and background textures based on its state,
    /// also has a localised tooltip.
    /// This is an abstract base class for buttons.
    /// </summary>
    public abstract class BaseUButton : UIButton, ISmartSizableControl {
        /// <summary>Works for U controls, same as CO.UI eventClick.</summary>
        public MouseEventHandler uOnClick;

        /// <summary>
        /// Set this to avoid subclassing UButton when all you need is a simple active
        /// mode toggle.
        /// NOTE: For this to work, <see cref="uCanActivate"/> must return true.
        /// </summary>
        public Func<UIComponent, bool> uIsActive;
        public Func<UIComponent, bool> uCanActivate;

        private UResizerConfig resizerConfig_ = new UResizerConfig();

        public UResizerConfig GetResizerConfig() {
            return resizerConfig_;
        }

        /// <summary>Called by UResizer for every control before it is to be 'resized'.</summary>
        public virtual void OnBeforeResizerUpdate() { }

        /// <summary>Called by UResizer for every control after it is to be 'resized'.</summary>
        public virtual void OnAfterResizerUpdate() { }

        /// <summary>Defines how the button looks, hovers and activates.</summary>
        public U.Button.ButtonSkin Skin;

        /// <summary>Checks whether a button can ever be "activated", i.e. stays highlighted.</summary>
        /// <returns>Whether a button can toggle-activate.</returns>
        public abstract bool CanActivate();

        public override void Start() {
            m_ForegroundSpriteMode = UIForegroundSpriteMode.Scale;
            UpdateButtonImageAndTooltip();

            // Enable button sounds.
            playAudioEvents = true;
        }

        /// <summary>
        /// Override this to return true when the button is activated and should be highlighted.
        /// </summary>
        protected abstract bool IsActive();

        /// <summary>Override this to return localized string for the tooltip.</summary>
        /// <returns>The tooltip for this button.</returns>
        protected abstract string GetTooltip();

        /// <summary>Override this to define whether the button should be visible on tool panel.</summary>
        /// <returns>Whether the button visible.</returns>
        protected abstract bool IsVisible();

        public virtual void HandleClick(UIMouseEventParameter p) {
        }

        /// <summary>
        /// Override this to return non-null, and it will display a keybind tooltip
        /// </summary>
        public virtual KeybindSetting GetShortcutKey() {
            return null;
        }

        protected override void OnClick(UIMouseEventParameter p) {
            this.HandleClick(p);
            uOnClick?.Invoke(this, p);
            UpdateButtonImageAndTooltip();
        }

        // TODO: Simplify tooltip override, use this code for MainMenu buttons, and return back to the original tooltip in all other
        internal void UpdateButtonImageAndTooltip() {
            UpdateButtonImage();

            // Update localized tooltip with shortcut key if available
            string overrideTooltip = GetTooltip();
            if (!string.IsNullOrEmpty(overrideTooltip)) {
                this.tooltip = overrideTooltip + GetShortcutTooltip();
            }

            this.isVisible = IsVisible();
            this.Invalidate();
        }

        internal void UpdateButtonImage() {
            if (this.Skin == null) {
                // No skin, no textures, nothing to be updated
                return;
            }

            ControlActiveState activeState = CanActivate() && IsActive()
                                                 ? ControlActiveState.Active
                                                 : ControlActiveState.Normal;
            ControlEnabledState enabledState =
                this.enabled ? ControlEnabledState.Enabled : ControlEnabledState.Disabled;

            m_BackgroundSprites.m_Normal
                = m_BackgroundSprites.m_Disabled =
                      m_BackgroundSprites.m_Focused =
                          Skin.GetBackgroundTextureId(
                              enabledState: enabledState,
                              hoveredState: ControlHoveredState.Normal,
                              activeState: activeState);
            m_BackgroundSprites.m_Hovered
                = Skin.GetBackgroundTextureId(
                    enabledState: enabledState,
                    hoveredState: ControlHoveredState.Hovered,
                    activeState: activeState);
            m_PressedBgSprite = Skin.GetBackgroundTextureId(
                enabledState: enabledState,
                hoveredState: ControlHoveredState.Normal,
                activeState: ControlActiveState.Active);

            m_ForegroundSprites.m_Normal =
                m_ForegroundSprites.m_Disabled =
                    m_ForegroundSprites.m_Focused =
                        Skin.GetForegroundTextureId(
                            enabledState: enabledState,
                            hoveredState: ControlHoveredState.Normal,
                            activeState: activeState);
            m_ForegroundSprites.m_Hovered
                = m_PressedFgSprite
                      = Skin.GetForegroundTextureId(
                          enabledState: enabledState,
                          hoveredState: ControlHoveredState.Hovered,
                          activeState: activeState);
        }

        /// <summary>
        /// If shortcut key was set to a non-empty something, then form a text tooltip,
        /// otherwise an empty string is returned.
        /// </summary>
        /// <returns>Tooltip to append to the main tooltip text, or an empty string</returns>
        // TODO: Move this code to MainMenu buttons which have shortcuts, other buttons don't have them
        private string GetShortcutTooltip() {
            return GetShortcutKey() != null
                       ? GetShortcutKey().ToLocalizedString("\n")
                       : string.Empty;
        }
    }
}