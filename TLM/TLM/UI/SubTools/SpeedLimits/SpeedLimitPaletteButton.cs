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
        /// <summary>Button must know its speed value.</summary>
        public SpeedValue AssignedValue;

        public SpeedLimitsTool ParentTool;

        protected override void OnClick(UIMouseEventParameter p) {
            base.OnClick(p);

            ParentTool.OnPaletteButtonClicked(this.AssignedValue);
        }

        public override bool CanActivate() => true;

        protected override bool IsActive() {
            return FloatUtil.NearlyEqual(
                this.AssignedValue.GameUnits,
                ParentTool.CurrentPaletteSpeedLimit.GameUnits);
        }
    }
}