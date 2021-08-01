namespace TrafficManager.UI.MainMenu.OSD {
    using ColossalFramework.UI;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;

    /// <summary>
    /// Displays a single text row, no markup, help text for the current edit mode.
    /// </summary>
    public class ModeDescription : OsdItem {
        private readonly string localizedText_;

        public ModeDescription(string localizedText) {
            localizedText_ = localizedText;
        }

        public override void Build(UIComponent parent,
                                   U.UBuilder builder) {
            ULabel l = builder.Label<U.ULabel, UIComponent>(
                parent,
                this.localizedText_,
                stack: UStackMode.NewRowBelow,
                processMarkup: false);

            l.opacity = 0.8f;
        }
    }
}