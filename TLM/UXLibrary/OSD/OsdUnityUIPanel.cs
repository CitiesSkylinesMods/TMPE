using ColossalFramework.UI;

namespace UXLibrary.OSD {
    public class OsdUnityUiPanel : UIPanel {
        protected override void OnPositionChanged() {
            var uxmod = this.objectUserData as UxLibrary;
            if (uxmod == null) {
                // the objectUserData field contains something wrong
                return;
            }

            bool posChanged = uxmod.OsdPanelX != (int)absolutePosition.x
                              || uxmod.OsdPanelY != (int)absolutePosition.y;

            if (posChanged) {
                uxmod.Log($"OSD position changed to {absolutePosition.x}|{absolutePosition.y}");

                uxmod.OsdPanelX.value = (int)absolutePosition.x;
                uxmod.OsdPanelY.value = (int)absolutePosition.y;
            }

            base.OnPositionChanged();
        }
    }
}