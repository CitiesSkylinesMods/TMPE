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

namespace TrafficManager.UI {
	[UsedImplicitly]
	public class TrafficLightTool : DefaultTool {
		private static ToolMode _toolMode;

		private bool _mouseDown;
		private bool _mouseClicked;

		private ushort _hoveredNetNodeIdx;

		private ushort _hoveredSegmentIdx;

		public static List<ushort> SelectedNodeIndexes = new List<ushort>();
		private static List<ushort> _selectedSegmentIds = new List<ushort>();

		private readonly int[] _hoveredButton = new int[2];
		private ushort _hoveredNode;

		private bool _cursorInSecondaryPanel;

		private readonly GUIStyle _counterStyle = new GUIStyle();

		private bool mouseClickProcessed;
		private Rect _windowRect;
		private Rect _windowRect2;

		private int _stepMinValue = 1;
		private int _stepMaxValue = 1;

		private String _stepMinValueStr = "1";
		private String _stepMaxValueStr = "1";

		private float _waitFlowBalance = 1f;

		private readonly float[] _sliderValues = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

		private Texture2D _secondPanelTexture;

		private static bool _timedShowNumbers;

		private const float DebugCloseLod = 300f;
		private const float PriorityCloseLod = 450f;

		private uint tooltipStartFrame = 0;
		private String tooltipText = null;
		private Vector3? tooltipWorldPos = null;

		private uint currentFrame = 0;

		private static bool nodeSelectionLocked = false;

		private static bool overlayHandleHovered = false;

		private static Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>> segmentCenterByDir = new Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>>();

		static Rect ResizeGUI(Rect rect) {
			var rectX = (rect.x / 800) * Screen.width;
			var rectY = (rect.y / 600) * Screen.height;

			return new Rect(rectX, rectY, rect.width, rect.height);
		}

		protected override void Awake() {
			Log._Debug("TrafficLightTool: Awake");
			_windowRect = ResizeGUI(new Rect(155, 45, 480, 350));
			_windowRect2 = ResizeGUI(new Rect(155, 45, 300, 150));

			_secondPanelTexture = MakeTex(1, 1, new Color(0.5f, 0.5f, 0.5f, 1f));

			base.Awake();
		}

		// Expose protected property
		// ReSharper disable once MemberCanBePrivate.Global
		//public new CursorInfo ToolCursor
		//{
		//    get { return base.ToolCursor; }
		//    set { base.ToolCursor = value; }
		//} // I think this was unnecessary

		public static ushort SelectedNode { get; private set; }

		public static ushort SelectedSegment { get; private set; }
		
		public static ToolMode getToolMode() {
			return _toolMode;
		}

		public static void SetToolMode(ToolMode mode) {
			Log._Debug($"SetToolMode: {mode}");

			_toolMode = mode;
			nodeSelectionLocked = false;

			if (mode == ToolMode.None) {
#if !TAM
				UITrafficManager.deactivateButtons();
#endif
				segmentCenterByDir.Clear();
			}

			if (mode != ToolMode.ManualSwitch) {
				DisableManual();
			}

			/*if (mode == ToolMode.SwitchTrafficLight) {
				Singleton<InfoManager>.instance.SetCurrentMode(InfoManager.InfoMode.Traffic, InfoManager.SubInfoMode.Default);
				UIView.library.Hide("TrafficInfoViewPanel");
			} else {
				Singleton<InfoManager>.instance.SetCurrentMode(InfoManager.InfoMode.None, InfoManager.SubInfoMode.Default);
			}*/

			SelectedNode = 0;
			SelectedSegment = 0;

			if (mode != ToolMode.TimedLightsSelectNode && mode != ToolMode.TimedLightsShowLights && mode != ToolMode.TimedLightsAddNode && mode != ToolMode.TimedLightsRemoveNode) {
				ClearSelectedNodes();
				_timedShowNumbers = false;
			}

			if (mode == ToolMode.TimedLightsShowLights) {
				foreach (var selectedNodeIndex in SelectedNodeIndexes) {
					TrafficPriority.nodeHousekeeping(selectedNodeIndex);
				}
			}

			_selectedSegmentIds.Clear();
		}

		// Overridden to disable base class behavior
		protected override void OnEnable() {
		}

		// Overridden to disable base class behavior
		protected override void OnDisable() {
		}

		public override void RenderGeometry(RenderManager.CameraInfo cameraInfo) {
			if (_hoveredNetNodeIdx != 0) {
				m_toolController.RenderCollidingNotifications(cameraInfo, 0, 0);
			}
		}

		/// <summary>
		///	Renders overlays (node selection, segment selection, etc.)
		/// </summary>
		/// <param name="cameraInfo"></param>
		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			Log._Debug($"RenderOverlay");

			switch (_toolMode) {
				case ToolMode.SwitchTrafficLight:
					if (m_toolController.IsInsideUI || !Cursor.visible) {
						return;
					}
					_renderOverlaySwitch(cameraInfo, _mouseDown);
					break;
				case ToolMode.AddPrioritySigns:
					_renderOverlaySwitch(cameraInfo);
					break;
				case ToolMode.ManualSwitch:
					_renderOverlayManual(cameraInfo);
					break;
				case ToolMode.TimedLightsSelectNode:
				case ToolMode.TimedLightsShowLights:
				case ToolMode.TimedLightsAddNode:
				case ToolMode.TimedLightsRemoveNode:
					_renderOverlayTimedSelectNodes(cameraInfo, _toolMode == ToolMode.TimedLightsRemoveNode);
					break;
				case ToolMode.LaneChange:
					_renderOverlayLaneChange(cameraInfo);
					break;
				case ToolMode.VehicleRestrictions:
					/*if (m_toolController.IsInsideUI || !Cursor.visible) {
						return;
					}*/
					_renderOverlayVehicleRestrictions(cameraInfo);
					break;
				case ToolMode.SpeedLimits:
					break;
				default:
					base.RenderOverlay(cameraInfo);
					break;
			}
		}

