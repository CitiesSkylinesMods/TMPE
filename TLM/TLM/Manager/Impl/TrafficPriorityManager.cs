namespace TrafficManager.Manager.Impl {
    using System;
    using System.Collections.Generic;
    using API.Traffic.Data;
    using API.Traffic.Enums;
    using API.TrafficLight;
    using ColossalFramework;
    using CSUtil.Commons;
    using Geometry;
    using State;
    using Traffic;
    using Traffic.Data;
    using TrafficLight;
    using UnityEngine;

    public class TrafficPriorityManager : AbstractGeometryObservingManager,
                                          ICustomDataManager<List<int[]>>, ICustomDataManager<List<Configuration.PrioritySegment>>,
                                          ITrafficPriorityManager {
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
            Log._Debug($"Priority signs:");
            for (int i = 0; i < PrioritySegments.Length; ++i) {
                if (PrioritySegments[i].IsDefault()) {
                    continue;
                }
                Log._Debug($"Segment {i}: {PrioritySegments[i]}");
            }
        }

        protected void AddInvalidPrioritySegment(ushort segmentId, ref PrioritySegment prioritySegment) {
            invalidPrioritySegments[segmentId] = prioritySegment;
        }

        public bool MayNodeHavePrioritySigns(ushort nodeId) {
            SetPrioritySignUnableReason reason;
            return MayNodeHavePrioritySigns(nodeId, out reason);
        }

        public bool MayNodeHavePrioritySigns(ushort nodeId, out SetPrioritySignUnableReason reason) {
#if DEBUG
            bool debug = GlobalConfig.Instance.Debug.Switches[13] && (GlobalConfig.Instance.Debug.NodeId <= 0 || nodeId == GlobalConfig.Instance.Debug.NodeId);
#endif
            if (!Services.NetService.CheckNodeFlags(nodeId, NetNode.Flags.Created | NetNode.Flags.Deleted | NetNode.Flags.Junction, NetNode.Flags.Created | NetNode.Flags.Junction)) {
                reason = SetPrioritySignUnableReason.NoJunction;
#if DEBUG
                if (debug) {
                    Log._Debug($"TrafficPriorityManager.MayNodeHavePrioritySigns: nodeId={nodeId}, result=false, reason={reason}");
                }
#endif
                return false;
            }

            if (TrafficLightSimulationManager.Instance.HasTimedSimulation(nodeId)) {
                reason = SetPrioritySignUnableReason.HasTimedLight;
#if DEBUG
                if (debug) {
                    Log._Debug($"TrafficPriorityManager.MayNodeHavePrioritySigns: nodeId={nodeId}, result=false, reason={reason}");
                }
#endif
                return false;
            }

            //Log._Debug($"TrafficPriorityManager.MayNodeHavePrioritySigns: nodeId={nodeId}, result=true");
            reason = SetPrioritySignUnableReason.None;
            return true;
        }

        public bool MaySegmentHavePrioritySign(ushort segmentId, bool startNode) {
            SetPrioritySignUnableReason reason;
            return MaySegmentHavePrioritySign(segmentId, startNode, out reason);
        }

        public bool MaySegmentHavePrioritySign(ushort segmentId, bool startNode, out SetPrioritySignUnableReason reason) {
#if DEBUG
            bool debug = GlobalConfig.Instance.Debug.Switches[13] && (GlobalConfig.Instance.Debug.SegmentId <= 0 || segmentId == GlobalConfig.Instance.Debug.SegmentId);
#endif
            if (! Services.NetService.IsSegmentValid(segmentId)) {
                reason = SetPrioritySignUnableReason.InvalidSegment;
#if DEBUG
                if (debug) {
                    Log._Debug($"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, startNode={startNode}, result=false, reason={reason}");
                }
#endif
                return false;
            }

            if (! MayNodeHavePrioritySigns(Services.NetService.GetSegmentNodeId(segmentId, startNode), out reason)) {
#if DEBUG
                if (debug) {
                    Log._Debug($"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, startNode={startNode}, result=false, reason={reason}");
                }
#endif
                return false;
            }

            IExtSegmentManager segMan = Constants.ManagerFactory.ExtSegmentManager;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            if (segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)].outgoing && segMan.ExtSegments[segmentId].oneWay) {
                reason = SetPrioritySignUnableReason.NotIncoming;
#if DEBUG
                if (debug) {
                    Log._Debug($"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, startNode={startNode}, result=false, reason={reason}");
                }
#endif
                return false;
            }

#if DEBUG
            if (debug) {
                Log._Debug($"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, startNode={startNode}, result=true");
            }
