namespace TrafficManager.UI.MainMenu {
    using ColossalFramework.UI;
    using TrafficManager.Manager.Impl;
    using TrafficManager.U.Button;

    public class ClearTrafficButton : BaseMenuButton {
        public override bool IsActive() => false;

        protected override ButtonFunction Function => new ButtonFunction("ClearTraffic");

        public override string GetTooltip() => Translation.Menu.Get("Tooltip:Clear traffic");

        public override bool IsVisible() => true;

        public override void OnClickInternal(UIMouseEventParameter p) {
            ConfirmPanel.ShowModal(
                Translation.Menu.Get("Tooltip:Clear traffic"),
                Translation.Menu.Get("Dialog.Text:Clear traffic, confirmation"),
                (comp, ret) => {
                    if (ret == 1) {
                        Constants.ServiceFactory.SimulationService.AddAction(
                            () => { UtilityManager.Instance.ClearTraffic(); });
                    }

                    ModUI.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
                });
        }
    }
}
