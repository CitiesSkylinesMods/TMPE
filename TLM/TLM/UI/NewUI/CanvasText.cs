namespace TrafficManager.UI.NewUI {
    using ColossalFramework.UI;
    using JetBrains.Annotations;
    using UnityEngine;
    using UnityEngine.UI;

    public class CanvasText : MonoBehaviour {
        /// <summary>
        /// Place a text label on the form
        /// </summary>
        /// <param name="parent">Used for nesting controls, can be NULL to nest under the
        ///     canvas itself.</param>
        /// <param name="position">Where the object is placed</param>
        /// <param name="size">The size</param>
        /// <param name="textName">GameObject is given this name</param>
        /// <param name="text">The text to show</param>
        public static GameObject Create([CanBeNull]
                                        GameObject parent,
                                        Vector2 position,
                                        Vector2 size,
                                        string textName,
                                        string text) {
            var textObject = new GameObject(textName);
            //textObject.AddComponent<CanvasRenderer>();

            UIView coUiView = UIView.GetAView(); // for font

            var textComponent = textObject.AddComponent<Text>();
            textComponent.font = coUiView.defaultFont.baseFont;
            textComponent.text = text;
            textComponent.fontSize = 15;
            textComponent.color = Color.white;

            // Text position
            var rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetPositionAndRotation(position, Quaternion.identity);
            rectTransform.sizeDelta = size;

            textObject.transform.SetParent(parent.transform, false);
            return textObject;
        }
    }
}