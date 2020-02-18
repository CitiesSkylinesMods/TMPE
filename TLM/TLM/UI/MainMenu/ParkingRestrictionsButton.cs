namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.State;
    using TrafficManager.U.Button;

    public class ParkingRestrictionsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.ParkingRestrictions;

        public override void SetupButtonSkin(List<string> atlasKeys) {
        }

        protected override ButtonFunction Function => new ButtonFunction("ParkingRestrictions");

        public override string GetTooltip() => Translation.Menu.Get("Tooltip:Parking restrictions");

        public override bool IsVisible() => Options.parkingRestrictionsEnabled;
    }
}
