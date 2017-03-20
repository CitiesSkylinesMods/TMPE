#define DEBUGCONNx

using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using TrafficManager.Util;
using UnityEngine;
using TrafficManager.Manager;

namespace TrafficManager.UI.SubTools {
	public class LaneConnectorTool : SubTool {
		enum MarkerSelectionMode {
			None,
			SelectSource,
			SelectTarget
		}

		enum StayInLaneMode {
			None,
			Both,
			Forward,
			Backward
		}

		private static readonly Color DefaultNodeMarkerColor = new Color(1f, 1f, 1f, 0.4f);
		private Dictionary<ushort, IDisposable> nodeGeometryUnsubscribers;
		private NodeLaneMarker selectedMarker = null;
		private NodeLaneMarker hoveredMarker = null;
		private Dictionary<ushort, List<NodeLaneMarker>> currentNodeMarkers;
		private StayInLaneMode stayInLaneMode = StayInLaneMode.None;
		//private bool initDone = false;

		class NodeLaneMarker {
			internal ushort segmentId;
			internal ushort nodeId;
			internal bool startNode;
			internal Vector3 position;
			internal Vector3 secondaryPosition;
			internal bool isSource;
			internal uint laneId;
			internal int innerSimilarLaneIndex;
			internal NetInfo.Direction finalDirection;
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

			NetManager netManager = Singleton<NetManager>.instance;

			var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
			//Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
			Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);

