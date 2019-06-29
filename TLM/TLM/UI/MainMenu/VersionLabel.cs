using ColossalFramework.UI;
using UnityEngine;

namespace TrafficManager.UI.MainMenu {
    public class VersionLabel : UILabel {
        public override void Start() {
            // TODO use current size profile
            size = new Vector2(MainMenuPanel.SIZE_PROFILES[0].MENU_WIDTH,
                               MainMenuPanel.SIZE_PROFILES[0].TOP_BORDER);
            text = "TM:PE " + TrafficManagerMod.Version;
            relativePosition = new Vector3(5f, 5f);
            textAlignment = UIHorizontalAlignment.Left;
        }
    }
}