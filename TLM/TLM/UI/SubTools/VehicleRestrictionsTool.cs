using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;

namespace TrafficManager.UI.SubTools {
	public class VehicleRestrictionsTool : SubTool {
		private static ExtVehicleType[] roadVehicleTypes = new ExtVehicleType[] { ExtVehicleType.PassengerCar, ExtVehicleType.Bus, ExtVehicleType.Taxi, ExtVehicleType.CargoTruck, ExtVehicleType.Service, ExtVehicleType.Emergency };
		private static ExtVehicleType[] railVehicleTypes = new ExtVehicleType[] { ExtVehicleType.PassengerTrain, ExtVehicleType.CargoTrain };
		private static float vehicleRestrictionsSignSize = 80f;
		private bool _cursorInSecondaryPanel;
		private bool overlayHandleHovered;
		private Texture2D SecondPanelTexture;
		private Rect windowRect = TrafficManagerTool.MoveGUI(new Rect(0, 0, 620, 100));

		public VehicleRestrictionsTool(TrafficManagerTool mainTool) : base(mainTool) {
			SecondPanelTexture = TrafficManagerTool.MakeTex(1, 1, new Color(0.5f, 0.5f, 0.5f, 1f));
		}

		public override void OnActivate() {
			_cursorInSecondaryPanel = false;
		}

		public override bool IsCursorInPanel() {
			return _cursorInSecondaryPanel;
		}

		public override void OnClickOverlay() {
			//Log._Debug($"Restrictions: {HoveredSegmentId} {overlayHandleHovered}");
			if (HoveredSegmentId == 0) return;
			if (overlayHandleHovered) return;

			SelectedSegmentId = HoveredSegmentId;
			MainTool.CheckClicked(); // TODO do we need that?
		}

		public override void OnToolGUI(Event e) {
			if (SelectedSegmentId != 0) {
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

				GUILayout.Window(255, windowRect, _guiVehicleRestrictionsWindow, Translation.GetString("Vehicle_restrictions"), style);
				_cursorInSecondaryPanel = windowRect.Contains(Event.current.mousePosition);

				overlayHandleHovered = false;
			}
			ShowSigns(false);
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

		public override void ShowIcons() {
			ShowSigns(true);
		}

		private void ShowSigns(bool viewOnly) {
			Array16<NetSegment> segments = Singleton<NetManager>.instance.m_segments;
			bool handleHovered = false;
			for (int i = 1; i < segments.m_size; ++i) {
				if (segments.m_buffer[i].m_flags == NetSegment.Flags.None) // segment is unused
					continue;

				var segmentInfo = segments.m_buffer[i].Info;

				Vector3 centerPos = segments.m_buffer[i].m_bounds.center;
				var screenPos = Camera.main.WorldToScreenPoint(centerPos);
				screenPos.y = Screen.height - screenPos.y;

				if (screenPos.z < 0)
					continue;

				// draw vehicle restrictions
				if (drawVehicleRestrictionHandles((ushort)i, viewOnly || i != SelectedSegmentId))
					handleHovered = true;
			}
			overlayHandleHovered = handleHovered;
		}

		private void _guiVehicleRestrictionsWindow(int num) {
			if (GUILayout.Button(Translation.GetString("Invert"))) {
				// invert pattern

				NetInfo selectedSegmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId].Info;
				List<object[]> sortedLanes = TrafficManagerTool.GetSortedVehicleLanes(SelectedSegmentId, selectedSegmentInfo, null); // TODO does not need to be sorted, but every lane should be a vehicle lane
				foreach (object[] laneData in sortedLanes) {
					uint laneId = (uint)laneData[0];
					uint laneIndex = (uint)laneData[2];
					NetInfo.Lane laneInfo = selectedSegmentInfo.m_lanes[laneIndex];

					ExtVehicleType baseMask = ExtVehicleType.None;
					if (VehicleRestrictionsManager.IsRoadLane(laneInfo)) {
						baseMask = ExtVehicleType.RoadVehicle;
					} else if (VehicleRestrictionsManager.IsRailLane(laneInfo)) {
						baseMask = ExtVehicleType.RailVehicle;
					}

					if (baseMask == ExtVehicleType.None)
						continue;

					ExtVehicleType allowedTypes = VehicleRestrictionsManager.GetAllowedVehicleTypes(SelectedSegmentId, selectedSegmentInfo, laneIndex, laneInfo);
					allowedTypes = ~allowedTypes & baseMask;
					VehicleRestrictionsManager.SetAllowedVehicleTypes(SelectedSegmentId, laneIndex, laneId, allowedTypes);
				}
			}

			GUILayout.BeginHorizontal();
			if (GUILayout.Button(Translation.GetString("Allow_all_vehicles"))) {
				// allow all vehicle types

				NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId].Info;
				List<object[]> sortedLanes = TrafficManagerTool.GetSortedVehicleLanes(SelectedSegmentId, segmentInfo, null); // TODO does not need to be sorted, but every lane should be a vehicle lane
				foreach (object[] laneData in sortedLanes) {
					uint laneId = (uint)laneData[0];
					uint laneIndex = (uint)laneData[2];
					NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

					ExtVehicleType baseMask = ExtVehicleType.None;
					if (VehicleRestrictionsManager.IsRoadLane(laneInfo)) {
						baseMask = ExtVehicleType.RoadVehicle;
					} else if (VehicleRestrictionsManager.IsRailLane(laneInfo)) {
						baseMask = ExtVehicleType.RailVehicle;
					}

					if (baseMask == ExtVehicleType.None)
						continue;

					VehicleRestrictionsManager.SetAllowedVehicleTypes(SelectedSegmentId, laneIndex, laneId, baseMask);
				}
			}

