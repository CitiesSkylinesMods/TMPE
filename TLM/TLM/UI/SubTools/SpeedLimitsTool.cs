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
using ColossalFramework.UI;
using static ColossalFramework.UI.UITextureAtlas;

namespace TrafficManager.UI.SubTools {
	public class SpeedLimitsTool : SubTool {
		private bool _cursorInSecondaryPanel;
		private int curSpeedLimitIndex = 0;
		private bool overlayHandleHovered;
		private Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>> segmentCenterByDir = new Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>>();
		private readonly float speedLimitSignSize = 80f;
		private readonly int guiSpeedSignSize = 100;
		private Texture2D SecondPanelTexture;
		private Rect windowRect = TrafficManagerTool.MoveGUI(new Rect(0, 0, 7 * 105, 210));
		private Rect defaultsWindowRect = TrafficManagerTool.MoveGUI(new Rect(0, 280, 400, 400));
		private HashSet<ushort> currentlyVisibleSegmentIds;
		private bool defaultsWindowVisible = false;
		private int currentInfoIndex = -1;
		private int currentSpeedLimitIndex = -1;
		private Texture2D roadTex;

		public SpeedLimitsTool(TrafficManagerTool mainTool) : base(mainTool) {
			SecondPanelTexture = TrafficManagerTool.MakeTex(1, 1, new Color(0.5f, 0.5f, 0.5f, 1f));
			currentlyVisibleSegmentIds = new HashSet<ushort>();
			roadTex = new Texture2D(guiSpeedSignSize, guiSpeedSignSize);
		}

		public override bool IsCursorInPanel() {
			return base.IsCursorInPanel() || _cursorInSecondaryPanel;
		}

		public override void OnActivate() {
			
		}

		public override void OnPrimaryClickOverlay() {
			
		}

		public override void OnToolGUI(Event e) {
			base.OnToolGUI(e);

			windowRect = GUILayout.Window(254, windowRect, _guiSpeedLimitsWindow, Translation.GetString("Speed_limits"));
			if (defaultsWindowVisible) {
				defaultsWindowRect = GUILayout.Window(258, defaultsWindowRect, _guiDefaultsWindow, Translation.GetString("Default_speed_limits"));
			}
			_cursorInSecondaryPanel = windowRect.Contains(Event.current.mousePosition) || (defaultsWindowVisible && defaultsWindowRect.Contains(Event.current.mousePosition));

			//overlayHandleHovered = false;
			//ShowSigns(false);
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			
		}

		public override void ShowGUIOverlay(bool viewOnly) {
			if (viewOnly && !Options.speedLimitsOverlay)
				return;

			overlayHandleHovered = false;
			ShowSigns(viewOnly);
		}

		public override void Cleanup() {
			segmentCenterByDir.Clear();
			currentlyVisibleSegmentIds.Clear();
			lastCamPos = null;
			lastCamRot = null;
			currentInfoIndex = -1;
			currentSpeedLimitIndex = -1;
		}

		private Quaternion? lastCamRot = null;
		private Vector3? lastCamPos = null;

		private void ShowSigns(bool viewOnly) {
			Quaternion camRot = Camera.main.transform.rotation;
			Vector3 camPos = Camera.main.transform.position;

			NetManager netManager = Singleton<NetManager>.instance;
			SpeedLimitManager speedLimitManager = SpeedLimitManager.Instance;

			if (lastCamPos == null || lastCamRot == null || !lastCamRot.Equals(camRot) || !lastCamPos.Equals(camPos)) {
				// cache visible segments
				currentlyVisibleSegmentIds.Clear();

				for (uint segmentId = 1; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
					if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
						continue;
					/*if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
						continue;*/

					if ((netManager.m_segments.m_buffer[segmentId].m_bounds.center - camPos).magnitude > TrafficManagerTool.PriorityCloseLod)
						continue; // do not draw if too distant

					Vector3 screenPos = Camera.main.WorldToScreenPoint(netManager.m_segments.m_buffer[segmentId].m_bounds.center);
					if (screenPos.z < 0)
						continue;

					if (!speedLimitManager.MayHaveCustomSpeedLimits((ushort)segmentId, ref netManager.m_segments.m_buffer[segmentId]))
						continue;

					currentlyVisibleSegmentIds.Add((ushort)segmentId);
				}

				lastCamPos = camPos;
				lastCamRot = camRot;
			}

			bool handleHovered = false;
			foreach (ushort segmentId in currentlyVisibleSegmentIds) {
				Vector3 screenPos = Camera.main.WorldToScreenPoint(netManager.m_segments.m_buffer[segmentId].m_bounds.center);
				screenPos.y = Screen.height - screenPos.y;
				if (screenPos.z < 0)
					continue;

				NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;

				// draw speed limits
				if (TrafficManagerTool.GetToolMode() != ToolMode.VehicleRestrictions || segmentId != SelectedSegmentId) { // no speed limit overlay on selected segment when in vehicle restrictions mode
					if (drawSpeedLimitHandles((ushort)segmentId, viewOnly, ref camPos))
						handleHovered = true;
				}
			}
			overlayHandleHovered = handleHovered;
		}

