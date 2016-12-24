using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TrafficManager.UI {
	public abstract class SubTool {
		public TrafficManagerTool MainTool { get; set; }

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
			float rx = Screen.width / nativeWidth;
			float ry = Screen.height / nativeHeight;
			GUI.matrix = Matrix4x4.TRS(new Vector3(0, 0, 0), Quaternion.identity, new Vector3(rx, ry, 1));
		}
		public abstract void RenderOverlay(RenderManager.CameraInfo cameraInfo);
		public virtual void Initialize() { }
		public virtual void Cleanup() { }
		public virtual void OnActivate() { }
		public virtual void RenderInfoOverlay(RenderManager.CameraInfo cameraInfo) { }
		public virtual void ShowGUIOverlay(bool viewOnly) { }
		public virtual bool IsCursorInPanel() {
			return UIBase.GetMenu().containsMouse;
		}

		protected void DragWindow(ref Rect window) {
			window.x = Mathf.Clamp(window.x, 0, Screen.width - window.width);
			window.y = Mathf.Clamp(window.y, 0, Screen.height - window.height);

			bool primaryMouseDown = Input.GetMouseButton(0);
			if (primaryMouseDown) {
				GUI.DragWindow();
			}
		}
	}
}
