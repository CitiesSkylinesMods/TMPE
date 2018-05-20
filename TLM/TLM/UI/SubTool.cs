using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TrafficManager.UI {
	public abstract class SubTool {
		public TrafficManagerTool MainTool { get; set; }

		protected Texture2D WindowTexture {
			get {
				if (windowTexture == null) {
					windowTexture = TrafficManagerTool.AdjustAlpha(TextureResources.WindowBackgroundTexture2D, MainTool.GetWindowAlpha());
				}
				return windowTexture;
			}
		}
		private Texture2D windowTexture = null;

		protected GUIStyle WindowStyle {
			get {
				if (windowStyle == null) {
					windowStyle = new GUIStyle {
						normal = {
							background = WindowTexture,
							textColor = Color.white
						},
						alignment = TextAnchor.UpperCenter,
						fontSize = 20,
						border = {
							left = 4,
							top = 41,
							right = 4,
							bottom = 8
						},
						overflow = {
							bottom = 0,
							top = 0,
							right = 12,
							left = 12
						},
						contentOffset = new Vector2(0, -44),
						padding = {
							top = 55
						}
					};
				}

				return windowStyle;
			}
		}
		private GUIStyle windowStyle = null;

		protected Texture2D BorderlessTexture {
			get {
				if (borderlessTexture == null) {
					borderlessTexture = TrafficManagerTool.MakeTex(1, 1, new Color(0.5f, 0.5f, 0.5f, MainTool.GetWindowAlpha()));
				}
				return borderlessTexture;
			}
		}
		private Texture2D borderlessTexture = null;

		protected GUIStyle BorderlessStyle {
			get {
				if (borderlessStyle == null) {
					borderlessStyle = new GUIStyle {
						normal = { background = BorderlessTexture },
						alignment = TextAnchor.MiddleCenter,
						border = {
							bottom = 2,
							top = 2,
							right = 2,
							left = 2
						}
					};
				}

				return borderlessStyle;
			}
		}
		private GUIStyle borderlessStyle = null;

		protected ushort HoveredNodeId {
			get { return TrafficManagerTool.HoveredNodeId; }
			set { TrafficManagerTool.HoveredNodeId = value; }
		}

		protected ushort HoveredSegmentId {
			get { return TrafficManagerTool.HoveredSegmentId; }
			set { TrafficManagerTool.HoveredSegmentId = value; }
		}

		protected ushort SelectedNodeId {
			get { return TrafficManagerTool.SelectedNodeId; }
			set { TrafficManagerTool.SelectedNodeId = value; }
		}

		protected ushort SelectedSegmentId {
			get { return TrafficManagerTool.SelectedSegmentId; }
			set { TrafficManagerTool.SelectedSegmentId = value; }
		}

		public SubTool(TrafficManagerTool mainTool) {
			MainTool = mainTool;
		}

		public void OnToolUpdate() {
			//OnLeftClickOverlay();
		}

		float nativeWidth = 1920;
		float nativeHeight = 1200;

		/// <summary>
		/// Called whenever the 
		/// </summary>
		public abstract void OnPrimaryClickOverlay();
		public virtual void OnSecondaryClickOverlay() { }
		public virtual void OnToolGUI(Event e) {
			//set up scaling
			/*Vector2 resolution = UIView.GetAView().GetScreenResolution();
			float rx = resolution.x / nativeWidth;
			float ry = resolution.y / nativeHeight;
			GUI.matrix = Matrix4x4.TRS(new Vector3(0, 0, 0), Quaternion.identity, new Vector3(rx, ry, 1));*/
		}
		public abstract void RenderOverlay(RenderManager.CameraInfo cameraInfo);
		public virtual void Initialize() {
			borderlessTexture = null;
			borderlessStyle = null;
			windowTexture = null;
			windowStyle = null;
		}
		public virtual void Cleanup() { }
		public virtual void OnActivate() { }
		public virtual void RenderInfoOverlay(RenderManager.CameraInfo cameraInfo) { }
		public virtual void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) { }
		public virtual bool IsCursorInPanel() {
			return LoadingExtension.BaseUI.GetMenu().containsMouse
#if DEBUG
				|| LoadingExtension.BaseUI.GetDebugMenu().containsMouse
#endif
				;
		}
		public virtual string GetTutorialKey() {
			return this.GetType().Name;
		}

		protected void DragWindow(ref Rect window) {
			Vector2 resolution = UIView.GetAView().GetScreenResolution();
			window.x = Mathf.Clamp(window.x, 0, resolution.x - window.width);
			window.y = Mathf.Clamp(window.y, 0, resolution.y - window.height);

			bool primaryMouseDown = Input.GetMouseButton(0);
			if (primaryMouseDown) {
				GUI.DragWindow();
			}
		}
	}
}
