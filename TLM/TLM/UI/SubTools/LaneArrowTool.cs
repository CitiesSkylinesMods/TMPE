using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using CSUtil.Commons;
using GenericGameBridge.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;
using TrafficManager.Manager;
using TrafficManager.Manager.Impl;
using TrafficManager.State;
using TrafficManager.State.ConfigData;
using TrafficManager.TrafficLight;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.UI.SubTools {
	public class LaneArrowTool : SubTool {
		private bool _cursorInSecondaryPanel;

		public LaneArrowTool(TrafficManagerTool mainTool) : base(mainTool) {
			
		}

		public override bool IsCursorInPanel() {
			return base.IsCursorInPanel() || _cursorInSecondaryPanel;
		}

		public override void OnPrimaryClickOverlay() {
			if (HoveredNodeId == 0 || HoveredSegmentId == 0) return;

			var netFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags;

			if ((netFlags & NetNode.Flags.Junction) == NetNode.Flags.None) return;

			if (Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId].m_startNode != HoveredNodeId &&
				Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId].m_endNode != HoveredNodeId)
				return;

			SelectedSegmentId = HoveredSegmentId;
			SelectedNodeId = HoveredNodeId;
		}

		public override void OnSecondaryClickOverlay() {
			if (!IsCursorInPanel()) {
				SelectedSegmentId = 0;
				SelectedNodeId = 0;
			}
		}

		public override void OnToolGUI(Event e) {
			//base.OnToolGUI(e);
			_cursorInSecondaryPanel = false;

			if (SelectedNodeId == 0 || SelectedSegmentId == 0) return;

			int numDirections;
			int numLanes = TrafficManagerTool.GetSegmentNumVehicleLanes(SelectedSegmentId, SelectedNodeId, out numDirections, LaneArrowManager.VEHICLE_TYPES);
			if (numLanes <= 0) {
				SelectedNodeId = 0;
				SelectedSegmentId = 0;
				return;
			}

			// Get currently selected now
			var nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
			Vector3 nodeWorldPos = nodesBuffer[SelectedNodeId].m_position;

			Vector3 nodeScreenPos;
			bool visible = MainTool.WorldToScreenPoint(nodeWorldPos, out nodeScreenPos);

			if (!visible) {
				return;
			}
			
			// Get the segment and the bezier for it to calculate screen angle
			var segment = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId];
			// var segmentDir = SelectedNodeId == segment.m_endNode ? segment.m_endDirection : segment.m_startDirection;
			float rotateDegrees;
			var otherNodeId = segment.GetOtherNode(SelectedNodeId);
			var otherNodeWorldPos = Singleton<NetManager>.instance.m_nodes.m_buffer[otherNodeId].m_position;

			if (segment.IsStraight()) {
				Vector3 otherNodeScreenPos;
				MainTool.WorldToScreenPoint(otherNodeWorldPos, out otherNodeScreenPos);
				var screenSegment = nodeScreenPos - otherNodeScreenPos;

				// Segment rotation in screen coords + 90 degrees
				rotateDegrees = 90f + Mathf.Atan2(screenSegment.y, screenSegment.x) * Mathf.Rad2Deg;
			} else {
				// Handle some curvature, take the last tangent
				var bezier = default(Bezier3);
				bezier.a = nodesBuffer[segment.m_startNode].m_position;
				bezier.d = nodesBuffer[segment.m_endNode].m_position;
				NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, 
				                                 bezier.d, segment.m_endDirection, 
				                                 false, false, 
				                                 out bezier.b, out bezier.c);
				var isStartNode = SelectedNodeId == segment.m_startNode;
				var tangent = bezier.Tangent(isStartNode ? 0f : 1f);

				// Build a short tangent 3d vector from the selected node towards the calculated tangent vec3
				Vector3 tangentScreen;
				MainTool.WorldToScreenPoint(nodeWorldPos + tangent.normalized * 10f, out tangentScreen);
				tangentScreen -= nodeScreenPos;
				if (isStartNode) {
					// For start node flip the direction, so that the GUI looks right
					tangentScreen *= -1;
				}
				rotateDegrees = 90f + Mathf.Atan2(tangentScreen.y, tangentScreen.x) * Mathf.Rad2Deg;
				Log.Info($"tang={tangent} ts={tangentScreen} ang={rotateDegrees}");
			}

			var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
			var diff = nodeWorldPos - camPos;

			if (diff.magnitude > TrafficManagerTool.MaxOverlayDistance)
				return; // do not draw if too distant

			const int GUI_LANE_WIDTH = 128;
			int width = numLanes * GUI_LANE_WIDTH;
			var windowRect3 = new Rect(nodeScreenPos.x - width / 2, nodeScreenPos.y - 70, width, 50);

			// Save the GUI rotation, rotate the GUI along the segment + 90°, and restore then
			var oldMatrix = GUI.matrix;
			GUIUtility.RotateAroundPivot(rotateDegrees, windowRect3.center);
			GUILayout.Window(250, windowRect3, _guiLaneChangeWindow, string.Empty, BorderlessStyle);
			GUI.matrix = oldMatrix;

			// Rotate the mouse in opposite direction and check whether it is inside the rotated window frame
			var mousePosRotated = rotateVector2_(Event.current.mousePosition, windowRect3.center, -rotateDegrees);
			_cursorInSecondaryPanel = windowRect3.Contains(mousePosRotated);
		}

		private Vector2 rotateVector2_(Vector2 v, Vector2 center, float degrees) {
			v -= center;
			var sin = Mathf.Sin(degrees * Mathf.Deg2Rad);
			var cos = Mathf.Cos(degrees * Mathf.Deg2Rad);
			var tx = v.x;
			var ty = v.y;
			v.x = (cos * tx) - (sin * ty);
			v.y = (sin * tx) + (cos * ty);
			return v + center;
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			NetManager netManager = Singleton<NetManager>.instance;
			//Log._Debug($"LaneArrow Overlay: {HoveredNodeId} {HoveredSegmentId} {SelectedNodeId} {SelectedSegmentId}");
			if (!_cursorInSecondaryPanel && HoveredSegmentId != 0 && HoveredNodeId != 0 && (HoveredSegmentId != SelectedSegmentId || HoveredNodeId != SelectedNodeId)) {
				var nodeFlags = netManager.m_nodes.m_buffer[HoveredNodeId].m_flags;
				
				if ((netManager.m_segments.m_buffer[HoveredSegmentId].m_startNode == HoveredNodeId || netManager.m_segments.m_buffer[HoveredSegmentId].m_endNode == HoveredNodeId) && (nodeFlags & NetNode.Flags.Junction) != NetNode.Flags.None) {
					NetTool.RenderOverlay(cameraInfo, ref Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId], MainTool.GetToolColor(false, false),
						MainTool.GetToolColor(false, false));
				}
			}

			if (SelectedSegmentId == 0) return;

			NetTool.RenderOverlay(cameraInfo, ref Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId], MainTool.GetToolColor(true, false), MainTool.GetToolColor(true, false));
		}

		private void _guiLaneChangeWindow(int num) {
			var info = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId].Info;

			IList<LanePos> laneList = Constants.ServiceFactory.NetService.GetSortedLanes(SelectedSegmentId, ref Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId], Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId].m_startNode == SelectedNodeId, LaneArrowManager.LANE_TYPES, LaneArrowManager.VEHICLE_TYPES, true);
			SegmentGeometry geometry = SegmentGeometry.Get(SelectedSegmentId);
			if (geometry == null) {
				Log.Error($"LaneArrowTool._guiLaneChangeWindow: No geometry information available for segment {SelectedSegmentId}");
				return;
			}
			bool startNode = geometry.StartNodeId() == SelectedNodeId;

			GUILayout.BeginHorizontal();

			for (var i = 0; i < laneList.Count; i++) {
				var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneList[i].laneId].m_flags;

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
				if (!Flags.applyLaneArrowFlags(laneList[i].laneId)) {
					Flags.removeLaneArrowFlags(laneList[i].laneId);
				}
				Flags.LaneArrowChangeResult res = Flags.LaneArrowChangeResult.Invalid;
				bool buttonClicked = false;
				if (GUILayout.Button("←", ((flags & NetLane.Flags.Left) == NetLane.Flags.Left ? style1 : style2), GUILayout.Width(35), GUILayout.Height(25))) {
					buttonClicked = true;
					LaneArrowManager.Instance.ToggleLaneArrows(laneList[i].laneId, startNode, Flags.LaneArrows.Left, out res);
				}
				if (GUILayout.Button("↑", ((flags & NetLane.Flags.Forward) == NetLane.Flags.Forward ? style1 : style2), GUILayout.Width(25), GUILayout.Height(35))) {
					buttonClicked = true;
					LaneArrowManager.Instance.ToggleLaneArrows(laneList[i].laneId, startNode, Flags.LaneArrows.Forward, out res);
				}
				if (GUILayout.Button("→", ((flags & NetLane.Flags.Right) == NetLane.Flags.Right ? style1 : style2), GUILayout.Width(35), GUILayout.Height(25))) {
					buttonClicked = true;
					LaneArrowManager.Instance.ToggleLaneArrows(laneList[i].laneId, startNode, Flags.LaneArrows.Right, out res);
				}

				if (buttonClicked) {
					switch (res) {
						case Flags.LaneArrowChangeResult.Invalid:
						case Flags.LaneArrowChangeResult.Success:
						default:
							break;
						case Flags.LaneArrowChangeResult.HighwayArrows:
							MainTool.ShowTooltip(Translation.GetString("Lane_Arrow_Changer_Disabled_Highway"));
							break;
						case Flags.LaneArrowChangeResult.LaneConnection:
							MainTool.ShowTooltip(Translation.GetString("Lane_Arrow_Changer_Disabled_Connection"));
							break;
					}
				}

				GUILayout.EndHorizontal();
				GUILayout.EndVertical();
				GUILayout.EndVertical();
			}

			GUILayout.EndHorizontal();
		}
	}
}
