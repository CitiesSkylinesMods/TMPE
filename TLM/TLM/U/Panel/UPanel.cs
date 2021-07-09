namespace TrafficManager.U {
    using ColossalFramework.UI;
    using TrafficManager.U.Autosize;

    /// <summary>Base panel for smart sizeable panels.</summary>
    public class UPanel : UIPanel, ISmartSizableControl {
        private UResizerConfig resizerConfig_ = new();

        public UResizerConfig GetResizerConfig() {
            return resizerConfig_;
        }

        /// <summary>Called by UResizer for every control before it is to be 'resized'.</summary>
        public virtual void OnBeforeResizerUpdate() { }

        /// <summary>Called by UResizer for every control after it is to be 'resized'.</summary>
        public virtual void OnAfterResizerUpdate() { }
    }
}