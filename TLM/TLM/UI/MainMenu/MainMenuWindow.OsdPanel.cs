namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using UnityEngine;

    public partial class MainMenuWindow {
        internal class OsdPanel : U.UPanel {
            public void SetupControls(MainMenuWindow window, UBuilder builder) {
                this.name = "TMPE_MainMenu_KeybindsPanel";

                // the GenericPanel sprite is Light Silver, make it dark
                this.atlas = TextureUtil.Ingame;
                this.backgroundSprite = "GenericPanel";
                this.color = new Color32(64, 64, 64, 240);
                this.opacity = GlobalConfig.Instance.Main.KeybindsPanelVisible
                                   ? 1f
                                   : 0f;

                this.SetPadding(UPadding.Default);

                // The keybinds panel belongs to main menu but does not expand it to fit
                this.ContributeToBoundingBox(false);

                this.ResizeFunction(
                    resizeFn: (UResizer r) => {
                        r.Stack(mode: UStackMode.NewRowBelow, spacing: UConst.UIPADDING * 2);

                        // As the control technically belongs inside the mainmenu, it will respect
                        // the 4px padding, we want to shift it slightly left to line up with the
                        // main menu panel.
                        r.MoveBy(new Vector2(-UConst.UIPADDING, 0f));
                        r.FitToChildren();
                    });
            }
        }
    }
}