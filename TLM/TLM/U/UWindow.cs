namespace TrafficManager.U {
    using CSUtil.Commons;
    using TrafficManager.U.Events;
    using ColossalFramework.UI;
    using Controls;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;
    using Object = UnityEngine.Object;

    /// <summary>
    /// UWindow is a component for GameObject.
    /// Created in an Unity Canvas to hold other UI elements. 
    /// Coordinate system: (0, 0) is screen center
    /// </summary>
    public class UWindow
        : MonoBehaviour
    {
        public uint textCounter_ = 1;
        public uint buttonCounter_ = 1;
        public int panelCounter_ = 1;
        
        private UEventsBlockingCoUiPanel coUiPanel_;
//        private GameObject windowBackground_;

        // private string GUI_FONT = "OpenSans-Semibold";

        /// <summary>
        /// Initializes a new instance of the <see cref="UWindow"/> class.
        /// UWindow component is added to the root level Canvas.
        /// Note the ctor is private, only UWindow can create self and only in an Unity canvas.
        /// </summary>
        private UWindow() {
            GameObject obj = this.gameObject;
            
            CreateCoUiPanel(obj.name);
            SetupEventSystem();

            // By default the form gets a vertical layout group component.
            CreateCanvasFormBackground();
            // don't do this on construction: ApplyConstraints();
        }

//        public static GameObject CreateCanvasGameObject(string canvasName) {
//            DestroyAllWithName(canvasName);
//
//            GameObject canvasObject = CreateCanvasObject(canvasName);
//            canvasObject.AddComponent<UControl>(); // must be before creating UWindow
//            canvasObject.AddComponent<UWindow>();
//            return canvasObject;
//        }

//        public void ApplyConstraints() {
//            Log._Assert(this.rootObject_ != null, "Must create rootObject before applying constraints");
//
//            void ApplyRecursive(GameObject obj) {
//                var rectTr = obj.GetComponent<RectTransform>();
//                var control = obj.GetComponent<UControl>();
//                
//                if ((control != null) && (rectTr != null)) {
//                    control.ApplyConstraints(rectTr);
//                }
//
//                foreach (Transform child in obj.transform) {
//                    ApplyRecursive(child.gameObject);
//                }
//            }
//            
//            // Root form will be inherited from UControl instead of containing a component UControl,
//            // so it will not be matched in `GetComponent<UControl>` above.
//            ApplyConstraints(this.rootObject_.GetComponent<RectTransform>());
//            
//            ApplyRecursive(this.rootObject_);
//            
//            this.coPanel_.AdjustToMatch(this.rootObject_);
//        }

        public static GameObject CreateCanvasObject(string canvasName) {
            DestroyAllWithName(canvasName);
            
            var canvasObject = new GameObject { name = canvasName };

            var canvasComponent = canvasObject.AddComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;

            var scalerComponent = canvasObject.AddComponent<CanvasScaler>();
            scalerComponent.scaleFactor = 1f;
            scalerComponent.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scalerComponent.referenceResolution = new Vector2(Screen.width, Screen.height);
            scalerComponent.dynamicPixelsPerUnit = 1f;
            // scalerComponent.referencePixelsPerUnit = 100f;

            canvasObject.AddComponent<GraphicRaycaster>();
            
            var rectComponent = canvasObject.GetComponent<RectTransform>();
            rectComponent.anchoredPosition = Vector2.zero;
            rectComponent.offsetMin = Vector2.zero;
            rectComponent.offsetMax = Vector2.zero;
            rectComponent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Screen.width);
            rectComponent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Screen.height);
            
            canvasObject.AddComponent<UWindow>();

            return canvasObject;
        }

        private void CreateCoUiPanel(string canvasName) {
            UIView coView = UIView.GetAView();
            this.coUiPanel_ = coView.AddUIComponent(typeof(UEventsBlockingCoUiPanel))
                           as UEventsBlockingCoUiPanel;
            this.coUiPanel_.name = canvasName;
        }

        private void SetupEventSystem() {
            // Event system is already created on canvas
            var eventSystem = this.gameObject.AddComponent<EventSystem>();
            eventSystem.sendNavigationEvents = true;

            this.gameObject.AddComponent<CanvasRenderer>();

            var standaloneInput = this.gameObject.AddComponent<StandaloneInputModule>();
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
//            this.windowBackground_ = new GameObject { name = "Form Background" };
//            this.windowBackground_.transform.SetParent(this.gameObject.transform, false);

            //formObject_.AddComponent<CanvasRenderer>();

            // Set form group size
//            this.windowBackground_.AddComponent<RectTransform>();

//            var imageComponent = this.windowBackground_.AddComponent<USimpleGraphic>();
            // imageComponent.color = new Color32(47, 47, 47, 220); // semi-transparent gray
//            imageComponent.color = Constants.NORMAL_UI_BACKGROUND; // solid gray
//            imageComponent.raycastTarget = true; // block clicks through it

//            imageComponent.overrideSprite = Sprite.Create(
//                JunctionUITextures.UturnAllowedTexture2D,
//                new Rect(
//                    0f,
//                    0f,
//                    JunctionUITextures.UturnAllowedTexture2D.width,
//                    JunctionUITextures.UturnAllowedTexture2D.height),
//                Vector2.zero); 

//            var vlgComponent = this.windowBackground_.AddComponent<VerticalLayoutGroup>();
//            vlgComponent.childControlHeight = false; // let the controls stack but not stretch
//            this.windowBackground_.AddComponent<UEventInlet>();
        }

        /// <summary>
        /// To prevent same forms created multiple times, try delete the old form with the same name
        /// </summary>
        /// <param name="canvasName">Gameobject name to find and delete</param>
        private static void DestroyAllWithName(string canvasName) {
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

            var ucontrol = GetComponent<UConstrained>();
            
            // For each constraint, find those setting left and top coords, and change the values
            ucontrol.ForEachConstraintModify(c => {
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
            ucontrol.ApplyConstraints();
        }
    }
}