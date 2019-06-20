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
using TrafficManager.Manager.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using TrafficManager.TrafficLight;
using TrafficManager.Util;
using UnityEngine;
using static ColossalFramework.UI.UITextureAtlas;
using static TrafficManager.Util.SegmentLaneTraverser;

namespace TrafficManager.UI.SubTools {
	public class SpeedLimitsTool : SubTool {
		private bool _cursorInSecondaryPanel;

		/// <summary>Currently selected speed limit on the limits palette</summary>
		private float currentPaletteSpeedLimit = -1f;

		private bool overlayHandleHovered;
		private Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>> segmentCenterByDir = new Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>>();
		private readonly float speedLimitSignSize = 80f;

		/// <summary>Visible sign size, slightly reduced from 100 to accomodate another column for MPH</summary>
		private readonly int guiSpeedSignSize = 90;

		private Rect windowRect = TrafficManagerTool.MoveGUI(new Rect(0, 0, 7 * 95, 225));
		private Rect defaultsWindowRect = TrafficManagerTool.MoveGUI(new Rect(0, 80, 50, 50));
		private HashSet<ushort> currentlyVisibleSegmentIds;
		private bool defaultsWindowVisible = false;
		private int currentInfoIndex = -1;
		private float currentSpeedLimit = -1f;
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

			var unitTitle = " (" + (GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
				                        ? Translation.GetString("Miles_per_hour")
				                        : Translation.GetString("Kilometers_per_hour")) + ")";
			windowRect.width = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph ? 10 * 95 : 8 * 95;
			windowRect = GUILayout.Window(254, windowRect, _guiSpeedLimitsWindow,
			                              Translation.GetString("Speed_limits") + unitTitle,
			                              WindowStyle);
			if (defaultsWindowVisible) {
				defaultsWindowRect = GUILayout.Window(
					258, defaultsWindowRect, _guiDefaultsWindow,
					Translation.GetString("Default_speed_limits"),
					WindowStyle);
			}
			_cursorInSecondaryPanel = windowRect.Contains(Event.current.mousePosition)
			                          || (defaultsWindowVisible
			                              && defaultsWindowRect.Contains(Event.current.mousePosition));

			//overlayHandleHovered = false;
			//ShowSigns(false);
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			
		}

