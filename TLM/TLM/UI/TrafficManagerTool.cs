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
using TrafficManager.Util;
using TrafficManager.UI.MainMenu;
using CSUtil.Commons;
using static TrafficManager.Manager.TrafficMeasurementManager;
using static TrafficManager.Manager.VehicleRestrictionsManager;

namespace TrafficManager.UI {
	[UsedImplicitly]
	public class TrafficManagerTool : DefaultTool {
		public struct NodeVisitItem {
			public ushort nodeId;
			public bool startNode;

			public NodeVisitItem(ushort nodeId, bool startNode) {
				this.nodeId = nodeId;
				this.startNode = startNode;
			}
		}

		private ToolMode _toolMode;

		internal static ushort HoveredNodeId;
		internal static ushort HoveredSegmentId;

		private static bool mouseClickProcessed;

		public static readonly float DebugCloseLod = 300f;
		public static readonly float MaxOverlayDistance = 450f;

		private IDictionary<ToolMode, SubTool> subTools = new TinyDictionary<ToolMode, SubTool>();

		public static ushort SelectedNodeId { get; internal set; }

		public static ushort SelectedSegmentId { get; internal set; }

        public static TransportDemandViewMode CurrentTransportDemandViewMode { get; internal set; } = TransportDemandViewMode.Outgoing;

        internal static ExtVehicleType[] InfoSignsToDisplay = new ExtVehicleType[] { ExtVehicleType.Bicycle, ExtVehicleType.Bus, ExtVehicleType.Taxi, ExtVehicleType.Tram, ExtVehicleType.CargoTruck, ExtVehicleType.Service, ExtVehicleType.RailVehicle };

		private static SubTool activeSubTool = null;

		static TrafficManagerTool() {
			
		}

		internal ToolController GetToolController() {
			return m_toolController;
		}

		internal static Rect MoveGUI(Rect rect) {
			return new Rect(85f + 50f + MainMenuPanel.MENU_WIDTH + rect.x, 60f + 20f + rect.y, rect.width, rect.height);
		}

