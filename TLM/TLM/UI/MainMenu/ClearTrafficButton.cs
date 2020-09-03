namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using TrafficManager.Manager.Impl;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.U;
    using TrafficManager.Util;

    public class ClearTrafficButton : BaseMenuButton {
        protected override bool IsActive() => false;

        protected override string U_OverrideTooltipText() => Translation.Menu.Get("Tooltip:Clear traffic");

        protected override bool IsVisible() => true;

        public override void SetupButtonSkin(AtlasBuilder futureAtlas) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.ButtonSkin() {
                Prefix = "ClearTraffic",
                BackgroundPrefix = "RoundButton",
                BackgroundHovered = true,
                BackgroundActive = true,
                ForegroundActive = true,
            };
            this.Skin.UpdateAtlasBuilder(
                atlasBuilder: futureAtlas,
                spriteSize: new IntVector2(50));
        }

        protected override void OnClick(UIMouseEventParameter p) {
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
            base.OnClick(p);
        }
    }
}
