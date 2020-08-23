namespace TrafficManager.UI.SubTools.SpeedLimits {
    using ColossalFramework.UI;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.U;
    using TrafficManager.Util;

    /// <summary>
    /// Speed limits palette button, has speed assigned to it, and on click will update the selected
    /// speed in the tool, and highlight self.
    /// </summary>
    internal class SpeedLimitPaletteButton : UButton {
        /// <summary>Button width if it contains value less than 100 and is not selected in the palette.</summary>
        public const float DEFAULT_WIDTH = 40f;

        /// <summary>Button must know its speed value.</summary>
        public SpeedValue AssignedValue;

        public SpeedLimitsTool ParentTool;

        protected override void OnClick(UIMouseEventParameter p) {
            base.OnClick(p);

            ParentTool.OnPaletteButtonClicked(this.AssignedValue);
        }

        /// <summary>If button active state changes, update visual to highlight it.</summary>
        public void UpdateSpeedlimitButton() {
            if (this.IsActive()) {
                this.textScale = 2.0f;
                // Can't set width directly, but can via the resizer
                this.GetResizerConfig().FixedSize.x = DEFAULT_WIDTH * 1.5f;
            } else {
                this.textScale = 1.0f;
                // Can't set width directly, but can via the resizer
                this.GetResizerConfig().FixedSize.x = DEFAULT_WIDTH;
            }
        }

        public override bool CanActivate() => true;

        protected override bool IsActive() {
            return FloatUtil.NearlyEqual(
                this.AssignedValue.GameUnits,
                ParentTool.CurrentPaletteSpeedLimit.GameUnits);
        }
    }
}