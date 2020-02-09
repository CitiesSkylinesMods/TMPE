namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;

    public class VehicleRestrictionsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.VehicleRestrictions;

        protected override ButtonFunction Function => ButtonFunction.VehicleRestrictions;

        public override string GetTooltip() => Translation.Menu.Get("Tooltip:Vehicle restrictions");

        public override bool IsVisible() => Options.vehicleRestrictionsEnabled;
    }
}
