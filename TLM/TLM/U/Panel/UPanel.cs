namespace TrafficManager.U.Panel {
    using ColossalFramework.UI;
    using TrafficManager.U.Autosize;

    /// <summary>Base panel for smart sizeable panels.</summary>
    public class UPanel : UIPanel, USizePositionInterface {
        private USizePosition sizePosition_;

        public USizePosition SizePosition {
            get {
                if (sizePosition_ == null) {
                    sizePosition_ = new USizePosition();
                }

                return sizePosition_;
            }
        }
    }
}