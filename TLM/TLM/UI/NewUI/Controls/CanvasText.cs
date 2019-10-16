namespace TrafficManager.UI.NewUI.Controls {
    using ColossalFramework.UI;
    using JetBrains.Annotations;
    using UnityEngine;
    using UnityEngine.UI;

    public class CanvasText
        : Text {
        private static LayoutElement layoutElement_;
        private static RectTransform rectTransform_;

        /// <summary>
        /// Place a text label on the form
        /// </summary>
        /// <param name="parent">Used for nesting controls, can be NULL to nest under the
        ///     canvas itself.</param>
        /// <param name="position">Where the object is placed</param>
        /// <param name="size">The size</param>
        /// <param name="textName">GameObject is given this name</param>
        /// <param name="text">The text to show</param>
        public static CanvasText Create([NotNull] GameObject parent,
                                        string textName,
                                        string text) {
            var textObject = new GameObject(textName);
            //textObject.AddComponent<CanvasRenderer>();

            rectTransform_ = textObject.GetComponent<RectTransform>();
            layoutElement_ = textObject.AddComponent<LayoutElement>();

            UIView coUiView = UIView.GetAView(); // for font

            var textComponent = textObject.AddComponent<CanvasText>();
            textComponent.font = coUiView.defaultFont.baseFont;
            textComponent.text = text;
            textComponent.fontSize = 15;
            textComponent.color = Color.white;

            textObject.transform.SetParent(parent.transform, false);
            return textComponent;
        }

        public CanvasText Position(Vector2 position) {
            rectTransform_.SetPositionAndRotation(position, Quaternion.identity);
            return this;
        }

        public CanvasText Size(Vector2 size) {
            rectTransform_.sizeDelta = size;
            return this;
        }

        /// <summary>
        /// See https://docs.unity3d.com/Manual/script-LayoutElement.html for preferred, minimum and
        /// flexible sizes
        /// </summary>
        /// <param name="val">Value to set</param>
        /// <returns>This</returns>
        public CanvasText PreferredHeight(float val) {
            layoutElement_.minHeight = val;
            layoutElement_.preferredHeight = val;
            return this;
        }
    }
}