			if (GUILayout.Button(Translation.GetString("Ban_all_vehicles"))) {
				// ban all vehicle types

				NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId].Info;
				List<object[]> sortedLanes = TrafficManagerTool.GetSortedVehicleLanes(SelectedSegmentId, segmentInfo, null); // TODO does not need to be sorted, but every lane should be a vehicle lane
				foreach (object[] laneData in sortedLanes) {
					uint laneId = (uint)laneData[0];
					uint laneIndex = (uint)laneData[2];
					NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

					ExtVehicleType baseMask = ExtVehicleType.None;
					if (VehicleRestrictionsManager.IsRoadLane(laneInfo)) {
						baseMask = ExtVehicleType.RoadVehicle;
					} else if (VehicleRestrictionsManager.IsRailLane(laneInfo)) {
						baseMask = ExtVehicleType.RailVehicle;
					}

					if (baseMask == ExtVehicleType.None)
						continue;

					VehicleRestrictionsManager.SetAllowedVehicleTypes(SelectedSegmentId, laneIndex, laneId, ~baseMask);
				}
			}
			GUILayout.EndHorizontal();

			if (GUILayout.Button(Translation.GetString("Apply_vehicle_restrictions_to_all_road_segments_between_two_junctions"))) {
				NetInfo selectedSegmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId].Info;
				List<object[]> selectedSortedLanes = TrafficManagerTool.GetSortedVehicleLanes(SelectedSegmentId, selectedSegmentInfo, null);

				LinkedList<ushort> nodesToProcess = new LinkedList<ushort>();
				HashSet<ushort> processedNodes = new HashSet<ushort>();
				HashSet<ushort> processedSegments = new HashSet<ushort>();
				processedSegments.Add(SelectedSegmentId);

