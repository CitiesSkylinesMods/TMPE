using System;
using System.Reflection;
using CSUtil.Commons;
using UnityEngine;
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
			scalerComponent.dynamicPixelsPerUnit = 8f;
			scalerComponent.referencePixelsPerUnit = 1f;

			canvasGameObj_.AddComponent<GraphicRaycaster>();
			canvasComponent.worldCamera = Camera.main;
		}

		public void DestroyCanvas() {
			UnityEngine.Object.Destroy(canvasGameObj_);
			canvasGameObj_ = null;
		}

		private RectTransform SetupRectTransform(GameObject gameObject, Vector3 pos, Vector2 size) {
			var rectTransform = gameObject.GetComponent<RectTransform>();
			rectTransform.localPosition = pos;
			rectTransform.sizeDelta = size;
			rectTransform.localScale = new Vector3(1f, 1f, 1f);
			rectTransform.localRotation = Quaternion.identity;
			return rectTransform;
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
			if (imageComponent == null) {
				imageComponent = button.AddComponent<Image>();
			}
			// imageComponent.material.mainTexture = tex;
			imageComponent.sprite = Sprite.Create(tex,
			                                      new Rect(0, 0, tex.width, tex.height),
			                                      Vector2.zero);
			SetupMaterial(imageComponent.material);
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

	}
}
