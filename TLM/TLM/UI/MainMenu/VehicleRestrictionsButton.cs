using TrafficManager.State;

namespace TrafficManager.UI.MainMenu {
    public class VehicleRestrictionsButton : MenuToolModeButton {
        public override ToolMode ToolMode => ToolMode.VehicleRestrictions;
        public override ButtonFunction Function => ButtonFunction.VehicleRestrictions;
        public override string Tooltip => "Vehicle_restrictions";
        public override bool Visible => Options.vehicleRestrictionsEnabled;
    }
}