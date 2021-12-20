namespace TrafficManager.UI.MainMenu.OSD {
    using ColossalFramework.UI;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;

    /// <summary>
    /// Displays a single text row, markup enabled, help text for the current edit mode or manually
    /// configured key shortcut. Use Translation.Get() or Translation.ColorizeKeybind() and let
    /// the translators provide key in double [[ ]].
    /// </summary>
    public class Label : OsdItem {
        private readonly string localizedText_;

        public Label(string localizedText) {
            localizedText_ = localizedText;
        }

        public override void Build(UIComponent parent,
                                   U.UBuilder builder) {
            ULabel l = builder.Label<U.ULabel>(
                parent,
                this.localizedText_,
                stack: UStackMode.NewRowBelow,
                processMarkup: true);

            l.opacity = 0.8f;
        }
    }
}