#endif
            reason = SetPrioritySignUnableReason.None;
            return true;
        }

        public bool MaySegmentHavePrioritySign(ushort segmentId) {
            SetPrioritySignUnableReason reason;
            return MaySegmentHavePrioritySign(segmentId, out reason);
        }

        public bool MaySegmentHavePrioritySign(ushort segmentId, out SetPrioritySignUnableReason reason) {
#if DEBUG
            bool debug = GlobalConfig.Instance.Debug.Switches[13] && (GlobalConfig.Instance.Debug.SegmentId <= 0 || segmentId == GlobalConfig.Instance.Debug.SegmentId);
#endif
            if (!Services.NetService.IsSegmentValid(segmentId)) {
                reason = SetPrioritySignUnableReason.InvalidSegment;
#if DEBUG
                if (debug) {
                    Log._Debug($"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, result=false, reason={reason}");
                }
#endif
                return false;
            }

            bool ret =
                (MaySegmentHavePrioritySign(segmentId, true, out reason) ||
                 MaySegmentHavePrioritySign(segmentId, false, out reason));
#if DEBUG
            if (debug) {
                Log._Debug($"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, result={ret}, reason={reason}");
            }
#endif
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
            bool debug = GlobalConfig.Instance.Debug.Switches[13] && (GlobalConfig.Instance.Debug.NodeId <= 0 || nodeId == GlobalConfig.Instance.Debug.NodeId);
#endif
            bool ret = false;
            Services.NetService.IterateNodeSegments(nodeId, delegate (ushort segmentId, ref NetSegment segment) {
                if (HasSegmentPrioritySign(segmentId, nodeId == segment.m_startNode)) {
                    ret = true;
                    return false;
                }
                return true;
            });
#if DEBUG
            if (debug) {
                Log._Debug($"TrafficPriorityManager.HasNodePrioritySign: nodeId={nodeId}, result={ret}");
            }
#endif
            return ret;
        }

        public bool SetPrioritySign(ushort segmentId, bool startNode, PriorityType type) {
            SetPrioritySignUnableReason reason;
            return SetPrioritySign(segmentId, startNode, type, out reason);
        }

        public bool SetPrioritySign(ushort segmentId, bool startNode, PriorityType type, out SetPrioritySignUnableReason reason) {
#if DEBUG
            bool debug = GlobalConfig.Instance.Debug.Switches[13] && (GlobalConfig.Instance.Debug.SegmentId <= 0 || segmentId == GlobalConfig.Instance.Debug.SegmentId);
#endif

            bool ret = true;
            reason = SetPrioritySignUnableReason.None;

            if (type != PriorityType.None &&
                ! MaySegmentHavePrioritySign(segmentId, startNode, out reason)) {
#if DEBUG
                if (debug) {
                    Log._Debug($"TrafficPriorityManager.SetPrioritySign: Segment {segmentId} @ {startNode} may not have a priority sign: {reason}");
                }
#endif
                ret = false;
                type = PriorityType.None;
            }

            if (type != PriorityType.None) {
                ushort nodeId = Services.NetService.GetSegmentNodeId(segmentId, startNode);
                Services.NetService.ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
                    TrafficLightManager.Instance.RemoveTrafficLight(nodeId, ref node);
                    return true;
                });
            }

            if (startNode) {
                PrioritySegments[segmentId].startType = type;
            } else {
                PrioritySegments[segmentId].endType = type;
            }

            SegmentEndManager.Instance.UpdateSegmentEnd(segmentId, startNode);
#if DEBUG
            if (debug) {
                Log._Debug($"TrafficPriorityManager.SetPrioritySign: segmentId={segmentId}, startNode={startNode}, type={type}, result={ret}, reason={reason}");
            }
#endif
            return ret;
        }

        public void RemovePrioritySignsFromNode(ushort nodeId) {
#if DEBUG
            bool debug = GlobalConfig.Instance.Debug.Switches[13] && (GlobalConfig.Instance.Debug.NodeId <= 0 || nodeId == GlobalConfig.Instance.Debug.NodeId);
            if (debug) {
                Log._Debug($"TrafficPriorityManager.RemovePrioritySignsFromNode: nodeId={nodeId}");
            }
#endif

            Services.NetService.IterateNodeSegments(nodeId, delegate(ushort segmentId, ref NetSegment segment) {
                RemovePrioritySignFromSegmentEnd(segmentId, nodeId == segment.m_startNode);
                return true;
            });
        }

        public void RemovePrioritySignsFromSegment(ushort segmentId) {
#if DEBUG
            bool debug = GlobalConfig.Instance.Debug.Switches[13] && (GlobalConfig.Instance.Debug.SegmentId <= 0 || segmentId == GlobalConfig.Instance.Debug.SegmentId);
            if (debug) {
                Log._Debug($"TrafficPriorityManager.RemovePrioritySignsFromSegment: segmentId={segmentId}");
            }
#endif

            RemovePrioritySignFromSegmentEnd(segmentId, true);
            RemovePrioritySignFromSegmentEnd(segmentId, false);
        }

        public void RemovePrioritySignFromSegmentEnd(ushort segmentId, bool startNode) {
#if DEBUG
            bool debug = GlobalConfig.Instance.Debug.Switches[13] && (GlobalConfig.Instance.Debug.SegmentId <= 0 || segmentId == GlobalConfig.Instance.Debug.SegmentId);
            if (debug) {
                Log._Debug($"TrafficPriorityManager.RemovePrioritySignFromSegment: segmentId={segmentId}, startNode={startNode}");
            }
#endif

            if (startNode) {
                PrioritySegments[segmentId].startType = PriorityType.None;
            } else {
                PrioritySegments[segmentId].endType = PriorityType.None;
            }

            SegmentEndManager.Instance.UpdateSegmentEnd(segmentId, startNode);
        }

        public PriorityType GetPrioritySign(ushort segmentId, bool startNode) {
            return startNode ? PrioritySegments[segmentId].startType : PrioritySegments[segmentId].endType;
        }

        public byte CountPrioritySignsAtNode(ushort nodeId, PriorityType sign) {
#if DEBUG
            bool debug = GlobalConfig.Instance.Debug.Switches[13] && (GlobalConfig.Instance.Debug.NodeId <= 0 || nodeId == GlobalConfig.Instance.Debug.NodeId);
#endif

            byte ret = 0;
            Services.NetService.IterateNodeSegments(nodeId, delegate (ushort segmentId, ref NetSegment segment) {
                if (GetPrioritySign(segmentId, segment.m_startNode == nodeId) == sign) {
                    ++ret;
                }
                return true;
            });
#if DEBUG
            if (debug) {
                Log._Debug($"TrafficPriorityManager.CountPrioritySignsAtNode: nodeId={nodeId}, sign={sign}, result={ret}");
            }
#endif
            return ret;
        }

        public bool HasPriority(ushort vehicleId, ref Vehicle vehicle, ref PathUnit.Position curPos, ref ExtSegmentEnd curEnd, ushort transitNodeId, bool startNode, ref PathUnit.Position nextPos, ref NetNode transitNode) {
            IExtSegmentEndManager extSegEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

//			ISegmentEndGeometry endGeo = SegmentGeometry.Get(curPos.m_segment)?.GetEnd(startNode);
//			if (Constants.ManagerFactory.ExtSegmentManager == null) {
//#if DEBUG
//				Log.Warning($"TrafficPriorityManager.HasPriority({vehicleId}): No segment end geometry found for segment {curPos.m_segment} @ {startNode}");
//				return true;
//#endif
//			}

            /*SegmentEnd end = SegmentEndManager.Instance.GetSegmentEnd(curPos.m_segment, startNode);
            if (end == null) {
#if DEBUG
                    Log.Warning($"TrafficPriorityManager.HasPriority({vehicleId}): No segment end found for segment {curPos.m_segment} @ {startNode}");
                    return true;
#endif
            }
            ushort transitNodeId = end.NodeId;*/

#if DEBUG
            bool debug = GlobalConfig.Instance.Debug.Switches[13] && (GlobalConfig.Instance.Debug.NodeId <= 0 || transitNodeId == GlobalConfig.Instance.Debug.NodeId);
            if (debug) {
                Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): Checking vehicle {vehicleId} at node {transitNodeId}. Coming from seg. {curPos.m_segment}, start {startNode}, lane {curPos.m_lane}, going to seg. {nextPos.m_segment}, lane {nextPos.m_lane}");
            }
