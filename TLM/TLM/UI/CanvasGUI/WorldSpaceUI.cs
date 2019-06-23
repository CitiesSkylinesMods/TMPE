using System;
using System.Reflection;
using CSUtil.Commons;
using UnityEngine;
using UnityEngine.UI;
using Object = System.Object;

namespace TrafficManager.UI.CanvasGUI {
	public class WorldSpaceUI
	{
		private GameObject canvasGameObj_;
		private ulong counter_;
		private Shader seeThroughShader_;

		public WorldSpaceUI(Vector3 pos, Quaternion rot) {
			// seeThroughShader_ = Resources.Load<Shader>("WorldSpaceGUI.SeeThroughZ");

			canvasGameObj_ = new GameObject();
			canvasGameObj_.name = "WorldSpaceUI Canvas " + (counter_++);

			var canvasComponent = canvasGameObj_.AddComponent<Canvas>();
			canvasComponent.renderMode = RenderMode.WorldSpace;

			var scalerComponent = canvasGameObj_.AddComponent<CanvasScaler>();
			canvasGameObj_.AddComponent<GraphicRaycaster>();

			// var rtComponent = canvasGameObj_.GetComponent<RectTransform>();
			// rtComponent.rotation = rot;
			// rtComponent.position = pos;
			// rtComponent.localScale = new Vector3(1f, 1f, 1f);

			scalerComponent.dynamicPixelsPerUnit = 2f;
			scalerComponent.referencePixelsPerUnit = 1f;

			canvasComponent.worldCamera = Camera.main;
			canvasGameObj_.transform.SetPositionAndRotation(pos, rot);
			canvasGameObj_.transform.localScale = new Vector3(1f, 1f, 1f);
		}

		public void DestroyCanvas() {
			// canvasGO_.transform.SetParent(null);
			UnityEngine.Object.Destroy(canvasGameObj_);
			canvasGameObj_ = null;
		}

		private RectTransform SetupRectTransform(Transform c, Vector3 pos, Vector2 size) {
			var rectTransform = c.GetComponent<RectTransform>();
			rectTransform.localPosition = pos;
			rectTransform.sizeDelta = size;

			c.localScale = new Vector3(1f, 1f, 1f);
			c.rotation = Quaternion.identity;
			return rectTransform;
		}

		public GameObject AddText(Vector3 pos, Vector2 size, string str, GameObject parent = null) {
			// Text
			var textGameObj = new GameObject();
			textGameObj.transform.SetParent(parent == null ? canvasGameObj_.transform : parent.transform);
			textGameObj.name = "Text " + (counter_++);

			var textComponent = textGameObj.AddComponent<Text>();
			// C:S fonts: OpenSans-Regular, OpenSans-Semibold. NanumGothic and ArchitectsDaughter
			textComponent.font = Resources.GetBuiltinResource<Font>("OpenSans-Regular");
			textComponent.text = str;
			textComponent.fontSize = 10;
			textComponent.color = Color.black;
			SetupMaterial(textComponent.material);

			// Text position
			SetupRectTransform(textComponent.GetComponent<RectTransform>(), pos, size);
			return textGameObj;
		}

		private void SetupMaterial(Material material) {
			// material.shader = seeThroughShader_;
		}

		public GameObject AddButton(Vector3 pos, Vector2 size,
					    string str = "",
					    GameObject parent = null) {
			// Add the button object
			var buttonGO = new GameObject();
			buttonGO.transform.SetParent(parent == null ? canvasGameObj_.transform : parent.transform);
			buttonGO.name = "Button " + (counter_++);

			//-----------------

			buttonGO.AddComponent<RectTransform>();

			// var imageComponent = buttonGO.AddComponent<Image>();

			var imageComponent = buttonGO.AddComponent<Image>();
			var buttonComponent = buttonGO.AddComponent<Button>();
			// buttonGO.transform.position = pos;

			SetupRectTransform(buttonGO.transform, pos, size);

			// Add text label
			AddText(pos, size, str, buttonGO);

			return buttonGO;
		}

		public void SetButtonImage(GameObject button, Texture2D tex) {
			var imageComponent = button.GetComponent<Image>();
			if (imageComponent == null) {
				imageComponent = button.AddComponent<Image>();
			}
			imageComponent.material.mainTexture = tex;
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
