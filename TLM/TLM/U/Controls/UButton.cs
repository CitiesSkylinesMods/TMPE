namespace TrafficManager.U.Controls {
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    public class CanvasButton
        : Button,
          IPointerExitHandler,
          IPointerEnterHandler {
        /// <summary>
        /// Either contains an image in this button, or colors the button with a solid color
        /// </summary>
        private Image imgComponent_;

        private static RectTransform rectTransform_;
        private static LayoutElement layoutElement_;

        public static CanvasButton Create([CanBeNull]
                                          GameObject parent,
                                          string buttonName,
                                          string text) {
            var btnObject = new GameObject(buttonName);

            var buttonComponent = btnObject.AddComponent<CanvasButton>();

            // Set the colors
            ColorBlock c = buttonComponent.colors;
            c.normalColor = Constants.NORMAL_BUTTON_BACKGROUND;
            c.highlightedColor = Constants.HOVERED_BUTTON_BACKGROUND;
            c.pressedColor = Constants.PRESSED_BUTTON_BACKGROUND;
            buttonComponent.colors = c;

//            buttonComponent.onClick.AddListener(() => Log.Info("Clicked canvas button"));

            layoutElement_ = btnObject.AddComponent<LayoutElement>();
            rectTransform_ = btnObject.GetComponent<RectTransform>();
//            var rectTransform = btnObject.AddComponent<RectTransform>();
//            rectTransform.SetPositionAndRotation(position, Quaternion.identity);
//            rectTransform.sizeDelta = size;

            // Nested text for the button
            if (!string.IsNullOrEmpty(text)) {
                CanvasText.Create(btnObject, "Label", text);
            }

            btnObject.transform.SetParent(parent.transform, false);

            return buttonComponent;
        }

        protected override void Start() {
            imgComponent_ = this.gameObject.AddComponent<Image>();
            imgComponent_.color = this.colors.normalColor;
        }

        /// <summary>
        /// Sets the position for the button, if it wasn't managed by a layout group
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public CanvasButton Position(Vector2 position) {
            rectTransform_.SetPositionAndRotation(position, Quaternion.identity);
            return this;
        }

        /// <summary>
        /// Sets size for the button if it wasn't managed by a layout group
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public CanvasButton Size(Vector2 size) {
            rectTransform_.sizeDelta = size;
            return this;
        }

        public void OnPointerEnter(PointerEventData eventData) {
            Log.Info("Entered CanvasButton");
            imgComponent_.color = this.colors.highlightedColor;
        }

        public void OnPointerExit(PointerEventData eventData) {
            Log.Info("Exited CanvasButton");
            imgComponent_.color = this.colors.normalColor;
        }

        /// <summary>
        /// See https://docs.unity3d.com/Manual/script-LayoutElement.html for preferred, minimum and
        /// flexible sizes
        /// </summary>
        /// <param name="val">Value to set</param>
        /// <returns>This</returns>
        public CanvasButton PreferredHeight(float val) {
            layoutElement_.minHeight = val;
            layoutElement_.preferredHeight = val;
            return this;
        }
    }
}