namespace TrafficManager.UI.MainMenu {
    using ColossalFramework.UI;
    using Manager.Impl;

    public class ClearTrafficButton : MenuButton {
        public override bool Active => false;

        protected override ButtonFunction Function => ButtonFunction.ClearTraffic;

        public override string Tooltip => Translation.Menu.Get("Tooltip:Clear traffic");

        public override bool Visible => true;

        public override void OnClickInternal(UIMouseEventParameter p) {
            ConfirmPanel.ShowModal(
                Translation.Menu.Get("Tooltip:Clear traffic"),
                Translation.Menu.Get("Dialog.Text:Clear traffic, confirmation"),
                (comp, ret) => {
                    if (ret == 1) {
                        Constants.ServiceFactory.SimulationService.AddAction(
                            () => { UtilityManager.Instance.ClearTraffic(); });
                    }

                    UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
                });
        }
    }
}
