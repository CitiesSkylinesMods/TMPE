namespace TrafficManager.U.Button {
    using System;
    using ColossalFramework.UI;
    using TrafficManager.U.Autosize;

    /// <summary>
    /// Basic button, cannot be activated, clickable, no tooltip.
    /// </summary>
    public class UButton : BaseUButton {
        [Obsolete("Remove this field and simplify tooltip handling in BaseUButton")]
        public string uTooltip;

        public override bool CanActivate() {
            if (this.uCanActivate != null) {
                return this.uCanActivate(this);
            }

            return false;
        }

        protected override bool IsActive() {
            if (this.uIsActive != null) {
                return this.uIsActive(this);
            }

            return false;
        }

        protected override string U_OverrideTooltipText() => this.uTooltip; // to override in subclass

        protected override bool IsVisible() => this.isVisible;
    }
}