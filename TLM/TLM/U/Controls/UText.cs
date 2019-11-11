namespace TrafficManager.U.Controls {
    using System;
    using ColossalFramework.UI;
    using JetBrains.Annotations;
    using UnityEngine;
    using UnityEngine.UI;

    public class UText
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
        /// <returns>Newly created component (inside a gameobject)</returns>
        public static GameObject Create([NotNull] GameObject parent,
                                        string textName,
                                        string text) {
            var textObject = new GameObject(textName);
            //textObject.AddComponent<CanvasRenderer>();

            rectTransform_ = textObject.GetComponent<RectTransform>();
            layoutElement_ = textObject.AddComponent<LayoutElement>();

            UIView coUiView = UIView.GetAView(); // for font

            var textComponent = textObject.AddComponent<UText>();
            textComponent.font = coUiView.defaultFont.baseFont;
            textComponent.text = text;
            textComponent.fontSize = 15;
            textComponent.color = Color.white;

            textObject.transform.SetParent(parent.transform, false);

            textObject.AddComponent<UConstrained>();

            return textObject;
        }

        public UText Position(Vector2 position) {
            rectTransform_.SetPositionAndRotation(position, Quaternion.identity);
            return this;
        }

        public UText Size(Vector2 size) {
            rectTransform_.sizeDelta = size;
            return this;
        }

        public UConstrained GetUControlComponent() {
            return this.gameObject.GetComponent<UConstrained>();
        }

        public UText Alignment(TextAnchor ta) {
            this.alignment = ta;
            return this;
        }
    }
}