namespace TrafficManager.UI.SubTools {
    using System.Collections.Generic;
    using System.Linq;
    using ColossalFramework;
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using State.ConfigData;
    using State.Keybinds;
    using UnityEngine;
    using Util.Caching;

    public class LaneConnectorTool : SubTool {
        private enum MarkerSelectionMode {
            None,
            SelectSource,
            SelectTarget
        }

        private enum StayInLaneMode {
            None,
            Both,
            Forward,
            Backward
        }

        private static readonly Color DefaultNodeMarkerColor = new Color(1f, 1f, 1f, 0.4f);
        private NodeLaneMarker selectedMarker;
        private NodeLaneMarker hoveredMarker;
        private readonly Dictionary<ushort, List<NodeLaneMarker>> currentNodeMarkers;
        private StayInLaneMode stayInLaneMode = StayInLaneMode.None;
        // private bool initDone = false;

        /// <summary>Unity frame when OnGui detected the shortcut for Stay in Lane.
        /// Resets when the event is consumed or after a few frames.</summary>
        private int frameStayInLanePressed;

        /// <summary>Clear lane lines is Delete/Backspace (configurable)</summary>
        private int frameClearPressed;

        /// <summary>
        /// Stores potentially visible ids for nodes while the camera did not move
        /// </summary>
        private GenericArrayCache<uint> CachedVisibleNodeIds { get; }

        /// <summary>
        /// Stores last cached camera position in <see cref="CachedVisibleNodeIds"/>
        /// </summary>
        private CameraTransformValue LastCachedCamera { get; set; }

        private class NodeLaneMarker {
            internal ushort SegmentId;
            internal ushort NodeId;
            internal bool StartNode;
            internal Vector3 Position;
            internal Vector3 SecondaryPosition;
            internal bool IsSource;
            internal bool IsTarget;
            internal uint LaneId;
            internal int InnerSimilarLaneIndex;
            internal int SegmentIndex;
            internal float Radius = 1f;
            internal Color Color;
            internal readonly List<NodeLaneMarker> ConnectedMarkers = new List<NodeLaneMarker>();
            [UsedImplicitly]
            internal NetInfo.LaneType LaneType;
            internal VehicleInfo.VehicleType VehicleType;
        }

        public LaneConnectorTool(TrafficManagerTool mainTool)
            : base(mainTool)
        {
            // Log._Debug($"LaneConnectorTool: Constructor called");
            currentNodeMarkers = new Dictionary<ushort, List<NodeLaneMarker>>();

            CachedVisibleNodeIds = new GenericArrayCache<uint>(NetManager.MAX_NODE_COUNT);
            LastCachedCamera = new CameraTransformValue();
        }

        public override void OnToolGUI(Event e) {
            // Log._Debug(
            //    $"LaneConnectorTool: OnToolGUI. SelectedNodeId={SelectedNodeId} " +
            //    $"SelectedSegmentId={SelectedSegmentId} HoveredNodeId={HoveredNodeId} " +
            //    $"HoveredSegmentId={HoveredSegmentId} IsInsideUI={MainTool.GetToolController().IsInsideUI}");
            if (KeybindSettingsBase.LaneConnectorStayInLane.IsPressed(e)) {
                frameStayInLanePressed = Time.frameCount;

                // this will be consumed in RenderOverlay() if the key was pressed
                // not too long ago (within 20 Unity frames or 0.33 sec)
            }

            if (KeybindSettingsBase.LaneConnectorDelete.IsPressed(e)) {
                frameClearPressed = Time.frameCount;

                // this will be consumed in RenderOverlay() if the key was pressed
                // not too long ago (within 20 Unity frames or 0.33 sec)
            }
        }

        public override void RenderInfoOverlay(RenderManager.CameraInfo cameraInfo) {
            ShowOverlay(true, cameraInfo);
        }

