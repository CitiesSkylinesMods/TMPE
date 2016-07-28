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
	public class SpeedLimitsTool : SubTool {
		private bool _cursorInSecondaryPanel;
		private int curSpeedLimitIndex = 0;
		private bool overlayHandleHovered;
		private Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>> segmentCenterByDir = new Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>>();
		private readonly float speedLimitSignSize = 80f;
		private Texture2D SecondPanelTexture;
		private Rect windowRect = TrafficManagerTool.MoveGUI(new Rect(0, 0, 7 * 105, 210));
		private HashSet<ushort> currentlyVisibleSegmentIds;

		public SpeedLimitsTool(TrafficManagerTool mainTool) : base(mainTool) {
			SecondPanelTexture = TrafficManagerTool.MakeTex(1, 1, new Color(0.5f, 0.5f, 0.5f, 1f));
			currentlyVisibleSegmentIds = new HashSet<ushort>();
		}

		public override bool IsCursorInPanel() {
			return _cursorInSecondaryPanel;
		}

		public override void OnPrimaryClickOverlay() {
			
		}

		public override void OnToolGUI(Event e) {
			_cursorInSecondaryPanel = false;

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

			GUILayout.Window(254, windowRect, _guiSpeedLimitsWindow, Translation.GetString("Speed_limits"), style);
			_cursorInSecondaryPanel = windowRect.Contains(Event.current.mousePosition);
			overlayHandleHovered = false;
			ShowSigns(false);
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			
		}

		public override void ShowGUIOverlay() {
			ShowSigns(true);
		}

		public override void Cleanup() {
			segmentCenterByDir.Clear();
			currentlyVisibleSegmentIds.Clear();
			lastCamPos = null;
			lastCamRot = null;
		}

		private Quaternion? lastCamRot = null;
		private Vector3? lastCamPos = null;

		private void ShowSigns(bool viewOnly) {
			if (viewOnly && !Options.speedLimitsOverlay)
				return;

			Quaternion camRot = Camera.main.transform.rotation;
			Vector3 camPos = Camera.main.transform.position;

			NetManager netManager = Singleton<NetManager>.instance;

			if (lastCamPos == null || lastCamRot == null || !lastCamRot.Equals(camRot) || !lastCamPos.Equals(camPos)) {
				// cache visible segments
				currentlyVisibleSegmentIds.Clear();

				for (ushort segmentId = 1; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
					if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
						continue;
					if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
						continue;

					if ((netManager.m_segments.m_buffer[segmentId].m_bounds.center - camPos).magnitude > TrafficManagerTool.PriorityCloseLod)
						continue; // do not draw if too distant

					Vector3 screenPos = Camera.main.WorldToScreenPoint(netManager.m_segments.m_buffer[segmentId].m_bounds.center);
					if (screenPos.z < 0)
						continue;

					ItemClass connectionClass = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.GetConnectionClass();
					if (!(connectionClass.m_service == ItemClass.Service.Road ||
						(connectionClass.m_service == ItemClass.Service.PublicTransport && connectionClass.m_subService == ItemClass.SubService.PublicTransportTrain)))
						continue;

					currentlyVisibleSegmentIds.Add(segmentId);
				}

				lastCamPos = camPos;
				lastCamRot = camRot;
			}

			bool handleHovered = false;
			foreach (ushort segmentId in currentlyVisibleSegmentIds) {
				Vector3 screenPos = Camera.main.WorldToScreenPoint(netManager.m_segments.m_buffer[segmentId].m_bounds.center);
				screenPos.y = Screen.height - screenPos.y;

				NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;

				// draw speed limits
				if (TrafficManagerTool.GetToolMode() != ToolMode.VehicleRestrictions || segmentId != SelectedSegmentId) { // no speed limit overlay on selected segment when in vehicle restrictions mode
					if (drawSpeedLimitHandles((ushort)segmentId, viewOnly, ref camPos))
						handleHovered = true;
				}
			}
			overlayHandleHovered = handleHovered;
		}

		private void _guiSpeedLimitsWindow(int num) {
			GUILayout.BeginHorizontal();

			Color oldColor = GUI.color;
			for (int i = 0; i < SpeedLimitManager.Instance().AvailableSpeedLimits.Count; ++i) {
				if (curSpeedLimitIndex != i)
					GUI.color = Color.gray;
				float signSize = TrafficManagerTool.AdaptWidth(100);
				if (GUILayout.Button(TrafficLightToolTextureResources.SpeedLimitTextures[SpeedLimitManager.Instance().AvailableSpeedLimits[i]], GUILayout.Width(signSize), GUILayout.Height(signSize))) {
					curSpeedLimitIndex = i;
				}
				GUI.color = oldColor;

				if (i == 6) {
					GUILayout.EndHorizontal();
					GUILayout.BeginHorizontal();
				}
			}

			GUILayout.EndHorizontal();
		}

		private bool drawSpeedLimitHandles(ushort segmentId, bool viewOnly, ref Vector3 camPos) {
			if (!LoadingExtension.IsPathManagerCompatible) {
				return false;
			}

			if (viewOnly && !Options.speedLimitsOverlay)
				return false;

			// draw speedlimits over mean middle points of lane beziers
			if (!segmentCenterByDir.ContainsKey(segmentId)) {
				segmentCenterByDir.Add(segmentId, new Dictionary<NetInfo.Direction, Vector3>());
				TrafficManagerTool.CalculateSegmentCenterByDir(segmentId, segmentCenterByDir[segmentId]);
			}

			bool hovered = false;
			foreach (KeyValuePair<NetInfo.Direction, Vector3> e in segmentCenterByDir[segmentId]) {
				var screenPos = Camera.main.WorldToScreenPoint(e.Value);
				screenPos.y = Screen.height - screenPos.y;

				float zoom = 1.0f / (e.Value - camPos).magnitude * 100f * MainTool.GetBaseZoom();
				float size = speedLimitSignSize * zoom;
				Color guiColor = GUI.color;
				Rect boundingBox = new Rect(screenPos.x - size / 2, screenPos.y - size / 2, size, size);
				bool hoveredHandle = !viewOnly && TrafficManagerTool.IsMouseOver(boundingBox);

				if (hoveredHandle) {
					// mouse hovering over sign
					hovered = true;
					guiColor.a = 0.8f;
				} else {
					guiColor.a = 0.5f;
				}

				GUI.color = guiColor;

				try {
					GUI.DrawTexture(boundingBox, TrafficLightToolTextureResources.SpeedLimitTextures[SpeedLimitManager.Instance().GetCustomSpeedLimit(segmentId, e.Key)]);
				} catch (Exception ex) {
					Log.Error("segment " + segmentId + " limit: " + SpeedLimitManager.Instance().GetCustomSpeedLimit(segmentId, e.Key) + ", ex: " + ex.ToString());
				}

				if (hoveredHandle && Input.GetMouseButton(0)) {
					// change the speed limit to the selected one
					ushort speedLimitToSet = SpeedLimitManager.Instance().AvailableSpeedLimits[curSpeedLimitIndex];
					//Log._Debug($"Setting speed limit of segment {segmentId}, dir {e.Key.ToString()} to {speedLimitToSet}");
					SpeedLimitManager.Instance().SetSpeedLimit(segmentId, e.Key, speedLimitToSet);

					// TODO use SegmentTraverser
					if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
						// apply new speed limit to connected segments
						NetInfo selectedSegmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
						List<object[]> selectedSortedLanes = TrafficManagerTool.GetSortedVehicleLanes(segmentId, selectedSegmentInfo, null);

						LinkedList<ushort> nodesToProcess = new LinkedList<ushort>();
						HashSet<ushort> processedNodes = new HashSet<ushort>();
						HashSet<ushort> processedSegments = new HashSet<ushort>();
						processedSegments.Add(SelectedSegmentId);

						ushort selectedStartNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startNode;
						ushort selectedEndNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endNode;

						if (selectedStartNodeId != 0)
							nodesToProcess.AddFirst(selectedStartNodeId);
						if (selectedEndNodeId != 0)
							nodesToProcess.AddFirst(selectedEndNodeId);

						while (nodesToProcess.First != null) {
							ushort nodeId = nodesToProcess.First.Value;
							nodesToProcess.RemoveFirst();
							processedNodes.Add(nodeId);

							if (Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].CountSegments() > 2)
								continue; // junction. stop.

							// explore segments at node
							for (var s = 0; s < 8; s++) {
								var otherSegmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(s);

								if (otherSegmentId <= 0 || processedSegments.Contains(otherSegmentId))
									continue;
								processedSegments.Add(otherSegmentId);

								NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].Info;
								List<object[]> sortedLanes = TrafficManagerTool.GetSortedVehicleLanes(otherSegmentId, segmentInfo, null);

								if (sortedLanes.Count == selectedSortedLanes.Count) {
									// number of lanes matches selected segment
									for (int i = 0; i < sortedLanes.Count; ++i) {
										object[] selectedLaneData = selectedSortedLanes[i];
										object[] laneData = sortedLanes[i];

										uint selectedLaneId = (uint)selectedLaneData[0];
										uint selectedLaneIndex = (uint)selectedLaneData[2];
										NetInfo.Lane selectedLaneInfo = segmentInfo.m_lanes[selectedLaneIndex];

										uint laneId = (uint)laneData[0];
										uint laneIndex = (uint)laneData[2];
										NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

										if (laneInfo.m_finalDirection == e.Key) {
											SpeedLimitManager.Instance().SetSpeedLimit(otherSegmentId, laneInfo.m_finalDirection, speedLimitToSet);
										}

										// apply restrictions of selected segment & lane
										//VehicleRestrictionsManager.SetAllowedVehicleTypes(otherSegmentId, segmentInfo, laneIndex, laneInfo, laneId, VehicleRestrictionsManager.GetAllowedVehicleTypes(SelectedSegmentId, selectedSegmentInfo, selectedLaneIndex, selectedLaneInfo));
									}

									// add nodes to explore
									ushort startNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].m_startNode;
									ushort endNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].m_endNode;

									if (startNodeId != 0 && !processedNodes.Contains(startNodeId))
										nodesToProcess.AddFirst(startNodeId);
									if (endNodeId != 0 && !processedNodes.Contains(endNodeId))
										nodesToProcess.AddFirst(endNodeId);
								}
							}
						}
					}
					//mouseClickProcessed = true;
				}

				guiColor.a = 1f;
				GUI.color = guiColor;
			}
			return hovered;
		}
	}
}
