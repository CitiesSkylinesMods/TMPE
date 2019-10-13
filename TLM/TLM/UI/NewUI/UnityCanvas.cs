namespace TrafficManager.UI.NewUI {
    using ColossalFramework.UI;
    using JetBrains.Annotations;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    /// <summary>
    /// Creates an Unity Canvas to hold other UI elements and assists with filling up the form.
    /// Coordinate system: (0, 0) is screen center
    /// </summary>
    public class UnityCanvas {
        private GameObject canvasObject_;
        private Canvas canvasComponent_;
        private uint textCounter_ = 1;
        private uint buttonCounter_ = 1;
        private string GUI_FONT = "OpenSans-Semibold";

        /// <summary>
        /// Allows scaling the UI up or down based on vertical resolution
        /// </summary>
        private ScreenScaling scaling_;

        /// <summary>
        /// Child to the canvas, contains form and background
        /// </summary>
        private GameObject formObject_;

        /// <summary>
        /// Transparent CO.UI panel to capture clicks, should maintain size and position always
        /// under the canvas' main background object
        /// </summary>
        private EventsBlockingCoUiPanel coPanel_;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnityCanvas"/> class.
        /// Constructs a root level Canvas with a name
        /// </summary>
        /// <param name="canvasName">The gameobject name in scene tree</param>
        public UnityCanvas(string canvasName, Vector2 pos, Vector2 size) {
            DestroyAllWithName(canvasName);
            CreateCanvasObject(canvasName, pos);
            CreateCoUiPanel(canvasName, pos, size);
            SetupEventSystem();
            // By default the form gets a vertical layout group component.
            CreateForm(pos, size);
        }

        private void CreateCanvasObject(string canvasName, Vector2 pos) {
            canvasObject_ = new GameObject { name = canvasName };
            canvasObject_.transform.SetPositionAndRotation(pos, Quaternion.identity);

            canvasComponent_ = canvasObject_.AddComponent<Canvas>();
            canvasComponent_.renderMode = RenderMode.ScreenSpaceOverlay;

            var scalerComponent = canvasObject_.AddComponent<CanvasScaler>();
            scalerComponent.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scalerComponent.referenceResolution = new Vector2(1920f, 1080f);
            scalerComponent.dynamicPixelsPerUnit = 1f;
            scalerComponent.referencePixelsPerUnit = 100f;

            canvasObject_.AddComponent<GraphicRaycaster>();

//            var rectComponent = canvasObject_.GetComponent<RectTransform>();
//            rectComponent.rect.Set(pos.x, pos.y, size.x, size.y);
        }

        private void CreateCoUiPanel(string canvasName, Vector2 pos, Vector2 size) {
            UIView coView = UIView.GetAView();
            coPanel_ = coView.AddUIComponent(typeof(EventsBlockingCoUiPanel)) as EventsBlockingCoUiPanel;
            coPanel_.name = canvasName;
            coPanel_.position = pos;
            coPanel_.size = size;

            UIView.SetFocus(coPanel_);
        }

        private void SetupEventSystem() {
            // Event system is already created on canvas
            var eventSystem = canvasObject_.AddComponent<EventSystem>();
            eventSystem.sendNavigationEvents = true;

            canvasObject_.AddComponent<CanvasRenderer>();

            var standaloneInput = canvasObject_.AddComponent<StandaloneInputModule>();
            standaloneInput.horizontalAxis = "Horizontal";
            standaloneInput.verticalAxis = "Vertical";
            standaloneInput.submitButton = "Submit";
            standaloneInput.cancelButton = "Cancel";
            standaloneInput.inputActionsPerSecond = 10;
            standaloneInput.repeatDelay = 0.5f;
            standaloneInput.forceModuleActive = false;
        }

        /// <summary>
        /// To prevent same forms created multiple times, try delete the old form with the same name
        /// </summary>
        /// <param name="canvasName"></param>
        private void DestroyAllWithName(string canvasName) {
            // Try finding and destroying objects with the given name
            for (var nTry = 0; nTry < 15; nTry++) {
                GameObject destroy = GameObject.Find(canvasName);
                if (destroy == null) {
                    break;
                }

                UnityEngine.Object.Destroy(destroy);
            }
        }

        /// <summary>
        /// In the empty canvas creates a form with background image.
        /// </summary>
        private void CreateForm(Vector2 pos, Vector2 size) {
            formObject_ = new GameObject { name = "Form Background" };
            formObject_.transform.SetParent(canvasObject_.transform, false);

            //formObject_.AddComponent<CanvasRenderer>();

            // Set form group size
            var rectTr = formObject_.AddComponent<RectTransform>();
            rectTr.SetPositionAndRotation(pos, Quaternion.identity);
//            rectTr.localPosition = pos;
            rectTr.anchoredPosition = new Vector2(0f, 0f);
            rectTr.sizeDelta = size;

            var imageComponent = formObject_.AddComponent<EventsBlockingGraphic>();
            // imageComponent.color = new Color32(47, 47, 47, 220); // semi-transparent gray
            imageComponent.color = Constants.NORMAL_UI_BACKGROUND; // solid gray
            imageComponent.raycastTarget = true; // block clicks through it

//            imageComponent.overrideSprite = Sprite.Create(
//                JunctionUITextures.UturnAllowedTexture2D,
//                new Rect(
//                    0f,
//                    0f,
//                    JunctionUITextures.UturnAllowedTexture2D.width,
//                    JunctionUITextures.UturnAllowedTexture2D.height),
//                Vector2.zero);

            formObject_.AddComponent<VerticalLayoutGroup>();

            formObject_.AddComponent<CanvasFormEvents>();
        }

        public GameObject Text([CanBeNull]
                               GameObject parent,
                               Vector2 position,
                               Vector2 size,
                               string text) {
            return CanvasText.Create(
                parent == null ? formObject_ : parent,
                position,
                size,
                $"Text{textCounter_++}",
                text);
        }

        public CanvasButton Button([CanBeNull]
                                   GameObject parent,
                                   Vector2 position,
                                   Vector2 size,
                                   string text) {
            return CanvasButton.Create(
                parent == null ? formObject_ : parent,
                position,
                size,
                $"Button{buttonCounter_++}",
                text);
        }

        public GameObject HorizontalLayoutGroup([CanBeNull]
                                                GameObject parent,
                                                string groupName) {
            var groupObject = new GameObject(groupName);
            groupObject.AddComponent<HorizontalLayoutGroup>();
            groupObject.transform.SetParent(
                parent == null ? formObject_.transform : parent.transform,
                false);
            return groupObject;
        }
    }
}