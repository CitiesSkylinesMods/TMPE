namespace TrafficManager.UI.MainMenu {
    using System.Reflection;
    using ColossalFramework.UI;
    using UnityEngine;

    public class VersionLabel : UILabel {
        public override void Start() {
            // TODO use current size profile
            size = new Vector2(
                MainMenuPanel.SIZE_PROFILES[0].MENU_WIDTH,
                MainMenuPanel.SIZE_PROFILES[0].TOP_BORDER);
            text = TrafficManagerMod.ModName;

            if(LoadingExtension.Instance.InGameHotReload) {
                // make it easier to Identify Hot reload.
                text += " HOT RELOAD " +
                    Assembly.GetExecutingAssembly().GetName().Version;
            }

            relativePosition = new Vector3(5f, 5f);
            textAlignment = UIHorizontalAlignment.Left;
        }
    }
}