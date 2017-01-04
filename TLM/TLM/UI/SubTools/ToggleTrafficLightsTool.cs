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

			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags & NetNode.Flags.Junction) == NetNode.Flags.None)
				return;

			TrafficLightSimulation sim = TrafficLightSimulationManager.Instance.GetNodeSimulation(HoveredNodeId);
			if (sim != null && sim.IsTimedLight()) {
				MainTool.ShowTooltip(Translation.GetString("NODE_IS_TIMED_LIGHT"), Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_position);
				return;
			}

			TrafficPriorityManager.Instance.RemovePrioritySegments(HoveredNodeId);
			TrafficLightManager.Instance.ToggleTrafficLight(HoveredNodeId);
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
			/*
			var segment = Singleton<NetManager>.instance.m_segments.m_buffer[Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_segment0];

			Bezier3 bezier;
			bezier.a = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_position;
			bezier.d = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_position;

			var color = MainTool.GetToolColor(Input.GetMouseButton(0), false);

			NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
				segment.m_endDirection,
				false, false, out bezier.b, out bezier.c);

			MainTool.DrawOverlayBezier(cameraInfo, bezier, color);*/
		}
	}
}
