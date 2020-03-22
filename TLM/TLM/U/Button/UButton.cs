namespace TrafficManager.U.Button {
    using ColossalFramework.UI;
    using TrafficManager.U.Autosize;

    /// <summary>
    /// Basic button, cannot be activated, clickable, no tooltip.
    /// </summary>
    public class UButton: BaseUButton, USizePositionInterface {
        private USizePosition sizePosition_;

        public USizePosition SizePosition {
            get {
                if (sizePosition_ == null) {
                    sizePosition_ = new USizePosition();
                }

                return sizePosition_;
            }
        }

        public override bool CanActivate() => false; // click only

        public override string ButtonName => this.name;

        public override bool IsActive() => false;

        public override string GetTooltip() => string.Empty;

        public override bool IsVisible() => this.isVisible;

        public override void HandleClick(UIMouseEventParameter p) {
        }
    }
}