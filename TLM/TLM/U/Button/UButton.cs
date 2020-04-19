namespace TrafficManager.U.Button {
    using ColossalFramework.UI;
    using TrafficManager.U.Autosize;

    /// <summary>
    /// Basic button, cannot be activated, clickable, no tooltip.
    /// </summary>
    public class UButton : BaseUButton {
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

        protected override string GetTooltip() => string.Empty; // to override in subclass

        protected override bool IsVisible() => this.isVisible;
    }
}