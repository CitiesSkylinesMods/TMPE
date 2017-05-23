using ColossalFramework;
using ColossalFramework.Math;
using GenericGameBridge.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Manager;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using TrafficManager.Util;
using UnityEngine;
using static TrafficManager.Manager.VehicleRestrictionsManager;
using static TrafficManager.UI.TrafficManagerTool;
using static TrafficManager.Util.SegmentLaneTraverser;

namespace TrafficManager.UI.SubTools {
	public class VehicleRestrictionsTool : SubTool {
		private static ExtVehicleType[] roadVehicleTypes = new ExtVehicleType[] { ExtVehicleType.PassengerCar, ExtVehicleType.Bus, ExtVehicleType.Taxi, ExtVehicleType.CargoTruck, ExtVehicleType.Service, ExtVehicleType.Emergency };
		private static ExtVehicleType[] railVehicleTypes = new ExtVehicleType[] { ExtVehicleType.PassengerTrain, ExtVehicleType.CargoTrain };
		private readonly float vehicleRestrictionsSignSize = 80f;
		private bool _cursorInSecondaryPanel;
		private bool overlayHandleHovered;
		private Rect windowRect = TrafficManagerTool.MoveGUI(new Rect(0, 0, 620, 100));
		private HashSet<ushort> currentRestrictedSegmentIds;

		public VehicleRestrictionsTool(TrafficManagerTool mainTool) : base(mainTool) {
			currentRestrictedSegmentIds = new HashSet<ushort>();
		}

		public override void OnActivate() {
			_cursorInSecondaryPanel = false;
			RefreshCurrentRestrictedSegmentIds();
		}

		private void RefreshCurrentRestrictedSegmentIds(ushort forceSegmentId=0) {
			if (forceSegmentId == 0) {
				currentRestrictedSegmentIds.Clear();
			} else {
				currentRestrictedSegmentIds.Remove(forceSegmentId);
			}

			for (uint segmentId = (forceSegmentId == 0 ? 1u : forceSegmentId); segmentId <= (forceSegmentId == 0 ? NetManager.MAX_SEGMENT_COUNT - 1 : forceSegmentId); ++segmentId) {
				if (!Constants.ServiceFactory.NetService.IsSegmentValid((ushort)segmentId)) {
					continue;
				}

				if (VehicleRestrictionsManager.Instance.HasSegmentRestrictions((ushort)segmentId))
					currentRestrictedSegmentIds.Add((ushort)segmentId);
			}
		}

		public override void Cleanup() {
			
		}

		public override void Initialize() {
			Cleanup();
			if (Options.vehicleRestrictionsOverlay) {
				RefreshCurrentRestrictedSegmentIds();
			} else {
				currentRestrictedSegmentIds.Clear();
			}
		}

		public override bool IsCursorInPanel() {
			return base.IsCursorInPanel() || _cursorInSecondaryPanel;
		}

		public override void OnPrimaryClickOverlay() {
			//Log._Debug($"Restrictions: {HoveredSegmentId} {overlayHandleHovered}");
			if (HoveredSegmentId == 0) return;
			if (overlayHandleHovered) return;

			SelectedSegmentId = HoveredSegmentId;
			currentRestrictedSegmentIds.Add(SelectedSegmentId);
			MainTool.CheckClicked(); // TODO do we need that?
		}

		public override void OnSecondaryClickOverlay() {
			if (!IsCursorInPanel()) {
				SelectedSegmentId = 0;
			}
		}

		public override void OnToolGUI(Event e) {
			base.OnToolGUI(e);

			if (SelectedSegmentId != 0) {
				_cursorInSecondaryPanel = false;

				windowRect = GUILayout.Window(255, windowRect, _guiVehicleRestrictionsWindow, Translation.GetString("Vehicle_restrictions"));
				_cursorInSecondaryPanel = windowRect.Contains(Event.current.mousePosition);

				//overlayHandleHovered = false;
			}
			//ShowSigns(false);
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			//Log._Debug($"Restrictions overlay {_cursorInSecondaryPanel} {HoveredNodeId} {SelectedNodeId} {HoveredSegmentId} {SelectedSegmentId}");

			if (SelectedSegmentId != 0)
				NetTool.RenderOverlay(cameraInfo, ref Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId], MainTool.GetToolColor(true, false), MainTool.GetToolColor(true, false));

			if (_cursorInSecondaryPanel)
				return;