		internal bool IsNodeWithinViewDistance(ushort nodeId) {
			bool ret = false;
			Constants.ServiceFactory.NetService.ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
				ret = IsPosWithinOverlayDistance(ref node.m_position);
				return true;
			});
			return ret;
		}

		internal bool IsSegmentWithinViewDistance(ushort segmentId) {
			bool ret = false;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				Vector3 centerPos = segment.m_bounds.center;
				ret = IsPosWithinOverlayDistance(ref centerPos);
				return true;
			});
			return ret;
		}

		internal bool IsPosWithinOverlayDistance(ref Vector3 position) {
			return (position - Singleton<SimulationManager>.instance.m_simulationView.m_position).magnitude <= TrafficManagerTool.MaxOverlayDistance;
		}

		internal static float AdaptWidth(float originalWidth) {
			return originalWidth;
			//return originalWidth * ((float)Screen.currentResolution.width / 1920f);
		}

		internal float GetBaseZoom() {
			return (float)Screen.currentResolution.height / 1200f;
		}

		internal void Initialize() {
			Log.Info("TrafficManagerTool: Initialization running now.");
			subTools.Clear();
			subTools[ToolMode.SwitchTrafficLight] = new ToggleTrafficLightsTool(this);
			subTools[ToolMode.AddPrioritySigns] = new PrioritySignsTool(this);
			subTools[ToolMode.ManualSwitch] = new ManualTrafficLightsTool(this);
			SubTool timedLightsTool = new TimedTrafficLightsTool(this);
			subTools[ToolMode.TimedLightsAddNode] = timedLightsTool;
			subTools[ToolMode.TimedLightsRemoveNode] = timedLightsTool;
			subTools[ToolMode.TimedLightsSelectNode] = timedLightsTool;
			subTools[ToolMode.TimedLightsShowLights] = timedLightsTool;
			subTools[ToolMode.TimedLightsCopyLights] = timedLightsTool;
			subTools[ToolMode.VehicleRestrictions] = new VehicleRestrictionsTool(this);
			subTools[ToolMode.SpeedLimits] = new SpeedLimitsTool(this);
			subTools[ToolMode.LaneChange] = new LaneArrowTool(this);
			subTools[ToolMode.LaneConnector] = new LaneConnectorTool(this);
			subTools[ToolMode.JunctionRestrictions] = new JunctionRestrictionsTool(this);
			subTools[ToolMode.ParkingRestrictions] = new ParkingRestrictionsTool(this);

			InitializeSubTools();

			SetToolMode(ToolMode.None);

			Log.Info("TrafficManagerTool: Initialization completed.");
		}

		internal void InitializeSubTools() {
			foreach (KeyValuePair<ToolMode, SubTool> e in subTools) {
				e.Value.Initialize();
			}
		}

		protected override void Awake() {
			Log._Debug($"TrafficLightTool: Awake {this.GetHashCode()}");
			base.Awake();
		}

		public SubTool GetSubTool(ToolMode mode) {
			SubTool ret;
			if (subTools.TryGetValue(mode, out ret)) {
				return ret;
			}
			return null;
		}
		
		public ToolMode GetToolMode() {
			return _toolMode;
		}

		public void SetToolMode(ToolMode mode) {
			Log._Debug($"SetToolMode: {mode}");
			
			if (mode == ToolMode.None) {
#if DEBUG
				DebugMenuPanel.deactivateButtons();
#endif
			}

			bool toolModeChanged = (mode != _toolMode);
			var oldToolMode = _toolMode;
			SubTool oldSubTool = null;
			subTools.TryGetValue(oldToolMode, out oldSubTool);
			_toolMode = mode;
			if (!subTools.TryGetValue(_toolMode, out activeSubTool)) {
				activeSubTool = null;
			}

			if (oldSubTool != null) {
				if ((oldToolMode == ToolMode.TimedLightsSelectNode || oldToolMode == ToolMode.TimedLightsShowLights || oldToolMode == ToolMode.TimedLightsAddNode || oldToolMode == ToolMode.TimedLightsRemoveNode || oldToolMode == ToolMode.TimedLightsCopyLights)) { // TODO refactor to SubToolMode
					if (mode != ToolMode.TimedLightsSelectNode && mode != ToolMode.TimedLightsShowLights && mode != ToolMode.TimedLightsAddNode && mode != ToolMode.TimedLightsRemoveNode && mode != ToolMode.TimedLightsCopyLights)
						oldSubTool.Cleanup();
				} else
					oldSubTool.Cleanup();
			}

			if (toolModeChanged && activeSubTool != null) {
				if ((oldToolMode == ToolMode.TimedLightsSelectNode || oldToolMode == ToolMode.TimedLightsShowLights || oldToolMode == ToolMode.TimedLightsAddNode || oldToolMode == ToolMode.TimedLightsRemoveNode || oldToolMode == ToolMode.TimedLightsCopyLights)) { // TODO refactor to SubToolMode
					if (mode != ToolMode.TimedLightsSelectNode && mode != ToolMode.TimedLightsShowLights && mode != ToolMode.TimedLightsAddNode && mode != ToolMode.TimedLightsRemoveNode && mode != ToolMode.TimedLightsCopyLights)
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
			foreach (KeyValuePair<ToolMode, SubTool> e in subTools) {
				e.Value.Cleanup();
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

			if (!this.isActiveAndEnabled) {
				return;
			}

			if (activeSubTool != null) {
				//Log._Debug($"Rendering overlay in {_toolMode}");
				activeSubTool.RenderOverlay(cameraInfo);
			}

			foreach (KeyValuePair<ToolMode, SubTool> e in subTools) {
				if (e.Key == GetToolMode())
					continue;
				e.Value.RenderInfoOverlay(cameraInfo);
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
			if (LoadingExtension.BaseUI.GetMenu().containsMouse
#if DEBUG
				|| LoadingExtension.BaseUI.GetDebugMenu().containsMouse
#endif
				) {
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

				foreach (KeyValuePair<ToolMode, SubTool> en in subTools) {
					en.Value.ShowGUIOverlay(en.Key != GetToolMode());
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

		public void DrawNodeCircle(RenderManager.CameraInfo cameraInfo, ushort nodeId, bool warning=false, bool alpha=false) {
			DrawNodeCircle(cameraInfo, nodeId, GetToolColor(warning, false), alpha);
		}

		public void DrawNodeCircle(RenderManager.CameraInfo cameraInfo, ushort nodeId, Color color, bool alpha = false) {
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

		public void DrawStaticSquareOverlayGridTexture(Texture2D texture, ref Vector3 camPos, ref Vector3 gridOrigin, float cellSize, ref Vector3 xu, ref Vector3 yu, uint x, uint y,
			float size, float alpha) {
			DrawGenericSquareOverlayGridTexture(texture, ref camPos, ref gridOrigin, cellSize, ref xu, ref yu, x, y, size, false, alpha);
		}

		public bool DrawHoverableSquareOverlayGridTexture(Texture2D texture, ref Vector3 camPos, ref Vector3 gridOrigin, float cellSize, ref Vector3 xu, ref Vector3 yu, uint x, uint y,
			float size, float defaultAlpha, float hoverAlpha) {
			return DrawGenericSquareOverlayGridTexture(texture, ref camPos, ref gridOrigin, cellSize, ref xu, ref yu, x, y, size, true, defaultAlpha, hoverAlpha);
		}

		public bool DrawGenericSquareOverlayGridTexture(Texture2D texture, ref Vector3 camPos, ref Vector3 gridOrigin, float cellSize, ref Vector3 xu, ref Vector3 yu, uint x, uint y,
			float size, bool canHover, float defaultAlpha, float? hoverAlpha = null) {
			return DrawGenericOverlayGridTexture(texture, ref camPos, ref gridOrigin, cellSize, cellSize, ref xu, ref yu, x, y, size, size, canHover, defaultAlpha, hoverAlpha);
		}

		public void DrawStaticOverlayGridTexture(Texture2D texture, ref Vector3 camPos, ref Vector3 gridOrigin, float cellWidth, float cellHeight, ref Vector3 xu, ref Vector3 yu, uint x, uint y,
			float width, float height, float alpha) {
			DrawGenericOverlayGridTexture(texture, ref camPos, ref gridOrigin, cellWidth, cellHeight, ref xu, ref yu, x, y, width, height, false, alpha);
		}

		public bool DrawHoverableOverlayGridTexture(Texture2D texture, ref Vector3 camPos, ref Vector3 gridOrigin, float cellWidth, float cellHeight, ref Vector3 xu, ref Vector3 yu, uint x, uint y,
			float width, float height, float defaultAlpha, float hoverAlpha) {
			return DrawGenericOverlayGridTexture(texture, ref camPos, ref gridOrigin, cellWidth, cellHeight, ref xu, ref yu, x, y, width, height, true, defaultAlpha, hoverAlpha);
		}

		public bool DrawGenericOverlayGridTexture(Texture2D texture, ref Vector3 camPos, ref Vector3 gridOrigin, float cellWidth, float cellHeight, ref Vector3 xu, ref Vector3 yu, uint x, uint y,
			float width, float height, bool canHover, float defaultAlpha, float? hoverAlpha = null) {
			Vector3 worldPos = gridOrigin + cellWidth * (float)x * xu + cellHeight * (float)y * yu; // grid position in game coordinates
			return DrawGenericOverlayTexture(texture, ref camPos, ref worldPos, width, height, canHover, defaultAlpha, hoverAlpha);
		}

		public void DrawStaticSquareOverlayTexture(Texture2D texture, ref Vector3 camPos, ref Vector3 worldPos, float size, float alpha) {
			DrawGenericOverlayTexture(texture, ref camPos, ref worldPos, size, size, false, alpha);
		}

		public bool DrawHoverableSquareOverlayTexture(Texture2D texture, ref Vector3 camPos, ref Vector3 worldPos, float size, float defaultAlpha, float hoverAlpha) {
			return DrawGenericOverlayTexture(texture, ref camPos, ref worldPos, size, size, true, defaultAlpha, hoverAlpha);
		}

		public bool DrawGenericSquareOverlayTexture(Texture2D texture, ref Vector3 camPos, ref Vector3 worldPos, float size, bool canHover, float defaultAlpha, float? hoverAlpha=null) {
			return DrawGenericOverlayTexture(texture, ref camPos, ref worldPos, size, size, canHover, defaultAlpha, hoverAlpha);
		}

		public void DrawStaticOverlayTexture(Texture2D texture, ref Vector3 camPos, ref Vector3 worldPos, float width, float height, float alpha) {
			DrawGenericOverlayTexture(texture, ref camPos, ref worldPos, width, height, false, alpha);
		}

		public bool DrawHoverableOverlayTexture(Texture2D texture, ref Vector3 camPos, ref Vector3 worldPos, float width, float height, float defaultAlpha, float hoverAlpha) {
			return DrawGenericOverlayTexture(texture, ref camPos, ref worldPos, width, height, true, defaultAlpha, hoverAlpha);
		}

		public bool DrawGenericOverlayTexture(Texture2D texture, ref Vector3 camPos, ref Vector3 worldPos, float width, float height, bool canHover, float defaultAlpha, float? hoverAlpha=null) {
			Vector3 screenPos;
			if (! WorldToScreenPoint(ref worldPos, out screenPos)) {
				return false;
			}

			float zoom = 1.0f / (worldPos - camPos).magnitude * 100f * GetBaseZoom();
			width *= zoom;
			height *= zoom;

			Rect boundingBox = new Rect(screenPos.x - width / 2f, screenPos.y - height / 2f, width, height);

			Color guiColor = GUI.color;
			guiColor.a = defaultAlpha;

			bool hovered = false;
			if (canHover) {
				hovered = IsMouseOver(boundingBox);
				if (hovered && hoverAlpha != null) {
					guiColor.a = (float)hoverAlpha;
				}
			}

			GUI.color = guiColor;
			GUI.DrawTexture(boundingBox, texture);

			return hovered;
		}

		public bool WorldToScreenPoint(ref Vector3 worldPos, out Vector3 screenPos) {
			screenPos = Camera.main.WorldToScreenPoint(worldPos);
			screenPos.y = Screen.height - screenPos.y;

			return screenPos.z >= 0;
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
				nodeInput.m_ignoreNodeFlags = NetNode.Flags.None;
				//nodeInput.m_ignoreNodeFlags = NetNode.Flags.Untouchable;

				RaycastOutput nodeOutput;
				if (RayCast(nodeInput, out nodeOutput)) {
					HoveredNodeId = nodeOutput.m_netNode;
				} else {
					// find train nodes
					nodeInput.m_netService.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
					nodeInput.m_netService.m_service = ItemClass.Service.PublicTransport;
					nodeInput.m_netService.m_subService = ItemClass.SubService.PublicTransportTrain;
					nodeInput.m_ignoreTerrain = true;
					nodeInput.m_ignoreNodeFlags = NetNode.Flags.None;
					//nodeInput.m_ignoreNodeFlags = NetNode.Flags.Untouchable;

					if (RayCast(nodeInput, out nodeOutput)) {
						HoveredNodeId = nodeOutput.m_netNode;
					} else {
						// find metro nodes
						nodeInput.m_netService.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
						nodeInput.m_netService.m_service = ItemClass.Service.PublicTransport;
						nodeInput.m_netService.m_subService = ItemClass.SubService.PublicTransportMetro;
						nodeInput.m_ignoreTerrain = true;
						nodeInput.m_ignoreNodeFlags = NetNode.Flags.None;
						//nodeInput.m_ignoreNodeFlags = NetNode.Flags.Untouchable;

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
				segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.None;
				//segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.Untouchable;

				RaycastOutput segmentOutput;
				if (RayCast(segmentInput, out segmentOutput)) {
					HoveredSegmentId = segmentOutput.m_netSegment;
				} else {
					// find train segments
					segmentInput.m_netService.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
					segmentInput.m_netService.m_service = ItemClass.Service.PublicTransport;
					segmentInput.m_netService.m_subService = ItemClass.SubService.PublicTransportTrain;
					segmentInput.m_ignoreTerrain = true;
					segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.None;
					//segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.Untouchable;

					if (RayCast(segmentInput, out segmentOutput)) {
						HoveredSegmentId = segmentOutput.m_netSegment;
					} else {
						// find metro segments
						segmentInput.m_netService.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
						segmentInput.m_netService.m_service = ItemClass.Service.PublicTransport;
						segmentInput.m_netService.m_subService = ItemClass.SubService.PublicTransportMetro;
						segmentInput.m_ignoreTerrain = true;
						segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.None;
						//segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.Untouchable;

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
			bool laneTrafficDataLoaded = TrafficMeasurementManager.Instance.GetLaneTrafficData(segmentId, segmentInfo, out laneTrafficData);

			uint curLaneId = segment.m_lanes;
			String labelStr = "";
			for (int i = 0; i < segmentInfo.m_lanes.Length; ++i) {
				if (curLaneId == 0)
					break;

				NetInfo.Lane laneInfo = segmentInfo.m_lanes[i];

				uint pfTrafficBuf = TrafficMeasurementManager.Instance.segmentDirTrafficData[TrafficMeasurementManager.Instance.GetDirIndex(segmentId, laneInfo.m_finalDirection)].totalPathFindTrafficBuffer;
				//TrafficMeasurementManager.Instance.GetTrafficData(segmentId, laneInfo.m_finalDirection, out dirTrafficData);

				//int dirIndex = laneInfo.m_finalDirection == NetInfo.Direction.Backward ? 1 : 0;

				labelStr += "Lane idx " + i + ", id " + curLaneId;
#if DEBUG
				labelStr += ", inner: " + RoutingManager.Instance.CalcInnerLaneSimilarIndex(segmentId, i) + ", outer: " + RoutingManager.Instance.CalcOuterLaneSimilarIndex(segmentId, i) + ", flags: " + ((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_flags).ToString() + ", limit: " + SpeedLimitManager.Instance.GetCustomSpeedLimit(curLaneId) + " km/h, restr: " + VehicleRestrictionsManager.Instance.GetAllowedVehicleTypes(segmentId, segmentInfo, (uint)i, laneInfo, RestrictionMode.Configured) + ", dir: " + laneInfo.m_direction + ", final: " + laneInfo.m_finalDirection + ", pos: " + String.Format("{0:0.##}", laneInfo.m_position) + ", sim. idx: " + laneInfo.m_similarLaneIndex + " for " + laneInfo.m_vehicleType + "/" + laneInfo.m_laneType;
#endif
				if (laneTrafficDataLoaded) {
					labelStr += ", avg. speed: " + (laneTrafficData[i].meanSpeed / 100) + "%";
#if DEBUG
					labelStr += ", buf: " + laneTrafficData[i].trafficBuffer + ", acc: " + laneTrafficData[i].accumulatedSpeeds;
					labelStr += ", pfBuf: " + laneTrafficData[i].pathFindTrafficBuffer + "/" + laneTrafficData[i].lastPathFindTrafficBuffer + ", (" + (pfTrafficBuf > 0 ? "" + ((laneTrafficData[i].lastPathFindTrafficBuffer * 100u) / pfTrafficBuf) : "n/a") + " %)";
#endif
#if MEASUREDENSITY
					if (dirTrafficDataLoaded) {
						labelStr += ", rel. dens.: " + (dirTrafficData.accumulatedDensities > 0 ? "" + Math.Min(laneTrafficData[i].accumulatedDensities * 100 / dirTrafficData.accumulatedDensities, 100) : "?") + "%";
					}
					labelStr += ", acc: " + laneTrafficData[i].accumulatedDensities;
#endif
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
			TrafficMeasurementManager trafficMeasurementManager = TrafficMeasurementManager.Instance;

			GUIStyle _counterStyle = new GUIStyle();
			SegmentEndManager endMan = SegmentEndManager.Instance;
			Array16<NetSegment> segments = Singleton<NetManager>.instance.m_segments;
			for (int i = 1; i < segments.m_size; ++i) {
				if (segments.m_buffer[i].m_flags == NetSegment.Flags.None) // segment is unused
					continue;
				ItemClass.Service service = segments.m_buffer[i].Info.GetService();
				ItemClass.SubService subService = segments.m_buffer[i].Info.GetSubService();
				/*if (service != ItemClass.Service.Road) {
					if (service != ItemClass.Service.PublicTransport) {
						continue;
					} else {
						if (subService != ItemClass.SubService.PublicTransportBus && subService != ItemClass.SubService.PublicTransportCableCar &&
							subService != ItemClass.SubService.PublicTransportMetro && subService != ItemClass.SubService.PublicTransportMonorail &&
							subService != ItemClass.SubService.PublicTransportTrain) {
							continue;
						}
					}
				}*/
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
#if DEBUG
				labelStr += ", flags: " + segments.m_buffer[i].m_flags.ToString(); // + ", condition: " + segments.m_buffer[i].m_condition;
#endif
#if DEBUG
				labelStr += "\nsvc: " + service + ", sub: " + subService;
				SegmentEnd startEnd = endMan.GetSegmentEnd((ushort)i, true);
				SegmentEnd endEnd = endMan.GetSegmentEnd((ushort)i, false);
				labelStr += "\nstart? " + (startEnd != null) + " veh.: " + startEnd?.GetRegisteredVehicleCount() + ", end? " + (endEnd != null) + " veh.: " + endEnd?.GetRegisteredVehicleCount();
#endif
				labelStr += "\nTraffic: " + segments.m_buffer[i].m_trafficDensity + " %";

#if DEBUG

				int fwdSegIndex = trafficMeasurementManager.GetDirIndex((ushort)i, NetInfo.Direction.Forward);
				int backSegIndex = trafficMeasurementManager.GetDirIndex((ushort)i, NetInfo.Direction.Backward);
				
				
				float fwdCongestionRatio = trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].numCongestionMeasurements > 0 ? ((uint)trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].numCongested * 100u) / (uint)trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].numCongestionMeasurements : 0; // now in %
				float backCongestionRatio = trafficMeasurementManager.segmentDirTrafficData[backSegIndex].numCongestionMeasurements > 0 ? ((uint)trafficMeasurementManager.segmentDirTrafficData[backSegIndex].numCongested * 100u) / (uint)trafficMeasurementManager.segmentDirTrafficData[backSegIndex].numCongestionMeasurements : 0; // now in %

				labelStr += "\nmin speeds: ";
				labelStr += " " + (trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].minSpeed / 100) + "%/" + (trafficMeasurementManager.segmentDirTrafficData[backSegIndex].minSpeed / 100) + "%";
				labelStr += ", mean speeds: ";
				labelStr += " " + (trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].meanSpeed / 100) + "%/" + (trafficMeasurementManager.segmentDirTrafficData[backSegIndex].meanSpeed / 100) + "%";
				labelStr += "\npf bufs: ";
				labelStr += " " + (trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].totalPathFindTrafficBuffer) + "/" + (trafficMeasurementManager.segmentDirTrafficData[backSegIndex].totalPathFindTrafficBuffer);
				labelStr += ", cong: ";
				labelStr += " " + fwdCongestionRatio + "% (" + trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].numCongested + "/" + trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].numCongestionMeasurements + ")/" + backCongestionRatio + "% (" + trafficMeasurementManager.segmentDirTrafficData[backSegIndex].numCongested + "/" + trafficMeasurementManager.segmentDirTrafficData[backSegIndex].numCongestionMeasurements + ")";
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
#if DEBUG
				labelStr += $"\nflags: {nodes.m_buffer[i].m_flags}";
				labelStr += $"\nlane: {nodes.m_buffer[i].m_lane}";
#endif
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
				/*PathUnit.Position curPos, nextPos;
				bool hasCurPos = vState?.GetCurrentPathPosition(ref vehicle, out curPos);
				bool hasNextPos = vState?.GetNextPathPosition(ref vehicle, out nextPos);*/
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
				String labelStr = "V #" + i + " is a " + (vState.Valid ? "valid" : "invalid") + " " + vState.VehicleType + " @ ~" + vehSpeed + " km/h (" + vState.JunctionTransitState + " @ " + vState.CurrentSegmentEnd?.SegmentId + ")\nd: " + driverInst?.InstanceId + " m: " + driverInst?.PathMode.ToString() + " f: " + driverInst?.FailedParkingAttempts + " l: " + driverInst?.ParkingSpaceLocation + " lid: " + driverInst?.ParkingSpaceLocationId;
#if USEPATHWAITCOUNTER
				labelStr += ", pwc: " + vState.PathWaitCounter + ", seg. " + vState.CurrentSegmentEnd?.SegmentId;
#endif
				//String labelStr = "Veh. " + i + " @ " + String.Format("{0:0.##}", vehSpeed) + "/" + (vState != null ? vState.CurrentMaxSpeed.ToString() : "-") + " (" + (vState != null ? vState.VehicleType.ToString() : "-") + ", valid? " + (vState != null ? vState.Valid.ToString() : "-") + ")" + ", len: " + (vState != null ? vState.TotalLength.ToString() : "-") + ", state: " + (vState != null ? vState.JunctionTransitState.ToString() : "-");
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

		new internal Color GetToolColor(bool warning, bool error) {
			return base.GetToolColor(warning, error);
		}

		internal static int GetSegmentNumVehicleLanes(ushort segmentId, ushort? nodeId, out int numDirections, VehicleInfo.VehicleType vehicleTypeFilter) {
			NetManager netManager = Singleton<NetManager>.instance;

			var info = netManager.m_segments.m_buffer[segmentId].Info;
			var curLaneId = netManager.m_segments.m_buffer[segmentId].m_lanes;
			var laneIndex = 0;

			NetInfo.Direction? dir = null;
			NetInfo.Direction? dir2 = null;
			//NetInfo.Direction? dir3 = null;

			numDirections = 0;
			HashSet<NetInfo.Direction> directions = new HashSet<NetInfo.Direction>();

			if (nodeId != null) {
				dir = (netManager.m_segments.m_buffer[segmentId].m_startNode == nodeId) ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;
				dir2 = ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection((NetInfo.Direction)dir);
				//dir3 = TrafficPriorityManager.IsLeftHandDrive() ? NetInfo.InvertDirection((NetInfo.Direction)dir2) : dir2;
			}

			var numLanes = 0;

			while (laneIndex < info.m_lanes.Length && curLaneId != 0u) {
				if (((info.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
					(info.m_lanes[laneIndex].m_vehicleType & vehicleTypeFilter) != VehicleInfo.VehicleType.None) &&
					(dir2 == null || info.m_lanes[laneIndex].m_finalDirection == dir2)) {

					if (!directions.Contains(info.m_lanes[laneIndex].m_finalDirection)) {
						directions.Add(info.m_lanes[laneIndex].m_finalDirection);
						++numDirections;
					}
					numLanes++;
				}

				curLaneId = netManager.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_nextLane;
				laneIndex++;
			}

			return numLanes;
		}
		
		internal static void CalculateSegmentCenterByDir(ushort segmentId, Dictionary<NetInfo.Direction, Vector3> segmentCenterByDir) {
			segmentCenterByDir.Clear();
			NetManager netManager = Singleton<NetManager>.instance;

			NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;
			uint curLaneId = netManager.m_segments.m_buffer[segmentId].m_lanes;
			Dictionary<NetInfo.Direction, int> numCentersByDir = new Dictionary<NetInfo.Direction, int>();
			uint laneIndex = 0;
			while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
				if ((segmentInfo.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == NetInfo.LaneType.None)
					goto nextIter;

				NetInfo.Direction dir = segmentInfo.m_lanes[laneIndex].m_finalDirection;
				Vector3 bezierCenter = netManager.m_lanes.m_buffer[curLaneId].m_bezier.Position(0.5f);

				if (!segmentCenterByDir.ContainsKey(dir)) {
					segmentCenterByDir[dir] = bezierCenter;
					numCentersByDir[dir] = 1;
				} else {
					segmentCenterByDir[dir] += bezierCenter;
					numCentersByDir[dir]++;
				}

				nextIter:

				curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
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

		public void ShowTooltip(String text) {
			if (text == null)
				return;

			UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Info", text, false);

			/*tooltipStartFrame = currentFrame;
			tooltipText = text;
			tooltipWorldPos = position;*/
		}
	}
}