        private void ShowOverlay(bool viewOnly, RenderManager.CameraInfo cameraInfo) {
            if (viewOnly && !Options.connectedLanesOverlay) {
                return;
            }

            NetManager netManager = Singleton<NetManager>.instance;

            Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;

            // Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
            Camera currentCamera = Camera.main;
            Ray mouseRay = currentCamera.ScreenPointToRay(Input.mousePosition);

            // Check if camera pos/angle has changed then re-filter the visible nodes
            // Assumption: The states checked in this loop don't change while the tool is active
            var currentCameraState = new CameraTransformValue(currentCamera);
            if (!LastCachedCamera.Equals(currentCameraState)) {
                CachedVisibleNodeIds.Clear();
                LastCachedCamera = currentCameraState;

                for (uint nodeId = 1; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
                    if (!Constants.ServiceFactory.NetService.IsNodeValid((ushort)nodeId)) {
                        continue;
                    }

                    //---------------------------
                    // Check the connection class
                    //---------------------------
                    // TODO refactor connection class check
                    ItemClass connectionClass =
                        NetManager.instance.m_nodes.m_buffer[nodeId].Info.GetConnectionClass();

                    if ((connectionClass == null) ||
                        !((connectionClass.m_service == ItemClass.Service.Road) ||
                          ((connectionClass.m_service == ItemClass.Service.PublicTransport) &&
                           ((connectionClass.m_subService == ItemClass.SubService.PublicTransportTrain) ||
                            (connectionClass.m_subService == ItemClass.SubService.PublicTransportMetro) ||
                            (connectionClass.m_subService == ItemClass.SubService.PublicTransportMonorail)))))
                    {
                        continue;
                    }

                    //--------------------------
                    // Check the camera distance
                    //--------------------------
                    Vector3 diff = NetManager.instance.m_nodes.m_buffer[nodeId].m_position - camPos;

                    if (diff.magnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE) {
                        continue; // do not draw if too distant
                    }

                    // Add
                    CachedVisibleNodeIds.Add(nodeId);
                }
            }

            for (int cacheIndex = CachedVisibleNodeIds.Size - 1; cacheIndex >= 0; cacheIndex--) {
                var nodeId = CachedVisibleNodeIds.Values[cacheIndex];

                List<NodeLaneMarker> nodeMarkers;
                bool hasMarkers = currentNodeMarkers.TryGetValue((ushort)nodeId, out nodeMarkers);

                if (!viewOnly && (GetMarkerSelectionMode() == MarkerSelectionMode.None)) {
                    MainTool.DrawNodeCircle(
                        cameraInfo,
                        (ushort)nodeId,
                        DefaultNodeMarkerColor,
                        true);
                }

                if (!hasMarkers) {
                    continue;
                }

                foreach (NodeLaneMarker laneMarker in nodeMarkers) {
                    if (!Constants.ServiceFactory.NetService.IsLaneValid(laneMarker.LaneId)) {
                        continue;
                    }

                    foreach (NodeLaneMarker targetLaneMarker in laneMarker.ConnectedMarkers) {
                        // render lane connection from laneMarker to targetLaneMarker
                        if (!Constants.ServiceFactory.NetService.IsLaneValid( targetLaneMarker.LaneId)) {
                            continue;
                        }

                        DrawLaneCurve(
                            cameraInfo,
                            laneMarker.Position,
                            targetLaneMarker.Position,
                            NetManager.instance.m_nodes.m_buffer[nodeId].m_position,
                            laneMarker.Color,
                            Color.black);
                    }

                    if (viewOnly || (nodeId != SelectedNodeId)) {
                        continue;
                    }

                    // bounds.center = laneMarker.position;
                    // bounds.IntersectRay(mouseRay);
                    bool markerIsHovered = IsLaneMarkerHovered(laneMarker, ref mouseRay);

                    // draw source marker in source selection mode,
                    // draw target marker (if segment turning angles are within bounds) and
                    // selected source marker in target selection mode
                    bool drawMarker
                        = ((GetMarkerSelectionMode() == MarkerSelectionMode.SelectSource)
                           && laneMarker.IsSource)
                          || ((GetMarkerSelectionMode() == MarkerSelectionMode.SelectTarget)
                              && ((laneMarker.IsTarget
                                   && ((laneMarker.VehicleType & selectedMarker.VehicleType)
                                       != VehicleInfo.VehicleType.None)
                                   && CheckSegmentsTurningAngle(
                                       selectedMarker.SegmentId,
                                       ref netManager.m_segments.m_buffer[selectedMarker.SegmentId],
                                       selectedMarker.StartNode,
                                       laneMarker.SegmentId,
                                       ref netManager.m_segments.m_buffer[laneMarker.SegmentId],
                                       laneMarker.StartNode))
                                  || (laneMarker == selectedMarker)));

                    // highlight hovered marker and selected marker
                    bool highlightMarker =
                        drawMarker && ((laneMarker == selectedMarker) || markerIsHovered);

                    if (drawMarker) {
                        laneMarker.Radius = highlightMarker ? 2f : 1f;
                    } else {
                        markerIsHovered = false;
                    }

                    if (markerIsHovered) {
                        hoveredMarker = laneMarker;
                    }

                    var circleColor = laneMarker.IsTarget ? Color.white : laneMarker.Color;

                    if (drawMarker) {
                        RenderManager.instance.OverlayEffect.DrawCircle(
                            cameraInfo,
                            circleColor,
                            laneMarker.Position,
                            laneMarker.Radius,
                            laneMarker.Position.y - 100f, // through all the geometry -100..100
                            laneMarker.Position.y + 100f,
                            false,
                            true);
                        RenderManager.instance.OverlayEffect.DrawCircle(
                            cameraInfo,
                            Color.black,
                            laneMarker.Position,
                            laneMarker.Radius * 0.75f, // inner black
                            laneMarker.Position.y - 100f, // through all the geometry -100..100
                            laneMarker.Position.y + 100f,
                            false,
                            false);
                    }
                } // end foreach lanemarker in node markers
            } // end for node in all nodes
        }

