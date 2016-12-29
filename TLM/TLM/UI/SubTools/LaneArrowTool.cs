using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using UnityEngine;
using TrafficManager.Manager;

namespace TrafficManager.UI.SubTools {
	public class LaneArrowTool : SubTool {
		private bool _cursorInSecondaryPanel;
		private Texture2D SecondPanelTexture;

		public LaneArrowTool(TrafficManagerTool mainTool) : base(mainTool) {
			SecondPanelTexture = TrafficManagerTool.MakeTex(1, 1, new Color(0.5f, 0.5f, 0.5f, 1f));
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
			int numLanes = TrafficManagerTool.GetSegmentNumVehicleLanes(SelectedSegmentId, SelectedNodeId, out numDirections, VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Metro); // TODO refactor vehicle mask
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

			int width = numLanes * 128;
			var windowRect3 = new Rect(screenPos.x - width / 2, screenPos.y - 70, width, 50);
			GUILayout.Window(250, windowRect3, _guiLaneChangeWindow, "", style);
			_cursorInSecondaryPanel = windowRect3.Contains(Event.current.mousePosition);
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			//Log._Debug($"LaneArrow Overlay: {HoveredNodeId} {HoveredSegmentId} {SelectedNodeId} {SelectedSegmentId}");
			if (!_cursorInSecondaryPanel && HoveredSegmentId != 0 && HoveredNodeId != 0 && (HoveredSegmentId != SelectedSegmentId || HoveredNodeId != SelectedNodeId)) {
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

			List<object[]> laneList = TrafficManagerTool.GetSortedVehicleLanes(SelectedSegmentId, info, SelectedNodeId, VehicleInfo.VehicleType.Car);
			SegmentGeometry geometry = SegmentGeometry.Get(SelectedSegmentId);
			bool startNode = geometry.StartNodeId() == SelectedNodeId;

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
				Flags.LaneArrowChangeResult res = Flags.LaneArrowChangeResult.Invalid;
				bool buttonClicked = false;
				if (GUILayout.Button("←", ((flags & NetLane.Flags.Left) == NetLane.Flags.Left ? style1 : style2), GUILayout.Width(35), GUILayout.Height(25))) {
					buttonClicked = true;
					LaneArrowManager.Instance.ToggleLaneArrows((uint)laneList[i][0], startNode, Flags.LaneArrows.Left, out res);
				}
				if (GUILayout.Button("↑", ((flags & NetLane.Flags.Forward) == NetLane.Flags.Forward ? style1 : style2), GUILayout.Width(25), GUILayout.Height(35))) {
					buttonClicked = true;
					LaneArrowManager.Instance.ToggleLaneArrows((uint)laneList[i][0], startNode, Flags.LaneArrows.Forward, out res);
				}
				if (GUILayout.Button("→", ((flags & NetLane.Flags.Right) == NetLane.Flags.Right ? style1 : style2), GUILayout.Width(35), GUILayout.Height(25))) {
					buttonClicked = true;
					LaneArrowManager.Instance.ToggleLaneArrows((uint)laneList[i][0], startNode, Flags.LaneArrows.Right, out res);
				}

				if (buttonClicked) {
					switch (res) {
						case Flags.LaneArrowChangeResult.Invalid:
						case Flags.LaneArrowChangeResult.Success:
						default:
							break;
						case Flags.LaneArrowChangeResult.HighwayArrows:
							MainTool.ShowTooltip(Translation.GetString("Lane_Arrow_Changer_Disabled_Highway"), Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId].m_position);
							break;
						case Flags.LaneArrowChangeResult.LaneConnection:
							MainTool.ShowTooltip(Translation.GetString("Lane_Arrow_Changer_Disabled_Connection"), Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId].m_position);
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
