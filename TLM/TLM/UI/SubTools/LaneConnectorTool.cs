#define DEBUGCONNx

using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.UI.SubTools {
	public class LaneConnectorTool : SubTool {
		enum MarkerSelectionMode {
			None,
			SelectSource,
			SelectTarget
		}

		private Dictionary<ushort, IDisposable> nodeGeometryUnsubscribers;
		private NodeLaneMarker selectedMarker = null;
		private NodeLaneMarker hoveredMarker = null;
		private Dictionary<ushort, List<NodeLaneMarker>> currentNodeMarkers;
		//private bool initDone = false;

		class NodeLaneMarker {
			internal ushort nodeId;
			internal bool startNode;
			internal Vector3 position;
			internal bool isSource;
			internal uint laneId;
			internal float radius = 1f;
			internal Color color;
			internal List<NodeLaneMarker> connectedMarkers = new List<NodeLaneMarker>();
			internal NetInfo.LaneType laneType;
			internal VehicleInfo.VehicleType vehicleType;
		}

		public LaneConnectorTool(TrafficManagerTool mainTool) : base(mainTool) {
			//Log._Debug($"TppLaneConnectorTool: Constructor called");
			nodeGeometryUnsubscribers = new Dictionary<ushort, IDisposable>();
			currentNodeMarkers = new Dictionary<ushort, List<NodeLaneMarker>>();
		}

		public override void OnToolGUI(Event e) {
			//Log._Debug($"TppLaneConnectorTool: OnToolGUI. SelectedNodeId={SelectedNodeId} SelectedSegmentId={SelectedSegmentId} HoveredNodeId={HoveredNodeId} HoveredSegmentId={HoveredSegmentId} IsInsideUI={MainTool.GetToolController().IsInsideUI}");
		}

		public override void RenderInfoOverlay(RenderManager.CameraInfo cameraInfo) {
			ShowOverlay(true, cameraInfo);
		}

		private void ShowOverlay(bool viewOnly, RenderManager.CameraInfo cameraInfo) {
			if (viewOnly && !Options.connectedLanesOverlay)
				return;

			var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
			Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
			Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);

			//for (ushort nodeId = 1; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
			foreach (KeyValuePair<ushort, List<NodeLaneMarker>> e in currentNodeMarkers) {
				ushort nodeId = e.Key;
				List<NodeLaneMarker> nodeMarkers = e.Value;
				Vector3 nodePos = NetManager.instance.m_nodes.m_buffer[nodeId].m_position;

				var diff = nodePos - camPos;
				if (diff.magnitude > TrafficManagerTool.PriorityCloseLod)
					continue; // do not draw if too distant

				foreach (NodeLaneMarker sourceLaneMarker in nodeMarkers) {
					foreach (NodeLaneMarker targetLaneMarker in sourceLaneMarker.connectedMarkers) {
						// render lane connection
						RenderLane(cameraInfo, sourceLaneMarker.position, targetLaneMarker.position, nodePos, sourceLaneMarker.color);
					}

					if (!viewOnly && nodeId == SelectedNodeId) {
						bounds.center = sourceLaneMarker.position;
						bool markerIsHovered = bounds.IntersectRay(mouseRay);

						// draw source marker in source selection mode, draw target marker and selected source marker in target selection mode
						bool drawMarker = (GetMarkerSelectionMode() == MarkerSelectionMode.SelectSource && sourceLaneMarker.isSource) ||
							(GetMarkerSelectionMode() == MarkerSelectionMode.SelectTarget && (
							(!sourceLaneMarker.isSource &&
							//(sourceLaneMarker.laneType & selectedMarker.laneType) != NetInfo.LaneType.None &&
							(sourceLaneMarker.vehicleType & selectedMarker.vehicleType) != VehicleInfo.VehicleType.None) || sourceLaneMarker == selectedMarker));
						// highlight hovered marker and selected marker
						bool highlightMarker = drawMarker && (sourceLaneMarker == selectedMarker || markerIsHovered);

						if (drawMarker) {
							if (highlightMarker) {
								sourceLaneMarker.radius = 2f;
							} else
								sourceLaneMarker.radius = 1f;
						} else {
							markerIsHovered = false;
						}

						if (markerIsHovered) {
							/*if (hoveredMarker != sourceLaneMarker)
								Log._Debug($"Marker @ lane {sourceLaneMarker.laneId} hovered");*/
							hoveredMarker = sourceLaneMarker;
						}

						if (drawMarker)
							RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, sourceLaneMarker.color, sourceLaneMarker.position, sourceLaneMarker.radius, -1f, 1280f, false, true);
					}
				}
			}
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			//Log._Debug($"TppLaneConnectorTool: RenderOverlay. SelectedNodeId={SelectedNodeId} SelectedSegmentId={SelectedSegmentId} HoveredNodeId={HoveredNodeId} HoveredSegmentId={HoveredSegmentId} IsInsideUI={MainTool.GetToolController().IsInsideUI}");
			// draw lane markers and connections

			hoveredMarker = null;

			ShowOverlay(false, cameraInfo);

			// draw bezier from source marker to mouse position in target marker selection 
			if (SelectedNodeId != 0) {
				if (GetMarkerSelectionMode() == MarkerSelectionMode.SelectTarget) {
					Vector3 selNodePos = NetManager.instance.m_nodes.m_buffer[SelectedNodeId].m_position;

					ToolBase.RaycastOutput output;
					if (RayCastSegmentAndNode(out output)) {
						RenderLane(cameraInfo, selectedMarker.position, output.m_hitPos, selNodePos, selectedMarker.color);
					}
				}

				if (Input.GetKey(KeyCode.Delete)) {
					// remove all connections at selected node

					List<NodeLaneMarker> nodeMarkers = GetNodeMarkers(SelectedNodeId);
					if (nodeMarkers != null) {
						selectedMarker = null;
						foreach (NodeLaneMarker sourceLaneMarker in nodeMarkers) {
							foreach (NodeLaneMarker targetLaneMarker in sourceLaneMarker.connectedMarkers) {
								LaneConnectionManager.Instance().RemoveLaneConnection(sourceLaneMarker.laneId, targetLaneMarker.laneId, sourceLaneMarker.startNode);
							}
						}
					}
					RefreshCurrentNodeMarkers();
				}
			}

			if (GetMarkerSelectionMode() == MarkerSelectionMode.None && HoveredNodeId != 0) {
				// draw hovered node
				MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId);
			}
		}

		public override void OnPrimaryClickOverlay() {
#if DEBUGCONN
			Log._Debug($"TppLaneConnectorTool: OnPrimaryClickOverlay. SelectedNodeId={SelectedNodeId} SelectedSegmentId={SelectedSegmentId} HoveredNodeId={HoveredNodeId} HoveredSegmentId={HoveredSegmentId}");
#endif

			if (GetMarkerSelectionMode() == MarkerSelectionMode.None) {
				if (HoveredNodeId != 0) {
#if DEBUGCONN
					Log._Debug($"TppLaneConnectorTool: HoveredNode != 0");
#endif

					if (NetManager.instance.m_nodes.m_buffer[HoveredNodeId].CountSegments() < 2) {
						// this node cannot be configured (dead end)
#if DEBUGCONN
						Log._Debug($"TppLaneConnectorTool: Node is a dead end");
#endif
						SelectedNodeId = 0;
						selectedMarker = null;
						return;
					}

					if (SelectedNodeId != HoveredNodeId) {
#if DEBUGCONN
						Log._Debug($"Node {HoveredNodeId} has been selected. Creating markers.");
#endif

						// selected node has changed. create markers
						List<NodeLaneMarker> markers = GetNodeMarkers(HoveredNodeId);
						if (markers != null) {
							SelectedNodeId = HoveredNodeId;
							selectedMarker = null;

							currentNodeMarkers[SelectedNodeId] = markers;
						}
						//this.allNodeMarkers[SelectedNodeId] = GetNodeMarkers(SelectedNodeId);
					}
				} else {
#if DEBUGCONN
					Log._Debug($"TppLaneConnectorTool: Node {SelectedNodeId} has been deselected.");
#endif

					// click on free spot. deselect node
					SelectedNodeId = 0;
					selectedMarker = null;
					return;
				}
			}

			if (hoveredMarker != null) {
#if DEBUGCONN
				Log._Debug($"TppLaneConnectorTool: hoveredMarker != null. selMode={GetMarkerSelectionMode()}");
#endif

				// hovered marker has been clicked
				if (GetMarkerSelectionMode() == MarkerSelectionMode.SelectSource) {
					// select source marker
					selectedMarker = hoveredMarker;
#if DEBUGCONN
					Log._Debug($"TppLaneConnectorTool: set selected marker");
#endif
				} else if (GetMarkerSelectionMode() == MarkerSelectionMode.SelectTarget) {
					// select target marker
					//bool success = false;
					if (LaneConnectionManager.Instance().RemoveLaneConnection(selectedMarker.laneId, hoveredMarker.laneId, selectedMarker.startNode)) { // try to remove connection
						selectedMarker.connectedMarkers.Remove(hoveredMarker);
#if DEBUGCONN
						Log._Debug($"TppLaneConnectorTool: removed lane connection: {selectedMarker.laneId}, {hoveredMarker.laneId}");
#endif
						//success = true;
					} else if (LaneConnectionManager.Instance().AddLaneConnection(selectedMarker.laneId, hoveredMarker.laneId, selectedMarker.startNode)) { // try to add connection
						selectedMarker.connectedMarkers.Add(hoveredMarker);
#if DEBUGCONN
						Log._Debug($"TppLaneConnectorTool: added lane connection: {selectedMarker.laneId}, {hoveredMarker.laneId}");
#endif
						//success = true;
					}

					/*if (success) {
						// connection has been modified. switch back to source marker selection
						Log._Debug($"TppLaneConnectorTool: switch back to source marker selection");
						selectedMarker = null;
						selMode = MarkerSelectionMode.SelectSource;
					}*/
				}
			}
		}

		public override void OnSecondaryClickOverlay() {
			switch (GetMarkerSelectionMode()) {
				case MarkerSelectionMode.None:
				default:
#if DEBUGCONN
					Log._Debug($"TppLaneConnectorTool: OnSecondaryClickOverlay: nothing to do");
#endif
					break;
				case MarkerSelectionMode.SelectSource:
					// deselect node
#if DEBUGCONN
					Log._Debug($"TppLaneConnectorTool: OnSecondaryClickOverlay: selected node id = 0");
#endif
					SelectedNodeId = 0;
					break;
				case MarkerSelectionMode.SelectTarget:
					// deselect source marker
#if DEBUGCONN
					Log._Debug($"TppLaneConnectorTool: OnSecondaryClickOverlay: switch to selected source mode");
#endif
					selectedMarker = null;
					break;
			}
		}

		public override void OnActivate() {
#if DEBUGCONN
			Log._Debug("TppLaneConnectorTool: OnActivate");
#endif
			SelectedNodeId = 0;
			selectedMarker = null;
			hoveredMarker = null;
			RefreshCurrentNodeMarkers();
		}

		private void RefreshCurrentNodeMarkers() {
			currentNodeMarkers.Clear();

			for (ushort nodeId = 1; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
					continue;

				List<NodeLaneMarker> nodeMarkers = GetNodeMarkers(nodeId);
				if (nodeMarkers == null)
					continue;
				currentNodeMarkers[nodeId] = nodeMarkers;
			}
		}

		private MarkerSelectionMode GetMarkerSelectionMode() {
			if (SelectedNodeId == 0)
				return MarkerSelectionMode.None;
			if (selectedMarker == null)
				return MarkerSelectionMode.SelectSource;
			return MarkerSelectionMode.SelectTarget;
		}

		public override void Initialize() {
#if DEBUGCONN
			Log.Warning("LaneConnectorTool.Initialize called");
#endif
			if (!Flags.IsInitDone()) {
#if DEBUGCONN
				Log.Warning("LaneConnectorTool.Initialize: Flags have not been initialized!");
#endif
				return;
			}

			RefreshCurrentNodeMarkers();

			/*NetManager netManager = Singleton<NetManager>.instance;

			allNodeMarkers.Clear();

			HashSet<ushort> processedNodes = new HashSet<ushort>();
			for (uint laneId = 1; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
				if (!Flags.CheckLane(laneId))
					continue;

				ushort segmentId = netManager.m_lanes.m_buffer[laneId].m_segment;
				ushort startNodeId = netManager.m_segments.m_buffer[segmentId].m_startNode;
				ushort endNodeId = netManager.m_segments.m_buffer[segmentId].m_endNode;

				if (startNodeId != 0 && !processedNodes.Contains(startNodeId)) {
					this.allNodeMarkers[startNodeId] = GetNodeMarkers(startNodeId);
					processedNodes.Add(startNodeId);
				}

				if (endNodeId != 0 && !processedNodes.Contains(endNodeId)) {
					this.allNodeMarkers[endNodeId] = GetNodeMarkers(endNodeId);
					processedNodes.Add(endNodeId);
				}
			}*/
		}

		private List<NodeLaneMarker> GetNodeMarkers(ushort nodeId) {
			if (nodeId == 0)
				return null;
			if ((NetManager.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
				return null;

			List<NodeLaneMarker> nodeMarkers = new List<NodeLaneMarker>();
			LaneConnectionManager connManager = LaneConnectionManager.Instance();

			int offsetMultiplier = NetManager.instance.m_nodes.m_buffer[nodeId].CountSegments() <= 2 ? 3 : 1;
			for (int i = 0; i < 8; i++) {
				ushort segmentId = NetManager.instance.m_nodes.m_buffer[nodeId].GetSegment(i);
				if (segmentId == 0)
					continue;

				bool isEndNode = NetManager.instance.m_segments.m_buffer[segmentId].m_endNode == nodeId;
				Vector3 offset = NetManager.instance.m_segments.m_buffer[segmentId].FindDirection(segmentId, nodeId) * offsetMultiplier;
				NetInfo.Lane[] lanes = NetManager.instance.m_segments.m_buffer[segmentId].Info.m_lanes;
				uint laneId = NetManager.instance.m_segments.m_buffer[segmentId].m_lanes;
				for (byte laneIndex = 0; laneIndex < lanes.Length && laneId != 0; laneIndex++) {
					if ((lanes[laneIndex].m_laneType & (NetInfo.LaneType.TransportVehicle | NetInfo.LaneType.Vehicle)) != NetInfo.LaneType.None &&
						(lanes[laneIndex].m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {

						Vector3? pos = null;
						bool isSource = false;
						if (connManager.GetLaneEndPoint(segmentId, !isEndNode, laneIndex, laneId, lanes[laneIndex], out isSource, out pos)) {
							nodeMarkers.Add(new NodeLaneMarker() {
								laneId = laneId,
								nodeId = nodeId,
								startNode = !isEndNode,
								position = (Vector3)pos + offset,
								color = colors[nodeMarkers.Count],
								isSource = isSource,
								laneType = lanes[laneIndex].m_laneType,
								vehicleType = lanes[laneIndex].m_vehicleType
							});
						}
					}

					laneId = NetManager.instance.m_lanes.m_buffer[laneId].m_nextLane;
				}
			}

			if (nodeMarkers.Count == 0)
				return null;

			foreach (NodeLaneMarker laneMarker1 in nodeMarkers) {
				if (!laneMarker1.isSource)
					continue;

				uint[] connections = LaneConnectionManager.Instance().GetLaneConnections(laneMarker1.laneId, laneMarker1.startNode);
				if (connections == null || connections.Length == 0)
					continue;

				foreach (NodeLaneMarker laneMarker2 in nodeMarkers) {
					if (laneMarker2.isSource)
						continue;

					if (connections.Contains(laneMarker2.laneId))
						laneMarker1.connectedMarkers.Add(laneMarker2);
				}
			}

			return nodeMarkers;
		}

		private void RenderLane(RenderManager.CameraInfo cameraInfo, Vector3 start, Vector3 end, Vector3 middlePoint, Color color, float size = 0.1f) {
			Bezier3 bezier;
			bezier.a = start;
			bezier.d = end;
			NetSegment.CalculateMiddlePoints(bezier.a, (middlePoint - bezier.a).normalized, bezier.d, (middlePoint - bezier.d).normalized, false, false, out bezier.b, out bezier.c);

			RenderManager.instance.OverlayEffect.DrawBezier(cameraInfo, color, bezier, size, 0, 0, -1f, 1280f, false, true);
		}

		private bool RayCastSegmentAndNode(out ToolBase.RaycastOutput output) {
			ToolBase.RaycastInput input = new ToolBase.RaycastInput(Camera.main.ScreenPointToRay(Input.mousePosition), Camera.main.farClipPlane);
			input.m_netService.m_service = ItemClass.Service.Road;
			input.m_netService.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
			input.m_ignoreSegmentFlags = NetSegment.Flags.None;
			input.m_ignoreNodeFlags = NetNode.Flags.None;
			input.m_ignoreTerrain = true;

			return MainTool.DoRayCast(input, out output);
		}

		private static readonly Color32[] colors = new Color32[]
		{
			new Color32(161, 64, 206, 255),
			new Color32(79, 251, 8, 255),
			new Color32(243, 96, 44, 255),
			new Color32(45, 106, 105, 255),
			new Color32(253, 165, 187, 255),
			new Color32(90, 131, 14, 255),
			new Color32(58, 20, 70, 255),
			new Color32(248, 246, 183, 255),
			new Color32(255, 205, 29, 255),
			new Color32(91, 50, 18, 255),
			new Color32(76, 239, 155, 255),
			new Color32(241, 25, 130, 255),
			new Color32(125, 197, 240, 255),
			new Color32(57, 102, 187, 255),
			new Color32(160, 27, 61, 255),
			new Color32(167, 251, 107, 255),
			new Color32(165, 94, 3, 255),
			new Color32(204, 18, 161, 255),
			new Color32(208, 136, 237, 255),
			new Color32(232, 211, 202, 255),
			new Color32(45, 182, 15, 255),
			new Color32(8, 40, 47, 255),
			new Color32(249, 172, 142, 255),
			new Color32(248, 99, 101, 255),
			new Color32(180, 250, 208, 255),
			new Color32(126, 25, 77, 255),
			new Color32(243, 170, 55, 255),
			new Color32(47, 69, 126, 255),
			new Color32(50, 105, 70, 255),
			new Color32(156, 49, 1, 255),
			new Color32(233, 231, 255, 255),
			new Color32(107, 146, 253, 255),
			new Color32(127, 35, 26, 255),
			new Color32(240, 94, 222, 255),
			new Color32(58, 28, 24, 255),
			new Color32(165, 179, 240, 255),
			new Color32(239, 93, 145, 255),
			new Color32(47, 110, 138, 255),
			new Color32(57, 195, 101, 255),
			new Color32(124, 88, 213, 255),
			new Color32(252, 220, 144, 255),
			new Color32(48, 106, 224, 255),
			new Color32(90, 109, 28, 255),
			new Color32(56, 179, 208, 255),
			new Color32(239, 73, 177, 255),
			new Color32(84, 60, 2, 255),
			new Color32(169, 104, 238, 255),
			new Color32(97, 201, 238, 255),
		};
	}
}
