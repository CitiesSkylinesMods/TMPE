namespace TrafficManager.UI.SubTools.RoutingDetector {
    using ColossalFramework.UI;
    using System.Linq;
    using TrafficManager.API.Manager;
    using UnityEngine;

    /// <summary>
    /// Lane Arrows control window floating above the node.
    /// </summary>
    public class RoutingDetectorToolWindow : UIPanel {
        UILabel label_;
        public override void Awake() {
            base.Awake();
            color = Color.grey;
            backgroundSprite = "GenericPanel";
            autoLayout = true;
            autoLayoutDirection = LayoutDirection.Vertical;
            position = new Vector2(10, 10);
            isVisible = false;

            label_ = AddUIComponent<UILabel>();
        }

        public void Info(LaneTransitionData[] transtions) {
            if (transtions == null || transtions.Length == 0) {
                Hide();
            } else {
                Show();
                string text = string.Empty;
                foreach (var transition in transtions) {
                    text += $"{transition.type} distance={transition.distance}\n";
                }
                text = text.Substring(0, text.Length - 1); // takeout last new line.

                label_.text = text;
                FitChildrenHorizontally(3);
                FitChildrenVertically(3);
            }
        }
    }
}