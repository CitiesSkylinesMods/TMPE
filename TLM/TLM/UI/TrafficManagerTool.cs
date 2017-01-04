#define MARKCONGESTEDSEGMENTS
#define USEPATHWAITCOUNTERx

using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using JetBrains.Annotations;
using TrafficManager.Custom.AI;
using TrafficManager.Geometry;
using TrafficManager.UI;
using UnityEngine;
using TrafficManager.State;
using TrafficManager.TrafficLight;
using TrafficManager.UI.SubTools;
using TrafficManager.Traffic;
using TrafficManager.Manager;

namespace TrafficManager.UI {
	[UsedImplicitly]
	public class TrafficManagerTool : DefaultTool {
		private static ToolMode _toolMode;

		internal static ushort HoveredNodeId;
		internal static ushort HoveredSegmentId;

		private static bool mouseClickProcessed;

		public static readonly float DebugCloseLod = 300f;
		public static readonly float PriorityCloseLod = 450f;

		private static SubTool[] subTools = new SubTool[13];
		private static bool initDone = false;

		public static ushort SelectedNodeId { get; internal set; }

		public static ushort SelectedSegmentId { get; internal set; }

        public static TransportDemandViewMode CurrentTransportDemandViewMode { get; internal set; } = TransportDemandViewMode.Outgoing;

        internal static ExtVehicleType[] InfoSignsToDisplay = new ExtVehicleType[] { ExtVehicleType.Bicycle, ExtVehicleType.Bus, ExtVehicleType.Taxi, ExtVehicleType.Tram, ExtVehicleType.CargoTruck, ExtVehicleType.Service };

		private static SubTool activeSubTool = null;

		static TrafficManagerTool() {
			
		}

		internal ToolController GetToolController() {
			return m_toolController;
		}

		internal static Rect MoveGUI(Rect rect) {
			/*var rectX = (rect.x / 800f) * (float)Screen.currentResolution.width;
			var rectY = (rect.y / 600f) * (float)Screen.currentResolution.height;*/

			//return new Rect(rectX, rectY, rect.width, rect.height);
			return new Rect(85f + (float)Translation.getMenuWidth() + 25f + AdaptWidth(rect.x), 80f + 10f + rect.y, AdaptWidth(rect.width), rect.height);
		}

		internal static float AdaptWidth(float originalWidth) {
			return originalWidth;
			//return originalWidth * ((float)Screen.currentResolution.width / 1920f);
		}

		internal float GetBaseZoom() {
			return (float)Screen.currentResolution.height / 1200f;
		}

		protected override void Awake() {
			Log._Debug($"TrafficLightTool: Awake {this.GetHashCode()}");
			base.Awake();

			if (!initDone) {
				Log.Info("TrafficManagerTool: Awake - Initialization running now.");
				subTools[(int)ToolMode.SwitchTrafficLight] = new ToggleTrafficLightsTool(this);
				subTools[(int)ToolMode.AddPrioritySigns] = new PrioritySignsTool(this);
				subTools[(int)ToolMode.ManualSwitch] = new ManualTrafficLightsTool(this);
				SubTool timedLightsTool = new TimedTrafficLightsTool(this);
				subTools[(int)ToolMode.TimedLightsAddNode] = timedLightsTool;
				subTools[(int)ToolMode.TimedLightsRemoveNode] = timedLightsTool;
				subTools[(int)ToolMode.TimedLightsSelectNode] = timedLightsTool;
				subTools[(int)ToolMode.TimedLightsShowLights] = timedLightsTool;
				subTools[(int)ToolMode.VehicleRestrictions] = new VehicleRestrictionsTool(this);
				subTools[(int)ToolMode.SpeedLimits] = new SpeedLimitsTool(this);
				subTools[(int)ToolMode.LaneChange] = new LaneArrowTool(this);
				subTools[(int)ToolMode.LaneConnector] = new LaneConnectorTool(this);
				subTools[(int)ToolMode.JunctionRestrictions] = new JunctionRestrictionsTool(this);

				for (int i = 0; i < subTools.Length; ++i) {
					if (subTools[i] == null)
						continue;
					subTools[i].Initialize();
				}

				Log.Info("TrafficManagerTool: Awake - Initialization completed.");
				initDone = true;
			} else {
				for (int i = 0; i < subTools.Length; ++i) {
					if (subTools[i] == null)
						continue;
					subTools[i].MainTool = this;
				}
			}
		}

		public static SubTool GetSubTool(ToolMode mode) {
			if (!initDone)
				return null;
			return subTools[(int)mode];
		}
		
		public static ToolMode GetToolMode() {
			return _toolMode;
		}

