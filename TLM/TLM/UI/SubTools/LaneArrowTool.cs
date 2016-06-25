using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;

namespace TrafficManager.UI.SubTools {
	public class LaneArrowTool : SubTool {
		private bool _cursorInSecondaryPanel;
		private Texture2D SecondPanelTexture;

		public LaneArrowTool(TrafficManagerTool mainTool) : base(mainTool) {
			SecondPanelTexture = TrafficManagerTool.MakeTex(1, 1, new Color(0.5f, 0.5f, 0.5f, 1f));
		}

		public override bool IsCursorInPanel() {
			return _cursorInSecondaryPanel;
		}

		public override void OnClickOverlay() {
			if (HoveredNodeId == 0 || HoveredSegmentId == 0) return;

			var netFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags;

			if ((netFlags & NetNode.Flags.Junction) == NetNode.Flags.None) return;

			if (Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId].m_startNode != HoveredNodeId &&
				Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId].m_endNode != HoveredNodeId)
				return;

			SelectedSegmentId = HoveredSegmentId;
			SelectedNodeId = HoveredNodeId;
		}

		public override void OnToolGUI(Event e) {
			_cursorInSecondaryPanel = false;

			if (SelectedNodeId == 0 || SelectedSegmentId == 0) return;

			int numDirections;
			int numLanes = TrafficManagerTool.GetSegmentNumVehicleLanes(SelectedSegmentId, SelectedNodeId, out numDirections);
			if (numLanes <= 0) {
				SelectedNodeId = 0;
				SelectedSegmentId = 0;
				return;
			}

			var style = new GUIStyle {
				normal = { background = SecondPanelTexture },
				alignment = TextAnchor.MiddleCenter,
				border =
				{
					bottom = 2,
					top = 2,
					right = 2,
					left = 2
				}
			};

			Vector3 nodePos = Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId].m_position;
			var screenPos = Camera.main.WorldToScreenPoint(nodePos);
			screenPos.y = Screen.height - screenPos.y;
			//Log._Debug($"node pos of {SelectedNodeId}: {nodePos.ToString()} {screenPos.ToString()}");
			if (screenPos.z < 0)
				return;
			var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
			var diff = nodePos - camPos;

			if (diff.magnitude > TrafficManagerTool.PriorityCloseLod)
				return; // do not draw if too distant

			int width = Math.Max(3 * 128 + 20, numLanes * 128);
			var windowRect3 = new Rect(screenPos.x - width / 2, screenPos.y - 70, width, 130);
			GUILayout.Window(250, windowRect3, _guiLaneChangeWindow, "", style);
			_cursorInSecondaryPanel = windowRect3.Contains(Event.current.mousePosition);
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			//Log._Debug($"LaneArrow Overlay: {HoveredNodeId} {HoveredSegmentId} {SelectedNodeId} {SelectedSegmentId}");
			if (HoveredSegmentId != 0 && HoveredNodeId != 0 && (HoveredSegmentId != SelectedSegmentId || HoveredNodeId != SelectedNodeId)) {
				var netFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags;

				if ((netFlags & NetNode.Flags.Junction) != NetNode.Flags.None) {
					NetTool.RenderOverlay(cameraInfo, ref Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId], MainTool.GetToolColor(false, false),
						MainTool.GetToolColor(false, false));
				}
			}

			if (SelectedSegmentId == 0) return;

			NetTool.RenderOverlay(cameraInfo, ref Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId], MainTool.GetToolColor(true, false), MainTool.GetToolColor(true, false));
		}

		private void _guiLaneChangeWindow(int num) {
			var info = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId].Info;

			List<object[]> laneList = TrafficManagerTool.GetSortedVehicleLanes(SelectedSegmentId, info, SelectedNodeId);
			SegmentGeometry geometry = SegmentGeometry.Get(SelectedSegmentId);

			GUILayout.BeginHorizontal();

			for (var i = 0; i < laneList.Count; i++) {
				var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[(uint)laneList[i][0]].m_flags;

				var style1 = new GUIStyle("button");
				var style2 = new GUIStyle("button") {
					normal = { textColor = new Color32(255, 0, 0, 255) },
					hover = { textColor = new Color32(255, 0, 0, 255) },
					focused = { textColor = new Color32(255, 0, 0, 255) }
				};

				var laneStyle = new GUIStyle { contentOffset = new Vector2(12f, 0f) };

				var laneTitleStyle = new GUIStyle {
					contentOffset = new Vector2(36f, 2f),
					normal = { textColor = new Color(1f, 1f, 1f) }
				};

				GUILayout.BeginVertical(laneStyle);
				GUILayout.Label(Translation.GetString("Lane") + " " + (i + 1), laneTitleStyle);
				GUILayout.BeginVertical();
				GUILayout.BeginHorizontal();
				if (!Flags.applyLaneArrowFlags((uint)laneList[i][0])) {
					Flags.removeLaneArrowFlags((uint)laneList[i][0]);
				}
				if (GUILayout.Button("←", ((flags & NetLane.Flags.Left) == NetLane.Flags.Left ? style1 : style2), GUILayout.Width(35), GUILayout.Height(25))) {
					if (!Flags.toggleLaneArrowFlags((uint)laneList[i][0], Flags.LaneArrows.Left) && SelectedNodeId > 0)
						MainTool.ShowTooltip(Translation.GetString("Lane_Arrow_Changer_Disabled"), Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId].m_position);
				}
				if (GUILayout.Button("↑", ((flags & NetLane.Flags.Forward) == NetLane.Flags.Forward ? style1 : style2), GUILayout.Width(25), GUILayout.Height(35))) {
					if (!Flags.toggleLaneArrowFlags((uint)laneList[i][0], Flags.LaneArrows.Forward) && SelectedNodeId > 0)
						MainTool.ShowTooltip(Translation.GetString("Lane_Arrow_Changer_Disabled"), Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId].m_position);
				}
				if (GUILayout.Button("→", ((flags & NetLane.Flags.Right) == NetLane.Flags.Right ? style1 : style2), GUILayout.Width(35), GUILayout.Height(25))) {
					if (!Flags.toggleLaneArrowFlags((uint)laneList[i][0], Flags.LaneArrows.Right) && SelectedNodeId > 0)
						MainTool.ShowTooltip(Translation.GetString("Lane_Arrow_Changer_Disabled"), Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId].m_position);
				}
				GUILayout.EndHorizontal();
				GUILayout.EndVertical();
				GUILayout.EndVertical();
			}

			GUILayout.EndHorizontal();

			GUILayout.BeginVertical();

			bool startNode = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId].m_startNode == SelectedNodeId;

			if (!geometry.AreHighwayRulesEnabled(startNode)) {
				if (!geometry.IsOneWay()) {
					Flags.setUTurnAllowed(SelectedSegmentId, startNode, GUILayout.Toggle(Flags.getUTurnAllowed(SelectedSegmentId, startNode), Translation.GetString("Allow_u-turns") + " (BETA feature)", new GUILayoutOption[] { }));
				}
				if (geometry.HasOutgoingStraightSegment(startNode)) {
					Flags.setStraightLaneChangingAllowed(SelectedSegmentId, startNode, GUILayout.Toggle(Flags.getStraightLaneChangingAllowed(SelectedSegmentId, startNode), Translation.GetString("Allow_lane_changing_for_vehicles_going_straight"), new GUILayoutOption[] { }));
				}
				Flags.setEnterWhenBlockedAllowed(SelectedSegmentId, startNode, GUILayout.Toggle(Flags.getEnterWhenBlockedAllowed(SelectedSegmentId, startNode), Translation.GetString("Allow_vehicles_to_enter_a_blocked_junction"), new GUILayoutOption[] { }));
			}

			GUILayout.EndVertical();
		}
	}
}
