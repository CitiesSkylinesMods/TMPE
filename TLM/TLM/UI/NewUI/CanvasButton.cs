namespace TrafficManager.UI.NewUI {
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    public class CanvasButton
        : Button,
          IPointerExitHandler,
          IPointerEnterHandler
    {
        /// <summary>
        /// Either contains an image in this button, or colors the button with a solid color
        /// </summary>
        private Image imgComponent_;

        public static CanvasButton Create([CanBeNull]
                                        GameObject parent,
                                        Vector2 position,
                                        Vector2 size,
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

//            var rectTransform = btnObject.AddComponent<RectTransform>();
//            rectTransform.SetPositionAndRotation(position, Quaternion.identity);
//            rectTransform.sizeDelta = size;

            // Nested text for the button
            if (!string.IsNullOrEmpty(text)) {
                CanvasText.Create(btnObject, position, size, "Label", text);
            }

            btnObject.transform.SetParent(parent.transform, false);

            return buttonComponent;
        }

        protected override void Start() {
            imgComponent_ = this.gameObject.AddComponent<Image>();
            imgComponent_.color = this.colors.normalColor;
        }

        public void OnPointerEnter(PointerEventData eventData) {
            Log.Info("Entered CanvasButton");
            imgComponent_.color = this.colors.highlightedColor;
        }

        public void OnPointerExit(PointerEventData eventData) {
            Log.Info("Exited CanvasButton");
            imgComponent_.color = this.colors.normalColor;
        }
    }
}