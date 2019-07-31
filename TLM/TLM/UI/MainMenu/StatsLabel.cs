namespace TrafficManager.UI.MainMenu {
    using ColossalFramework.UI;
    using Custom.PathFinding;
    using State;
    using UnityEngine;

    public class StatsLabel : UILabel {
        public override void Start() {
            // TODO use current size profile
            size = new Vector2(
                MainMenuPanel.SIZE_PROFILES[0].MENU_WIDTH / 2f,
                MainMenuPanel.SIZE_PROFILES[0].TOP_BORDER);
            text = string.Empty;
            relativePosition = new Vector3(5f, -20f);
            textAlignment = UIHorizontalAlignment.Left;
            anchor = UIAnchorStyle.Top | UIAnchorStyle.Left;
        }

#if QUEUEDSTATS
        public override void Update() {
            if (Options.showPathFindStats) {
                uint queued = CustomPathManager.TotalQueuedPathFinds;
                if (queued < 1000) {
                    textColor = Color.Lerp(Color.green, Color.yellow, queued / 1000f);
                } else if (queued < 2500) {
                    textColor = Color.Lerp(
                        Color.yellow,
                        Color.red,
                        (queued - 1000f) / 1500f);
                } else {
                    textColor = Color.red;
                }

                text = CustomPathManager.TotalQueuedPathFinds + " PFs";
            } else {
                text = string.Empty;
                m_TextColor = Color.white;
            }
        }
#endif
    }
}