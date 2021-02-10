namespace TrafficManager.UI.SubTools {
    using ColossalFramework.Math;
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State.Keybinds;
    using TrafficManager.State;
    using TrafficManager.Util.Caching;
    using UnityEngine;
    using TrafficManager.Util;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.MainMenu.OSD;
    using static TrafficManager.Util.Shortcuts;
    using TrafficManager.UI.SubTools.PrioritySigns;

    public class LaneConnectorTool
        : LegacySubTool,
          UI.MainMenu.IOnscreenDisplayProvider
    {
        public LaneConnectorTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            // Log._Debug($"LaneConnectorTool: Constructor called");
            currentLaneEnds = new Dictionary<ushort, List<LaneEnd>>();

            CachedVisibleNodeIds = new GenericArrayCache<ushort>(NetManager.MAX_NODE_COUNT);
            LastCachedCamera = new CameraTransformValue();
        }

        /// <summary>State of the tool UI.</summary>
        private enum SelectionMode {
            None,
            SelectSource,
            SelectTarget,
        }

        /// <summary>
        /// State for rotating "Stay In Lane" modes.
        /// </summary>
        public enum StayInLaneMode {
            None,
            Both,
            Forward,
            Backward,
        }

        private static bool verbose_ =>
#if DEBUG
            DebugSwitch.LaneConnections.Get();
#else
            false;
#endif
        private static readonly Color DefaultLaneEndColor = new Color(1f, 1f, 1f, 0.4f);
        private LaneEnd selectedLaneEnd;
        private LaneEnd hoveredLaneEnd;
        private readonly Dictionary<ushort, List<LaneEnd>> currentLaneEnds;
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
        private GenericArrayCache<ushort> CachedVisibleNodeIds { get; }

        /// <summary>
        /// Stores last cached camera position in <see cref="CachedVisibleNodeIds"/>
        /// </summary>
        private CameraTransformValue LastCachedCamera { get; set; }

        private class LaneEnd {
            internal ushort SegmentId;
            internal ushort NodeId;
            internal bool StartNode;
            internal uint LaneId;
            internal bool IsSource;
            internal bool IsTarget;
            internal int OuterSimilarLaneIndex;
            internal int InnerSimilarLaneIndex; // used for stay in lane.
            internal int SegmentIndex; // index accesable by NetNode.GetSegment(SegmentIndex);
            internal readonly List<LaneEnd> ConnectedLaneEnds = new List<LaneEnd>();
            internal Color Color;

            internal SegmentLaneMarker SegmentMarker;
            internal NodeLaneMarker NodeMarker;

            internal NetInfo.LaneType LaneType;
            internal VehicleInfo.VehicleType VehicleType;

            /// <summary>
            ///  Intersects mouse ray with marker bounds.
            /// </summary>
            /// <returns><c>true</c>if mouse ray intersects with marker <c>false</c> otherwise</returns>
            internal bool IntersectRay() => SegmentMarker.IntersectRay();

            /// <summary>
            /// renders lane overlay. If highlighted, renders englarged sheath(lane+circle) overlay. Otherwise
            /// renders circle at lane end.
            /// </summary>
            internal void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color color, bool highlight = false) {
                if (highlight) {
                    SegmentMarker.RenderOverlay(cameraInfo, color, enlarge: true);
                }
                NodeMarker.RenderOverlay(cameraInfo, color, enlarge: highlight);
            }
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

            if (KeybindSettingsBase.RestoreDefaultsKey.IsPressed(e)) {
                frameClearPressed = Time.frameCount;

                // this will be consumed in RenderOverlay() if the key was pressed
                // not too long ago (within 20 Unity frames or 0.33 sec)
            }
        }

        public override void RenderOverlayForOtherTools(RenderManager.CameraInfo cameraInfo) {
            ShowOverlay(true, cameraInfo);
        }

        private void ShowOverlay(bool viewOnly, RenderManager.CameraInfo cameraInfo) {
            if (viewOnly && !(Options.connectedLanesOverlay ||
                MassEditOverlay.IsActive)) {
                return;
            }

            NetManager netManager = Singleton<NetManager>.instance;

            Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;

            // Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
            Camera currentCamera = Camera.main;

            // Check if camera pos/angle has changed then re-filter the visible nodes
            // Assumption: The states checked in this loop don't change while the tool is active
            var currentCameraState = new CameraTransformValue(currentCamera);
            if (!LastCachedCamera.Equals(currentCameraState)) {
                CachedVisibleNodeIds.Clear();
                LastCachedCamera = currentCameraState;

                for (ushort nodeId = 1; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
                    if (!Constants.ServiceFactory.NetService.IsNodeValid(nodeId)) {
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
                            (connectionClass.m_subService == ItemClass.SubService.PublicTransportMonorail))))) {
                        continue;
                    }

                    //--------------------------
                    // Check the camera distance
                    //--------------------------
                    Vector3 diff = NetManager.instance.m_nodes.m_buffer[nodeId].m_position - camPos;

                    if (diff.sqrMagnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE_SQR) {
                        continue; // do not draw if too distant
                    }

                    // Add
                    CachedVisibleNodeIds.Add(nodeId);
                }
            }

            for (int cacheIndex = CachedVisibleNodeIds.Size - 1; cacheIndex >= 0; cacheIndex--) {
                var nodeId = CachedVisibleNodeIds.Values[cacheIndex];

                bool hasMarkers = currentLaneEnds.TryGetValue((ushort)nodeId, out List<LaneEnd> laneEnds);
                if (!viewOnly && (GetSelectionMode() == SelectionMode.None)) {
                    MainTool.DrawNodeCircle(
                        cameraInfo: cameraInfo,
                        nodeId: (ushort)nodeId,
                        color: DefaultLaneEndColor,
                        alpha: true);
                }

                if (!hasMarkers) {
                    continue;
                }

                foreach (LaneEnd laneEnd in laneEnds) {
                    if (!Constants.ServiceFactory.NetService.IsLaneAndItsSegmentValid(laneEnd.LaneId)) {
                        continue;
                    }

                    if (laneEnd != selectedLaneEnd) {
                        foreach (LaneEnd targetLaneEnd in laneEnd.ConnectedLaneEnds) {
                            // render lane connection from laneEnd to targetLaneEnd
                            if (!Constants.ServiceFactory.NetService.IsLaneAndItsSegmentValid(targetLaneEnd.LaneId)) {
                                continue;
                            }

                            DrawLaneCurve(
                                cameraInfo: cameraInfo,
                                start: laneEnd.NodeMarker.TerrainPosition,
                                end: targetLaneEnd.NodeMarker.TerrainPosition,
                                middlePoint: NetManager.instance.m_nodes.m_buffer[nodeId].m_position,
                                color: laneEnd.Color,
                                outlineColor: Color.black);
                        }
                    }

                    if (viewOnly || (nodeId != SelectedNodeId)) {
                        continue;
                    }

                    bool drawMarker = false;
                    bool SourceMode = GetSelectionMode() == SelectionMode.SelectSource;
                    bool TargetMode = GetSelectionMode() == SelectionMode.SelectTarget;
                    if ( SourceMode & laneEnd.IsSource) {
                        // draw source marker in source selection mode,
                        // make exception for markers that have no target:
                        foreach(var targetLaneEnd in laneEnds) {
                            if (CanConnect(laneEnd, targetLaneEnd)){
                                drawMarker = true;
                                break;
                            }
                        }
                    } else if (TargetMode) {
                        // selected source marker in target selection mode
                        drawMarker =
                            selectedLaneEnd == laneEnd ||
                            CanConnect(selectedLaneEnd, laneEnd);
                    }

                    // highlight hovered marker and selected marker
                    if (drawMarker) {
                        bool markerIsHovered = false;
                        if (hoveredLaneEnd == null) {
                            float hitH = TrafficManagerTool.GetAccurateHitHeight();
                            markerIsHovered =
                                laneEnd.IntersectRay();

                            if (markerIsHovered) {
                                hoveredLaneEnd = laneEnd;
                            }
                        }

                        bool isTarget = selectedLaneEnd != null && laneEnd != selectedLaneEnd;
                        var color = isTarget ? Color.white : laneEnd.Color;
                        bool highlightMarker = laneEnd == selectedLaneEnd || markerIsHovered;
                        laneEnd.RenderOverlay(cameraInfo, color, highlightMarker);
                    } // if drawMarker

                    if (selectedLaneEnd != null) {
                        // lane curves for selectedMarker will be drawn last to
                        // be on the top of other lane markers.
                        foreach (LaneEnd targetLaneEnd in selectedLaneEnd.ConnectedLaneEnds) {
                            if (!Constants.ServiceFactory.NetService.IsLaneAndItsSegmentValid(targetLaneEnd.LaneId)) {
                                continue;
                            }

                            DrawLaneCurve(
                                cameraInfo: cameraInfo,
                                start: selectedLaneEnd.NodeMarker.TerrainPosition,
                                end: targetLaneEnd.NodeMarker.TerrainPosition,
                                middlePoint: NetManager.instance.m_nodes.m_buffer[nodeId].m_position,
                                color: selectedLaneEnd.Color,
                                outlineColor: Color.grey,
                                size: 0.18f); // Embolden
                        } // end foreach selectedMarker.ConnectedMarkers
                    } // end if selectedMarker != null
                } // end foreach lanemarker in node markers
            } // end for node in all nodes
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            // Log._Debug($"LaneConnectorTool: RenderOverlay. SelectedNodeId={SelectedNodeId}
            //     SelectedSegmentId={SelectedSegmentId} HoveredNodeId={HoveredNodeId}
            //     HoveredSegmentId={HoveredSegmentId} IsInsideUI={MainTool.GetToolController().IsInsideUI}");

            // draw lane markers and connections
            hoveredLaneEnd = null;

            ShowOverlay(false, cameraInfo);

            // draw bezier from source marker to mouse position in target marker selection
            if (SelectedNodeId != 0) {
                if (GetSelectionMode() == SelectionMode.SelectTarget) {
                    Vector3 selNodePos =
                        NetManager.instance.m_nodes.m_buffer[SelectedNodeId].m_position;

                    // Draw a currently dragged curve
                    var pos = HitPos;
                    if (hoveredLaneEnd == null) {
                        float hitH = TrafficManagerTool.GetAccurateHitHeight();
                        pos.y = hitH; // fix height.
                        float mouseH = MousePosition.y;
                        if (hitH < mouseH - TrafficManagerTool.MAX_HIT_ERROR) {
                            // for metros lane curve is projected on the ground.
                            pos = MousePosition;
                        }
                    } else {
                        // snap to hovered:
                        pos = hoveredLaneEnd.NodeMarker.TerrainPosition;
                    }
                    DrawLaneCurve(
                        cameraInfo: cameraInfo,
                        start: selectedLaneEnd.NodeMarker.TerrainPosition,
                        end: pos,
                        middlePoint: selNodePos,
                        color: Color.Lerp(a: selectedLaneEnd.Color, b: Color.white, t: 0.33f),
                        outlineColor: Color.white,
                        size:0.11f);
                }

                NetNode[] nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;

                if ((frameClearPressed > 0) && ((Time.frameCount - frameClearPressed) < 20)) {
                    // 0.33 sec
                    frameClearPressed = 0; // consumed
                    // remove all connections at selected node
                    LaneConnectionManager.Instance.RemoveLaneConnectionsFromNode(SelectedNodeId);
                    RefreshCurrentNodeMarkers(SelectedNodeId);
                }

                // Must press Shift+S (or another shortcut) within last 20 frames for this to work
                bool quickSetup = (frameStayInLanePressed > 0)
                                 && ((Time.frameCount - frameStayInLanePressed) < 20); // 0.33 sec
                if (quickSetup) {
                    frameStayInLanePressed = 0; // not pressed anymore (consumed)
                    frameClearPressed = 0; // consumed
                    selectedLaneEnd = null;
                    ref NetNode node = ref SelectedNodeId.ToNode();

                    bool stayInLane = GetSortedSegments(SelectedNodeId, out List<ushort> segList);
                    bool oneway = segMan.CalculateIsOneWay(segList[0]) || segMan.CalculateIsOneWay(segList[1]);

                    if (stayInLane) {
                        switch (stayInLaneMode) {
                            case StayInLaneMode.None: {
                                    stayInLaneMode = !oneway ? StayInLaneMode.Both : StayInLaneMode.Forward;
                                    break;
                                }

                            case StayInLaneMode.Both: {
                                    stayInLaneMode = StayInLaneMode.Forward;
                                    break;
                                }

                            case StayInLaneMode.Forward: {
                                    stayInLaneMode = !oneway ? StayInLaneMode.Backward : StayInLaneMode.None;
                                    break;
                                }

                            case StayInLaneMode.Backward: {
                                    stayInLaneMode = StayInLaneMode.None;
                                    break;
                                }
                        }
                    }

                    Log._Debug($"stayInLane:{stayInLane} stayInLaneMode:{stayInLaneMode}\n" +
                        $"GetMarkerSelectionMode()={GetSelectionMode()} SelectedNodeId={SelectedNodeId}");

                    if (stayInLane) {
                        stayInLane = StayInLane(SelectedNodeId, stayInLaneMode);
                        RefreshCurrentNodeMarkers(SelectedNodeId);
                    } // end if stay in lane

                    if (stayInLane) {
                        MainTool.Guide.Deactivate("LaneConnectorTool:stay-in-lane is not supported for this setup");
                    } else {
                        MainTool.Guide.Activate("LaneConnectorTool:stay-in-lane is not supported for this setup");
                    }
                } // end if quick setup
            } // end if selected node

            if ((GetSelectionMode() == SelectionMode.None) && (HoveredNodeId != 0)) {
                // draw hovered node
                MainTool.DrawNodeCircle(
                    cameraInfo: cameraInfo,
                    nodeId: HoveredNodeId,
                    warning: Input.GetMouseButton(0),
                    alpha: true);
            }
        }

        /// <summary> special case where all segments are oneway.
        /// supported scenarios:
        /// - one segment is going toward the junction and 2 to 3 segments are going against the junction
        /// - one segment is going against the junction and 2 to 3 segments are going toward the junction
        /// post condtion:
        ///  segment[0] is the middle source segment. (in middle of the inned/outer segments)
        ///  segment[1] is the middle target segment.(in middle of the inned/outer segments)
        ///  segment[2] is the segment that is attached to the junction from the outer side
        ///  segment[3] is the segment that is attached to the junction from the inner side.
        /// </summary>
        /// <param name="nodeId">junction</param>
        /// <param name="segments">list of segments. size must be 4. last element must be zero if there are only 3 segments</param>
        /// <returns><c>true</c> if the scenario is supported</returns>
        private static bool ArrangeOneWay(ushort nodeId, List<ushort> segments) {
            if(verbose_)
                Log._Debug($"called ArrangeOneWay({nodeId}, {segments.ToSTR()}");
            if (nodeId.ToNode().CountSegments() > 4)
                return false;
            foreach (var segmentId in segments) {
                if (segmentId != 0 && !segMan.CalculateIsOneWay(segmentId))
                    return false;
            }
            int sourceCount = segments
                .Where(segmentId => segmentId != 0 && netService.GetHeadNode(segmentId) == nodeId)
                .Count();

            int targetCount = segments
                .Where(segmentId => segmentId != 0 && netService.GetTailNode(segmentId) == nodeId)
                .Count();

            if (sourceCount == 1) {
                ushort sourceSegment = segments.FirstOrDefault(
                    segmentId => segmentId != 0 && netService.GetHeadNode(segmentId) == nodeId);
                Assert(sourceSegment != 0, "sourceSegment != 0");

                ushort outerSegment = sourceSegment.ToSegment().GetNearSegment(nodeId);
                ushort middleTargetSegment = outerSegment.ToSegment().GetNearSegment(nodeId);
                ushort innerSegment = segments[3] == 0 ? (ushort)0 :
                                      middleTargetSegment.ToSegment().GetNearSegment(nodeId);

                segments[0] = sourceSegment;
                segments[1] = middleTargetSegment;
                segments[2] = outerSegment;
                segments[3] = innerSegment;
                return true;
            } else if (targetCount == 1) {
                ushort targetSegment = segments.FirstOrDefault(
                    segmentId => segmentId != 0 && netService.GetTailNode(segmentId) == nodeId);
                Assert(targetSegment != 0, "targetSegment != 0");

                ushort outerSegment = targetSegment.ToSegment().GetFarSegment(nodeId);
                ushort middleSourceSegment = outerSegment.ToSegment().GetFarSegment(nodeId);
                ushort innerSegment = segments[3] == 0 ? (ushort)0 :
                                      middleSourceSegment.ToSegment().GetFarSegment(nodeId);

                segments[0] = middleSourceSegment;
                segments[1] = targetSegment;
                segments[2] = outerSegment;
                segments[3] = innerSegment;
                return true;
            }

            return false;
        }

        /// <summary>
        /// arranges the segments such that
        /// segments[0] is part of the main road and is going toward the node/junction.
        /// segments[1] is part of the main road and is going against the node/junction.
        /// segments[2] is the segments connected to main road from outter side.
        /// segments[3] is the segments connected to main road from the other side.
        /// </summary>
        /// <param name="nodeId">junction</param>
        /// <param name="segments">arranged list of segments. the size will be 4.
        /// if there are only 3 segmetns last elemnt will be 0</param>
        /// <returns><c>true</c> if sucessful</returns>
        public static bool GetSortedSegments(ushort nodeId, out List<ushort> segments) {
            segments = PriorityRoad.GetNodeSegments(nodeId);
            bool ret = false;
            int n = segments.Count;
            if (n == 2) {
                segments.Add(0);
                segments.Add(0);
                if(netService.GetHeadNode(segments[1]) == netService.GetTailNode(segments[0])) {
                    segments.Swap(0, 1);
                }
                ret = true;
            } else if (n == 3) {
                if (!PriorityRoad.ArrangeT(segments)) {
                    segments.Sort(PriorityRoad.CompareSegments);
                }

                // Prevent confusion if all roads are the same.
                ret = PriorityRoad.CompareSegments(segments[1], segments[2]) != 0;
                segments.Add(0);
            } else if(n == 4) {
                segments.Sort(PriorityRoad.CompareSegments);

                // Prevent confusion if all roads are the same.
                ret = PriorityRoad.CompareSegments(segments[1], segments[2]) != 0;
            }
            if (ret) {
                if (segments[2] != 0) {
                    // in case where all segments are oneway make sure:
                    // segments[2] is connected from outter side (or zero if non-existant)
                    // segments[3] is connected from inner side (or zero if non-existant)
                    bool oneway = segMan.CalculateIsOneWay(segments[0]) &&
                                  segMan.CalculateIsOneWay(segments[1]);
                    if (oneway) {
                        if (netService.GetTailNode(segments[0]) == nodeId) {
                            segments.Swap(0, 1);
                        }

                        // if the near side segment to segments[2] is going toward the junction
                        // then we know segment[2] is connected from inside.
                        var nearSegment = segments[2].ToSegment().GetNearSegment(nodeId);
                        bool connectedFromInside =
                            netService.GetHeadNode(nearSegment) == nodeId;
                        if (connectedFromInside) {
                            segments.Swap(2, 3);
                        }
                    } else {
                        // ensure segments[0] is comming toward the junction (is to the far side of segments[2])
                        // and segments[1] is going against the junction (is to the near side of segments[2])
                        if (segments[1] != segments[2].ToSegment().GetNearSegment(nodeId)) {
                            segments.Swap(0, 1);
                        }
                    }
                }
            } else {
                // final attempt to arrange one-way roads.
                // this code path is reached when all incomming/outgoing segments have the same size.
                ret = ArrangeOneWay(nodeId, segments);
            }

            return ret;
        }

        /// <summary>
        /// connects lanes in a T junction such that each lane is connected to one other lane.
        /// lane arithmatic must work for the side of the road which has a segment connection.
        /// in the case of all one way road and extra segment connection from inner side is also supported.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="mode">determines for which side to connect lanes.</param>
        /// <returns><c>true</c> if any lanes were connectde, <c>false</c> otherwise</returns>
        public static bool StayInLane(ushort nodeId, StayInLaneMode mode = StayInLaneMode.None) {
            Log._Debug($"Stay In Lane called node:{nodeId} mode:{mode}");
            LaneConnectionManager.Instance.RemoveLaneConnectionsFromNode(nodeId);

            GetSortedSegments(nodeId, out List<ushort> segments);
            ushort innerMinor = 0;
            bool oneway = segMan.CalculateIsOneWay(segments[0]) &&
                          segMan.CalculateIsOneWay(segments[1]);
            if (oneway) {
                // only when the main road is oneway do we support segment connected from inner side.
                innerMinor = segments[3];
            }

            bool ret = false;
            if (mode == StayInLaneMode.Both || mode == StayInLaneMode.Forward) {
                ret |= StayInLane(nodeId, segments[0], segments[1], segments[2], innerMinor);
            }
            if(mode == StayInLaneMode.Both || mode == StayInLaneMode.Backward) {
                ret |= StayInLane(nodeId, segments[1], segments[0], segments[3], 0);
            }
            return ret;
        }

        /// <summary>
        /// connects lanes such that cars will stay on lane.
        ///
        /// if segments are connected from inside and/or outside, then some lane connections
        /// are diverted toward those segments such that there is no criss-cross and cars stay on
        /// their respective lanes.
        ///
        /// if the number of lanes does not match:
        ///  - if the main road is two ways then we prefer to merge/split inner lanes.
        ///  - else if <paramref name="minorSegmentId"/> == 0 then
        ///    we prefer to merge/split outer lanes.
        ///  - else if <paramref name="minorSegmentId"/> != 0 and <paramref name="minorSegment2Id"/> == 0 then
        ///    we prefer to merge/split inner lanes.
        ///  - otherwise we have <paramref name="minorSegmentId"/> != 0 and <paramref name="minorSegment2Id"/> != 0 and
        ///    we prefer to merge/split centeral lanes.
        /// </summary>
        /// <param name="nodeId">The junction</param>
        /// <param name="mainSegmentSourceId">segment on the main road comming toward the junction</param>
        /// <param name="mainSegmentTargetId">segment on the main road going against the junction</param>
        /// <param name="minorSegmentId">minor segment attached from the outer side to the main road</param>
        /// <param name="minorSegment2Id">only valid where main road is oneway.
        /// this is the segment that is attached from the inner side to the main road.</param>
        /// <returns><c>false</c> if there is only one incomming/outgoing lane, <c>true</c> otherwise</returns>
        private static bool StayInLane(
            ushort nodeId,
            ushort mainSegmentSourceId,
            ushort mainSegmentTargetId,
            ushort minorSegmentId,
            ushort minorSegment2Id) {
            if (verbose_) {
                Log._Debug($"StayInLane(nodeId:{nodeId}, " +
                    $"mainSegmentSourceId:{mainSegmentSourceId}, mainSegmentTargetId:{mainSegmentTargetId}, " +
                    $"minorSegmentId:{minorSegmentId}, minorSegment2Id:{minorSegment2Id})");
            }
            ref NetSegment segment = ref minorSegmentId.ToSegment();
            ref NetNode node = ref nodeId.ToNode();
            bool oneway0 = segMan.CalculateIsOneWay(mainSegmentTargetId);
            bool oneway1 = segMan.CalculateIsOneWay(mainSegmentSourceId);
            bool oneway = oneway0 && oneway1;

            // which lanes should split/merge in case of lane mismatch:
            bool splitMiddle = oneway && minorSegmentId != 0 && minorSegment2Id != 0;
            bool splitOuter = oneway && minorSegmentId == 0 && minorSegment2Id != 0;
            bool splitInner = !splitMiddle && !splitOuter;
            if (verbose_) {
                Log._Debug($"splitOuter={splitOuter}  " +
                    $"splitInner={splitInner} " +
                    $"splitMiddle={splitMiddle}");
            }

            // count relavent source(going toward the junction) lanes and
            // target (going aginst the junction) lanes on each segment.
            int laneCountMinorSource = minorSegmentId == 0 ? 0 : CountLanesTowardJunction(minorSegmentId, nodeId);
            int laneCountMinorTarget = minorSegmentId == 0 ? 0 : CountLanesAgainstJunction(minorSegmentId, nodeId);
            int laneCountMinor2Source = minorSegment2Id == 0 ? 0 : CountLanesTowardJunction(minorSegment2Id, nodeId);
            int laneCountMinor2Target = minorSegment2Id == 0 ? 0 : CountLanesAgainstJunction(minorSegment2Id, nodeId);
            int laneCountMainSource = CountLanesTowardJunction(mainSegmentSourceId, nodeId);
            int laneCountMainTarget = CountLanesAgainstJunction(mainSegmentTargetId, nodeId);
            int totalSource = laneCountMinorSource + laneCountMainSource + laneCountMinor2Source;
            int totalTarget = laneCountMinorTarget + laneCountMainTarget + laneCountMinor2Target;

            if (verbose_) {
                bool laneArithmaticWorks = totalSource == totalTarget && laneCountMainSource >= laneCountMinorTarget;
                Log._Debug($"StayInLane: " +
                    $"laneCountMinorSource={laneCountMinorSource} " +
                    $"laneCountMinorTarget={laneCountMinorTarget} " +
                    $"laneCountMainSource={laneCountMainSource} " +
                    $"laneCountMainTarget={laneCountMainTarget} " +
                    $"laneCountMinor2Source={laneCountMinor2Source} " +
                    $"laneCountMinor2Target={laneCountMinor2Target} " +
                    $"totalSource={totalSource} " +
                    $"totalTarget={totalTarget} " +
                    $"laneArithmaticWorks={laneArithmaticWorks}");
            }

            bool ret = totalSource > 0 && totalTarget > 0;
            if (!nodeId.ToNode().m_flags.IsFlagSet(NetNode.Flags.Junction)) {
                ret &= totalSource > 1 || totalTarget > 1;
            }
            if (!ret)
                return false;

            float ratio =
                totalSource >= totalTarget ?
                totalSource / (float)totalTarget :
                totalTarget / (float)totalSource;

            /* here we are trying to create bounds based on the ratio of source VS target lanes.
             * these bounds determine which two lanes are matched (should be connected)
             * for example if totalSource is 3 and totalTarget is 6 the bounds would be:
             * source lane index : [Minimum inclusive matching target lane idnex, Maximum exclusive target laen index)
             * 0 : [0,2)
             * 1 : [2,4)
             * 2 : [4,6)
             * if totalSource > totalTarget then source and lanes will swap sides.
             */
            int LowerBound(int idx) => Round(idx * ratio);
            int UpperBound(int idx) => Round((idx + 1) * ratio);

            // checks if value is in [InclusiveLowerBound,ExclusiveUpperBound)
            bool InBound(int inclusiveLowerBound, int exclusiveUpperBound, int value) =>
                inclusiveLowerBound <= value && value < exclusiveUpperBound;

            // precondition: idx1 <= idx2
            bool IndexesMatchHelper(int idx1, int idx2) =>
                InBound(LowerBound(idx1), UpperBound(idx1), idx2);

            // calculates if input lane indeces match(ie should be cannected) according to ratio of source lanes VS target lanes.
            // if totalSource > totalTarget then source and target swap sides.
            bool IndexesMatch(int sourceIdx, int targetIdx) =>
                totalSource <= totalTarget ?
                IndexesMatchHelper(sourceIdx, targetIdx) :
                IndexesMatchHelper(targetIdx, sourceIdx);

            const float EPSILON = 1e-10f;
            // rounding approach controls which lanes will split/merge in case totoalSource != totalTarget.
            int Round(float f) {
                if (splitInner)
                    return Mathf.FloorToInt(f + EPSILON);
                else if (splitOuter)
                    return Mathf.CeilToInt(f - EPSILON);
                else // splitMiddle
                    return Mathf.RoundToInt(f);
            }

            // determines if the lanes on the main road should be
            // connected to minorSegmentId.
            bool ConnectToMinor(int sourceIdx, int targetIdx) {
                return totalSource >= totalTarget ?
                       sourceIdx < UpperBound(laneCountMinorTarget - 1) :
                       targetIdx < UpperBound(laneCountMinorSource - 1);
            }

            // determines wheather the lanes on the main road should be
            // connected to minorSegment2Id.
            bool ConnectToMinor2(int sourceIdx, int targetIdx) {
                return totalSource >= totalTarget ?
                       sourceIdx >= LowerBound(totalTarget - laneCountMinor2Target) :
                       targetIdx >= LowerBound(totalSource - laneCountMinor2Source);
            }

            List<LaneEnd> laneEnds = GetLaneEnds(nodeId, ref node);
            foreach (LaneEnd sourceLaneEnd in laneEnds) {
                if (!sourceLaneEnd.IsSource ||
                    sourceLaneEnd.SegmentId == mainSegmentTargetId) {
                    continue;
                }
                foreach (LaneEnd targetLaneEnd in laneEnds) {
                    if (!targetLaneEnd.IsTarget ||
                        targetLaneEnd.SegmentId == sourceLaneEnd.SegmentId ||
                        targetLaneEnd.SegmentId == mainSegmentSourceId ||
                        !CanConnect(sourceLaneEnd, targetLaneEnd)
                        ) {
                        continue;
                    }
                    bool connect = false;
                    if (
                        sourceLaneEnd.SegmentId == mainSegmentSourceId &&
                        targetLaneEnd.SegmentId == minorSegmentId &&
                        IndexesMatch(sourceLaneEnd.OuterSimilarLaneIndex, targetLaneEnd.OuterSimilarLaneIndex)) {
                        connect = true;
                    } else if (
                        sourceLaneEnd.SegmentId == minorSegmentId &&
                        targetLaneEnd.SegmentId == mainSegmentTargetId &&
                        IndexesMatch(sourceLaneEnd.OuterSimilarLaneIndex, targetLaneEnd.OuterSimilarLaneIndex)) {
                        connect = true;
                    } else if (
                        sourceLaneEnd.SegmentId == mainSegmentSourceId &&
                        targetLaneEnd.SegmentId == mainSegmentTargetId &&
                        !ConnectToMinor(sourceLaneEnd.OuterSimilarLaneIndex, targetLaneEnd.OuterSimilarLaneIndex) &&
                        !ConnectToMinor2(sourceLaneEnd.OuterSimilarLaneIndex, targetLaneEnd.OuterSimilarLaneIndex) &&
                        IndexesMatch(sourceLaneEnd.OuterSimilarLaneIndex + laneCountMinorSource,
                                     targetLaneEnd.OuterSimilarLaneIndex + laneCountMinorTarget)
                        ) {
                        connect = true;
                    } else if (
                        sourceLaneEnd.SegmentId == mainSegmentSourceId &&
                        targetLaneEnd.SegmentId == minorSegment2Id &&
                        IndexesMatch(
                            sourceLaneEnd.OuterSimilarLaneIndex + laneCountMinorSource,
                            targetLaneEnd.OuterSimilarLaneIndex + laneCountMinorTarget + laneCountMainTarget)) {
                        connect = true;
                    } else if (
                        sourceLaneEnd.SegmentId == minorSegment2Id &&
                        targetLaneEnd.SegmentId == mainSegmentTargetId &&
                        IndexesMatch(
                            sourceLaneEnd.OuterSimilarLaneIndex + laneCountMinorSource + laneCountMainSource,
                            targetLaneEnd.OuterSimilarLaneIndex + laneCountMinorTarget)) {
                        connect = true;
                    }

                    if (connect) {
                        LaneConnectionManager.Instance.AddLaneConnection(
                            sourceLaneEnd.LaneId,
                            targetLaneEnd.LaneId,
                            sourceLaneEnd.StartNode);
                    }
                } // foreach
            } // foreach
            return true;
        }

        private static int CountLanes(ushort segmentId, ushort nodeId, bool toward) {
            return netService.GetSortedLanes(
                                segmentId,
                                ref segmentId.ToSegment(),
                                netService.IsStartNode(segmentId, nodeId) ^ (!toward),
                                LaneConnectionManager.LANE_TYPES,
                                LaneConnectionManager.VEHICLE_TYPES,
                                true).Count;
        }
        internal static int CountLanesTowardJunction(ushort segmentId, ushort nodeId) => CountLanes(segmentId, nodeId, true);
        internal static int CountLanesAgainstJunction(ushort segmentId, ushort nodeId) => CountLanes(segmentId, nodeId, false);


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

            if (GetSelectionMode() == SelectionMode.None) {
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
                        selectedLaneEnd = null;
                        stayInLaneMode = StayInLaneMode.None;
                        MainTool.RequestOnscreenDisplayUpdate();
                        return;
                    }

                    if (SelectedNodeId != HoveredNodeId) {
                        Log._DebugIf(
                            logLaneConn,
                            () => $"Node {HoveredNodeId} has been selected. Creating markers.");

                        // selected node has changed. create markers
                        List<LaneEnd> laneEnds = GetLaneEnds(
                            HoveredNodeId,
                            ref Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId]);

                        if (laneEnds != null) {
                            SelectedNodeId = HoveredNodeId;
                            selectedLaneEnd = null;
                            stayInLaneMode = StayInLaneMode.None;

                            currentLaneEnds[SelectedNodeId] = laneEnds;
                            MainTool.RequestOnscreenDisplayUpdate();
                        }

                        // this.allNodeMarkers[SelectedNodeId] = GetNodeMarkers(SelectedNodeId);
                    }
                } else {
                    Log._DebugIf(
                        logLaneConn,
                        () => $"LaneConnectorTool: Node {SelectedNodeId} has been deselected.");

                    // click on free spot. deselect node
                    SelectedNodeId = 0;
                    selectedLaneEnd = null;
                    stayInLaneMode = StayInLaneMode.None;
                    MainTool.RequestOnscreenDisplayUpdate();
                    return;
                }
            }

            if (hoveredLaneEnd == null) {
                return;
            }

            //-----------------------------------
            // Hovered Marker
            //-----------------------------------
            stayInLaneMode = StayInLaneMode.None;

            Log._DebugIf(
                logLaneConn,
                () => $"LaneConnectorTool: hoveredMarker != null. selMode={GetSelectionMode()}");

            // hovered marker has been clicked
            if (GetSelectionMode() == SelectionMode.SelectSource) {
                // select source marker
                selectedLaneEnd = hoveredLaneEnd;
                Log._DebugIf(
                    logLaneConn,
                    () => "LaneConnectorTool: set selected marker");
                MainTool.RequestOnscreenDisplayUpdate();
            } else if (GetSelectionMode() == SelectionMode.SelectTarget) {
                // select target marker
                // bool success = false;
                if (LaneConnectionManager.Instance.RemoveLaneConnection(
                    selectedLaneEnd.LaneId,
                    hoveredLaneEnd.LaneId,
                    selectedLaneEnd.StartNode)) {

                    // try to remove connection
                    selectedLaneEnd.ConnectedLaneEnds.Remove(hoveredLaneEnd);
                    Log._DebugIf(
                        logLaneConn,
                        () => $"LaneConnectorTool: removed lane connection: {selectedLaneEnd.LaneId}, " +
                        $"{hoveredLaneEnd.LaneId}");

                    // success = true;
                } else if (LaneConnectionManager.Instance.AddLaneConnection(
                    selectedLaneEnd.LaneId,
                    hoveredLaneEnd.LaneId,
                    selectedLaneEnd.StartNode)) {
                    // try to add connection
                    selectedLaneEnd.ConnectedLaneEnds.Add(hoveredLaneEnd);
                    Log._DebugIf(
                        logLaneConn,
                        () => $"LaneConnectorTool: added lane connection: {selectedLaneEnd.LaneId}, " +
                        $"{hoveredLaneEnd.LaneId}");

                    // success = true;
                }

                /*if (success) {
                            // connection has been modified. switch back to source marker selection
                            Log._Debug($"LaneConnectorTool: switch back to source marker selection");
                            selectedMarker = null;
                            selMode = MarkerSelectionMode.SelectSource;
                    }*/
                MainTool.RequestOnscreenDisplayUpdate();
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

            switch (GetSelectionMode()) {
                // also: case MarkerSelectionMode.None:
                default: {
                        Log._DebugIf(
                            logLaneConn,
                            () => "LaneConnectorTool: OnSecondaryClickOverlay: nothing to do");
                        stayInLaneMode = StayInLaneMode.None;
                        MainTool.RequestOnscreenDisplayUpdate();
                        break;
                    }

                case SelectionMode.SelectSource: {
                        // deselect node
                        Log._DebugIf(
                            logLaneConn,
                            () => "LaneConnectorTool: OnSecondaryClickOverlay: selected node id = 0");
                        SelectedNodeId = 0;
                        MainTool.RequestOnscreenDisplayUpdate();
                        break;
                    }

                case SelectionMode.SelectTarget: {
                        // deselect source marker
                        Log._DebugIf(
                            logLaneConn,
                            () => "LaneConnectorTool: OnSecondaryClickOverlay: switch to selected source mode");
                        selectedLaneEnd = null;
                        MainTool.RequestOnscreenDisplayUpdate();
                        break;
                    }
            }

            if (GetSelectionMode() == SelectionMode.None) {
                MainTool.SetToolMode(ToolMode.None);
            }
        }

        public override void OnActivate() {
            base.OnActivate();
#if DEBUG
            bool logLaneConn = DebugSwitch.LaneConnections.Get();
            if (logLaneConn) {
                Log._Debug("LaneConnectorTool: OnActivate");
            }
#endif
            SelectedNodeId = 0;
            selectedLaneEnd = null;
            hoveredLaneEnd = null;
            stayInLaneMode = StayInLaneMode.None;
            RefreshCurrentNodeMarkers();
            MainTool.RequestOnscreenDisplayUpdate();
        }

        private void RefreshCurrentNodeMarkers(ushort forceNodeId = 0) {
            if (forceNodeId == 0) {
                currentLaneEnds.Clear();
            } else {
                currentLaneEnds.Remove(forceNodeId);
            }

            for (ushort nodeId = forceNodeId == 0 ? (ushort)1 : forceNodeId;
                 nodeId <= (forceNodeId == 0 ? NetManager.MAX_NODE_COUNT - 1 : forceNodeId);
                 ++nodeId) {
                if (!Constants.ServiceFactory.NetService.IsNodeValid(nodeId)) {
                    continue;
                }

                if (nodeId != SelectedNodeId &&
                    !LaneConnectionManager.Instance.HasNodeConnections(nodeId)) {
                    continue;
                }

                List<LaneEnd> laneEnds = GetLaneEnds(
                    nodeId,
                    ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId]);

                if (laneEnds == null) {
                    continue;
                }

                currentLaneEnds[nodeId] = laneEnds;
            }
        }

        private SelectionMode GetSelectionMode() {
            if (SelectedNodeId == 0) {
                return SelectionMode.None;
            }

            return selectedLaneEnd == null
                       ? SelectionMode.SelectSource
                       : SelectionMode.SelectTarget;
        }

        public override void Cleanup() { }

        public override void Initialize() {
            base.Initialize();
            Cleanup();
            if (Options.connectedLanesOverlay ||
                MassEditOverlay.IsActive) {
                RefreshCurrentNodeMarkers();
            } else {
                currentLaneEnds.Clear();
            }
        }

        /// <summary>For NodeId extract the lane ends coming into that node.</summary>
        /// <param name="nodeId">Node id.</param>
        /// <param name="node">Ref to the node struct.</param>
        /// <returns>List of lane end structs.</returns>
        private static List<LaneEnd> GetLaneEnds(ushort nodeId, ref NetNode node) {
            if (nodeId == 0) {
                return null;
            }

            if ((node.m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
                return null;
            }

            List<LaneEnd> laneEnds = new List<LaneEnd>();
            int nodeMarkerColorIndex = 0;
            LaneConnectionManager connManager = LaneConnectionManager.Instance;

            float offset = node.CountSegments() <= 2 ? 3 : 1;

            for (int i = 0; i < 8; i++) {
                ushort segmentId = node.GetSegment(i);

                if (segmentId == 0) {
                    continue;
                }

                NetSegment[] segmentsBuffer = NetManager.instance.m_segments.m_buffer;
                bool startNode = segmentsBuffer[segmentId].m_startNode == nodeId;
                NetInfo.Lane[] lanes = segmentsBuffer[segmentId].Info.m_lanes;
                uint laneId = segmentsBuffer[segmentId].m_lanes;
                float offsetT = offset / segmentId.ToSegment().m_averageLength;

                for (byte laneIndex = 0; (laneIndex < lanes.Length) && (laneId != 0); laneIndex++) {
                    NetInfo.Lane laneInfo = lanes[laneIndex];

                    if (((laneInfo.m_laneType & LaneConnectionManager.LANE_TYPES) != NetInfo.LaneType.None)
                        && ((laneInfo.m_vehicleType & LaneConnectionManager.VEHICLE_TYPES)
                            != VehicleInfo.VehicleType.None)) {
                        if (connManager.GetLaneEndPoint(
                            segmentId: segmentId,
                            startNode: startNode,
                            laneIndex: laneIndex,
                            laneId: laneId,
                            laneInfo: laneInfo,
                            outgoing: out bool isSource,
                            incoming: out bool isTarget,
                            pos: out _))
                        {
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
                            NodeLaneMarker nodeMarker = new NodeLaneMarker {
                                TerrainPosition = terrainPos,
                                Position = (Vector3)pos,
                            };

                            Color32 nodeMarkerColor = isSource
                                      ? COLOR_CHOICES[nodeMarkerColorIndex % COLOR_CHOICES.Length]
                                      : default; // transparent

                            bool isFoward = (laneInfo.m_direction & NetInfo.Direction.Forward) != 0;
                            int innerSimilarLaneIndex;
                            if (isFoward) {
                                innerSimilarLaneIndex = laneInfo.m_similarLaneIndex;
                            } else {
                                innerSimilarLaneIndex = laneInfo.m_similarLaneCount -
                                              laneInfo.m_similarLaneIndex - 1;
                            }
                            int outerSimilarLaneIndex = laneInfo.m_similarLaneCount - innerSimilarLaneIndex - 1;

                            laneEnds.Add(
                                new LaneEnd {
                                    SegmentId = segmentId,
                                    LaneId = laneId,
                                    NodeId = nodeId,
                                    StartNode = startNode,
                                    Color = nodeMarkerColor,
                                    IsSource = isSource,
                                    IsTarget = isTarget,
                                    LaneType = laneInfo.m_laneType,
                                    VehicleType = laneInfo.m_vehicleType,
                                    InnerSimilarLaneIndex = innerSimilarLaneIndex,
                                    OuterSimilarLaneIndex = outerSimilarLaneIndex,
                                    SegmentIndex = i,
                                    NodeMarker = nodeMarker,
                                    SegmentMarker = segmentMarker,
                                });

                            if (isSource) {
                                nodeMarkerColorIndex++;
                            }
                        }
                    }

                    laneId = NetManager.instance.m_lanes.m_buffer[laneId].m_nextLane;
                }
            }

            if (laneEnds.Count == 0) {
                return null;
            }

            foreach (LaneEnd laneEnd1 in laneEnds) {
                if (!laneEnd1.IsSource) {
                    continue;
                }

                uint[] connections =
                    LaneConnectionManager.Instance.GetLaneConnections(
                        laneEnd1.LaneId,
                        laneEnd1.StartNode);

                if ((connections == null) || (connections.Length == 0)) {
                    continue;
                }

                foreach (LaneEnd laneEnd2 in laneEnds) {
                    if (!laneEnd2.IsTarget) {
                        continue;
                    }

                    if (connections.Contains(laneEnd2.LaneId)) {
                        laneEnd1.ConnectedLaneEnds.Add(laneEnd2);
                    }
                }
            }

            return laneEnds;
        }

        private static bool CanConnect(LaneEnd source, LaneEnd target) {
            bool ret = source != target && source.IsSource && target.IsTarget;
            ret &= (target.VehicleType & source.VehicleType) != 0;

            bool IsRoad(LaneEnd laneEnd) =>
                (laneEnd.LaneType & LaneArrowManager.LANE_TYPES) != 0 &&
                (laneEnd.VehicleType & LaneArrowManager.VEHICLE_TYPES) != 0;

            // turning angle does not apply to roads.
            bool isRoad = IsRoad(source) && IsRoad(target);

            // check track turning angles are within bounds
            ret &= isRoad || CheckSegmentsTurningAngle(
                    sourceSegmentId: source.SegmentId,
                    sourceSegment: ref GetSeg(source.SegmentId),
                    sourceStartNode: source.StartNode,
                    targetSegmentId: target.SegmentId,
                    targetSegment: ref GetSeg(target.SegmentId),
                    targetStartNode: target.StartNode);

            return ret;
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
        private static bool CheckSegmentsTurningAngle(ushort sourceSegmentId,
                                                      ref NetSegment sourceSegment,
                                                      bool sourceStartNode,
                                                      ushort targetSegmentId,
                                                      ref NetSegment targetSegment,
                                                      bool targetStartNode) {
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

                Vector3 targetDirection = targetStartNode
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
                                   float size = 0.08f) {
            Bezier3 bezier;
            bezier.a = start;
            bezier.d = end;

            NetSegment.CalculateMiddlePoints(
                startPos: bezier.a,
                startDir: (middlePoint - bezier.a).normalized,
                endPos: bezier.d,
                endDir: (middlePoint - bezier.d).normalized,
                smoothStart: false,
                smoothEnd: false,
                middlePos1: out bezier.b,
                middlePos2: out bezier.c);

            // Draw black outline
            RenderManager.instance.OverlayEffect.DrawBezier(
                cameraInfo: cameraInfo,
                color: outlineColor,
                bezier: bezier,
                size: size * 1.5f,
                cutStart: 0,
                cutEnd: 0,
                minY: -1f,
                maxY: 1280f,
                renderLimits: false,
                alphaBlend: false);

            // Inside the outline draw colored bezier
            RenderManager.instance.OverlayEffect.DrawBezier(
                cameraInfo: cameraInfo,
                color: color,
                bezier: bezier,
                size: size,
                cutStart: 0,
                cutEnd: 0,
                minY: -1f,
                maxY: 1280f,
                renderLimits: false,
                alphaBlend: true);
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
                  new Color32(189, 186, 142, 255),
                  new Color32(106, 41, 163, 255),
                  new Color32(0, 99, 53, 255),
                  new Color32(54, 118, 214, 255),
                  new Color32(163, 57, 41, 255),
                  new Color32(54, 161, 214, 255),
                  new Color32(107, 214, 193, 255),
                  new Color32(214, 161, 175, 255),
                  new Color32(214, 0, 171, 255),
                  new Color32(151, 178, 201, 255),
                  new Color32(189, 101, 0, 255),
                  new Color32(163, 41, 73, 255),
                  new Color32(154, 142, 189, 255),
                  new Color32(176, 88, 147, 255),
                  new Color32(150, 140, 0, 255),
                  new Color32(0, 140, 150, 255),
                  new Color32(0, 0, 138, 255),
                  new Color32(0, 60, 112, 255),
                  new Color32(120, 189, 94, 255),
                  new Color32(112, 86, 56, 255),
                  new Color32(88, 112, 84, 255),
                  new Color32(75, 75, 99, 255),
                  new Color32(99, 75, 85, 255),
            };

        private static string T(string key) => Translation.LaneRouting.Get(key);

        /// <inheritdoc/>
        public void UpdateOnscreenDisplayPanel() {
            SelectionMode m = GetSelectionMode();

            switch (m) {
                case SelectionMode.None: {
                    var items = new List<OsdItem>();
                    items.Add(new ModeDescription(localizedText: T("LaneConnector.Mode:Select")));
                    OnscreenDisplay.Display(items);
                    return;
                }
                case SelectionMode.SelectTarget:
                case SelectionMode.SelectSource: {
                    var items = new List<OsdItem>();
                    items.Add(new ModeDescription(
                                  m == SelectionMode.SelectSource
                                      ? T("LaneConnector.Mode:Source")
                                      : T("LaneConnector.Mode:Target")));
                    items.Add(new Shortcut(
                                  keybindSetting: KeybindSettingsBase.LaneConnectorStayInLane,
                                  localizedText: T("LaneConnector.Label:Stay in lane, multiple modes")));
                    items.Add(new Shortcut(
                                  keybindSetting: KeybindSettingsBase.RestoreDefaultsKey,
                                  localizedText: T("LaneConnector.Label:Reset to default")));

                    items.Add(m == SelectionMode.SelectSource
                                  ? OnscreenDisplay.RightClick_LeaveNode()
                                  : OnscreenDisplay.RightClick_LeaveLane());
                    OnscreenDisplay.Display(items);
                    return;
                }
            }

            // Default: no hint
            OnscreenDisplay.Clear();
        }
    }
}
