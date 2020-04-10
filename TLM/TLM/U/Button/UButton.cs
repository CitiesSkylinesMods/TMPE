namespace TrafficManager.U.Button {
    using ColossalFramework.UI;
    using TrafficManager.U.Autosize;

    /// <summary>
    /// Basic button, cannot be activated, clickable, no tooltip.
    /// </summary>
    public class UButton : BaseUButton {
        public override bool CanActivate() => false; // click only

        protected override bool IsActive() => false;

        protected override string GetTooltip() => string.Empty;

        protected override bool IsVisible() => this.isVisible;

        public override void HandleClick(UIMouseEventParameter p) { }
    }
}