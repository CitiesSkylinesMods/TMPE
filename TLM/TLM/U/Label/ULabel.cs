namespace TrafficManager.U {
    using ColossalFramework.UI;
    using TrafficManager.U.Autosize;

    public class ULabel: UILabel, ISmartSizableControl {
        private UResizerConfig resizerConfig_ = new();

        public UResizerConfig GetResizerConfig() {
            return resizerConfig_;
        }

        /// <summary>Called by UResizer for every control before it is to be 'resized'.</summary>
        public virtual void OnBeforeResizerUpdate() {
            this.textScale = UIScaler.UIScale;
        }

        /// <summary>Called by UResizer for every control after it is to be 'resized'.</summary>
        public virtual void OnAfterResizerUpdate() { }
    }
}