#else
			bool debug = false;
#endif

            if ((vehicle.m_flags & Vehicle.Flags.Spawned) == 0) {
#if DEBUG
                if (debug)
                    Log.Warning($"TrafficPriorityManager.HasPriority({vehicleId}): Vehicle is not spawned.");
#endif
                return true;
            }

            if ((vehicle.m_flags & Vehicle.Flags.Emergency2) != 0) {
                // target vehicle is on emergency
#if DEBUG
                if (debug)
                    Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): Vehicle is on emergency.");
#endif
                return true;
            }

            if (vehicle.Info.m_vehicleType == VehicleInfo.VehicleType.Monorail) {
                // monorails do not obey priority signs
#if DEBUG
                if (debug)
                    Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): Vehicle is a monorail.");
#endif
                return true;
            }

            PriorityType curSign = GetPrioritySign(curPos.m_segment, startNode);
            if (curSign == PriorityType.None) {
#if DEBUG
                if (debug)
                    Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): Sign is None @ seg. {curPos.m_segment}, start {startNode} -> setting to Main");
#endif
                curSign = PriorityType.Main;
            }
            bool onMain = curSign == PriorityType.Main;

            /*if (! Services.VehicleService.IsVehicleValid(vehicleId)) {
                    curEnd.RequestCleanup();
                    return true;
            }*/

            // calculate approx. time after which the transit node will be reached
            Vector3 targetToNode = transitNode.m_position - vehicle.GetLastFramePosition();
            Vector3 targetVel = vehicle.GetLastFrameVelocity();
            float targetSpeed = targetVel.magnitude;
            float targetDistanceToTransitNode = targetToNode.magnitude;

            float targetTimeToTransitNode = Single.NaN;
            if (targetSpeed > 0)
                targetTimeToTransitNode = targetDistanceToTransitNode / targetSpeed;
            else
                targetTimeToTransitNode = 0;

#if DEBUG
            if (debug)
                Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): estimated target time to transit node {transitNodeId} is {targetTimeToTransitNode} for vehicle {vehicleId}");
#endif

            ArrowDirection targetToDir = extSegEndMan.GetDirection(ref curEnd, nextPos.m_segment); // absolute target direction of target vehicle

            // iterate over all cars approaching the transit node and check if the target vehicle should be prioritized
            ExtVehicleManager vehStateManager = ExtVehicleManager.Instance;
            CustomSegmentLightsManager segLightsManager = CustomSegmentLightsManager.Instance;

            for (int i = 0; i < 8; ++i) {
                ushort otherSegmentId = transitNode.GetSegment(i);
                if (otherSegmentId == curEnd.segmentId || otherSegmentId == 0) {
                    continue;
                }

                bool otherStartNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(otherSegmentId, transitNodeId);
                /*ISegmentEnd incomingEnd = SegmentEndManager.Instance.GetSegmentEnd(otherSegmentId, otherStartNode);
                if (incomingEnd == null) {
#if DEBUG
                        if (debug)
                                Log.Error($"TrafficPriorityManager.HasPriority({vehicleId}): No segment end found for other segment {otherSegmentId} @ {otherStartNode}");
#endif
                        return true;
                }*/

                ICustomSegmentLights otherLights = null;
                if (Options.trafficLightPriorityRules) {
                    otherLights = segLightsManager.GetSegmentLights(otherSegmentId, otherStartNode, false);
                }

                PriorityType otherSign = GetPrioritySign(otherSegmentId, otherStartNode);
                if (otherSign == PriorityType.None) {
                    otherSign = PriorityType.Main;
                    //continue;
                }
                bool incomingOnMain = otherSign == PriorityType.Main;

                ArrowDirection incomingFromDir = extSegEndMan.GetDirection(ref curEnd, otherSegmentId); // absolute incoming direction of incoming vehicle

#if DEBUG
                if (debug)
                    Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): checking other segment {otherSegmentId} @ {transitNodeId}");
#endif

                int otherEndIndex = extSegEndMan.GetIndex(otherSegmentId, otherStartNode);
                ushort incomingVehicleId = extSegEndMan.ExtSegmentEnds[otherEndIndex].firstVehicleId;
                int numIter = 0;
                while (incomingVehicleId != 0) {
#if DEBUG
                    if (debug) {
                        Log._Debug("");
                        Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): checking other vehicle {incomingVehicleId} @ seg. {otherSegmentId}");
                    }
#endif
                    if (IsConflictingVehicle(debug, transitNode.m_position, targetTimeToTransitNode, vehicleId, ref vehicle, ref curPos, transitNodeId, startNode, ref nextPos, onMain, ref curEnd, targetToDir, incomingVehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[incomingVehicleId], ref vehStateManager.ExtVehicles[incomingVehicleId], incomingOnMain, ref extSegEndMan.ExtSegmentEnds[otherEndIndex], otherLights, incomingFromDir)) {
#if DEBUG
                        if (debug) {
                            Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): incoming vehicle {incomingVehicleId} is conflicting.");
                        }
#endif
                        return false;
                    }

                    // check next incoming vehicle
                    incomingVehicleId = vehStateManager.ExtVehicles[incomingVehicleId].nextVehicleIdOnSegment;

                    if (++numIter > Constants.ServiceFactory.VehicleService.MaxVehicleCount) {
                        CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }
            }
#if DEBUG
            if (debug) {
                Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): No conflicting incoming vehicles found.");
            }
