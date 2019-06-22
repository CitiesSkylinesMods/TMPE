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
using TrafficManager.TrafficLight;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.UI.SubTools {
	public class LaneArrowTool : SubTool {
		/// <summary>
		/// Arrow sign bits are combined together to find index in turn arrow textures
		/// located in TextureResources.TurnSignTextures.
		/// </summary>
		public const int SIGN_BIT_LEFT = 1;
		public const int SIGN_BIT_FORWARD = 2;
		public const int SIGN_BIT_RIGHT = 4;

		/// <summary>Size for white turn sign in the GUI</summary>
		private const float BUTTON_GUI_SCALE = 50f;

		/// <summary>
		/// Sum of widths of GUI elements for 1 lane.
		/// NOTE this also adds spacing between lane columns.
		/// </summary>
		private const float LANE_GUI_WIDTH = BUTTON_GUI_SCALE * 1.33f + (2f * LANE_LABEL_OFFSET_X);
		private const float GUI_HEIGHT = 24f + BUTTON_GUI_SCALE * 1.66f; // label size + buttons

		/// <summary>The horizontal offset for "Lane #" text in each column</summary>
		private const float LANE_LABEL_OFFSET_X = 36f;

		private bool _cursorInSecondaryPanel;

		public LaneArrowTool(TrafficManagerTool mainTool) : base(mainTool) {
			
		}

		public override bool IsCursorInPanel() {
			return base.IsCursorInPanel() || _cursorInSecondaryPanel;
		}

		public override void OnPrimaryClickOverlay() {
			if (HoveredNodeId == 0 || HoveredSegmentId == 0) {
				return;
			}

			var netFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags;
			if ((netFlags & NetNode.Flags.Junction) == NetNode.Flags.None) {
				return;
			}

			var hoveredSegment = Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId];
			if (hoveredSegment.m_startNode != HoveredNodeId &&
				hoveredSegment.m_endNode != HoveredNodeId) {
				return;
			}

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

			Vector3 nodePos = Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId].m_position;

			Vector3 screenPos;
			bool visible = MainTool.WorldToScreenPoint(nodePos, out screenPos);

			if (!visible)
				return;

			var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
			var diff = nodePos - camPos;

			if (diff.magnitude > TrafficManagerTool.MaxOverlayDistance)
				return; // do not draw if too distant

			int width = numLanes * (int)LANE_GUI_WIDTH;
			var proposedWindowRect = new Rect(screenPos.x - width / 2, screenPos.y - 70, width, GUI_HEIGHT);
			var actualWindowRect = GUILayout.Window(250, proposedWindowRect, _guiLaneChangeWindow, "", BorderlessStyle);
			_cursorInSecondaryPanel = actualWindowRect.Contains(Event.current.mousePosition);
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			NetManager netManager = Singleton<NetManager>.instance;
			//Log._Debug($"LaneArrow Overlay: {HoveredNodeId} {HoveredSegmentId} {SelectedNodeId} {SelectedSegmentId}");
			if (!_cursorInSecondaryPanel 
			    && HoveredSegmentId != 0 
			    && HoveredNodeId != 0 
			    && (HoveredSegmentId != SelectedSegmentId || HoveredNodeId != SelectedNodeId)) {
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

				var laneStyle = new GUIStyle { contentOffset = new Vector2(12f, 0f) };

				var laneTitleStyle = new GUIStyle {
					contentOffset = new Vector2(LANE_LABEL_OFFSET_X, 2f),
					normal = { textColor = new Color(1f, 1f, 1f) }
				};

				GUILayout.BeginVertical(laneStyle);
				GUILayout.Label(Translation.GetString("Lane") + " " + (i + 1), laneTitleStyle);

				//----------------------
				// Button group
				//----------------------
				GUILayout.BeginVertical();
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (!Flags.applyLaneArrowFlags(laneList[i].laneId)) {
					Flags.removeLaneArrowFlags(laneList[i].laneId);
				}
				Flags.LaneArrowChangeResult res = Flags.LaneArrowChangeResult.Invalid;

				bool buttonClicked = false;
				var isLeft = (flags & NetLane.Flags.Left) == NetLane.Flags.Left;
				var isForward = (flags & NetLane.Flags.Forward) == NetLane.Flags.Forward;
				var isRight = (flags & NetLane.Flags.Right) == NetLane.Flags.Right;

				if (GUILayout.Button(isForward ? TextureResources.TurnButtonForward : TextureResources.TurnButtonForwardGray, 
				                     GUILayout.Width(BUTTON_GUI_SCALE * 1.33f), 
				                     GUILayout.Height(BUTTON_GUI_SCALE * 0.66f))) {
					buttonClicked = true;
					LaneArrowManager.Instance.ToggleLaneArrows(laneList[i].laneId, startNode, 
					                                           Flags.LaneArrows.Forward, out res);
				}
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				//----------------------
				// Arrow sign row
				//----------------------
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (GUILayout.Button(isLeft ? TextureResources.TurnButtonLeft : TextureResources.TurnButtonLeftGray, 
				                     GUILayout.Width(BUTTON_GUI_SCALE * 0.66f),
				                     GUILayout.Height(BUTTON_GUI_SCALE))) {
					buttonClicked = true;
					LaneArrowManager.Instance.ToggleLaneArrows(laneList[i].laneId, startNode,
					                                           Flags.LaneArrows.Left, out res);
				}
				if (GUILayout.Button(isRight ? TextureResources.TurnButtonRight : TextureResources.TurnButtonRightGray, 
						     GUILayout.Width(BUTTON_GUI_SCALE * 0.66f),
						     GUILayout.Height(BUTTON_GUI_SCALE))) {
					buttonClicked = true;
					LaneArrowManager.Instance.ToggleLaneArrows(laneList[i].laneId, startNode,
					                                           Flags.LaneArrows.Right, out res);
				}
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				//----------------------
				// Button click handling
				//----------------------
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

				GUILayout.EndVertical();
				GUILayout.EndVertical();
			}

			GUILayout.EndHorizontal();
		}
	}
}
