namespace TrafficManager.U.Label {
    using ColossalFramework.UI;
    using TrafficManager.U.Autosize;

    public class ULabel: UILabel, ISmartSizableControl {
        private UResizerConfig resizerConfig_;

        public UResizerConfig GetResizerInfo() {
            if (resizerConfig_ == null) {
                resizerConfig_ = new UResizerConfig();
            }
            return resizerConfig_;
        }
    }
}