		public static void SetToolMode(ToolMode mode) {
			//Log._Debug($"SetToolMode: {mode}");
			
			if (mode == ToolMode.None) {
#if !TAM
				UITrafficManager.deactivateButtons();
#endif
			}

			bool toolModeChanged = (mode != _toolMode);
			var oldToolMode = _toolMode;
			var oldSubTool = subTools[(int)oldToolMode];
			_toolMode = mode;
			activeSubTool = subTools[(int)_toolMode];

			if (oldSubTool != null) {
				if ((oldToolMode == ToolMode.TimedLightsSelectNode || oldToolMode == ToolMode.TimedLightsShowLights || oldToolMode == ToolMode.TimedLightsAddNode || oldToolMode == ToolMode.TimedLightsRemoveNode)) { // TODO refactor to SubToolMode
					if (mode != ToolMode.TimedLightsSelectNode && mode != ToolMode.TimedLightsShowLights && mode != ToolMode.TimedLightsAddNode && mode != ToolMode.TimedLightsRemoveNode)
						oldSubTool.Cleanup();
				} else
					oldSubTool.Cleanup();
			}

			if (toolModeChanged && activeSubTool != null) {
				if ((oldToolMode == ToolMode.TimedLightsSelectNode || oldToolMode == ToolMode.TimedLightsShowLights || oldToolMode == ToolMode.TimedLightsAddNode || oldToolMode == ToolMode.TimedLightsRemoveNode)) { // TODO refactor to SubToolMode
					if (mode != ToolMode.TimedLightsSelectNode && mode != ToolMode.TimedLightsShowLights && mode != ToolMode.TimedLightsAddNode && mode != ToolMode.TimedLightsRemoveNode)
						activeSubTool.Cleanup();
				} else
					activeSubTool.Cleanup();
			}

			SelectedNodeId = 0;
			SelectedSegmentId = 0;

			//Log._Debug($"Getting activeSubTool for mode {_toolMode} {subTools.Count}");
			
			//subTools.TryGetValue((int)_toolMode, out activeSubTool);
			//Log._Debug($"activeSubTool is now {activeSubTool}");

			if (toolModeChanged && activeSubTool != null)
				activeSubTool.OnActivate();
		}

		// Overridden to disable base class behavior
		protected override void OnEnable() {
			Log._Debug($"TrafficManagerTool.OnEnable");
			for (int i = 0; i < subTools.Length; ++i) {
				if (subTools[i] == null)
					continue;
				subTools[i].Cleanup();
			}
		}

		// Overridden to disable base class behavior
		protected override void OnDisable() {
		}

		public override void RenderGeometry(RenderManager.CameraInfo cameraInfo) {
			if (HoveredNodeId != 0) {
				m_toolController.RenderCollidingNotifications(cameraInfo, 0, 0);
			}
		}

		/// <summary>
		///	Renders overlays (node selection, segment selection, etc.)
		/// </summary>
		/// <param name="cameraInfo"></param>
		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			//Log._Debug($"RenderOverlay");
			//Log._Debug($"RenderOverlay: {_toolMode} {activeSubTool} {this.GetHashCode()}");

			if (activeSubTool != null) {
				//Log._Debug($"Rendering overlay in {_toolMode}");
				activeSubTool.RenderOverlay(cameraInfo);
			}

			for (int i = 0; i < subTools.Length; ++i) {
				if (subTools[i] == null)
					continue;
				if (i == (int)GetToolMode())
					continue;
				subTools[i].RenderInfoOverlay(cameraInfo);
			}
		}

		/// <summary>
		/// Primarily handles click events on hovered nodes/segments
		/// </summary>
		protected override void OnToolUpdate() {
			base.OnToolUpdate();
			//Log._Debug($"OnToolUpdate");

			if (Input.GetKeyUp(KeyCode.PageDown)) {
				InfoManager.instance.SetCurrentMode(InfoManager.InfoMode.Traffic, InfoManager.SubInfoMode.Default);
				UIView.library.Hide("TrafficInfoViewPanel");
			} else if (Input.GetKeyUp(KeyCode.PageUp))
				InfoManager.instance.SetCurrentMode(InfoManager.InfoMode.None, InfoManager.SubInfoMode.Default);

			bool primaryMouseClicked = Input.GetMouseButtonDown(0);
			bool secondaryMouseClicked = Input.GetMouseButtonDown(1);

			// check if clicked
			if (!primaryMouseClicked && !secondaryMouseClicked)
				return;

			// check if mouse is inside panel
			if (UIBase.GetMenu().containsMouse) {
#if DEBUG
				Log._Debug($"TrafficManagerTool: OnToolUpdate: Menu contains mouse. Ignoring click.");
#endif
				return;
			}

			if (/*!elementsHovered || (*/activeSubTool != null && activeSubTool.IsCursorInPanel()/*)*/) {
#if DEBUG
				Log._Debug($"TrafficManagerTool: OnToolUpdate: Subtool contains mouse. Ignoring click.");
#endif
				//Log.Message("inside ui: " + m_toolController.IsInsideUI + " visible: " + Cursor.visible + " in secondary panel: " + _cursorInSecondaryPanel);
				return;
			}

			/*if (HoveredSegmentId == 0 && HoveredNodeId == 0) {
				//Log.Message("no hovered segment");
				return;
			}*/

			if (activeSubTool != null) {
				determineHoveredElements();

				if (primaryMouseClicked)
					activeSubTool.OnPrimaryClickOverlay();

				if (secondaryMouseClicked)
					activeSubTool.OnSecondaryClickOverlay();
			}
		}