				ushort selectedStartNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId].m_startNode;
				ushort selectedEndNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId].m_endNode;

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
						var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(s);

						if (segmentId <= 0 || processedSegments.Contains(segmentId))
							continue;
						processedSegments.Add(segmentId);

						NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
						List<object[]> sortedLanes = TrafficManagerTool.GetSortedVehicleLanes(segmentId, segmentInfo, null);

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

								// apply restrictions of selected segment & lane
								VehicleRestrictionsManager.SetAllowedVehicleTypes(segmentId, laneIndex, laneId, VehicleRestrictionsManager.GetAllowedVehicleTypes(SelectedSegmentId, selectedSegmentInfo, selectedLaneIndex, selectedLaneInfo));
							}

							// add nodes to explore
							ushort startNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startNode;
							ushort endNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endNode;

							if (startNodeId != 0 && !processedNodes.Contains(startNodeId))
								nodesToProcess.AddFirst(startNodeId);
							if (endNodeId != 0 && !processedNodes.Contains(endNodeId))
								nodesToProcess.AddFirst(endNodeId);
						}
					}
				}
			}
		}

		private bool drawVehicleRestrictionHandles(ushort segmentId, bool viewOnly) {
			if (!LoadingExtension.IsPathManagerCompatible) {
				return false;
			}

			if (viewOnly && !Options.vehicleRestrictionsOverlay && TrafficManagerTool.GetToolMode() != ToolMode.VehicleRestrictions)
				return false;

			Vector3 center = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_bounds.center;

			var screenPos = Camera.main.WorldToScreenPoint(center);
			screenPos.y = Screen.height - screenPos.y;
			if (screenPos.z < 0)
				return false;
			var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
			var diff = center - camPos;

			if (diff.magnitude > TrafficManagerTool.PriorityCloseLod)
				return false; // do not draw if too distant

			int numDirections;
			int numLanes = TrafficManagerTool.GetSegmentNumVehicleLanes(segmentId, null, out numDirections);

			// draw vehicle restrictions over each lane
			NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
			Vector3 yu = (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endDirection - Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startDirection).normalized;
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None)
				yu = -yu;
			Vector3 xu = Vector3.Cross(yu, new Vector3(0, 1f, 0)).normalized;
			float f = viewOnly ? 4f : 7f;
			ItemClass connectionClass = segmentInfo.GetConnectionClass();
			int maxNumSigns = 0;
			if (connectionClass.m_service == ItemClass.Service.Road)
				maxNumSigns = roadVehicleTypes.Length;
			else if (connectionClass.m_service == ItemClass.Service.PublicTransport && connectionClass.m_subService == ItemClass.SubService.PublicTransportTrain)
				maxNumSigns = railVehicleTypes.Length;
			//Vector3 zero = center - 0.5f * (float)(numLanes + numDirections - 1) * f * (xu + yu); // "bottom left"
			Vector3 zero = center - 0.5f * (float)(numLanes - 1 + numDirections - 1) * f * xu - 0.5f * (float)maxNumSigns * f * yu; // "bottom left"

			/*if (!viewOnly)
				Log._Debug($"xu: {xu.ToString()} yu: {yu.ToString()} center: {center.ToString()} zero: {zero.ToString()} numLanes: {numLanes} numDirections: {numDirections}");*/

			uint x = 0;
			var guiColor = GUI.color;
			List<object[]> sortedLanes = TrafficManagerTool.GetSortedVehicleLanes(segmentId, segmentInfo, null);
			bool hovered = false;
			HashSet<NetInfo.Direction> directions = new HashSet<NetInfo.Direction>();
			foreach (object[] laneData in sortedLanes) {
				uint laneId = (uint)laneData[0];
				uint laneIndex = (uint)laneData[2];

				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
				if (!directions.Contains(laneInfo.m_direction)) {
					if (directions.Count > 0)
						++x; // space between different directions
					directions.Add(laneInfo.m_direction);
				}

				ExtVehicleType[] possibleVehicleTypes = null;
				if (VehicleRestrictionsManager.IsRoadLane(laneInfo)) {
					possibleVehicleTypes = roadVehicleTypes;
				} else if (VehicleRestrictionsManager.IsRailLane(laneInfo)) {
					possibleVehicleTypes = railVehicleTypes;
				} else {
					++x;
					continue;
				}

				ExtVehicleType allowedTypes = VehicleRestrictionsManager.GetAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo);

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
					bool allowed = VehicleRestrictionsManager.IsAllowed(allowedTypes, vehicleType);
					if (allowed && viewOnly)
						continue; // do not draw allowed vehicles in view-only mode

					bool hoveredHandle;
					DrawRestrictionsSign(viewOnly, camPos, out diff, xu, yu, f, zero, x, y, ref guiColor, TrafficLightToolTextureResources.VehicleRestrictionTextures[vehicleType][allowed], out hoveredHandle);
					if (hoveredHandle)
						hovered = true;

					if (hoveredHandle && MainTool.CheckClicked()) {
						// toggle vehicle restrictions
						//Log._Debug($"Setting vehicle restrictions of segment {segmentId}, lane idx {laneIndex}, {vehicleType.ToString()} to {!allowed}");
						VehicleRestrictionsManager.ToggleAllowedType(segmentId, segmentInfo, laneIndex, laneId, laneInfo, vehicleType, !allowed);
					}

					++y;
				}

				++x;
			}

			guiColor.a = 1f;
			GUI.color = guiColor;

			return hovered;
		}

		private void DrawRestrictionsSign(bool viewOnly, Vector3 camPos, out Vector3 diff, Vector3 xu, Vector3 yu, float f, Vector3 zero, uint x, uint y, ref Color guiColor, Texture2D signTexture, out bool hoveredHandle) {
			Vector3 signCenter = zero + f * (float)x * xu + f * (float)y * yu; // in game coordinates

			var signScreenPos = Camera.main.WorldToScreenPoint(signCenter);
			signScreenPos.y = Screen.height - signScreenPos.y;
			diff = signCenter - camPos;

			var zoom = 1.0f / diff.magnitude * 100f * MainTool.GetBaseZoom();
			var size = (viewOnly ? 0.8f : 1f) * vehicleRestrictionsSignSize * zoom;

			var boundingBox = new Rect(signScreenPos.x - size / 2, signScreenPos.y - size / 2, size, size);
			hoveredHandle = !viewOnly && TrafficManagerTool.IsMouseOver(boundingBox);
			if (hoveredHandle) {
				// mouse hovering over sign
				guiColor.a = 0.8f;
			} else {
				guiColor.a = 0.5f;
			}

			GUI.color = guiColor;
			GUI.DrawTexture(boundingBox, signTexture);
		}
	}
}
