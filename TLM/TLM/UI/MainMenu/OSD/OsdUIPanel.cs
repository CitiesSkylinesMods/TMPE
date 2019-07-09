namespace TrafficManager.UI.MainMenu.OSD {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using State;

    public class OsdUIPanel: UIPanel {
        protected override void OnPositionChanged() {
            GlobalConfig config = GlobalConfig.Instance;

            bool posChanged = config.Main.OSDPanelX != (int)absolutePosition.x
                              || config.Main.OSDPanelY != (int)absolutePosition.y;

            if (posChanged) {
                Log._Debug($"OSD position changed to {absolutePosition.x}|{absolutePosition.y}");

                config.Main.OSDPanelX = (int)absolutePosition.x;
                config.Main.OSDPanelY = (int)absolutePosition.y;

                GlobalConfig.WriteConfig();
            }

            base.OnPositionChanged();
        }
    }
}