		public override void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) {
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
			currentSpeedLimit = -1f;
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

					Vector3 screenPos;
					bool visible = MainTool.WorldToScreenPoint(netManager.m_segments.m_buffer[segmentId].m_bounds.center, out screenPos);
					
					if (! visible)
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
				Vector3 screenPos;
				bool visible = MainTool.WorldToScreenPoint(netManager.m_segments.m_buffer[segmentId].m_bounds.center, out screenPos);

				if (!visible)
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

		/// <summary>
		/// The window for setting the defaullt speeds per road type
		/// </summary>
		/// <param name="num"></param>
		private void _guiDefaultsWindow(int num) {
			var mainNetInfos = SpeedLimitManager.Instance.GetCustomizableNetInfos();

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

			if (currentSpeedLimit < 0f) {
				currentSpeedLimit = SpeedLimitManager.Instance.GetCustomNetInfoSpeedLimit(info);
				Log._Debug($"set currentSpeedLimit to {currentSpeedLimit}");
			}
			//Log._Debug($"currentInfoIndex={currentInfoIndex} currentSpeedLimitIndex={currentSpeedLimitIndex}");

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
				currentInfoIndex =
					(currentInfoIndex + mainNetInfos.Count - 1) % mainNetInfos.Count;
				info = mainNetInfos[currentInfoIndex];
				currentSpeedLimit = SpeedLimitManager.Instance.GetCustomNetInfoSpeedLimit(info);
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
				currentSpeedLimit = SpeedLimitManager.Instance.GetCustomNetInfoSpeedLimit(info);
				UpdateRoadTex(info);
			}

			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();

			GUILayout.EndHorizontal();

			var centeredTextStyle = new GUIStyle("label") { alignment = TextAnchor.MiddleCenter };

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
				// currentSpeedLimit = (currentSpeedLimitIndex + SpeedLimitManager.Instance.AvailableSpeedLimits.Count - 1) % SpeedLimitManager.Instance.AvailableSpeedLimits.Count;
				currentSpeedLimit = SpeedLimit.GetPrevious(currentSpeedLimit);
			}

			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();

			GUILayout.FlexibleSpace();

			GUILayout.BeginVertical();
			GUILayout.FlexibleSpace();

			// speed limit sign
			GUILayout.Box(TextureResources.GetSpeedLimitTexture(currentSpeedLimit),
			              GUILayout.Width(guiSpeedSignSize),
			              GUILayout.Height(guiSpeedSignSize));
			GUILayout.Label(GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
				                ? Translation.GetString("Miles_per_hour")
				                : Translation.GetString("Kilometers_per_hour"));

			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();

			GUILayout.FlexibleSpace();

			GUILayout.BeginVertical();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("→", GUILayout.Width(50))) {
				// currentSpeedLimitIndex = (currentSpeedLimitIndex + 1) % SpeedLimitManager.Instance.AvailableSpeedLimits.Count;
				currentSpeedLimit = SpeedLimit.GetNext(currentSpeedLimit);
			}

			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();

			GUILayout.EndHorizontal();

			// Save & Apply
			GUILayout.BeginVertical();
			GUILayout.Space(10);

			GUILayout.BeginHorizontal();

			// Close button. TODO: Make more visible or obey 'Esc' pressed or something
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("X", GUILayout.Width(80))) {
				defaultsWindowVisible = false;
			}

			GUILayout.FlexibleSpace();
			if (GUILayout.Button(Translation.GetString("Save"), GUILayout.Width(70))) {
				SpeedLimitManager.Instance.FixCurrentSpeedLimits(info);
				SpeedLimitManager.Instance.SetCustomNetInfoSpeedLimit(info, currentSpeedLimit);
			}

			GUILayout.FlexibleSpace();
			if (GUILayout.Button(
				Translation.GetString("Save") + " & " + Translation.GetString("Apply"),
				GUILayout.Width(160))) {
				SpeedLimitManager.Instance.SetCustomNetInfoSpeedLimit(info, currentSpeedLimit);
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

		/// <summary>
		/// The window for selecting and applying a speed limit
		/// </summary>
		/// <param name="num"></param>
		private void _guiSpeedLimitsWindow(int num) {
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();

			var oldColor = GUI.color;
			var allSpeedLimits = SpeedLimit.EnumerateSpeedLimits(SpeedUnit.CurrentlyConfigured);
			allSpeedLimits.Add(0); // add last item: no limit

			var showMph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
			var column = 0u; // break palette to a new line at breakColumn
			var breakColumn = showMph ? SpeedLimit.BREAK_PALETTE_COLUMN_MPH
				                  : SpeedLimit.BREAK_PALETTE_COLUMN_KMPH; 

			foreach (var speedLimit in allSpeedLimits) {
				// Highlight palette item if it is very close to its float speed
				if (SpeedLimit.NearlyEqual(currentPaletteSpeedLimit, speedLimit)) {
					GUI.color = Color.gray;
				}

				_guiSpeedLimitsWindow_AddButton(showMph, speedLimit);
				GUI.color = oldColor;

				// TODO: This can be calculated from SpeedLimit MPH or KMPH limit constants
				column++;
				if (column % breakColumn == 0) {
					GUILayout.FlexibleSpace();
					GUILayout.EndHorizontal();
					GUILayout.BeginHorizontal();
					GUILayout.FlexibleSpace();
				} 
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(Translation.GetString("Default_speed_limits"),
			                     GUILayout.Width(200))) {
				TrafficManagerTool.ShowAdvisor(this.GetType().Name + "_Defaults");
				defaultsWindowVisible = true;
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			showLimitsPerLane = GUILayout.Toggle(showLimitsPerLane, Translation.GetString("Show_lane-wise_speed_limits"));
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			DragWindow(ref windowRect);
		}

		/// <summary>Like addButton helper below, but adds unclickable space of the same size</summary>
		private void _guiSpeedLimitsWindow_AddDummy() {
			GUILayout.BeginVertical();
			var signSize = TrafficManagerTool.AdaptWidth(guiSpeedSignSize);
			GUILayout.Button( null as Texture, GUILayout.Width(signSize), GUILayout.Height(signSize));
			GUILayout.EndVertical();
		}

		/// <summary>Helper to create speed limit sign + label below converted to the opposite unit</summary>
		/// <param name="showMph">Config value from GlobalConfig.I.M.ShowMPH</param>
		/// <param name="speedLimit">The float speed to show</param>
		private void _guiSpeedLimitsWindow_AddButton(bool showMph, float speedLimit) {
			// The button is wrapped in vertical sub-layout and a label for MPH/KMPH is added
			GUILayout.BeginVertical();
			var signSize = TrafficManagerTool.AdaptWidth(guiSpeedSignSize);
			if (GUILayout.Button(
				TextureResources.GetSpeedLimitTexture(speedLimit),
				GUILayout.Width(signSize),
				GUILayout.Height(signSize))) {
				currentPaletteSpeedLimit = speedLimit;
			}
			// For MPH setting display KM/H below, for KM/H setting display MPH
			GUILayout.Label(showMph ? SpeedLimit.ToKmphPreciseString(speedLimit)
				                : SpeedLimit.ToMphPreciseString(speedLimit));
			GUILayout.EndVertical();
		}

		private bool drawSpeedLimitHandles(ushort segmentId, ref NetSegment segment, bool viewOnly, ref Vector3 camPos) {
			if (viewOnly && !Options.speedLimitsOverlay) {
				return false;
			}

			var center = segment.m_bounds.center;
			var netManager = Singleton<NetManager>.instance;

			var hovered = false;
			var speedLimitToSet = viewOnly ? -1f : currentPaletteSpeedLimit;

			bool showPerLane = showLimitsPerLane;
			if (!viewOnly) {
				showPerLane = showLimitsPerLane ^ (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
			}
			if (showPerLane) {
				// show individual speed limit handle per lane
				int numDirections;
				var numLanes = TrafficManagerTool.GetSegmentNumVehicleLanes(segmentId, null, out numDirections, SpeedLimitManager.VEHICLE_TYPES);

				var segmentInfo = segment.Info;
				var yu = (segment.m_endDirection - segment.m_startDirection).normalized;
				var xu = Vector3.Cross(yu, new Vector3(0, 1f, 0)).normalized;
				/*if ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) {
					xu = -xu;
				}*/
				var f = viewOnly ? 4f : 7f; // reserved sign size in game coordinates
				var zero = center - 0.5f * (float)(numLanes - 1 + numDirections - 1) * f * xu;

				uint x = 0;
				var guiColor = GUI.color;
				var sortedLanes = Constants.ServiceFactory.NetService.GetSortedLanes(
					segmentId, ref segment, null, SpeedLimitManager.LANE_TYPES,
					SpeedLimitManager.VEHICLE_TYPES);
				var onlyMonorailLanes = sortedLanes.Count > 0;
				if (!viewOnly) {
					foreach (LanePos laneData in sortedLanes) {
						var laneIndex = laneData.laneIndex;
						var laneInfo = segmentInfo.m_lanes[laneIndex];

						if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Monorail) == VehicleInfo.VehicleType.None) {
							onlyMonorailLanes = false;
							break;
						}
					}
				}

				var directions = new HashSet<NetInfo.Direction>();
				var sortedLaneIndex = -1;
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

					var laneSpeedLimit = SpeedLimitManager.Instance.GetCustomSpeedLimit(laneId);
					var hoveredHandle = MainTool.DrawGenericSquareOverlayGridTexture(
						TextureResources.GetSpeedLimitTexture(laneSpeedLimit),
						camPos, zero, f, xu, yu, x, 0, speedLimitSignSize,
						!viewOnly);

					if (!viewOnly
					    && !onlyMonorailLanes
					    && (laneInfo.m_vehicleType & VehicleInfo.VehicleType.Monorail) != VehicleInfo.VehicleType.None) {
						MainTool.DrawStaticSquareOverlayGridTexture(
							TextureResources.VehicleInfoSignTextures[ExtVehicleType.PassengerTrain],
							camPos, zero, f, xu, yu, x, 1, speedLimitSignSize);
					}
					if (hoveredHandle) {
						hovered = true;
					}

					if (hoveredHandle && Input.GetMouseButton(0) && !IsCursorInPanel()) {
						SpeedLimitManager.Instance.SetSpeedLimit(segmentId, laneIndex, laneInfo, laneId, speedLimitToSet);

						if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
							SegmentLaneTraverser.Traverse(segmentId, SegmentTraverser.TraverseDirection.AnyDirection, SegmentTraverser.TraverseSide.AnySide, SegmentLaneTraverser.LaneStopCriterion.LaneCount, SegmentTraverser.SegmentStopCriterion.Junction, SpeedLimitManager.LANE_TYPES, SpeedLimitManager.VEHICLE_TYPES, delegate (SegmentLaneVisitData data) {
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
					Vector3 screenPos;
					var visible = MainTool.WorldToScreenPoint(e.Value, out screenPos);

					if (!visible) {
						continue;
					}

					var zoom = 1.0f / (e.Value - camPos).magnitude * 100f * MainTool.GetBaseZoom();
					var size = (viewOnly ? 0.8f : 1f) * speedLimitSignSize * zoom;
					var guiColor = GUI.color;
					var boundingBox = new Rect(screenPos.x - (size / 2),
					                            screenPos.y - (size / 2),
					                            size, size);
					var hoveredHandle = !viewOnly && TrafficManagerTool.IsMouseOver(boundingBox);

					guiColor.a = MainTool.GetHandleAlpha(hoveredHandle);
					if (hoveredHandle) {
						// mouse hovering over sign
						hovered = true;
					}

					// Draw something right here, the road sign texture
					GUI.color = guiColor;
					var displayLimit = SpeedLimitManager.Instance.GetCustomSpeedLimit(segmentId, e.Key);
					var tex = TextureResources.GetSpeedLimitTexture(displayLimit);
					GUI.DrawTexture(boundingBox, tex);

					if (hoveredHandle && Input.GetMouseButton(0) && !IsCursorInPanel()) {
						// change the speed limit to the selected one
						//Log._Debug($"Setting speed limit of segment {segmentId}, dir {e.Key.ToString()} to {speedLimitToSet}");
						SpeedLimitManager.Instance.SetSpeedLimit(segmentId, e.Key, currentPaletteSpeedLimit);

						if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {

							NetInfo.Direction normDir = e.Key;
							if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
								normDir = NetInfo.InvertDirection(normDir);
							}

							SegmentLaneTraverser.Traverse(segmentId, SegmentTraverser.TraverseDirection.AnyDirection, SegmentTraverser.TraverseSide.AnySide, SegmentLaneTraverser.LaneStopCriterion.LaneCount, SegmentTraverser.SegmentStopCriterion.Junction, SpeedLimitManager.LANE_TYPES, SpeedLimitManager.VEHICLE_TYPES, delegate (SegmentLaneVisitData data) {
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