		protected override void OnToolGUI(Event e) {
			try {
				if (!Input.GetMouseButtonDown(0)) {
					mouseClickProcessed = false;
				}

				if (Options.nodesOverlay) {
					_guiSegments();
					_guiNodes();
				}

//#if DEBUG
				if (Options.vehicleOverlay) {
					_guiVehicles();
				}

				if (Options.citizenOverlay) {
					_guiCitizens();
				}

				if (Options.buildingOverlay) {
					_guiBuildings();
				}
//#endif

				for (int i = 0; i < subTools.Length; ++i) {
					if (subTools[i] == null)
						continue;
					/*if (i == (int)GetToolMode())
						continue;*/
					subTools[i].ShowGUIOverlay(i != (int)GetToolMode());
				}

				var guiColor = GUI.color;
				guiColor.a = 0.9f;
				GUI.color = guiColor;

				if (activeSubTool != null)
					activeSubTool.OnToolGUI(e);
				else
					base.OnToolGUI(e);
			} catch (Exception ex) {
				Log.Error("GUI Error: " + ex.ToString());
			}
		}

		internal void DrawNodeCircle(RenderManager.CameraInfo cameraInfo, ushort nodeId, bool warning=false, bool alpha=false) {
			DrawNodeCircle(cameraInfo, nodeId, GetToolColor(warning, false), alpha);
		}

		internal void DrawNodeCircle(RenderManager.CameraInfo cameraInfo, ushort nodeId, Color color, bool alpha = false) {
			var segment = Singleton<NetManager>.instance.m_segments.m_buffer[Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_segment0];

			Vector3 pos = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position;
			float terrainY = Singleton<TerrainManager>.instance.SampleDetailHeightSmooth(pos);
			if (terrainY > pos.y)
				pos.y = terrainY;

			Bezier3 bezier;
			bezier.a = pos;
			bezier.d = pos;
			
			NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d, segment.m_endDirection, false, false, out bezier.b, out bezier.c);

