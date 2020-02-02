namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;

    public class ParkingRestrictionsButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.ParkingRestrictions;

        protected override ButtonFunction Function => ButtonFunction.ParkingRestrictions;

        public override string Tooltip => Translation.Menu.Get("Tooltip:Parking restrictions");

        public override bool Visible => Options.parkingRestrictionsEnabled;
    }
}
