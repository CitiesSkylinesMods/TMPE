namespace TrafficManager.UI.MainMenu {
    using State;

    public class VehicleRestrictionsButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.VehicleRestrictions;

        protected override ButtonFunction Function => ButtonFunction.VehicleRestrictions;

        public override string Tooltip => "Vehicle_restrictions";

        public override bool Visible => Options.vehicleRestrictionsEnabled;
    }
}