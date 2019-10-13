namespace TrafficManager.UI.NewUI {
    using ColossalFramework.UI;
    using UnityEngine;
    using UnityEngine.UI;

    public class EventsBlockingGraphic : Graphic {
        protected override void Start() {
            // material.color = Color.white;
        }
    }

    public class EventsBlockingCoUiPanel : UIPanel {
        protected void Start() {
            this.backgroundSprite = "GenericPanel";
            this.color = new Color32(255, 0, 0, 200);
        }

        protected void OnGUI() {
            Event.current.Use();
        }
    }

}