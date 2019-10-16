namespace TrafficManager.UI.NewUI {
    using System;
    using ColossalFramework.UI;
    using Controls;
    using JetBrains.Annotations;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    /// <summary>
    /// Creates an Unity Canvas to hold other UI elements and assists with filling up the form.
    /// Coordinate system: (0, 0) is screen center
    /// </summary>
    public class CanvasForm {
        private GameObject canvasObject_;
        private Canvas canvasComponent_;

        public uint textCounter_ = 1;
        public uint buttonCounter_ = 1;
        public int panelCounter_ = 1;

        // private string GUI_FONT = "OpenSans-Semibold";

        /// <summary>
        /// Allows scaling the UI up or down based on vertical resolution
        /// </summary>
        private ScreenScaling scaling_;

        /// <summary>
        /// Child to the canvas, contains form root GameObject inside the Canvas, and is responsive
        /// for rendering solid background.
        /// </summary>
        public GameObject rootObject_;

        /// <summary>
        /// Transparent CO.UI panel to capture clicks, should maintain size and position always
        /// under the canvas' main background object
        /// </summary>
        private EventsBlockingCoUiPanel coPanel_;

        /// <summary>
        /// Initializes a new instance of the <see cref="CanvasForm"/> class.
        /// Constructs a root level Canvas with a name
        /// </summary>
        /// <param name="canvasName">The gameobject name in scene tree</param>
        public CanvasForm(string canvasName, Vector2 pos, Vector2 size) {
            DestroyAllWithName(canvasName);

            CreateCanvasObject(canvasName, pos);
            CreateCoUiPanel(canvasName, pos, size);
            SetupEventSystem();

            // By default the form gets a vertical layout group component.
            CreateCanvasFormBackground(pos, size);
            coPanel_.AdjustToMatch(this.rootObject_);
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
            coPanel_ = coView.AddUIComponent(typeof(EventsBlockingCoUiPanel))
                           as EventsBlockingCoUiPanel;
            coPanel_.name = canvasName;
            coPanel_.size = size;
            // UIView.SetFocus(coPanel_);
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
        /// In the empty canvas creates a form with background image.
        /// By default the form gets a vertical layout group component.
        /// </summary>
        private void CreateCanvasFormBackground(Vector2 pos, Vector2 size) {
            rootObject_ = new GameObject { name = "Form Background" };
            rootObject_.transform.SetParent(canvasObject_.transform, false);

            //formObject_.AddComponent<CanvasRenderer>();

            // Set form group size
            var rectTr = rootObject_.AddComponent<RectTransform>();
            // rectTr.position = pos;
            rectTr.anchoredPosition = pos;
//            rectTr.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, size.x);
//            rectTr.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 0, size.y);
            rectTr.sizeDelta = size;

            var imageComponent = rootObject_.AddComponent<SimpleGraphic>();
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

            rootObject_.AddComponent<VerticalLayoutGroup>();

            rootObject_.AddComponent<CanvasFormEvents>();
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
    }
}