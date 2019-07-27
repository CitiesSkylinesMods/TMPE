namespace TrafficManager.UI.MainMenu {
    using State;

    public class ParkingRestrictionsButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.ParkingRestrictions;

        protected override ButtonFunction Function => ButtonFunction.ParkingRestrictions;

        public override string Tooltip => "Parking_restrictions";

        public override bool Visible => Options.parkingRestrictionsEnabled;
    }
}