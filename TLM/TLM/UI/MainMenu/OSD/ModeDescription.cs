namespace TrafficManager.UI.MainMenu.OSD {
    using TrafficManager.U;
    using TrafficManager.U.Autosize;

    /// <summary>
    /// Displays a single text row with different background.
    /// </summary>
    public class ModeDescription : OsdItem {
        private readonly string localizedText_;

        public ModeDescription(string localizedText) {
            localizedText_ = localizedText;
        }

        public override void Build(U.UiBuilder<U.UPanel> builder) {
            using (UiBuilder<ULabel> labelB = builder.Label<U.ULabel>(string.Empty)) {
                labelB.ResizeFunction(r => { r.Stack(mode: UStackMode.NewRowBelow); });
                labelB.Control.text = this.localizedText_;
                labelB.Control.opacity = 0.8f;
            }
        }
    }
}