			if (HoveredSegmentId != 0 && HoveredSegmentId != SelectedSegmentId && !overlayHandleHovered) {
				NetTool.RenderOverlay(cameraInfo, ref Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId], MainTool.GetToolColor(false, false), MainTool.GetToolColor(false, false));
			}
		}

		public override void ShowGUIOverlay(bool viewOnly) {
			if (viewOnly && !Options.vehicleRestrictionsOverlay)
				return;

			ShowSigns(viewOnly);
		}

		private void ShowSigns(bool viewOnly) {
			Vector3 camPos = Camera.main.transform.position;
			NetManager netManager = Singleton<NetManager>.instance;
			ushort updatedSegmentId = 0;
			bool handleHovered = false;
			foreach (ushort segmentId in currentRestrictedSegmentIds) {
				if (!Constants.ServiceFactory.NetService.IsSegmentValid(segmentId)) {
					continue;
				}

				var segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;

				Vector3 centerPos = netManager.m_segments.m_buffer[segmentId].m_bounds.center;
				var screenPos = Camera.main.WorldToScreenPoint(centerPos);
				screenPos.y = Screen.height - screenPos.y;

				if (screenPos.z < 0)
					continue;

				if ((netManager.m_segments.m_buffer[segmentId].m_bounds.center - camPos).magnitude > TrafficManagerTool.MaxOverlayDistance)
					continue; // do not draw if too distant

				// draw vehicle restrictions
				bool update;
				if (drawVehicleRestrictionHandles(segmentId, ref netManager.m_segments.m_buffer[segmentId], viewOnly || segmentId != SelectedSegmentId, out update))
					handleHovered = true;

				if (update) {
					updatedSegmentId = segmentId;
				}
			}
			overlayHandleHovered = handleHovered;

			if (updatedSegmentId != 0) {
				RefreshCurrentRestrictedSegmentIds(updatedSegmentId);
			}
		}

		private void _guiVehicleRestrictionsWindow(int num) {
			if (GUILayout.Button(Translation.GetString("Invert"))) {
				// invert pattern

				NetInfo selectedSegmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId].Info;
				IList<LanePos> sortedLanes = Constants.ServiceFactory.NetService.GetSortedLanes(SelectedSegmentId, ref Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId], null, VehicleRestrictionsManager.LANE_TYPES, VehicleRestrictionsManager.VEHICLE_TYPES); // TODO does not need to be sorted, but every lane should be a vehicle lane
				foreach (LanePos laneData in sortedLanes) {
					uint laneId = laneData.laneId;
					byte laneIndex = laneData.laneIndex;
					NetInfo.Lane laneInfo = selectedSegmentInfo.m_lanes[laneIndex];

					ExtVehicleType baseMask = VehicleRestrictionsManager.Instance.GetBaseMask(laneInfo, RestrictionMode.Configured);

					if (baseMask == ExtVehicleType.None)
						continue;

					ExtVehicleType allowedTypes = VehicleRestrictionsManager.Instance.GetAllowedVehicleTypes(SelectedSegmentId, selectedSegmentInfo, laneIndex, laneInfo, RestrictionMode.Configured);
					allowedTypes = ~(allowedTypes & VehicleRestrictionsManager.EXT_VEHICLE_TYPES) & baseMask;
					VehicleRestrictionsManager.Instance.SetAllowedVehicleTypes(SelectedSegmentId, selectedSegmentInfo, laneIndex, laneInfo, laneId, allowedTypes);
				}
				RefreshCurrentRestrictedSegmentIds(SelectedSegmentId);
			}

			GUILayout.BeginHorizontal();
			if (GUILayout.Button(Translation.GetString("Allow_all_vehicles"))) {
				// allow all vehicle types

				NetInfo selectedSegmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId].Info;
				IList<LanePos> sortedLanes = Constants.ServiceFactory.NetService.GetSortedLanes(SelectedSegmentId, ref Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId], null, VehicleRestrictionsManager.LANE_TYPES, VehicleRestrictionsManager.VEHICLE_TYPES); // TODO does not need to be sorted, but every lane should be a vehicle lane
				foreach (LanePos laneData in sortedLanes) {
					uint laneId = laneData.laneId;
					byte laneIndex = laneData.laneIndex;
					NetInfo.Lane laneInfo = selectedSegmentInfo.m_lanes[laneIndex];

					ExtVehicleType baseMask = VehicleRestrictionsManager.Instance.GetBaseMask(laneInfo, RestrictionMode.Configured);

					if (baseMask == ExtVehicleType.None)
						continue;

					VehicleRestrictionsManager.Instance.SetAllowedVehicleTypes(SelectedSegmentId, selectedSegmentInfo, laneIndex, laneInfo, laneId, baseMask);
				}
				RefreshCurrentRestrictedSegmentIds(SelectedSegmentId);
			}

			if (GUILayout.Button(Translation.GetString("Ban_all_vehicles"))) {
				// ban all vehicle types

				NetInfo selectedSegmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId].Info;
				IList<LanePos> sortedLanes = Constants.ServiceFactory.NetService.GetSortedLanes(SelectedSegmentId, ref Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId], null, VehicleRestrictionsManager.LANE_TYPES, VehicleRestrictionsManager.VEHICLE_TYPES); // TODO does not need to be sorted, but every lane should be a vehicle lane
				foreach (LanePos laneData in sortedLanes) {
					uint laneId = laneData.laneId;
					byte laneIndex = laneData.laneIndex;
					NetInfo.Lane laneInfo = selectedSegmentInfo.m_lanes[laneIndex];

					ExtVehicleType baseMask = VehicleRestrictionsManager.Instance.GetBaseMask(laneInfo, RestrictionMode.Configured);

					if (baseMask == ExtVehicleType.None)
						continue;

					VehicleRestrictionsManager.Instance.SetAllowedVehicleTypes(SelectedSegmentId, selectedSegmentInfo, laneIndex, laneInfo, laneId, ~VehicleRestrictionsManager.EXT_VEHICLE_TYPES & baseMask);
				}
				RefreshCurrentRestrictedSegmentIds(SelectedSegmentId);
			}
			GUILayout.EndHorizontal();

			if (GUILayout.Button(Translation.GetString("Apply_vehicle_restrictions_to_all_road_segments_between_two_junctions"))) {
				ApplyRestrictionsToAllSegments();
			}

			DragWindow(ref windowRect);
		}

		private void ApplyRestrictionsToAllSegments(int? sortedLaneIndex=null) {
			NetManager netManager = Singleton<NetManager>.instance;

			NetInfo selectedSegmentInfo = netManager.m_segments.m_buffer[SelectedSegmentId].Info;
			ushort selectedStartNodeId = netManager.m_segments.m_buffer[SelectedSegmentId].m_startNode;
			ushort selectedEndNodeId = netManager.m_segments.m_buffer[SelectedSegmentId].m_endNode;

			SegmentLaneTraverser.Traverse(SelectedSegmentId, SegmentTraverser.TraverseDirection.Both, SegmentLaneTraverser.LaneStopCriterion.LaneCount, SegmentTraverser.SegmentStopCriterion.Junction, VehicleRestrictionsManager.LANE_TYPES, VehicleRestrictionsManager.VEHICLE_TYPES, delegate (SegmentLaneVisitData data) {
				if (data.segVisitData.initial) {
					return true;
				}

				if (sortedLaneIndex != null && data.sortedLaneIndex != sortedLaneIndex) {
					return true;
				}

				ushort segmentId = data.segVisitData.curGeo.SegmentId;
				NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;

				uint selectedLaneId = data.initLanePos.laneId;
				byte selectedLaneIndex = data.initLanePos.laneIndex;
				NetInfo.Lane selectedLaneInfo = selectedSegmentInfo.m_lanes[selectedLaneIndex];

				uint laneId = data.curLanePos.laneId;
				byte laneIndex = data.curLanePos.laneIndex;
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

				ExtVehicleType baseMask = VehicleRestrictionsManager.Instance.GetBaseMask(laneInfo, RestrictionMode.Configured);
				if (baseMask == ExtVehicleType.None) {
					return true;
				}

				// apply restrictions of selected segment & lane
				ExtVehicleType mask = ~VehicleRestrictionsManager.EXT_VEHICLE_TYPES & baseMask; // ban all possible controllable vehicles
				mask |= VehicleRestrictionsManager.EXT_VEHICLE_TYPES & VehicleRestrictionsManager.Instance.GetAllowedVehicleTypes(SelectedSegmentId, selectedSegmentInfo, selectedLaneIndex, selectedLaneInfo, RestrictionMode.Configured); // allow all enabled and controllable vehicles

				VehicleRestrictionsManager.Instance.SetAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo, laneId, mask);

				RefreshCurrentRestrictedSegmentIds(segmentId);

				return true;
			});
		}

		private bool drawVehicleRestrictionHandles(ushort segmentId, ref NetSegment segment, bool viewOnly, out bool stateUpdated) {
			stateUpdated = false;

			if (viewOnly && !Options.vehicleRestrictionsOverlay && MainTool.GetToolMode() != ToolMode.VehicleRestrictions)
				return false;

			Vector3 center = segment.m_bounds.center;

			var screenPos = Camera.main.WorldToScreenPoint(center);
			screenPos.y = Screen.height - screenPos.y;
			if (screenPos.z < 0)
				return false;
			var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
			var diff = center - camPos;

			if (diff.magnitude > TrafficManagerTool.MaxOverlayDistance)
				return false; // do not draw if too distant

			int numDirections;
			int numLanes = TrafficManagerTool.GetSegmentNumVehicleLanes(segmentId, null, out numDirections, VehicleRestrictionsManager.VEHICLE_TYPES);

			// draw vehicle restrictions over each lane
			NetInfo segmentInfo = segment.Info;
			Vector3 yu = (segment.m_endDirection - segment.m_startDirection).normalized;
			/*if ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None)
				yu = -yu;*/
			Vector3 xu = Vector3.Cross(yu, new Vector3(0, 1f, 0)).normalized;
			float f = viewOnly ? 4f : 7f; // reserved sign size in game coordinates
			int maxNumSigns = 0;
			if (VehicleRestrictionsManager.Instance.IsRoadSegment(segmentInfo))
				maxNumSigns = roadVehicleTypes.Length;
			else if (VehicleRestrictionsManager.Instance.IsRailSegment(segmentInfo))
				maxNumSigns = railVehicleTypes.Length;
			//Vector3 zero = center - 0.5f * (float)(numLanes + numDirections - 1) * f * (xu + yu); // "bottom left"
			Vector3 zero = center - 0.5f * (float)(numLanes - 1 + numDirections - 1) * f * xu - 0.5f * (float)maxNumSigns * f * yu; // "bottom left"

			/*if (!viewOnly)
				Log._Debug($"xu: {xu.ToString()} yu: {yu.ToString()} center: {center.ToString()} zero: {zero.ToString()} numLanes: {numLanes} numDirections: {numDirections}");*/

			uint x = 0;
			var guiColor = GUI.color;
			IList<LanePos> sortedLanes = Constants.ServiceFactory.NetService.GetSortedLanes(segmentId, ref segment, null, VehicleRestrictionsManager.LANE_TYPES, VehicleRestrictionsManager.VEHICLE_TYPES);
			bool hovered = false;
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

				ExtVehicleType[] possibleVehicleTypes = null;
				if (VehicleRestrictionsManager.Instance.IsRoadLane(laneInfo)) {
					possibleVehicleTypes = roadVehicleTypes;
				} else if (VehicleRestrictionsManager.Instance.IsRailLane(laneInfo)) {
					possibleVehicleTypes = railVehicleTypes;
				} else {
					++x;
					continue;
				}

				ExtVehicleType allowedTypes = VehicleRestrictionsManager.Instance.GetAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo, RestrictionMode.Configured);

				uint y = 0;
