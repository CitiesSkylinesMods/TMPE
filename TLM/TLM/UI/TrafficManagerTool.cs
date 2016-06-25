using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using JetBrains.Annotations;
using TrafficManager.Custom.AI;
using TrafficManager.Traffic;
using TrafficManager.UI;
using UnityEngine;
using TrafficManager.State;
using TrafficManager.TrafficLight;
using TrafficManager.UI.SubTools;

namespace TrafficManager.UI {
	[UsedImplicitly]
	public class TrafficManagerTool : DefaultTool {
		private static ToolMode _toolMode;

		private bool _mouseDown;
		private bool _mouseClicked;

		internal ushort HoveredNodeId;
		internal ushort HoveredSegmentId;

		private bool mouseClickProcessed;

		public const float DebugCloseLod = 300f;
		public const float PriorityCloseLod = 450f;

		/*private uint tooltipStartFrame = 0;
		private String tooltipText = null;
		private Vector3? tooltipWorldPos = null;*/

		private static SubTool[] subTools = new SubTool[11];
		private static bool initDone = false;

		public static ushort SelectedNodeId { get; internal set; }

		public static ushort SelectedSegmentId { get; internal set; }

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
			return new Rect(85f + (float)Translation.getMenuWidth() + 50f + rect.x, 80f + 10f + rect.y, rect.width, rect.height);
		}

		internal float GetBaseZoom() {
			return (float)Screen.currentResolution.height / 1200f;
		}

		protected override void Awake() {
			//Log._Debug($"TrafficLightTool: Awake {this.GetHashCode()}");
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
			if (toolModeChanged && activeSubTool != null) {
				if ((_toolMode == ToolMode.TimedLightsSelectNode || _toolMode == ToolMode.TimedLightsShowLights || _toolMode == ToolMode.TimedLightsAddNode || _toolMode == ToolMode.TimedLightsRemoveNode)) { // TODO refactor to SubToolMode
					if (mode != ToolMode.TimedLightsSelectNode && mode != ToolMode.TimedLightsShowLights && mode != ToolMode.TimedLightsAddNode && mode != ToolMode.TimedLightsRemoveNode)
						activeSubTool.Cleanup();
				} else
					activeSubTool.Cleanup();
			}
			_toolMode = mode;

			SelectedNodeId = 0;
			SelectedSegmentId = 0;

			activeSubTool = null;
			//Log._Debug($"Getting activeSubTool for mode {_toolMode} {subTools.Count}");
			activeSubTool = subTools[(int)_toolMode];
			//subTools.TryGetValue((int)_toolMode, out activeSubTool);
			//Log._Debug($"activeSubTool is now {activeSubTool}");

			if (toolModeChanged && activeSubTool != null)
				activeSubTool.OnActivate();
		}

		// Overridden to disable base class behavior
		protected override void OnEnable() {
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

			_mouseDown = Input.GetMouseButton(0);

			if (_mouseDown) {
				if (_mouseClicked) return;

				_mouseClicked = true;

				bool elementsHovered = determineHoveredElements();

				if (!elementsHovered || (activeSubTool != null && activeSubTool.IsCursorInPanel())) {
					//Log.Message("inside ui: " + m_toolController.IsInsideUI + " visible: " + Cursor.visible + " in secondary panel: " + _cursorInSecondaryPanel);
					return;
				}
				if (HoveredSegmentId == 0 && HoveredNodeId == 0) {
					//Log.Message("no hovered segment");
					return;
				}

				if (activeSubTool != null)
					activeSubTool.OnClickOverlay();
			} else {
				//showTooltip(false, null, Vector3.zero);
				_mouseClicked = false;
			}
		}

