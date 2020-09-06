namespace TrafficManager.UI.SubTools.SpeedLimits {
    using ColossalFramework.UI;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.U;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>
    /// Speed limits palette button, has speed assigned to it, and on click will update the selected
    /// speed in the tool, and highlight self.
    /// </summary>
    internal class SpeedLimitPaletteButton : UButton {
        /// <summary>Button width if it contains value less than 100 and is not selected in the palette.</summary>
        public const float DEFAULT_WIDTH = 40f;
        public const float DEFAULT_HEIGHT = 100f;

        /// <summary>Button must know its speed value.</summary>
        public SpeedValue AssignedValue;

        public SpeedLimitsTool ParentTool;

        /// <summary>Label below the speed limit button displaying alternate unit.</summary>
        public ULabel AltUnitsLabel;

        protected override void OnClick(UIMouseEventParameter p) {
            base.OnClick(p);

            // Tell the parent to update all buttons this will unmark all inactive buttons and
            // mark one which is active. The call turns back here to this.UpdateSpeedlimitButton()
            ParentTool.OnPaletteButtonClicked(this.AssignedValue);
        }

        /// <summary>
        /// If button active state changes, update visual to highlight it.
        /// Active button has large text and is blue.
        /// </summary>
        public void UpdateSpeedlimitButton() {
            if (this.IsActive()) {
                this.textScale = 2.0f;
                this.ColorizeAllStates(new Color32(0, 128, 255, 255));

                // Can't set width directly, but can via the resizer
                this.GetResizerConfig().FixedSize.x = DEFAULT_WIDTH * 1.5f;

                if (this.AltUnitsLabel) { this.AltUnitsLabel.Show(); }
            } else {
                this.textScale = 1.0f;
                this.ColorizeAllStates(new Color32(255, 255, 255, 255));

                // Can't set width directly, but can via the resizer
                this.GetResizerConfig().FixedSize.x = DEFAULT_WIDTH;

                if (this.AltUnitsLabel) { this.AltUnitsLabel.Hide(); }
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