        private bool IsLaneMarkerHovered(NodeLaneMarker laneMarker, ref Ray mouseRay) {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one) {
                center = laneMarker.Position
            };

            if (bounds.IntersectRay(mouseRay)) {
                return true;
            }

            bounds = new Bounds(Vector3.zero, Vector3.one) {
                center = laneMarker.SecondaryPosition
            };
            return bounds.IntersectRay(mouseRay);
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            // Log._Debug($"LaneConnectorTool: RenderOverlay. SelectedNodeId={SelectedNodeId}
            //     SelectedSegmentId={SelectedSegmentId} HoveredNodeId={HoveredNodeId}
            //     HoveredSegmentId={HoveredSegmentId} IsInsideUI={MainTool.GetToolController().IsInsideUI}");

            // draw lane markers and connections
            hoveredMarker = null;

            ShowOverlay(false, cameraInfo);

            // draw bezier from source marker to mouse position in target marker selection
            if (SelectedNodeId != 0) {
                if (GetMarkerSelectionMode() == MarkerSelectionMode.SelectTarget) {
                    Vector3 selNodePos =
                        NetManager.instance.m_nodes.m_buffer[SelectedNodeId].m_position;

                    ToolBase.RaycastOutput output;
                    // Draw a currently dragged curve
                    if (RayCastSegmentAndNode(out output)) {
                        DrawLaneCurve(
                            cameraInfo,
                            selectedMarker.Position,
                            output.m_hitPos,
                            selNodePos,
                            Color.Lerp(selectedMarker.Color, Color.white, 0.33f),
                            Color.white);
                    }
                }

                bool deleteAll =
                    (frameClearPressed > 0) && ((Time.frameCount - frameClearPressed) < 20); // 0.33 sec

                NetNode[] nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;

                // Must press Shift+S (or another shortcut) within last 20 frames for this to work
                bool stayInLane = (frameStayInLanePressed > 0)
                                 && ((Time.frameCount - frameStayInLanePressed) < 20) // 0.33 sec
                                 && (nodesBuffer[SelectedNodeId].CountSegments() == 2);

                if (stayInLane) {
                    frameStayInLanePressed = 0; // not pressed anymore (consumed)
                    deleteAll = true;
                }

                if (deleteAll) {
                    frameClearPressed = 0; // consumed
                    // remove all connections at selected node
                    LaneConnectionManager.Instance.RemoveLaneConnectionsFromNode(SelectedNodeId);
                    RefreshCurrentNodeMarkers(SelectedNodeId);
                }

                if (stayInLane) {
                    // "stay in lane"
                    switch (stayInLaneMode) {
                        case StayInLaneMode.None: {
                            stayInLaneMode = StayInLaneMode.Both;
                            break;
                        }

                        case StayInLaneMode.Both: {
                            stayInLaneMode = StayInLaneMode.Forward;
                            break;
                        }

                        case StayInLaneMode.Forward: {
                            stayInLaneMode = StayInLaneMode.Backward;
                            break;
                        }

                        case StayInLaneMode.Backward: {
                            stayInLaneMode = StayInLaneMode.None;
                            break;
                        }
                    }

                    if (stayInLaneMode != StayInLaneMode.None) {
                        List<NodeLaneMarker> nodeMarkers = GetNodeMarkers(
                            SelectedNodeId,
                            ref nodesBuffer[SelectedNodeId]);

                        if (nodeMarkers != null) {
                            selectedMarker = null;

                            foreach (NodeLaneMarker sourceLaneMarker in nodeMarkers) {
                                if (!sourceLaneMarker.IsSource) {
                                    continue;
                                }

                                if ((stayInLaneMode == StayInLaneMode.Forward) ||
                                    (stayInLaneMode == StayInLaneMode.Backward)) {
                                    if ((sourceLaneMarker.SegmentIndex == 0)
                                        ^ (stayInLaneMode == StayInLaneMode.Backward)) {
                                        continue;
                                    }
                                }

                                foreach (NodeLaneMarker targetLaneMarker in nodeMarkers) {
                                    if (!targetLaneMarker.IsTarget || (targetLaneMarker.SegmentId ==
                                                                       sourceLaneMarker.SegmentId)) {
                                        continue;
                                    }

                                    if (targetLaneMarker.InnerSimilarLaneIndex
                                        == sourceLaneMarker.InnerSimilarLaneIndex) {
                                        Log._Debug(
                                            $"Adding lane connection {sourceLaneMarker.LaneId} -> " +
                                            $"{targetLaneMarker.LaneId}");
                                        LaneConnectionManager.Instance.AddLaneConnection(
                                            sourceLaneMarker.LaneId,
                                            targetLaneMarker.LaneId,
                                            sourceLaneMarker.StartNode);
                                    }
                                }
                            }
                        }

                        RefreshCurrentNodeMarkers(SelectedNodeId);
                    } // if stay in lane is not None
                } // if stay in lane
            } // if selected node

