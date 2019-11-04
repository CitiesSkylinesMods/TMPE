namespace TrafficManager.UI.SubTools {
    using System;
    using System.Collections.Generic;
    using API.Manager;
    using API.Traffic.Data;
    using API.Traffic.Enums;
    using ColossalFramework;
    using CSUtil.Commons;
    using Manager.Impl;
    using State;
    using Textures;
    using UnityEngine;
    using Util;
    using static Util.SegmentTraverser;
    using GenericGameBridge.Service;

    public class PrioritySignsTool : SubTool {
        private enum PrioritySignsMassEditMode {
            MainYield = 0,
            MainStop = 1,
            YieldMain = 2,
            StopMain = 3,
            Delete = 4
        }

        private readonly HashSet<ushort> currentPriorityNodeIds;
        private PrioritySignsMassEditMode massEditMode = PrioritySignsMassEditMode.MainYield;

        public PrioritySignsTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            currentPriorityNodeIds = new HashSet<ushort>();
        }


        private static int CountCarLanes(ushort segmentId) {
            NetSegment segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            int forward = 0, backward = 0;
            segment.CountLanes(
                segmentId,
                        NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                        VehicleInfo.VehicleType.Car,
                        ref forward,
                        ref backward);
            return forward + backward;
        }
        private static class FixPriorityJunction {
            private static int CompareSegments(ushort seg1Id, ushort seg2Id) {
                NetSegment seg1 = Singleton<NetManager>.instance.m_segments.m_buffer[seg1Id];
                NetSegment seg2 = Singleton<NetManager>.instance.m_segments.m_buffer[seg2Id];
                int diff = (int)Math.Ceiling(seg2.Info.m_halfWidth - seg1.Info.m_halfWidth);
                if(diff == 0) {
                    diff = CountCarLanes(seg2Id) - CountCarLanes(seg1Id);
                }

                return diff;
            }


            private static void AddSeg_helper(List<ushort> seglist, ushort segId) {
                if(segId != 0) {
                    seglist.Add(segId);
                }
            }
            private static bool IsOneWay(ushort segmentId, ushort nodeId) {
                NetSegment seg = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
                int forward = 0, backward = 0;
                seg.CountLanes(
                        segmentId,
                        NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                        VehicleInfo.VehicleType.Car,
                        ref forward,
                        ref backward);
                return forward == 0 || backward == 0;
            }

            private static void FixMajorSegment(ushort segmentId, ushort nodeId) {
                ref NetSegment seg = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
                bool startNode = seg.m_startNode == nodeId;

                JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(segmentId, startNode, true);
                JunctionRestrictionsManager.Instance.SetPedestrianCrossingAllowed(segmentId, startNode, false);
                TrafficPriorityManager.Instance.SetPrioritySign(segmentId, startNode, PriorityType.Main);

                int n_right = LaneArrowManager.SeparateTurningLanes.CountTargetLanesTowardDirection(segmentId, nodeId, ArrowDirection.Right);
                int n_left = LaneArrowManager.SeparateTurningLanes.CountTargetLanesTowardDirection(segmentId, nodeId, ArrowDirection.Left);
                int n_forward = LaneArrowManager.SeparateTurningLanes.CountTargetLanesTowardDirection(segmentId, nodeId, ArrowDirection.Forward);

                //list of outgoing lanes from current segment to current node.
                IList<LanePos> laneList =
                    Constants.ServiceFactory.NetService.GetSortedLanes(
                        segmentId,
                        ref seg,
                        startNode,
                        LaneArrowManager.LANE_TYPES,
                        LaneArrowManager.VEHICLE_TYPES,
                        true
                        );

                //TODO: code for left hand drive
                //TODO: code for bendy avenue.
                // ban left turns and use of FR arrow where applicable.
                for (int i = 0; i < laneList.Count; ++i) {
                    LaneArrowManager.Instance.SetLaneArrows(
                        laneList[i].laneId,
                        LaneArrows.Forward);
                }
                if (laneList.Count > 0 && n_right > 0) {
                    LanePos righMostLane = laneList[laneList.Count - 1];
                    LaneArrowManager.Instance.SetLaneArrows(righMostLane.laneId, LaneArrows.ForwardRight);
                }
            }

            private static void FixMinorSegment(ushort segmentId, ushort nodeId) {
                ref NetSegment seg = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
                bool startNode = seg.m_startNode == nodeId;
                TrafficPriorityManager.Instance.SetPrioritySign(segmentId, startNode, PriorityType.Yield);

                // direction of target lanes
                int n_right = LaneArrowManager.SeparateTurningLanes.CountTargetLanesTowardDirection(segmentId, nodeId, ArrowDirection.Right);
                int n_left = LaneArrowManager.SeparateTurningLanes.CountTargetLanesTowardDirection(segmentId, nodeId, ArrowDirection.Left);
                int n_forward = LaneArrowManager.SeparateTurningLanes.CountTargetLanesTowardDirection(segmentId, nodeId, ArrowDirection.Forward);

                IList<LanePos> laneList =
                    Constants.ServiceFactory.NetService.GetSortedLanes(
                        segmentId,
                        ref seg,
                        startNode,
                        LaneArrowManager.LANE_TYPES,
                        LaneArrowManager.VEHICLE_TYPES,
                        true
                        );

                // TODO: add code for bend roads
                // TODO: add code for LHD
                // only right turn
                for (int i = 0; i < laneList.Count; ++i) {
                    LaneArrowManager.Instance.SetLaneArrows(
                        laneList[i].laneId,
                        LaneArrows.Right);
                }
            }

            public static void Fix(ushort nodeId) {
                if (nodeId == 0) {
                    return;
                }
                ref NetNode node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];

                // a list of segments attached to node arranged by size
                List<ushort> seglist = new List<ushort>();
                AddSeg_helper(seglist, node.m_segment0);
                AddSeg_helper(seglist, node.m_segment1);
                AddSeg_helper(seglist, node.m_segment2);
                AddSeg_helper(seglist, node.m_segment3);
                AddSeg_helper(seglist, node.m_segment4);
                AddSeg_helper(seglist, node.m_segment5);
                AddSeg_helper(seglist, node.m_segment6);
                AddSeg_helper(seglist, node.m_segment7);
                if(seglist.Count < 3) {
                    // this is not a junctiuon
                    return;
                }
                seglist.Sort(CompareSegments);
                if(CompareSegments(seglist[0], seglist[2]) == 0){
                    // all roads connected to the junction are equal.
                    return;
                }
                if(IsOneWay(seglist[0],nodeId) || IsOneWay(seglist[1], nodeId)){
                    // the rules do not apply to oneway main road.
                    return;
                }

                Constants.ManagerFactory.TrafficLightManager.HasTrafficLight(nodeId, ref node);
                for (int i = 0; i < seglist.Count; ++i) {
                    if(i < 2) {
                        FixMajorSegment(seglist[i], nodeId);
                    } else {
                        FixMinorSegment(seglist[i], nodeId);
                    }
                } //end for
            } // end method
        } // end class

        public override void OnPrimaryClickOverlay() {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
                if (HoveredSegmentId == 0) {
                    return;
                }

                SelectedNodeId = 0;

                var primaryPrioType = PriorityType.None;
                var secondaryPrioType = PriorityType.None;

                switch (massEditMode) {
                    case PrioritySignsMassEditMode.MainYield: {
                        primaryPrioType = PriorityType.Main;
                        secondaryPrioType = PriorityType.Yield;
                        break;
                    }

                    case PrioritySignsMassEditMode.MainStop: {
                        primaryPrioType = PriorityType.Main;
                        secondaryPrioType = PriorityType.Stop;
                        break;
                    }

                    case PrioritySignsMassEditMode.YieldMain: {
                        primaryPrioType = PriorityType.Yield;
                        secondaryPrioType = PriorityType.Main;
                        break;
                    }

                    case PrioritySignsMassEditMode.StopMain: {
                        primaryPrioType = PriorityType.Stop;
                        secondaryPrioType = PriorityType.Main;
                        break;
                    }
                }

                IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

                bool VisitorFun(SegmentVisitData data) {
                    foreach (bool startNode in Constants.ALL_BOOL) {
                        TrafficPriorityManager.Instance.SetPrioritySign(
                            data.CurSeg.segmentId,
                            startNode,
                            primaryPrioType);
                        ushort nodeId = Constants.ServiceFactory.NetService.GetSegmentNodeId(
                            data.CurSeg.segmentId,
                            startNode);
                        ExtSegmentEnd curEnd = segEndMan.ExtSegmentEnds[
                            segEndMan.GetIndex(data.CurSeg.segmentId,startNode)];

                        for (int i = 0; i < 8; ++i) {
                            ushort otherSegmentId = Singleton<NetManager>.instance.m_nodes
                                                                         .m_buffer[nodeId]
                                                                         .GetSegment(i);

                            if (otherSegmentId == 0 || otherSegmentId == data.CurSeg.segmentId) {
                                continue;
                            }

                            ArrowDirection dir = segEndMan.GetDirection(
                                ref curEnd,
                                otherSegmentId);

                            if (dir != ArrowDirection.Forward) {
                                TrafficPriorityManager.Instance.SetPrioritySign(
                                    otherSegmentId,
                                    (bool)Constants.ServiceFactory.NetService.IsStartNode(
                                        otherSegmentId,
                                        nodeId),
                                    secondaryPrioType);
                            }
                        }
                    }

                    return true;
                }

                SegmentTraverser.Traverse(
                    HoveredSegmentId,
                    TraverseDirection.AnyDirection,
                    TraverseSide.Straight,
                    SegmentStopCriterion.None,
                    VisitorFun);

                // cycle mass edit mode
                massEditMode =
                    (PrioritySignsMassEditMode)(((int)massEditMode + 1) %
                                                Enum.GetValues(typeof(PrioritySignsMassEditMode))
                                                    .GetLength(0));

                // update priority node cache
                RefreshCurrentPriorityNodeIds();
                return;
            }

            if (TrafficPriorityManager.Instance.HasNodePrioritySign(HoveredNodeId)) {
                return;
            }

            if (!MayNodeHavePrioritySigns(HoveredNodeId)) {
                return;
            }

            SelectedNodeId = HoveredNodeId;
            Log._Debug($"PrioritySignsTool.OnPrimaryClickOverlay: SelectedNodeId={SelectedNodeId}");

            bool ctrlDown = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool altDown = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (altDown) {
                return;
            } else if (ctrlDown) {
                FixPriorityJunction.Fix(HoveredNodeId);
                return;
            }

            // update priority node cache
            RefreshCurrentPriorityNodeIds();
        }

        public override void OnToolGUI(Event e) { }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            if (MainTool.GetToolController().IsInsideUI || !Cursor.visible) {
                return;
            }

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
                // draw hovered segments
                if (HoveredSegmentId != 0) {
                    Color color = MainTool.GetToolColor(Input.GetMouseButton(0), false);
                    SegmentTraverser.Traverse(
                        HoveredSegmentId,
                        TraverseDirection.AnyDirection,
                        TraverseSide.Straight,
                        SegmentStopCriterion.None,
                        data => {
                            NetTool.RenderOverlay(
                                cameraInfo,
                                ref Singleton<NetManager>.instance.m_segments.m_buffer[
                                    data.CurSeg.segmentId],
                                color,
                                color);
                            return true;
                        });
                } else {
                    massEditMode = PrioritySignsMassEditMode.MainYield;
                }

                return;
            }

            massEditMode = PrioritySignsMassEditMode.MainYield;

            if (HoveredNodeId == SelectedNodeId) {
                return;
            }

            // no highlight for existing priority node in sign mode
            if (TrafficPriorityManager.Instance.HasNodePrioritySign(HoveredNodeId)) {
                // Log._Debug($"PrioritySignsTool.RenderOverlay: HasNodePrioritySign({HoveredNodeId})=true");
                return;
            }

            if (!TrafficPriorityManager.Instance.MayNodeHavePrioritySigns(HoveredNodeId)) {
                // Log._Debug($"PrioritySignsTool.RenderOverlay: MayNodeHavePrioritySigns({HoveredNodeId})=false");
                return;
            }

            MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId, Input.GetMouseButton(0));
        }

        private void RefreshCurrentPriorityNodeIds() {
            TrafficPriorityManager tpm = TrafficPriorityManager.Instance;

            currentPriorityNodeIds.Clear();
            for (uint nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
                if (!Constants.ServiceFactory.NetService.IsNodeValid((ushort)nodeId)) {
                    continue;
                }

                if (!tpm.MayNodeHavePrioritySigns((ushort)nodeId)) {
                    continue;
                }

                if (!tpm.HasNodePrioritySign((ushort)nodeId) && nodeId != SelectedNodeId) {
                    continue;
                }

                /*if (! MainTool.IsNodeWithinViewDistance(nodeId)) {
                        continue;
                }*/

                currentPriorityNodeIds.Add((ushort)nodeId);
            }

            // Log._Debug($"PrioritySignsTool.RefreshCurrentPriorityNodeIds:
            //     currentPriorityNodeIds={string.Join(", ", currentPriorityNodeIds.Select(
            //     x => x.ToString()).ToArray())}");
        }

        public override void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) {
            if (viewOnly && !Options.prioritySignsOverlay) {
                return;
            }

            if (UIBase.GetTrafficManagerTool(false)?.GetToolMode()
                == ToolMode.JunctionRestrictions)
            {
                return;
            }

            ShowGUI(viewOnly);
        }

        private void ShowGUI(bool viewOnly) {
            try {
                IExtSegmentManager segMan = Constants.ManagerFactory.ExtSegmentManager;
                IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
                TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

                Vector3 camPos = Constants.ServiceFactory.SimulationService.CameraPosition;

                bool clicked = !viewOnly && MainTool.CheckClicked();

                ushort removedNodeId = 0;
                bool showRemoveButton = false;

                foreach (ushort nodeId in currentPriorityNodeIds) {
                    if (! Constants.ServiceFactory.NetService.IsNodeValid(nodeId)) {
                        continue;
                    }

                    if (!MainTool.IsNodeWithinViewDistance(nodeId)) {
                        continue;
                    }

                    Vector3 nodePos = default;
                    Constants.ServiceFactory.NetService.ProcessNode(
                        nodeId,
                        (ushort nId, ref NetNode node) => {
                            nodePos = node.m_position;
                            return true;
                        });

                    for (int i = 0; i < 8; ++i) {
                        ushort segmentId = 0;
                        Constants.ServiceFactory.NetService.ProcessNode(
                            nodeId,
                            (ushort nId, ref NetNode node) => {
                                segmentId = node.GetSegment(i);
                                return true;
                            });

                        if (segmentId == 0) {
                            continue;
                        }

                        bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);
                        ExtSegment seg = segMan.ExtSegments[segmentId];
                        ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)];

                        if (seg.oneWay && segEnd.outgoing) {
                            continue;
                        }

                        // calculate sign position
                        Vector3 signPos = nodePos;

                        Constants.ServiceFactory.NetService.ProcessSegment(
                            segmentId,
                            (ushort sId, ref NetSegment segment) => {
                                signPos +=
                                    10f * (startNode
                                               ? segment.m_startDirection
                                               : segment.m_endDirection);
                                return true;
                            });

                        if (!MainTool.WorldToScreenPoint(signPos, out Vector3 _)) {
                            continue;
                        }

                        // draw sign and handle input
                        PriorityType sign = prioMan.GetPrioritySign(segmentId, startNode);
                        if (viewOnly && sign == PriorityType.None) {
                            continue;
                        }

                        if (!viewOnly && sign != PriorityType.None) {
                            showRemoveButton = true;
                        }

                        if (MainTool.DrawGenericSquareOverlayTexture(
                                RoadUITextures.PrioritySignTextures[sign],
                                camPos,
                                signPos,
                                90f,
                                !viewOnly) && clicked)
                        {
                            PriorityType? newSign;
                            switch (sign) {
                                case PriorityType.Main: {
                                    newSign = PriorityType.Yield;
                                    break;
                                }

                                case PriorityType.Yield: {
                                    newSign = PriorityType.Stop;
                                    break;
                                }

                                case PriorityType.Stop: {
                                    newSign = PriorityType.Main;
                                    break;
                                }

                                // also: case PriorityType.None:
                                default: {
                                    newSign = prioMan.CountPrioritySignsAtNode(
                                                  nodeId,
                                                  PriorityType.Main) >= 2
                                                  ? PriorityType.Yield
                                                  : PriorityType.Main;
                                    break;
                                }
                            }

                            // newSign is never null here
                            SetPrioritySign(segmentId, startNode, (PriorityType)newSign);
                        } // draw sign
                    } // foreach segment end

                    if (viewOnly) {
                        continue;
                    }

                    // draw remove button and handle click
                    if (showRemoveButton
                        && MainTool.DrawHoverableSquareOverlayTexture(
                            RoadUITextures.SignRemove,
                            camPos,
                            nodePos,
                            90f)
                        && clicked)
                    {
                        prioMan.RemovePrioritySignsFromNode(nodeId);
                        Log._Debug($"PrioritySignsTool.ShowGUI: Removed priority signs from node {nodeId}");
                        removedNodeId = nodeId;
                    }
                } // foreach node

                if (removedNodeId != 0) {
                    currentPriorityNodeIds.Remove(removedNodeId);
                    SelectedNodeId = 0;
                }
            } catch (Exception e) {
                Log.Error(e.ToString());
            }
        }

        private bool SetPrioritySign(ushort segmentId, bool startNode, PriorityType sign) {
            ushort nodeId = Constants.ServiceFactory.NetService.GetSegmentNodeId(segmentId, startNode);

            // check for restrictions
            if (!MayNodeHavePrioritySigns(nodeId)) {
                Log._Debug($"PrioritySignsTool.SetPrioritySign: MayNodeHavePrioritySigns({nodeId})=false");
                return false;
            }

            bool success = TrafficPriorityManager.Instance.SetPrioritySign(segmentId, startNode, sign);
            Log._Debug($"PrioritySignsTool.SetPrioritySign: SetPrioritySign({segmentId}, " +
                       $"{startNode}, {sign})={success}");

            if (!success || (sign != PriorityType.Stop && sign != PriorityType.Yield)) {
                return success;
            }

            // make all undefined segments a main road
            Log._Debug("PrioritySignsTool.SetPrioritySign: flagging remaining segments at node " +
                       $"{nodeId} as main road.");

            for (int i = 0; i < 8; ++i) {
                ushort otherSegmentId = 0;
                Constants.ServiceFactory.NetService.ProcessNode(
                    nodeId,
                    (ushort nId, ref NetNode node) => {
                        otherSegmentId = node.GetSegment(i);
                        return true;
                    });

                if (otherSegmentId == 0 || otherSegmentId == segmentId) {
                    continue;
                }

                bool otherStartNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(otherSegmentId, nodeId);

                if (TrafficPriorityManager.Instance.GetPrioritySign(otherSegmentId, otherStartNode)
                    == PriorityType.None)
                {
                    Log._Debug("PrioritySignsTool.SetPrioritySign: setting main priority sign " +
                               $"for segment {otherSegmentId} @ {nodeId}");
                    TrafficPriorityManager.Instance.SetPrioritySign(
                        otherSegmentId,
                        otherStartNode,
                        PriorityType.Main);
                }
            }

            return success;
        }

        public override void Cleanup() {
            //TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;
            //foreach (PrioritySegment trafficSegment in prioMan.PrioritySegments) {
            //	try {
            //		trafficSegment?.Instance1?.Reset();
            //		trafficSegment?.Instance2?.Reset();
            //	} catch (Exception e) {
            //		Log.Error($"Error occured while performing PrioritySignsTool.Cleanup: {e.ToString()}");
            //	}
            //}
        }

        public override void OnActivate() {
            RefreshCurrentPriorityNodeIds();
        }

        public override void Initialize() {
            base.Initialize();
            Cleanup();

            if (Options.prioritySignsOverlay) {
                RefreshCurrentPriorityNodeIds();
            } else {
                currentPriorityNodeIds.Clear();
            }
        }

        private bool MayNodeHavePrioritySigns(ushort nodeId) {
            SetPrioritySignError reason;
            // Log._Debug($"PrioritySignsTool.MayNodeHavePrioritySigns: Checking if node {nodeId}
            //     may have priority signs.");

            if (!TrafficPriorityManager.Instance.MayNodeHavePrioritySigns(nodeId, out reason)) {
                // Log._Debug($"PrioritySignsTool.MayNodeHavePrioritySigns: Node {nodeId} does not
                //     allow priority signs: {reason}");
                if (reason == SetPrioritySignError.HasTimedLight) {
                    MainTool.ShowError(
                        Translation.TrafficLights.Get("Dialog.Text:Node has timed TL script"));
                }

                return false;
            }

            // Log._Debug($"PrioritySignsTool.MayNodeHavePrioritySigns: Node {nodeId} allows priority signs");
            return true;
        }
    }
}