#endif
            return true;
        }

        private bool IsConflictingVehicle(bool debug, Vector3 transitNodePos, float targetTimeToTransitNode, ushort vehicleId, ref Vehicle vehicle,
                                          ref PathUnit.Position curPos, ushort transitNodeId, bool startNode, ref PathUnit.Position nextPos, bool onMain, ref ExtSegmentEnd curEnd,
                                          ArrowDirection targetToDir, ushort incomingVehicleId, ref Vehicle incomingVehicle, ref ExtVehicle incomingState, bool incomingOnMain,
                                          ref ExtSegmentEnd incomingEnd, ICustomSegmentLights incomingLights, ArrowDirection incomingFromDir) {
#if DEBUG
            if (debug) {
                Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Checking against other vehicle {incomingVehicleId}.");
                Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): TARGET is coming from seg. {curPos.m_segment}, start {startNode}, lane {curPos.m_lane}, going to seg. {nextPos.m_segment}, lane {nextPos.m_lane}");
                Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): INCOMING is coming from seg. {incomingState.currentSegmentId}, start {incomingState.currentStartNode}, lane {incomingState.currentLaneIndex}, going to seg. {incomingState.nextSegmentId}, lane {incomingState.nextLaneIndex}\nincoming state: {incomingState}");
            }
#endif

            if ((incomingState.flags & ExtVehicleFlags.Spawned) == ExtVehicleFlags.None) {
#if DEBUG
                if (debug)
                    Log.Warning($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming vehicle is not spawned.");
#endif
                return false;
            }

            if (incomingVehicle.Info.m_vehicleType == VehicleInfo.VehicleType.Monorail) {
                // monorails and cars do not collide
#if DEBUG
                if (debug) {
                    Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming vehicle is a monorail.");
                }
#endif
                return false;
            }

            ArrowDirection incomingToRelDir = Constants.ManagerFactory.ExtSegmentEndManager.GetDirection(ref incomingEnd, incomingState.nextSegmentId); // relative target direction of incoming vehicle

            if (incomingLights != null) {
                ICustomSegmentLight incomingLight = incomingLights.GetCustomLight(incomingState.currentLaneIndex);
#if DEBUG
                if (debug)
                    Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Detected traffic light. Incoming state ({incomingToRelDir}): {incomingLight.GetLightState(incomingToRelDir)}");
#endif
                if (incomingLight.IsRed(incomingToRelDir)) {
#if DEBUG
                    if (debug)
                        Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming traffic light is red.");
#endif
                    return false;
                }
            }

            if (incomingState.junctionTransitState != VehicleJunctionTransitState.None) {
                Vector3 incomingVel = incomingVehicle.GetLastFrameVelocity();
                bool incomingStateChangedRecently = Constants.ManagerFactory.ExtVehicleManager.IsJunctionTransitStateNew(ref incomingState);
                if (incomingState.junctionTransitState == VehicleJunctionTransitState.Approach ||
                    incomingState.junctionTransitState == VehicleJunctionTransitState.Leave
                    ) {
                    if ((incomingState.vehicleType & API.Traffic.Enums.ExtVehicleType.RoadVehicle) != API.Traffic.Enums.ExtVehicleType.None) {
                        float incomingSqrSpeed = incomingVel.sqrMagnitude;
                        if (!incomingStateChangedRecently && incomingSqrSpeed <= GlobalConfig.Instance.PriorityRules.MaxStopVelocity) {
#if DEBUG
                            if (debug)
                                Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} is LEAVING or APPROACHING but not moving. -> BLOCKED");
#endif
                            Constants.ManagerFactory.ExtVehicleManager.SetJunctionTransitState(ref incomingState, VehicleJunctionTransitState.Blocked);
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
#if DEBUG
                        if (debug)
                            Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} is moving away from the transit node ({dot}). *IGNORING*");
#endif
                        return false;
                    }
#if DEBUG
                    if (debug)
                        Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} is moving towards the transit node ({dot}). Distance: {incomingToNode.magnitude}");
#endif

                    // check if estimated approach time of the incoming vehicle is within bounds (only if incoming vehicle is far enough away from the junction and target vehicle is moving)
                    if (!Single.IsInfinity(targetTimeToTransitNode) &&
                        !Single.IsNaN(targetTimeToTransitNode) &&
                        incomingToNode.sqrMagnitude > GlobalConfig.Instance.PriorityRules.MaxPriorityCheckSqrDist
                        ) {

                        // check speeds
                        float incomingSpeed = incomingVel.magnitude;
                        float incomingDistanceToTransitNode = incomingToNode.magnitude;
                        float incomingTimeToTransitNode = Single.NaN;

                        if (incomingSpeed > 0)
                            incomingTimeToTransitNode = incomingDistanceToTransitNode / incomingSpeed;
                        else
                            incomingTimeToTransitNode = Single.PositiveInfinity;

                        float timeDiff = Mathf.Abs(incomingTimeToTransitNode - targetTimeToTransitNode);
                        if (timeDiff > GlobalConfig.Instance.PriorityRules.MaxPriorityApproachTime) {
#if DEBUG
                            if (debug)
                                Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} needs {incomingTimeToTransitNode} time units to get to the node where target needs {targetTimeToTransitNode} time units (diff = {timeDiff}). Difference to large. *IGNORING*");
#endif
                            return false;
                        } else {
#if DEBUG
                            if (debug)
                                Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} needs {incomingTimeToTransitNode} time units to get to the node where target needs {targetTimeToTransitNode} time units (diff = {timeDiff}). Difference within bounds. Priority check required.");
#endif
                        }
                    } else {
#if DEBUG
                        if (debug)
                            Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming is stopped.");
#endif
                    }
                }

                if (!incomingStateChangedRecently &&
                    (incomingState.junctionTransitState == VehicleJunctionTransitState.Blocked/* ||
					(incomingState.JunctionTransitState == VehicleJunctionTransitState.Stop && vehicleId < incomingVehicleId)*/)
                    ) {
#if DEBUG
                    if (debug)
                        Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} is BLOCKED and has waited a bit or is STOP and targetVehicleId {vehicleId} < incomingVehicleId {incomingVehicleId}. *IGNORING*");
