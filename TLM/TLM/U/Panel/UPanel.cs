namespace TrafficManager.U.Panel {
    using ColossalFramework.UI;
    using TrafficManager.U.Autosize;

    /// <summary>Base panel for smart sizeable panels.</summary>
    public class UPanel : UIPanel, ISmartSizableControl {
        private UResizerConfig resizerConfig_ = new UResizerConfig();

        public UResizerConfig GetResizerConfig() {
            return resizerConfig_;
        }
    }
}