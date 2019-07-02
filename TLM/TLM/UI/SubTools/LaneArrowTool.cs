﻿using ColossalFramework;
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
using TrafficManager.TrafficLight;
using TrafficManager.UI.CanvasGUI;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.UI.SubTools {
	public class LaneArrowTool : SubTool {
		private static WorldSpaceUI wsUi = null;
		
		private bool _cursorInSecondaryPanel;

		public LaneArrowTool(TrafficManagerTool mainTool)
			: base(mainTool) {
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

			Deselect();
			SelectedSegmentId = HoveredSegmentId;
			SelectedNodeId = HoveredNodeId;
		}

		public override void OnSecondaryClickOverlay() {
			if (!IsCursorInPanel()) {
				Deselect();
			}
		}

		public override void OnToolGUI(Event e) {
			//base.OnToolGUI(e);
			_cursorInSecondaryPanel = false;

			if (SelectedNodeId == 0 || SelectedSegmentId == 0) return;

			int numDirections;
			int numLanes = TrafficManagerTool.GetSegmentNumVehicleLanes(SelectedSegmentId, SelectedNodeId, out numDirections, LaneArrowManager.VEHICLE_TYPES);
			if (numLanes <= 0) {
				Deselect();
				return;
			}

			Vector3 nodePos = Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId].m_position;

			Vector3 screenPos;
			bool visible = MainTool.WorldToScreenPoint(nodePos, out screenPos);

			if (!visible)
				return;

			var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
			var diff = nodePos - camPos;

			if (diff.magnitude > TrafficManagerTool.MaxOverlayDistance)
				return; // do not draw if too distant

			int width = numLanes * 128;
			var windowRect3 = new Rect(screenPos.x - width / 2, screenPos.y - 70, width, 50);
			GUILayout.Window(250, windowRect3, _guiLaneChangeWindow, "", BorderlessStyle);
			_cursorInSecondaryPanel = windowRect3.Contains(Event.current.mousePosition);
		}

		private void Deselect() {
			SelectedSegmentId = 0;
			SelectedNodeId = 0;
			if (wsUi != null) {
				wsUi.DestroyCanvas();
				wsUi = null;
			}
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

			var netSegment = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId];
			NetTool.RenderOverlay(cameraInfo, ref netSegment, MainTool.GetToolColor(true, false), MainTool.GetToolColor(true, false));

			// Create UI on the ground
			if (wsUi == null) {
				var nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;

				// Make the canvas be aligned with the segment and looking UP
				var otherNodeId = SelectedNodeId == netSegment.m_startNode
					                  ? netSegment.m_endNode
					                  : SelectedNodeId;
				var forward = nodesBuffer[SelectedNodeId].m_position - nodesBuffer[otherNodeId].m_position;
				forward.y = 0f; // make strictly flat
				// var lookRotation = Quaternion.LookRotation(Vector3.down, forward.normalized);
				var lookRotation = Quaternion.Euler(90f, 0f, 0f);
				Log.Info(lookRotation.ToString());
				wsUi = new WorldSpaceUI(nodesBuffer[SelectedNodeId].m_position, lookRotation);

				// Create button on the ground 10m by 5m
				wsUi.AddButton(Vector3.zero, new Vector2(10f, 5f), "Forward");
				wsUi.AddText(new Vector3(0, -5f, 0f), new Vector2(40f, 10f), "Hello text");
			}
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
