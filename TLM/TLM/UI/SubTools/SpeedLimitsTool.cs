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
using TrafficManager.Manager;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using TrafficManager.Util;
using UnityEngine;
using static ColossalFramework.UI.UITextureAtlas;
using static TrafficManager.Util.SegmentLaneTraverser;

namespace TrafficManager.UI.SubTools {
	public class SpeedLimitsTool : SubTool {
		private bool _cursorInSecondaryPanel;
		private int curSpeedLimitIndex = 0;
		private bool overlayHandleHovered;
		private Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>> segmentCenterByDir = new Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>>();
		private readonly float speedLimitSignSize = 80f;
		private readonly int guiSpeedSignSize = 100;
		private Texture2D SecondPanelTexture {
			get {
				if (secondPanelTexture == null) {
					secondPanelTexture = TrafficManagerTool.MakeTex(1, 1, new Color(0.5f, 0.5f, 0.5f, 1f));
				}
				return secondPanelTexture;
			}
		}
		private Texture2D secondPanelTexture = null;
		private Rect windowRect = TrafficManagerTool.MoveGUI(new Rect(0, 0, 7 * 105, 225));
		private Rect defaultsWindowRect = TrafficManagerTool.MoveGUI(new Rect(0, 280, 400, 400));
		private HashSet<ushort> currentlyVisibleSegmentIds;
		private bool defaultsWindowVisible = false;
		private int currentInfoIndex = -1;
		private int currentSpeedLimitIndex = -1;
		private Texture2D RoadTexture {
			get {
				if (roadTexture == null) {
					roadTexture = new Texture2D(guiSpeedSignSize, guiSpeedSignSize);
				}
				return roadTexture;
			}
		}
		private Texture2D roadTexture = null;
		private bool showLimitsPerLane = false;

		public SpeedLimitsTool(TrafficManagerTool mainTool) : base(mainTool) {
			currentlyVisibleSegmentIds = new HashSet<ushort>();
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
					if (!Constants.ServiceFactory.NetService.IsSegmentValid((ushort)segmentId)) {
						continue;
					}
					/*if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
						continue;*/

					if ((netManager.m_segments.m_buffer[segmentId].m_bounds.center - camPos).magnitude > TrafficManagerTool.MaxOverlayDistance)
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
				if (MainTool.GetToolMode() != ToolMode.VehicleRestrictions || segmentId != SelectedSegmentId) { // no speed limit overlay on selected segment when in vehicle restrictions mode
					if (drawSpeedLimitHandles((ushort)segmentId, ref netManager.m_segments.m_buffer[segmentId], viewOnly, ref camPos))
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
			GUILayout.Box(RoadTexture, GUILayout.Height(guiSpeedSignSize));
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
			GUILayout.Box(TextureResources.SpeedLimitTextures[SpeedLimitManager.Instance.AvailableSpeedLimits[currentSpeedLimitIndex]], GUILayout.Width(guiSpeedSignSize), GUILayout.Height(guiSpeedSignSize));

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
			if (GUILayout.Button(Translation.GetString("Save") + " & " + Translation.GetString("Apply"), GUILayout.Width(160))) {
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
							roadTexture = new Texture2D((int)spriteInfo.texture.width, (int)spriteInfo.texture.height, TextureFormat.ARGB32, false);
							roadTexture.SetPixels(0, 0, roadTexture.width, roadTexture.height, mainTex.GetPixels((int)(spriteInfo.region.x * mainTex.width), (int)(spriteInfo.region.y * mainTex.height), (int)(spriteInfo.region.width * mainTex.width), (int)(spriteInfo.region.height * mainTex.height)));
							roadTexture.Apply();
							return;
						} catch (Exception e) {
							Log.Warning($"Could not get texture from NetInfo {info.name}: {e.ToString()}");
						}
					}
				}
			}

