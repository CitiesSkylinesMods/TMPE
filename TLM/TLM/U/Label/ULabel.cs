namespace TrafficManager.U.Label {
    using ColossalFramework.UI;
    using TrafficManager.U.Autosize;

    public class ULabel: UILabel, ISmartSizableControl {
        private UResizerConfig resizerConfig_ = new UResizerConfig();

        public UResizerConfig GetResizerConfig() {
            return resizerConfig_;
        }
    }
}