            if ((GetMarkerSelectionMode() == MarkerSelectionMode.None) && (HoveredNodeId != 0)) {
                // draw hovered node
                MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId, Input.GetMouseButton(0));
            }
        }

        public override void OnPrimaryClickOverlay() {
#if DEBUG
            bool logLaneConn = DebugSwitch.LaneConnections.Get();
#else
            const bool logLaneConn = false;
#endif
            Log._DebugIf(
                logLaneConn,
                () => $"LaneConnectorTool: OnPrimaryClickOverlay. SelectedNodeId={SelectedNodeId} " +
                $"SelectedSegmentId={SelectedSegmentId} HoveredNodeId={HoveredNodeId} " +
                $"HoveredSegmentId={HoveredSegmentId}");

            if (IsCursorInPanel()) {
                return;
            }

            if (GetMarkerSelectionMode() == MarkerSelectionMode.None) {
                if (HoveredNodeId != 0) {
                    Log._DebugIf(
                        logLaneConn,
                        () => "LaneConnectorTool: HoveredNode != 0");

                    if (NetManager.instance.m_nodes.m_buffer[HoveredNodeId].CountSegments() < 2) {
                        // this node cannot be configured (dead end)
                        Log._DebugIf(
                            logLaneConn,
                            () => "LaneConnectorTool: Node is a dead end");

                        SelectedNodeId = 0;
                        selectedMarker = null;
                        stayInLaneMode = StayInLaneMode.None;
                        return;
                    }

                    if (SelectedNodeId != HoveredNodeId) {
                        Log._DebugIf(
                            logLaneConn,
                            () => $"Node {HoveredNodeId} has been selected. Creating markers.");

                        // selected node has changed. create markers
                        List<NodeLaneMarker> markers = GetNodeMarkers(
                            HoveredNodeId,
                            ref Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId]);

                        if (markers != null) {
                            SelectedNodeId = HoveredNodeId;
                            selectedMarker = null;
                            stayInLaneMode = StayInLaneMode.None;

                            currentNodeMarkers[SelectedNodeId] = markers;
                        }

                        // this.allNodeMarkers[SelectedNodeId] = GetNodeMarkers(SelectedNodeId);
                    }
                } else {
                    Log._DebugIf(
                        logLaneConn,
                        () => $"LaneConnectorTool: Node {SelectedNodeId} has been deselected.");

                    // click on free spot. deselect node
                    SelectedNodeId = 0;
                    selectedMarker = null;
                    stayInLaneMode = StayInLaneMode.None;
                    return;
                }
            }

            if (hoveredMarker == null) {
                return;
            }

            //-----------------------------------
            // Hovered Marker
            //-----------------------------------
            stayInLaneMode = StayInLaneMode.None;

            Log._DebugIf(
                logLaneConn,
                () => $"LaneConnectorTool: hoveredMarker != null. selMode={GetMarkerSelectionMode()}");

            // hovered marker has been clicked
            if (GetMarkerSelectionMode() == MarkerSelectionMode.SelectSource) {
                // select source marker
                selectedMarker = hoveredMarker;
                Log._DebugIf(
                    logLaneConn,
                    () => "LaneConnectorTool: set selected marker");
            } else if (GetMarkerSelectionMode() == MarkerSelectionMode.SelectTarget) {
                // select target marker
                // bool success = false;
                if (LaneConnectionManager.Instance.RemoveLaneConnection(
                    selectedMarker.LaneId,
                    hoveredMarker.LaneId,
                    selectedMarker.StartNode)) {

                    // try to remove connection
                    selectedMarker.ConnectedMarkers.Remove(hoveredMarker);
                    Log._DebugIf(
                        logLaneConn,
                        () => $"LaneConnectorTool: removed lane connection: {selectedMarker.LaneId}, " +
                        $"{hoveredMarker.LaneId}");

                    // success = true;
                } else if (LaneConnectionManager.Instance.AddLaneConnection(
                    selectedMarker.LaneId,
                    hoveredMarker.LaneId,
                    selectedMarker.StartNode))
                {
                    // try to add connection
                    selectedMarker.ConnectedMarkers.Add(hoveredMarker);
                    Log._DebugIf(
                        logLaneConn,
                        () => $"LaneConnectorTool: added lane connection: {selectedMarker.LaneId}, " +
                        $"{hoveredMarker.LaneId}");

                    // success = true;
                }

                /*if (success) {
                            // connection has been modified. switch back to source marker selection
                            Log._Debug($"LaneConnectorTool: switch back to source marker selection");
                            selectedMarker = null;
                            selMode = MarkerSelectionMode.SelectSource;
                    }*/
            }
        }

        public override void OnSecondaryClickOverlay() {
#if DEBUG
            bool logLaneConn = DebugSwitch.LaneConnections.Get();
#else
            const bool logLaneConn = false;
#endif

            if (IsCursorInPanel()) {
                return;
            }

            switch (GetMarkerSelectionMode()) {
                // also: case MarkerSelectionMode.None:
                default: {
                    Log._DebugIf(
                        logLaneConn,
                        () => "LaneConnectorTool: OnSecondaryClickOverlay: nothing to do");
                    stayInLaneMode = StayInLaneMode.None;
                    break;
                }

                case MarkerSelectionMode.SelectSource: {
                    // deselect node
                    Log._DebugIf(
                        logLaneConn,
                        () => "LaneConnectorTool: OnSecondaryClickOverlay: selected node id = 0");
                    SelectedNodeId = 0;
                    break;
                }

                case MarkerSelectionMode.SelectTarget: {
                    // deselect source marker
                    Log._DebugIf(
                        logLaneConn,
                        () => "LaneConnectorTool: OnSecondaryClickOverlay: switch to selected source mode");
                    selectedMarker = null;
                    break;
                }
            }
        }

        public override void OnActivate() {
#if DEBUG
            bool logLaneConn = DebugSwitch.LaneConnections.Get();
            if (logLaneConn) {
                Log._Debug("LaneConnectorTool: OnActivate");
            }
#endif
            SelectedNodeId = 0;
            selectedMarker = null;
            hoveredMarker = null;
            stayInLaneMode = StayInLaneMode.None;
            RefreshCurrentNodeMarkers();
        }

        private void RefreshCurrentNodeMarkers(ushort forceNodeId = 0) {
            if (forceNodeId == 0) {
                currentNodeMarkers.Clear();
            } else {
                currentNodeMarkers.Remove(forceNodeId);
            }

            for (uint nodeId = forceNodeId == 0 ? 1u : forceNodeId;
                 nodeId <= (forceNodeId == 0 ? NetManager.MAX_NODE_COUNT - 1 : forceNodeId);
                 ++nodeId)
            {
                if (!Constants.ServiceFactory.NetService.IsNodeValid((ushort)nodeId)) {
                    continue;
                }

                if (!LaneConnectionManager.Instance.HasNodeConnections((ushort)nodeId)) {
                    continue;
                }

                List<NodeLaneMarker> nodeMarkers = GetNodeMarkers(
                    (ushort)nodeId,
                    ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId]);

                if (nodeMarkers == null) {
                    continue;
                }

                currentNodeMarkers[(ushort)nodeId] = nodeMarkers;
            }
        }

        private MarkerSelectionMode GetMarkerSelectionMode() {
            if (SelectedNodeId == 0) {
                return MarkerSelectionMode.None;
            }

            return selectedMarker == null
                       ? MarkerSelectionMode.SelectSource
                       : MarkerSelectionMode.SelectTarget;
        }

        public override void Cleanup() { }

        public override void Initialize() {
            base.Initialize();
            Cleanup();
            if (Options.connectedLanesOverlay) {
                RefreshCurrentNodeMarkers();
            } else {
                currentNodeMarkers.Clear();
            }
        }

        private List<NodeLaneMarker> GetNodeMarkers(ushort nodeId, ref NetNode node) {
            if (nodeId == 0) {
                return null;
            }

            if ((node.m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
                return null;
            }

            List<NodeLaneMarker> nodeMarkers = new List<NodeLaneMarker>();
            int nodeMarkerColorIndex = 0;
            LaneConnectionManager connManager = LaneConnectionManager.Instance;

            int offsetMultiplier = node.CountSegments() <= 2 ? 3 : 1;

            for (int i = 0; i < 8; i++) {
                ushort segmentId = node.GetSegment(i);

                if (segmentId == 0) {
                    continue;
                }

                NetSegment[] segmentsBuffer = NetManager.instance.m_segments.m_buffer;
                bool isEndNode = segmentsBuffer[segmentId].m_endNode == nodeId;
                Vector3 offset = segmentsBuffer[segmentId]
                                     .FindDirection(segmentId, nodeId) * offsetMultiplier;
                NetInfo.Lane[] lanes = segmentsBuffer[segmentId].Info.m_lanes;
                uint laneId = segmentsBuffer[segmentId].m_lanes;

                for (byte laneIndex = 0; (laneIndex < lanes.Length) && (laneId != 0); laneIndex++)
                {
                    NetInfo.Lane laneInfo = lanes[laneIndex];

                    if (((laneInfo.m_laneType & LaneConnectionManager.LANE_TYPES) != NetInfo.LaneType.None)
                        && ((laneInfo.m_vehicleType & LaneConnectionManager.VEHICLE_TYPES)
                            != VehicleInfo.VehicleType.None))
                    {
                        if (connManager.GetLaneEndPoint(
                            segmentId,
                            !isEndNode,
                            laneIndex,
                            laneId,
                            laneInfo,
                            out bool isSource,
                            out bool isTarget,
                            out Vector3? pos))
                        {
                            pos = pos.Value + offset;

                            float terrainY =
                                Singleton<TerrainManager>.instance.SampleDetailHeightSmooth(pos.Value);

                            var finalPos = new Vector3(pos.Value.x, terrainY, pos.Value.z);

                            Color32 nodeMarkerColor
                                = isSource
                                      ? COLOR_CHOICES[nodeMarkerColorIndex % COLOR_CHOICES.Length]
                                      : default; // or black (not used while rendering)

                            nodeMarkers.Add(
                                new NodeLaneMarker {
                                    SegmentId = segmentId,
                                    LaneId = laneId,
                                    NodeId = nodeId,
                                    StartNode = !isEndNode,
                                    Position = finalPos,
                                    SecondaryPosition = (Vector3)pos,
                                    Color = nodeMarkerColor,
                                    IsSource = isSource,
                                    IsTarget = isTarget,
                                    LaneType = laneInfo.m_laneType,
                                    VehicleType = laneInfo.m_vehicleType,
                                    InnerSimilarLaneIndex =
                                        ((byte)(laneInfo.m_direction & NetInfo.Direction.Forward) != 0)
                                            ? laneInfo.m_similarLaneIndex
                                            : laneInfo.m_similarLaneCount -
                                              laneInfo.m_similarLaneIndex - 1,
                                    SegmentIndex = i
                                });

                            if (isSource) {
                                nodeMarkerColorIndex++;
                            }
                        }
                    }

                    laneId = NetManager.instance.m_lanes.m_buffer[laneId].m_nextLane;
                }
            }

            if (nodeMarkers.Count == 0) {
                return null;
            }

            foreach (NodeLaneMarker laneMarker1 in nodeMarkers) {
                if (!laneMarker1.IsSource) {
                    continue;
                }

                uint[] connections =
                    LaneConnectionManager.Instance.GetLaneConnections(
                        laneMarker1.LaneId,
                        laneMarker1.StartNode);

                if ((connections == null) || (connections.Length == 0)) {
                    continue;
                }

                foreach (NodeLaneMarker laneMarker2 in nodeMarkers) {
                    if (!laneMarker2.IsTarget) {
                        continue;
                    }

                    if (connections.Contains(laneMarker2.LaneId)) {
                        laneMarker1.ConnectedMarkers.Add(laneMarker2);
                    }
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
        private bool CheckSegmentsTurningAngle(ushort sourceSegmentId,
                                               ref NetSegment sourceSegment,
                                               bool sourceStartNode,
                                               ushort targetSegmentId,
                                               ref NetSegment targetSegment,
                                               bool targetStartNode)
        {
            NetManager netManager = Singleton<NetManager>.instance;
            NetInfo sourceSegmentInfo = netManager.m_segments.m_buffer[sourceSegmentId].Info;
            NetInfo targetSegmentInfo = netManager.m_segments.m_buffer[targetSegmentId].Info;

            float turningAngle = 0.01f - Mathf.Min(
                                     sourceSegmentInfo.m_maxTurnAngleCos,
                                     targetSegmentInfo.m_maxTurnAngleCos);

            if (turningAngle < 1f) {
                Vector3 sourceDirection = sourceStartNode
                                              ? sourceSegment.m_startDirection
                                              : sourceSegment.m_endDirection;
                Vector3 targetDirection;

                targetDirection = targetStartNode
                                      ? targetSegment.m_startDirection
                                      : targetSegment.m_endDirection;

                float dirDotProd = (sourceDirection.x * targetDirection.x) +
                                   (sourceDirection.z * targetDirection.z);
                return dirDotProd < turningAngle;
            }

            return true;
        }

        /// <summary>
        /// Draw a bezier curve from `start` to `end` and bent towards `middlePoint` with `color`
        /// </summary>
        /// <param name="cameraInfo">The camera to use</param>
        /// <param name="start">Where the bezier to begin</param>
        /// <param name="end">Where the bezier to end</param>
        /// <param name="middlePoint">Where the bezier is bent towards</param>
        /// <param name="color">The inner curve color</param>
        /// <param name="outlineColor">The outline color</param>
        /// <param name="size">The thickness</param>
        private void DrawLaneCurve(RenderManager.CameraInfo cameraInfo,
                                   Vector3 start,
                                   Vector3 end,
                                   Vector3 middlePoint,
                                   Color color,
                                   Color outlineColor,
                                   float size = 0.1f) {
            Bezier3 bezier;
            bezier.a = start;
            bezier.d = end;

            NetSegment.CalculateMiddlePoints(
                bezier.a,
                (middlePoint - bezier.a).normalized,
                bezier.d,
                (middlePoint - bezier.d).normalized,
                false,
                false,
                out bezier.b,
                out bezier.c);

            // Draw black outline
            RenderManager.instance.OverlayEffect.DrawBezier(
                cameraInfo,
                outlineColor,
                bezier,
                size * 2f,
                0,
                0,
                -1f,
                1280f,
                false,
                false);
            // Inside the outline draw colored bezier
            RenderManager.instance.OverlayEffect.DrawBezier(
                cameraInfo,
                color,
                bezier,
                size,
                0,
                0,
                -1f,
                1280f,
                false,
                true);
        }

        private bool RayCastSegmentAndNode(out ToolBase.RaycastOutput output) {
            ToolBase.RaycastInput input = new ToolBase.RaycastInput(
                Camera.main.ScreenPointToRay(Input.mousePosition),
                Camera.main.farClipPlane);

            input.m_netService.m_service = ItemClass.Service.Road;
            input.m_netService.m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
            input.m_ignoreSegmentFlags = NetSegment.Flags.None;
            input.m_ignoreNodeFlags = NetNode.Flags.None;
            input.m_ignoreTerrain = true;

            return MainTool.DoRayCast(input, out output);
        }

        /// <summary>
        /// Generated with http://phrogz.net/css/distinct-colors.html
        /// HSV Value start 84%, end 37% (cutting away too bright and too dark).
        /// The colors are slightly reordered to create some variety
        /// </summary>
        private static readonly Color32[] COLOR_CHOICES
            = {
                  new Color32(240, 30, 30, 255),
                  new Color32(80, 214, 0, 255),
                  new Color32(30, 30, 214, 255),
                  new Color32(214, 136, 107, 255),
                  new Color32(120, 189, 94, 255),
                  new Color32(106, 41, 163, 255),
                  new Color32(54, 118, 214, 255),
                  new Color32(163, 57, 41, 255),
                  new Color32(54, 161, 214, 255),
                  new Color32(107, 214, 193, 255),
                  new Color32(214, 161, 175, 255),
                  new Color32(214, 0, 171, 255),
                  new Color32(151, 178, 201, 255),
                  new Color32(189, 101, 0, 255),
                  new Color32(154, 142, 189, 255),
                  new Color32(189, 186, 142, 255),
                  new Color32(176, 88, 147, 255),
                  new Color32(163, 41, 73, 255),
                  new Color32(150, 140, 0, 255),
                  new Color32(0, 140, 150, 255),
                  new Color32(0, 0, 138, 255),
                  new Color32(0, 60, 112, 255),
                  new Color32(112, 86, 56, 255),
                  new Color32(88, 112, 84, 255),
                  new Color32(0, 99, 53, 255),
                  new Color32(75, 75, 99, 255),
                  new Color32(99, 75, 85, 255)
            };
    }
}