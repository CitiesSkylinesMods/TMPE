using CSUtil.Commons;
using TrafficManager.U.Events;

namespace TrafficManager.U {
    using System.Reflection;
    using ColossalFramework.UI;
    using Controls;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    /// <summary>
    /// Creates an Unity Canvas to hold other UI elements and assists with filling up the form.
    /// Coordinate system: (0, 0) is screen center
    /// </summary>
    public class UWindow
        : UControl
    {
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
        private UEventsBlockingCoUiPanel coPanel_;

        /// <summary>
        /// Initializes a new instance of the <see cref="UWindow"/> class.
        /// Constructs a root level Canvas with a name
        /// </summary>
        /// <param name="canvasName">The gameobject name in scene tree</param>
        public UWindow(string canvasName) {
            DestroyAllWithName(canvasName);

            CreateCanvasObject(canvasName);
            CreateCoUiPanel(canvasName);
            SetupEventSystem();

            // By default the form gets a vertical layout group component.
            CreateCanvasFormBackground();
            ApplyConstraints();
        }

        public override void ApplyConstraints() {
            Log._Assert(this.rootObject_ != null, "Must create rootObject before applying constraints");

            void ApplyRecursive(GameObject obj) {
                var rectTr = obj.GetComponent<RectTransform>();
                var control = obj.GetComponent<UControl>();
                
                if ((control != null) && (rectTr != null)) {
                    control.ApplyConstraints(rectTr);
                }

                foreach (Transform child in obj.transform) {
                    ApplyRecursive(child.gameObject);
                }
            }
            
            // Root form will be inherited from UControl instead of containing a component UControl,
            // so it will not be matched in `GetComponent<UControl>` above.
            ApplyConstraints(this.rootObject_.GetComponent<RectTransform>());
            
            ApplyRecursive(this.rootObject_);
            
            this.coPanel_.AdjustToMatch(this.rootObject_);
        }

        private void CreateCanvasObject(string canvasName) {
            this.canvasObject_ = new GameObject { name = canvasName };

            this.canvasComponent_ = this.canvasObject_.AddComponent<Canvas>();
            this.canvasComponent_.renderMode = RenderMode.ScreenSpaceOverlay;

            var scalerComponent = this.canvasObject_.AddComponent<CanvasScaler>();
            scalerComponent.scaleFactor = 1f;
            scalerComponent.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scalerComponent.referenceResolution = new Vector2(Screen.width, Screen.height);
            scalerComponent.dynamicPixelsPerUnit = 1f;
            // scalerComponent.referencePixelsPerUnit = 100f;

            this.canvasObject_.AddComponent<GraphicRaycaster>();
            
            var rectComponent = this.canvasObject_.GetComponent<RectTransform>();
            rectComponent.anchoredPosition = Vector2.zero;
            rectComponent.offsetMin = Vector2.zero;
            rectComponent.offsetMax = Vector2.zero;
            rectComponent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Screen.width);
            rectComponent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Screen.height);
        }

        private void CreateCoUiPanel(string canvasName) {
            UIView coView = UIView.GetAView();
            this.coPanel_ = coView.AddUIComponent(typeof(UEventsBlockingCoUiPanel))
                           as UEventsBlockingCoUiPanel;
            this.coPanel_.name = canvasName;
        }

        private void SetupEventSystem() {
            // Event system is already created on canvas
            var eventSystem = this.canvasObject_.AddComponent<EventSystem>();
            eventSystem.sendNavigationEvents = true;

            this.canvasObject_.AddComponent<CanvasRenderer>();

            var standaloneInput = this.canvasObject_.AddComponent<StandaloneInputModule>();
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
        private void CreateCanvasFormBackground() {
            this.rootObject_ = new GameObject { name = "Form Background" };
            this.rootObject_.transform.SetParent(this.canvasObject_.transform, false);

            //formObject_.AddComponent<CanvasRenderer>();

            // Set form group size
            this.rootObject_.AddComponent<RectTransform>();

            var imageComponent = this.rootObject_.AddComponent<USimpleGraphic>();
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

            this.rootObject_.AddComponent<VerticalLayoutGroup>();

            this.rootObject_.AddComponent<UEventInlet>();
        }

        /// <summary>
        /// To prevent same forms created multiple times, try delete the old form with the same name
        /// </summary>
        /// <param name="canvasName">Gameobject name to find and delete</param>
        private void DestroyAllWithName(string canvasName) {
            // Try finding and destroying objects with the given name
            for (var nTry = 0; nTry < 15; nTry++) {
                GameObject destroy = GameObject.Find(canvasName);
                if (destroy == null) {
                    break;
                }

                Object.Destroy(destroy);
            }
        }

        public void OnDrag(Vector2 drag) {
            Log._Debug($"OnDrag {drag}");
            // For each constraint, find those setting left and top coords, and change the values
            ForEachConstraintModify(c => {
                // magic scale is used to adjust mouse movements to match actual screen coords
                // i have no idea what is happening here, probably canvas scaling is borked
                const float MAGIC_SCALE = 2f;
                
                switch (c.field_) {
                    case TransformField.Left: {
                        Log._Assert(c.unit_ == Unit.Pixels, 
                                    "For dragging to work, the Left constraint must be set in pixels");
                        c.value_ += drag.x * MAGIC_SCALE;
                        break;
                    }
                    case TransformField.Top: {
                        Log._Assert(c.unit_ == Unit.Pixels, 
                                    "For dragging to work, the Top constraint must be set in pixels");
                        c.value_ += -drag.y * MAGIC_SCALE;
                        break;
                    }
                }
            });
            ApplyConstraints();
        }
    }
}