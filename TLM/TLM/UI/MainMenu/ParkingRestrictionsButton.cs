namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;

    public class ParkingRestrictionsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.ParkingRestrictions;

        protected override ButtonFunction Function => ButtonFunction.ParkingRestrictions;

        public override string GetTooltip() => Translation.Menu.Get("Tooltip:Parking restrictions");

        public override bool IsVisible() => Options.parkingRestrictionsEnabled;
    }
}
