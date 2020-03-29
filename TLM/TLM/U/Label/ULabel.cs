namespace TrafficManager.U.Label {
    using ColossalFramework.UI;
    using TrafficManager.U.Autosize;

    public class ULabel: UILabel, ISmartSizableControl {
        private UResizerConfig resizerConfig_;

        public UResizerConfig GetResizerConfig() {
            if (resizerConfig_ == null) {
                resizerConfig_ = new UResizerConfig(null, 0f);
            }
            return resizerConfig_;
        }
    }
}