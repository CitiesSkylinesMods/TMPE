namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using ColossalFramework;
    using ColossalFramework.UI;
    using TrafficManager.Manager.Impl;
    using TrafficManager.U;
    using TrafficManager.Util;

    public class ClearTrafficButton : BaseMenuButton {
        protected override bool IsActive() => false;

        protected override string U_OverrideTooltipText() => Translation.Menu.Get("Tooltip:Clear traffic");

        protected override bool IsVisible() => true;

        public override void SetupButtonSkin(AtlasBuilder futureAtlas) {
            // Button background (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = ButtonSkin.CreateSimple(
                                      foregroundPrefix: "ClearTraffic",
                                      backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG)
                                  .CanHover(foreground: false)
                                  .CanActivate();
            this.Skin.UpdateAtlasBuilder(
                atlasBuilder: futureAtlas,
                spriteSize: new IntVector2(50));
        }

        protected override void OnClick(UIMouseEventParameter p) {
            ConfirmPanel.ShowModal(
                title: Translation.Menu.Get("Tooltip:Clear traffic"),
                message: Translation.Menu.Get("Dialog.Text:Clear traffic, confirmation"),
                callback: (comp, ret) => {
                    if (ret == 1) {
                        Singleton<SimulationManager>.instance.AddAction(
                            () => UtilityManager.Instance.ClearTraffic());
                    }

                    ModUI.GetTrafficManagerTool()?.SetToolMode(ToolMode.None);
                });
            base.OnClick(p);
        }
    }
}