		private void _guiDefaultsWindow(int num) {
			List<NetInfo> mainNetInfos = SpeedLimitManager.Instance.GetCustomizableNetInfos();

			if (mainNetInfos == null || mainNetInfos.Count <= 0) {
				Log._Debug($"mainNetInfos={mainNetInfos?.Count}");
				DragWindow(ref defaultsWindowRect);
				return;
			}

			bool updateRoadTex = false;
			if (currentInfoIndex < 0 || currentInfoIndex >= mainNetInfos.Count) {
				currentInfoIndex = 0;
				updateRoadTex = true;
				Log._Debug($"set currentInfoIndex to 0");
			}

			NetInfo info = mainNetInfos[currentInfoIndex];
			if (updateRoadTex)
				UpdateRoadTex(info);

			if (currentSpeedLimitIndex < 0) {
				currentSpeedLimitIndex = SpeedLimitManager.Instance.GetCustomNetInfoSpeedLimitIndex(info);
				Log._Debug($"set currentSpeedLimitIndex to {currentSpeedLimitIndex}");
			}
			//Log._Debug($"currentInfoIndex={currentInfoIndex} currentSpeedLimitIndex={currentSpeedLimitIndex}");

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("X", GUILayout.Width(25))) {
				defaultsWindowVisible = false;
			}
			GUILayout.EndHorizontal();

			// Road type label
			GUILayout.BeginVertical();
			GUILayout.Space(10);
			GUILayout.Label(Translation.GetString("Road_type") + ":");
			GUILayout.EndVertical();

			// switch between NetInfos
			GUILayout.BeginHorizontal();
			
