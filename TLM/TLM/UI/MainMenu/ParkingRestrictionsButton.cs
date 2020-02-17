namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;
    using TrafficManager.U.Button;

    public class ParkingRestrictionsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.ParkingRestrictions;

        protected override ButtonFunction Function => new ButtonFunction("ParkingRestrictions");

        public override string GetTooltip() => Translation.Menu.Get("Tooltip:Parking restrictions");

        public override bool IsVisible() => Options.parkingRestrictionsEnabled;
    }
}
