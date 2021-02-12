namespace TrafficManager.Traffic.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using System.Linq;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.API.Traffic;
    using TrafficManager.Geometry.Impl;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Util;

    /// <summary>
    /// A segment end describes a directional traffic segment connected to a controlled node
    /// (having custom traffic lights or priority signs).
    /// </summary>
    [Obsolete("should be removed when implementing issue #240")]
    public class SegmentEnd : SegmentEndId, ISegmentEnd {
        public SegmentEnd(ushort segmentId, bool startNode)
            : base(segmentId, startNode) {
            Update();
        }

        // TODO convert to struct
        [Obsolete]
        public ushort NodeId => Constants.ServiceFactory.NetService.GetSegmentNodeId(SegmentId, StartNode);

        private int numLanes;

        //    /// <summary>
        //    /// Vehicles that are traversing or will traverse this segment
        //    /// </summary>
        //    public ushort FirstRegisteredVehicleId { get; set; } = 0; // TODO private set

        private bool cleanupRequested = false;

        //    /// <summary>
        //    /// Vehicles that are traversing or will traverse this segment
        //    /// </summary>
        //    private ushort[] frontVehicleIds;

        /// <summary>
        /// Number of vehicles / vehicle length going to a certain segment.
        /// First key: source lane index, second key: target segment id, value: total normalized vehicle length
        /// </summary>
        private IDictionary<ushort, uint>[] numVehiclesMovingToSegmentId; // minimum speed required
        private IDictionary<ushort, uint>[] numVehiclesGoingToSegmentId; // no minimum speed required

        public override string ToString() {
            return $"[SegmentEnd {base.ToString()}\n" +
                   "\t" + $"NodeId = {NodeId}\n" +
                   "\t" + $"numLanes = {numLanes}\n" +
                   "\t" + $"cleanupRequested = {cleanupRequested}\n" +
                   "\t" + $"numVehiclesMovingToSegmentId = " +
                   (numVehiclesMovingToSegmentId == null
                        ? "<null>"
                        : numVehiclesMovingToSegmentId.ArrayToString()) + "\n" +
                   "\t" + $"numVehiclesGoingToSegmentId = " +
                   (numVehiclesGoingToSegmentId == null
                        ? "<null>"
                        : numVehiclesGoingToSegmentId.ArrayToString()) + "\n" +
                   "SegmentEnd]";
        }

        /// <summary>
        /// Calculates for each segment the number of cars going to this segment.
        /// We use integer arithmetic for better performance.
        /// </summary>
        public IDictionary<ushort, uint>[] MeasureOutgoingVehicles(
            bool includeStopped = true,
            bool logDebug = false)
        {
            // VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
            // NetManager netManager = Singleton<NetManager>.instance;
            ExtVehicleManager vehStateManager = ExtVehicleManager.Instance;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

            // TODO pre-calculate this
            uint avgSegLen = (uint)SegmentId.ToSegment().m_averageLength;

            IDictionary<ushort, uint>[] ret =
                includeStopped ? numVehiclesGoingToSegmentId : numVehiclesMovingToSegmentId;

            // reset
            for (byte laneIndex = 0; laneIndex < ret.Length; ++laneIndex) {
                IDictionary<ushort, uint> laneMetrics = ret[laneIndex];
                foreach (KeyValuePair<ushort, uint> e in laneMetrics) {
                    laneMetrics[e.Key] = 0;
                }
            }

            Log._DebugIf(
                logDebug,
                () => $"GetVehicleMetricGoingToSegment: Segment {SegmentId}, Node {NodeId}, " +
                $"includeStopped={includeStopped}.");

            int endIndex = segEndMan.GetIndex(SegmentId, StartNode);
            ushort vehicleId = segEndMan.ExtSegmentEnds[endIndex].firstVehicleId;
            int numProcessed = 0;
            int numIter = 0;

            while (vehicleId != 0) {
                Constants.ServiceFactory.VehicleService.ProcessVehicle(
                    vehicleId,
                    (ushort vId, ref Vehicle veh) => {
                        MeasureOutgoingVehicle(
                            logDebug,
                            ret,
                            includeStopped,
                            avgSegLen,
                            vehicleId,
                            ref veh,
                            ref vehStateManager.ExtVehicles[vehicleId],
                            ref numProcessed);
                        return true;
                    });

                if ((Options.simulationAccuracy <= SimulationAccuracy.Low && numProcessed >= 3) ||
                    (Options.simulationAccuracy == SimulationAccuracy.Medium && numProcessed >= 5) ||
                    (Options.simulationAccuracy == SimulationAccuracy.High && numProcessed >= 10)) {
                    break;
                }

                vehicleId = vehStateManager.ExtVehicles[vehicleId].nextVehicleIdOnSegment;

                if (++numIter > Constants.ServiceFactory.VehicleService.MaxVehicleCount) {
                    CODebugBase<LogChannel>.Error(
                        LogChannel.Core,
                        $"Invalid list detected!\n{Environment.StackTrace}");
                    break;
                }
            }

            if (logDebug) {
                string SelectFun(IDictionary<ushort, uint> e) =>
                    "[" + string.Join(
                        ", ",
                        e.Select(x => x.Key.ToString() + "=" + x.Value.ToString()).ToArray())
                        + "]";
                string result = string.Join(", ", ret.Select(SelectFun).ToArray());
                Log._Debug("GetVehicleMetricGoingToSegment: Calculation completed. " + result);
            }

            return ret;
        }

        protected void MeasureOutgoingVehicle(bool logDebug,
                                              IDictionary<ushort, uint>[] ret,
                                              bool includeStopped,
                                              uint avgSegmentLength,
                                              ushort vehicleId,
                                              ref Vehicle vehicle,
                                              ref ExtVehicle state,
                                              ref int numProcessed)
        {
            if (logDebug) {
                Log._DebugFormat(
                    " MeasureOutgoingVehicle: (Segment {0}, Node {1} (start={2})) Checking vehicle {3}. " +
                    "Coming from seg. {4}, start {5}, lane {6} going to seg. {7}, lane {8}",
                    SegmentId,
                    NodeId,
                    StartNode,
                    vehicleId,
                    state.currentSegmentId,
                    state.currentStartNode,
                    state.currentLaneIndex,
                    state.nextSegmentId,
                    state.nextLaneIndex);
            }

            if ((state.flags & ExtVehicleFlags.Spawned) == ExtVehicleFlags.None) {
                Log._DebugIf(
                    logDebug,
                    () => $" MeasureOutgoingVehicle: Vehicle {vehicleId} is unspawned. Ignoring.");
                return;
            }

#if DEBUG
            if (state.currentSegmentId != SegmentId || state.currentStartNode != StartNode) {
                if (logDebug) {
                    Log._Debug(
                        $" MeasureOutgoingVehicle: (Segment {SegmentId}, Node {NodeId} " +
                        $"(start={StartNode})) Vehicle {vehicleId} error: Segment end mismatch! {state}");
                }

                return;
            }
#endif

            if (state.nextSegmentId == 0) {
                if (logDebug) {
                    Log._Debug($" MeasureOutgoingVehicle: (Segment {SegmentId}, Node {NodeId} " +
                               $"(start={StartNode})) Vehicle {vehicleId}: Ignoring vehicle");
                }

                return;
            }

            if (state.currentLaneIndex >= ret.Length
                || !ret[state.currentLaneIndex].ContainsKey(state.nextSegmentId)) {
                if (logDebug) {
                    Log._DebugFormat(
                        " MeasureOutgoingVehicle: (Segment {0}, Node {1} (start={2})) Vehicle {3} is " +
                        "on lane {4} and wants to go to segment {5} but one or both are invalid: {6}",
                        SegmentId,
                        NodeId,
                        StartNode,
                        vehicleId,
                        state.currentLaneIndex,
                        state.nextSegmentId,
                        ret.CollectionToString());
                }

                return;
            }

            if (!includeStopped && vehicle.GetLastFrameVelocity().sqrMagnitude
                < GlobalConfig.Instance.PriorityRules.MaxStopVelocity
                    * GlobalConfig.Instance.PriorityRules.MaxStopVelocity)
            {
                if (logDebug) {
                    Log._DebugFormat(
                        "  MeasureOutgoingVehicle: (Segment {0}, Node {1}) Vehicle {2}: too slow ({3})",
                        SegmentId,
                        NodeId,
                        vehicleId,
                        vehicle.GetLastFrameVelocity().sqrMagnitude);
                }

                ++numProcessed;
                return;
            }

            uint normLength = 10u;

            if (avgSegmentLength > 0) {
                // TODO +1 because the vehicle length calculation for trains/monorail in the method VehicleState.OnVehicleSpawned returns 0 (or a very small number maybe?)
                normLength = Math.Min(100u, (uint)(Math.Max(1u, state.totalLength) * 100u) / avgSegmentLength) + 1;
            }

            if (logDebug) {
                Log._DebugFormat(
                    "  MeasureOutgoingVehicle: (Segment {0}, Node {1}) NormLength of vehicle {2}: " +
                    "{3} -> {4} (avgSegmentLength={5})",
                    SegmentId,
                    NodeId,
                    vehicleId,
                    state.totalLength,
                    normLength,
                    avgSegmentLength);
            }

            ret[state.currentLaneIndex][state.nextSegmentId] += normLength;
            ++numProcessed;

            if (logDebug) {
                Log._DebugFormat(
                    "  MeasureOutgoingVehicle: (Segment {0}, Node {1}) Vehicle {2}: ***ADDED*** " +
                    "({3}@{4} -> {5}@{6})!",
                    SegmentId,
                    NodeId,
                    vehicleId,
                    state.currentSegmentId,
                    state.currentLaneIndex,
                    state.nextSegmentId,
                    state.nextLaneIndex);
            }
        }

        [UsedImplicitly]
        public uint GetRegisteredVehicleCount() {
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            return segEndMan.GetRegisteredVehicleCount(
                ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(SegmentId, StartNode)]);
        }

        // public void Destroy() {
        //        UnregisterAllVehicles();
        // }

        // private void UnregisterAllVehicles() {
        //        ExtVehicleManager extVehicleMan = ExtVehicleManager.Instance;
        //        IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
        //
        //        int endIndex = segEndMan.GetIndex(SegmentId, StartNode);
        //        int numIter = 0;
        //        while (segEndMan.ExtSegmentEnds[endIndex].firstVehicleId != 0) {
        //                extVehicleMan.Unlink(ref extVehicleMan.ExtVehicles[segEndMan.ExtSegmentEnds
        //                 [endIndex].firstVehicleId]);
        //                if (++numIter > Constants.ServiceFactory.VehicleService.MaxVehicleCount) {
        //                        CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n"
        //                         + Environment.StackTrace);
        //                        break;
        //                }
        //        }
        // }

        public void Update() {
            ref NetSegment segment = ref SegmentId.ToSegment();
            StartNode = segment.m_startNode == NodeId;
            numLanes = segment.Info.m_lanes.Length;

            if (!Constants.ServiceFactory.NetService.IsSegmentValid(SegmentId)) {
                Log.Error($"SegmentEnd.Update: Segment {SegmentId} is invalid.");
                return;
            }

            RebuildVehicleNumDicts(ref NodeId.ToNode());
        }

        private void RebuildVehicleNumDicts(ref NetNode node) {
            numVehiclesMovingToSegmentId = new TinyDictionary<ushort, uint>[numLanes];
            numVehiclesGoingToSegmentId = new TinyDictionary<ushort, uint>[numLanes];

            Constants.ServiceFactory.NetService.IterateSegmentLanes(
                SegmentId,
                (uint laneId,
                 ref NetLane lane,
                 NetInfo.Lane laneInfo,
                 ushort segmentId,
                 ref NetSegment segment,
                 byte laneIndex) =>
                {
                    IDictionary<ushort, uint> numVehicleMoving = new TinyDictionary<ushort, uint>();
                    IDictionary<ushort, uint> numVehicleGoing = new TinyDictionary<ushort, uint>();

                    numVehiclesMovingToSegmentId[laneIndex] = numVehicleMoving;
                    numVehiclesGoingToSegmentId[laneIndex] = numVehicleGoing;

                    return true;
                });

            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

            for (int i = 0; i < 8; ++i) {
                ushort segId = node.GetSegment(i);
                if (segId == 0) {
                    continue;
                }

                int index0 = segEndMan.GetIndex(
                    segId,
                    (bool)Constants.ServiceFactory.NetService.IsStartNode(segId, NodeId));

                if (!segEndMan.ExtSegmentEnds[index0].outgoing) {
                    continue;
                }

                foreach (TinyDictionary<ushort, uint> numVehiclesMovingToSegId in numVehiclesMovingToSegmentId) {
                    numVehiclesMovingToSegId[segId] = 0;
                }

                foreach (TinyDictionary<ushort, uint> numVehiclesGoingToSegId in numVehiclesGoingToSegmentId) {
                    numVehiclesGoingToSegId[segId] = 0;
                }
            }
        } // end RebuildVehicleNumDicts
    } // end class
}