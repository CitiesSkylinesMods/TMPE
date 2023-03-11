namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Geometry;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.API.Traffic;
    using TrafficManager.Geometry;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State;
    using TrafficManager.Traffic;
    using TrafficManager.TrafficLight;
    using UnityEngine;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using TrafficManager.TrafficLight.Impl;

    public class TrafficPriorityManager
        : AbstractGeometryObservingManager,
          ICustomDataManager<List<int[]>>,
          ICustomDataManager<List<Configuration.PrioritySegment>>,
          ITrafficPriorityManager
    {
        public static readonly TrafficPriorityManager Instance = new TrafficPriorityManager();

        /// <summary>
        /// List of segments that are connected to roads with timed traffic lights or priority signs. Index: segment id
        /// </summary>
        private PrioritySegment[] PrioritySegments = null;

        private PrioritySegment[] invalidPrioritySegments;

        private TrafficPriorityManager() {
            PrioritySegments = new PrioritySegment[NetManager.MAX_SEGMENT_COUNT];
            invalidPrioritySegments = new PrioritySegment[NetManager.MAX_SEGMENT_COUNT];
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug("Priority signs:");

            for (int i = 0; i < PrioritySegments.Length; ++i) {
                if (PrioritySegments[i].IsDefault()) {
                    continue;
                }

                Log._Debug($"Segment {i}: {PrioritySegments[i]}");
            }
        }

        protected void AddInvalidPrioritySegment(ushort segmentId,
                                                 ref PrioritySegment prioritySegment) {
            invalidPrioritySegments[segmentId] = prioritySegment;
        }

        public bool MayNodeHavePrioritySigns(ushort nodeId) {
            SetPrioritySignError reason;
            return MayNodeHavePrioritySigns(nodeId, out reason);
        }

        public bool MayNodeHavePrioritySigns(ushort nodeId, out SetPrioritySignError reason) {
#if DEBUG
            bool logPriority = DebugSwitch.PriorityRules.Get()
                               && (DebugSettings.NodeId <= 0 || nodeId == DebugSettings.NodeId);
#else
            const bool logPriority = false;
#endif
            ref NetNode netNode = ref nodeId.ToNode();

            if (!netNode.m_flags.CheckFlags(
                    required: NetNode.Flags.Created | NetNode.Flags.Junction,
                    forbidden: NetNode.Flags.Deleted)) {
                reason = SetPrioritySignError.NoJunction;
                Log._DebugIf(
                    logPriority,
                    () => $"TrafficPriorityManager.MayNodeHavePrioritySigns: nodeId={nodeId}, " +
                    "result=false, reason=NoJunction");

                return false;
            }

            if (TrafficLightSimulationManager.Instance.HasTimedSimulation(nodeId)) {
                reason = SetPrioritySignError.HasTimedLight;

                Log._DebugIf(
                    logPriority,
                    () => $"TrafficPriorityManager.MayNodeHavePrioritySigns: nodeId={nodeId}, " +
                    "result=false, reason=HasTimedLight");
                return false;
            }

            // Log._Debug($"TrafficPriorityManager.MayNodeHavePrioritySigns: nodeId={nodeId}, result=true");
            reason = SetPrioritySignError.None;
            return true;
        }

        public bool MaySegmentHavePrioritySign(ushort segmentId, bool startNode) {
            SetPrioritySignError reason;
            return MaySegmentHavePrioritySign(segmentId, startNode, out reason);
        }

        public bool MaySegmentHavePrioritySign(ushort segmentId,
                                               bool startNode,
                                               out SetPrioritySignError reason) {
#if DEBUG
            bool logPriority = DebugSwitch.PriorityRules.Get() &&
                         (DebugSettings.SegmentId <= 0 || segmentId == DebugSettings.SegmentId);
#else
            const bool logPriority = false;
#endif
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                reason = SetPrioritySignError.InvalidSegment;
                Log._DebugIf(
                    logPriority,
                    () => $"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, " +
                    $"startNode={startNode}, result=false, reason=InvalidSegment");
                return false;
            }

            ushort nodeId = startNode ? netSegment.m_startNode : netSegment.m_endNode;

            if (!MayNodeHavePrioritySigns(nodeId, out reason)) {
                var reasonCopy = reason;
                Log._DebugIf(
                    logPriority,
                    () => $"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, " +
                    $"startNode={startNode}, result=false, reason={reasonCopy}");

                return false;
            }

            IExtSegmentManager segMan = Constants.ManagerFactory.ExtSegmentManager;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

            if (segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)].outgoing &&
                segMan.ExtSegments[segmentId].oneWay) {
                reason = SetPrioritySignError.NotIncoming;
                Log._DebugIf(
                    logPriority,
                    () => $"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, " +
                    $"startNode={startNode}, result=false, reason=NotIncoming");
                return false;
            }

            Log._DebugIf(
                logPriority,
                () => $"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, " +
                $"startNode={startNode}, result=true");
            reason = SetPrioritySignError.None;
            return true;
        }

        [UsedImplicitly]
        public bool MaySegmentHavePrioritySign(ushort segmentId) {
            SetPrioritySignError reason;
            return MaySegmentHavePrioritySign(segmentId, out reason);
        }

        public bool MaySegmentHavePrioritySign(ushort segmentId, out SetPrioritySignError reason) {
#if DEBUG
            bool logPriority = DebugSwitch.PriorityRules.Get()
                               && (DebugSettings.SegmentId <= 0
                                   || segmentId == DebugSettings.SegmentId);
#else
            const bool logPriority = false;
#endif
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                reason = SetPrioritySignError.InvalidSegment;
                Log._DebugIf(
                    logPriority,
                    () => $"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, " +
                    "result=false, reason=InvalidSegment");
                return false;
            }

            bool ret =
                MaySegmentHavePrioritySign(segmentId, true, out reason)
                || MaySegmentHavePrioritySign(segmentId, false, out reason);

            Log._DebugIf(
                logPriority,
                () => "TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, " +
                "result={ret}, reason={reason}");
            return ret;
        }

        public bool HasSegmentPrioritySign(ushort segmentId) {
            return !PrioritySegments[segmentId].IsDefault();
        }

        public bool HasSegmentPrioritySign(ushort segmentId, bool startNode) {
            return PrioritySegments[segmentId].HasPrioritySignAtNode(startNode);
        }

        public bool HasNodePrioritySign(ushort nodeId) {
#if DEBUG
            bool logPriority = DebugSwitch.PriorityRules.Get()
                               && (DebugSettings.NodeId <= 0 || nodeId == DebugSettings.NodeId);
#else
            const bool logPriority = false;
#endif

            var res = false;

            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    if (HasSegmentPrioritySign(segmentId, nodeId == segmentId.ToSegment().m_startNode)) {
                        res = true;
                        break;
                    }
                }
            }

            Log._DebugIf(logPriority, () => $"TrafficPriorityManager.HasNodePrioritySign: nodeId={nodeId}, result={res}");

            return res;
        }

        public bool SetPrioritySign(ushort segmentId, bool startNode, PriorityType type) {
            return SetPrioritySign(segmentId, startNode, type, out SetPrioritySignError _);
        }

        public bool SetPrioritySign(ushort segmentId,
                                    bool startNode,
                                    PriorityType type,
                                    out SetPrioritySignError reason) {
#if DEBUG
            bool logPriority = DebugSwitch.PriorityRules.Get()
                               && (DebugSettings.SegmentId <= 0
                                   || segmentId == DebugSettings.SegmentId);
#else
            const bool logPriority = false;
#endif
            bool ret = true;
            reason = SetPrioritySignError.None;

            if (type != PriorityType.None &&
                !MaySegmentHavePrioritySign(segmentId, startNode, out reason)) {
                var reasonCopy = reason;
                Log._DebugIf(
                    logPriority,
                    () => $"TrafficPriorityManager.SetPrioritySign: Segment {segmentId} " +
                    $"@ {startNode} may not have a priority sign: {reasonCopy}");

                ret = false;
                type = PriorityType.None;
            }

            ref NetSegment netSegment = ref segmentId.ToSegment();
            ushort nodeId = startNode ? netSegment.m_startNode : netSegment.m_endNode;
            if (type != PriorityType.None) {
                TrafficLightManager.Instance.RemoveTrafficLight(nodeId, ref nodeId.ToNode());
            }

            if (startNode) {
                PrioritySegments[segmentId].startType = type;
            } else {
                PrioritySegments[segmentId].endType = type;
            }

            SegmentEndManager.Instance.UpdateSegmentEnd(segmentId, startNode);
            if (logPriority) {
                Log._Debug(
                    $"TrafficPriorityManager.SetPrioritySign: segmentId={segmentId}, " +
                    $"startNode={startNode}, type={type}, result={ret}, reason={reason}");
            }

            Notifier.Instance.OnNodeModified(nodeId, this);
            return ret;
        }

        public void RemovePrioritySignsFromNode(ushort nodeId) {
#if DEBUG
            bool logPriority = DebugSwitch.PriorityRules.Get()
                               && (DebugSettings.NodeId <= 0 || nodeId == DebugSettings.NodeId);
#else
            const bool logPriority = false;
#endif
            Log._DebugIf(
                logPriority,
                () => $"TrafficPriorityManager.RemovePrioritySignsFromNode: nodeId={nodeId}");

            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    RemovePrioritySignFromSegmentEnd(segmentId, nodeId == segmentId.ToSegment().m_startNode);
                }
            }
        }

        public void RemovePrioritySignsFromSegment(ushort segmentId) {
#if DEBUG
            bool logPriority = DebugSwitch.PriorityRules.Get()
                               && (DebugSettings.SegmentId <= 0
                                   || segmentId == DebugSettings.SegmentId);
#else
            const bool logPriority = false;
#endif
            Log._DebugIf(
                logPriority,
                () => $"TrafficPriorityManager.RemovePrioritySignsFromSegment: segmentId={segmentId}");

            RemovePrioritySignFromSegmentEnd(segmentId, true);
            RemovePrioritySignFromSegmentEnd(segmentId, false);
        }

        public void RemovePrioritySignFromSegmentEnd(ushort segmentId, bool startNode) {
#if DEBUG
            bool logPriority = DebugSwitch.PriorityRules.Get()
                               && (DebugSettings.SegmentId <= 0
                                   || segmentId == DebugSettings.SegmentId);
#else
            const bool logPriority = false;
#endif
            Log._DebugIf(
                logPriority,
                () => $"TrafficPriorityManager.RemovePrioritySignFromSegment: segmentId={segmentId}, " +
                      $"startNode={startNode}");

            if (startNode) {
                PrioritySegments[segmentId].startType = PriorityType.None;
            } else {
                PrioritySegments[segmentId].endType = PriorityType.None;
            }

            SegmentEndManager.Instance.UpdateSegmentEnd(segmentId, startNode);
            Notifier.Instance.OnNodeModified(segmentId.ToSegment().GetNodeId(startNode), this);
        }

        public PriorityType GetPrioritySign(ushort segmentId, bool startNode) {
            return startNode
                       ? PrioritySegments[segmentId].startType
                       : PrioritySegments[segmentId].endType;
        }

        public byte CountPrioritySignsAtNode(ushort nodeId, PriorityType sign) {
#if DEBUG
            bool logPriority = DebugSwitch.PriorityRules.Get()
                               && (DebugSettings.NodeId <= 0 || nodeId == DebugSettings.NodeId);
#else
            const bool logPriority = false;
#endif

            byte ret = 0;

            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    if (GetPrioritySign(segmentId, segmentId.ToSegment().m_startNode == nodeId) == sign) {
                        ++ret;
                    }
                }
            }

            Log._DebugIf(
                logPriority,
                () => $"TrafficPriorityManager.CountPrioritySignsAtNode: nodeId={nodeId}, " +
                      $"sign={sign}, result={ret}");

            return ret;
        }

        public bool HasPriority(ushort vehicleId,
                                ref Vehicle vehicle,
                                ref PathUnit.Position curPos,
                                ref ExtSegmentEnd curEnd,
                                ushort transitNodeId,
                                bool startNode,
                                ref PathUnit.Position nextPos,
                                ref NetNode transitNode)
        {
            IExtSegmentEndManager extSegEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

            // ISegmentEndGeometry endGeo = SegmentGeometry.Get(curPos.m_segment)?.GetEnd(startNode);
            // if (Constants.ManagerFactory.ExtSegmentManager == null) {
            // #if DEBUG
            //    Log.Warning(
            //        $"TrafficPriorityManager.HasPriority({vehicleId}): No segment end geometry
            //         found for segment {curPos.m_segment} @ {startNode}");
            //    return true;
            // #endif
            // }

            // SegmentEnd end = SegmentEndManager.Instance.GetSegmentEnd(curPos.m_segment, startNode);
            // if (end == null) {
            // #if DEBUG
            //    Log.Warning(
            //        $"TrafficPriorityManager.HasPriority({vehicleId}): No segment end found for
            //         segment {curPos.m_segment} @ {startNode}");
            //    return true;
            // #endif
            // }
            //
            // ushort transitNodeId = end.NodeId;
#if DEBUG
            bool logPriority = DebugSwitch.PriorityRules.Get()
                               && (DebugSettings.NodeId <= 0 || transitNodeId == DebugSettings.NodeId);
#else
            const bool logPriority = false;
#endif
            if (logPriority) {
                Log._DebugFormat(
                    "TrafficPriorityManager.HasPriority({0}): Checking vehicle {1} at node {2}. " +
                    "Coming from seg. {3}, start {4}, lane {5}, going to seg. {6}, lane {7}",
                    vehicleId,
                    vehicleId,
                    transitNodeId,
                    curPos.m_segment,
                    startNode,
                    curPos.m_lane,
                    nextPos.m_segment,
                    nextPos.m_lane);
            }

            if ((vehicle.m_flags & Vehicle.Flags.Spawned) == 0) {
                if (logPriority) {
                    ushort vehicleIdCopy = vehicleId;
                    Log.Warning($"TrafficPriorityManager.HasPriority({vehicleIdCopy}): Vehicle is not spawned.");
                }
                return true;
            }

            if ((vehicle.m_flags & Vehicle.Flags.Emergency2) != 0) {
                // target vehicle is on emergency
                Log._DebugIf(
                    logPriority,
                    () => $"TrafficPriorityManager.HasPriority({vehicleId}): Vehicle is on emergency.");
                return true;
            }

            if (vehicle.Info.m_vehicleType == VehicleInfo.VehicleType.Monorail) {
                // monorails do not obey priority signs
                Log._DebugIf(
                    logPriority,
                    () => $"TrafficPriorityManager.HasPriority({vehicleId}): Vehicle is a monorail.");
                return true;
            }

            PriorityType curSign = GetPrioritySign(curPos.m_segment, startNode);
            if (curSign == PriorityType.None) {
                if (logPriority) {
                    Log._Debug(
                        $"TrafficPriorityManager.HasPriority({vehicleId}): Sign is None @ seg. " +
                        $"{curPos.m_segment}, start {startNode} -> setting to Main");
                }

                curSign = PriorityType.Main;
            }

            bool onMain = curSign == PriorityType.Main;

            // if (!Services.VehicleService.IsVehicleValid(vehicleId)) {
            //    curEnd.RequestCleanup();
            //    return true;
            // }

            // calculate approx. time after which the transit node will be reached
            float targetTimeToTransitNode = float.NaN;
            if (SavedGameOptions.Instance.simulationAccuracy >= SimulationAccuracy.High) {
                Vector3 targetToNode = transitNode.m_position - vehicle.GetLastFramePosition();
                Vector3 targetVel = vehicle.GetLastFrameVelocity();
                float targetSpeed = targetVel.magnitude;
                float targetDistanceToTransitNode = targetToNode.magnitude;

                targetTimeToTransitNode = targetSpeed > 0
                                              ? targetDistanceToTransitNode / targetSpeed
                                              : 0;
            }

            Log._DebugIf(
                logPriority,
                () => $"TrafficPriorityManager.HasPriority({vehicleId}): estimated target time to " +
                $"transit node {transitNodeId} is {targetTimeToTransitNode} for vehicle {vehicleId}");

            // absolute target direction of target vehicle
            ArrowDirection targetToDir = extSegEndMan.GetDirection(ref curEnd, nextPos.m_segment);

            // iterate over all cars approaching the transit node and check if the target vehicle should be prioritized
            ExtVehicleManager vehStateManager = ExtVehicleManager.Instance;
            CustomSegmentLightsManager segLightsManager = CustomSegmentLightsManager.Instance;

            for (int i = 0; i < 8; ++i) {
                ushort otherSegmentId = transitNode.GetSegment(i);
                if (otherSegmentId == curEnd.segmentId || otherSegmentId == 0) {
                    continue;
                }

                bool otherStartNode = otherSegmentId.ToSegment().IsStartNode(transitNodeId);

                // ISegmentEnd incomingEnd =
                //    SegmentEndManager.Instance.GetSegmentEnd(otherSegmentId, otherStartNode);
                // if (incomingEnd == null) {
                //    Log.ErrorIf(
                //        logPriority,
                //        () => $"TrafficPriorityManager.HasPriority({vehicleId}): No segment end " +
                //        $"found for other segment {otherSegmentId} @ {otherStartNode}");
                //    return true;
                // }

                CustomSegmentLights otherLights = null;
                if (SavedGameOptions.Instance.trafficLightPriorityRules) {
                    otherLights = segLightsManager.GetSegmentLights(
                        otherSegmentId,
                        otherStartNode,
                        false);
                }

                PriorityType otherSign = GetPrioritySign(otherSegmentId, otherStartNode);
                if (otherSign == PriorityType.None) {
                    otherSign = PriorityType.Main;
                    // continue;
                }

                bool incomingOnMain = otherSign == PriorityType.Main;

                // absolute incoming direction of incoming vehicle
                ArrowDirection incomingFromDir = extSegEndMan.GetDirection( ref curEnd, otherSegmentId);

                Log._DebugIf(
                    logPriority,
                    () => $"TrafficPriorityManager.HasPriority({vehicleId}): checking other " +
                    $"segment {otherSegmentId} @ {transitNodeId}");

                int otherEndIndex = extSegEndMan.GetIndex(otherSegmentId, otherStartNode);
                ushort incomingVehicleId = extSegEndMan.ExtSegmentEnds[otherEndIndex].firstVehicleId;
                int numIter = 0;
                var maxVehicleCount = VehicleManager.instance.m_vehicles.m_buffer.Length;

                while (incomingVehicleId != 0) {
                    Log._DebugIf(
                        logPriority,
                        () => $"\nTrafficPriorityManager.HasPriority({vehicleId}): checking other " +
                        $"vehicle {incomingVehicleId} @ seg. {otherSegmentId}");

                    if (IsConflictingVehicle(
                        logPriority,
                        transitNode.m_position,
                        targetTimeToTransitNode,
                        vehicleId,
                        ref vehicle,
                        ref curPos,
                        transitNodeId,
                        startNode,
                        ref nextPos,
                        onMain,
                        ref curEnd,
                        targetToDir,
                        incomingVehicleId,
                        ref incomingVehicleId.ToVehicle(),
                        ref vehStateManager.ExtVehicles[incomingVehicleId],
                        incomingOnMain,
                        ref extSegEndMan.ExtSegmentEnds[otherEndIndex],
                        otherLights,
                        incomingFromDir))
                    {
                        Log._DebugIf(
                            logPriority,
                            () => $"TrafficPriorityManager.HasPriority({vehicleId}): incoming " +
                            $"vehicle {incomingVehicleId} is conflicting.");
                        return false;
                    }

                    // check next incoming vehicle
                    incomingVehicleId = vehStateManager.ExtVehicles[incomingVehicleId].nextVehicleIdOnSegment;

                    if (++numIter > maxVehicleCount) {
                        CODebugBase<LogChannel>.Error(
                            LogChannel.Core,
                            $"Invalid list detected!\n{Environment.StackTrace}");
                        break;
                    }
                }
            }
            Log._DebugIf(
                logPriority,
                () => $"TrafficPriorityManager.HasPriority({vehicleId}): No conflicting incoming vehicles found.");
            return true;
        }

        private bool IsConflictingVehicle(bool logPriority,
                                          Vector3 transitNodePos,
                                          float targetTimeToTransitNode,
                                          ushort vehicleId,
                                          ref Vehicle vehicle,
                                          ref PathUnit.Position curPos,
                                          ushort transitNodeId,
                                          bool startNode,
                                          ref PathUnit.Position nextPos,
                                          bool onMain,
                                          ref ExtSegmentEnd curEnd,
                                          ArrowDirection targetToDir,
                                          ushort incomingVehicleId,
                                          ref Vehicle incomingVehicle,
                                          ref ExtVehicle incomingState,
                                          bool incomingOnMain,
                                          ref ExtSegmentEnd incomingEnd,
                                          CustomSegmentLights incomingLights,
                                          ArrowDirection incomingFromDir)
        {
            if (logPriority) {
                Log._DebugFormat(
                    "TrafficPriorityManager.IsConflictingVehicle({0}, {1}): Checking against other " +
                    "vehicle {2}.\nTrafficPriorityManager.IsConflictingVehicle({3}, {4}): TARGET is " +
                    "coming from seg. {5}, start {6}, lane {7}, going to seg. {8}, lane {9}\n" +
                    "TrafficPriorityManager.IsConflictingVehicle({10}, {11}): INCOMING is coming from " +
                    "seg. {12}, start {13}, lane {14}, going to seg. {15}, lane {16}\nincoming state: {17}",
                    vehicleId,
                    incomingVehicleId,
                    incomingVehicleId,
                    vehicleId,
                    incomingVehicleId,
                    curPos.m_segment,
                    startNode,
                    curPos.m_lane,
                    nextPos.m_segment,
                    nextPos.m_lane,
                    vehicleId,
                    incomingVehicleId,
                    incomingState.currentSegmentId,
                    incomingState.currentStartNode,
                    incomingState.currentLaneIndex,
                    incomingState.nextSegmentId,
                    incomingState.nextLaneIndex,
                    incomingState);
            }

            if ((incomingState.flags & ExtVehicleFlags.Spawned) == ExtVehicleFlags.None) {
                if (logPriority) {
                    Log.Warning($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, " +
                              $"{incomingVehicleId}): Incoming vehicle is not spawned.");
                }
                return false;
            }

            if (incomingVehicle.Info.m_vehicleType == VehicleInfo.VehicleType.Monorail) {
                // monorails and cars do not collide
                Log._DebugIf(
                    logPriority,
                    () => $"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, " +
                    $"{incomingVehicleId}): Incoming vehicle is a monorail.");
                return false;
            }

            // relative target direction of incoming vehicle
            ArrowDirection incomingToRelDir =
                Constants.ManagerFactory.ExtSegmentEndManager.GetDirection(
                    ref incomingEnd,
                    incomingState.nextSegmentId);

            if (incomingLights != null) {
                CustomSegmentLight incomingLight = incomingLights.GetCustomLight(incomingState.currentLaneIndex);
                if (logPriority) {
                    Log._DebugFormat(
                        "TrafficPriorityManager.IsConflictingVehicle({0}, {1}): Detected traffic " +
                        "light. Incoming state ({2}): {3}",
                        vehicleId,
                        incomingVehicleId,
                        incomingToRelDir,
                        incomingLight.GetLightState(incomingToRelDir));
                }

                if (incomingLight.IsRed(incomingToRelDir)) {
                    Log._DebugIf(
                        logPriority,
                        () => $"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, " +
                        $"{incomingVehicleId}): Incoming traffic light is red.");
                    return false;
                }
            }

            if (incomingState.junctionTransitState != VehicleJunctionTransitState.None) {
                Vector3 incomingVel = incomingVehicle.GetLastFrameVelocity();
                bool incomingStateChangedRecently =
                    Constants.ManagerFactory.ExtVehicleManager.IsJunctionTransitStateNew(
                        ref incomingState);

                if (incomingState.junctionTransitState == VehicleJunctionTransitState.Approach
                    || incomingState.junctionTransitState == VehicleJunctionTransitState.Leave)
                {
                    if ((incomingState.vehicleType & API.Traffic.Enums.ExtVehicleType.RoadVehicle) !=
                        API.Traffic.Enums.ExtVehicleType.None)
                    {
                        float incomingSqrSpeed = incomingVel.sqrMagnitude;

                        if (!incomingStateChangedRecently && incomingSqrSpeed
                            <= GlobalConfig.Instance.PriorityRules.MaxStopVelocity)
                        {
                            Log._DebugIf(
                                logPriority,
                                () => $"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, " +
                                $"{incomingVehicleId}): Incoming {incomingVehicleId} is " +
                                "LEAVING or APPROACHING but not moving. -> BLOCKED");

                            Constants.ManagerFactory.ExtVehicleManager.SetJunctionTransitState(
                                ref incomingState,
                                VehicleJunctionTransitState.Blocked);

                            incomingStateChangedRecently = true;
                            return false;
                        }
                    }

                    // incoming vehicle is (1) entering the junction or (2) leaving
                    Vector3 incomingPos = incomingVehicle.GetLastFramePosition();
                    Vector3 incomingToNode = transitNodePos - incomingPos;

                    // check if incoming vehicle moves towards node
                    float dot = Vector3.Dot(incomingToNode, incomingVel);

                    if (dot <= 0) {
                        Log._DebugIf(
                            logPriority,
                            () => $"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, " +
                            $"{incomingVehicleId}): Incoming {incomingVehicleId} is moving away from " +
                            $"the transit node ({dot}). *IGNORING*");
                        return false;
                    }

                    Log._DebugIf(
                        logPriority,
                        () => $"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, " +
                        $"{incomingVehicleId}): Incoming {incomingVehicleId} is moving towards the " +
                        $"transit node ({dot}). Distance: {incomingToNode.magnitude}");

                    // check if estimated approach time of the incoming vehicle is within bounds
                    // (only if incoming vehicle is far enough away from the junction and target
                    // vehicle is moving)
                    if (!Single.IsInfinity(targetTimeToTransitNode)
                        && !Single.IsNaN(targetTimeToTransitNode)
                        && incomingToNode.sqrMagnitude > GlobalConfig.Instance.PriorityRules.MaxPriorityCheckSqrDist)
                    {
                        // check speeds
                        float incomingSpeed = incomingVel.magnitude;
                        float incomingDistanceToTransitNode = incomingToNode.magnitude;
                        float incomingTimeToTransitNode;

                        incomingTimeToTransitNode =
                            incomingSpeed > 0
                                ? incomingDistanceToTransitNode / incomingSpeed
                                : Single.PositiveInfinity;

                        float timeDiff = Mathf.Abs(incomingTimeToTransitNode - targetTimeToTransitNode);

                        if (timeDiff > GlobalConfig.Instance.PriorityRules.MaxPriorityApproachTime) {
                            if (logPriority) {
                                Log._DebugFormat(
                                    "TrafficPriorityManager.IsConflictingVehicle({0}, {1}): Incoming " +
                                    "{2} needs {3} time units to get to the node where target needs {4} " +
                                    "time units (diff = {5}). Difference to large. *IGNORING*",
                                    vehicleId,
                                    incomingVehicleId,
                                    incomingVehicleId,
                                    incomingTimeToTransitNode,
                                    targetTimeToTransitNode,
                                    timeDiff);
                            }

                            return false;
                        }

                        if (logPriority) {
                            Log._DebugFormat(
                                "TrafficPriorityManager.IsConflictingVehicle({0}, {1}): Incoming {2} " +
                                "needs {3} time units to get to the node where target needs {4} time " +
                                "units (diff = {5}). Difference within bounds. Priority check required.",
                                vehicleId,
                                incomingVehicleId,
                                incomingVehicleId,
                                incomingTimeToTransitNode,
                                targetTimeToTransitNode,
                                timeDiff);
                        }
                    } else {
                        Log._DebugIf(
                            logPriority,
                            () => $"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, " +
                            $"{incomingVehicleId}): Incoming is stopped.");
                    }
                }

                if (!incomingStateChangedRecently
                    && (incomingState.junctionTransitState == VehicleJunctionTransitState.Blocked)) {
                    // || (incomingState.JunctionTransitState == VehicleJunctionTransitState.Stop
                    // && vehicleId < incomingVehicleId))) {
                    if (logPriority) {
                        Log._DebugFormat(
                            "TrafficPriorityManager.IsConflictingVehicle({0}, {1}): Incoming {2} is " +
                            "BLOCKED and has waited a bit or is STOP and targetVehicleId {3} " +
                            "< incomingVehicleId {4}. *IGNORING*",
                            vehicleId,
                            incomingVehicleId,
                            incomingVehicleId,
                            vehicleId,
                            incomingVehicleId);
                    }

                    // incoming vehicle waits because the junction is blocked or it does not get
                    // priority and we waited for some time. Allow target vehicle to enter
                    // the junciton.
                    return false;
                }

                // check priority rules
                if (HasVehiclePriority(
                    logPriority,
                    vehicleId,
                    ref vehicle,
                    ref curPos,
                    transitNodeId,
                    startNode,
                    ref nextPos,
                    onMain,
                    targetToDir,
                    incomingVehicleId,
                    ref incomingVehicle,
                    ref incomingState,
                    incomingOnMain,
                    incomingFromDir,
                    incomingToRelDir))
                {
                    Log._DebugIf(
                        logPriority,
                        () => $"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, " +
                        $"{incomingVehicleId}): Incoming {incomingVehicleId} is not conflicting.");
                    return false;
                }

                Log._DebugIf(
                    logPriority,
                    () => $"==========> TrafficPriorityManager.IsConflictingVehicle({vehicleId}, " +
                    $"{incomingVehicleId}): Incoming {incomingVehicleId} IS conflicting.");
                return true;
            }

            if (logPriority) {
                Log._Debug(
                    $"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, " +
                    $"{incomingVehicleId}): Incoming {incomingVehicleId} (main) is not conflicting " +
                    $"({incomingState.junctionTransitState}).");
            }

            return false;
        }

        /// <summary>
        /// Implements priority checking for two vehicles approaching or waiting at a junction.
        /// </summary>
        /// <param name="logPriority"></param>
        /// <param name="transitNodeId">id of the junction</param>
        /// <param name="vehicleId">target vehicle for which priority is being checked</param>
        /// <param name="vehicle">target vehicle data</param>
        /// <param name="targetCurPos">target vehicle current path position</param>
        /// <param name="targetNextPos">target vehicle next path position</param>
        /// <param name="onMain">true if the target vehicle is coming from a main road</param>
        /// <param name="incomingVehicleId">possibly conflicting incoming vehicle</param>
        /// <param name="incomingCurPos">incoming vehicle current path position</param>
        /// <param name="incomingNextPos">incoming vehicle next path position</param>
        /// <param name="incomingOnMain">true if the incoming vehicle is coming from a main road</param>
        /// <returns>true if the target vehicle has priority, false otherwise</returns>
        private bool HasVehiclePriority(bool logPriority,
                                        ushort vehicleId,
                                        ref Vehicle vehicle,
                                        ref PathUnit.Position curPos,
                                        ushort transitNodeId,
                                        bool startNode,
                                        ref PathUnit.Position nextPos,
                                        bool onMain,
                                        ArrowDirection targetToDir,
                                        ushort incomingVehicleId,
                                        ref Vehicle incomingVehicle,
                                        ref ExtVehicle incomingState,
                                        bool incomingOnMain,
                                        ArrowDirection incomingFromDir,
                                        ArrowDirection incomingToRelDir) {
            if (logPriority) {
                Log._DebugFormat(
                    "\n  TrafficPriorityManager.HasVehiclePriority({0}, {1}): " +
                    "*** Checking if vehicle {2} (main road = {3}) @ (seg. {4}, start {5}, lane {6}) " +
                    "-> (seg. {7}, lane {8}) has priority over {9} (main road = {10}) " +
                    "@ (seg. {11}, start {12}, lane {13}) -> (seg. {14}, lane {15}).",
                    vehicleId,
                    incomingVehicleId,
                    vehicleId,
                    onMain,
                    curPos.m_segment,
                    startNode,
                    curPos.m_lane,
                    nextPos.m_segment,
                    nextPos.m_lane,
                    incomingVehicleId,
                    incomingOnMain,
                    incomingState.currentSegmentId,
                    incomingState.currentStartNode,
                    incomingState.currentLaneIndex,
                    incomingState.nextSegmentId,
                    incomingState.nextLaneIndex);
            }

            if (targetToDir == ArrowDirection.None || incomingFromDir == ArrowDirection.None ||
                incomingToRelDir == ArrowDirection.None) {
                if (logPriority) {
                    Log._DebugFormat(
                        "  TrafficPriorityManager.HasVehiclePriority({0}, {1}): Invalid directions " +
                        "given: targetToDir={2}, incomingFromDir={3}, incomingToRelDir={4}",
                        vehicleId,
                        incomingVehicleId,
                        targetToDir,
                        incomingFromDir,
                        incomingToRelDir);
                }

                return true;
            }

            if (curPos.m_segment == incomingState.currentSegmentId) {
                // both vehicles are coming from the same segment. do not apply priority
                // rules in this case.
                Log._DebugIf(
                    logPriority,
                    () => $"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, " +
                    $"{incomingVehicleId}): Both vehicles come from the same segment. *IGNORING*");

                return true;
            }

            //       FORWARD
            //          |
            //          |
            // LEFT --- + --- RIGHT
            //          |
            //          |
            //        TURN
            //
            // - Target car is always coming from TURN.
            // - Target car is going to `targetToDir` (relative to TURN).
            // - Incoming car is coming from `incomingFromDir` (relative to TURN).
            // - Incoming car is going to `incomingToRelDir` (relative to `incomingFromDir`).
            if (logPriority) {
                Log._DebugFormat(
                    "  TrafficPriorityManager.HasVehiclePriority({0}, {1}): targetToDir: {2}, " +
                    "incomingFromDir: {3}, incomingToRelDir: {4}",
                    vehicleId,
                    incomingVehicleId,
                    targetToDir,
                    incomingFromDir,
                    incomingToRelDir);
            }

            if (Shortcuts.LHT) {
                // mirror situation if traffic drives on left
                targetToDir = ArrowDirectionUtil.InvertLeftRight(targetToDir);
                incomingFromDir = ArrowDirectionUtil.InvertLeftRight(incomingFromDir);
                incomingToRelDir = ArrowDirectionUtil.InvertLeftRight(incomingToRelDir);

                if (logPriority) {
                    Log._DebugFormat(
                        "  TrafficPriorityManager.HasVehiclePriority({0}, {1}): LHT! targetToDir: {2}, " +
                        "incomingFromDir: {3}, incomingToRelDir: {4}",
                        vehicleId,
                        incomingVehicleId,
                        targetToDir,
                        incomingFromDir,
                        incomingToRelDir);
                }
            }

            if (logPriority) {
                Log._DebugFormat(
                    "  TrafficPriorityManager.HasVehiclePriority({0}, {1}): targetToDir={2}, " +
                    "incomingFromDir={3}, incomingToRelDir={4}",
                    vehicleId,
                    incomingVehicleId,
                    targetToDir,
                    incomingFromDir,
                    incomingToRelDir);
            }

            //---------------------------
            // (1) COLLISION DETECTION
            //---------------------------
            // bool sameTargets = nextPos.m_segment == incomingState.nextSegmentId;
            bool wouldCollide = DetectCollision(
                logPriority,
                ref curPos,
                transitNodeId,
                startNode,
                ref nextPos,
                ref incomingState,
                targetToDir,
                incomingFromDir,
                incomingToRelDir,
                vehicleId,
                incomingVehicleId);

            if (!wouldCollide) {
                // both vehicles would not collide. allow both to pass.
                Log._DebugIf(
                    logPriority,
                    () => $"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): " +
                    $"Cars {vehicleId} and {incomingVehicleId} would not collide. NO CONFLICT.");

                return true;
            }

            // -> vehicles would collide
            Log._DebugIf(
                logPriority,
                () => $"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): " +
                $"Cars {vehicleId} and {incomingVehicleId} would collide. Checking priority rules.");

            //---------------------------
            // (2) CHECK PRIORITY RULES
            //---------------------------
            bool ret;
            if ((!onMain && !incomingOnMain) || (onMain && incomingOnMain)) {
                // both vehicles are on the same priority level: check common priority rules
                // (left yields to right, left turning vehicles yield to others)
                ret = HasPriorityOnSameLevel(
                    logPriority,
                    targetToDir,
                    incomingFromDir,
                    incomingToRelDir,
                    vehicleId,
                    incomingVehicleId);

                Log._DebugIf(
                    logPriority,
                    () => $"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): " +
                    $"Cars {vehicleId} and {incomingVehicleId} are on the same priority level. " +
                    $"Checking common priority rules. ret={ret}");
            } else {
                // both vehicles are on a different priority level: prioritize vehicle on main road
                ret = onMain;
                Log._DebugIf(
                    logPriority,
                    () => $"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): " +
                    $"Cars {vehicleId} and {incomingVehicleId} are on a different priority. " +
                    $"Prioritizing vehicle on main road. ret={ret}");
            }

            if (ret) {
                // check if the incoming vehicle is leaving (though the target vehicle has priority)
                bool incomingIsLeaving = incomingState.junctionTransitState == VehicleJunctionTransitState.Leave;
                Log._DebugIf(
                    logPriority,
                    () => $"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): " +
                    $">>> Car {vehicleId} has priority over {incomingVehicleId}. incomingIsLeaving={incomingIsLeaving}");
                return !incomingIsLeaving;
            }

            // the target vehicle must wait
            Log._DebugIf(
                logPriority,
                () => $"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): " +
                $">>> Car {vehicleId} must wait for {incomingVehicleId}. returning FALSE.");

            return false;
        }

        /// <summary>
        /// Checks if two vehicles are on a collision course.
        /// </summary>
        /// <param name="logPriority">enable debugging</param>
        /// <param name="transitNodeId">junction node</param>
        /// <param name="incomingState">incoming vehicle state</param>
        /// <param name="targetToDir">absolute target vehicle destination direction</param>
        /// <param name="incomingFromDir">absolute incoming vehicle source direction</param>
        /// <param name="incomingToRelDir">relative incoming vehicle destination direction</param>
        /// <param name="vehicleId">(optional) target vehicle id</param>
        /// <param name="incomingVehicleId">(optional) incoming vehicle id</param>
        /// <returns>true if both vehicles are on a collision course, false otherwise</returns>
        public bool DetectCollision(bool logPriority,
                                    ref PathUnit.Position curPos,
                                    ushort transitNodeId,
                                    bool startNode,
                                    ref PathUnit.Position nextPos,
                                    ref ExtVehicle incomingState,
                                    ArrowDirection targetToDir,
                                    ArrowDirection incomingFromDir,
                                    ArrowDirection incomingToRelDir,
                                    ushort vehicleId = 0,
                                    ushort incomingVehicleId = 0)
        {
            bool sameTargets = nextPos.m_segment == incomingState.nextSegmentId;
            bool wouldCollide;
            // bool incomingIsLeaving = incomingState.junctionTransitState == VehicleJunctionTransitState.Leave;

            if (sameTargets) {
                // both are going to the same segment
                Log._DebugIf(
                    logPriority,
                    () => $"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): " +
                    "Target and incoming are going to the same segment.");

                if (nextPos.m_lane == incomingState.nextLaneIndex) {
                    // both are going to the same lane: lane order is always incorrect
                    Log._DebugIf(
                        logPriority,
                        () => $"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): " +
                        "Target and incoming are going to the same segment AND lane. lane order is incorrect!");

                    wouldCollide = true;
                } else {
                    // both are going to a different lane: check lane order
                    Log._DebugIf(
                        logPriority,
                        () => $"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): " +
                        "Target and incoming are going to the same segment BUT NOT to the same lane. " +
                        "Determining if lane order is correct.");

                    switch (targetToDir) {
                        case ArrowDirection.Left:
                        case ArrowDirection.Turn:
                        default: // (should not happen)
                        {
                            // target & incoming are going left: stay left
                            wouldCollide = !IsLaneOrderConflictFree(
                                               logPriority,
                                               nextPos.m_segment,
                                               transitNodeId,
                                               nextPos.m_lane,
                                               incomingState.nextLaneIndex); // stay left
                            if (logPriority) {
                                Log._DebugFormat(
                                    "  TrafficPriorityManager.DetectCollision({0}, {1}): Target is going {2}. " +
                                    "Checking if lane {3} is LEFT to {4}. would collide? {5}",
                                    vehicleId,
                                    incomingVehicleId,
                                    targetToDir,
                                    nextPos.m_lane,
                                    incomingState.nextLaneIndex,
                                    wouldCollide);
                            }

                            break;
                        }

                        case ArrowDirection.Forward: {
                            // target is going forward/turn
                            switch (incomingFromDir) {
                                case ArrowDirection.Left:
                                case ArrowDirection.Forward: {
                                    // target is going forward, incoming is coming from left/forward: stay right
                                    wouldCollide = !IsLaneOrderConflictFree(
                                                       logPriority,
                                                       nextPos.m_segment,
                                                       transitNodeId,
                                                       incomingState.nextLaneIndex,
                                                       nextPos.m_lane); // stay right
                                    if (logPriority) {
                                        Log._DebugFormat(
                                            "  TrafficPriorityManager.DetectCollision({0}, {1}): " +
                                            "Target is going {2} and incoming is coming from {3}. " +
                                            "Checking if lane {4} is RIGHT to {5}. would collide? {6}",
                                            vehicleId,
                                            incomingVehicleId,
                                            targetToDir,
                                            incomingFromDir,
                                            nextPos.m_lane,
                                            incomingState.nextLaneIndex,
                                            wouldCollide);
                                    }

                                    break;
                                }

                                case ArrowDirection.Right: {
                                    // target is going forward, incoming is coming from right: stay left
                                    wouldCollide = !IsLaneOrderConflictFree(
                                                       logPriority,
                                                       nextPos.m_segment,
                                                       transitNodeId,
                                                       nextPos.m_lane,
                                                       incomingState.nextLaneIndex); // stay left
                                    if (logPriority) {
                                        Log._DebugFormat(
                                            "  TrafficPriorityManager.DetectCollision({0}, {1}): Target " +
                                            "is going {2} and incoming is coming from {3}. Checking if " +
                                            "lane {4} is LEFT to {5}. would collide? {6}",
                                            vehicleId,
                                            incomingVehicleId,
                                            targetToDir,
                                            incomingFromDir,
                                            nextPos.m_lane,
                                            incomingState.nextLaneIndex,
                                            wouldCollide);
                                    }

                                    break;
                                }

                                case ArrowDirection.Turn: // (should not happen)
                                default: // (should not happen)
                                {
                                    wouldCollide = false;
                                    if (logPriority) {
                                        Log.Warning($"  TrafficPriorityManager.DetectCollision({vehicleId}, " +
                                                    $"{incomingVehicleId}): Target is going {targetToDir} and " +
                                                    $"incoming is coming from {incomingFromDir} (SHOULD NOT HAPPEN). " +
                                                    $"would collide? {wouldCollide}");
                                    }
                                    break;
                                }
                            }

                            break;
                        }

                        case ArrowDirection.Right:
                            // target is going right: stay right
                            wouldCollide = !IsLaneOrderConflictFree(
                                               logPriority,
                                               nextPos.m_segment,
                                               transitNodeId,
                                               incomingState.nextLaneIndex,
                                               nextPos.m_lane); // stay right
                            if (logPriority) {
                                Log._DebugFormat(
                                    "  TrafficPriorityManager.DetectCollision({0}, {1}): Target is " +
                                    "going RIGHT. Checking if lane {2} is RIGHT to {3}. would collide? {4}",
                                    vehicleId,
                                    incomingVehicleId,
                                    nextPos.m_lane,
                                    incomingState.nextLaneIndex,
                                    wouldCollide);
                            }

                            break;
                    }

                    Log._DebugIf(
                        logPriority,
                        () => $"    TrafficPriorityManager.DetectCollision({vehicleId}, " +
                        $"{incomingVehicleId}): >>> would collide? {wouldCollide}");
                }
            } else {
                Log._DebugIf(
                    logPriority,
                    () => $"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): " +
                    $"Target and incoming are going to different segments.");

                switch (targetToDir) {
                    case ArrowDirection.Left: {
                        switch (incomingFromDir) {
                            case ArrowDirection.Left: {
                                wouldCollide = incomingToRelDir != ArrowDirection.Right;
                                break;
                            }

                            case ArrowDirection.Forward: {
                                wouldCollide = incomingToRelDir != ArrowDirection.Left &&
                                               incomingToRelDir != ArrowDirection.Turn;
                                break;
                            }

                            case ArrowDirection.Right: {
                                wouldCollide = incomingToRelDir != ArrowDirection.Right &&
                                               incomingToRelDir != ArrowDirection.Turn;
                                break;
                            }

                            default: // (should not happen)
                            {
                                wouldCollide = false;
                                if (logPriority) {
                                    Log.WarningFormat(
                                        "  TrafficPriorityManager.DetectCollision({0}, {1}): Target " +
                                        "is going {2}, incoming is coming from {3} and going {4}. " +
                                        "SHOULD NOT HAPPEN. would collide? {5}",
                                        vehicleId,
                                        incomingVehicleId,
                                        targetToDir,
                                        incomingFromDir,
                                        incomingToRelDir,
                                        wouldCollide);
                                }

                                break;
                            }
                        }

                        break;
                    }

                    case ArrowDirection.Forward: {
                        switch (incomingFromDir) {
                            case ArrowDirection.Left: {
                                wouldCollide = incomingToRelDir != ArrowDirection.Right &&
                                               incomingToRelDir != ArrowDirection.Turn;
                                break;
                            }

                            case ArrowDirection.Forward: {
                                wouldCollide = incomingToRelDir != ArrowDirection.Right &&
                                               incomingToRelDir != ArrowDirection.Forward;
                                break;
                            }

                            case ArrowDirection.Right: {
                                wouldCollide = true; // TODO allow u-turns? (check LHD)
                                break;
                            }

                            default: // (should not happen)
                            {
                                wouldCollide = false;
                                if (logPriority) {
                                    Log.WarningFormat(
                                        "  TrafficPriorityManager.DetectCollision({0}, {1}): Target " +
                                        "is going {2}, incoming is coming from {3} and going {4}. " +
                                        "SHOULD NOT HAPPEN. would collide? {5}",
                                        vehicleId,
                                        incomingVehicleId,
                                        targetToDir,
                                        incomingFromDir,
                                        incomingToRelDir,
                                        wouldCollide);
                                }

                                break;
                            }
                        }

                        break;
                    }

                    case ArrowDirection.Right:
                    case ArrowDirection.Turn:
                    default: {
                        wouldCollide = false;
                        break;
                    }
                }

                if (logPriority) {
                    Log._DebugFormat(
                        "  TrafficPriorityManager.DetectCollision({0}, {1}): Target is going {2}, " +
                        "incoming is coming from {3} and going {4}. would collide? {5}",
                        vehicleId,
                        incomingVehicleId,
                        targetToDir,
                        incomingFromDir,
                        incomingToRelDir,
                        wouldCollide);
                }
            }

            return wouldCollide;
        }

        /// <summary>
        /// Check common priority rules if both vehicles are on a collision course and on the same
        ///     priority level [(main AND main) OR (!main AND !main)]:
        /// 1. left yields to right
        /// 2. left-turning vehicles must yield to straight-going vehicles
        /// </summary>
        /// <param name="logPriority">enable debugging</param>
        /// <param name="targetToDir">absolute target vehicle destination direction</param>
        /// <param name="incomingFromDir">absolute incoming vehicle source direction</param>
        /// <param name="incomingToRelDir">relative incoming vehicle destination direction</param>
        /// <param name="vehicleId">(optional) target vehicle id</param>
        /// <param name="incomingVehicleId">(optional) incoming vehicle id</param>
        /// <returns></returns>
        public bool HasPriorityOnSameLevel(bool logPriority,
                                           ArrowDirection targetToDir,
                                           ArrowDirection incomingFromDir,
                                           ArrowDirection incomingToRelDir,
                                           ushort vehicleId = 0,
                                           ushort incomingVehicleId = 0)
        {
            bool ret;
            switch (incomingFromDir) {
                case ArrowDirection.Left:
                case ArrowDirection.Right: {
                    // (1) left yields to right
                    ret = incomingFromDir == ArrowDirection.Left;
                    break;
                }

                default: {
                    if (incomingToRelDir == ArrowDirection.Left ||
                        incomingToRelDir == ArrowDirection.Turn) {
                        // (2) incoming vehicle must wait
                        ret = true;
                    } else if (targetToDir == ArrowDirection.Left ||
                               targetToDir == ArrowDirection.Turn) {
                        // (2) target vehicle must wait
                        ret = false;
                    } else {
                        // (should not happen)
                        if (logPriority) {
                            Log.WarningFormat(
                                "TrafficPriorityManager.HasPriorityOnSameLevel({0}, {1}): targetToDir={2}, " +
                                "incomingFromDir={3}, incomingToRelDir={4}: SHOULD NOT HAPPEN",
                                vehicleId,
                                incomingVehicleId,
                                targetToDir,
                                incomingFromDir,
                                incomingToRelDir);
                        }

                        ret = true;
                    }

                    break;
                }
            }

            if (logPriority) {
                Log._DebugFormat(
                    "TrafficPriorityManager.HasPriorityOnSameLevel({0}, {1}): targetToDir={2}, " +
                    "incomingFromDir={3}, incomingToRelDir={4}: ret={5}",
                    vehicleId,
                    incomingVehicleId,
                    targetToDir,
                    incomingFromDir,
                    incomingToRelDir,
                    ret);
            }

            return ret;
        }

        /// <summary>
        /// Checks if lane <paramref name="leftLaneIndex"/> lies to the left of lane
        ///     <paramref name="rightLaneIndex"/>.
        /// </summary>
        /// <param name="logPriority">enable debugging</param>
        /// <param name="segmentId">segment id</param>
        /// <param name="nodeId">transit node id</param>
        /// <param name="leftLaneIndex">lane index that is checked to lie left</param>
        /// <param name="rightLaneIndex">lane index that is checked to lie right</param>
        /// <returns></returns>
        // TODO refactor
        public bool IsLaneOrderConflictFree(bool logPriority,
                                            ushort segmentId,
                                            ushort nodeId,
                                            byte leftLaneIndex,
                                            byte rightLaneIndex)
        {
            try {
                if (leftLaneIndex == rightLaneIndex) {
                    return false;
                }

                ref NetSegment netSegment = ref segmentId.ToSegment();

                NetInfo segmentInfo = netSegment.Info;

                NetInfo.Direction dir = nodeId == netSegment.m_startNode
                    ? NetInfo.Direction.Backward
                    : NetInfo.Direction.Forward;
                NetInfo.Direction dir2 = ((netSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None)
                    ? dir
                    : NetInfo.InvertDirection(dir);
                NetInfo.Direction dir3 = Shortcuts.LHT
                    ? NetInfo.InvertDirection(dir2)
                    : dir2;

                NetInfo.Lane leftLane = segmentInfo.m_lanes[leftLaneIndex];
                NetInfo.Lane rightLane = segmentInfo.m_lanes[rightLaneIndex];

                if (logPriority) {
                    Log._DebugFormat(
                        "    IsLaneOrderConflictFree({0}, {1}, {2}): dir={3}, dir2={4}, dir3={5} " +
                        "laneDir={6}, leftLanePos={7}, rightLanePos={8}",
                        segmentId,
                        leftLaneIndex,
                        rightLaneIndex,
                        dir,
                        dir2,
                        dir3,
                        leftLane.m_direction,
                        leftLane.m_position,
                        rightLane.m_position);
                }

                return (dir3 == NetInfo.Direction.Forward)
                       ^ (leftLane.m_position < rightLane.m_position);
            } catch (Exception e) {
                Log.Error($"IsLaneOrderConflictFree({segmentId}, {leftLaneIndex}, {rightLaneIndex}): Error: {e}");
            }

            return true;
        }

        protected override void HandleInvalidSegment(ref ExtSegment seg) {
            if (!PrioritySegments[seg.segmentId].IsDefault()) {
                AddInvalidPrioritySegment(seg.segmentId, ref PrioritySegments[seg.segmentId]);
            }

            RemovePrioritySignsFromSegment(seg.segmentId);
        }

        protected override void HandleValidSegment(ref ExtSegment seg) {
            ref NetSegment netSegment = ref seg.segmentId.ToSegment();

            if (!MaySegmentHavePrioritySign(seg.segmentId, true)) {
                RemovePrioritySignFromSegmentEnd(seg.segmentId, true);
            } else {
                UpdateNode(netSegment.m_startNode);
            }

            if (!MaySegmentHavePrioritySign(seg.segmentId, false)) {
                RemovePrioritySignFromSegmentEnd(seg.segmentId, false);
            } else {
                UpdateNode(netSegment.m_endNode);
            }
        }

        protected override void HandleSegmentEndReplacement(SegmentEndReplacement replacement,
                                                            ref ExtSegmentEnd segEnd) {
            ISegmentEndId oldSegmentEndId = replacement.oldSegmentEndId;
            ISegmentEndId newSegmentEndId = replacement.newSegmentEndId;

            PriorityType sign;

            if (oldSegmentEndId.StartNode) {
                sign = invalidPrioritySegments[oldSegmentEndId.SegmentId].startType;
                invalidPrioritySegments[oldSegmentEndId.SegmentId].startType = PriorityType.None;
            } else {
                sign = invalidPrioritySegments[oldSegmentEndId.SegmentId].endType;
                invalidPrioritySegments[oldSegmentEndId.SegmentId].endType = PriorityType.None;
            }

            if (sign == PriorityType.None) {
                return;
            }

            Log._Debug(
                $"TrafficPriorityManager.HandleSegmentEndReplacement({replacement}): " +
                $"Segment replacement detected: {oldSegmentEndId.SegmentId} -> {newSegmentEndId.SegmentId}\n" +
                $"Moving priority sign {sign} to new segment.");

            SetPrioritySign(newSegmentEndId.SegmentId, newSegmentEndId.StartNode, sign);
        }

        protected void UpdateNode(ushort nodeId) {
            SetPrioritySignError reason;

            if (!MayNodeHavePrioritySigns(nodeId, out reason)) {
                RemovePrioritySignsFromNode(nodeId);
                return;
            }
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();
            for (int i = 0; i < PrioritySegments.Length; ++i) {
                RemovePrioritySignsFromSegment((ushort)i);
            }

            for (int i = 0; i < invalidPrioritySegments.Length; ++i) {
                invalidPrioritySegments[i].Reset();
            }
        }

        [Obsolete]
        public bool LoadData(List<int[]> data) {
            bool success = true;
            Log.Info($"Loading {data.Count} priority segments (old method)");

            foreach (int[] segment in data) {
                try {
                    if (segment.Length < 3) {
                        continue;
                    }

                    if ((PriorityType)segment[2] == PriorityType.None) {
                        continue;
                    }

                    var nodeId = (ushort)segment[0];
                    var segmentId = (ushort)segment[1];
                    var sign = (PriorityType)segment[2];

                    if (!nodeId.ToNode().IsValid()) {
                        continue;
                    }

                    ref NetSegment netSegment = ref segmentId.ToSegment();

                    if (!netSegment.IsValid()) {
                        continue;
                    }

                    bool? startNode = netSegment.GetRelationToNode(nodeId);
                    if (!startNode.HasValue) {
                        Log.Error("TrafficPriorityManager.LoadData: No node found for node id " +
                                  $"{nodeId} @ seg. {segmentId}");
                        continue;
                    }

                    SetPrioritySign(segmentId, startNode.Value, sign);
                } catch (Exception e) {
                    // ignore, as it's probably corrupt save data. it'll be culled on next save
                    Log.Warning($"Error loading data from Priority segments: {e}");
                    success = false;
                }
            }

            return success;
        }

        [Obsolete]
        public List<int[]> SaveData(ref bool success) {
            return null;
        }

        public bool LoadData(List<Configuration.PrioritySegment> data) {
            bool success = true;
            Log.Info($"Loading {data.Count} priority segments (new method)");
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
            foreach (var prioSegData in data) {
                try {
                    if ((PriorityType)prioSegData.priorityType == PriorityType.None) {
                        continue;
                    }

                    ref NetNode netNode = ref prioSegData.nodeId.ToNode();
                    if (!netNode.IsValid()) {
                        continue;
                    }

                    ref NetSegment netSegment = ref prioSegData.segmentId.ToSegment();
                    if (!netSegment.IsValid()) {
                        continue;
                    }

                    bool? startNode = netSegment.GetRelationToNode(prioSegData.nodeId);

                    if (!startNode.HasValue) {
                        Log.Error(
                            "TrafficPriorityManager.LoadData: No node found for node id " +
                            $"{prioSegData.nodeId} @ seg. {prioSegData.segmentId}");
                        continue;
                    }

#if DEBUGLOAD
                    Log._Debug($"Loading priority sign {(PriorityType)prioSegData.priorityType} @ seg. "+
                    $"{prioSegData.segmentId}, start node? {startNode}");
#endif
                    SetPrioritySign(
                        prioSegData.segmentId,
                        startNode.Value,
                        (PriorityType)prioSegData.priorityType);
                }
                catch (Exception e) {
                    // ignore, as it's probably corrupt save data. it'll be culled on next save
                    Log.Warning($"Error loading data from Priority segments: {e}");
                    success = false;
                }
            }

            return success;
        }

        List<Configuration.PrioritySegment>
            ICustomDataManager<List<Configuration.PrioritySegment>>.SaveData(ref bool success)
        {
            var ret = new List<Configuration.PrioritySegment>();

            for (uint segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                try {
                    ref NetSegment netSegment = ref ((ushort)segmentId).ToSegment();

                    if (!netSegment.IsValid() ||
                        !HasSegmentPrioritySign((ushort)segmentId)) {
                        continue;
                    }

                    PriorityType startSign = GetPrioritySign((ushort)segmentId, true);

                    if (startSign != PriorityType.None) {
                        ushort startNodeId = netSegment.m_startNode;
                        ref NetNode startNode = ref startNodeId.ToNode();

                        if (startNode.IsValid()) {
#if DEBUGSAVE
                            Log._Debug($"Saving priority sign of type {startSign} @ start node "+
                            $"{startNodeId} of segment {segmentId}");
#endif
                            ret.Add(
                                new Configuration.PrioritySegment(
                                    (ushort)segmentId,
                                    startNodeId,
                                    (int)startSign));
                        }
                    }

                    PriorityType endSign = GetPrioritySign((ushort)segmentId, false);

                    if (endSign != PriorityType.None) {
                        ushort endNodeId = netSegment.m_endNode;
                        ref NetNode endNode = ref endNodeId.ToNode();

                        if (endNode.IsValid()) {
#if DEBUGSAVE
                            Log._Debug($"Saving priority sign of type {endSign} @ end node "+
                            $"{endNodeId} of segment {segmentId}");
#endif
                            ret.Add(
                                new Configuration.PrioritySegment(
                                    (ushort)segmentId,
                                    endNodeId,
                                    (int)endSign));
                        }
                    }
                }
                catch (Exception e) {
                    Log.Error($"Exception occurred while saving priority segment @ seg. {segmentId}: {e}");
                    success = false;
                }
            }

            return ret;
        }

        /// <summary>
        /// Used for loading and saving PrioritySegments
        /// </summary>
        /// <returns>ICustomDataManager for prio segments</returns>
        public static ICustomDataManager<List<int[]>> AsPrioritySegmentsDM() {
            return Instance;
        }

        /// <summary>
        /// Used for loading and saving CustomPrioritySegments
        /// </summary>
        /// <returns>ICustomDataManager for custom prio segments</returns>
        public static ICustomDataManager<List<Configuration.PrioritySegment>> AsCustomPrioritySegmentsDM() {
            return Instance;
        }
    }
}