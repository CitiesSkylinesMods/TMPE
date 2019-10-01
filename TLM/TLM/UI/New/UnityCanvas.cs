namespace TrafficManager.UI.New {
    using ColossalFramework.UI;
    using JetBrains.Annotations;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Creates an Unity Canvas to hold other UI elements and assists with filling up the form.
    /// Coordinate system: (0, 0) is screen center
    /// </summary>
    public class UnityCanvas {
        private readonly GameObject canvasGameObject_;
        private readonly Canvas canvasComponent_;
        private uint textCounter_ = 1;
        private string GUI_FONT = "OpenSans-Semibold";

        /// <summary>
        /// Allows scaling the UI up or down based on vertical resolution
        /// </summary>
        private ScreenScaling scaling_;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnityCanvas"/> class.
        /// Constructs a root level Canvas with a name
        /// </summary>
        /// <param name="canvasName">The gameobject name in scene tree</param>
        public UnityCanvas(string canvasName) {
            canvasGameObject_ = new GameObject { name = canvasName };

            canvasComponent_ = canvasGameObject_.AddComponent<Canvas>();
            canvasComponent_.renderMode = RenderMode.ScreenSpaceOverlay;

            var scalerComponent = canvasGameObject_.AddComponent<CanvasScaler>();
            scalerComponent.dynamicPixelsPerUnit = 1f;
            scalerComponent.referencePixelsPerUnit = 1f;

            canvasGameObject_.AddComponent<GraphicRaycaster>();
        }

        /// <summary>
        /// Place a text label on the form
        /// </summary>
        /// <param name="parent">Used for nesting controls, can be NULL to nest under the
        ///     canvas itself.</param>
        /// <param name="position">Where the object is placed</param>
        /// <param name="size">The size</param>
        /// <param name="text">The text to show</param>
        public GameObject Text([CanBeNull]
                               GameObject parent,
                               Vector2 position,
                               Vector2 size,
                               string text) {
            var textGameObject = new GameObject();
            textGameObject.transform.parent = parent == null
                                                  ? canvasGameObject_.transform
                                                  : parent.transform;
            textGameObject.name = $"Text{textCounter_++}";

            UIView coUiView = UIView.GetAView();

            var textComponent = textGameObject.AddComponent<Text>();
            textComponent.font = coUiView.defaultFont.baseFont;
            textComponent.text = text;
            textComponent.fontSize = 15;
            textComponent.color = Color.white;

            // Text position
            var rectTransform = textComponent.GetComponent<RectTransform>();
            rectTransform.localPosition = position;
            rectTransform.sizeDelta = new Vector2(400, 200);

            return textGameObject;
        }
    }
}