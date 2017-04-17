using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using UnityEngine;
using TrafficManager.Manager;

namespace TrafficManager.UI.SubTools {
	public class ToggleTrafficLightsTool : SubTool {
		public ToggleTrafficLightsTool(TrafficManagerTool mainTool) : base(mainTool) {
			
		}

		public override void OnPrimaryClickOverlay() {
			if (IsCursorInPanel())
				return;
			if (HoveredNodeId == 0)
				return;

			ToggleTrafficLight(HoveredNodeId);
		}

		public void ToggleTrafficLight(ushort nodeId) {
			TrafficLightManager.UnableReason reason;
			if (!TrafficLightManager.Instance.IsTrafficLightToggleable(nodeId, out reason)) {
				if (reason == TrafficLightManager.UnableReason.HasTimedLight) {
					Constants.ServiceFactory.NetService.ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
						MainTool.ShowTooltip(Translation.GetString("NODE_IS_TIMED_LIGHT"), node.m_position);
						return true;
					});
				}
				return;
			}

			TrafficPriorityManager.Instance.RemovePrioritySignsFromNode(nodeId);
			TrafficLightManager.Instance.ToggleTrafficLight(nodeId);
		}

		public override void OnToolGUI(Event e) {
			
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			if (MainTool.GetToolController().IsInsideUI || !Cursor.visible) {
				return;
			}

			if (HoveredNodeId == 0) return;

			if (!Flags.mayHaveTrafficLight(HoveredNodeId)) return;

			MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId, Input.GetMouseButton(0), false);
		}
	}
}