#endif

                    // incoming vehicle waits because the junction is blocked or it does not get priority and we waited for some time. Allow target vehicle to enter the junciton.
                    return false;
                }

                // check priority rules
                if (HasVehiclePriority(debug, vehicleId, ref vehicle, ref curPos, transitNodeId, startNode, ref nextPos, onMain, targetToDir, incomingVehicleId, ref incomingVehicle, ref incomingState, incomingOnMain, incomingFromDir, incomingToRelDir)) {
#if DEBUG
                    if (debug)
                        Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} is not conflicting.");
#endif
                    return false;
                } else {
#if DEBUG
                    if (debug)
                        Log._Debug($"==========> TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} IS conflicting.");
#endif
                    return true;
                }
            } else {
#if DEBUG
                if (debug)
                    Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} (main) is not conflicting ({incomingState.junctionTransitState}).");
#endif
                return false;
            }
        }

        /// <summary>
        /// Implements priority checking for two vehicles approaching or waiting at a junction.
        /// </summary>
        /// <param name="debug"></param>
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
        private bool HasVehiclePriority(bool debug, ushort vehicleId, ref Vehicle vehicle, ref PathUnit.Position curPos, ushort transitNodeId, bool startNode, ref PathUnit.Position nextPos,
                                        bool onMain, ArrowDirection targetToDir, ushort incomingVehicleId, ref Vehicle incomingVehicle, ref ExtVehicle incomingState, bool incomingOnMain,
                                        ArrowDirection incomingFromDir, ArrowDirection incomingToRelDir) {
#if DEBUG
            if (debug) {
                Log._Debug("");
                Log._Debug($"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): *** Checking if vehicle {vehicleId} (main road = {onMain}) @ (seg. {curPos.m_segment}, start {startNode}, lane {curPos.m_lane}) -> (seg. {nextPos.m_segment}, lane {nextPos.m_lane}) has priority over {incomingVehicleId} (main road = {incomingOnMain}) @ (seg. {incomingState.currentSegmentId}, start {incomingState.currentStartNode}, lane {incomingState.currentLaneIndex}) -> (seg. {incomingState.nextSegmentId}, lane {incomingState.nextLaneIndex}).");
            }
#endif

            if (targetToDir == ArrowDirection.None || incomingFromDir == ArrowDirection.None || incomingToRelDir == ArrowDirection.None) {
#if DEBUG
                if (debug) {
                    Log._Debug($"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): Invalid directions given: targetToDir={targetToDir}, incomingFromDir={incomingFromDir}, incomingToRelDir={incomingToRelDir}");
                }
#endif
                return true;
            }

            if (curPos.m_segment == incomingState.currentSegmentId) {
                // both vehicles are coming from the same segment. do not apply priority rules in this case.
#if DEBUG
                if (debug) {
                    Log._Debug($"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): Both vehicles come from the same segment. *IGNORING*");
                }
#endif
                return true;
            }

            /*       FORWARD
             *          |
             *          |
             * LEFT --- + --- RIGHT
             *          |
             *          |
             *        TURN

            /*
             * - Target car is always coming from TURN.
             * - Target car is going to `targetToDir` (relative to TURN).
             * - Incoming car is coming from `incomingFromDir` (relative to TURN).
             * - Incoming car is going to `incomingToRelDir` (relative to `incomingFromDir`).
             */
#if DEBUG
            if (debug) {
                Log._Debug($"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): targetToDir: {targetToDir.ToString()}, incomingFromDir: {incomingFromDir.ToString()}, incomingToRelDir: {incomingToRelDir.ToString()}");
            }
#endif

            if (Services.SimulationService.LeftHandDrive) {
                // mirror situation for left-hand traffic systems
                targetToDir = ArrowDirectionUtil.InvertLeftRight(targetToDir);
                incomingFromDir = ArrowDirectionUtil.InvertLeftRight(incomingFromDir);
                incomingToRelDir = ArrowDirectionUtil.InvertLeftRight(incomingToRelDir);
#if DEBUG
                if (debug) {
                    Log._Debug($"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): LHD! targetToDir: {targetToDir.ToString()}, incomingFromDir: {incomingFromDir.ToString()}, incomingToRelDir: {incomingToRelDir.ToString()}");
                }
#endif
            }

#if DEBUG
            if (debug) {
                Log._Debug($"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): targetToDir={targetToDir}, incomingFromDir={incomingFromDir}, incomingToRelDir={incomingToRelDir}");
            }
#endif

            /*
             * (1) COLLISION DETECTION
             */

            bool sameTargets = nextPos.m_segment == incomingState.nextSegmentId;
            bool wouldCollide = DetectCollision(debug, ref curPos, transitNodeId, startNode, ref nextPos, ref incomingState, targetToDir, incomingFromDir, incomingToRelDir, vehicleId, incomingVehicleId);

            if (!wouldCollide) {
                // both vehicles would not collide. allow both to pass.
#if DEBUG
                if (debug) {
                    Log._Debug($"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): Cars {vehicleId} and {incomingVehicleId} would not collide. NO CONFLICT.");
                }
#endif
                return true;
            }

            // -> vehicles would collide
#if DEBUG
            if (debug) {
                Log._Debug($"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): Cars {vehicleId} and {incomingVehicleId} would collide. Checking priority rules.");
            }
#endif

            /*
             * (2) CHECK PRIORITY RULES
             */

            bool ret;
            if ((!onMain && !incomingOnMain) || (onMain && incomingOnMain)) {
                // both vehicles are on the same priority level: check common priority rules (left yields to right, left turning vehicles yield to others)
                ret = HasPriorityOnSameLevel(debug, targetToDir, incomingFromDir, incomingToRelDir, vehicleId, incomingVehicleId);
#if DEBUG
                if (debug) {
                    Log._Debug($"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): Cars {vehicleId} and {incomingVehicleId} are on the same priority level. Checking commong priority rules. ret={ret}");
                }
#endif
            } else {
                // both vehicles are on a different priority level: prioritize vehicle on main road
                ret = onMain;
#if DEBUG
                if (debug) {
                    Log._Debug($"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): Cars {vehicleId} and {incomingVehicleId} are on a different priority. Prioritizing vehicle on main road. ret={ret}");
                }
#endif
            }

            if (ret) {
                // check if the incoming vehicle is leaving (though the target vehicle has priority)
                bool incomingIsLeaving = incomingState.junctionTransitState == VehicleJunctionTransitState.Leave;
#if DEBUG
                if (debug) {
                    Log._Debug($"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): >>> Car {vehicleId} has priority over {incomingVehicleId}. incomingIsLeaving={incomingIsLeaving}");
                }
#endif
                return !incomingIsLeaving;
            } else {
                // the target vehicle must wait
#if DEBUG
                if (debug) {
                    Log._Debug($"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): >>> Car {vehicleId} must wait for {incomingVehicleId}. returning FALSE.");
                }
#endif

                return false;
            }
        }

        /// <summary>
        /// Checks if two vehicles are on a collision course.
        /// </summary>
        /// <param name="debug">enable debugging</param>
        /// <param name="transitNodeId">junction node</param>
        /// <param name="incomingState">incoming vehicle state</param>
        /// <param name="targetToDir">absolute target vehicle destination direction</param>
        /// <param name="incomingFromDir">absolute incoming vehicle source direction</param>
        /// <param name="incomingToRelDir">relative incoming vehicle destination direction</param>
        /// <param name="vehicleId">(optional) target vehicle id</param>
        /// <param name="incomingVehicleId">(optional) incoming vehicle id</param>
        /// <returns>true if both vehicles are on a collision course, false otherwise</returns>
        public bool DetectCollision(bool debug, ref PathUnit.Position curPos, ushort transitNodeId, bool startNode, ref PathUnit.Position nextPos,
                                    ref ExtVehicle incomingState, ArrowDirection targetToDir, ArrowDirection incomingFromDir, ArrowDirection incomingToRelDir, ushort vehicleId=0, ushort incomingVehicleId=0
            ) {
            bool sameTargets = nextPos.m_segment == incomingState.nextSegmentId;
            bool wouldCollide;
            bool incomingIsLeaving = incomingState.junctionTransitState == VehicleJunctionTransitState.Leave;
            if (sameTargets) {
                // both are going to the same segment
#if DEBUG
                if (debug) {
                    Log._Debug($"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): Target and incoming are going to the same segment.");
                }
#endif

                if (nextPos.m_lane == incomingState.nextLaneIndex) {
                    // both are going to the same lane: lane order is always incorrect
#if DEBUG
                    if (debug) {
                        Log._Debug($"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): Target and incoming are going to the same segment AND lane. lane order is incorrect!");
                    }
#endif
                    wouldCollide = true;
                } else {
                    // both are going to a different lane: check lane order
#if DEBUG
                    if (debug) {
                        Log._Debug($"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): Target and incoming are going to the same segment BUT NOT to the same lane. Determining if lane order is correct.");
                    }
#endif
                    switch (targetToDir) {
                        case ArrowDirection.Left:
                        case ArrowDirection.Turn:
                        default: // (should not happen)
                            // target & incoming are going left: stay left
                            wouldCollide = !IsLaneOrderConflictFree(debug, nextPos.m_segment, transitNodeId, nextPos.m_lane, incomingState.nextLaneIndex); // stay left
#if DEBUG
                            if (debug) {
                                Log._Debug($"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): Target is going {targetToDir}. Checking if lane {nextPos.m_lane} is LEFT to {incomingState.nextLaneIndex}. would collide? {wouldCollide}");
                            }
#endif
                            break;
                        case ArrowDirection.Forward:
                            // target is going forward/turn
                            switch (incomingFromDir) {
                                case ArrowDirection.Left:
                                case ArrowDirection.Forward:
                                    // target is going forward, incoming is coming from left/forward: stay right
                                    wouldCollide = !IsLaneOrderConflictFree(debug, nextPos.m_segment, transitNodeId, incomingState.nextLaneIndex, nextPos.m_lane); // stay right
#if DEBUG
                                    if (debug) {
                                        Log._Debug($"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): Target is going {targetToDir} and incoming is coming from {incomingFromDir}. Checking if lane {nextPos.m_lane} is RIGHT to {incomingState.nextLaneIndex}. would collide? {wouldCollide}");
                                    }
#endif
                                    break;
                                case ArrowDirection.Right:
                                    // target is going forward, incoming is coming from right: stay left
                                    wouldCollide = !IsLaneOrderConflictFree(debug, nextPos.m_segment, transitNodeId, nextPos.m_lane, incomingState.nextLaneIndex); // stay left
#if DEBUG
                                    if (debug) {
                                        Log._Debug($"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): Target is going {targetToDir} and incoming is coming from {incomingFromDir}. Checking if lane {nextPos.m_lane} is LEFT to {incomingState.nextLaneIndex}. would collide? {wouldCollide}");
                                    }
#endif
                                    break;
                                case ArrowDirection.Turn: // (should not happen)
                                default: // (should not happen)
                                    wouldCollide = false;
#if DEBUG
                                    if (debug) {
                                        Log.Warning($"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): Target is going {targetToDir} and incoming is coming from {incomingFromDir} (SHOULD NOT HAPPEN). would collide? {wouldCollide}");
                                    }
#endif
                                    break;
                            }
                            break;
                        case ArrowDirection.Right:
                            // target is going right: stay right
                            wouldCollide = !IsLaneOrderConflictFree(debug, nextPos.m_segment, transitNodeId, incomingState.nextLaneIndex, nextPos.m_lane); // stay right
#if DEBUG
                            if (debug) {
                                Log._Debug($"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): Target is going RIGHT. Checking if lane {nextPos.m_lane} is RIGHT to {incomingState.nextLaneIndex}. would collide? {wouldCollide}");
                            }
#endif
                            break;
                    }
#if DEBUG
                    if (debug) {
                        Log._Debug($"    TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): >>> would collide? {wouldCollide}");
                    }
#endif
                }
            } else {
#if DEBUG
                if (debug) {
                    Log._Debug($"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): Target and incoming are going to different segments.");
                }
#endif
                switch (targetToDir) {
                    case ArrowDirection.Left:
                        switch (incomingFromDir) {
                            case ArrowDirection.Left:
                                wouldCollide = incomingToRelDir != ArrowDirection.Right;
                                break;
                            case ArrowDirection.Forward:
                                wouldCollide = incomingToRelDir != ArrowDirection.Left && incomingToRelDir != ArrowDirection.Turn;
                                break;
                            case ArrowDirection.Right:
                                wouldCollide = incomingToRelDir != ArrowDirection.Right && incomingToRelDir != ArrowDirection.Turn;
                                break;
                            default: // (should not happen)
                                wouldCollide = false;
#if DEBUG
                                if (debug) {
                                    Log.Warning($"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): Target is going {targetToDir}, incoming is coming from {incomingFromDir} and going {incomingToRelDir}. SHOULD NOT HAPPEN. would collide? {wouldCollide}");
                                }
#endif
                                break;
                        }
                        break;
                    case ArrowDirection.Forward:
                        switch (incomingFromDir) {
                            case ArrowDirection.Left:
                                wouldCollide = incomingToRelDir != ArrowDirection.Right && incomingToRelDir != ArrowDirection.Turn;
                                break;
                            case ArrowDirection.Forward:
                                wouldCollide = incomingToRelDir != ArrowDirection.Right && incomingToRelDir != ArrowDirection.Forward;
                                break;
                            case ArrowDirection.Right:
                                wouldCollide = true; // TODO allow u-turns?
                                break;
                            default: // (should not happen)
                                wouldCollide = false;
#if DEBUG
                                if (debug) {
                                    Log.Warning($"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): Target is going {targetToDir}, incoming is coming from {incomingFromDir} and going {incomingToRelDir}. SHOULD NOT HAPPEN. would collide? {wouldCollide}");
                                }
#endif
                                break;
                        }
                        break;
                    case ArrowDirection.Right:
                    case ArrowDirection.Turn:
                    default:
                        wouldCollide = false;
                        break;
                }
#if DEBUG
                if (debug) {
                    Log._Debug($"  TrafficPriorityManager.DetectCollision({vehicleId}, {incomingVehicleId}): Target is going {targetToDir}, incoming is coming from {incomingFromDir} and going {incomingToRelDir}. would collide? {wouldCollide}");
                }
#endif
            }

            return wouldCollide;
        }

        /// <summary>
        /// Check common priority rules if both vehicles are on a collision course and on the same priority level [(main AND main) OR (!main AND !main)]:
        /// 1. left yields to right
        /// 2. left-turning vehicles must yield to straight-going vehicles
        /// </summary>
        /// <param name="debug">enable debugging</param>
        /// <param name="targetToDir">absolute target vehicle destination direction</param>
        /// <param name="incomingFromDir">absolute incoming vehicle source direction</param>
        /// <param name="incomingToRelDir">relative incoming vehicle destination direction</param>
        /// <param name="vehicleId">(optional) target vehicle id</param>
        /// <param name="incomingVehicleId">(optional) incoming vehicle id</param>
        /// <returns></returns>
        public bool HasPriorityOnSameLevel(bool debug, ArrowDirection targetToDir, ArrowDirection incomingFromDir, ArrowDirection incomingToRelDir, ushort vehicleId=0, ushort incomingVehicleId=0) {
            bool ret;
            switch (incomingFromDir) {
                case ArrowDirection.Left:
                case ArrowDirection.Right:
                    // (1) left yields to right
                    ret = incomingFromDir == ArrowDirection.Left;
                    break;
                default:
                    if (incomingToRelDir == ArrowDirection.Left || incomingToRelDir == ArrowDirection.Turn) {
                        // (2) incoming vehicle must wait
                        ret = true;
                    } else if (targetToDir == ArrowDirection.Left || targetToDir == ArrowDirection.Turn) {
                        // (2) target vehicle must wait
                        ret = false;
                    } else {
                        // (should not happen)
#if DEBUG
                        if (debug) {
                            Log.Warning($"TrafficPriorityManager.HasPriorityOnSameLevel({vehicleId}, {incomingVehicleId}): targetToDir={targetToDir}, incomingFromDir={incomingFromDir}, incomingToRelDir={incomingToRelDir}: SHOULD NOT HAPPEN");
                        }
#endif
                        ret = true;
                    }
                    break;
            }

#if DEBUG
            if (debug) {
                Log._Debug($"TrafficPriorityManager.HasPriorityOnSameLevel({vehicleId}, {incomingVehicleId}): targetToDir={targetToDir}, incomingFromDir={incomingFromDir}, incomingToRelDir={incomingToRelDir}: ret={ret}");
            }
#endif

            return ret;
        }

        /// <summary>
        /// Checks if lane <paramref name="leftLaneIndex"/> lies to the left of lane <paramref name="rightLaneIndex"/>.
        /// </summary>
        /// <param name="debug">enable debugging</param>
        /// <param name="segmentId">segment id</param>
        /// <param name="nodeId">transit node id</param>
        /// <param name="leftLaneIndex">lane index that is checked to lie left</param>
        /// <param name="rightLaneIndex">lane index that is checked to lie right</param>
        /// <returns></returns>
        public bool IsLaneOrderConflictFree(bool debug, ushort segmentId, ushort nodeId, byte leftLaneIndex, byte rightLaneIndex) { // TODO refactor
            try {
                if (leftLaneIndex == rightLaneIndex) {
                    return false;
                }

                NetManager netManager = Singleton<NetManager>.instance;

                NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;

                NetInfo.Direction dir = nodeId == netManager.m_segments.m_buffer[segmentId].m_startNode ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;
                NetInfo.Direction dir2 = ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
                NetInfo.Direction dir3 = Services.SimulationService.LeftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

                NetInfo.Lane leftLane = segmentInfo.m_lanes[leftLaneIndex];
                NetInfo.Lane rightLane = segmentInfo.m_lanes[rightLaneIndex];

#if DEBUG
                if (debug) {
                    Log._Debug($"    IsLaneOrderConflictFree({segmentId}, {leftLaneIndex}, {rightLaneIndex}): dir={dir}, dir2={dir2}, dir3={dir3} laneDir={leftLane.m_direction}, leftLanePos={leftLane.m_position}, rightLanePos={rightLane.m_position}");
                }
#endif

                bool ret = (dir3 == NetInfo.Direction.Forward) ^ (leftLane.m_position < rightLane.m_position);
                return ret;
            } catch (Exception e) {
                Log.Error($"IsLaneOrderConflictFree({segmentId}, {leftLaneIndex}, {rightLaneIndex}): Error: {e.ToString()}");
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
            if (! MaySegmentHavePrioritySign(seg.segmentId, true)) {
                RemovePrioritySignFromSegmentEnd(seg.segmentId, true);
            } else {
                UpdateNode(Services.NetService.GetSegmentNodeId(seg.segmentId, true));
            }

            if (!MaySegmentHavePrioritySign(seg.segmentId, false)) {
                RemovePrioritySignFromSegmentEnd(seg.segmentId, false);
            } else {
                UpdateNode(Services.NetService.GetSegmentNodeId(seg.segmentId, false));
            }
        }

        protected override void HandleSegmentEndReplacement(SegmentEndReplacement replacement, ref ExtSegmentEnd segEnd) {
            ISegmentEndId oldSegmentEndId = replacement.oldSegmentEndId;
            ISegmentEndId newSegmentEndId = replacement.newSegmentEndId;

            PriorityType sign = PriorityType.None;
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

            Log._Debug($"TrafficPriorityManager.HandleSegmentEndReplacement({replacement}): Segment replacement detected: {oldSegmentEndId.SegmentId} -> {newSegmentEndId.SegmentId}\n" +
                       $"Moving priority sign {sign} to new segment."
                );

            SetPrioritySign(newSegmentEndId.SegmentId, newSegmentEndId.StartNode, sign);
        }

        protected void UpdateNode(ushort nodeId) {
            SetPrioritySignUnableReason reason;
            if (! MayNodeHavePrioritySigns(nodeId, out reason)) {
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
            foreach (var segment in data) {
                try {
                    if (segment.Length < 3)
                        continue;

                    if ((PriorityType)segment[2] == PriorityType.None) {
                        continue;
                    }

                    ushort nodeId = (ushort)segment[0];
                    ushort segmentId = (ushort)segment[1];
                    PriorityType sign = (PriorityType)segment[2];

                    if (!Services.NetService.IsNodeValid(nodeId)) {
                        continue;
                    }
                    if (!Services.NetService.IsSegmentValid(segmentId)) {
                        continue;
                    }

                    bool? startNode = Services.NetService.IsStartNode(segmentId, nodeId);
                    if (startNode == null) {
                        Log.Error($"TrafficPriorityManager.LoadData: No node found for node id {nodeId} @ seg. {segmentId}");
                        continue;
                    }

                    SetPrioritySign(segmentId, (bool)startNode, sign);
                } catch (Exception e) {
                    // ignore, as it's probably corrupt save data. it'll be culled on next save
                    Log.Warning("Error loading data from Priority segments: " + e.ToString());
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
            foreach (var prioSegData in data) {
                try {
                    if ((PriorityType)prioSegData.priorityType == PriorityType.None) {
                        continue;
                    }
                    if (!Services.NetService.IsNodeValid(prioSegData.nodeId)) {
                        continue;
                    }
                    if (!Services.NetService.IsSegmentValid(prioSegData.segmentId)) {
                        continue;
                    }
                    bool? startNode = Services.NetService.IsStartNode(prioSegData.segmentId, prioSegData.nodeId);
                    if (startNode == null) {
                        Log.Error($"TrafficPriorityManager.LoadData: No node found for node id {prioSegData.nodeId} @ seg. {prioSegData.segmentId}");
                        continue;
                    }

#if DEBUGLOAD
					Log._Debug($"Loading priority sign {(PriorityType)prioSegData.priorityType} @ seg. {prioSegData.segmentId}, start node? {startNode}");
#endif
                    SetPrioritySign(prioSegData.segmentId, (bool)startNode, (PriorityType)prioSegData.priorityType);
                } catch (Exception e) {
                    // ignore, as it's probably corrupt save data. it'll be culled on next save
                    Log.Warning("Error loading data from Priority segments: " + e.ToString());
                    success = false;
                }
            }
            return success;
        }

        List<Configuration.PrioritySegment> ICustomDataManager<List<Configuration.PrioritySegment>>.SaveData(ref bool success) {
            List<Configuration.PrioritySegment> ret = new List<Configuration.PrioritySegment>();
            for (uint segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                try {
                    if (! Services.NetService.IsSegmentValid((ushort)segmentId) || ! HasSegmentPrioritySign((ushort)segmentId)) {
                        continue;
                    }

                    PriorityType startSign = GetPrioritySign((ushort)segmentId, true);
                    if (startSign != PriorityType.None) {
                        ushort startNodeId = Services.NetService.GetSegmentNodeId((ushort)segmentId, true);
                        if (Services.NetService.IsNodeValid(startNodeId)) {
#if DEBUGSAVE
							Log._Debug($"Saving priority sign of type {startSign} @ start node {startNodeId} of segment {segmentId}");
#endif
                            ret.Add(new Configuration.PrioritySegment((ushort)segmentId, startNodeId, (int)startSign));
                        }
                    }

                    PriorityType endSign = GetPrioritySign((ushort)segmentId, false);
                    if (endSign != PriorityType.None) {
                        ushort endNodeId = Services.NetService.GetSegmentNodeId((ushort)segmentId, false);
                        if (Services.NetService.IsNodeValid(endNodeId)) {
#if DEBUGSAVE
							Log._Debug($"Saving priority sign of type {endSign} @ end node {endNodeId} of segment {segmentId}");
#endif
                            ret.Add(new Configuration.PrioritySegment((ushort)segmentId, endNodeId, (int)endSign));
                        }
                    }
                } catch (Exception e) {
                    Log.Error($"Exception occurred while saving priority segment @ seg. {segmentId}: {e.ToString()}");
                    success = false;
                }
            }
            return ret;
        }
    }
}