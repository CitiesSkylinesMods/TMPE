namespace TrafficManager.U {
    using System;
    using ColossalFramework.UI;

    /// <summary>
    /// Basic button, cannot be activated, clickable, no tooltip.
    /// </summary>
    public class UButton : BaseUButton {
        [Obsolete("Remove this field and simplify tooltip handling in BaseUButton")]
        public string uTooltip;

        public override void Awake() {
            base.Awake();
            SetupDefaultSprites();
        }

        private void SetupDefaultSprites() {
            this.atlas = U.TextureUtil.Ingame;
            this.normalBgSprite = "ButtonMenu";
            this.hoveredBgSprite = "ButtonMenuHovered";
            this.pressedBgSprite = "ButtonMenuPressed";
        }

        public override bool CanActivate() {
            if (this.uCanActivate != null) {
                return this.uCanActivate(this);
            }

            return false;
        }

        protected override bool IsActive() {
            // use uIsActive if its defined, otherwise false. Override this in your buttons.
            return this.uIsActive != null && this.uIsActive(this);
        }

        protected override string U_OverrideTooltipText() => this.uTooltip; // to override in subclass

        protected override bool IsVisible() => this.isVisible;

        /// <summary>
        /// Sets up a clickable button which can be active (to toggle textures on the button).
        /// </summary>
        public void SetupToggleButton(MouseEventHandler onClickFun,
                                      Func<UIComponent, bool> isActiveFun) {
            this.uOnClick = onClickFun;
            this.uIsActive = isActiveFun;
            this.uCanActivate = _ => true;
        }
    }
}