			GUILayout.BeginVertical();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("←", GUILayout.Width(50))) {
				currentInfoIndex = (currentInfoIndex + mainNetInfos.Count - 1) % mainNetInfos.Count;
				info = mainNetInfos[currentInfoIndex];
				currentSpeedLimitIndex = SpeedLimitManager.Instance.GetCustomNetInfoSpeedLimitIndex(info);
				UpdateRoadTex(info);
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();

			GUILayout.FlexibleSpace();
			GUILayout.BeginVertical();
			GUILayout.FlexibleSpace();

			// NetInfo thumbnail
			GUILayout.Box(roadTex, GUILayout.Height(guiSpeedSignSize));
			GUILayout.FlexibleSpace();

			GUILayout.EndVertical();
			GUILayout.FlexibleSpace();

			GUILayout.BeginVertical();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("→", GUILayout.Width(50))) {
				currentInfoIndex = (currentInfoIndex + 1) % mainNetInfos.Count;
				info = mainNetInfos[currentInfoIndex];
				currentSpeedLimitIndex = SpeedLimitManager.Instance.GetCustomNetInfoSpeedLimitIndex(info);
				UpdateRoadTex(info);
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();

			GUILayout.EndHorizontal();

			GUIStyle centeredTextStyle = new GUIStyle("label");
			centeredTextStyle.alignment = TextAnchor.MiddleCenter;

			// NetInfo name
			GUILayout.Label(info.name, centeredTextStyle);

			// Default speed limit label
			GUILayout.BeginVertical();
			GUILayout.Space(10);
			GUILayout.Label(Translation.GetString("Default_speed_limit") + ":");
			GUILayout.EndVertical();

			// switch between speed limits
			GUILayout.BeginHorizontal();

			GUILayout.BeginVertical();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("←", GUILayout.Width(50))) {
				currentSpeedLimitIndex = (currentSpeedLimitIndex + SpeedLimitManager.Instance.AvailableSpeedLimits.Count - 1) % SpeedLimitManager.Instance.AvailableSpeedLimits.Count;
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();

			GUILayout.FlexibleSpace();

			GUILayout.BeginVertical();
			GUILayout.FlexibleSpace();

			// speed limit sign
			GUILayout.Box(TrafficLightToolTextureResources.SpeedLimitTextures[SpeedLimitManager.Instance.AvailableSpeedLimits[currentSpeedLimitIndex]], GUILayout.Width(guiSpeedSignSize), GUILayout.Height(guiSpeedSignSize));

			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();

			GUILayout.FlexibleSpace();

			GUILayout.BeginVertical();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("→", GUILayout.Width(50))) {
				currentSpeedLimitIndex = (currentSpeedLimitIndex + 1) % SpeedLimitManager.Instance.AvailableSpeedLimits.Count;
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();

			GUILayout.EndHorizontal();

			// Save & Apply
			GUILayout.BeginVertical();
			GUILayout.Space(10);

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(Translation.GetString("Save"), GUILayout.Width(70))) {
				SpeedLimitManager.Instance.FixCurrentSpeedLimits(info);
				SpeedLimitManager.Instance.SetCustomNetInfoSpeedLimitIndex(info, currentSpeedLimitIndex);
			}
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(Translation.GetString("Save") + " & " + Translation.GetString("Apply"), GUILayout.Width(120))) {
				SpeedLimitManager.Instance.SetCustomNetInfoSpeedLimitIndex(info, currentSpeedLimitIndex);
				SpeedLimitManager.Instance.ClearCurrentSpeedLimits(info);
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			GUILayout.EndVertical();

			DragWindow(ref defaultsWindowRect);
		}

		private void UpdateRoadTex(NetInfo info) {
			if (info != null) {
				if (info.m_Atlas != null && info.m_Atlas.material != null && info.m_Atlas.material.mainTexture != null && info.m_Atlas.material.mainTexture is Texture2D) {
					Texture2D mainTex = (Texture2D)info.m_Atlas.material.mainTexture;
					SpriteInfo spriteInfo = info.m_Atlas[info.m_Thumbnail];

					if (spriteInfo != null && spriteInfo.texture != null && spriteInfo.texture.width > 0 && spriteInfo.texture.height > 0) {
						try {
							roadTex = new Texture2D((int)spriteInfo.texture.width, (int)spriteInfo.texture.height, TextureFormat.ARGB32, false);
							roadTex.SetPixels(0, 0, roadTex.width, roadTex.height, mainTex.GetPixels((int)(spriteInfo.region.x * mainTex.width), (int)(spriteInfo.region.y * mainTex.height), (int)(spriteInfo.region.width * mainTex.width), (int)(spriteInfo.region.height * mainTex.height)));
							roadTex.Apply();
							return;
						} catch (Exception e) {
							Log.Warning($"Could not get texture from NetInfo {info.name}: {e.ToString()}");
						}
					}
				}
			}

			// fallback to "noimage" texture
			roadTex = TrafficLightToolTextureResources.NoImageTexture2D;
		}

		private void _guiSpeedLimitsWindow(int num) {
			GUILayout.BeginHorizontal();

			Color oldColor = GUI.color;
			for (int i = 0; i < SpeedLimitManager.Instance.AvailableSpeedLimits.Count; ++i) {
				if (curSpeedLimitIndex != i)
					GUI.color = Color.gray;
				float signSize = TrafficManagerTool.AdaptWidth(guiSpeedSignSize);
				if (GUILayout.Button(TrafficLightToolTextureResources.SpeedLimitTextures[SpeedLimitManager.Instance.AvailableSpeedLimits[i]], GUILayout.Width(signSize), GUILayout.Height(signSize))) {
					curSpeedLimitIndex = i;
				}
				GUI.color = oldColor;

				if (i == 6) {
					GUILayout.EndHorizontal();
					GUILayout.BeginHorizontal();
				}
			}

			GUILayout.EndHorizontal();

			if (GUILayout.Button(Translation.GetString("Default_speed_limits"))) {
				defaultsWindowVisible = true;
			}

			DragWindow(ref windowRect);
		}

		private bool drawSpeedLimitHandles(ushort segmentId, bool viewOnly, ref Vector3 camPos) {
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
					GUI.DrawTexture(boundingBox, TrafficLightToolTextureResources.SpeedLimitTextures[SpeedLimitManager.Instance.GetCustomSpeedLimit(segmentId, e.Key)]);
				} catch (Exception ex) {
					Log.Error("segment " + segmentId + " limit: " + SpeedLimitManager.Instance.GetCustomSpeedLimit(segmentId, e.Key) + ", ex: " + ex.ToString());
				}

				if (hoveredHandle && Input.GetMouseButton(0) && !IsCursorInPanel()) {
					// change the speed limit to the selected one
					ushort speedLimitToSet = SpeedLimitManager.Instance.AvailableSpeedLimits[curSpeedLimitIndex];
					//Log._Debug($"Setting speed limit of segment {segmentId}, dir {e.Key.ToString()} to {speedLimitToSet}");
					SpeedLimitManager.Instance.SetSpeedLimit(segmentId, e.Key, speedLimitToSet);

					// TODO use SegmentTraverser
					if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
						// apply new speed limit to connected segments
						NetInfo selectedSegmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
						List<object[]> selectedSortedLanes = TrafficManagerTool.GetSortedVehicleLanes(segmentId, selectedSegmentInfo, null, VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Train); // TODO refactor vehicle mask

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
								List<object[]> sortedLanes = TrafficManagerTool.GetSortedVehicleLanes(otherSegmentId, segmentInfo, null, VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Train); // TODO refactor vehicle mask

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
											SpeedLimitManager.Instance.SetSpeedLimit(otherSegmentId, laneInfo.m_finalDirection, speedLimitToSet);
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