#if DEBUGx
				Vector3 labelCenter = zero + f * (float)x * xu + f * (float)y * yu; // in game coordinates

				var labelScreenPos = Camera.main.WorldToScreenPoint(labelCenter);
				labelScreenPos.y = Screen.height - labelScreenPos.y;
				diff = labelCenter - camPos;

				var labelZoom = 1.0f / diff.magnitude * 100f;
				_counterStyle.fontSize = (int)(11f * labelZoom);
				_counterStyle.normal.textColor = new Color(1f, 1f, 0f);

				string labelStr = $"Idx {laneIndex}";
				Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
				Rect labelRect = new Rect(labelScreenPos.x - dim.x / 2f, labelScreenPos.y, dim.x, dim.y);
				GUI.Label(labelRect, labelStr, _counterStyle);

				++y;
#endif
				foreach (ExtVehicleType vehicleType in possibleVehicleTypes) {
					bool allowed = VehicleRestrictionsManager.Instance.IsAllowed(allowedTypes, vehicleType);
					if (allowed && viewOnly)
						continue; // do not draw allowed vehicles in view-only mode

					bool hoveredHandle = MainTool.DrawGenericSquareOverlayGridTexture(TextureResources.VehicleRestrictionTextures[vehicleType][allowed], ref camPos, ref zero, f, ref xu, ref yu, x, y, vehicleRestrictionsSignSize, !viewOnly, 0.5f, 0.8f);
					if (hoveredHandle)
						hovered = true;

					if (hoveredHandle && MainTool.CheckClicked()) {
						// toggle vehicle restrictions
						//Log._Debug($"Setting vehicle restrictions of segment {segmentId}, lane idx {laneIndex}, {vehicleType.ToString()} to {!allowed}");
						VehicleRestrictionsManager.Instance.ToggleAllowedType(segmentId, segmentInfo, laneIndex, laneId, laneInfo, vehicleType, !allowed);
						stateUpdated = true;

						// TODO use SegmentTraverser
						if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
							ApplyRestrictionsToAllSegments(sortedLaneIndex);
						}
					}

					++y;
				}

				++x;
			}

			guiColor.a = 1f;
			GUI.color = guiColor;

			return hovered;
		}
	}
}