			foreach (KeyValuePair<ushort, List<NodeLaneMarker>> e in currentNodeMarkers) {
				ushort nodeId = e.Key;
				List<NodeLaneMarker> nodeMarkers = e.Value;
				Vector3 nodePos = NetManager.instance.m_nodes.m_buffer[nodeId].m_position;

				var diff = nodePos - camPos;
				if (diff.magnitude > TrafficManagerTool.PriorityCloseLod)
					continue; // do not draw if too distant

				if (!viewOnly && GetMarkerSelectionMode() == MarkerSelectionMode.None) {
					MainTool.DrawNodeCircle(cameraInfo, nodeId, DefaultNodeMarkerColor, true);
				}

				foreach (NodeLaneMarker laneMarker in nodeMarkers) {
					foreach (NodeLaneMarker targetLaneMarker in laneMarker.connectedMarkers) {
						// render lane connection from laneMarker to targetLaneMarker
						RenderLane(cameraInfo, laneMarker.position, targetLaneMarker.position, nodePos, laneMarker.color);
					}

					if (!viewOnly && nodeId == SelectedNodeId) {
						//bounds.center = laneMarker.position;
						bool markerIsHovered = IsLaneMarkerHovered(laneMarker, ref mouseRay);// bounds.IntersectRay(mouseRay);

						// draw source marker in source selection mode,
						// draw target marker (if segment turning angles are within bounds) and selected source marker in target selection mode
						bool drawMarker = (GetMarkerSelectionMode() == MarkerSelectionMode.SelectSource && laneMarker.isSource) ||
							(GetMarkerSelectionMode() == MarkerSelectionMode.SelectTarget && (
							(!laneMarker.isSource &&
							(laneMarker.vehicleType & selectedMarker.vehicleType) != VehicleInfo.VehicleType.None &&
							CheckSegmentsTurningAngle(selectedMarker.segmentId, ref netManager.m_segments.m_buffer[selectedMarker.segmentId], selectedMarker.startNode, laneMarker.segmentId, ref netManager.m_segments.m_buffer[laneMarker.segmentId], laneMarker.startNode)
							) || laneMarker == selectedMarker));
						// highlight hovered marker and selected marker
						bool highlightMarker = drawMarker && (laneMarker == selectedMarker || markerIsHovered);

						if (drawMarker) {
							if (highlightMarker) {
								laneMarker.radius = 2f;
							} else
								laneMarker.radius = 1f;
						} else {
							markerIsHovered = false;
						}

						if (markerIsHovered) {
							/*if (hoveredMarker != sourceLaneMarker)
								Log._Debug($"Marker @ lane {sourceLaneMarker.laneId} hovered");*/
							hoveredMarker = laneMarker;
						}

						if (drawMarker) {
							//DrawLaneMarker(laneMarker, cameraInfo);
							RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, laneMarker.color, laneMarker.position, laneMarker.radius, laneMarker.position.y - 100f, laneMarker.position.y + 100f, false, true);
						}
					}
				}
			}
		}

		private bool IsLaneMarkerHovered(NodeLaneMarker laneMarker, ref Ray mouseRay) {
			Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
			bounds.center = laneMarker.position;
			if (bounds.IntersectRay(mouseRay))
				return true;

			bounds = new Bounds(Vector3.zero, Vector3.one);
			bounds.center = laneMarker.secondaryPosition;
			return bounds.IntersectRay(mouseRay);
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

				bool deleteAll = Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace);
				bool stayInLane = Input.GetKeyDown(KeyCode.S) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId].CountSegments() == 2;
				if (stayInLane)
					deleteAll = true;

				if (deleteAll) {
					// remove all connections at selected node

					List<NodeLaneMarker> nodeMarkers = GetNodeMarkers(SelectedNodeId);
					if (nodeMarkers != null) {
						selectedMarker = null;
						foreach (NodeLaneMarker sourceLaneMarker in nodeMarkers) {
							foreach (NodeLaneMarker targetLaneMarker in sourceLaneMarker.connectedMarkers) {
								LaneConnectionManager.Instance.RemoveLaneConnection(sourceLaneMarker.laneId, targetLaneMarker.laneId, sourceLaneMarker.startNode);
							}
						}
					}
					RefreshCurrentNodeMarkers();
				}

				if (stayInLane) {
					// "stay in lane"
					switch (stayInLaneMode) {
						case StayInLaneMode.None:
							stayInLaneMode = StayInLaneMode.Both;
							break;
						case StayInLaneMode.Both:
							stayInLaneMode = StayInLaneMode.Forward;
							break;
						case StayInLaneMode.Forward:
							stayInLaneMode = StayInLaneMode.Backward;
							break;
						case StayInLaneMode.Backward:
							stayInLaneMode = StayInLaneMode.None;
							break;
					}

					if (stayInLaneMode != StayInLaneMode.None) {
						List<NodeLaneMarker> nodeMarkers = GetNodeMarkers(SelectedNodeId);
						if (nodeMarkers != null) {
							selectedMarker = null;
							foreach (NodeLaneMarker sourceLaneMarker in nodeMarkers) {
								if (!sourceLaneMarker.isSource)
									continue;

								if (stayInLaneMode == StayInLaneMode.Forward || stayInLaneMode == StayInLaneMode.Backward) {
									if (sourceLaneMarker.finalDirection == NetInfo.Direction.Backward ^ stayInLaneMode == StayInLaneMode.Backward) {
										continue;
									}
								}

								foreach (NodeLaneMarker targetLaneMarker in nodeMarkers) {
									if (targetLaneMarker.isSource || targetLaneMarker.segmentId == sourceLaneMarker.segmentId)
										continue;

									if (targetLaneMarker.innerSimilarLaneIndex == sourceLaneMarker.innerSimilarLaneIndex) {
										Log._Debug($"Adding lane connection {sourceLaneMarker.laneId} -> {targetLaneMarker.laneId}");
										LaneConnectionManager.Instance.AddLaneConnection(sourceLaneMarker.laneId, targetLaneMarker.laneId, sourceLaneMarker.startNode);
									}
								}
							}
						}
						RefreshCurrentNodeMarkers();
					}
				}
			}

			if (GetMarkerSelectionMode() == MarkerSelectionMode.None && HoveredNodeId != 0) {
				// draw hovered node
				MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId, Input.GetMouseButton(0));
			}
		}

		public override void OnPrimaryClickOverlay() {
#if DEBUGCONN
			Log._Debug($"TppLaneConnectorTool: OnPrimaryClickOverlay. SelectedNodeId={SelectedNodeId} SelectedSegmentId={SelectedSegmentId} HoveredNodeId={HoveredNodeId} HoveredSegmentId={HoveredSegmentId}");
#endif

			if (IsCursorInPanel())
				return;

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
						stayInLaneMode = StayInLaneMode.None;
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
							stayInLaneMode = StayInLaneMode.None;

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
					stayInLaneMode = StayInLaneMode.None;
					return;
				}
			}

			if (hoveredMarker != null) {
				stayInLaneMode = StayInLaneMode.None;

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
					if (LaneConnectionManager.Instance.RemoveLaneConnection(selectedMarker.laneId, hoveredMarker.laneId, selectedMarker.startNode)) { // try to remove connection
						selectedMarker.connectedMarkers.Remove(hoveredMarker);
#if DEBUGCONN
						Log._Debug($"TppLaneConnectorTool: removed lane connection: {selectedMarker.laneId}, {hoveredMarker.laneId}");
#endif
						//success = true;
					} else if (LaneConnectionManager.Instance.AddLaneConnection(selectedMarker.laneId, hoveredMarker.laneId, selectedMarker.startNode)) { // try to add connection
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
			if (IsCursorInPanel())
				return;

			switch (GetMarkerSelectionMode()) {
				case MarkerSelectionMode.None:
				default:
#if DEBUGCONN
					Log._Debug($"TppLaneConnectorTool: OnSecondaryClickOverlay: nothing to do");
#endif
					stayInLaneMode = StayInLaneMode.None;
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
			stayInLaneMode = StayInLaneMode.None;
			RefreshCurrentNodeMarkers();
		}

		private void RefreshCurrentNodeMarkers() {
			currentNodeMarkers.Clear();

			for (uint nodeId = 1; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
					continue;

				List<NodeLaneMarker> nodeMarkers = GetNodeMarkers((ushort)nodeId);
				if (nodeMarkers == null)
					continue;
				currentNodeMarkers[(ushort)nodeId] = nodeMarkers;
			}
		}

		private MarkerSelectionMode GetMarkerSelectionMode() {
			if (SelectedNodeId == 0)
				return MarkerSelectionMode.None;
			if (selectedMarker == null)
				return MarkerSelectionMode.SelectSource;
			return MarkerSelectionMode.SelectTarget;
		}

		public override void Cleanup() {
			RefreshCurrentNodeMarkers();
		}

		public override void Initialize() {
			Cleanup();
		}

		private List<NodeLaneMarker> GetNodeMarkers(ushort nodeId) {
			if (nodeId == 0)
				return null;
			if ((NetManager.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
				return null;

			List<NodeLaneMarker> nodeMarkers = new List<NodeLaneMarker>();
			LaneConnectionManager connManager = LaneConnectionManager.Instance;

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
					NetInfo.Lane laneInfo = lanes[laneIndex];
					if ((laneInfo.m_laneType & (NetInfo.LaneType.TransportVehicle | NetInfo.LaneType.Vehicle)) != NetInfo.LaneType.None &&
						(laneInfo.m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro)) != VehicleInfo.VehicleType.None) { // TODO refactor vehicle mask

						Vector3? pos = null;
						bool isSource = false;
						if (connManager.GetLaneEndPoint(segmentId, !isEndNode, laneIndex, laneId, laneInfo, out isSource, out pos)) {

							pos = (Vector3)pos + offset;
							float terrainY = Singleton<TerrainManager>.instance.SampleDetailHeightSmooth(((Vector3)pos));
							Vector3 finalPos = new Vector3(((Vector3)pos).x, terrainY, ((Vector3)pos).z);

							nodeMarkers.Add(new NodeLaneMarker() {
								segmentId = segmentId,
								laneId = laneId,
								nodeId = nodeId,
								startNode = !isEndNode,
								position = finalPos,
								secondaryPosition = (Vector3)pos,
								color = colors[nodeMarkers.Count],
								isSource = isSource,
								laneType = laneInfo.m_laneType,
								vehicleType = laneInfo.m_vehicleType,
								innerSimilarLaneIndex = ((byte)(laneInfo.m_finalDirection & NetInfo.Direction.Forward) != 0) ? laneInfo.m_similarLaneIndex : laneInfo.m_similarLaneCount - laneInfo.m_similarLaneIndex - 1,
								finalDirection = laneInfo.m_finalDirection
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

				uint[] connections = LaneConnectionManager.Instance.GetLaneConnections(laneMarker1.laneId, laneMarker1.startNode);
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

		/// <summary>
		/// Checks if the turning angle between two segments at the given node is within bounds.
		/// </summary>
		/// <param name="sourceSegmentId"></param>
		/// <param name="sourceSegment"></param>
		/// <param name="sourceStartNode"></param>
		/// <param name="targetSegmentId"></param>
		/// <param name="targetSegment"></param>
		/// <param name="targetStartNode"></param>
		/// <returns></returns>
		private bool CheckSegmentsTurningAngle(ushort sourceSegmentId, ref NetSegment sourceSegment, bool sourceStartNode, ushort targetSegmentId, ref NetSegment targetSegment, bool targetStartNode) {
			NetManager netManager = Singleton<NetManager>.instance;

			NetInfo sourceSegmentInfo = netManager.m_segments.m_buffer[sourceSegmentId].Info;
			NetInfo targetSegmentInfo = netManager.m_segments.m_buffer[targetSegmentId].Info;

			float turningAngle = 0.01f - Mathf.Min(sourceSegmentInfo.m_maxTurnAngleCos, targetSegmentInfo.m_maxTurnAngleCos);
			if (turningAngle < 1f) {
				Vector3 sourceDirection;
				if (sourceStartNode) {
					sourceDirection = sourceSegment.m_startDirection;
				} else {
					sourceDirection = sourceSegment.m_endDirection;
				}

				Vector3 targetDirection;
				if (targetStartNode) {
					targetDirection = targetSegment.m_startDirection;
				} else {
					targetDirection = targetSegment.m_endDirection;
				}
				float dirDotProd = sourceDirection.x * targetDirection.x + sourceDirection.z * targetDirection.z;
				return dirDotProd < turningAngle;
			}
			return true;
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