		protected override void OnToolGUI(Event e) {
			//Log._Debug($"OnToolGUI");

			try {
				if (!Input.GetMouseButtonDown(0)) {
					mouseClickProcessed = false;
				}

				_guiSegments();
				if (Options.nodesOverlay) {
					_guiNodes();
#if DEBUG
					_guiVehicles();
					//_guiCitizens();
#endif
				}

				for (int i = 0; i < subTools.Length; ++i) {
					if (subTools[i] == null)
						continue;
					if (i == (int)GetToolMode())
						continue;
					subTools[i].ShowIcons();
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

		internal void DrawOverlayBezier(RenderManager.CameraInfo cameraInfo, Bezier3 bezier, Color color) {
			const float width = 8f; // 8 - small roads; 16 - big roads

			Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls = Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls + 1;
			Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(cameraInfo, color, bezier, width * 2f, width, width, -1f, 1280f, false, false);
		}

		internal void DrawOverlayCircle(RenderManager.CameraInfo cameraInfo, Color color, Vector3 position, float width, bool alpha) {
			var exprEaCp0 = Singleton<ToolManager>.instance;
			exprEaCp0.m_drawCallData.m_overlayCalls = exprEaCp0.m_drawCallData.m_overlayCalls + 1;
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

			uint totalDensity = 0u;
			uint curLaneId = segment.m_lanes;
			for (int i = 0; i < segmentInfo.m_lanes.Length; ++i) {
				if (curLaneId == 0)
					break;

				if (CustomRoadAI.currentLaneDensities[segmentId] != null && i < CustomRoadAI.currentLaneDensities[segmentId].Length)
					totalDensity += CustomRoadAI.currentLaneDensities[segmentId][i];

				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
			}

			curLaneId = segment.m_lanes;
			String labelStr = "";
			for (int i = 0; i < segmentInfo.m_lanes.Length; ++i) {
				if (curLaneId == 0)
					break;

				NetInfo.Lane laneInfo = segmentInfo.m_lanes[i];

				labelStr += "Lane idx " + i + ", id " + curLaneId;
#if DEBUG
				labelStr += ", flags: " + ((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_flags).ToString() + ", limit: " + SpeedLimitManager.GetCustomSpeedLimit(curLaneId) + " km/h, dir: " + laneInfo.m_direction + ", final: " + laneInfo.m_finalDirection + ", pos: " + String.Format("{0:0.##}", laneInfo.m_position) + ", sim. idx: " + laneInfo.m_similarLaneIndex + " for " + laneInfo.m_vehicleType + "/" + laneInfo.m_laneType;
#endif
				if (CustomRoadAI.InStartupPhase)
					labelStr += ", in start-up phase";
				else
					labelStr += ", avg. speed: " + (CustomRoadAI.laneMeanSpeeds[segmentId] != null && i < CustomRoadAI.laneMeanSpeeds[segmentId].Length ? ""+CustomRoadAI.laneMeanSpeeds[segmentId][i] : "?") + " %";
				labelStr += ", rel. density: " + (CustomRoadAI.laneMeanDensities[segmentId] != null && i < CustomRoadAI.laneMeanDensities[segmentId].Length ? "" + CustomRoadAI.laneMeanDensities[segmentId][i] : "?") + " %";
#if DEBUG
				labelStr += " (" + (CustomRoadAI.currentLaneDensities[segmentId] != null && i < CustomRoadAI.currentLaneDensities[segmentId].Length ? "" + CustomRoadAI.currentLaneDensities[segmentId][i] : "?") + "/" + totalDensity + ")";
#endif
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

				if (Options.nodesOverlay) {
					var zoom = 1.0f / diff.magnitude * 150f;

					_counterStyle.fontSize = (int)(12f * zoom);
					_counterStyle.normal.textColor = new Color(1f, 0f, 0f);

					String labelStr = "Segment " + i;
#if DEBUGx
					labelStr += ", flags: " + segments.m_buffer[i].m_flags.ToString() + ", condition: " + segments.m_buffer[i].m_condition;
#endif
#if DEBUG
					SegmentEnd startEnd = TrafficPriority.GetPrioritySegment(segments.m_buffer[i].m_startNode, (ushort)i);
					SegmentEnd endEnd = TrafficPriority.GetPrioritySegment(segments.m_buffer[i].m_endNode, (ushort)i);
					labelStr += "\nstart veh.: " + startEnd?.GetRegisteredVehicleCount() + ", end veh.: " + endEnd?.GetRegisteredVehicleCount();
#endif
					labelStr += "\nTraffic: " + segments.m_buffer[i].m_trafficDensity + " %";

					float meanLaneSpeed = 0f;
					
					int lIndex = 0;
					uint laneId = segments.m_buffer[i].m_lanes;
					int validLanes = 0;
					while (lIndex < segmentInfo.m_lanes.Length && laneId != 0u) {
						NetInfo.Lane lane = segmentInfo.m_lanes[lIndex];
						if (lane.CheckType(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car)) {
							if (CustomRoadAI.laneMeanSpeeds[i] != null && lIndex < CustomRoadAI.laneMeanSpeeds[i].Length) {
								if (CustomRoadAI.laneMeanSpeeds[i][lIndex] >= 0) {
									meanLaneSpeed += (float)CustomRoadAI.laneMeanSpeeds[i][lIndex];
									++validLanes;
								}
							}
						}
						lIndex++;
						laneId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_nextLane;
					}

					if (validLanes > 0) {
						meanLaneSpeed /= Convert.ToSingle(validLanes);
					}

					if (CustomRoadAI.InStartupPhase)
						labelStr += " (in start-up phase)";
					else
						labelStr += " (avg. speed: " + String.Format("{0:0.##}", meanLaneSpeed) + " %)";

#if DEBUG
					labelStr += "\nstart: " + segments.m_buffer[i].m_startNode + ", end: " + segments.m_buffer[i].m_endNode;
#endif

					Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
					Rect labelRect = new Rect(screenPos.x - dim.x / 2f, screenPos.y, dim.x, dim.y);

					GUI.Label(labelRect, labelStr, _counterStyle);

					if (Options.showLanes)
						_guiLanes((ushort)i, ref segments.m_buffer[i], ref segmentInfo);
				}
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
			for (int i = 1; i < vehicles.m_size; ++i) {
				Vehicle vehicle = vehicles.m_buffer[i];
				if (vehicle.m_flags == 0) // node is unused
					continue;

				Vector3 pos = vehicle.GetLastFramePosition();
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
				_counterStyle.normal.textColor = new Color(1f, 1f, 1f);
				//_counterStyle.normal.background = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.4f));

				VehicleState vState = VehicleStateManager.GetVehicleState((ushort)i);
				String labelStr = "Veh. " + i + /*" @ " + String.Format("{0:0.##}", vehicle.GetLastFrameVelocity().magnitude)*/ " (" + (vState != null ? vState.VehicleType.ToString() : "-") + ", valid? " + (vState != null ? vState.Valid.ToString() : "-") + ")" + ", len: " + (vState != null ? vState.TotalLength.ToString() : "-") + ", state: " + (vState != null ? vState.JunctionTransitState.ToString() : "-") + "\npos: " + vState?.GetCurrentPosition()?.SourceSegmentId + "(" + vState?.GetCurrentPosition()?.SourceLaneIndex + ")->" + vState?.GetCurrentPosition()?.TransitNodeId + ", last update: " + vState?.LastPositionUpdate;
				// add current path info
				/*var currentPathId = vehicle.m_path;
				if (currentPathId > 0) {
					var vehiclePathUnit = Singleton<PathManager>.instance.m_pathUnits.m_buffer[currentPathId];
					if ((vehiclePathUnit.m_pathFindFlags & PathUnit.FLAG_READY) != 0) {
						var realTimePosition = vehiclePathUnit.GetPosition(vehicle.m_pathPositionIndex >> 1);
						labelStr += "\n@ seg " + realTimePosition.m_segment + "\nlane " + realTimePosition.m_lane + "\noff " + realTimePosition.m_offset;
					}
				}*/

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

				Vector3 pos = citizenInstance.GetLastFramePosition();
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

				String labelStr = "Cit. " + i;
				
				Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
				Rect labelRect = new Rect(screenPos.x - dim.x / 2f, screenPos.y - dim.y - 50f, dim.x, dim.y);

				GUI.Box(labelRect, labelStr, _counterStyle);
			}
		}
		
		internal static List<object[]> GetSortedVehicleLanes(ushort segmentId, NetInfo info, ushort? nodeId) { // TODO refactor together with getSegmentNumVehicleLanes, especially the vehicle type and lane type checks
			var laneList = new List<object[]>();

			NetInfo.Direction? dir = null;
			NetInfo.Direction? dir2 = null;
			NetInfo.Direction? dir3 = null;
			if (nodeId != null) {
				dir = nodeId == Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startNode ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;
				dir2 = ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection((NetInfo.Direction)dir);
				dir3 = TrafficPriority.IsLeftHandDrive() ? NetInfo.InvertDirection((NetInfo.Direction)dir2) : dir2;
			}

			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			uint laneIndex = 0;
			while (laneIndex < info.m_lanes.Length && curLaneId != 0u) {
				if ((info.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
					(info.m_lanes[laneIndex].m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train)) != VehicleInfo.VehicleType.None &&
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

		internal static int GetSegmentNumVehicleLanes(ushort segmentId, ushort? nodeId, out int numDirections) {
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
				dir3 = TrafficPriority.IsLeftHandDrive() ? NetInfo.InvertDirection((NetInfo.Direction)dir2) : dir2;
			}

			var numLanes = 0;

			while (laneIndex < info.m_lanes.Length && num2 != 0u) {
				if (((info.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
					(info.m_lanes[laneIndex].m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train)) != VehicleInfo.VehicleType.None) &&
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
			var numMainRoads = 0;
			for (var s = 0; s < 8; s++) {
				var segmentId2 = node.GetSegment(s);

				if (segmentId2 == 0 ||
					!TrafficPriority.IsPrioritySegment(nodeId, segmentId2))
					continue;
				var prioritySegment2 = TrafficPriority.GetPrioritySegment(nodeId,
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
