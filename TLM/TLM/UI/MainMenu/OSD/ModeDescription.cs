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
            ULabel control = builder.Label(
                t: this.localizedText_,
                stack: UStackMode.NewRowBelow);
            control.opacity = 0.8f;
        }
    }
}