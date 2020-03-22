namespace TrafficManager.U.Label {
    using ColossalFramework.UI;
    using TrafficManager.U.Autosize;

    public class ULabel: UILabel, USizePositionInterface {
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