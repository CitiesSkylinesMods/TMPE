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
    using ColossalFramework.UI;
    using TrafficManager.API.Manager;
    using TrafficManager.UI.MainMenu;
    using TrafficManager.UI.MainMenu.OSD;

    public class RoutingDetectorTool : TrafficManagerSubTool, IOnscreenDisplayProvider {

        private LaneEnd selectedLaneEnd_;
        private LaneEnd hoveredLaneEnd_;
        private LaneEnd[] nodeLaneEnds_;
        private LaneTransitionData[] transitions_;

        public RoutingDetectorTool(TrafficManagerTool mainTool)
            : base(mainTool) {
        }

        private static RoutingManager RMan => RoutingManager.Instance;

        private uint HoveredLaneId => hoveredLaneEnd_?.LaneId ?? 0;

        public override void OnActivateTool() {
            SelectedNodeId = 0;
            selectedLaneEnd_ = null;
            nodeLaneEnds_ = null;
            MainTool.RequestOnscreenDisplayUpdate();
        }

        public override void OnDeactivateTool() {
            SelectedNodeId = 0;
            selectedLaneEnd_ = null;
            nodeLaneEnds_ = null;
        }

        public override void RenderActiveToolOverlay(RenderManager.CameraInfo cameraInfo) {
            if (nodeLaneEnds_ == null) {
                if (HoveredNodeId != 0) {
                    Highlight.DrawNodeCircle(cameraInfo, HoveredNodeId);
                }
            } else if (selectedLaneEnd_ == null) {
                foreach (var sourceLaneEnd in nodeLaneEnds_) {
                    if (sourceLaneEnd.Connections != null) {
                        bool highlight = HoveredLaneId == sourceLaneEnd.LaneId;
                        sourceLaneEnd.RenderOverlay(cameraInfo, Color.white, highlight: highlight);
                    }
                }
                if(hoveredLaneEnd_ != null) {
                    foreach (var connection in hoveredLaneEnd_.Connections) {
                        connection.TargetLaneEnd.RenderOverlay(cameraInfo, connection.Color, highlight: true);
                    }
                }
            } else {
                selectedLaneEnd_.RenderOverlay(cameraInfo, Color.white, highlight: true);
                bool connectionHighlighted = false;
                foreach (var connection in selectedLaneEnd_.Connections) {
                    bool highlight = HoveredLaneId == connection.TargetLaneEnd.LaneId;
                    connectionHighlighted |= highlight;
                    connection.TargetLaneEnd.RenderOverlay(cameraInfo, connection.Color, highlight: highlight);
                }

                if (!connectionHighlighted && !(hoveredLaneEnd_?.Connections).IsNullOrEmpty()) {
                    hoveredLaneEnd_?.RenderOverlay(cameraInfo, Color.white, highlight: true);
                }
            }
        }

        public override void UpdateEveryFrame() {
            var prev = hoveredLaneEnd_;
            hoveredLaneEnd_ = null;
            if (nodeLaneEnds_ != null) {
                foreach(var laneEnd in nodeLaneEnds_) {
                    if (laneEnd.IntersectRay()) {
                        hoveredLaneEnd_ = laneEnd;
                    }
                }
            }

            if(prev != hoveredLaneEnd_) {
                var connection = selectedLaneEnd_?.Connections?.FirstOrDefault(item => item.TargetLaneEnd == hoveredLaneEnd_);
                transitions_ = connection?.Transtitions;
                MainTool.RequestOnscreenDisplayUpdate();
            }
        }

        public override void RenderActiveToolOverlay_GUI() { }

        public override void RenderGenericInfoOverlay(RenderManager.CameraInfo cameraInfo) { }

        public override void RenderGenericInfoOverlay_GUI() { }

        public override void OnToolLeftClick() {
            if (nodeLaneEnds_ == null) {
                if (HoveredNodeId.ToNode().IsValid()) {
                    SelectedNodeId = HoveredNodeId;
                    nodeLaneEnds_ = CalcualteNodeLaneEnds(SelectedNodeId);
                    MainTool.RequestOnscreenDisplayUpdate();
                }
            } else if (hoveredLaneEnd_?.Connections != null) {
                selectedLaneEnd_ = hoveredLaneEnd_;
                MainTool.RequestOnscreenDisplayUpdate();
            }
        }

        public override void OnToolRightClick() {
            if(selectedLaneEnd_ != null) {
                selectedLaneEnd_ = null;
                MainTool.RequestOnscreenDisplayUpdate();
            } else if(nodeLaneEnds_ != null) {
                SelectedNodeId = 0;
                nodeLaneEnds_ = null;
                MainTool.RequestOnscreenDisplayUpdate();
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
                    if (routedLane) {
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

                            Color color = transition.type switch {
                                LaneEndTransitionType.Default => Color.blue,
                                LaneEndTransitionType.LaneConnection => Color.green,
                                LaneEndTransitionType.Relaxed => Color.yellow,
                                _ => Color.black,
                            };

                            connection = new Connection {
                                Transtitions = new[] { transition },
                                TargetLaneEnd = targetLaneEnd,
                                Color = color,
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

        public void UpdateOnscreenDisplayPanel() {
            // no need to translate debug only tool.

            var items = new List<OsdItem>();
            if (nodeLaneEnds_ == null) {
                items.Add(new MainMenu.OSD.HardcodedMouseShortcut(
                    button: UIMouseButton.Left,
                    shift: false,
                    ctrl: false,
                    alt: false,
                    localizedText: "Select node"));
                items.Add(new MainMenu.OSD.HardcodedMouseShortcut(
                    button: UIMouseButton.Right,
                    shift: false,
                    ctrl: false,
                    alt: false,
                    localizedText: "Exit subtool"));
            } else if (selectedLaneEnd_ == null) {
                items.Add(new MainMenu.OSD.HardcodedMouseShortcut(
                    button: UIMouseButton.Left,
                    shift: false,
                    ctrl: false,
                    alt: false,
                    localizedText: "Select source lane"));
                items.Add(new MainMenu.OSD.HardcodedMouseShortcut(
                    button: UIMouseButton.Right,
                    shift: false,
                    ctrl: false,
                    alt: false,
                    localizedText: "Deselect node"));
            } else {
                items.Add(new MainMenu.OSD.Label("Hover over target lane to show transitions"));
                items.Add(new MainMenu.OSD.HardcodedMouseShortcut(
                    button: UIMouseButton.Right,
                    shift: false,
                    ctrl: false,
                    alt: false,
                    localizedText: "Deselect source lane"));
                items.Add(new MainMenu.OSD.HardcodedMouseShortcut(
                    button: UIMouseButton.Left,
                    shift: false,
                    ctrl: false,
                    alt: false,
                    localizedText: "Select source lane"));

                if (hoveredLaneEnd_ != null && !transitions_.IsNullOrEmpty()) {
                    foreach (var transition in transitions_) {
                        items.Add(new MainMenu.OSD.Label($"type:{transition.type} | group:{transition.group} | distance:{transition.distance}"));
                    }
                }
            }

            OnscreenDisplay.Display(items: items);
        }
    }
}
