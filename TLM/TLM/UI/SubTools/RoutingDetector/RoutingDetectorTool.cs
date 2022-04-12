namespace TrafficManager.UI.SubTools.RoutingDetector {
    using System;
    using TrafficManager.Util;
    using TrafficManager.UI.Helpers;
    using TrafficManager.Util.Extensions;
    using UnityEngine;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;
    using System.Linq;
    using TrafficManager.API.Traffic.Enums;
    using ColossalFramework.Math;
    using ColossalFramework;
    using CSUtil.Commons;

    public class RoutingDetectorTool : TrafficManagerSubTool {

        private LaneEnd selectedLaneEnd_;
        private LaneEnd hoveredLaneEnd_;
        private LaneEnd[] nodeLaneEnds_;

        public RoutingDetectorTool(TrafficManagerTool mainTool)
            : base(mainTool) {
        }

        private static RoutingManager RMan => RoutingManager.Instance;

        public override void OnActivateTool() {
            SelectedNodeId = 0;
            selectedLaneEnd_ = null;
            nodeLaneEnds_ = null;
        }

        public override void OnDeactivateTool() {
            SelectedNodeId = 0;
            selectedLaneEnd_ = null;
            nodeLaneEnds_ = null;
        }

        public override void RenderActiveToolOverlay(RenderManager.CameraInfo cameraInfo) {
            if (SelectedNodeId == 0) {
                if (HoveredNodeId != 0) {
                    Highlight.DrawNodeCircle(cameraInfo, HoveredNodeId);
                }
            } else if (selectedLaneEnd_ == null) {
                foreach (var sourceLaneEnd in nodeLaneEnds_) {
                    if (sourceLaneEnd.Connections != null) {
                        bool highlight = sourceLaneEnd.LaneId == hoveredLaneEnd_.LaneId;
                        sourceLaneEnd.RenderOverlay(cameraInfo, Color.white, highlight: highlight);
                    }
                }
            } else {
                foreach (var connection in selectedLaneEnd_.Connections) {
                    bool highlight = connection.TargetLaneEnd.LaneId == hoveredLaneEnd_.LaneId;
                    Color color = connection.Transtitions[0].type switch {
                        LaneEndTransitionType.Default => Color.blue,
                        LaneEndTransitionType.LaneConnection => Color.green,
                        LaneEndTransitionType.Relaxed => Color.yellow,
                        _ => Color.black,
                    };

                    connection.TargetLaneEnd.RenderOverlay(cameraInfo, color, highlight: highlight);
                }
            }
        }

        public override void UpdateEveryFrame() {
            //throw new NotImplementedException();
        }

        public override void RenderActiveToolOverlay_GUI() {
            //throw new NotImplementedException();
        }

        public override void RenderGenericInfoOverlay(RenderManager.CameraInfo cameraInfo) {
            //throw new NotImplementedException();
        }

        public override void RenderGenericInfoOverlay_GUI() {
            //throw new NotImplementedException();
        }

        public override void OnToolLeftClick() {
            if (SelectedNodeId == 0) {
                if (HoveredNodeId.ToNode().IsValid()) {
                    SelectedNodeId = HoveredNodeId;
                    nodeLaneEnds_ = CalcualteNodeLaneEnds(SelectedNodeId);
                }
            } else if (hoveredLaneEnd_?.Connections != null) {
                selectedLaneEnd_ = hoveredLaneEnd_;
            }
        }

        public override void OnToolRightClick() {
            if(selectedLaneEnd_ != null) {
                selectedLaneEnd_ = null;
            } else if(SelectedNodeId != 0) {
                SelectedNodeId = 0;
            } else {
                MainTool.SetToolMode(ToolMode.None);
            }
        }



        private LaneEnd[] CalcualteNodeLaneEnds(ushort nodeId) {
            ref NetNode netNode = ref nodeId.ToNode();
            if (!netNode.IsValid())
                return null;

            List<LaneEnd> laneEnds = new();

            float offset = netNode.CountSegments() <= 2 ? 3 : 1;
            bool isUnderground = nodeId.ToNode().m_flags.IsFlagSet(NetNode.Flags.Underground);

            for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                ushort segmentId = netNode.GetSegment(segmentIndex);
                if (segmentId == 0) {
                    continue;
                }

                ref NetSegment netSegment = ref segmentId.ToSegment();
                float offsetT = FloatUtil.IsZero(netSegment.m_averageLength) ? 0.1f : offset / netSegment.m_averageLength;

                foreach (var item in netSegment.GetSegmentLaneIdsAndLaneIndexes()) {
                    uint laneId = item.laneId;
                    NetInfo.Lane laneInfo = netSegment.Info.m_lanes[item.laneIndex];
                    bool routedLane = laneInfo.CheckType(RoutingManager.ROUTED_LANE_TYPES, RoutingManager.ROUTED_VEHICLE_TYPES);
                    if (!routedLane) {
                        ref NetLane netLane = ref laneId.ToLane();
                        bool startNode = netLane.IsStartNode(nodeId);
                        uint routingIndex = RoutingManager.Instance.GetLaneEndRoutingIndex(laneId, startNode);
                        var routing = RoutingManager.Instance.LaneEndForwardRoutings[routingIndex];
                        Vector3 pos;
                        Bezier3 bezier = laneId.ToLane().m_bezier;
                        if (startNode) {
                            bezier = bezier.Cut(offsetT, 1f);
                            pos = bezier.a;
                        } else {
                            bezier = bezier.Cut(0, 1f - offsetT);
                            pos = bezier.d;
                        }
                        float terrainY = Singleton<TerrainManager>.instance.SampleDetailHeightSmooth(pos);
                        var terrainPos = new Vector3(pos.x, terrainY, pos.z);

                        SegmentLaneMarker segmentMarker = new SegmentLaneMarker(bezier);
                        if (isUnderground) {
                            // force overlay height to match node position
                            segmentMarker.ForceBezierHeight(netNode.m_position.y);
                            pos.y = netNode.m_position.y;
                        }

                        NodeLaneMarker nodeMarker = new NodeLaneMarker {
                            TerrainPosition = terrainPos,
                            Position = pos,
                        };

                        LaneEnd laneEnd = new LaneEnd {
                            LaneId = item.laneId,
                            LaneIndex = item.laneIndex,
                            LaneInfo = laneInfo,

                            SegmentId = segmentId,
                            NodeId = nodeId,
                            StartNode = startNode,
                            NodeMarker = nodeMarker,
                            SegmentMarker = segmentMarker,
                        };
                        laneEnds.Add(laneEnd);
                    }
                }
            }

            // populate target lane ends:
            foreach (LaneEnd sourceLaneEnd in laneEnds) {
                uint routingIndex = RMan.GetLaneEndRoutingIndex(sourceLaneEnd.LaneId, sourceLaneEnd.StartNode);
                var routing = RMan.LaneEndForwardRoutings[routingIndex];
                bool hasValidTransitions =
                    routing.routed &&
                    routing.transitions != null &&
                    routing.transitions.Any(item => item.type != LaneEndTransitionType.Invalid);
                if (!hasValidTransitions) {
                    sourceLaneEnd.Connections = null;
                    continue;
                }

                List<Connection> connections = new();
                foreach (var transition in routing.transitions) {
                    if (transition.type != LaneEndTransitionType.Invalid) {
                        uint targetLaneId = transition.laneId;
                        Connection connection = connections.FirstOrDefault(item => item.TargetLaneEnd.LaneId == targetLaneId);
                        if (connection == null) {
                            LaneEnd targetLaneEnd = laneEnds.FirstOrDefault(item => item.LaneId == targetLaneId);
                            if (targetLaneEnd == null) {
                                Log.Error($"could not find target lane end for node:{nodeId} laneId:{targetLaneId}");
                                continue;
                            }
                            connection = new Connection {
                                Transtitions = new[] { transition },
                                TargetLaneEnd = targetLaneEnd,
                            };
                            connections.Add(connection);
                        } else {
                            connection.Transtitions = connection.Transtitions.Append(transition);
                        }
                    }
                }

                sourceLaneEnd.Connections = connections.ToArray();
            }

            return laneEnds.ToArray();
        }
    }
}