		/// <summary>
		/// Primarily handles click events on hovered nodes/segments
		/// </summary>
		protected override void OnToolUpdate() {
			base.OnToolUpdate();
			Log._Debug($"OnToolUpdate");

			_mouseDown = Input.GetMouseButton(0);

			if (_mouseDown) {
				if (_mouseClicked) return;

				_mouseClicked = true;

				bool elementsHovered = determineHoveredElements();

				if (!elementsHovered || _cursorInSecondaryPanel) {
					//Log.Message("inside ui: " + m_toolController.IsInsideUI + " visible: " + Cursor.visible + " in secondary panel: " + _cursorInSecondaryPanel);
					return;
				}
				if (_hoveredSegmentIdx == 0 && _hoveredNetNodeIdx == 0) {
					//Log.Message("no hovered segment");
					return;
				}

				switch (_toolMode) {
					case ToolMode.SwitchTrafficLight:
						SwitchTrafficLightToolMode(ref Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx]);
						break;
					case ToolMode.AddPrioritySigns:
						//AddPrioritySignsToolMode(ref Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx]);
						break;
					case ToolMode.ManualSwitch:
						ManualSwitchToolMode(ref Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx]);
						break;
					case ToolMode.TimedLightsSelectNode:
					case ToolMode.TimedLightsShowLights:
						TimedLightSelectNodeToolMode(ref Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx]);
						break;
					case ToolMode.TimedLightsAddNode:
						TimedLightAddNodeToolMode(ref Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx]);
						break;
					case ToolMode.TimedLightsRemoveNode:
						TimedLightRemoveNodeToolMode(ref Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx]);
						break;
					case ToolMode.LaneChange:
						LaneChangeToolMode();
						break;
					case ToolMode.VehicleRestrictions:
						VehicleRestrictionsToolMode();
						break;
					case ToolMode.SpeedLimits:
						break;
				}
			} else {
				//showTooltip(false, null, Vector3.zero);
				_mouseClicked = false;
			}
		}

		protected override void OnToolGUI(Event e) {
			Log._Debug($"OnToolGUI");

			try {
				if (!Input.GetMouseButtonDown(0)) {
					mouseClickProcessed = false;
				}

				_guiSegments();
				if (Options.nodesOverlay) {
					_guiNodes();
#if DEBUG
					/*_guiVehicles();*/
					//_guiCitizens();
#endif
				}

				showTimedLightIcons();
				if (_toolMode != ToolMode.AddPrioritySigns) {
					_guiPrioritySigns(true);
				}

				_cursorInSecondaryPanel = false;

				switch (_toolMode) {
					case ToolMode.AddPrioritySigns:
						_guiPrioritySigns(false);
						break;
					case ToolMode.ManualSwitch:
						_guiManualTrafficLights();
						break;
					case ToolMode.TimedLightsSelectNode:
						_guiTimedTrafficLightsNode();
						break;
					case ToolMode.TimedLightsShowLights:
					case ToolMode.TimedLightsAddNode:
					case ToolMode.TimedLightsRemoveNode:
						_guiTimedTrafficLights();
						break;
					case ToolMode.LaneChange:
						_guiLaneChange();
						break;
					case ToolMode.VehicleRestrictions:
						_guiVehicleRestrictions();
						break;
					case ToolMode.SpeedLimits:
						_guiSpeedLimits();
						break;
					default:
						base.OnToolGUI(e);
						break;
				}
			} catch (Exception ex) {
				Log.Error("GUI Error: " + ex.ToString());
			}
		}

		private void _renderOverlaySwitch(RenderManager.CameraInfo cameraInfo, bool warning = false) {
			if (_hoveredNetNodeIdx == 0) return;

			// no highlight for existing priority node in sign mode
			if (_toolMode == ToolMode.AddPrioritySigns && TrafficPriority.IsPriorityNode(_hoveredNetNodeIdx))
				return;

			if (!Flags.mayHaveTrafficLight(_hoveredNetNodeIdx)) return;

			var segment = Singleton<NetManager>.instance.m_segments.m_buffer[Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_segment0];

			Bezier3 bezier;
			bezier.a = Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_position;
			bezier.d = Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_position;

			var color = GetToolColor(warning, false);

			NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
				segment.m_endDirection,
				false, false, out bezier.b, out bezier.c);

			_renderOverlayDraw(cameraInfo, bezier, color);
		}

		private void _renderOverlayManual(RenderManager.CameraInfo cameraInfo) {
			if (SelectedNode != 0) {
				RenderManualNodeOverlays(cameraInfo);
			} else {
				RenderManualSelectionOverlay(cameraInfo);
			}
		}

		private void RenderManualSelectionOverlay(RenderManager.CameraInfo cameraInfo) {
			if (_hoveredNetNodeIdx == 0) return;
			var segment = Singleton<NetManager>.instance.m_segments.m_buffer[Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_segment0];

			//if ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) return;
			Bezier3 bezier;
			bezier.a = Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_position;
			bezier.d = Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_position;

			var color = GetToolColor(false, false);

			NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
				segment.m_endDirection, false, false, out bezier.b, out bezier.c);
			_renderOverlayDraw(cameraInfo, bezier, color);
		}

		private void RenderManualNodeOverlays(RenderManager.CameraInfo cameraInfo) {
			var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(SelectedNode);

			for (var i = 0; i < 8; i++) {
				var colorGray = new Color(0.25f, 0.25f, 0.25f, 0.25f);
				ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode].GetSegment(i);

				if (segmentId == 0 ||
					(nodeSimulation != null && CustomTrafficLights.IsSegmentLight(SelectedNode, segmentId)))
					continue;

				var position = CalculateNodePositionForSegment(Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode], segmentId);

				var width = _hoveredButton[0] == segmentId ? 11.25f : 10f;
				_renderOverlayDraw(cameraInfo, colorGray, position, width, segmentId != _hoveredButton[0]);
			}
		}

		private static Vector3 CalculateNodePositionForSegment(NetNode node, int segmentId) {
			var position = node.m_position;

			var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
			if (segment.m_startNode == SelectedNode) {
				position.x += segment.m_startDirection.x * 10f;
				position.y += segment.m_startDirection.y * 10f;
				position.z += segment.m_startDirection.z * 10f;
			} else {
				position.x += segment.m_endDirection.x * 10f;
				position.y += segment.m_endDirection.y * 10f;
				position.z += segment.m_endDirection.z * 10f;
			}
			return position;
		}

		private void _renderOverlayTimedSelectNodes(RenderManager.CameraInfo cameraInfo, bool onlySelected=false) {
			if (! nodeSelectionLocked && _hoveredNetNodeIdx != 0 && (!IsNodeSelected(_hoveredNetNodeIdx) ^ onlySelected) && !m_toolController.IsInsideUI && Cursor.visible && Flags.mayHaveTrafficLight(_hoveredNetNodeIdx)) {
				var segment = Singleton<NetManager>.instance.m_segments.m_buffer[Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_segment0];

				//if ((node.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None) {
					Bezier3 bezier;
					bezier.a = Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_position;
					bezier.d = Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_position;

					var color = GetToolColor(false, false);

					NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
						segment.m_endDirection, false, false, out bezier.b, out bezier.c);
					_renderOverlayDraw(cameraInfo, bezier, color);
				//}
			}

			if (SelectedNodeIndexes.Count <= 0) return;

			foreach (var index in SelectedNodeIndexes) {
				var segment = Singleton<NetManager>.instance.m_segments.m_buffer[Singleton<NetManager>.instance.m_nodes.m_buffer[index].m_segment0];

				Bezier3 bezier;

				bezier.a = Singleton<NetManager>.instance.m_nodes.m_buffer[index].m_position;
				bezier.d = Singleton<NetManager>.instance.m_nodes.m_buffer[index].m_position;

				var color = GetToolColor(true, false);

				NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
					segment.m_endDirection, false, false, out bezier.b, out bezier.c);
				_renderOverlayDraw(cameraInfo, bezier, color);
			}
		}

		private void _renderOverlayLaneChange(RenderManager.CameraInfo cameraInfo) {

			if (_hoveredSegmentIdx != 0 && _hoveredNetNodeIdx != 0 && (_hoveredSegmentIdx != SelectedSegment || _hoveredNetNodeIdx != SelectedNode)) {
				var netFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_flags;

				if ((netFlags & NetNode.Flags.Junction) != NetNode.Flags.None) {
					NetTool.RenderOverlay(cameraInfo, ref Singleton<NetManager>.instance.m_segments.m_buffer[_hoveredSegmentIdx], GetToolColor(false, false),
						GetToolColor(false, false));
				}
			}

			if (SelectedSegment == 0) return;

			NetTool.RenderOverlay(cameraInfo, ref Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegment], GetToolColor(true, false), GetToolColor(true, false));
		}

		private void _renderOverlayVehicleRestrictions(RenderManager.CameraInfo cameraInfo) {
			if (SelectedSegment != 0)
				NetTool.RenderOverlay(cameraInfo, ref Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegment], GetToolColor(true, false), GetToolColor(true, false));

			if (_cursorInSecondaryPanel)
				return;

			if (_hoveredSegmentIdx != 0 && _hoveredSegmentIdx != SelectedSegment && !overlayHandleHovered) {
				NetTool.RenderOverlay(cameraInfo, ref Singleton<NetManager>.instance.m_segments.m_buffer[_hoveredSegmentIdx], GetToolColor(false, false), GetToolColor(false, false));
			}
		}

		private void _renderOverlayDraw(RenderManager.CameraInfo cameraInfo, Bezier3 bezier, Color color) {
			const float width = 8f;

			var exprEaCp0 = Singleton<ToolManager>.instance;
			exprEaCp0.m_drawCallData.m_overlayCalls = exprEaCp0.m_drawCallData.m_overlayCalls + 1;
			Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(cameraInfo, color, bezier,
				width * 2f, width, width, -1f, 1280f, false, false);

			// 8 - small roads; 16 - big roads
		}

		private void _renderOverlayDraw(RenderManager.CameraInfo cameraInfo, Color color, Vector3 position, float width, bool alpha) {
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

			if (_toolMode == ToolMode.None) {
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
			var mouseRayValid = !UIView.IsInsideUI() && Cursor.visible && !_cursorInSecondaryPanel;

			if (mouseRayValid) {
				ushort oldHoveredSegmentId = _hoveredSegmentIdx;
				ushort oldHoveredNodeId = _hoveredNetNodeIdx;

				_hoveredSegmentIdx = 0;
				_hoveredNetNodeIdx = 0;

				// find currently hovered node & segment

				/*var mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
				var mouseRayLength = Camera.main.farClipPlane;
				var rayRight = Camera.main.transform.TransformDirection(Vector3.right);*/

				var nodeInput = new RaycastInput(this.m_mouseRay, this.m_mouseRayLength);
				//input.m_netService = new RaycastService(ItemClass.Service.Road, ItemClass.SubService.None, ItemClass.Layer.Default);
				//input.m_rayRight = rayRight;
				nodeInput.m_netService.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
				nodeInput.m_netService.m_service = ItemClass.Service.Road;
				nodeInput.m_netService2.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.PublicTransport | ItemClass.Layer.MetroTunnels;
				nodeInput.m_netService2.m_service = ItemClass.Service.PublicTransport;
				nodeInput.m_netService2.m_subService = ItemClass.SubService.PublicTransportTrain;
				nodeInput.m_ignoreTerrain = true;
				nodeInput.m_ignoreNodeFlags = NetNode.Flags.None;
				//nodeInput.m_ignoreSegmentFlags = NetSegment.Flags.Untouchable;

				RaycastOutput nodeOutput;
				if (RayCast(nodeInput, out nodeOutput)) {
					_hoveredNetNodeIdx = nodeOutput.m_netNode;
				}

				var segmentInput = new RaycastInput(this.m_mouseRay, this.m_mouseRayLength);
				//input.m_netService = new RaycastService(ItemClass.Service.Road, ItemClass.SubService.None, ItemClass.Layer.Default);
				//input.m_rayRight = rayRight;
				segmentInput.m_netService.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
				segmentInput.m_netService.m_service = ItemClass.Service.Road;
				segmentInput.m_netService2.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.PublicTransport | ItemClass.Layer.MetroTunnels;
				segmentInput.m_netService2.m_service = ItemClass.Service.PublicTransport;
				segmentInput.m_netService2.m_subService = ItemClass.SubService.PublicTransportTrain;
				segmentInput.m_ignoreTerrain = true;
				//nodeInput.m_ignoreNodeFlags = NetNode.Flags.None;
				segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.Untouchable;

				RaycastOutput segmentOutput;
				if (RayCast(segmentInput, out segmentOutput)) {
					_hoveredSegmentIdx = segmentOutput.m_netSegment;

					if (_hoveredNetNodeIdx <= 0) {
						ushort startNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[_hoveredSegmentIdx].m_startNode;
						ushort endNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[_hoveredSegmentIdx].m_endNode;

						float startDist = (segmentOutput.m_hitPos - Singleton<NetManager>.instance.m_nodes.m_buffer[startNodeId].m_position).magnitude;
						float endDist = (segmentOutput.m_hitPos - Singleton<NetManager>.instance.m_nodes.m_buffer[endNodeId].m_position).magnitude;
						if (startDist < endDist && startDist < 25f)
							_hoveredNetNodeIdx = startNodeId;
						else if (endDist < startDist && endDist < 25f)
							_hoveredNetNodeIdx = endNodeId;
					}
				}

				if (oldHoveredNodeId != _hoveredNetNodeIdx || oldHoveredSegmentId != _hoveredSegmentIdx) {
					Log._Debug($"*** Mouse ray @ node {_hoveredNetNodeIdx}, segment {_hoveredSegmentIdx}, toolMode={_toolMode}");
                }

				return (_hoveredNetNodeIdx != 0 || _hoveredSegmentIdx != 0);
			} else {
				//Log.Message($"Mouse ray invalid: {UIView.IsInsideUI()} {Cursor.visible} {_cursorInSecondaryPanel}");
			}

			return mouseRayValid;
		}

		private void LaneChangeToolMode() {
			if (_hoveredNetNodeIdx == 0 || _hoveredSegmentIdx == 0) return;

			var netFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_flags;

			if ((netFlags & NetNode.Flags.Junction) == NetNode.Flags.None) return;

			if (Singleton<NetManager>.instance.m_segments.m_buffer[_hoveredSegmentIdx].m_startNode != _hoveredNetNodeIdx &&
				Singleton<NetManager>.instance.m_segments.m_buffer[_hoveredSegmentIdx].m_endNode != _hoveredNetNodeIdx)
				return;

			SelectedSegment = _hoveredSegmentIdx;
			SelectedNode = _hoveredNetNodeIdx;
		}

		private void VehicleRestrictionsToolMode() {
			if (_hoveredSegmentIdx == 0) return;
			if (overlayHandleHovered) return;

			SelectedSegment = _hoveredSegmentIdx;
			mouseClickProcessed = true;
		}

		private void TimedLightSelectNodeToolMode(ref NetNode node) {
			if (_hoveredNetNodeIdx <= 0 || nodeSelectionLocked)
				return;

			if (_toolMode == ToolMode.TimedLightsShowLights) {
				_toolMode = ToolMode.TimedLightsSelectNode;
				ClearSelectedNodes();
			}

			TrafficLightSimulation timedSim = TrafficLightSimulation.GetNodeSimulation(_hoveredNetNodeIdx);
			if (timedSim == null || !timedSim.IsTimedLight()) {
				if (IsNodeSelected(_hoveredNetNodeIdx)) {
					RemoveSelectedNode(_hoveredNetNodeIdx);
				} else {
					AddSelectedNode(_hoveredNetNodeIdx);
				}
			} else {
				if (SelectedNodeIndexes.Count == 0) {
					timedSim.housekeeping(true);
					var timedLight = timedSim.TimedLight;

					if (timedLight != null) {
						SelectedNodeIndexes = new List<ushort>(timedLight.NodeGroup);
						SetToolMode(ToolMode.TimedLightsShowLights);
					}
				} else {
					showTooltip(Translation.GetString("NODE_IS_TIMED_LIGHT"), node.m_position);
				}
			}
		}

		private void TimedLightAddNodeToolMode(ref NetNode node) {
			if (_hoveredNetNodeIdx <= 0 || nodeSelectionLocked)
				return;

			if (SelectedNodeIndexes.Count <= 0) {
				SetToolMode(ToolMode.TimedLightsSelectNode);
				return;
			}

			if (SelectedNodeIndexes.Contains(_hoveredNetNodeIdx))
				return;

			//bool mayEnterBlocked = Options.mayEnterBlockedJunctions;
			TimedTrafficLights existingTimedLight = null;
			foreach (var nodeId in SelectedNodeIndexes) {
				var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(nodeId);
				if (nodeSimulation == null || !nodeSimulation.IsTimedLight())
					continue;
				TimedTrafficLights timedNode = nodeSimulation.TimedLight;
				if (timedNode == null)
					continue;

				//mayEnterBlocked = timedNode.vehiclesMayEnterBlockedJunctions;
				existingTimedLight = timedNode;
			}

			var timedSim = TrafficLightSimulation.GetNodeSimulation(_hoveredNetNodeIdx);
			if (timedSim != null)
				timedSim.housekeeping(true);
			TimedTrafficLights timedLight = null;
			if (timedSim == null || !timedSim.IsTimedLight()) {
				var nodeGroup = new List<ushort>();
				nodeGroup.Add(_hoveredNetNodeIdx);
				timedSim = TrafficLightSimulation.AddNodeToSimulation(_hoveredNetNodeIdx);
				timedSim.SetupTimedTrafficLight(nodeGroup);
				timedLight = timedSim.TimedLight;
				//timedLight.vehiclesMayEnterBlockedJunctions = mayEnterBlocked;
			} else {
				timedLight = timedSim.TimedLight;
			}

			timedLight.Join(existingTimedLight);
			ClearSelectedNodes();
			foreach (ushort nodeId in timedLight.NodeGroup)
				AddSelectedNode(nodeId);
			SetToolMode(ToolMode.TimedLightsShowLights);
		}

		private void TimedLightRemoveNodeToolMode(ref NetNode node) {
			if (_hoveredNetNodeIdx <= 0 || nodeSelectionLocked)
				return;

			if (SelectedNodeIndexes.Count <= 0) {
				SetToolMode(ToolMode.TimedLightsSelectNode);
				return;
			}

			if (SelectedNodeIndexes.Contains(_hoveredNetNodeIdx)) {
				TrafficLightSimulation.RemoveNodeFromSimulation(_hoveredNetNodeIdx, false);
			}
			RemoveSelectedNode(_hoveredNetNodeIdx);
			SetToolMode(ToolMode.TimedLightsShowLights);
		}

		private void ManualSwitchToolMode(ref NetNode node) {
			if (SelectedNode != 0) return;

			TrafficLightSimulation sim = TrafficLightSimulation.GetNodeSimulation(_hoveredNetNodeIdx);
			if (sim == null || !sim.IsTimedLight()) {
				if ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) {
					TrafficPriority.RemovePrioritySegments(_hoveredNetNodeIdx);
					Flags.setNodeTrafficLight(_hoveredNetNodeIdx, true);
				}

				SelectedNode = _hoveredNetNodeIdx;

				sim = TrafficLightSimulation.AddNodeToSimulation(SelectedNode);
				sim.SetupManualTrafficLight();

				for (var s = 0; s < 8; s++) {
					var segment = Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode].GetSegment(s);
					if (segment != 0 && !TrafficPriority.IsPrioritySegment(SelectedNode, segment)) {
						TrafficPriority.AddPrioritySegment(SelectedNode, segment, SegmentEnd.PriorityType.None);
					}
				}
			} else {
				if (SelectedNodeIndexes.Count == 0) {
				}
				showTooltip(Translation.GetString("NODE_IS_TIMED_LIGHT"), node.m_position);
			}
		}

		private void AddPrioritySignsToolMode(NetNode node) {
			bool ok = false;
			if ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) {
				// no traffic light set
				ok = true;
			} else {
				TrafficLightSimulation nodeSim = TrafficLightSimulation.GetNodeSimulation(_hoveredNetNodeIdx);
				if (nodeSim == null || !nodeSim.IsTimedLight())
					ok = true;
				else {
					Log.Warning("Could not add priority signs. " + (nodeSim == null));
				}
			}

			if (ok) {
				mouseClickProcessed = true;
				SelectedNode = _hoveredNetNodeIdx;
			} else {
				showTooltip(Translation.GetString("NODE_IS_LIGHT"), node.m_position);
			}
		}

		private void SwitchTrafficLightToolMode(ref NetNode node) {
			if ((node.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None) {
				_switchTrafficLights();
			} else {
				//Log.Message("No junction");
			}
		}

		private void _guiManualTrafficLights() { // TODO unite with _guiTimedTrafficLights
			var hoveredSegment = false;

			if (SelectedNode != 0) {
				var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(SelectedNode);
				nodeSimulation.housekeeping(true);

				/*if (Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode].CountSegments() == 2) {
					_guiManualTrafficLightsCrosswalk(ref Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode]);
					return;
				}*/ // TODO check

				for (var i = 0; i < 8; i++) {
					var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode].GetSegment(i);

					if (segmentId == 0 || nodeSimulation == null ||
						!CustomTrafficLights.IsSegmentLight(SelectedNode, segmentId)) continue;

					var position = CalculateNodePositionForSegment(Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode], Singleton<NetManager>.instance.m_segments.m_buffer[segmentId]);
					var segmentLights = CustomTrafficLights.GetSegmentLights(SelectedNode, segmentId);

					var screenPos = Camera.main.WorldToScreenPoint(position);
					screenPos.y = Screen.height - screenPos.y;

					if (screenPos.z < 0)
						continue;

					var diff = position - Camera.main.transform.position;
					var zoom = 1.0f / diff.magnitude * 100f;

					// original / 2.5
					var lightWidth = 41f * zoom;
					var lightHeight = 97f * zoom;

					var pedestrianWidth = 36f * zoom;
					var pedestrianHeight = 61f * zoom;

					// SWITCH MODE BUTTON
					var modeWidth = 41f * zoom;
					var modeHeight = 38f * zoom;

					var guiColor = GUI.color;

					if (segmentLights.PedestrianLightState != null) {
						// pedestrian light

						// SWITCH MANUAL PEDESTRIAN LIGHT BUTTON
						hoveredSegment = RenderManualPedestrianLightSwitch(zoom, segmentId, screenPos, lightWidth, segmentLights, hoveredSegment);

						// SWITCH PEDESTRIAN LIGHT
						guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 2 && segmentLights.ManualPedestrianMode ? 0.92f : 0.6f;
						GUI.color = guiColor;

						var myRect3 = new Rect(screenPos.x - pedestrianWidth / 2 - lightWidth + 5f * zoom, screenPos.y - pedestrianHeight / 2 + 22f * zoom, pedestrianWidth, pedestrianHeight);

						switch (segmentLights.PedestrianLightState) {
							case RoadBaseAI.TrafficLightState.Green:
								GUI.DrawTexture(myRect3, TrafficLightToolTextureResources.PedestrianGreenLightTexture2D);
								break;
							case RoadBaseAI.TrafficLightState.Red:
							default:
								GUI.DrawTexture(myRect3, TrafficLightToolTextureResources.PedestrianRedLightTexture2D);
								break;
						}

						hoveredSegment = IsPedestrianLightHovered(myRect3, segmentId, hoveredSegment, segmentLights);
					}

					int lightOffset = -1;
					foreach (ExtVehicleType vehicleType in segmentLights.VehicleTypes) {
						++lightOffset;
						CustomSegmentLight segmentLight = segmentLights.GetCustomLight(vehicleType);

						Vector3 offsetScreenPos = screenPos;
						offsetScreenPos.y -= (lightHeight + 10f * zoom) * lightOffset;

						SetAlpha(segmentId, -1);

						var myRect1 = new Rect(offsetScreenPos.x - modeWidth / 2, offsetScreenPos.y - modeHeight / 2 + modeHeight - 7f * zoom, modeWidth, modeHeight);

						GUI.DrawTexture(myRect1, TrafficLightToolTextureResources.LightModeTexture2D);

						hoveredSegment = GetHoveredSegment(myRect1, segmentId, hoveredSegment, segmentLight);

						// COUNTER
						hoveredSegment = RenderCounter(segmentId, offsetScreenPos, modeWidth, modeHeight, zoom, segmentLights, hoveredSegment);

						if (lightOffset > 0) {
							// Info sign
							var infoWidth = 56.125f * zoom;
							var infoHeight = 51.375f * zoom;

							int numInfos = 0;
							for (int k = 0; k < infoSignsToDisplay.Length; ++k) {
								if ((infoSignsToDisplay[k] & vehicleType) == ExtVehicleType.None)
									continue;
								var infoRect = new Rect(offsetScreenPos.x + modeWidth / 2f + 7f * zoom * (float)(numInfos + 1) + infoWidth * (float)numInfos, offsetScreenPos.y - infoHeight / 2f, infoWidth, infoHeight);
								guiColor.a = 0.6f;
								GUI.DrawTexture(infoRect, TrafficLightToolTextureResources.VehicleInfoSignTextures[infoSignsToDisplay[k]]);
								++numInfos;
							}
						}

						SegmentGeometry geometry = CustomRoadAI.GetSegmentGeometry(segmentId);

						if (geometry.IsOutgoingOneWay(SelectedNode)) continue;

						var hasLeftSegment = geometry.HasLeftSegment(SelectedNode);
						var hasForwardSegment = geometry.HasStraightSegment(SelectedNode);
						var hasRightSegment = geometry.HasRightSegment(SelectedNode);

						switch (segmentLight.CurrentMode) {
							case CustomSegmentLight.Mode.Simple:
								hoveredSegment = SimpleManualSegmentLightMode(segmentId, offsetScreenPos, lightWidth, pedestrianWidth, zoom, lightHeight, segmentLight, hoveredSegment);
								break;
							case CustomSegmentLight.Mode.SingleLeft:
								hoveredSegment = LeftForwardRManualSegmentLightMode(hasLeftSegment, segmentId, offsetScreenPos, lightWidth, pedestrianWidth, zoom, lightHeight, segmentLight, hoveredSegment, hasForwardSegment, hasRightSegment);
								break;
							case CustomSegmentLight.Mode.SingleRight:
								hoveredSegment = RightForwardLSegmentLightMode(segmentId, offsetScreenPos, lightWidth, pedestrianWidth, zoom, lightHeight, hasForwardSegment, hasLeftSegment, segmentLight, hasRightSegment, hoveredSegment);
								break;
							default:
								// left arrow light
								if (hasLeftSegment)
									hoveredSegment = LeftArrowLightMode(segmentId, lightWidth, hasRightSegment, hasForwardSegment, offsetScreenPos, pedestrianWidth, zoom, lightHeight, segmentLight, hoveredSegment);

								// forward arrow light
								if (hasForwardSegment)
									hoveredSegment = ForwardArrowLightMode(segmentId, lightWidth, hasRightSegment, offsetScreenPos, pedestrianWidth, zoom, lightHeight, segmentLight, hoveredSegment);

								// right arrow light
								if (hasRightSegment)
									hoveredSegment = RightArrowLightMode(segmentId, offsetScreenPos, lightWidth, pedestrianWidth, zoom, lightHeight, segmentLight, hoveredSegment);
								break;
						}
					}
				}
			}

			if (hoveredSegment) return;
			_hoveredButton[0] = 0;
			_hoveredButton[1] = 0;
		}

		private bool RightArrowLightMode(int segmentId, Vector3 screenPos, float lightWidth,
			float pedestrianWidth, float zoom, float lightHeight, CustomSegmentLight segmentDict, bool hoveredSegment) {
			SetAlpha(segmentId, 5);

			var myRect5 =
				new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
					screenPos.y - lightHeight / 2, lightWidth, lightHeight);

			switch (segmentDict.LightRight) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.GreenLightRightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
					GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.RedLightRightTexture2D);
					break;
			}

			if (!myRect5.Contains(Event.current.mousePosition))
				return hoveredSegment;

			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 5;

			if (!Input.GetMouseButtonDown(0) || mouseClickProcessed)
				return true;

			mouseClickProcessed = true;
			segmentDict.ChangeLightRight();
			return true;
		}

		private void SetAlpha(int segmentId, int buttonId) {
			var guiColor = GUI.color;

			guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == buttonId ? 0.92f : 0.6f;

			GUI.color = guiColor;
		}

		private bool ForwardArrowLightMode(int segmentId, float lightWidth, bool hasRightSegment,
			Vector3 screenPos, float pedestrianWidth, float zoom, float lightHeight, CustomSegmentLight segmentDict,
			bool hoveredSegment) {
			SetAlpha(segmentId, 4);

			var offsetLight = lightWidth;

			if (hasRightSegment)
				offsetLight += lightWidth;

			var myRect6 =
				new Rect(screenPos.x - lightWidth / 2 - offsetLight - pedestrianWidth + 5f * zoom,
					screenPos.y - lightHeight / 2, lightWidth, lightHeight);

			switch (segmentDict.LightMain) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(myRect6, TrafficLightToolTextureResources.GreenLightStraightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
					GUI.DrawTexture(myRect6, TrafficLightToolTextureResources.RedLightStraightTexture2D);
					break;
			}

			if (!myRect6.Contains(Event.current.mousePosition))
				return hoveredSegment;

			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 4;

			if (!Input.GetMouseButtonDown(0) || mouseClickProcessed)
				return true;

			mouseClickProcessed = true;
			segmentDict.ChangeLightMain();
			return true;
		}

		private bool LeftArrowLightMode(int segmentId, float lightWidth, bool hasRightSegment,
			bool hasForwardSegment, Vector3 screenPos, float pedestrianWidth, float zoom, float lightHeight,
			CustomSegmentLight segmentDict, bool hoveredSegment) {
			SetAlpha(segmentId, 3);

			var offsetLight = lightWidth;

			if (hasRightSegment)
				offsetLight += lightWidth;

			if (hasForwardSegment)
				offsetLight += lightWidth;

			var myRect4 =
				new Rect(screenPos.x - lightWidth / 2 - offsetLight - pedestrianWidth + 5f * zoom,
					screenPos.y - lightHeight / 2, lightWidth, lightHeight);

			switch (segmentDict.LightLeft) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightLeftTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
					GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightLeftTexture2D);
					break;
			}

			if (!myRect4.Contains(Event.current.mousePosition))
				return hoveredSegment;
			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 3;

			if (!Input.GetMouseButtonDown(0) || mouseClickProcessed)
				return true;

			mouseClickProcessed = true;
			segmentDict.ChangeLightLeft();

			if (!hasForwardSegment) {
				segmentDict.ChangeLightMain();
			}
			return true;
		}

		private bool RightForwardLSegmentLightMode(int segmentId, Vector3 screenPos, float lightWidth, float pedestrianWidth,
			float zoom, float lightHeight, bool hasForwardSegment, bool hasLeftSegment, CustomSegmentLight segmentDict,
			bool hasRightSegment, bool hoveredSegment) {
			SetAlpha(segmentId, 3);

			var myRect4 = new Rect(screenPos.x - lightWidth / 2 - lightWidth * 2 - pedestrianWidth + 5f * zoom,
				screenPos.y - lightHeight / 2, lightWidth, lightHeight);

			if (hasForwardSegment && hasLeftSegment) {
				switch (segmentDict.LightLeft) {
					case RoadBaseAI.TrafficLightState.Green:
						GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightForwardLeftTexture2D);
						break;
					case RoadBaseAI.TrafficLightState.Red:
						GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightForwardLeftTexture2D);
						break;
				}
			} else if (!hasLeftSegment) {
				if (!hasRightSegment) {
					myRect4 = new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
						screenPos.y - lightHeight / 2, lightWidth, lightHeight);
				}

				switch (segmentDict.LightMain) {
					case RoadBaseAI.TrafficLightState.Green:
						GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightStraightTexture2D);
						break;
					case RoadBaseAI.TrafficLightState.Red:
						GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightStraightTexture2D);
						break;
				}
			} else {
				if (!hasRightSegment) {
					myRect4 = new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
						screenPos.y - lightHeight / 2, lightWidth, lightHeight);
				}

				switch (segmentDict.LightMain) {
					case RoadBaseAI.TrafficLightState.Green:
						GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightLeftTexture2D);
						break;
					case RoadBaseAI.TrafficLightState.Red:
						GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightLeftTexture2D);
						break;
				}
			}


			if (myRect4.Contains(Event.current.mousePosition)) {
				_hoveredButton[0] = segmentId;
				_hoveredButton[1] = 3;
				hoveredSegment = true;

				if (checkClicked()) {
					segmentDict.ChangeLightMain();
				}
			}

			var guiColor = GUI.color;
			// right arrow light
			if (hasRightSegment)
				guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 4 ? 0.92f : 0.6f;

			GUI.color = guiColor;

			var myRect5 =
				new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
					screenPos.y - lightHeight / 2, lightWidth, lightHeight);

			switch (segmentDict.LightRight) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.GreenLightRightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
					GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.RedLightRightTexture2D);
					break;
			}


			if (!myRect5.Contains(Event.current.mousePosition))
				return hoveredSegment;

			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 4;

			if (!Input.GetMouseButtonDown(0) || mouseClickProcessed)
				return true;
			mouseClickProcessed = true;
			segmentDict.ChangeLightRight();
			return true;
		}

		private bool LeftForwardRManualSegmentLightMode(bool hasLeftSegment, int segmentId, Vector3 screenPos, float lightWidth,
			float pedestrianWidth, float zoom, float lightHeight, CustomSegmentLight segmentDict, bool hoveredSegment,
			bool hasForwardSegment, bool hasRightSegment) {
			if (hasLeftSegment) {
				// left arrow light
				SetAlpha(segmentId, 3);

				var myRect4 =
					new Rect(screenPos.x - lightWidth / 2 - lightWidth * 2 - pedestrianWidth + 5f * zoom,
						screenPos.y - lightHeight / 2, lightWidth, lightHeight);

				switch (segmentDict.LightLeft) {
					case RoadBaseAI.TrafficLightState.Green:
						GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightLeftTexture2D);
						break;
					case RoadBaseAI.TrafficLightState.Red:
						GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightLeftTexture2D);
						break;
				}

				if (myRect4.Contains(Event.current.mousePosition)) {
					_hoveredButton[0] = segmentId;
					_hoveredButton[1] = 3;
					hoveredSegment = true;

					if (checkClicked()) {
						segmentDict.ChangeLightLeft();
					}
				}
			}

			// forward-right arrow light
			SetAlpha(segmentId, 4);

			var myRect5 =
				new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
					screenPos.y - lightHeight / 2, lightWidth, lightHeight);

			if (hasForwardSegment && hasRightSegment) {
				switch (segmentDict.LightMain) {
					case RoadBaseAI.TrafficLightState.Green:
						GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.GreenLightForwardRightTexture2D);
						break;
					case RoadBaseAI.TrafficLightState.Red:
						GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.RedLightForwardRightTexture2D);
						break;
				}
			} else if (!hasRightSegment) {
				switch (segmentDict.LightMain) {
					case RoadBaseAI.TrafficLightState.Green:
						GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.GreenLightStraightTexture2D);
						break;
					case RoadBaseAI.TrafficLightState.Red:
						GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.RedLightStraightTexture2D);
						break;
				}
			} else {
				switch (segmentDict.LightMain) {
					case RoadBaseAI.TrafficLightState.Green:
						GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.GreenLightRightTexture2D);
						break;
					case RoadBaseAI.TrafficLightState.Red:
						GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.RedLightRightTexture2D);
						break;
				}
			}

			if (!myRect5.Contains(Event.current.mousePosition))
				return hoveredSegment;
			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 4;

			if (!Input.GetMouseButtonDown(0) || mouseClickProcessed)
				return true;
			mouseClickProcessed = true;
			segmentDict.ChangeLightMain();
			return true;
		}

		private bool SimpleManualSegmentLightMode(int segmentId, Vector3 screenPos, float lightWidth, float pedestrianWidth,
			float zoom, float lightHeight, CustomSegmentLight segmentDict, bool hoveredSegment) {
			SetAlpha(segmentId, 3);

			var myRect4 =
				new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
					screenPos.y - lightHeight / 2, lightWidth, lightHeight);

			switch (segmentDict.LightMain) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
					GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightTexture2D);
					break;
			}

			if (!myRect4.Contains(Event.current.mousePosition))
				return hoveredSegment;
			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 3;

			if (!Input.GetMouseButtonDown(0) || mouseClickProcessed)
				return true;

			mouseClickProcessed = true;
			segmentDict.ChangeLightMain();
			return true;
		}

		private bool IsPedestrianLightHovered(Rect myRect3, int segmentId, bool hoveredSegment, CustomSegmentLights segmentLights) {
			if (!myRect3.Contains(Event.current.mousePosition))
				return hoveredSegment;
			if (segmentLights.PedestrianLightState == null)
				return false;

			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 2;

			if (!Input.GetMouseButtonDown(0) || mouseClickProcessed)
				return true;
			mouseClickProcessed = true;

			if (!segmentLights.ManualPedestrianMode) {
				segmentLights.ManualPedestrianMode = true;
			} else {
				segmentLights.ChangeLightPedestrian();
			}
			return true;
		}

		private bool RenderManualPedestrianLightSwitch(float zoom, int segmentId, Vector3 screenPos, float lightWidth,
			CustomSegmentLights segmentLights, bool hoveredSegment) {
			if (segmentLights.PedestrianLightState == null)
				return false;

			var guiColor = GUI.color;
			var manualPedestrianWidth = 36f * zoom;
			var manualPedestrianHeight = 35f * zoom;

			guiColor.a = _hoveredButton[0] == segmentId && (_hoveredButton[1] == 1 || _hoveredButton[1] == 2) ? 0.92f : 0.6f;

			GUI.color = guiColor;

			var myRect2 = new Rect(screenPos.x - manualPedestrianWidth / 2 - lightWidth + 5f * zoom,
				screenPos.y - manualPedestrianHeight / 2 - 9f * zoom, manualPedestrianWidth, manualPedestrianHeight);

			GUI.DrawTexture(myRect2, segmentLights.ManualPedestrianMode ? TrafficLightToolTextureResources.PedestrianModeManualTexture2D : TrafficLightToolTextureResources.PedestrianModeAutomaticTexture2D);

			if (!myRect2.Contains(Event.current.mousePosition))
				return hoveredSegment;

			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 1;

			if (!Input.GetMouseButtonDown(0) || mouseClickProcessed)
				return true;

			mouseClickProcessed = true;
			segmentLights.ManualPedestrianMode = true;
			return true;
		}

		private bool RenderCounter(int segmentId, Vector3 screenPos, float modeWidth, float modeHeight, float zoom,
			CustomSegmentLights segmentLights, bool hoveredSegment) {
			SetAlpha(segmentId, 0);

			var myRectCounter = new Rect(screenPos.x - modeWidth / 2, screenPos.y - modeHeight / 2 - 6f * zoom, modeWidth, modeHeight);

			GUI.DrawTexture(myRectCounter, TrafficLightToolTextureResources.LightCounterTexture2D);

			var counterSize = 20f * zoom;

			var counter = segmentLights.LastChange();

			var myRectCounterNum = new Rect(screenPos.x - counterSize + 15f * zoom + (counter >= 10 ? -5 * zoom : 0f),
				screenPos.y - counterSize + 11f * zoom, counterSize, counterSize);

			_counterStyle.fontSize = (int)(18f * zoom);
			_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

			GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

			if (!myRectCounter.Contains(Event.current.mousePosition))
				return hoveredSegment;
			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 0;
			return true;
		}

		private bool GetHoveredSegment(Rect myRect1, int segmentId, bool hoveredSegment, CustomSegmentLight segmentDict) {
			if (!myRect1.Contains(Event.current.mousePosition))
				return hoveredSegment;

			//Log.Message("mouse in myRect1");
			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = -1;

			if (!Input.GetMouseButtonDown(0) || mouseClickProcessed)
				return true;
			mouseClickProcessed = true;
			segmentDict.ChangeMode();
			return true;
		}

		private static Vector3 CalculateNodePositionForSegment(NetNode node, NetSegment segment) {
			var position = node.m_position;

			const float offset = 25f;

			if (segment.m_startNode == SelectedNode) {
				position.x += segment.m_startDirection.x * offset;
				position.y += segment.m_startDirection.y * offset;
				position.z += segment.m_startDirection.z * offset;
			} else {
				position.x += segment.m_endDirection.x * offset;
				position.y += segment.m_endDirection.y * offset;
				position.z += segment.m_endDirection.z * offset;
			}
			return position;
		}

		/*private void _guiManualTrafficLightsCrosswalk(ref NetNode node) {
			var hoveredSegment = false;

			ushort segment1 = 0;
			ushort segment2 = 0;

			for (var i = 0; i < 8; i++) {
				var segmentId = node.GetSegment(i);

				if (segmentId == 0) continue;

				if (segment1 == 0) {
					segment1 = segmentId;
				} else {
					segment2 = segmentId;
				}
			}

			var segmentDict1 = CustomTrafficLights.GetSegmentLights(SelectedNode, segment1);
			var segmentDict2 = CustomTrafficLights.GetSegmentLights(SelectedNode, segment2);
			var position = node.m_position;

			const float offset = 0f;

			if (Singleton<NetManager>.instance.m_segments.m_buffer[segment1].m_startNode == SelectedNode) {
				position.x += Singleton<NetManager>.instance.m_segments.m_buffer[segment1].m_startDirection.x * offset;
				position.y += Singleton<NetManager>.instance.m_segments.m_buffer[segment1].m_startDirection.y * offset;
				position.z += Singleton<NetManager>.instance.m_segments.m_buffer[segment1].m_startDirection.z * offset;
			} else {
				position.x += Singleton<NetManager>.instance.m_segments.m_buffer[segment1].m_endDirection.x * offset;
				position.y += Singleton<NetManager>.instance.m_segments.m_buffer[segment1].m_endDirection.y * offset;
				position.z += Singleton<NetManager>.instance.m_segments.m_buffer[segment1].m_endDirection.z * offset;
			}

			var guiColor = GUI.color;

			var screenPos = Camera.main.WorldToScreenPoint(position);
			screenPos.y = Screen.height - screenPos.y;

			if (screenPos.z < 0)
				return;

			var diff = position - Camera.main.transform.position;
			var zoom = 1.0f / diff.magnitude * 100f;

			// original / 2.5
			var lightWidth = 41f * zoom;
			var lightHeight = 97f * zoom;

			// SWITCH PEDESTRIAN LIGHT
			var pedestrianWidth = 36f * zoom;
			var pedestrianHeight = 61f * zoom;

			guiColor.a = _hoveredButton[0] == segment1 && _hoveredButton[1] == 0 ? 0.92f : 0.6f;

			GUI.color = guiColor;

			var myRect3 = new Rect(screenPos.x - pedestrianWidth / 2 - lightWidth + 5f * zoom, screenPos.y - pedestrianHeight / 2 + 22f * zoom, pedestrianWidth, pedestrianHeight);

			switch (segmentDict1.LightPedestrian) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(myRect3, TrafficLightToolTextureResources.PedestrianGreenLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
				default:
					GUI.DrawTexture(myRect3, TrafficLightToolTextureResources.PedestrianRedLightTexture2D);
					break;
			}

			if (myRect3.Contains(Event.current.mousePosition)) {
				_hoveredButton[0] = segment1;
				_hoveredButton[1] = 0;
				hoveredSegment = true;

				checkClicked();
			}

			// no arrow light
			guiColor.a = _hoveredButton[0] == segment1 && _hoveredButton[1] == 1 ? 0.92f : 0.6f;

			GUI.color = guiColor;

			var myRect4 =
				new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
					screenPos.y - lightHeight / 2, lightWidth, lightHeight);

			switch (segmentDict1.LightMain) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
					GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightTexture2D);
					break;
			}

			if (myRect4.Contains(Event.current.mousePosition)) {
				_hoveredButton[0] = segment1;
				_hoveredButton[1] = 1;
				hoveredSegment = true;

				if (checkClicked()) {
					segmentDict1.ChangeLightMain();
					segmentDict2.ChangeLightMain();
				}
			}

			if (hoveredSegment) return;

			_hoveredButton[0] = 0;
			_hoveredButton[1] = 0;
		}*/

		/// <summary>
		/// Displays lane ids over lanes
		/// </summary>
		private void _guiLanes(ref NetSegment segment, ref NetInfo segmentInfo) {
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

				totalDensity += CustomRoadAI.currentLaneDensities[curLaneId];

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
				labelStr += ", flags: " + ((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_flags).ToString() + ", limit: " + SpeedLimitManager.GetCustomSpeedLimit(curLaneId) + " km/h, dir: " + laneInfo.m_direction + ", final: " + laneInfo.m_finalDirection + ", pos: " + String.Format("{0:0.##}", laneInfo.m_position) + ", sim. idx: " + laneInfo.m_similarLaneIndex + " for " + laneInfo.m_vehicleType;
#endif
				if (CustomRoadAI.InStartupPhase)
					labelStr += ", in start-up phase";
				else
					labelStr += ", avg. speed: " + CustomRoadAI.laneMeanSpeeds[curLaneId] + " %";
				labelStr += ", avg. density: " + CustomRoadAI.laneMeanDensities[curLaneId] + " %";
#if DEBUG
				labelStr += " (" + CustomRoadAI.currentLaneDensities[curLaneId] + "/" + totalDensity + ")";
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
			Array16<NetSegment> segments = Singleton<NetManager>.instance.m_segments;
			bool handleHovered = false;
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

				// draw speed limits
				if (_toolMode != ToolMode.VehicleRestrictions || i != SelectedSegment) { // no speed limit overlay on selected segment when in vehicle restrictions mode
					if (drawSpeedLimitHandles((ushort)i, _toolMode != ToolMode.SpeedLimits))
						handleHovered = true;
				}

				// draw vehicle restrictions
				if (drawVehicleRestrictionHandles((ushort)i, _toolMode != ToolMode.VehicleRestrictions || i != SelectedSegment))
					handleHovered = true;

				var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
				var diff = centerPos - camPos;
				if (diff.magnitude > DebugCloseLod)
					continue; // do not draw if too distant

				if (Options.nodesOverlay) {
					var zoom = 1.0f / diff.magnitude * 150f;

					_counterStyle.fontSize = (int)(12f * zoom);
					_counterStyle.normal.textColor = new Color(1f, 0f, 0f);

					String labelStr = "Segment " + i;
#if DEBUG
					labelStr += ", flags: " + segments.m_buffer[i].m_flags.ToString() + ", condition: " + segments.m_buffer[i].m_condition;
#endif
					labelStr += "\nTraffic: " + segments.m_buffer[i].m_trafficDensity + " %";

					float meanLaneSpeed = 0f;
					
					int lIndex = 0;
					uint laneId = segments.m_buffer[i].m_lanes;
					int validLanes = 0;
					while (lIndex < segmentInfo.m_lanes.Length && laneId != 0u) {
						NetInfo.Lane lane = segmentInfo.m_lanes[lIndex];
						if (lane.CheckType(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car)) {
							if (CustomRoadAI.laneMeanSpeeds[laneId] >= 0) {
								meanLaneSpeed += (float)CustomRoadAI.laneMeanSpeeds[laneId];
								++validLanes;
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
						_guiLanes(ref segments.m_buffer[i], ref segmentInfo);
				}
			}
			overlayHandleHovered = handleHovered;
		}

		/// <summary>
		/// Displays node ids over nodes
		/// </summary>
		private void _guiNodes() {
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
			Array16<Vehicle> vehicles = Singleton<VehicleManager>.instance.m_vehicles;
			for (int i = 1; i < vehicles.m_size; ++i) {
				Vehicle vehicle = vehicles.m_buffer[i];
				if (vehicle.m_flags == Vehicle.Flags.None) // node is unused
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

				VehiclePosition vPos = TrafficPriority.GetVehiclePosition((ushort)i);
				String labelStr = "Veh. " + i + " @ " + String.Format("{0:0.##}", vehicle.GetLastFrameVelocity().magnitude) + ", len: " + vehicle.CalculateTotalLength((ushort)i) + ", state: " + vPos.CarState;
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
			Array16<CitizenInstance> citizenInstances = Singleton<CitizenManager>.instance.m_instances;
			for (int i = 1; i < citizenInstances.m_size; ++i) {
				if (i % (int)Options.someValue4 != 0)
					continue;

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

		ExtVehicleType[] infoSignsToDisplay = new ExtVehicleType[] { ExtVehicleType.Bicycle, ExtVehicleType.Bus, ExtVehicleType.Taxi, ExtVehicleType.Tram, ExtVehicleType.CargoTruck, ExtVehicleType.Service };

		private void _guiTimedTrafficLights() {
			_cursorInSecondaryPanel = false;

			GUILayout.Window(253, _windowRect, _guiTimedControlPanel, Translation.GetString("Timed_traffic_lights_manager"));

			_cursorInSecondaryPanel = _windowRect.Contains(Event.current.mousePosition);

			var hoveredSegment = false;

			foreach (var nodeId in SelectedNodeIndexes) {
				var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(nodeId);
				nodeSimulation.housekeeping(true);
				if (nodeSimulation == null || !nodeSimulation.IsTimedLight())
					continue;
				TimedTrafficLights timedNode = nodeSimulation.TimedLight;

				var nodePos = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position;

				var nodeScreenPos = Camera.main.WorldToScreenPoint(nodePos);
				nodeScreenPos.y = Screen.height - nodeScreenPos.y;

				if (nodeScreenPos.z < 0)
					continue;

				var diff = nodePos - Camera.main.transform.position;
				var zoom = 1.0f / diff.magnitude * 100f;

				for (var i = 0; i < 8; i++) {
					ushort srcSegmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(i); // source segment

					if (srcSegmentId == 0 || nodeSimulation == null ||
						!CustomTrafficLights.IsSegmentLight(nodeId, srcSegmentId)) continue;

					CustomSegmentLights liveSegmentLights = CustomTrafficLights.GetSegmentLights(nodeId, srcSegmentId);
					if (! nodeSimulation.IsTimedLightActive()) {
						liveSegmentLights.MakeRedOrGreen();
					}

					var offset = 17f;
					Vector3 segmentLightPos = nodePos;

					if (Singleton<NetManager>.instance.m_segments.m_buffer[srcSegmentId].m_startNode == nodeId) {
						segmentLightPos.x += Singleton<NetManager>.instance.m_segments.m_buffer[srcSegmentId].m_startDirection.x * offset;
						segmentLightPos.y += Singleton<NetManager>.instance.m_segments.m_buffer[srcSegmentId].m_startDirection.y;
						segmentLightPos.z += Singleton<NetManager>.instance.m_segments.m_buffer[srcSegmentId].m_startDirection.z * offset;
					} else {
						segmentLightPos.x += Singleton<NetManager>.instance.m_segments.m_buffer[srcSegmentId].m_endDirection.x * offset;
						segmentLightPos.y += Singleton<NetManager>.instance.m_segments.m_buffer[srcSegmentId].m_endDirection.y;
						segmentLightPos.z += Singleton<NetManager>.instance.m_segments.m_buffer[srcSegmentId].m_endDirection.z * offset;
					}

					var screenPos = Camera.main.WorldToScreenPoint(segmentLightPos);
					screenPos.y = Screen.height - screenPos.y;

					var timedActive = nodeSimulation.IsTimedLightActive();
					var guiColor = GUI.color;

					var manualPedestrianWidth = 36f * zoom;
					var manualPedestrianHeight = 35f * zoom;

					var pedestrianWidth = 36f * zoom;
					var pedestrianHeight = 61f * zoom;

					// original / 2.5
					var lightWidth = 41f * zoom;
					var lightHeight = 97f * zoom;

					// SWITCH MODE BUTTON
					var modeWidth = 41f * zoom;
					var modeHeight = 38f * zoom;

					if (liveSegmentLights.PedestrianLightState != null) {
						// pedestrian light

						// SWITCH MANUAL PEDESTRIAN LIGHT BUTTON
						if (!timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
							guiColor.a = _hoveredButton[0] == srcSegmentId &&
										 (_hoveredButton[1] == 1 || _hoveredButton[1] == 2) &&
										 _hoveredNode == nodeId
								? 0.92f
								: 0.6f;

							GUI.color = guiColor;

							var myRect2 = new Rect(screenPos.x - manualPedestrianWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) + 5f * zoom,
								screenPos.y - manualPedestrianHeight / 2 - 9f * zoom, manualPedestrianWidth,
								manualPedestrianHeight);

							GUI.DrawTexture(myRect2, liveSegmentLights.ManualPedestrianMode ? TrafficLightToolTextureResources.PedestrianModeManualTexture2D : TrafficLightToolTextureResources.PedestrianModeAutomaticTexture2D);

							if (myRect2.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
								_hoveredButton[0] = srcSegmentId;
								_hoveredButton[1] = 1;
								_hoveredNode = nodeId;
								hoveredSegment = true;

								if (checkClicked()) {
									liveSegmentLights.ManualPedestrianMode = true;
								}
							}
						}

						// SWITCH PEDESTRIAN LIGHT
						guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 2 && _hoveredNode == nodeId ? 0.92f : 0.6f;

						GUI.color = guiColor;

						var myRect3 = new Rect(screenPos.x - pedestrianWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) + 5f * zoom, screenPos.y - pedestrianHeight / 2 + 22f * zoom, pedestrianWidth, pedestrianHeight);

						switch (liveSegmentLights.PedestrianLightState) {
							case RoadBaseAI.TrafficLightState.Green:
								GUI.DrawTexture(myRect3, TrafficLightToolTextureResources.PedestrianGreenLightTexture2D);
								break;
							case RoadBaseAI.TrafficLightState.Red:
							default:
								GUI.DrawTexture(myRect3, TrafficLightToolTextureResources.PedestrianRedLightTexture2D);
								break;
						}

						if (myRect3.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
							_hoveredButton[0] = srcSegmentId;
							_hoveredButton[1] = 2;
							_hoveredNode = nodeId;
							hoveredSegment = true;

							if (Input.GetMouseButtonDown(0) && !mouseClickProcessed && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
								mouseClickProcessed = true;

								if (!liveSegmentLights.ManualPedestrianMode) {
									liveSegmentLights.ManualPedestrianMode = true;
								} else {
									liveSegmentLights.ChangeLightPedestrian();
								}
							}
						}
					}

					int lightOffset = -1;
					foreach (ExtVehicleType vehicleType in liveSegmentLights.VehicleTypes) {
						++lightOffset;
						CustomSegmentLight liveSegmentLight = liveSegmentLights.GetCustomLight(vehicleType);

						Vector3 offsetScreenPos = screenPos;
						offsetScreenPos.y -= (lightHeight + 10f * zoom) * lightOffset;

						if (!timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
							guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == -1 &&
										 _hoveredNode == nodeId
								? 0.92f
								: 0.6f;

							GUI.color = guiColor;

							var myRect1 = new Rect(offsetScreenPos.x - modeWidth / 2,
								offsetScreenPos.y - modeHeight / 2 + modeHeight - 7f * zoom, modeWidth, modeHeight);

							GUI.DrawTexture(myRect1, TrafficLightToolTextureResources.LightModeTexture2D);

							if (myRect1.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
								_hoveredButton[0] = srcSegmentId;
								_hoveredButton[1] = -1;
								_hoveredNode = nodeId;
								hoveredSegment = true;

								if (checkClicked()) {
									liveSegmentLight.ChangeMode();
									timedNode.ChangeLightMode(srcSegmentId, vehicleType, liveSegmentLight.CurrentMode);
								}
							}
						}

						if (lightOffset > 0) {
							// Info sign
							var infoWidth = 56.125f * zoom;
							var infoHeight = 51.375f * zoom;

							int numInfos = 0;
							for (int k = 0; k < infoSignsToDisplay.Length; ++k) {
								if ((infoSignsToDisplay[k] & vehicleType) == ExtVehicleType.None)
									continue;
								var infoRect = new Rect(offsetScreenPos.x + modeWidth / 2f + 7f * zoom * (float)(numInfos+1) + infoWidth * (float)numInfos, offsetScreenPos.y - infoHeight / 2f, infoWidth, infoHeight);
								guiColor.a = 0.6f;
								GUI.DrawTexture(infoRect, TrafficLightToolTextureResources.VehicleInfoSignTextures[infoSignsToDisplay[k]]);
								++numInfos;
							}
						}
						

						

#if DEBUG
						if (timedActive /*&& _timedShowNumbers*/) {
							var prioSeg = TrafficPriority.GetPrioritySegment(nodeId, srcSegmentId);

							var counterSize = 20f * zoom;
							var yOffset = counterSize + 77f * zoom - modeHeight * 2;
							var carNumRect = new Rect(offsetScreenPos.x, offsetScreenPos.y - yOffset, counterSize, counterSize);
							var segIdRect = new Rect(offsetScreenPos.x, offsetScreenPos.y - yOffset - counterSize - 2f, counterSize, counterSize);

							_counterStyle.fontSize = (int)(15f * zoom);
							_counterStyle.normal.textColor = new Color(1f, 0f, 0f);

							String labelStr = "n/a";
							if (prioSeg != null) {
								labelStr = prioSeg.getNumCars().ToString() + " " + Translation.GetString("incoming");
								/*for (int k = 0; k < prioSeg.numLanes; ++k) {
									if (k > 0)
										labelStr += "/";
									labelStr += prioSeg.CarsOnLanes[k];
								}*/
							}
							GUI.Label(carNumRect, labelStr, _counterStyle);

							_counterStyle.normal.textColor = new Color(1f, 0f, 0f);
							GUI.Label(segIdRect, Translation.GetString("Segment") + " " + srcSegmentId, _counterStyle);
						}
#endif

						if (lightOffset == 0 && liveSegmentLights.PedestrianLightState != null) {
							// PEDESTRIAN COUNTER
							if (timedActive && _timedShowNumbers) {
								var counterSize = 20f * zoom;

								var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 3);

								float numOffset;

								if (liveSegmentLights.PedestrianLightState == RoadBaseAI.TrafficLightState.Red) { // TODO check this
									numOffset = counterSize + 53f * zoom - modeHeight * 2;
								} else {
									numOffset = counterSize + 29f * zoom - modeHeight * 2;
								}

								var myRectCounterNum =
									new Rect(offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 1f) + 24f * zoom - pedestrianWidth / 2,
										offsetScreenPos.y - numOffset, counterSize, counterSize);

								_counterStyle.fontSize = (int)(15f * zoom);
								_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

								GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

								if (myRectCounterNum.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
									_hoveredButton[0] = srcSegmentId;
									_hoveredButton[1] = 2;
									_hoveredNode = nodeId;
									hoveredSegment = true;
								}
							}
						}

						SegmentGeometry geometry = CustomRoadAI.GetSegmentGeometry(srcSegmentId);
						if (geometry.IsOutgoingOneWay(nodeId)) continue;

						var hasLeftSegment = geometry.HasLeftSegment(nodeId);
						var hasForwardSegment = geometry.HasStraightSegment(nodeId);
						var hasRightSegment = geometry.HasRightSegment(nodeId);

						switch (liveSegmentLight.CurrentMode) {
							case CustomSegmentLight.Mode.Simple: {
									// no arrow light
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 3 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var myRect4 =
										new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) - pedestrianWidth + 5f * zoom,
											offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									drawMainLightTexture(liveSegmentLight.LightMain, myRect4);

									if (myRect4.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 3;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (Input.GetMouseButtonDown(0) && !mouseClickProcessed && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											mouseClickProcessed = true;
											liveSegmentLight.ChangeLightMain();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 0);

										float numOffset;

										if (liveSegmentLight.LightMain == RoadBaseAI.TrafficLightState.Red) {
											numOffset = counterSize + 96f * zoom - modeHeight * 2;
										} else {
											numOffset = counterSize + 40f * zoom - modeHeight * 2;
										}

										var myRectCounterNum =
											new Rect(offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) - pedestrianWidth + 5f * zoom,
												offsetScreenPos.y - numOffset, counterSize, counterSize);

										_counterStyle.fontSize = (int)(18f * zoom);
										_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

										GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

										if (myRectCounterNum.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
											_hoveredButton[0] = srcSegmentId;
											_hoveredButton[1] = 3;
											_hoveredNode = nodeId;
											hoveredSegment = true;
										}
									}

									GUI.color = guiColor;
								}
								break;
							case CustomSegmentLight.Mode.SingleLeft:
								if (hasLeftSegment) {
									// left arrow light
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 3 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var myRect4 =
										new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth * 2 : lightWidth) - pedestrianWidth + 5f * zoom,
											offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									drawLeftLightTexture(liveSegmentLight.LightLeft, myRect4);

									if (myRect4.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 3;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (Input.GetMouseButtonDown(0) && !mouseClickProcessed && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											mouseClickProcessed = true;
											liveSegmentLight.ChangeLightLeft();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 1);

										float numOffset;

										if (liveSegmentLight.LightLeft == RoadBaseAI.TrafficLightState.Red) {
											numOffset = counterSize + 96f * zoom - modeHeight * 2;
										} else {
											numOffset = counterSize + 40f * zoom - modeHeight * 2;
										}

										var myRectCounterNum =
											new Rect(offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) - pedestrianWidth + 5f * zoom - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth * 2 : lightWidth),
												offsetScreenPos.y - numOffset, counterSize, counterSize);

										_counterStyle.fontSize = (int)(18f * zoom);
										_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

										GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

										if (myRectCounterNum.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
											_hoveredButton[0] = srcSegmentId;
											_hoveredButton[1] = 3;
											_hoveredNode = nodeId;
											hoveredSegment = true;
										}
									}
								}

								// forward-right arrow light
								guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 4 && _hoveredNode == nodeId ? 0.92f : 0.6f;

								GUI.color = guiColor;

								var myRect5 =
									new Rect(offsetScreenPos.x - lightWidth / 2 - pedestrianWidth - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) + 5f * zoom,
										offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

								if (hasForwardSegment && hasRightSegment) {
									drawForwardRightLightTexture(liveSegmentLight.LightMain, myRect5);
								} else if (!hasRightSegment) {
									drawStraightLightTexture(liveSegmentLight.LightMain, myRect5);
								} else {
									drawRightLightTexture(liveSegmentLight.LightMain, myRect5);
								}

								if (myRect5.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
									_hoveredButton[0] = srcSegmentId;
									_hoveredButton[1] = 4;
									_hoveredNode = nodeId;
									hoveredSegment = true;

									if (Input.GetMouseButtonDown(0) && !mouseClickProcessed && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
										mouseClickProcessed = true;
										liveSegmentLight.ChangeLightMain();
									}
								}

								// COUNTER
								if (timedActive && _timedShowNumbers) {
									var counterSize = 20f * zoom;

									var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 0);

									float numOffset;

									if (liveSegmentLight.LightMain == RoadBaseAI.TrafficLightState.Red) {
										numOffset = counterSize + 96f * zoom - modeHeight * 2;
									} else {
										numOffset = counterSize + 40f * zoom - modeHeight * 2;
									}

									var myRectCounterNum =
										new Rect(offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) - pedestrianWidth + 5f * zoom - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f),
											offsetScreenPos.y - numOffset, counterSize, counterSize);

									_counterStyle.fontSize = (int)(18f * zoom);
									_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

									GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

									if (myRectCounterNum.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 4;
										_hoveredNode = nodeId;
										hoveredSegment = true;
									}
								}
								break;
							case CustomSegmentLight.Mode.SingleRight: {
									// forward-left light
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 3 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var myRect4 = new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth * 2 : lightWidth) - pedestrianWidth + 5f * zoom,
										offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									var lightType = 0;

									if (hasForwardSegment && hasLeftSegment) {
										drawForwardLeftLightTexture(liveSegmentLight.LightMain, myRect4);

										lightType = 1;
									} else if (!hasLeftSegment) {
										if (!hasRightSegment) {
											myRect4 = new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
												offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);
										}

										drawStraightLightTexture(liveSegmentLight.LightMain, myRect4);
									} else {
										if (!hasRightSegment) {
											myRect4 = new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
												offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);
										}

										drawMainLightTexture(liveSegmentLight.LightMain, myRect4);
									}


									if (myRect4.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 3;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (Input.GetMouseButtonDown(0) && !mouseClickProcessed && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											mouseClickProcessed = true;
											liveSegmentLight.ChangeLightMain();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, lightType);

										float numOffset;

										if (liveSegmentLight.LightMain == RoadBaseAI.TrafficLightState.Red) {
											numOffset = counterSize + 96f * zoom - modeHeight * 2;
										} else {
											numOffset = counterSize + 40f * zoom - modeHeight * 2;
										}

										var myRectCounterNum =
											new Rect(offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) - pedestrianWidth + 5f * zoom - (_timedPanelAdd || _timedEditStep >= 0 ? (hasRightSegment ? lightWidth * 2 : lightWidth) : (hasRightSegment ? lightWidth : 0f)),
												offsetScreenPos.y - numOffset, counterSize, counterSize);

										_counterStyle.fontSize = (int)(18f * zoom);
										_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

										GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

										if (myRectCounterNum.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
											_hoveredButton[0] = srcSegmentId;
											_hoveredButton[1] = 3;
											_hoveredNode = nodeId;
											hoveredSegment = true;
										}
									}

									// right arrow light
									if (hasRightSegment) {
										guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 4 &&
													 _hoveredNode == nodeId
											? 0.92f
											: 0.6f;

										GUI.color = guiColor;

										var rect5 =
											new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
												offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

										drawRightLightTexture(liveSegmentLight.LightRight, rect5);

										if (rect5.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
											_hoveredButton[0] = srcSegmentId;
											_hoveredButton[1] = 4;
											_hoveredNode = nodeId;
											hoveredSegment = true;

											if (Input.GetMouseButtonDown(0) && !mouseClickProcessed && !timedActive &&
												(_timedPanelAdd || _timedEditStep >= 0)) {
												mouseClickProcessed = true;
												liveSegmentLight.ChangeLightRight();
											}
										}

										// COUNTER
										if (timedActive && _timedShowNumbers) {
											var counterSize = 20f * zoom;

											var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 2);

											float numOffset;

											if (liveSegmentLight.LightRight == RoadBaseAI.TrafficLightState.Red) {
												numOffset = counterSize + 96f * zoom - modeHeight * 2;
											} else {
												numOffset = counterSize + 40f * zoom - modeHeight * 2;
											}

											var myRectCounterNum =
												new Rect(
													offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) -
													pedestrianWidth + 5f * zoom -
													(_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f),
													offsetScreenPos.y - numOffset, counterSize, counterSize);

											_counterStyle.fontSize = (int)(18f * zoom);
											_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

											GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

											if (myRectCounterNum.Contains(Event.current.mousePosition) &&
												!_cursorInSecondaryPanel) {
												_hoveredButton[0] = srcSegmentId;
												_hoveredButton[1] = 4;
												_hoveredNode = nodeId;
												hoveredSegment = true;
											}
										}
									}
								}
								break;
							default:
								// left arrow light
								if (hasLeftSegment) {
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 3 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var offsetLight = lightWidth;

									if (hasRightSegment)
										offsetLight += lightWidth;

									if (hasForwardSegment)
										offsetLight += lightWidth;

									var myRect4 =
										new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth) - pedestrianWidth + 5f * zoom,
											offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									drawLeftLightTexture(liveSegmentLight.LightLeft, myRect4);

									if (myRect4.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 3;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (Input.GetMouseButtonDown(0) && !mouseClickProcessed && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											mouseClickProcessed = true;
											liveSegmentLight.ChangeLightLeft();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 1);

										float numOffset;

										if (liveSegmentLight.LightLeft == RoadBaseAI.TrafficLightState.Red) {
											numOffset = counterSize + 96f * zoom - modeHeight * 2;
										} else {
											numOffset = counterSize + 40f * zoom - modeHeight * 2;
										}

										var myRectCounterNum =
											new Rect(
												offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) -
												pedestrianWidth + 5f * zoom -
												(_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth),
												offsetScreenPos.y - numOffset, counterSize, counterSize);

										_counterStyle.fontSize = (int)(18f * zoom);
										_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

										GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

										if (myRectCounterNum.Contains(Event.current.mousePosition) &&
											!_cursorInSecondaryPanel) {
											_hoveredButton[0] = srcSegmentId;
											_hoveredButton[1] = 3;
											_hoveredNode = nodeId;
											hoveredSegment = true;
										}
									}
								}

								// forward arrow light
								if (hasForwardSegment) {
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 4 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var offsetLight = lightWidth;

									if (hasRightSegment)
										offsetLight += lightWidth;

									var myRect6 =
										new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth) - pedestrianWidth + 5f * zoom,
											offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									drawStraightLightTexture(liveSegmentLight.LightMain, myRect6);

									if (myRect6.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 4;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (Input.GetMouseButtonDown(0) && !mouseClickProcessed && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											mouseClickProcessed = true;
											liveSegmentLight.ChangeLightMain();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 0);

										float numOffset;

										if (liveSegmentLight.LightMain == RoadBaseAI.TrafficLightState.Red) {
											numOffset = counterSize + 96f * zoom - modeHeight * 2;
										} else {
											numOffset = counterSize + 40f * zoom - modeHeight * 2;
										}

										var myRectCounterNum =
											new Rect(
												offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) -
												pedestrianWidth + 5f * zoom -
												(_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth),
												offsetScreenPos.y - numOffset, counterSize, counterSize);

										_counterStyle.fontSize = (int)(18f * zoom);
										_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

										GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

										if (myRectCounterNum.Contains(Event.current.mousePosition) &&
											!_cursorInSecondaryPanel) {
											_hoveredButton[0] = srcSegmentId;
											_hoveredButton[1] = 4;
											_hoveredNode = nodeId;
											hoveredSegment = true;
										}
									}
								}

								// right arrow light
								if (hasRightSegment) {
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 5 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var rect6 =
										new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
											offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									drawRightLightTexture(liveSegmentLight.LightRight, rect6);

									if (rect6.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 5;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (Input.GetMouseButtonDown(0) && !mouseClickProcessed && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											mouseClickProcessed = true;
											liveSegmentLight.ChangeLightRight();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 2);

										float numOffset;

										if (liveSegmentLight.LightRight == RoadBaseAI.TrafficLightState.Red) {
											numOffset = counterSize + 96f * zoom - modeHeight * 2;
										} else {
											numOffset = counterSize + 40f * zoom - modeHeight * 2;
										}

										var myRectCounterNum =
											new Rect(
												offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) -
												pedestrianWidth + 5f * zoom -
												(_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f),
												offsetScreenPos.y - numOffset, counterSize, counterSize);

										_counterStyle.fontSize = (int)(18f * zoom);
										_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

										GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

										if (myRectCounterNum.Contains(Event.current.mousePosition) &&
											!_cursorInSecondaryPanel) {
											_hoveredButton[0] = srcSegmentId;
											_hoveredButton[1] = 5;
											_hoveredNode = nodeId;
											hoveredSegment = true;
										}
									}
								}
								break;
						} // end switch liveSegmentLight.CurrentMode
					} // end foreach light
				} // end foreach segment
			} // end foreach node

			if (!hoveredSegment) {
				_hoveredButton[0] = 0;
				_hoveredButton[1] = 0;
			}
		}

		private void drawStraightLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
			switch (state) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.GreenLightStraightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.GreenToRed:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
				default:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.RedLightStraightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.RedToGreen:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightStraightTexture2D);
					break;
			}
		}

		private void drawForwardLeftLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
			switch (state) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.GreenLightForwardLeftTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.GreenToRed:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
				default:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.RedLightForwardLeftTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.RedToGreen:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightForwardLeftTexture2D);
					break;
			}
		}

		private void drawForwardRightLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
			switch (state) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.GreenLightForwardRightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.GreenToRed:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
				default:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.RedLightForwardRightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.RedToGreen:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightForwardRightTexture2D);
					break;
			}
		}

		private void drawLeftLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
			switch (state) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.GreenLightLeftTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.GreenToRed:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
				default:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.RedLightLeftTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.RedToGreen:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightLeftTexture2D);
					break;
			}
		}

		private void drawRightLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
			switch (state) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.GreenLightRightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.GreenToRed:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
				default:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.RedLightRightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.RedToGreen:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightRightTexture2D);
					break;
			}
		}

		private void drawMainLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
			switch (state) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.GreenLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.GreenToRed:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
				default:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.RedLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.RedToGreen:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowRedLightTexture2D);
					break;
			}
		}

		private static List<object[]> getSortedVehicleLanes(ushort segmentId, NetInfo info, ushort? nodeId) { // TODO refactor together with getSegmentNumVehicleLanes, especially the vehicle type and lane type checks
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

		private int getSegmentNumVehicleLanes(ushort segmentId, ushort? nodeId, out int numDirections) {
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

		private void _guiLaneChange() {
			_cursorInSecondaryPanel = false;

			if (SelectedNode == 0 || SelectedSegment == 0) return;

			int numDirections;
			int numLanes = getSegmentNumVehicleLanes(SelectedSegment, SelectedNode, out numDirections);
			if (numLanes <= 0) {
				SelectedNode = 0;
				SelectedSegment = 0;
				return;
			}

			var style = new GUIStyle {
				normal = { background = _secondPanelTexture },
				alignment = TextAnchor.MiddleCenter,
				border =
				{
					bottom = 2,
					top = 2,
					right = 2,
					left = 2
				}
			};

            Vector3 nodePos = Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode].m_position;
			var screenPos = Camera.main.WorldToScreenPoint(nodePos);
			screenPos.y = Screen.height - screenPos.y;
			Log._Debug($"node pos of {SelectedNode}: {nodePos.ToString()} {screenPos.ToString()}");
			if (screenPos.z < 0)
				return;
			var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
			var diff = nodePos - camPos;

			if (diff.magnitude > PriorityCloseLod)
				return; // do not draw if too distant

			int width = Math.Max(3 * 128 + 20, numLanes * 128);
			var windowRect3 = new Rect(screenPos.x - width/2, screenPos.y - 70, width, 125);
			GUILayout.Window(250, windowRect3, _guiLaneChangeWindow, "", style);
			_cursorInSecondaryPanel = windowRect3.Contains(Event.current.mousePosition);
		}

		private void _guiLaneChangeWindow(int num) {
			var info = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegment].Info;

			List<object[]> laneList = getSortedVehicleLanes(SelectedSegment, info, SelectedNode);
			SegmentGeometry geometry = CustomRoadAI.GetSegmentGeometry(SelectedSegment);

			GUILayout.BeginHorizontal();

			for (var i = 0; i < laneList.Count; i++) {
				var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[(uint)laneList[i][0]].m_flags;

				var style1 = new GUIStyle("button");
				var style2 = new GUIStyle("button") {
					normal = { textColor = new Color32(255, 0, 0, 255) },
					hover = { textColor = new Color32(255, 0, 0, 255) },
					focused = { textColor = new Color32(255, 0, 0, 255) }
				};

				var laneStyle = new GUIStyle { contentOffset = new Vector2(12f, 0f) };

				var laneTitleStyle = new GUIStyle {
					contentOffset = new Vector2(36f, 2f),
					normal = { textColor = new Color(1f, 1f, 1f) }
				};

				GUILayout.BeginVertical(laneStyle);
				GUILayout.Label(Translation.GetString("Lane") + " " + (i + 1), laneTitleStyle);
				GUILayout.BeginVertical();
				GUILayout.BeginHorizontal();
				if (!Flags.applyLaneArrowFlags((uint)laneList[i][0])) {
					Flags.removeLaneArrowFlags((uint)laneList[i][0]);
				}
				if (GUILayout.Button("←", ((flags & NetLane.Flags.Left) == NetLane.Flags.Left ? style1 : style2), GUILayout.Width(35), GUILayout.Height(25))) {
					if (!Flags.toggleLaneArrowFlags((uint)laneList[i][0], Flags.LaneArrows.Left) && SelectedNode > 0)
						showTooltip(Translation.GetString("Lane_Arrow_Changer_Disabled"), Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode].m_position);
				}
				if (GUILayout.Button("↑", ((flags & NetLane.Flags.Forward) == NetLane.Flags.Forward ? style1 : style2), GUILayout.Width(25), GUILayout.Height(35))) {
					if (!Flags.toggleLaneArrowFlags((uint)laneList[i][0], Flags.LaneArrows.Forward) && SelectedNode > 0)
						showTooltip(Translation.GetString("Lane_Arrow_Changer_Disabled"), Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode].m_position);
				}
				if (GUILayout.Button("→", ((flags & NetLane.Flags.Right) == NetLane.Flags.Right ? style1 : style2), GUILayout.Width(35), GUILayout.Height(25))) {
					if (!Flags.toggleLaneArrowFlags((uint)laneList[i][0], Flags.LaneArrows.Right) && SelectedNode > 0)
						showTooltip(Translation.GetString("Lane_Arrow_Changer_Disabled"), Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode].m_position);
				}
				GUILayout.EndHorizontal();
				GUILayout.EndVertical();
				GUILayout.EndVertical();
			}

			GUILayout.EndHorizontal();

			GUILayout.BeginVertical();

			bool startNode = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegment].m_startNode == SelectedNode;

			if (!geometry.AreHighwayRulesEnabled(startNode)) {
				if (!geometry.IsOneWay()) {
					Flags.setUTurnAllowed(SelectedSegment, startNode, GUILayout.Toggle(Flags.getUTurnAllowed(SelectedSegment, startNode), Translation.GetString("Allow_u-turns") + " (BETA feature)", new GUILayoutOption[] { }));
				}
				if (geometry.HasOutgoingStraightSegment(SelectedNode)) {
					Flags.setStraightLaneChangingAllowed(SelectedSegment, startNode, GUILayout.Toggle(Flags.getStraightLaneChangingAllowed(SelectedSegment, startNode), Translation.GetString("Allow_lane_changing_for_vehicles_going_straight"), new GUILayoutOption[] { }));
				}
				Flags.setEnterWhenBlockedAllowed(SelectedSegment, startNode, GUILayout.Toggle(Flags.getEnterWhenBlockedAllowed(SelectedSegment, startNode), Translation.GetString("Allow_vehicles_to_enter_a_blocked_junction"), new GUILayoutOption[] { }));
			}

			GUILayout.EndVertical();
		}

		private static ExtVehicleType[] roadVehicleTypes = new ExtVehicleType[] { ExtVehicleType.PassengerCar, ExtVehicleType.Bus, ExtVehicleType.Taxi, ExtVehicleType.CargoTruck, ExtVehicleType.Service, ExtVehicleType.Emergency };
		private static ExtVehicleType[] railVehicleTypes = new ExtVehicleType[] { ExtVehicleType.PassengerTrain, ExtVehicleType.CargoTrain };

		private bool drawVehicleRestrictionHandles(ushort segmentId, bool viewOnly) {
			if (!LoadingExtension.IsPathManagerCompatible) {
				return false;
			}

			if (viewOnly && !Options.vehicleRestrictionsOverlay && _toolMode != ToolMode.VehicleRestrictions)
				return false;

			Vector3 center = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_bounds.center;

			var screenPos = Camera.main.WorldToScreenPoint(center);
			screenPos.y = Screen.height - screenPos.y;
			if (screenPos.z < 0)
				return false;
			var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
			var diff = center - camPos;

			if (diff.magnitude > PriorityCloseLod)
				return false; // do not draw if too distant

			int numDirections;
			int numLanes = getSegmentNumVehicleLanes(segmentId, null, out numDirections);

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

			if (!viewOnly)
				Log._Debug($"xu: {xu.ToString()} yu: {yu.ToString()} center: {center.ToString()} zero: {zero.ToString()} numLanes: {numLanes} numDirections: {numDirections}");

			uint x = 0;
			var guiColor = GUI.color;
			List<object[]> sortedLanes = getSortedVehicleLanes(segmentId, segmentInfo, null);
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

				ExtVehicleType allowedTypes = VehicleRestrictionsManager.GetAllowedVehicleTypes(segmentId, laneIndex, laneId, laneInfo);

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

					if (hoveredHandle && !mouseClickProcessed && Input.GetMouseButtonDown(0)) {
						// toggle vehicle restrictions
						Log._Debug($"Setting vehicle restrictions of segment {segmentId}, lane idx {laneIndex}, {vehicleType.ToString()} to {!allowed}");
						VehicleRestrictionsManager.ToggleAllowedType(segmentId, laneIndex, laneId, laneInfo, vehicleType, !allowed);
						mouseClickProcessed = true;
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

			var zoom = 1.0f / diff.magnitude * 100f;
			var size = (viewOnly ? 0.7f : 1f) * vehicleRestrictionsSignSize * zoom;

			var boundingBox = new Rect(signScreenPos.x - size / 2, signScreenPos.y - size / 2, size, size);
			hoveredHandle = !viewOnly && IsMouseOver(boundingBox);
			if (hoveredHandle) {
				// mouse hovering over sign
				guiColor.a = 0.8f;
			} else {
				guiColor.a = 0.5f;
			}

			GUI.color = guiColor;
			GUI.DrawTexture(boundingBox, signTexture);
		}

		private static void calculateSegmentCenterByDir(ushort segmentId, Dictionary<NetInfo.Direction, Vector3> segmentCenterByDir) {
			segmentCenterByDir.Clear();

			NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			Dictionary<NetInfo.Direction, int> numCentersByDir = new Dictionary<NetInfo.Direction, int>();
			uint laneIndex = 0;
			while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
				if ((segmentInfo.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == NetInfo.LaneType.None)
					goto nextIter;

				NetInfo.Direction dir = segmentInfo.m_lanes[laneIndex].m_direction;
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

		private void _guiSpeedLimits() {
			_cursorInSecondaryPanel = false;

			var style = new GUIStyle {
				normal = { background = _secondPanelTexture },
				alignment = TextAnchor.MiddleCenter,
				border =
				{
					bottom = 2,
					top = 2,
					right = 2,
					left = 2
				}
			};

			var windowRect = ResizeGUI(new Rect(155, 45, 7 * 105, 210));
			GUILayout.Window(254, windowRect, _guiSpeedLimitsWindow, Translation.GetString("Speed_limits"), style);
			_cursorInSecondaryPanel = windowRect.Contains(Event.current.mousePosition);
		}

		private void _guiVehicleRestrictions() {
			if (SelectedSegment <= 0)
				return;

			_cursorInSecondaryPanel = false;

			var style = new GUIStyle {
				normal = { background = _secondPanelTexture },
				alignment = TextAnchor.MiddleCenter,
				border =
				{
					bottom = 2,
					top = 2,
					right = 2,
					left = 2
				}
			};

			var windowRect = ResizeGUI(new Rect(155, 45, 620, 80));
			GUILayout.Window(255, windowRect, _guiVehicleRestrictionsWindow, Translation.GetString("Vehicle_restrictions"), style);
			_cursorInSecondaryPanel = windowRect.Contains(Event.current.mousePosition);
		}

		private void _guiVehicleRestrictionsWindow(int num) {
			if (GUILayout.Button(Translation.GetString("Invert"))) {
				// invert pattern

				NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegment].Info;
				List<object[]> sortedLanes = getSortedVehicleLanes(SelectedSegment, segmentInfo, null); // TODO does not need to be sorted, but every lane should be a vehicle lane
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

					ExtVehicleType allowedTypes = VehicleRestrictionsManager.GetAllowedVehicleTypes(SelectedSegment, laneIndex, laneId, laneInfo);
					allowedTypes = ~allowedTypes & baseMask;
					VehicleRestrictionsManager.SetAllowedVehicleTypes(SelectedSegment, laneIndex, laneId, allowedTypes);
				}
			}

			GUILayout.BeginHorizontal();
			if (GUILayout.Button(Translation.GetString("Allow_all_vehicles"))) {
				// allow all vehicle types

				NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegment].Info;
				List<object[]> sortedLanes = getSortedVehicleLanes(SelectedSegment, segmentInfo, null); // TODO does not need to be sorted, but every lane should be a vehicle lane
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

					VehicleRestrictionsManager.SetAllowedVehicleTypes(SelectedSegment, laneIndex, laneId, baseMask);
				}
			}

			if (GUILayout.Button(Translation.GetString("Ban_all_vehicles"))) {
				// ban all vehicle types

				NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegment].Info;
				List<object[]> sortedLanes = getSortedVehicleLanes(SelectedSegment, segmentInfo, null); // TODO does not need to be sorted, but every lane should be a vehicle lane
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

					VehicleRestrictionsManager.SetAllowedVehicleTypes(SelectedSegment, laneIndex, laneId, ~baseMask);
				}
			}
			GUILayout.EndHorizontal();

			if (GUILayout.Button(Translation.GetString("Apply_vehicle_restrictions_to_all_road_segments_between_two_junctions"))) {
				NetInfo selectedSegmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegment].Info;
				List<object[]> selectedSortedLanes = getSortedVehicleLanes(SelectedSegment, selectedSegmentInfo, null);

				LinkedList<ushort> nodesToProcess = new LinkedList<ushort>();
				HashSet<ushort> processedNodes = new HashSet<ushort>();
				HashSet<ushort> processedSegments = new HashSet<ushort>();
				processedSegments.Add(SelectedSegment);

				ushort selectedStartNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegment].m_startNode;
				ushort selectedEndNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegment].m_endNode;

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
						List<object[]> sortedLanes = getSortedVehicleLanes(segmentId, segmentInfo, null);

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
								VehicleRestrictionsManager.SetAllowedVehicleTypes(segmentId, laneIndex, laneId, VehicleRestrictionsManager.GetAllowedVehicleTypes(SelectedSegment, selectedLaneIndex, selectedLaneId, selectedLaneInfo));
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

		private int curSpeedLimitIndex = 0;

		private void _guiSpeedLimitsWindow(int num) {
			GUILayout.BeginHorizontal();

			Color oldColor = GUI.color;
			for (int i = 0; i < SpeedLimitManager.AvailableSpeedLimits.Count; ++i) {
				if (curSpeedLimitIndex != i)
					GUI.color = Color.gray;
				if (GUILayout.Button(TrafficLightToolTextureResources.SpeedLimitTextures[SpeedLimitManager.AvailableSpeedLimits[i]], GUILayout.Width(100), GUILayout.Height(100))) {
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

		private bool drawSpeedLimitHandles(ushort segmentId, bool viewOnly) {
			if (!LoadingExtension.IsPathManagerCompatible) {
				return false;
			}

			if (viewOnly && !Options.speedLimitsOverlay)
				return false;

			// draw speedlimits over mean middle points of lane beziers
				if (!segmentCenterByDir.ContainsKey(segmentId)) {
				segmentCenterByDir.Add(segmentId, new Dictionary<NetInfo.Direction, Vector3>());
				calculateSegmentCenterByDir(segmentId, segmentCenterByDir[segmentId]);
			}

			bool hovered = false;
			foreach (KeyValuePair<NetInfo.Direction, Vector3> e in segmentCenterByDir[segmentId]) {
				Vector3 signPos = e.Value;
				var screenPos = Camera.main.WorldToScreenPoint(signPos);
				screenPos.y = Screen.height - screenPos.y;
				if (screenPos.z < 0)
					return false;
				var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
				var diff = signPos - camPos;

				if (diff.magnitude > PriorityCloseLod)
					return false; // do not draw if too distant

				ItemClass connectionClass = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.GetConnectionClass();
				if (!(connectionClass.m_service == ItemClass.Service.Road ||
					(connectionClass.m_service == ItemClass.Service.PublicTransport && connectionClass.m_subService == ItemClass.SubService.PublicTransportTrain)))
					return false;

				var zoom = 1.0f / diff.magnitude * 100f;
				var size = speedLimitSignSize * zoom;
				var guiColor = GUI.color;
				var boundingBox = new Rect(screenPos.x - size / 2, screenPos.y - size / 2, size, size);
				bool hoveredHandle = !viewOnly && IsMouseOver(boundingBox);

				if (hoveredHandle) {
					// mouse hovering over sign
					hovered = true;
					guiColor.a = 0.8f;
				} else {
					guiColor.a = 0.5f;
				}

				GUI.color = guiColor;

				try {
					GUI.DrawTexture(boundingBox, TrafficLightToolTextureResources.SpeedLimitTextures[SpeedLimitManager.GetCustomSpeedLimit(segmentId, e.Key)]);
				} catch (Exception ex) {
					Log.Error("segment " + segmentId + " limit: " + SpeedLimitManager.GetCustomSpeedLimit(segmentId, e.Key) + ", ex: " + ex.ToString());
				}

				if (hoveredHandle && Input.GetMouseButton(0)) {
					// change the speed limit to the selected one
					ushort speedLimitToSet = SpeedLimitManager.AvailableSpeedLimits[curSpeedLimitIndex];
					Log._Debug($"Setting speed limit of segment {segmentId}, dir {e.Key.ToString()} to {speedLimitToSet}");
					SpeedLimitManager.SetSpeedLimit(segmentId, e.Key, speedLimitToSet);
					//mouseClickProcessed = true;
				}

				guiColor.a = 1f;
				GUI.color = guiColor;
			}
			return hovered;
		}

		private Texture2D MakeTex(int width, int height, Color col) {
			var pix = new Color[width * height];

			for (var i = 0; i < pix.Length; i++)
				pix[i] = col;

			var result = new Texture2D(width, height);
			result.SetPixels(pix);
			result.Apply();

			return result;
		}

		private void CreateZoneBlocks(int segment, ref NetSegment data, NetInfo info) {
			var instance = Singleton<NetManager>.instance;
			var randomizer = new Randomizer(segment);
			var position = instance.m_nodes.m_buffer[data.m_startNode].m_position;
			var position2 = instance.m_nodes.m_buffer[data.m_endNode].m_position;
			var startDirection = data.m_startDirection;
			var endDirection = data.m_endDirection;
			var num = startDirection.x * endDirection.x + startDirection.z * endDirection.z;
			var flag = !NetSegment.IsStraight(position, startDirection, position2, endDirection);
			var num2 = Mathf.Max(8f, info.m_halfWidth);
			var num3 = 32f;
			if (flag) {
				var num4 = VectorUtils.LengthXZ(position2 - position);
				var flag2 = startDirection.x * endDirection.z - startDirection.z * endDirection.x > 0f;
				var flag3 = num < -0.8f || num4 > 50f;
				if (flag2) {
					num2 = -num2;
					num3 = -num3;
				}
				var vector = position - new Vector3(startDirection.z, 0f, -startDirection.x) * num2;
				var vector2 = position2 + new Vector3(endDirection.z, 0f, -endDirection.x) * num2;
				Vector3 vector3;
				Vector3 vector4;
				NetSegment.CalculateMiddlePoints(vector, startDirection, vector2, endDirection, true, true, out vector3, out vector4);
				if (flag3) {
					var num5 = num * 0.025f + 0.04f;
					var num6 = num * 0.025f + 0.06f;
					if (num < -0.9f) {
						num6 = num5;
					}
					var bezier = new Bezier3(vector, vector3, vector4, vector2);
					vector = bezier.Position(num5);
					vector3 = bezier.Position(0.5f - num6);
					vector4 = bezier.Position(0.5f + num6);
					vector2 = bezier.Position(1f - num5);
				} else {
					var bezier2 = new Bezier3(vector, vector3, vector4, vector2);
					vector3 = bezier2.Position(0.86f);
					vector = bezier2.Position(0.14f);
				}
				float num7;
				var vector5 = VectorUtils.NormalizeXZ(vector3 - vector, out num7);
				var num8 = Mathf.FloorToInt(num7 / 8f + 0.01f);
				var num9 = num7 * 0.5f + (num8 - 8) * ((!flag2) ? -4f : 4f);
				if (num8 != 0) {
					var angle = (!flag2) ? Mathf.Atan2(vector5.x, -vector5.z) : Mathf.Atan2(-vector5.x, vector5.z);
					var position3 = vector + new Vector3(vector5.x * num9 - vector5.z * num3, 0f, vector5.z * num9 + vector5.x * num3);
					if (flag2) {
						Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartRight, ref randomizer, position3, angle, num8, data.m_buildIndex);
					} else {
						Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartLeft, ref randomizer, position3, angle, num8, data.m_buildIndex);
					}
				}
				if (flag3) {
					vector5 = VectorUtils.NormalizeXZ(vector2 - vector4, out num7);
					num8 = Mathf.FloorToInt(num7 / 8f + 0.01f);
					num9 = num7 * 0.5f + (num8 - 8) * ((!flag2) ? -4f : 4f);
					if (num8 != 0) {
						var angle2 = (!flag2) ? Mathf.Atan2(vector5.x, -vector5.z) : Mathf.Atan2(-vector5.x, vector5.z);
						var position4 = vector4 + new Vector3(vector5.x * num9 - vector5.z * num3, 0f, vector5.z * num9 + vector5.x * num3);
						if (flag2) {
							Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndRight, ref randomizer, position4, angle2, num8, data.m_buildIndex + 1u);
						} else {
							Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndLeft, ref randomizer, position4, angle2, num8, data.m_buildIndex + 1u);
						}
					}
				}
				var vector6 = position + new Vector3(startDirection.z, 0f, -startDirection.x) * num2;
				var vector7 = position2 - new Vector3(endDirection.z, 0f, -endDirection.x) * num2;
				Vector3 b;
				Vector3 c;
				NetSegment.CalculateMiddlePoints(vector6, startDirection, vector7, endDirection, true, true, out b, out c);
				var bezier3 = new Bezier3(vector6, b, c, vector7);
				var vector8 = bezier3.Position(0.5f);
				var vector9 = bezier3.Position(0.25f);
				vector9 = Line2.Offset(VectorUtils.XZ(vector6), VectorUtils.XZ(vector8), VectorUtils.XZ(vector9));
				var vector10 = bezier3.Position(0.75f);
				vector10 = Line2.Offset(VectorUtils.XZ(vector7), VectorUtils.XZ(vector8), VectorUtils.XZ(vector10));
				var vector11 = vector6;
				var a = vector7;
				float d;
				float num10;
				if (Line2.Intersect(VectorUtils.XZ(position), VectorUtils.XZ(vector6), VectorUtils.XZ(vector11 - vector9), VectorUtils.XZ(vector8 - vector9), out d, out num10)) {
					vector6 = position + (vector6 - position) * d;
				}
				if (Line2.Intersect(VectorUtils.XZ(position2), VectorUtils.XZ(vector7), VectorUtils.XZ(a - vector10), VectorUtils.XZ(vector8 - vector10), out d, out num10)) {
					vector7 = position2 + (vector7 - position2) * d;
				}
				if (Line2.Intersect(VectorUtils.XZ(vector11 - vector9), VectorUtils.XZ(vector8 - vector9), VectorUtils.XZ(a - vector10), VectorUtils.XZ(vector8 - vector10), out d, out num10)) {
					vector8 = vector11 - vector9 + (vector8 - vector11) * d;
				}
				float num11;
				var vector12 = VectorUtils.NormalizeXZ(vector8 - vector6, out num11);
				var num12 = Mathf.FloorToInt(num11 / 8f + 0.01f);
				var num13 = num11 * 0.5f + (num12 - 8) * ((!flag2) ? 4f : -4f);
				if (num12 != 0) {
					var angle3 = (!flag2) ? Mathf.Atan2(-vector12.x, vector12.z) : Mathf.Atan2(vector12.x, -vector12.z);
					var position5 = vector6 + new Vector3(vector12.x * num13 + vector12.z * num3, 0f, vector12.z * num13 - vector12.x * num3);
					if (flag2) {
						Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartLeft, ref randomizer, position5, angle3, num12, data.m_buildIndex);
					} else {
						Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartRight, ref randomizer, position5, angle3, num12, data.m_buildIndex);
					}
				}
				vector12 = VectorUtils.NormalizeXZ(vector7 - vector8, out num11);
				num12 = Mathf.FloorToInt(num11 / 8f + 0.01f);
				num13 = num11 * 0.5f + (num12 - 8) * ((!flag2) ? 4f : -4f);

				if (num12 == 0) return;

				var angle4 = (!flag2) ? Mathf.Atan2(-vector12.x, vector12.z) : Mathf.Atan2(vector12.x, -vector12.z);
				var position6 = vector8 + new Vector3(vector12.x * num13 + vector12.z * num3, 0f, vector12.z * num13 - vector12.x * num3);
				if (flag2) {
					Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndLeft, ref randomizer, position6, angle4, num12, data.m_buildIndex + 1u);
				} else {
					Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndRight, ref randomizer, position6, angle4, num12, data.m_buildIndex + 1u);
				}
			} else {
				num2 += num3;
				var vector13 = new Vector2(position2.x - position.x, position2.z - position.z);
				var magnitude = vector13.magnitude;
				var num14 = Mathf.FloorToInt(magnitude / 8f + 0.1f);
				var num15 = (num14 <= 8) ? num14 : (num14 + 1 >> 1);
				var num16 = (num14 <= 8) ? 0 : (num14 >> 1);
				if (num15 > 0) {
					var num17 = Mathf.Atan2(startDirection.x, -startDirection.z);
					var position7 = position + new Vector3(startDirection.x * 32f - startDirection.z * num2, 0f, startDirection.z * 32f + startDirection.x * num2);
					Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartLeft, ref randomizer, position7, num17, num15, data.m_buildIndex);
					position7 = position + new Vector3(startDirection.x * (num15 - 4) * 8f + startDirection.z * num2, 0f, startDirection.z * (num15 - 4) * 8f - startDirection.x * num2);
					Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartRight, ref randomizer, position7, num17 + 3.14159274f, num15, data.m_buildIndex);
				}

				if (num16 <= 0) return;

				var num18 = magnitude - num14 * 8f;
				var num19 = Mathf.Atan2(endDirection.x, -endDirection.z);
				var position8 = position2 + new Vector3(endDirection.x * (32f + num18) - endDirection.z * num2, 0f, endDirection.z * (32f + num18) + endDirection.x * num2);
				Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndLeft, ref randomizer, position8, num19, num16, data.m_buildIndex + 1u);
				position8 = position2 + new Vector3(endDirection.x * ((num16 - 4) * 8f + num18) + endDirection.z * num2, 0f, endDirection.z * ((num16 - 4) * 8f + num18) - endDirection.x * num2);
				Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndRight, ref randomizer, position8, num19 + 3.14159274f, num16, data.m_buildIndex + 1u);
			}
		}

		private void _guiTimedTrafficLightsNode() {
			_cursorInSecondaryPanel = false;

			GUILayout.Window(252, _windowRect2, _guiTimedTrafficLightsNodeWindow, Translation.GetString("Select_nodes_windowTitle"));

			_cursorInSecondaryPanel = _windowRect2.Contains(Event.current.mousePosition);
		}

		private void showTimedLightIcons() {
			if (!Options.timedLightsOverlay && _toolMode != ToolMode.TimedLightsAddNode && _toolMode != ToolMode.TimedLightsRemoveNode && _toolMode != ToolMode.TimedLightsSelectNode && _toolMode != ToolMode.TimedLightsShowLights)
				return;

			foreach (ushort nodeId in TrafficPriority.getPriorityNodes()) {
				if (SelectedNodeIndexes.Contains(nodeId))
					continue;

				TrafficLightSimulation lightSim = TrafficLightSimulation.GetNodeSimulation(nodeId);
				if (lightSim != null && lightSim.IsTimedLight()) {
					TimedTrafficLights timedNode = lightSim.TimedLight;
					if (timedNode == null) {
						TrafficLightSimulation.RemoveNodeFromSimulation(nodeId, true);
						break;
					}

					var nodePositionVector3 = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position;
					var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
					var diff = nodePositionVector3 - camPos;
					if (diff.magnitude > PriorityCloseLod)
						continue; // do not draw if too distant

					var nodeScreenPosition = Camera.main.WorldToScreenPoint(nodePositionVector3);
					nodeScreenPosition.y = Screen.height - nodeScreenPosition.y;
					if (nodeScreenPosition.z < 0)
						continue;
					var zoom = 1.0f / diff.magnitude * 100f;
					var size = 120f * zoom;
					var guiColor = GUI.color;
					guiColor.a = 0.5f;
					GUI.color = guiColor;
					var nodeDrawingBox = new Rect(nodeScreenPosition.x - size / 2, nodeScreenPosition.y - size / 2, size, size);

					GUI.DrawTexture(nodeDrawingBox, lightSim.IsTimedLightActive() ? (timedNode.IsInTestMode() ? TrafficLightToolTextureResources.ClockTestTexture2D : TrafficLightToolTextureResources.ClockPlayTexture2D) : TrafficLightToolTextureResources.ClockPauseTexture2D);
				}
			}
		}

		private void _guiTimedTrafficLightsNodeWindow(int num) {
			if (SelectedNodeIndexes.Count < 1) {
				GUILayout.Label(Translation.GetString("Select_nodes"));
			} else {
				var txt = SelectedNodeIndexes.Aggregate("", (current, t) => current + (Translation.GetString("Node") + " " + t + "\n"));

				GUILayout.Label(txt);

				if (SelectedNodeIndexes.Count > 0 && GUILayout.Button(Translation.GetString("Deselect_all_nodes"))) {
					ClearSelectedNodes();
				}
				if (!GUILayout.Button(Translation.GetString("Setup_timed_traffic_light"))) return;

				_waitFlowBalance = 1f;
				foreach (var selectedNodeIndex in SelectedNodeIndexes) {
					TrafficLightSimulation.AddNodeToSimulation(selectedNodeIndex);
					var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(selectedNodeIndex);
					nodeSimulation.SetupTimedTrafficLight(TrafficLightTool.SelectedNodeIndexes);

					for (var s = 0; s < 8; s++) {
						var segment = Singleton<NetManager>.instance.m_nodes.m_buffer[selectedNodeIndex].GetSegment(s);
						if (segment <= 0)
							continue;

						if (!TrafficPriority.IsPrioritySegment(selectedNodeIndex, segment)) {
							TrafficPriority.AddPrioritySegment(selectedNodeIndex, segment, SegmentEnd.PriorityType.None);
						} else {
							TrafficPriority.GetPrioritySegment(selectedNodeIndex, segment).Type = SegmentEnd.PriorityType.None;
						}
					}
				}

				SetToolMode(ToolMode.TimedLightsShowLights);
			}
		}

		private bool _timedPanelAdd;
		private int _timedEditStep = -1;
		private int _timedViewedStep = -1;
		private readonly TrafficLightToolTextureResources _trafficLightToolTextureResources = new TrafficLightToolTextureResources();
		private static float speedLimitSignSize = 80f;
		private static float vehicleRestrictionsSignSize = 80f;

		private void _guiTimedControlPanel(int num) {
			var layout = new GUIStyle { normal = { textColor = new Color(1f, 1f, 1f) } };
			var layoutRed = new GUIStyle { normal = { textColor = new Color(1f, 0f, 0f) } };
			var layoutGreen = new GUIStyle { normal = { textColor = new Color(0f, 1f, 0f) } };
			var layoutYellow = new GUIStyle { normal = { textColor = new Color(1f, 1f, 0f) } };

			if (_toolMode == ToolMode.TimedLightsAddNode || _toolMode == ToolMode.TimedLightsRemoveNode) {
				GUILayout.Label(Translation.GetString("Select_junction"));
				if (GUILayout.Button(Translation.GetString("Cancel"))) {
					SetToolMode(ToolMode.TimedLightsShowLights);
				} else
					return;
			}

			var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(SelectedNodeIndexes[0]);
			var timedNodeMain = nodeSimulation.TimedLight;

			if (nodeSimulation == null || timedNodeMain == null) {
				SetToolMode(ToolMode.TimedLightsSelectNode);
				return;
			}

			for (var i = 0; i < timedNodeMain.NumSteps(); i++) {
				GUILayout.BeginHorizontal();

				if (_timedEditStep != i) {
					if (nodeSimulation.IsTimedLightActive()) {
						if (i == timedNodeMain.CurrentStep) {
							GUILayout.BeginVertical();
							GUILayout.Space(5);
							String labelStr = Translation.GetString("State") + " " + (i + 1) + ": (" + Translation.GetString("min/max") + ")" + timedNodeMain.GetStep(i).MinTimeRemaining() + "/" + timedNodeMain.GetStep(i).MaxTimeRemaining();
							float flow = Single.NaN;
							float wait = Single.NaN;
							if (timedNodeMain.IsInTestMode()) {
								try {
									timedNodeMain.GetStep(timedNodeMain.CurrentStep).calcWaitFlow(out wait, out flow);
								} catch (Exception e) {
									Log.Warning("calcWaitFlow in UI: This is not thread-safe: " + e.ToString());
								}
							} else {
								wait = timedNodeMain.GetStep(i).maxWait;
								flow = timedNodeMain.GetStep(i).minFlow;
							}
							if (!Single.IsNaN(flow) && !Single.IsNaN(wait))
								labelStr += " " + Translation.GetString("avg._flow") + ": " + String.Format("{0:0.##}", flow) + " " + Translation.GetString("avg._wait") + ": " + String.Format("{0:0.##}", wait);
							GUIStyle labelLayout = layout;
							if (timedNodeMain.IsInTestMode() && !Single.IsNaN(wait) && !Single.IsNaN(flow)) {
								if (wait > 0 && flow < wait)
									labelLayout = layoutRed;
								else
									labelLayout = layoutGreen;
							} else {
								labelLayout = timedNodeMain.GetStep(i).isInEndTransition() ? layoutYellow : layoutGreen;
							}
							GUILayout.Label(labelStr, labelLayout);
							GUILayout.Space(5);
							GUILayout.EndVertical();
							if (GUILayout.Button(Translation.GetString("Skip"), GUILayout.Width(80))) {
								foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
									sim.TimedLight.SkipStep();
								}
							}
						} else {
							GUILayout.Label(Translation.GetString("State") + " " + (i + 1) + ": " + timedNodeMain.GetStep(i).minTime + " - " + timedNodeMain.GetStep(i).maxTime, layout);
						}
					} else {
						GUIStyle labelLayout = layout;
						if (_timedViewedStep == i) {
							labelLayout = layoutGreen;
						}
						GUILayout.Label(Translation.GetString("State") + " " + (i + 1) + ": " + timedNodeMain.GetStep(i).minTime + " - " + timedNodeMain.GetStep(i).maxTime, labelLayout);

						if (_timedEditStep < 0) {
							GUILayout.BeginHorizontal(GUILayout.Width(100));

							if (i > 0) {
								if (GUILayout.Button(Translation.GetString("up"), GUILayout.Width(48))) {
									foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
										sim.TimedLight.MoveStep(i, i - 1);
										_timedViewedStep = i - 1;
									}
								}
							} else {
								GUILayout.Space(50);
							}

							if (i < timedNodeMain.NumSteps() - 1) {
								if (GUILayout.Button(Translation.GetString("down"), GUILayout.Width(48))) {
									foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
										sim.TimedLight.MoveStep(i, i + 1);
										_timedViewedStep = i + 1;
									}
								}
							} else {
								GUILayout.Space(50);
							}

							GUILayout.EndHorizontal();

							if (GUILayout.Button(Translation.GetString("View"), GUILayout.Width(70))) {
								_timedPanelAdd = false;
								_timedViewedStep = i;

								foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
									sim.TimedLight.GetStep(i).SetLights(true);
								}
							}

							if (GUILayout.Button(Translation.GetString("Edit"), GUILayout.Width(65))) {
								_timedPanelAdd = false;
								_timedEditStep = i;
								_timedViewedStep = -1;
								_stepMinValue = timedNodeMain.GetStep(i).minTime;
								_stepMaxValue = timedNodeMain.GetStep(i).maxTime;
								_waitFlowBalance = timedNodeMain.GetStep(i).waitFlowBalance;
								_stepMinValueStr = _stepMinValue.ToString();
								_stepMaxValueStr = _stepMaxValue.ToString();
								nodeSelectionLocked = true;

								foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
									sim.TimedLight.GetStep(i).SetLights(true);
								}
							}

							if (GUILayout.Button(Translation.GetString("Delete"), GUILayout.Width(70))) {
								_timedPanelAdd = false;
								_timedViewedStep = -1;

								foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
									sim.TimedLight.RemoveStep(i);
								}
							}
						}
					}
				} else {
					nodeSelectionLocked = true;
					int oldStepMinValue = _stepMinValue;
					int oldStepMaxValue = _stepMaxValue;

					// Editing step
					GUILayout.Label(Translation.GetString("Min._Time:"), GUILayout.Width(75));
					_stepMinValueStr = GUILayout.TextField(_stepMinValueStr, GUILayout.Height(20));
					if (! Int32.TryParse(_stepMinValueStr, out _stepMinValue))
						_stepMinValue = oldStepMinValue;

					GUILayout.Label(Translation.GetString("Max._Time:"), GUILayout.Width(75));
					_stepMaxValueStr = GUILayout.TextField(_stepMaxValueStr, GUILayout.Height(20));
					if (!Int32.TryParse(_stepMaxValueStr, out _stepMaxValue))
						_stepMaxValue = oldStepMaxValue;
					
					if (GUILayout.Button(Translation.GetString("Save"), GUILayout.Width(70))) {
						foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {

							if (_stepMinValue <= 0)
								_stepMinValue = 1;
							if (_stepMaxValue <= 0)
								_stepMaxValue = 1;
							if (_stepMaxValue < _stepMinValue)
								_stepMaxValue = _stepMinValue;
							if (_waitFlowBalance <= 0)
								_waitFlowBalance = 1f;

							sim.TimedLight.GetStep(_timedEditStep).minTime = (int)_stepMinValue;
							sim.TimedLight.GetStep(_timedEditStep).maxTime = (int)_stepMaxValue;
							sim.TimedLight.GetStep(_timedEditStep).waitFlowBalance = _waitFlowBalance;
							sim.TimedLight.GetStep(_timedEditStep).UpdateLights();
						}

						_timedEditStep = -1;
						_timedViewedStep = -1;
						nodeSelectionLocked = false;
					}

					GUILayout.EndHorizontal();
					makeFlowPolicySlider();
					GUILayout.BeginHorizontal();
				}

				GUILayout.EndHorizontal();
			}

			GUILayout.BeginHorizontal();

			if (_timedEditStep < 0 && !nodeSimulation.IsTimedLightActive()) {
				if (_timedPanelAdd) {
					nodeSelectionLocked = true;
					// new step
					int oldStepMinValue = _stepMinValue;
					int oldStepMaxValue = _stepMaxValue;

					GUILayout.Label(Translation.GetString("Min._Time:"), GUILayout.Width(65));
					_stepMinValueStr = GUILayout.TextField(_stepMinValueStr, GUILayout.Height(20));
					if (!Int32.TryParse(_stepMinValueStr, out _stepMinValue))
						_stepMinValue = oldStepMinValue;

					GUILayout.Label(Translation.GetString("Max._Time:"), GUILayout.Width(65));
					_stepMaxValueStr = GUILayout.TextField(_stepMaxValueStr, GUILayout.Height(20));
					if (!Int32.TryParse(_stepMaxValueStr, out _stepMaxValue))
						_stepMaxValue = oldStepMaxValue;

					if (GUILayout.Button(Translation.GetString("Add"), GUILayout.Width(70))) {
						foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
							if (_stepMinValue <= 0)
								_stepMinValue = 1;
							if (_stepMaxValue <= 0)
								_stepMaxValue = 1;
							if (_stepMaxValue < _stepMinValue)
								_stepMaxValue = _stepMinValue;
							if (_waitFlowBalance <= 0)
								_waitFlowBalance = 1f;

							sim.TimedLight.AddStep(_stepMinValue, _stepMaxValue, _waitFlowBalance);
						}
						_timedPanelAdd = false;
					}
					if (GUILayout.Button("X", GUILayout.Width(22))) {
						_timedPanelAdd = false;
					}

					GUILayout.EndHorizontal();
					makeFlowPolicySlider();
					GUILayout.BeginHorizontal();

				} else {
					if (_timedEditStep < 0) {
						if (GUILayout.Button(Translation.GetString("Add_step"))) {
							_timedPanelAdd = true;
							nodeSelectionLocked = true;
							_timedViewedStep = -1;
							_timedEditStep = -1;
						}
					}
				}
			}

			GUILayout.EndHorizontal();

			GUILayout.Space(5);

			if (timedNodeMain.NumSteps() > 1 && _timedEditStep < 0) {
				if (nodeSimulation.IsTimedLightActive()) {
					if (GUILayout.Button(_timedShowNumbers ? Translation.GetString("Hide_counters") : Translation.GetString("Show_counters"))) {
						_timedShowNumbers = !_timedShowNumbers;
					}

					if (GUILayout.Button(Translation.GetString("Stop"))) {
						foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
							sim.TimedLight.Stop();
						}
					}

					var curStep = timedNodeMain.CurrentStep;
					_waitFlowBalance = timedNodeMain.GetStep(curStep).waitFlowBalance;
					makeFlowPolicySlider();
					foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
						sim.TimedLight.GetStep(curStep).waitFlowBalance = _waitFlowBalance;
					}

					//var mayEnterIfBlocked = GUILayout.Toggle(timedNodeMain.vehiclesMayEnterBlockedJunctions, Translation.GetString("Vehicles_may_enter_blocked_junctions"), new GUILayoutOption[] { });
					var testMode = GUILayout.Toggle(timedNodeMain.IsInTestMode(), Translation.GetString("Enable_test_mode_(stay_in_current_step)"), new GUILayoutOption[] { });
					foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
						sim.TimedLight.SetTestMode(testMode);
						//sim.TimedLight.vehiclesMayEnterBlockedJunctions = mayEnterIfBlocked;
					}
				} else {
					if (_timedEditStep < 0 && !_timedPanelAdd) {
						if (GUILayout.Button(Translation.GetString("Start"))) {
							_timedPanelAdd = false;
							nodeSelectionLocked = false;

							foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
#if DEBUG
								Log._Debug("Starting traffic light @ " + sim.TimedLight.NodeId);
#endif
								sim.TimedLight.Start();
							}
						}
					}
				}
			}

			if (_timedEditStep >= 0) {
				return;
			}

			GUILayout.Space(30);

			if (GUILayout.Button(Translation.GetString("Add_junction_to_timed_light"))) {
				SetToolMode(ToolMode.TimedLightsAddNode);
			}

			if (SelectedNodeIndexes.Count > 1) {
				if (GUILayout.Button(Translation.GetString("Remove_junction_from_timed_light"))) {
					SetToolMode(ToolMode.TimedLightsRemoveNode);
				}
			}

			GUILayout.Space(30);

			if (GUILayout.Button(Translation.GetString("Remove_timed_traffic_light"))) {
				DisableTimed();
				ClearSelectedNodes();
				SetToolMode(ToolMode.None);
			}
		}

		private void makeFlowPolicySlider() {
			string formatStr;
			if (_waitFlowBalance < 0.01f)
				formatStr = "{0:0.###}";
			else if (_waitFlowBalance < 0.1f)
				formatStr = "{0:0.##}";
			else
				formatStr = "{0:0.#}";

			GUILayout.BeginHorizontal();
			GUILayout.Label(Translation.GetString("Sensitivity") + " (" + String.Format(formatStr, _waitFlowBalance) + ", " + getWaitFlowBalanceInfo() + "):");
			if (_waitFlowBalance <= 0.01f) {
				if (_waitFlowBalance >= 0) {
					if (GUILayout.Button("-.001")) {
						_waitFlowBalance -= 0.001f;
					}
				}
				if (_waitFlowBalance < 0.01f) {
					if (GUILayout.Button("+.001")) {
						_waitFlowBalance += 0.001f;
					}
				}
			} else if (_waitFlowBalance <= 0.1f) {
				if (GUILayout.Button("-.01")) {
					_waitFlowBalance -= 0.01f;
				}
				if (_waitFlowBalance < 0.1f) {
					if (GUILayout.Button("+.01")) {
						_waitFlowBalance += 0.01f;
					}
				}
			}
			if (_waitFlowBalance < 0)
				_waitFlowBalance = 0;
			if (_waitFlowBalance > 5)
				_waitFlowBalance = 5;
			GUILayout.EndHorizontal();

			_waitFlowBalance = GUILayout.HorizontalSlider(_waitFlowBalance, 0.001f, 5f);
			GUILayout.BeginHorizontal();
			GUIStyle style = new GUIStyle();
			style.normal.textColor = Color.white;
			style.alignment = TextAnchor.LowerLeft;
			GUILayout.Label(Translation.GetString("Low"), style, new GUILayoutOption[] { GUILayout.Height(10) });
			style.alignment = TextAnchor.LowerRight;
			GUILayout.Label(Translation.GetString("High"), style, new GUILayoutOption[] { GUILayout.Height(10) });
			GUILayout.EndHorizontal();

			GUILayout.Space(5);
		}

		private string getWaitFlowBalanceInfo() {
			if (_waitFlowBalance < 0.1f) {
				return Translation.GetString("Extreme_long_green/red_phases");
			} else if (_waitFlowBalance < 0.5f) {
				return Translation.GetString("Very_long_green/red_phases");
			} else if (_waitFlowBalance < 0.75f) {
				return Translation.GetString("Long_green/red_phases");
			} else if (_waitFlowBalance < 1.25f) {
				return Translation.GetString("Moderate_green/red_phases");
			} else if (_waitFlowBalance < 1.5f) {
				return Translation.GetString("Short_green/red_phases");
			} else if (_waitFlowBalance < 2.5f) {
				return Translation.GetString("Very_short_green/red_phases");
			} else {
				return Translation.GetString("Extreme_short_green/red_phases");
			}
		}

		private void _guiPrioritySigns(bool viewOnly) {
			try {
				if (viewOnly && !Options.prioritySignsOverlay)
					return;

				bool clicked = !viewOnly ? checkClicked() : false;
				var hoveredSegment = false;
				//Log.Message("_guiPrioritySigns called. num of prio segments: " + TrafficPriority.PrioritySegments.Count);

				HashSet<ushort> nodeIdsWithSigns = new HashSet<ushort>();
				for (ushort segmentId = 0; segmentId < TrafficPriority.PrioritySegments.Length; ++segmentId) {
					var trafficSegment = TrafficPriority.PrioritySegments[segmentId];
					if (trafficSegment == null)
						continue;
					SegmentGeometry geometry = CustomRoadAI.GetSegmentGeometry(segmentId);

					List<SegmentEnd> prioritySegments = new List<SegmentEnd>();
					if (TrafficLightSimulation.GetNodeSimulation(trafficSegment.Node1) == null) {
						SegmentEnd tmpSeg1 = TrafficPriority.GetPrioritySegment(trafficSegment.Node1, segmentId);
						if (tmpSeg1 != null && !geometry.IsOutgoingOneWay(trafficSegment.Node1)) {
							prioritySegments.Add(tmpSeg1);
							nodeIdsWithSigns.Add(trafficSegment.Node1);
						}
					}
					if (TrafficLightSimulation.GetNodeSimulation(trafficSegment.Node2) == null) {
						SegmentEnd tmpSeg2 = TrafficPriority.GetPrioritySegment(trafficSegment.Node2, segmentId);
						if (tmpSeg2 != null && !geometry.IsOutgoingOneWay(trafficSegment.Node2)) {
							prioritySegments.Add(tmpSeg2);
							nodeIdsWithSigns.Add(trafficSegment.Node2);
						}
					}

					//Log.Message("init ok");
					
					foreach (var prioritySegment in prioritySegments) {
						var nodeId = prioritySegment.NodeId;
						//Log.Message("_guiPrioritySigns: nodeId=" + nodeId);

						var nodePositionVector3 = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position;
						var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
						var diff = nodePositionVector3 - camPos;
						if (diff.magnitude > PriorityCloseLod)
							continue; // do not draw if too distant
						
						if (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startNode == (ushort)nodeId) {
							nodePositionVector3.x += Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startDirection.x * 10f;
							nodePositionVector3.y += Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startDirection.y * 10f;
							nodePositionVector3.z += Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startDirection.z * 10f;
						} else {
							nodePositionVector3.x += Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endDirection.x * 10f;
							nodePositionVector3.y += Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endDirection.y * 10f;
							nodePositionVector3.z += Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endDirection.z * 10f;
						}

						var nodeScreenPosition = Camera.main.WorldToScreenPoint(nodePositionVector3);
						nodeScreenPosition.y = Screen.height - nodeScreenPosition.y;
						if (nodeScreenPosition.z < 0)
							continue;
						var zoom = 1.0f / diff.magnitude * 100f;
						var size = 110f * zoom;
						var guiColor = GUI.color;
						var nodeBoundingBox = new Rect(nodeScreenPosition.x - size / 2, nodeScreenPosition.y - size / 2, size, size);
						hoveredSegment = !viewOnly && IsMouseOver(nodeBoundingBox);

						if (hoveredSegment) {
							// mouse hovering over sign
							guiColor.a = 0.8f;
						} else {
							guiColor.a = 0.5f;
							size = 90f * zoom;
						}
						var nodeDrawingBox = new Rect(nodeScreenPosition.x - size / 2, nodeScreenPosition.y - size / 2, size, size);

						GUI.color = guiColor;

						switch (prioritySegment.Type) {
							case SegmentEnd.PriorityType.Main:
								GUI.DrawTexture(nodeDrawingBox, TrafficLightToolTextureResources.SignPriorityTexture2D);
								if (clicked && hoveredSegment) {
									Log._Debug("Click on node " + nodeId + ", segment " + segmentId + " to change prio type (1)");
									//Log.Message("PrioritySegment.Type = Yield");
									prioritySegment.Type = SegmentEnd.PriorityType.Yield;
									clicked = false;
								}
								break;
							case SegmentEnd.PriorityType.Yield:
								GUI.DrawTexture(nodeDrawingBox, TrafficLightToolTextureResources.SignYieldTexture2D);
								if (clicked && hoveredSegment) {
									Log._Debug("Click on node " + nodeId + ", segment " + segmentId + " to change prio type (2)");
									prioritySegment.Type = SegmentEnd.PriorityType.Stop;
									clicked = false;
								}

								break;
							case SegmentEnd.PriorityType.Stop:
								GUI.DrawTexture(nodeDrawingBox, TrafficLightToolTextureResources.SignStopTexture2D);
								if (clicked && hoveredSegment) {
									Log._Debug("Click on node " + nodeId + ", segment " + segmentId + " to change prio type (3)");
									prioritySegment.Type = SegmentEnd.PriorityType.None;
									clicked = false;
								}
								break;
							case SegmentEnd.PriorityType.None:
								if (viewOnly)
									break;
								GUI.DrawTexture(nodeDrawingBox, TrafficLightToolTextureResources.SignNoneTexture2D);

								if (clicked && hoveredSegment) {
									Log._Debug("Click on node " + nodeId + ", segment " + segmentId + " to change prio type (4)");
									//Log.Message("PrioritySegment.Type = None");
									prioritySegment.Type = GetNumberOfMainRoads(nodeId, ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId]) >= 2
										? SegmentEnd.PriorityType.Yield
										: SegmentEnd.PriorityType.Main;
									clicked = false;
								}
								break;
						}
					}
				}

				if (viewOnly)
					return;

				ushort hoveredExistingNodeId = 0;
				foreach (ushort nodeId in nodeIdsWithSigns) {
					var nodePositionVector3 = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position;
					var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
					var diff = nodePositionVector3 - camPos;
					if (diff.magnitude > PriorityCloseLod)
						continue;

					// draw deletion button
					var nodeScreenPosition = Camera.main.WorldToScreenPoint(nodePositionVector3);
					nodeScreenPosition.y = Screen.height - nodeScreenPosition.y;
					if (nodeScreenPosition.z < 0)
						continue;
					var zoom = 1.0f / diff.magnitude * 100f;
					var size = 100f * zoom;
					var nodeBoundingBox = new Rect(nodeScreenPosition.x - size / 2, nodeScreenPosition.y - size / 2, size, size);

					var guiColor = GUI.color;
					var nodeCenterHovered = IsMouseOver(nodeBoundingBox);
					if (nodeCenterHovered) {
						hoveredExistingNodeId = nodeId;
						guiColor.a = 0.8f;
					} else {
						guiColor.a = 0.5f;
					}
					GUI.color = guiColor;

					GUI.DrawTexture(nodeBoundingBox, TrafficLightToolTextureResources.SignRemoveTexture2D);
				}

				// add a new or delete a priority segment node
				if (_hoveredNetNodeIdx != 0 || hoveredExistingNodeId != 0) {
					bool delete = false;
					if (hoveredExistingNodeId > 0) {
						delete = true;
					}

					// determine if we may add new priority signs to this node
					bool ok = false;
					if ((Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) {
						// no traffic light set
						ok = true;
					} else {
						TrafficLightSimulation nodeSim = TrafficLightSimulation.GetNodeSimulation(_hoveredNetNodeIdx);
						if (nodeSim == null || !nodeSim.IsTimedLight()) {
							ok = true;
						}
					}

					if (clicked) {
						Log._Debug("_guiPrioritySigns: hovered+clicked @ nodeId=" + _hoveredNetNodeIdx + "/" + hoveredExistingNodeId);

						if (delete) {
							TrafficPriority.RemovePrioritySegments(hoveredExistingNodeId);
						} else if (ok) {
							if (!TrafficPriority.IsPriorityNode(_hoveredNetNodeIdx)) {
								Log._Debug("_guiPrioritySigns: adding prio segments @ nodeId=" + _hoveredNetNodeIdx);
								TrafficLightSimulation.RemoveNodeFromSimulation(_hoveredNetNodeIdx, false); // TODO refactor!
								Flags.setNodeTrafficLight(_hoveredNetNodeIdx, false); // TODO refactor!
								TrafficPriority.AddPriorityNode(_hoveredNetNodeIdx);
							}
						} else {
							showTooltip(Translation.GetString("NODE_IS_TIMED_LIGHT"), Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_position);
						}
					}
				}
			} catch (Exception e) {
				Log.Error(e.ToString());
			}
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

		private bool IsMouseOver(Rect boundingBox) {
			return boundingBox.Contains(Event.current.mousePosition);
		}

		private bool checkClicked() {
			if (Input.GetMouseButtonDown(0) && !mouseClickProcessed) {
				mouseClickProcessed = true;
				return true;
			}
			return false;
		}

		private void showTooltip(String text, Vector3 position) {
			if (text == null)
				return;

			UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Info", text, false);

			/*tooltipStartFrame = currentFrame;
			tooltipText = text;
			tooltipWorldPos = position;*/
		}

		private void _switchTrafficLights() {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None) {
				TrafficLightSimulation sim = TrafficLightSimulation.GetNodeSimulation(_hoveredNetNodeIdx);
				if (sim != null && sim.IsTimedLight()) {
					showTooltip(Translation.GetString("NODE_IS_TIMED_LIGHT"), Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_position);
				} else {
					TrafficLightSimulation.RemoveNodeFromSimulation(_hoveredNetNodeIdx, true); // TODO refactor!
					Flags.setNodeTrafficLight(_hoveredNetNodeIdx, false); // TODO refactor!
				}
			} else {
				TrafficPriority.RemovePrioritySegments(_hoveredNetNodeIdx);
				Flags.setNodeTrafficLight(_hoveredNetNodeIdx, true);
			}
		}

		public void AddTimedNodes() {
			foreach (var selectedNodeIndex in SelectedNodeIndexes) {
				TrafficLightSimulation.AddNodeToSimulation(selectedNodeIndex);
				var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(selectedNodeIndex);
				nodeSimulation.SetupTimedTrafficLight(TrafficLightTool.SelectedNodeIndexes);

				for (var s = 0; s < 8; s++) {
					var segment = Singleton<NetManager>.instance.m_nodes.m_buffer[selectedNodeIndex].GetSegment(s);

					if (segment != 0 && !TrafficPriority.IsPrioritySegment(selectedNodeIndex, segment)) {
						TrafficPriority.AddPrioritySegment(selectedNodeIndex, segment, SegmentEnd.PriorityType.None);
					}
				}
			}
		}

		public bool SwitchManual() {
			if (SelectedNode == 0) return false;

			var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(SelectedNode);

			if (nodeSimulation == null) {
				//node.Info.m_netAI = _myGameObject.GetComponent<CustomRoadAI>();
				//node.Info.m_netAI.m_info = node.Info;
				nodeSimulation = TrafficLightSimulation.AddNodeToSimulation(SelectedNode);
				nodeSimulation.SetupManualTrafficLight();

				for (var s = 0; s < 8; s++) {
					var segment = Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode].GetSegment(s);

					if (segment != 0 && !TrafficPriority.IsPrioritySegment(SelectedNode, segment)) {
						TrafficPriority.AddPrioritySegment(SelectedNode, segment, SegmentEnd.PriorityType.None);
					}
				}

				return true;
			}
			nodeSimulation.DestroyManualTrafficLight();
			TrafficLightSimulation.RemoveNodeFromSimulation(SelectedNode, true);

			for (var s = 0; s < 8; s++) {
				var segment = Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode].GetSegment(s);

				if (segment != 0 && !TrafficPriority.IsPrioritySegment(SelectedNode, segment)) {
					TrafficPriority.AddPrioritySegment(SelectedNode, segment, SegmentEnd.PriorityType.None);
				}
			}

			return false;
		}

		private static void DisableManual() {
			if (SelectedNode == 0) return;
			var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(SelectedNode);

			if (nodeSimulation == null || !nodeSimulation.IsManualLight()) return;

			nodeSimulation.DestroyManualTrafficLight();
			TrafficLightSimulation.RemoveNodeFromSimulation(SelectedNode, true);
		}

		private void DisableTimed() {
			if (SelectedNodeIndexes.Count <= 0) return;

			foreach (var selectedNodeIndex in SelectedNodeIndexes) {
				var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(selectedNodeIndex);
				if (nodeSimulation == null) continue;
				nodeSimulation.Destroy(true);
			}
		}

		private static void AddSelectedNode(ushort node) {
			SelectedNodeIndexes.Add(node);
		}

		private static bool IsNodeSelected(ushort node) {
			return SelectedNodeIndexes.Contains(node);
		}

		private static void RemoveSelectedNode(ushort node) {
			SelectedNodeIndexes.Remove(node);
		}

		private static void ClearSelectedNodes() {
			SelectedNodeIndexes.Clear();
		}

		private static void AddSelectedSegment(ushort segment) {
			_selectedSegmentIds.Add(segment);
		}

		private static bool IsSegmentSelected(ushort segment) {
			return _selectedSegmentIds.Contains(segment);
		}

		private static void RemoveSelectedSegment(ushort segment) {
			_selectedSegmentIds.Remove(segment);
		}
	}
}
