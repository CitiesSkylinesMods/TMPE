using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using CSUtil.Commons;
using OptionsFramework.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = System.Object;

namespace TrafficManager.UI.CanvasGUI {
    /// <summary>
    /// Creates canvas in the world space somewhere on map, and also creates
    /// controls for it and assists with styling, etc.
    /// </summary>
    public class WorldSpaceGUI {
        private GameObject canvasGameObj_;
        private ulong counter_;
        private Shader seeThroughShader_;

        // Raycaster and eventsystem handle the input
        private GraphicRaycaster raycaster_;
        private EventSystem eventSystem_;
        private bool mouse1Held = false;

        public WorldSpaceGUI(Vector3 pos, Quaternion rot) {
            // seeThroughShader_ = Resources.Load<Shader>("WorldSpaceGUI.SeeThroughZ");

            canvasGameObj_ = new GameObject();
            canvasGameObj_.name = "Canvas " + (counter_++);

            var canvasComponent = canvasGameObj_.AddComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.WorldSpace;

            var rtComponent = canvasGameObj_.GetComponent<RectTransform>();
            rtComponent.localScale = new Vector3(1f, 1f, 1f);
            rtComponent.localPosition = pos;
            rtComponent.localRotation = rot;

            var scalerComponent = canvasGameObj_.AddComponent<CanvasScaler>();
            scalerComponent.dynamicPixelsPerUnit = 1f; // 1f is blurry, 8f gives reasonably readable text
            scalerComponent.referencePixelsPerUnit = 1f;

            // Fetch the Event System from the Scene
            raycaster_ = canvasGameObj_.AddComponent<GraphicRaycaster>();
            eventSystem_ = UnityEngine.Object.FindObjectOfType<EventSystem>();

            // Set the camera
            //	var mainCam = GameObject.FindGameObjectWithTag("MainCamera");
            //	Debug.Assert(mainCam != null);
            //	canvasComponent.worldCamera = mainCam.GetComponent<Camera>();

            // canvasComponent.worldCamera = Camera.main;
            // Log.Info($"All cameras count {Camera.allCamerasCount}");
        }

        public void DestroyCanvas() {
            UnityEngine.Object.Destroy(canvasGameObj_);
            canvasGameObj_ = null;
        }

        /// <summary>
        /// Sets up rect transform for the form element.
        /// As Unity canvas have Y facing up and it is more natural to have Y facing
        /// down like on a computer screen, the input position has its Y negated and
        /// shifted accordingly.
        /// </summary>
        /// <param name="gameObject">What are we moving</param>
        /// <param name="pos">The position with Y facing down, in a natural way</param>
        /// <param name="size">The size of the element</param>
        private void SetupRectTransform(GameObject gameObject, Vector3 pos, Vector2 size) {
            var rectTransform = gameObject.GetComponent<RectTransform>();

            // adjust position from a more natural way to Unity3d Y facing down
            pos.y = -(pos.y + size.y);

            rectTransform.localPosition = pos;
            rectTransform.localScale = new Vector3(1f, 1f, 1f);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            rectTransform.localRotation = Quaternion.identity;
        }

        public GameObject AddText(Vector3 pos, Vector2 size, string str, GameObject parent = null) {
            // Text
            var textGameObj = new GameObject();
            textGameObj.transform.SetParent(parent == null ? canvasGameObj_.transform : parent.transform);
            textGameObj.name = "Text " + (counter_++);

            var textComponent = textGameObj.AddComponent<Text>();
            // C:S fonts: OpenSans-Regular, OpenSans-Semibold. NanumGothic and ArchitectsDaughter
            // textComponent.font = Resources.GetBuiltinResource<Font>("OpenSans-Regular");
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComponent.text = str;
            textComponent.fontSize = 5; // in metres
            textComponent.color = Color.black;
            SetupMaterial(textComponent.material);

            // Text position
            SetupRectTransform(textGameObj, pos, size);
            return textGameObj;
        }

        private void SetupMaterial(Material material) {
            // material.shader = seeThroughShader_;
        }

        public GameObject AddButton(Vector3 pos, Vector2 size,
                                    string str = "",
                                    GameObject parent = null) {
            // Add the button object
            var buttonGameObj = new GameObject();
            buttonGameObj.transform.SetParent(parent == null ? canvasGameObj_.transform : parent.transform);
            buttonGameObj.name = "Button " + (counter_++);

            //-----------------

            buttonGameObj.AddComponent<RectTransform>();

            buttonGameObj.AddComponent<Image>();
            buttonGameObj.AddComponent<Button>();
            SetupRectTransform(buttonGameObj, pos, size);

            // Add text label
            if (str != string.Empty) {
                AddText(Vector3.zero, size, str, buttonGameObj);
            }

            return buttonGameObj;
        }

        public void SetButtonImage(GameObject button, Texture2D tex) {
            var imageComponent = button.GetComponent<Image>();

            // copy the material and reassign the texture
            var imageComponentMat = new Material(imageComponent.material);
            imageComponentMat.mainTexture = tex;
            SetupMaterial(imageComponentMat);

            imageComponent.material = imageComponentMat;
        }

        public void SetButtonSprite(GameObject button, Sprite sprite) {
            var imageComponent = button.GetComponent<Image>();
            imageComponent.sprite = sprite;
        }

        //		private static Shader LoadDllShader(string resourceName) {
        //			try {
        //				var myAssembly = Assembly.GetExecutingAssembly();
        //				var myStream = myAssembly.GetManifestResourceStream("TrafficManager.Resources." + resourceName);
        //
        //				var sh = new Shader();
        //
        //				sh.R(ReadToEnd(myStream));
        //
        //				return sh;
        //			}
        //			catch (Exception e) {
        //				Log.Error(e.StackTrace.ToString());
        //				return null;
        //			}
        //		}

        public void HandleInput() {
            if (!Input.GetKey(KeyCode.Mouse0)) {
                // Mouse1 is released, clear the flag
                mouse1Held = false;
                return;
            }

            if (mouse1Held) {
                // Do not create more than 1 click event
                return;
            }

            mouse1Held = true;

            var results = RaycastMouse();

            // For every result returned, output the name of the GameObject on the Canvas hit by the Ray
            if (results.Count > 0) {
                var button = results[0].gameObject.GetComponent<Button>();
                if (button != null) {
                    button.onClick.Invoke();
                }
            }

//			foreach (var result in results) {
//				Log.Info("Hit " + result.gameObject.name);
//			}
        }

        /// <summary>
        /// Take mouse position and find whether we hit anything
        /// </summary>
        /// <returns></returns>
        public List<RaycastResult> RaycastMouse() {
            // Set up the new Pointer Event
            var pointerEventData = new PointerEventData(eventSystem_);

            // Set the Pointer Event Position to that of the mouse position
            pointerEventData.position = Input.mousePosition;

            // Create a list of Raycast Results
            var results = new List<RaycastResult>();

            // Raycast using the Graphics Raycaster and mouse click position
            raycaster_.Raycast(pointerEventData, results);
            return results;
        }
    }
}