			DrawOverlayBezier(cameraInfo, bezier, color, alpha);
		}

		private void DrawOverlayBezier(RenderManager.CameraInfo cameraInfo, Bezier3 bezier, Color color, bool alpha=false) {
			const float width = 8f; // 8 - small roads; 16 - big roads
			Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
			Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(cameraInfo, color, bezier, width * 2f, width, width, -1f, 1280f, false, alpha);
		}

		private void DrawOverlayCircle(RenderManager.CameraInfo cameraInfo, Color color, Vector3 position, float width, bool alpha) {
			Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
			Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(cameraInfo, color, position, width, position.y - 100f, position.y + 100f, false, alpha);
		}

		public override void SimulationStep() {
			base.SimulationStep();

			/*currentFrame = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 2;

			string displayToolTipText = tooltipText;
			if (displayToolTipText != null) {
				if (currentFrame <= tooltipStartFrame + 50) {
					ShowToolInfo(true, displayToolTipText, (Vector3)tooltipWorldPos);
				} else {
					//ShowToolInfo(false, tooltipText, (Vector3)tooltipWorldPos);
					//ShowToolInfo(false, null, Vector3.zero);
					tooltipStartFrame = 0;
					tooltipText = null;
					tooltipWorldPos = null;
				}
			}*/

			if (GetToolMode() == ToolMode.None) {
				ToolCursor = null;
			} else {
				bool elementsHovered = determineHoveredElements();

				var netTool = ToolsModifierControl.toolController.Tools.OfType<NetTool>().FirstOrDefault(nt => nt.m_prefab != null);

				if (netTool != null && elementsHovered) {
					ToolCursor = netTool.m_upgradeCursor;
				}
			}
		}

		public bool DoRayCast(RaycastInput input, out RaycastOutput output) {
			return RayCast(input, out output);
		}

		private bool determineHoveredElements() {
			var mouseRayValid = !UIView.IsInsideUI() && Cursor.visible && (activeSubTool == null || !activeSubTool.IsCursorInPanel());

			if (mouseRayValid) {
				ushort oldHoveredSegmentId = HoveredSegmentId;
				ushort oldHoveredNodeId = HoveredNodeId;

				HoveredSegmentId = 0;
				HoveredNodeId = 0;

				// find currently hovered node
				var nodeInput = new RaycastInput(this.m_mouseRay, this.m_mouseRayLength);
				// find road nodes
				nodeInput.m_netService.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
				nodeInput.m_netService.m_service = ItemClass.Service.Road;
				/*nodeInput.m_netService2.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.PublicTransport | ItemClass.Layer.MetroTunnels;
				nodeInput.m_netService2.m_service = ItemClass.Service.PublicTransport;
				nodeInput.m_netService2.m_subService = ItemClass.SubService.PublicTransportTrain;*/
				nodeInput.m_ignoreTerrain = true;
				nodeInput.m_ignoreNodeFlags = NetNode.Flags.Untouchable;

				RaycastOutput nodeOutput;
				if (RayCast(nodeInput, out nodeOutput)) {
					HoveredNodeId = nodeOutput.m_netNode;
				} else {
					// find train nodes
					nodeInput.m_netService.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
					nodeInput.m_netService.m_service = ItemClass.Service.PublicTransport;
					nodeInput.m_netService.m_subService = ItemClass.SubService.PublicTransportTrain;
					nodeInput.m_ignoreTerrain = true;
					nodeInput.m_ignoreNodeFlags = NetNode.Flags.Untouchable;

					if (RayCast(nodeInput, out nodeOutput)) {
						HoveredNodeId = nodeOutput.m_netNode;
					} else {
						// find metro nodes
						nodeInput.m_netService.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
						nodeInput.m_netService.m_service = ItemClass.Service.PublicTransport;
						nodeInput.m_netService.m_subService = ItemClass.SubService.PublicTransportMetro;
						nodeInput.m_ignoreTerrain = true;
						nodeInput.m_ignoreNodeFlags = NetNode.Flags.Untouchable;

						if (RayCast(nodeInput, out nodeOutput)) {
							HoveredNodeId = nodeOutput.m_netNode;
						}
					}
				}

				// find currently hovered segment
				var segmentInput = new RaycastInput(this.m_mouseRay, this.m_mouseRayLength);
				// find road segments
				segmentInput.m_netService.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
				segmentInput.m_netService.m_service = ItemClass.Service.Road;
				segmentInput.m_ignoreTerrain = true;
				segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.Untouchable;

				RaycastOutput segmentOutput;
				if (RayCast(segmentInput, out segmentOutput)) {
					HoveredSegmentId = segmentOutput.m_netSegment;
				} else {
					// find train segments
					segmentInput.m_netService.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
					segmentInput.m_netService.m_service = ItemClass.Service.PublicTransport;
					segmentInput.m_netService.m_subService = ItemClass.SubService.PublicTransportTrain;
					segmentInput.m_ignoreTerrain = true;
					segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.Untouchable;

					if (RayCast(segmentInput, out segmentOutput)) {
						HoveredSegmentId = segmentOutput.m_netSegment;
					} else {
						// find metro segments
						segmentInput.m_netService.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
						segmentInput.m_netService.m_service = ItemClass.Service.PublicTransport;
						segmentInput.m_netService.m_subService = ItemClass.SubService.PublicTransportMetro;
						segmentInput.m_ignoreTerrain = true;
						segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.Untouchable;

						if (RayCast(segmentInput, out segmentOutput)) {
							HoveredSegmentId = segmentOutput.m_netSegment;
						}
					}
				}

				if (HoveredNodeId <= 0 && HoveredSegmentId > 0) {
					// alternative way to get a node hit: check distance to start and end nodes of the segment
					ushort startNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId].m_startNode;
					ushort endNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId].m_endNode;

					float startDist = (segmentOutput.m_hitPos - Singleton<NetManager>.instance.m_nodes.m_buffer[startNodeId].m_position).magnitude;
					float endDist = (segmentOutput.m_hitPos - Singleton<NetManager>.instance.m_nodes.m_buffer[endNodeId].m_position).magnitude;
					if (startDist < endDist && startDist < 25f)
						HoveredNodeId = startNodeId;
					else if (endDist < startDist && endDist < 25f)
						HoveredNodeId = endNodeId;
				}

				/*if (oldHoveredNodeId != HoveredNodeId || oldHoveredSegmentId != HoveredSegmentId) {
					Log._Debug($"*** Mouse ray @ node {HoveredNodeId}, segment {HoveredSegmentId}, toolMode={GetToolMode()}");
                }*/

				return (HoveredNodeId != 0 || HoveredSegmentId != 0);
			} else {
				//Log._Debug($"Mouse ray invalid: {UIView.IsInsideUI()} {Cursor.visible} {activeSubTool == null} {activeSubTool.IsCursorInPanel()}");
            }

			return mouseRayValid;
		}
		
		/// <summary>
		/// Displays lane ids over lanes
		/// </summary>
		private void _guiLanes(ushort segmentId, ref NetSegment segment, ref NetInfo segmentInfo) {
			GUIStyle _counterStyle = new GUIStyle();
			Vector3 centerPos = segment.m_bounds.center;
			var screenPos = Camera.main.WorldToScreenPoint(centerPos);
			screenPos.y = Screen.height - screenPos.y - 200;

			if (screenPos.z < 0)
				return;

			var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
			var diff = centerPos - camPos;
			if (diff.magnitude > DebugCloseLod)
				return; // do not draw if too distant

			var zoom = 1.0f / diff.magnitude * 150f;

			_counterStyle.fontSize = (int)(11f * zoom);
			_counterStyle.normal.textColor = new Color(1f, 1f, 0f);

			/*uint totalDensity = 0u;
			for (int i = 0; i < segmentInfo.m_lanes.Length; ++i) {
				if (CustomRoadAI.currentLaneDensities[segmentId] != null && i < CustomRoadAI.currentLaneDensities[segmentId].Length)
					totalDensity += CustomRoadAI.currentLaneDensities[segmentId][i];
			}*/

			TrafficMeasurementManager.LaneTrafficData[] laneTrafficData;
			bool laneTrafficDataLoaded = TrafficMeasurementManager.Instance.GetTrafficData(segmentId, segmentInfo, out laneTrafficData);

			uint curLaneId = segment.m_lanes;
			String labelStr = "";
			for (int i = 0; i < segmentInfo.m_lanes.Length; ++i) {
				if (curLaneId == 0)
					break;

				NetInfo.Lane laneInfo = segmentInfo.m_lanes[i];
				TrafficMeasurementManager.SegmentDirTrafficData dirTrafficData;
				bool dirTrafficDataLoaded = TrafficMeasurementManager.Instance.GetTrafficData(segmentId, laneInfo.m_finalDirection, out dirTrafficData);

				//int dirIndex = laneInfo.m_finalDirection == NetInfo.Direction.Backward ? 1 : 0;

				labelStr += "Lane idx " + i + ", id " + curLaneId;
#if DEBUG
				labelStr += ", flags: " + ((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_flags).ToString() + ", limit: " + SpeedLimitManager.Instance.GetCustomSpeedLimit(curLaneId) + " km/h, dir: " + laneInfo.m_direction + ", final: " + laneInfo.m_finalDirection + ", pos: " + String.Format("{0:0.##}", laneInfo.m_position) + ", sim. idx: " + laneInfo.m_similarLaneIndex + " for " + laneInfo.m_vehicleType + "/" + laneInfo.m_laneType;
#endif
				if (laneTrafficDataLoaded) {
					labelStr += ", avg. speed: " + (laneTrafficData[i].meanSpeed / 100) + "% ";
					if (dirTrafficDataLoaded) {
						labelStr += ", rel. dens.: " + (dirTrafficData.accumulatedDensities > 0 ? "" + Math.Min(laneTrafficData[i].accumulatedDensities * 100 / dirTrafficData.accumulatedDensities, 100) : "?") + "%";
					}
				}
#if DEBUG
				//labelStr += " (" + (CustomRoadAI.currentLaneDensities[segmentId] != null && i < CustomRoadAI.currentLaneDensities[segmentId].Length ? "" + CustomRoadAI.currentLaneDensities[segmentId][i] : "?") + "/" + (CustomRoadAI.maxLaneDensities[segmentId] != null && i < CustomRoadAI.maxLaneDensities[segmentId].Length ? "" + CustomRoadAI.maxLaneDensities[segmentId][i] : "?") + "/" + totalDensity + ")";
				//labelStr += " (" + (CustomRoadAI.currentLaneDensities[segmentId] != null && i < CustomRoadAI.currentLaneDensities[segmentId].Length ? "" + CustomRoadAI.currentLaneDensities[segmentId][i] : "?") + "/" + totalDensity + ")";
#endif
				//labelStr += ", abs. dens.: " + (CustomRoadAI.laneMeanAbsDensities[segmentId] != null && i < CustomRoadAI.laneMeanAbsDensities[segmentId].Length ? "" + CustomRoadAI.laneMeanAbsDensities[segmentId][i] : "?") + " %";
				labelStr += "\n";

				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
			}
			
			Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
			Rect labelRect = new Rect(screenPos.x - dim.x / 2f, screenPos.y, dim.x, dim.y);

			GUI.Label(labelRect, labelStr, _counterStyle);
		}

		/// <summary>
		/// Displays segment ids over segments
		/// </summary>
		private void _guiSegments() {
			GUIStyle _counterStyle = new GUIStyle();
			TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;
			Array16<NetSegment> segments = Singleton<NetManager>.instance.m_segments;
			for (int i = 1; i < segments.m_size; ++i) {
				if (segments.m_buffer[i].m_flags == NetSegment.Flags.None) // segment is unused
					continue;
#if !DEBUG
				if ((segments.m_buffer[i].m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
					continue;
#endif
				var segmentInfo = segments.m_buffer[i].Info;

				Vector3 centerPos = segments.m_buffer[i].m_bounds.center;
				var screenPos = Camera.main.WorldToScreenPoint(centerPos);
				screenPos.y = Screen.height - screenPos.y;

				if (screenPos.z < 0)
					continue;

				var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
				var diff = centerPos - camPos;
				if (diff.magnitude > DebugCloseLod)
					continue; // do not draw if too distant
				
				var zoom = 1.0f / diff.magnitude * 150f;

				_counterStyle.fontSize = (int)(12f * zoom);
				_counterStyle.normal.textColor = new Color(1f, 0f, 0f);

				String labelStr = "Segment " + i;
#if DEBUGx
				labelStr += ", flags: " + segments.m_buffer[i].m_flags.ToString() + ", condition: " + segments.m_buffer[i].m_condition;
#endif
#if DEBUG
				SegmentEnd startEnd = prioMan.GetPrioritySegment(segments.m_buffer[i].m_startNode, (ushort)i);
				SegmentEnd endEnd = prioMan.GetPrioritySegment(segments.m_buffer[i].m_endNode, (ushort)i);
				labelStr += "\nstart? " + (startEnd != null) + " veh.: " + startEnd?.GetRegisteredVehicleCount() + ", end? " + (endEnd != null) + " veh.: " + endEnd?.GetRegisteredVehicleCount();
#endif
				labelStr += "\nTraffic: " + segments.m_buffer[i].m_trafficDensity + " %";

#if DEBUG
				TrafficMeasurementManager.SegmentDirTrafficData forwardTrafficData;
				TrafficMeasurementManager.SegmentDirTrafficData backwardTrafficData;
				if (TrafficMeasurementManager.Instance.GetTrafficData((ushort)i, NetInfo.Direction.Forward, out forwardTrafficData) &&
					TrafficMeasurementManager.Instance.GetTrafficData((ushort)i, NetInfo.Direction.Backward, out backwardTrafficData)) {
					labelStr += "\nmin speeds: ";
					labelStr += " " + (forwardTrafficData.minSpeed / 100) + "%/" + (backwardTrafficData.minSpeed / 100) + "%";
					labelStr += ", mean speeds: ";
					labelStr += " " + (forwardTrafficData.meanSpeed / 100) + "%/" + (backwardTrafficData.meanSpeed / 100) + "%";
				}
				labelStr += "\nstart: " + segments.m_buffer[i].m_startNode + ", end: " + segments.m_buffer[i].m_endNode;
#endif

				Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
				Rect labelRect = new Rect(screenPos.x - dim.x / 2f, screenPos.y, dim.x, dim.y);

				GUI.Label(labelRect, labelStr, _counterStyle);

				if (Options.showLanes)
					_guiLanes((ushort)i, ref segments.m_buffer[i], ref segmentInfo);
			}
		}

		/// <summary>
		/// Displays node ids over nodes
		/// </summary>
		private void _guiNodes() {
			GUIStyle _counterStyle = new GUIStyle();
			Array16<NetNode> nodes = Singleton<NetManager>.instance.m_nodes;
			for (int i = 1; i < nodes.m_size; ++i) {
				if ((nodes.m_buffer[i].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) // node is unused
					continue;

				Vector3 pos = nodes.m_buffer[i].m_position;
				var screenPos = Camera.main.WorldToScreenPoint(pos);
				screenPos.y = Screen.height - screenPos.y;

				if (screenPos.z < 0)
					continue;

				var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
				var diff = pos - camPos;
				if (diff.magnitude > DebugCloseLod)
					continue; // do not draw if too distant
				
				var zoom = 1.0f / diff.magnitude * 150f;

				_counterStyle.fontSize = (int)(15f * zoom);
				_counterStyle.normal.textColor = new Color(0f, 0f, 1f);

				String labelStr = "Node " + i;
				Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
				Rect labelRect = new Rect(screenPos.x - dim.x / 2f, screenPos.y, dim.x, dim.y);

				GUI.Label(labelRect, labelStr, _counterStyle);
			}
		}

		/// <summary>
		/// Displays vehicle ids over vehicles
		/// </summary>
		private void _guiVehicles() {
			GUIStyle _counterStyle = new GUIStyle();
			Array16<Vehicle> vehicles = Singleton<VehicleManager>.instance.m_vehicles;
			LaneConnectionManager connManager = LaneConnectionManager.Instance;
			SimulationManager simManager = Singleton<SimulationManager>.instance;
			NetManager netManager = Singleton<NetManager>.instance;
			VehicleStateManager vehStateManager = VehicleStateManager.Instance;
			for (int i = 1; i < vehicles.m_size; ++i) {
				Vehicle vehicle = vehicles.m_buffer[i];
				if (vehicle.m_flags == 0) // node is unused
					continue;

				Vector3 vehPos = vehicle.GetSmoothPosition((ushort)i);
				var screenPos = Camera.main.WorldToScreenPoint(vehPos);
				screenPos.y = Screen.height - screenPos.y;

				if (screenPos.z < 0)
					continue;

				var camPos = simManager.m_simulationView.m_position;
				var diff = vehPos - camPos;
				if (diff.magnitude > DebugCloseLod)
					continue; // do not draw if too distant

				var zoom = 1.0f / diff.magnitude * 150f;

				_counterStyle.fontSize = (int)(10f * zoom);
				_counterStyle.normal.textColor = new Color(1f, 1f, 1f);
				//_counterStyle.normal.background = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.4f));

				VehicleState vState = vehStateManager._GetVehicleState((ushort)i);
				ExtCitizenInstance driverInst = vState.GetDriverExtInstance();
				PathUnit.Position? curPos = vState?.GetCurrentPathPosition(ref vehicle);
				PathUnit.Position? nextPos = vState?.GetNextPathPosition(ref vehicle);
				bool? startNode = vState?.CurrentSegmentEnd?.StartNode;
				ushort? segmentId = vState?.CurrentSegmentEnd?.SegmentId;
				ushort? transitNodeId = vState?.CurrentSegmentEnd?.NodeId;
				/*float distanceToTransitNode = Single.NaN;
				float timeToTransitNode = Single.NaN;*/
				ushort vehSpeed = SpeedLimitManager.Instance.VehicleToCustomSpeed(vehicle.GetLastFrameVelocity().magnitude);

				Vector3? targetPos = null;
				if (transitNodeId != null)
					targetPos = netManager.m_nodes.m_buffer[(ushort)transitNodeId].m_position;

				/*if (transitNodeId != null && segmentId != null && startNode != null && curPos != null) {
					bool outgoing = false;
					connManager.GetLaneEndPoint((ushort)segmentId, (bool)startNode, ((PathUnit.Position)curPos).m_lane, null, null, out outgoing, out targetPos);
				}*/

				float distanceToTransitNode = Single.NaN;
				if (targetPos != null) {
					distanceToTransitNode = ((Vector3)targetPos - vehPos).magnitude;
					/*if (vehSpeed > 0)
						timeToTransitNode = distanceToTransitNode / vehSpeed;
					else
						timeToTransitNode = Single.PositiveInfinity;*/
				}
				String labelStr = "V #" + i + " is a " + (vState.Valid ? "valid" : "invalid") + " " + vState.VehicleType + " @ ~" + vehSpeed + " km/h (" + vState.JunctionTransitState + ")\nd: " + driverInst?.InstanceId + " m: " + driverInst?.PathMode.ToString() + " f: " + driverInst?.FailedParkingAttempts + " l: " + driverInst?.ParkingSpaceLocation + " lid: " + driverInst?.ParkingSpaceLocationId;
#if USEPATHWAITCOUNTER
				labelStr += ", pwc: " + vState.PathWaitCounter + ", seg. " + vState.CurrentSegmentEnd?.SegmentId;
#endif
				//String labelStr = "Veh. " + i + " @ " + String.Format("{0:0.##}", vehSpeed) + "/" + (vState != null ? vState.CurrentMaxSpeed.ToString() : "-") + " (" + (vState != null ? vState.VehicleType.ToString() : "-") + ", valid? " + (vState != null ? vState.Valid.ToString() : "-") + ")" + ", len: " + (vState != null ? vState.TotalLength.ToString() : "-") + ", state: " + (vState != null ? vState.JunctionTransitState.ToString() : "-");
#if PATHRECALC
				labelStr += ", recalc: " + (vState != null ? vState.LastPathRecalculation.ToString() : "-");
#endif
				//labelStr += "\npos: " + curPos?.m_segment + "(" + curPos?.m_lane + ")->" + nextPos?.m_segment + "(" + nextPos?.m_lane + ")" /* + ", dist: " + distanceToTransitNode + ", time: " + timeToTransitNode*/ + ", last update: " + vState?.LastPositionUpdate;

				Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
				Rect labelRect = new Rect(screenPos.x - dim.x / 2f, screenPos.y - dim.y - 50f, dim.x, dim.y);

				GUI.Box(labelRect, labelStr, _counterStyle);

				//_counterStyle.normal.background = null;
			}
		}

		private void _guiCitizens() {
			GUIStyle _counterStyle = new GUIStyle();
			Array16<CitizenInstance> citizenInstances = Singleton<CitizenManager>.instance.m_instances;
			for (int i = 1; i < citizenInstances.m_size; ++i) {
				CitizenInstance citizenInstance = citizenInstances.m_buffer[i];
				if (citizenInstance.m_flags == CitizenInstance.Flags.None)
					continue;
				if ((citizenInstance.m_flags & CitizenInstance.Flags.Character) == CitizenInstance.Flags.None)
					continue;

				Vector3 pos = citizenInstance.GetSmoothPosition((ushort)i);
				var screenPos = Camera.main.WorldToScreenPoint(pos);
				screenPos.y = Screen.height - screenPos.y;

				if (screenPos.z < 0)
					continue;

				var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
				var diff = pos - camPos;
				if (diff.magnitude > DebugCloseLod)
					continue; // do not draw if too distant

				var zoom = 1.0f / diff.magnitude * 150f;

				_counterStyle.fontSize = (int)(10f * zoom);
				_counterStyle.normal.textColor = new Color(1f, 0f, 1f);
				//_counterStyle.normal.background = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.4f));

				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance((ushort)i);

				String labelStr = "Inst. " + i + ", Cit. " + citizenInstance.m_citizen + ", m: " + extInstance.PathMode.ToString();
				
				Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
				Rect labelRect = new Rect(screenPos.x - dim.x / 2f, screenPos.y - dim.y - 50f, dim.x, dim.y);

				GUI.Box(labelRect, labelStr, _counterStyle);
			}
		}

		private void _guiBuildings() {
			GUIStyle _counterStyle = new GUIStyle();
			Array16<Building> buildings = Singleton<BuildingManager>.instance.m_buildings;
			for (int i = 1; i < buildings.m_size; ++i) {
				Building building = buildings.m_buffer[i];
				if (building.m_flags == Building.Flags.None)
					continue;

				Vector3 pos = building.m_position;
				var screenPos = Camera.main.WorldToScreenPoint(pos);
				screenPos.y = Screen.height - screenPos.y;

				if (screenPos.z < 0)
					continue;

				var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
				var diff = pos - camPos;
				if (diff.magnitude > DebugCloseLod)
					continue; // do not draw if too distant

				var zoom = 1.0f / diff.magnitude * 150f;

				_counterStyle.fontSize = (int)(10f * zoom);
				_counterStyle.normal.textColor = new Color(0f, 1f, 0f);
				//_counterStyle.normal.background = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.4f));

				ExtBuilding extBuilding = ExtBuildingManager.Instance.GetExtBuilding((ushort)i);

				String labelStr = "Building " + i + ", PDemand: " + extBuilding.ParkingSpaceDemand + ", IncTDem: " + extBuilding.IncomingPublicTransportDemand + ", OutTDem: " + extBuilding.OutgoingPublicTransportDemand;

				Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
				Rect labelRect = new Rect(screenPos.x - dim.x / 2f, screenPos.y - dim.y - 50f, dim.x, dim.y);

				GUI.Box(labelRect, labelStr, _counterStyle);
			}
		}

		internal static List<object[]> GetSortedVehicleLanes(ushort segmentId, NetInfo info, ushort? nodeId, VehicleInfo.VehicleType vehicleTypeFilter) { // TODO refactor together with getSegmentNumVehicleLanes, especially the vehicle type and lane type checks
			var laneList = new List<object[]>();

			NetInfo.Direction? dir = null;
			NetInfo.Direction? dir2 = null;
			//NetInfo.Direction? dir3 = null;
			if (nodeId != null) {
				dir = nodeId == Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startNode ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;
				dir2 = ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection((NetInfo.Direction)dir);
				//dir3 = TrafficPriority.IsLeftHandDrive() ? NetInfo.InvertDirection((NetInfo.Direction)dir2) : dir2;
			}

			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			uint laneIndex = 0;
			while (laneIndex < info.m_lanes.Length && curLaneId != 0u) {
				if ((info.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
					(info.m_lanes[laneIndex].m_vehicleType & vehicleTypeFilter) != VehicleInfo.VehicleType.None &&
					(dir2 == null || info.m_lanes[laneIndex].m_finalDirection == dir2)) {
					laneList.Add(new object[] { curLaneId, info.m_lanes[laneIndex].m_position, laneIndex });
				}

				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
				laneIndex++;
			}

			// sort lanes from left to right
			laneList.Sort(delegate (object[] x, object[] y) {
				if ((float)x[1] == (float)y[1])
					return 0;

				if ((dir2 == NetInfo.Direction.Forward) ^ (float)x[1] < (float)y[1]) {
					return 1;
				}
				return -1;
			});
			return laneList;
		}

		new internal Color GetToolColor(bool warning, bool error) {
			return base.GetToolColor(warning, error);
		}

		internal static int GetSegmentNumVehicleLanes(ushort segmentId, ushort? nodeId, out int numDirections, VehicleInfo.VehicleType vehicleTypeFilter) {
			var info = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
			var num2 = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			var laneIndex = 0;

			NetInfo.Direction? dir = null;
			NetInfo.Direction? dir2 = null;
			NetInfo.Direction? dir3 = null;

			numDirections = 0;
			HashSet<NetInfo.Direction> directions = new HashSet<NetInfo.Direction>();

			if (nodeId != null) {
				dir = (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startNode == nodeId) ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;
				dir2 = ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection((NetInfo.Direction)dir);
				dir3 = TrafficPriorityManager.IsLeftHandDrive() ? NetInfo.InvertDirection((NetInfo.Direction)dir2) : dir2;
			}

			var numLanes = 0;

			while (laneIndex < info.m_lanes.Length && num2 != 0u) {
				if (((info.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
					(info.m_lanes[laneIndex].m_vehicleType & vehicleTypeFilter) != VehicleInfo.VehicleType.None) &&
					(dir3 == null || info.m_lanes[laneIndex].m_direction == dir3)) {

					if (!directions.Contains(info.m_lanes[laneIndex].m_direction)) {
						directions.Add(info.m_lanes[laneIndex].m_direction);
						++numDirections;
					}
					numLanes++;
				}

				num2 = Singleton<NetManager>.instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
				laneIndex++;
			}

			return numLanes;
		}
		
		internal static void CalculateSegmentCenterByDir(ushort segmentId, Dictionary<NetInfo.Direction, Vector3> segmentCenterByDir) {
			segmentCenterByDir.Clear();

			NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			Dictionary<NetInfo.Direction, int> numCentersByDir = new Dictionary<NetInfo.Direction, int>();
			uint laneIndex = 0;
			while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
				if ((segmentInfo.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == NetInfo.LaneType.None)
					goto nextIter;

				NetInfo.Direction dir = segmentInfo.m_lanes[laneIndex].m_finalDirection;
				Vector3 bezierCenter = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_bezier.Position(0.5f);

				if (!segmentCenterByDir.ContainsKey(dir)) {
					segmentCenterByDir[dir] = bezierCenter;
					numCentersByDir[dir] = 1;
				} else {
					segmentCenterByDir[dir] += bezierCenter;
					numCentersByDir[dir]++;
				}

				nextIter:

				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
				laneIndex++;
			}

			foreach (KeyValuePair<NetInfo.Direction, int> e in numCentersByDir) {
				segmentCenterByDir[e.Key] /= (float)e.Value;
			}
		}

		public static Texture2D MakeTex(int width, int height, Color col) {
			var pix = new Color[width * height];

			for (var i = 0; i < pix.Length; i++)
				pix[i] = col;

			var result = new Texture2D(width, height);
			result.SetPixels(pix);
			result.Apply();

			return result;
		}
		
		private static int GetNumberOfMainRoads(ushort nodeId, ref NetNode node) {
			TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

			var numMainRoads = 0;
			for (var s = 0; s < 8; s++) {
				var segmentId2 = node.GetSegment(s);

				if (segmentId2 == 0 ||
					!prioMan.IsPrioritySegment(nodeId, segmentId2))
					continue;
				var prioritySegment2 = prioMan.GetPrioritySegment(nodeId,
					segmentId2);

				if (prioritySegment2.Type == SegmentEnd.PriorityType.Main) {
					numMainRoads++;
				}
			}
			return numMainRoads;
		}

		internal static bool IsMouseOver(Rect boundingBox) {
			return boundingBox.Contains(Event.current.mousePosition);
		}

		internal bool CheckClicked() {
			if (Input.GetMouseButtonDown(0) && !mouseClickProcessed) {
				mouseClickProcessed = true;
				return true;
			}
			return false;
		}

		internal void ShowTooltip(String text, Vector3 position) {
			if (text == null)
				return;

			UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Info", text, false);

			/*tooltipStartFrame = currentFrame;
			tooltipText = text;
			tooltipWorldPos = position;*/
		}
	}
}