			// fallback to "noimage" texture
			roadTexture = TextureResources.NoImageTexture2D;
		}

		private void _guiSpeedLimitsWindow(int num) {
			GUILayout.BeginHorizontal();

			Color oldColor = GUI.color;
			for (int i = 0; i < SpeedLimitManager.Instance.AvailableSpeedLimits.Count; ++i) {
				if (curSpeedLimitIndex != i)
					GUI.color = Color.gray;
				float signSize = TrafficManagerTool.AdaptWidth(guiSpeedSignSize);
				if (GUILayout.Button(TextureResources.SpeedLimitTextures[SpeedLimitManager.Instance.AvailableSpeedLimits[i]], GUILayout.Width(signSize), GUILayout.Height(signSize))) {
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

			showLimitsPerLane = GUILayout.Toggle(showLimitsPerLane, Translation.GetString("Show_lane-wise_speed_limits"));

			DragWindow(ref windowRect);
		}

		private bool drawSpeedLimitHandles(ushort segmentId, ref NetSegment segment, bool viewOnly, ref Vector3 camPos) {
			if (viewOnly && !Options.speedLimitsOverlay)
				return false;

			Vector3 center = segment.m_bounds.center;
			NetManager netManager = Singleton<NetManager>.instance;

			bool hovered = false;
			ushort speedLimitToSet = viewOnly ? (ushort)0 : SpeedLimitManager.Instance.AvailableSpeedLimits[curSpeedLimitIndex];

			bool showPerLane = showLimitsPerLane;
			if (!viewOnly) {
				showPerLane = showLimitsPerLane ^ (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
			}
			if (showPerLane) {
				// show individual speed limit handle per lane
				int numDirections;
				int numLanes = TrafficManagerTool.GetSegmentNumVehicleLanes(segmentId, null, out numDirections, SpeedLimitManager.VEHICLE_TYPES);

				NetInfo segmentInfo = segment.Info;
				Vector3 yu = (segment.m_endDirection - segment.m_startDirection).normalized;
				Vector3 xu = Vector3.Cross(yu, new Vector3(0, 1f, 0)).normalized;
				/*if ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) {
					xu = -xu;
				}*/
				float f = viewOnly ? 4f : 7f; // reserved sign size in game coordinates
				Vector3 zero = center - 0.5f * (float)(numLanes - 1 + numDirections - 1) * f * xu;

				uint x = 0;
				var guiColor = GUI.color;
				IList<LanePos> sortedLanes = Constants.ServiceFactory.NetService.GetSortedLanes(segmentId, ref segment, null, SpeedLimitManager.LANE_TYPES, SpeedLimitManager.VEHICLE_TYPES);
				bool onlyMonorailLanes = sortedLanes.Count > 0;
				if (!viewOnly) {
					foreach (LanePos laneData in sortedLanes) {
						byte laneIndex = laneData.laneIndex;
						NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

						if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Monorail) == VehicleInfo.VehicleType.None) {
							onlyMonorailLanes = false;
							break;
						}
					}
				}

				HashSet<NetInfo.Direction> directions = new HashSet<NetInfo.Direction>();
				int sortedLaneIndex = -1;
				foreach (LanePos laneData in sortedLanes) {
					++sortedLaneIndex;
					uint laneId = laneData.laneId;
					byte laneIndex = laneData.laneIndex;

					NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
					if (!directions.Contains(laneInfo.m_finalDirection)) {
						if (directions.Count > 0)
							++x; // space between different directions
						directions.Add(laneInfo.m_finalDirection);
					}

					bool hoveredHandle = MainTool.DrawGenericSquareOverlayGridTexture(TextureResources.SpeedLimitTextures[SpeedLimitManager.Instance.GetCustomSpeedLimit(laneId)], ref camPos, ref zero, f, ref xu, ref yu, x, 0, speedLimitSignSize, !viewOnly, 0.5f, 0.8f);
					if (!viewOnly && !onlyMonorailLanes && (laneInfo.m_vehicleType & VehicleInfo.VehicleType.Monorail) != VehicleInfo.VehicleType.None) {
						MainTool.DrawStaticSquareOverlayGridTexture(TextureResources.VehicleInfoSignTextures[ExtVehicleType.PassengerTrain], ref camPos, ref zero, f, ref xu, ref yu, x, 1, speedLimitSignSize, 0.5f);
					}
					if (hoveredHandle)
						hovered = true;

					if (hoveredHandle && Input.GetMouseButton(0) && !IsCursorInPanel()) {
						SpeedLimitManager.Instance.SetSpeedLimit(segmentId, laneIndex, laneInfo, laneId, speedLimitToSet);

						if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
							SegmentLaneTraverser.Traverse(segmentId, SegmentTraverser.TraverseDirection.Both, SegmentLaneTraverser.LaneStopCriterion.LaneCount, SegmentTraverser.SegmentStopCriterion.Junction, SpeedLimitManager.LANE_TYPES, SpeedLimitManager.VEHICLE_TYPES, delegate (SegmentLaneVisitData data) {
								if (data.segVisitData.initial) {
									return true;
								}
								
								if (sortedLaneIndex != data.sortedLaneIndex) {
									return true;
								}

								Constants.ServiceFactory.NetService.ProcessSegment(data.segVisitData.curGeo.SegmentId, delegate (ushort curSegmentId, ref NetSegment curSegment) {
									NetInfo.Lane curLaneInfo = curSegment.Info.m_lanes[data.curLanePos.laneIndex];
									SpeedLimitManager.Instance.SetSpeedLimit(curSegmentId, data.curLanePos.laneIndex, curLaneInfo, data.curLanePos.laneId, speedLimitToSet);
									return true;
								});

								return true;
							});
						}
					}
					
					++x;
				}
			} else {
				// draw speedlimits over mean middle points of lane beziers
				Dictionary<NetInfo.Direction, Vector3> segCenter;
				if (!segmentCenterByDir.TryGetValue(segmentId, out segCenter)) {
					segCenter = new Dictionary<NetInfo.Direction, Vector3>();
					segmentCenterByDir.Add(segmentId, segCenter);
					TrafficManagerTool.CalculateSegmentCenterByDir(segmentId, segCenter);
				}

				foreach (KeyValuePair<NetInfo.Direction, Vector3> e in segCenter) {
					var screenPos = Camera.main.WorldToScreenPoint(e.Value);
					screenPos.y = Screen.height - screenPos.y;

					float zoom = 1.0f / (e.Value - camPos).magnitude * 100f * MainTool.GetBaseZoom();
					float size = (viewOnly ? 0.8f : 1f) * speedLimitSignSize * zoom;
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
					GUI.DrawTexture(boundingBox, TextureResources.SpeedLimitTextures[SpeedLimitManager.Instance.GetCustomSpeedLimit(segmentId, e.Key)]);

					if (hoveredHandle && Input.GetMouseButton(0) && !IsCursorInPanel()) {
						// change the speed limit to the selected one
						//Log._Debug($"Setting speed limit of segment {segmentId}, dir {e.Key.ToString()} to {speedLimitToSet}");
						SpeedLimitManager.Instance.SetSpeedLimit(segmentId, e.Key, speedLimitToSet);

						if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {

							NetInfo.Direction normDir = e.Key;
							if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
								normDir = NetInfo.InvertDirection(normDir);
							}

							SegmentLaneTraverser.Traverse(segmentId, SegmentTraverser.TraverseDirection.Both, SegmentLaneTraverser.LaneStopCriterion.LaneCount, SegmentTraverser.SegmentStopCriterion.Junction, SpeedLimitManager.LANE_TYPES, SpeedLimitManager.VEHICLE_TYPES, delegate (SegmentLaneVisitData data) {
								if (data.segVisitData.initial) {
									return true;
								}
								bool reverse = data.segVisitData.viaStartNode == data.segVisitData.viaInitialStartNode;

								ushort otherSegmentId = data.segVisitData.curGeo.SegmentId;
								NetInfo otherSegmentInfo = netManager.m_segments.m_buffer[otherSegmentId].Info;
								uint laneId = data.curLanePos.laneId;
								byte laneIndex = data.curLanePos.laneIndex;
								NetInfo.Lane laneInfo = otherSegmentInfo.m_lanes[laneIndex];

								NetInfo.Direction otherNormDir = laneInfo.m_finalDirection;
								if ((netManager.m_segments.m_buffer[otherSegmentId].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None ^
									reverse) {
									otherNormDir = NetInfo.InvertDirection(otherNormDir);
								}

								if (otherNormDir == normDir) {
									SpeedLimitManager.Instance.SetSpeedLimit(otherSegmentId, laneInfo.m_finalDirection, speedLimitToSet);
								}

								return true;
							});
						}
					}

					guiColor.a = 1f;
					GUI.color = guiColor;
				}
			}
			return hovered;
		}
	}
}
