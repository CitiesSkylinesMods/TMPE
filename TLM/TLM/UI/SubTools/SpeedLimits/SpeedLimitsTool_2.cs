namespace TrafficManager.UI.SubTools.SpeedLimits {
    using TrafficManager.U;

    /// <summary>
    /// Implements new style Speed Limits palette and speed limits management UI.
    /// </summary>
    public class SpeedLimitsTool
        : TrafficManagerSubTool,
          UI.MainMenu.IOnscreenDisplayProvider
    {
        //private const ushort LOWER_KMPH = 10;
        public const ushort UPPER_KMPH = 140;
        public const ushort KMPH_STEP = 10;

        //private const ushort LOWER_MPH = 5;
        public const ushort UPPER_MPH = 90;
        public const ushort MPH_STEP = 5;

        /// <summary>If exists, contains tool panel floating on the selected node.</summary>
        private SpeedLimitsWindow Window { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpeedLimitsTool"/> class.
        /// </summary>
        /// <param name="mainTool">Reference to the parent maintool.</param>
        public SpeedLimitsTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            // Create a generic self-sizing window with padding of 4px.
            Window = UiBuilder<SpeedLimitsWindow>.CreateWindow<SpeedLimitsWindow>(
                setupFn: b => b.Control.SetupControls(b));
        }

        private static string T(string key) => Translation.SpeedLimits.Get(key);

        public override void ActivateTool() {
        }

        public override void DeactivateTool() {
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
        }

        public override void OnToolLeftClick() {
        }

        public override void OnToolRightClick() {
        }

        public override void UpdateEveryFrame() {
        }

        /// <summary>Called when the tool must update onscreen keyboard/mouse hints.</summary>
        public void UpdateOnscreenDisplayPanel() {
        }
    } // end class
}
