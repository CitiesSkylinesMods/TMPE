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
using TrafficManager.TrafficLight;
using TrafficManager.Util;
using UnityEngine;
using static ColossalFramework.UI.UITextureAtlas;
using static TrafficManager.Util.SegmentLaneTraverser;
using static TrafficManager.Util.SegmentTraverser;

namespace TrafficManager.UI.SubTools {
	public class ParkingRestrictionsTool : SubTool {
		private bool overlayHandleHovered;
		private Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>> segmentCenterByDir = new Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>>();
		private readonly float signSize = 80f;
		private HashSet<ushort> currentlyVisibleSegmentIds;
		
		public ParkingRestrictionsTool(TrafficManagerTool mainTool) : base(mainTool) {
			currentlyVisibleSegmentIds = new HashSet<ushort>();
		}

		public override void OnActivate() {
			
		}

		public override void OnPrimaryClickOverlay() {
			
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			
		}

		public override void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) {
			if (viewOnly && !Options.parkingRestrictionsOverlay)
				return;

			overlayHandleHovered = false;
			ShowSigns(viewOnly);
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
			Quaternion camRot = Camera.main.transform.rotation;
			Vector3 camPos = Camera.main.transform.position;

			NetManager netManager = Singleton<NetManager>.instance;
			ParkingRestrictionsManager parkingManager = ParkingRestrictionsManager.Instance;

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

					if (!visible)
						continue;

					if (!parkingManager.MayHaveParkingRestriction((ushort)segmentId))
						continue;

					currentlyVisibleSegmentIds.Add((ushort)segmentId);
				}

				lastCamPos = camPos;
				lastCamRot = camRot;
			}

			bool handleHovered = false;
			bool clicked = !viewOnly && MainTool.CheckClicked();
			foreach (ushort segmentId in currentlyVisibleSegmentIds) {
				Vector3 screenPos;
				bool visible = MainTool.WorldToScreenPoint(netManager.m_segments.m_buffer[segmentId].m_bounds.center, out screenPos);

				if (!visible)
					continue;

				NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;

				// draw parking restrictions
				if (MainTool.GetToolMode() != ToolMode.SpeedLimits && (MainTool.GetToolMode() != ToolMode.VehicleRestrictions || segmentId != SelectedSegmentId)) { // no parking restrictions overlay on selected segment when in vehicle restrictions mode
					if (drawParkingRestrictionHandles((ushort)segmentId, clicked, ref netManager.m_segments.m_buffer[segmentId], viewOnly, ref camPos))
						handleHovered = true;
				}
			}
			overlayHandleHovered = handleHovered;
		}

		private bool drawParkingRestrictionHandles(ushort segmentId, bool clicked, ref NetSegment segment, bool viewOnly, ref Vector3 camPos) {
			if (viewOnly && !Options.parkingRestrictionsOverlay)
				return false;

			Vector3 center = segment.m_bounds.center;
			NetManager netManager = Singleton<NetManager>.instance;
			ParkingRestrictionsManager parkingManager = ParkingRestrictionsManager.Instance;

			bool hovered = false;
			
			// draw parking restriction signs over mean middle points of lane beziers
			Dictionary<NetInfo.Direction, Vector3> segCenter;
			if (!segmentCenterByDir.TryGetValue(segmentId, out segCenter)) {
				segCenter = new Dictionary<NetInfo.Direction, Vector3>();
				segmentCenterByDir.Add(segmentId, segCenter);
				TrafficManagerTool.CalculateSegmentCenterByDir(segmentId, segCenter);
			}

			foreach (KeyValuePair<NetInfo.Direction, Vector3> e in segCenter) {
				bool allowed = parkingManager.IsParkingAllowed(segmentId, e.Key);
				if (allowed && viewOnly) {
					continue;
				}

				Vector3 screenPos;
				bool visible = MainTool.WorldToScreenPoint(e.Value, out screenPos);

				if (!visible)
					continue;

				float zoom = 1.0f / (e.Value - camPos).magnitude * 100f * MainTool.GetBaseZoom();
				float size = (viewOnly ? 0.8f : 1f) * signSize * zoom;
				Color guiColor = GUI.color;
				Rect boundingBox = new Rect(screenPos.x - size / 2, screenPos.y - size / 2, size, size);
				if (Options.speedLimitsOverlay) {
					boundingBox.y -= size + 10f;
				}
				bool hoveredHandle = !viewOnly && TrafficManagerTool.IsMouseOver(boundingBox);

				guiColor.a = MainTool.GetHandleAlpha(hoveredHandle);
				if (hoveredHandle) {
					// mouse hovering over sign
					hovered = true;
				}

				GUI.color = guiColor;
				GUI.DrawTexture(boundingBox, TextureResources.ParkingRestrictionTextures[allowed]);

				if (hoveredHandle && clicked && !IsCursorInPanel()) {
					if (parkingManager.ToggleParkingAllowed(segmentId, e.Key)) {
						allowed = !allowed;

						if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {

							NetInfo.Direction normDir = e.Key;
							if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
								normDir = NetInfo.InvertDirection(normDir);
							}

							SegmentLaneTraverser.Traverse(segmentId, SegmentTraverser.TraverseDirection.AnyDirection, SegmentTraverser.TraverseSide.AnySide, SegmentLaneTraverser.LaneStopCriterion.LaneCount, SegmentTraverser.SegmentStopCriterion.Junction, ParkingRestrictionsManager.LANE_TYPES, ParkingRestrictionsManager.VEHICLE_TYPES, delegate (SegmentLaneVisitData data) {
								if (data.segVisitData.initial) {
									return true;
								}
								bool reverse = data.segVisitData.viaStartNode == data.segVisitData.viaInitialStartNode;

								ushort otherSegmentId = data.segVisitData.curSeg.segmentId;
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
									parkingManager.SetParkingAllowed(otherSegmentId, laneInfo.m_finalDirection, allowed);
								}

								return true;
							});
						}
					}
				}

				guiColor.a = 1f;
				GUI.color = guiColor;
			}
			return hovered;
		}
	}
}
