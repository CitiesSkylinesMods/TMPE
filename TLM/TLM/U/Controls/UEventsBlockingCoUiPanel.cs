namespace TrafficManager.U.Controls {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using UnityEngine;

    public class UEventsBlockingCoUiPanel : UIPanel {
        protected void Start() {
            this.backgroundSprite = "GenericPanel";
            this.color = new Color32(255, 0, 0, 255);
        }

        protected void OnGUI() {
            // Consume all mouse motions and clicks
            if (Event.current.isMouse) {
                Event.current.Use();
            }
        }

        public void AdjustToMatch(GameObject formObject) {
            // Move coPanel to match the form rect
            // var rect = RectTransformToScreenSpace(formObject_.GetComponent<RectTransform>());
            // coPanel_.absolutePosition = rect.position;
            // coPanel_.size = rect.size;

            Bounds formBounds = GetRectTransformBounds(formObject.GetComponent<RectTransform>());
//            var formPos = formBounds.min;
//            formPos.y = (Screen.height * 0.5f) - formPos.y;

            // Adjust from bottom-left to top-left corner
            Vector3 pos = formBounds.min + new Vector3(0f, formBounds.size.y, 0f);
            pos.y = Screen.height - pos.y;
            this.absolutePosition = pos;
            // absolutePosition = formBounds.min;
            this.size = formBounds.size;
            Log.Info($"CoUi panel bounds {formBounds.min} {formBounds.max}");
        }

        /// <summary>Convert Canvas RectTransform to screen</summary>
        /// <param name="transform">Rect transform from a canvas UI element</param>
        /// <returns>Bounds converted to screen pixels</returns>
        private static Bounds GetRectTransformBounds(RectTransform transform) {
            var worldCorners = new Vector3[4];

            transform.GetWorldCorners(worldCorners);
            var bounds = new Bounds(worldCorners[0], Vector3.zero);

            for (var i = 1; i < 4; ++i) {
                bounds.Encapsulate(worldCorners[i]);
            }

            return bounds;
        }
    }
}