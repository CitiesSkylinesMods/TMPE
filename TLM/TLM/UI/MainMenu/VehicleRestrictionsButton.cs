namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;

    public class VehicleRestrictionsButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.VehicleRestrictions;

        protected override ButtonFunction Function => ButtonFunction.VehicleRestrictions;

        public override string Tooltip => Translation.Menu.Get("Tooltip:Vehicle restrictions");

        public override bool Visible => Options.vehicleRestrictionsEnabled;
    }
}
