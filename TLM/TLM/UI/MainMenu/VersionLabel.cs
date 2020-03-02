namespace TrafficManager.UI.MainMenu {
    using System.Reflection;
    using ColossalFramework.UI;
    using UnityEngine;

    public class VersionLabel : UILabel {
        public override void Start() {
            // TODO use current size profile
            size = new Vector2(
                ModUI.Instance.MainMenu.width,
                MainMenuPanel.ScaledSize.GetTitlebarHeight());
            text = TrafficManagerMod.ModName;

            if(TrafficManagerMod.Instance.InGameHotReload) {
                // make it easier to Identify Hot reload.
                text += " HOT RELOAD " +
                    Assembly.GetExecutingAssembly().GetName().Version;
            }

            relativePosition = new Vector3(5f, 5f);
            textAlignment = UIHorizontalAlignment.Left;
        }
    }
}