namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.State;
    using TrafficManager.U.Button;

    public class VehicleRestrictionsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.VehicleRestrictions;

        public override void SetupButtonSkin(List<string> atlasKeys) {
        }

        protected override ButtonFunction Function => new ButtonFunction("VehicleRestrictions");

        public override string GetTooltip() => Translation.Menu.Get("Tooltip:Vehicle restrictions");

        public override bool IsVisible() => Options.vehicleRestrictionsEnabled;
    }
}
