namespace TrafficManager.UI.SubTools.SpeedLimits {
    using ColossalFramework.UI;
    using TrafficManager.State;

    internal partial class SpeedLimitsToolWindow {
        /// <summary>Button to toggle MPH/Kmph display.</summary>
        public class MphToggleButton : U.BaseUButton {
            protected override bool IsVisible() => true;

            public override void HandleClick(UIMouseEventParameter p) {
                bool mph = !GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
                GeneralTab_LocalisationGroup.DisplaySpeedLimitsMph.Value = mph;
            }

            /// <summary>Always clickable.</summary>
            public override bool CanActivate() => true;

            /// <summary>Active will mean MPH is activated.</summary>
            protected override bool IsActive() => GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
        }
    }
}