// #define DEBUGFLAGS

namespace TrafficManager.State {
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using System.Threading;
    using System;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State.ConfigData;
    using static TrafficManager.Util.Shortcuts;

    [Obsolete]
    public class Flags {
        public static readonly uint lfr = (uint)NetLane.Flags.LeftForwardRight;

        /// <summary>
        /// For each lane: Defines the lane arrows which are set
        /// </summary>
        private static LaneArrows?[] laneArrowFlags;

        /// <summary>
        /// For each lane (by id): list of lanes that are connected with this lane by the T++ lane connector
        /// key 1: source lane id
        /// key 2: at start node?
        /// values: target lane id
        /// </summary>
        internal static uint[][][] laneConnections;

        /// <summary>
        /// For each lane: Defines the currently set speed limit
        /// </summary>
        private static Dictionary<uint, float> laneSpeedLimit; // TODO remove

        // for faster, lock-free access, 1st index: segment id, 2nd index: lane index
        internal static float?[][] laneSpeedLimitArray;

        /// <summary>
        /// For each lane: Defines the lane arrows which are set in highway rule mode (they are not saved)
        /// </summary>
        private static LaneArrows?[] highwayLaneArrowFlags;

        /// <summary>
        /// For each lane: Defines the allowed vehicle types
        /// </summary>
        internal static ExtVehicleType?[][] laneAllowedVehicleTypesArray; // for faster, lock-free access, 1st index: segment id, 2nd index: lane index

        private static object laneSpeedLimitLock = new object();

        static Flags() {
            laneConnections = new uint[NetManager.MAX_LANE_COUNT][][];
            laneSpeedLimitArray = new float?[NetManager.MAX_SEGMENT_COUNT][];
            laneSpeedLimit = new Dictionary<uint, float>();
            laneAllowedVehicleTypesArray = new ExtVehicleType?[NetManager.MAX_SEGMENT_COUNT][];
            laneArrowFlags = new LaneArrows?[NetManager.MAX_LANE_COUNT];
            highwayLaneArrowFlags = new LaneArrows?[NetManager.MAX_LANE_COUNT];
        }

        internal static void PrintDebugInfo() {
            Log.Info("------------------------");
            Log.Info("--- LANE ARROW FLAGS ---");
            Log.Info("------------------------");
            for (uint i = 0; i < laneArrowFlags.Length; ++i) {
                if (highwayLaneArrowFlags[i] != null || laneArrowFlags[i] != null) {
                    Log.Info($"Lane {i}: valid? {Constants.ServiceFactory.NetService.IsLaneAndItsSegmentValid(i)}");
                }

                if (highwayLaneArrowFlags[i] != null) {
                    Log.Info($"\thighway arrows: {highwayLaneArrowFlags[i]}");
                }

                if (laneArrowFlags[i] != null) {
                    Log.Info($"\tcustom arrows: {laneArrowFlags[i]}");
                }
            }

            Log.Info("------------------------");
            Log.Info("--- LANE CONNECTIONS ---");
            Log.Info("------------------------");
            for (uint i = 0; i < laneConnections.Length; ++i) {
                if (laneConnections[i] == null)
                    continue;

                ushort segmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[i].m_segment;
                Log.Info($"Lane {i}: valid? {Constants.ServiceFactory.NetService.IsLaneAndItsSegmentValid(i)}, seg. valid? {Constants.ServiceFactory.NetService.IsSegmentValid(segmentId)}");
                for (int x = 0; x < 2; ++x) {
                    if (laneConnections[i][x] == null)
                        continue;

                    ushort nodeId = x == 0 ? Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startNode : Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endNode;
                    Log.Info($"\tNode idx {x} ({nodeId}, seg. {segmentId}): valid? {Constants.ServiceFactory.NetService.IsNodeValid(nodeId)}");

                    for (int y = 0; y < laneConnections[i][x].Length; ++y) {
                        if (laneConnections[i][x][y] == 0)
                            continue;

                        Log.Info($"\t\tEntry {y}: {laneConnections[i][x][y]} (valid? {Constants.ServiceFactory.NetService.IsLaneAndItsSegmentValid(laneConnections[i][x][y])})");
                    }
                }
            }

            Log.Info("-------------------------");
            Log.Info("--- LANE SPEED LIMITS ---");
            Log.Info("-------------------------");
            for (uint i = 0; i < laneSpeedLimitArray.Length; ++i) {
                if (laneSpeedLimitArray[i] == null)
                    continue;
                Log.Info($"Segment {i}: valid? {Constants.ServiceFactory.NetService.IsSegmentValid((ushort)i)}");
                for (int x = 0; x < laneSpeedLimitArray[i].Length; ++x) {
                    if (laneSpeedLimitArray[i][x] == null)
                        continue;
                    Log.Info($"\tLane idx {x}: {laneSpeedLimitArray[i][x]}");
                }
            }

            Log.Info("---------------------------------");
            Log.Info("--- LANE VEHICLE RESTRICTIONS ---");
            Log.Info("---------------------------------");
            for (uint i = 0; i < laneAllowedVehicleTypesArray.Length; ++i) {
                if (laneAllowedVehicleTypesArray[i] == null)
                    continue;
                Log.Info($"Segment {i}: valid? {Constants.ServiceFactory.NetService.IsSegmentValid((ushort)i)}");
                for (int x = 0; x < laneAllowedVehicleTypesArray[i].Length; ++x) {
                    if (laneAllowedVehicleTypesArray[i][x] == null)
                        continue;
                    Log.Info($"\tLane idx {x}: {laneAllowedVehicleTypesArray[i][x]}");
                }
            }
        }

        [Obsolete]
        public static bool MayHaveTrafficLight(ushort nodeId) {
            if (nodeId <= 0) {
                return false;
            }

            if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags &
                 (NetNode.Flags.Created | NetNode.Flags.Deleted)) != NetNode.Flags.Created)
            {
                // Log._Debug($"Flags: Node {nodeId} may not have a traffic light (not created).
                // flags={Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags}");
                Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags
                    &= ~NetNode.Flags.TrafficLights;
                return false;
            }

            ItemClass connectionClass = Singleton<NetManager>
                                        .instance.m_nodes.m_buffer[nodeId].Info
                                        .GetConnectionClass();
            if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags &
                 NetNode.Flags.Junction) == NetNode.Flags.None &&
                connectionClass.m_service != ItemClass.Service.PublicTransport)
            {
                // Log._Debug($"Flags: Node {nodeId} may not have a traffic light (no junction or
                // not public transport). flags={Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags}
                // connectionClass={connectionClass?.m_service}");
                Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags
                    &= ~NetNode.Flags.TrafficLights;
                return false;
            }

            if (connectionClass == null ||
                (connectionClass.m_service != ItemClass.Service.Road &&
                 connectionClass.m_service != ItemClass.Service.PublicTransport))
            {
                // Log._Debug($"Flags: Node {nodeId} may not have a traffic light (no connection class).
                // connectionClass={connectionClass?.m_service}");
                Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags
                    &= ~NetNode.Flags.TrafficLights;
                return false;
            }

            return true;
        }

        [Obsolete]
        public static bool SetNodeTrafficLight(ushort nodeId, bool flag) {
            if (nodeId <= 0) {
                return false;
            }

#if DEBUGFLAGS
            Log._Debug($"Flags: Set node traffic light: {nodeId}={flag}");
#endif

            if (!MayHaveTrafficLight(nodeId)) {
                //Log.Warning($"Flags: Refusing to add/delete traffic light to/from node: {nodeId} {flag}");
                return false;
            }

            ref NetNode node = ref nodeId.ToNode();
            NetNode.Flags flags = node.m_flags | NetNode.Flags.CustomTrafficLights;
            if (flag) {
#if DEBUGFLAGS
                Log._Debug($"Adding traffic light @ node {nId}");
#endif
                flags |= NetNode.Flags.TrafficLights;
            } else {
#if DEBUGFLAGS
                Log._Debug($"Removing traffic light @ node {nId}");
#endif
                flags &= ~NetNode.Flags.TrafficLights;
            }

            node.m_flags = flags;

            return true;
        }

        /// <summary>
        /// Removes lane connections that point from lane <paramref name="sourceLaneId"/> to lane
        /// <paramref name="targetLaneId"/> at node <paramref name="startNode"/>.
        /// </summary>
        /// <param name="sourceLaneId"></param>
        /// <param name="targetLaneId"></param>
        /// <param name="startNode"></param>
        /// <returns></returns>
        private static bool RemoveSingleLaneConnection(uint sourceLaneId,
                                                       uint targetLaneId,
                                                       bool startNode) {
#if DEBUGFLAGS
            Log._Debug(
                $"Flags.CleanupLaneConnections({sourceLaneId}, {targetLaneId}, {startNode}) called.");
#endif
            int nodeArrayIndex = startNode ? 0 : 1;

            if (laneConnections[sourceLaneId] == null ||
                laneConnections[sourceLaneId][nodeArrayIndex] == null)
                return false;

            uint[] srcLaneConnections = laneConnections[sourceLaneId][nodeArrayIndex];

            bool ret = false;
            int remainingConnections = 0;
            for (int i = 0; i < srcLaneConnections.Length; ++i) {
                if (srcLaneConnections[i] != targetLaneId) {
                    ++remainingConnections;
                } else {
                    ret = true;
                    srcLaneConnections[i] = 0;
                }
            }

            if (remainingConnections <= 0) {
                laneConnections[sourceLaneId][nodeArrayIndex] = null;
                if (laneConnections[sourceLaneId][1 - nodeArrayIndex] == null)
                    laneConnections[sourceLaneId] = null; // total cleanup
                return ret;
            }

            if (remainingConnections != srcLaneConnections.Length) {
                laneConnections[sourceLaneId][nodeArrayIndex] = new uint[remainingConnections];
                int k = 0;
                for (int i = 0; i < srcLaneConnections.Length; ++i) {
                    if (srcLaneConnections[i] == 0)
                        continue;
                    laneConnections[sourceLaneId][nodeArrayIndex][k++] = srcLaneConnections[i];
                }
            }

            return ret;
        }

        /// <summary>
        /// Removes any lane connections that exist between two given lanes
        /// </summary>
        /// <param name="lane1Id"></param>
        /// <param name="lane2Id"></param>
        /// <param name="startNode1"></param>
        /// <returns></returns>
        internal static bool RemoveLaneConnection(uint lane1Id, uint lane2Id, bool startNode1) {
#if DEBUG
            bool debug = DebugSwitch.LaneConnections.Get();
            if (debug) {
                Log._Debug($"Flags.RemoveLaneConnection({lane1Id}, {lane2Id}, {startNode1}) called.");
            }
#endif
            bool lane1Valid = CheckLane(lane1Id);
            bool lane2Valid = CheckLane(lane2Id);

            bool ret = false;

            if (!lane1Valid) {
                // remove all incoming/outgoing lane connections
                RemoveLaneConnections(lane1Id);
                ret = true;
            }

            if (!lane2Valid) {
                // remove all incoming/outgoing lane connections
                RemoveLaneConnections(lane2Id);
                ret = true;
            }

            if (lane1Valid || lane2Valid) {
                LaneConnectionManager.Instance.GetCommonNodeId(
                    lane1Id,
                    lane2Id,
                    startNode1,
                    out ushort commonNodeId,
                    out bool startNode2); // TODO refactor
                if (commonNodeId == 0) {
                    Log.Warning($"Flags.RemoveLaneConnection({lane1Id}, {lane2Id}, {startNode1}): " +
                                $"Could not identify common node between lanes {lane1Id} and {lane2Id}");
                }

                if (RemoveSingleLaneConnection(lane1Id, lane2Id, startNode1)) {
                    ret = true;
                }

                if (RemoveSingleLaneConnection(lane2Id, lane1Id, startNode2)) {
                    ret = true;
                }
            }

#if DEBUG
            if (debug) {
                Log._Debug($"Flags.RemoveLaneConnection({lane1Id}, {lane2Id}, {startNode1}). ret={ret}");
            }
#endif
            return ret;
        }

        /// <summary>
        /// Removes all incoming/outgoing lane connections of the given lane
        /// </summary>
        /// <param name="laneId"></param>
        /// <param name="startNode"></param>
        internal static void RemoveLaneConnections(uint laneId, bool? startNode = null) {
#if DEBUG
            bool debug = DebugSwitch.LaneConnections.Get();
            if (debug) {
                Log._Debug($"Flags.RemoveLaneConnections({laneId}, {startNode}) called. " +
                           $"laneConnections[{laneId}]={laneConnections[laneId]}");
            }
#endif
            if (laneConnections[laneId] == null) {
                return;
            }

            bool laneValid = CheckLane(laneId);
            bool clearBothSides = startNode == null || !laneValid;
#if DEBUG
            if (debug) {
                Log._Debug($"Flags.RemoveLaneConnections({laneId}, {startNode}): laneValid={laneValid}, " +
                           $"clearBothSides={clearBothSides}");
            }
#endif
            int? nodeArrayIndex = null;
            if (!clearBothSides) {
                nodeArrayIndex = (bool)startNode ? 0 : 1;
            }

            for (int k = 0; k <= 1; ++k) {
                if (nodeArrayIndex != null && k != (int)nodeArrayIndex) {
                    continue;
                }

                bool startNode1 = k == 0;

                if (laneConnections[laneId][k] == null) {
                    continue;
                }

                for (int i = 0; i < laneConnections[laneId][k].Length; ++i) {
                    uint otherLaneId = laneConnections[laneId][k][i];
                    LaneConnectionManager.Instance.GetCommonNodeId(
                        laneId,
                        otherLaneId,
                        startNode1,
                        out ushort commonNodeId,
                        out bool startNode2); // TODO refactor

                    if (commonNodeId == 0) {
                        Log.Warning($"Flags.RemoveLaneConnections({laneId}, {startNode}): Could " +
                                    $"not identify common node between lanes {laneId} and {otherLaneId}");
                    }

                    RemoveSingleLaneConnection(otherLaneId, laneId, startNode2);
                }

                laneConnections[laneId][k] = null;
            }

            if (clearBothSides) {
                laneConnections[laneId] = null;
            }
        }

        /// <summary>
        /// adds lane connections between two given lanes
        /// </summary>
        /// <param name="lane1Id"></param>
        /// <param name="lane2Id"></param>
        /// <param name="startNode1"></param>
        /// <returns></returns>
        internal static bool AddLaneConnection(uint lane1Id, uint lane2Id, bool startNode1) {
            bool lane1Valid = CheckLane(lane1Id);
            bool lane2Valid = CheckLane(lane2Id);

            if (!lane1Valid) {
                // remove all incoming/outgoing lane connections
                RemoveLaneConnections(lane1Id);
            }

            if (!lane2Valid) {
                // remove all incoming/outgoing lane connections
                RemoveLaneConnections(lane2Id);
            }

            if (!lane1Valid || !lane2Valid) {
                return false;
            }

            LaneConnectionManager.Instance.GetCommonNodeId(
                lane1Id,
                lane2Id,
                startNode1,
                out ushort commonNodeId,
                out bool startNode2); // TODO refactor

            if (commonNodeId != 0) {
                CreateLaneConnection(lane1Id, lane2Id, startNode1);
                CreateLaneConnection(lane2Id, lane1Id, startNode2);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds a lane connection from lane <paramref name="sourceLaneId"/> to lane <paramref name="targetLaneId"/> at node <paramref name="startNode"/>
        /// Assumes that both lanes are valid.
        /// </summary>
        /// <param name="sourceLaneId"></param>
        /// <param name="targetLaneId"></param>
        /// <param name="startNode"></param>
        private static void CreateLaneConnection(uint sourceLaneId,
                                                 uint targetLaneId,
                                                 bool startNode) {
            if (laneConnections[sourceLaneId] == null) {
                laneConnections[sourceLaneId] = new uint[2][];
            }

            int nodeArrayIndex = startNode ? 0 : 1;

            if (laneConnections[sourceLaneId][nodeArrayIndex] == null) {
                laneConnections[sourceLaneId][nodeArrayIndex] = new uint[] { targetLaneId };
                return;
            }

            uint[] oldConnections = laneConnections[sourceLaneId][nodeArrayIndex];
            laneConnections[sourceLaneId][nodeArrayIndex] = new uint[oldConnections.Length + 1];
            Array.Copy(
                oldConnections,
                laneConnections[sourceLaneId][nodeArrayIndex],
                oldConnections.Length);
            laneConnections[sourceLaneId][nodeArrayIndex][oldConnections.Length] = targetLaneId;
        }

        internal static bool CheckLane(uint laneId) {
            // TODO refactor
            if (laneId <= 0) {
                return false;
            }

            if (((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags &
                 (NetLane.Flags.Created | NetLane.Flags.Deleted)) != NetLane.Flags.Created) {
                return false;
            }

            ushort segmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment;
            if (segmentId <= 0) {
                return false;
            }

            return (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags &
                    (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) == NetSegment.Flags.Created;
        }

        public static void SetLaneSpeedLimit(uint laneId, float? speedLimit) {
            if (!CheckLane(laneId)) {
                return;
            }

            ushort segmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment;
            NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
            uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
            uint laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                if (curLaneId == laneId) {
                    SetLaneSpeedLimit(segmentId, laneIndex, laneId, speedLimit);
                    return;
                }

                laneIndex++;
                curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
            }
        }

        public static void RemoveLaneSpeedLimit(uint laneId) {
            SetLaneSpeedLimit(laneId, null);
        }

        public static void SetLaneSpeedLimit(ushort segmentId,
                                             uint laneIndex,
                                             uint laneId,
                                             float? speedLimit) {
            if (segmentId <= 0 || laneId <= 0) {
                return;
            }

            if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags &
                 (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                return;
            }

            if (((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags &
                 (NetLane.Flags.Created | NetLane.Flags.Deleted)) != NetLane.Flags.Created) {
                return;
            }

            NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;

            if (laneIndex >= segmentInfo.m_lanes.Length) {
                return;
            }

            lock(laneSpeedLimitLock) {
#if DEBUGFLAGS
                Log._Debug(
                    $"Flags.setLaneSpeedLimit: setting speed limit of lane index {laneIndex} @ seg. " +
                    $"{segmentId} to {speedLimit}");
#endif
                if (speedLimit == null) {
                    laneSpeedLimit.Remove(laneId);

                    if (laneSpeedLimitArray[segmentId] == null) {
                        return;
                    }

                    if (laneIndex >= laneSpeedLimitArray[segmentId].Length) {
                        return;
                    }

                    laneSpeedLimitArray[segmentId][laneIndex] = null;
                } else {
                    laneSpeedLimit[laneId] = speedLimit.Value;

                    // save speed limit into the fast-access array.
                    // (1) ensure that the array is defined and large enough
                    if (laneSpeedLimitArray[segmentId] == null) {
                        laneSpeedLimitArray[segmentId] = new float?[segmentInfo.m_lanes.Length];
                    } else if (laneSpeedLimitArray[segmentId].Length < segmentInfo.m_lanes.Length) {
                        float?[] oldArray = laneSpeedLimitArray[segmentId];
                        laneSpeedLimitArray[segmentId] = new float?[segmentInfo.m_lanes.Length];
                        Array.Copy(oldArray, laneSpeedLimitArray[segmentId], oldArray.Length);
                    }

                    // (2) insert the custom speed limit
                    laneSpeedLimitArray[segmentId][laneIndex] = speedLimit;
                }
            }
        }

        public static void SetLaneAllowedVehicleTypes(uint laneId, ExtVehicleType vehicleTypes) {
            if (laneId <= 0) {
                return;
            }

            if (((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags &
                 (NetLane.Flags.Created | NetLane.Flags.Deleted)) != NetLane.Flags.Created) {
                return;
            }

            ushort segmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment;

            if (segmentId <= 0) {
                return;
            }

            if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags &
                 (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                return;
            }

            NetInfo segmentInfo =
                Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
            uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
            uint laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                if (curLaneId == laneId) {
                    SetLaneAllowedVehicleTypes(segmentId, laneIndex, laneId, vehicleTypes);
                    return;
                }

                laneIndex++;
                curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
            }
        }

        public static void SetLaneAllowedVehicleTypes(ushort segmentId,
                                                      uint laneIndex,
                                                      uint laneId,
                                                      ExtVehicleType vehicleTypes)
        {
            if (segmentId <= 0 || laneId <= 0) {
                return;
            }

            if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags &
                 (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                return;
            }

            if (((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags &
                 (NetLane.Flags.Created | NetLane.Flags.Deleted)) != NetLane.Flags.Created) {
                return;
            }

            NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;

            if (laneIndex >= segmentInfo.m_lanes.Length) {
                return;
            }

#if DEBUGFLAGS
            Log._Debug("Flags.setLaneAllowedVehicleTypes: setting allowed vehicles of lane index " +
                       $"{laneIndex} @ seg. {segmentId} to {vehicleTypes.ToString()}");
#endif

            // save allowed vehicle types into the fast-access array.
            // (1) ensure that the array is defined and large enough
            if (laneAllowedVehicleTypesArray[segmentId] == null) {
                laneAllowedVehicleTypesArray[segmentId] = new ExtVehicleType?[segmentInfo.m_lanes.Length];
            } else if (laneAllowedVehicleTypesArray[segmentId].Length <
                       segmentInfo.m_lanes.Length) {
                ExtVehicleType?[] oldArray = laneAllowedVehicleTypesArray[segmentId];
                laneAllowedVehicleTypesArray[segmentId] = new ExtVehicleType?[segmentInfo.m_lanes.Length];
                Array.Copy(oldArray, laneAllowedVehicleTypesArray[segmentId], oldArray.Length);
            }

            // (2) insert the custom speed limit
            laneAllowedVehicleTypesArray[segmentId][laneIndex] = vehicleTypes;
        }

        public static void ResetSegmentVehicleRestrictions(ushort segmentId) {
            if (segmentId <= 0) {
                return;
            }
#if DEBUGFLAGS
            Log._Debug("Flags.resetSegmentVehicleRestrictions: Resetting vehicle restrictions " +
                       $"of segment {segmentId}.");
#endif
            laneAllowedVehicleTypesArray[segmentId] = null;
        }

        public static void ResetSegmentArrowFlags(ushort segmentId) {
            if (segmentId <= 0) {
                return;
            }
#if DEBUGFLAGS
            Log._Debug($"Flags.resetSegmentArrowFlags: Resetting lane arrows of segment {segmentId}.");
#endif
            NetManager netManager = Singleton<NetManager>.instance;
            NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;
            uint curLaneId = netManager.m_segments.m_buffer[segmentId].m_lanes;
            int numLanes = segmentInfo.m_lanes.Length;
            int laneIndex = 0;

            while (laneIndex < numLanes && curLaneId != 0u) {
#if DEBUGFLAGS
                Log._Debug($"Flags.resetSegmentArrowFlags: Resetting lane arrows of segment {segmentId}: " +
                           $"Resetting lane {curLaneId}.");
#endif
                laneArrowFlags[curLaneId] = null;

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                ++laneIndex;
            }
        }

        /// <summary>
        /// removes the custom lane arrow flags. requires post recalculation.
        /// </summary>
        /// <param name="laneId"></param>
        /// <returns><c>true</c>on success, <c>false</c> otherwise</returns>
        public static bool ResetLaneArrowFlags(uint laneId) {
#if DEBUGFLAGS
            Log._Debug($"Flags.resetLaneArrowFlags: Resetting lane arrows of lane {laneId}.");
#endif
            if (LaneConnectionManager.Instance.HasConnections(laneId)) {
                return false;
            }

            laneArrowFlags[laneId] = null;
            return true;
        }

        public static bool SetLaneArrowFlags(uint laneId,
                                             LaneArrows flags,
                                             bool overrideHighwayArrows = false) {
#if DEBUGFLAGS
            Log._Debug($"Flags.setLaneArrowFlags({laneId}, {flags}, {overrideHighwayArrows}) called");
#endif

            if (!CanHaveLaneArrows(laneId)) {
#if DEBUGFLAGS
                Log._Debug($"Flags.setLaneArrowFlags({laneId}, {flags}, {overrideHighwayArrows}): " +
                           $"lane must not have lane arrows");
#endif
                RemoveLaneArrowFlags(laneId);
                return false;
            }

            if (!overrideHighwayArrows && highwayLaneArrowFlags[laneId] != null) {
#if DEBUGFLAGS
                Log._Debug($"Flags.setLaneArrowFlags({laneId}, {flags}, {overrideHighwayArrows}): " +
                           "highway arrows may not be overridden");
#endif
                return false; // disallow custom lane arrows in highway rule mode
            }

            if (overrideHighwayArrows) {
#if DEBUGFLAGS
                Log._Debug($"Flags.setLaneArrowFlags({laneId}, {flags}, {overrideHighwayArrows}): " +
                           $"overriding highway arrows");
#endif
                highwayLaneArrowFlags[laneId] = null;
            }

#if DEBUGFLAGS
            Log._Debug($"Flags.setLaneArrowFlags({laneId}, {flags}, {overrideHighwayArrows}): setting flags");
#endif
            laneArrowFlags[laneId] = flags;
            return ApplyLaneArrowFlags(laneId, false);
        }

        public static void SetHighwayLaneArrowFlags(uint laneId,
                                                    LaneArrows flags,
                                                    bool check = true) {
            if (check && !CanHaveLaneArrows(laneId)) {
                RemoveLaneArrowFlags(laneId);
                return;
            }

            highwayLaneArrowFlags[laneId] = flags;
#if DEBUGFLAGS
            Log._Debug($"Flags.setHighwayLaneArrowFlags: Setting highway arrows of lane {laneId} to {flags}");
#endif
            ApplyLaneArrowFlags(laneId, false);
        }

        public static bool ToggleLaneArrowFlags(uint laneId,
                                                bool startNode,
                                                LaneArrows flags,
                                                out SetLaneArrow_Result res) {
            if (!CanHaveLaneArrows(laneId)) {
                RemoveLaneArrowFlags(laneId);
                res = SetLaneArrow_Result.Invalid;
                return false;
            }

            if (highwayLaneArrowFlags[laneId] != null) {
                res = SetLaneArrow_Result.HighwayArrows;
                return false; // disallow custom lane arrows in highway rule mode
            }

            if (LaneConnectionManager.Instance.HasConnections(laneId, startNode)) {
                // TODO refactor
                res = SetLaneArrow_Result.LaneConnection;
                return false; // custom lane connection present
            }

            LaneArrows? arrows = laneArrowFlags[laneId];
            if (arrows == null) {
                // read currently defined arrows
                uint laneFlags = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags;
                laneFlags &= lfr; // filter arrows
                arrows = (LaneArrows)laneFlags;
            }

            arrows ^= flags;
            laneArrowFlags[laneId] = arrows;
            if (ApplyLaneArrowFlags(laneId, false)) {
                res = SetLaneArrow_Result.Success;
                return true;
            }

            res = SetLaneArrow_Result.Invalid;
            return false;
        }

        internal static bool CanHaveLaneArrows(uint laneId, bool? startNode = null) {
            if (laneId <= 0) {
                return false;
            }

            NetManager netManager = Singleton<NetManager>.instance;

            if (((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags &
                 (NetLane.Flags.Created | NetLane.Flags.Deleted)) != NetLane.Flags.Created) {
                return false;
            }

            ushort segmentId = netManager.m_lanes.m_buffer[laneId].m_segment;

            const NetInfo.Direction DIR = NetInfo.Direction.Forward;
            NetInfo.Direction dir2 =
                ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) ==
                 NetSegment.Flags.None)
                    ? DIR
                    : NetInfo.InvertDirection(DIR);

            NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;
            uint curLaneId = netManager.m_segments.m_buffer[segmentId].m_lanes;
            int numLanes = segmentInfo.m_lanes.Length;
            int laneIndex = 0;
            int wIter = 0;

            while (laneIndex < numLanes && curLaneId != 0u) {
                ++wIter;
                if (wIter >= 100) {
                    Log.Error("Too many iterations in Flags.mayHaveLaneArrows!");
                    break;
                }

                if (curLaneId == laneId) {
                    NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                    bool isStartNode = (laneInfo.m_finalDirection & dir2) == NetInfo.Direction.None;
                    if (startNode != null && isStartNode != startNode) {
                        return false;
                    }

                    ushort nodeId = isStartNode
                                        ? netManager.m_segments.m_buffer[segmentId].m_startNode
                                        : netManager.m_segments.m_buffer[segmentId].m_endNode;

                    return (netManager.m_nodes.m_buffer[nodeId].m_flags &
                            (NetNode.Flags.Created | NetNode.Flags.Deleted)) == NetNode.Flags.Created
                           && (netManager.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Junction)
                           != NetNode.Flags.None;
                }

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                ++laneIndex;
            }

            return false;
        }

        public static float? GetLaneSpeedLimit(uint laneId) {
            lock(laneSpeedLimitLock) {

                if (laneId <= 0 || !laneSpeedLimit.TryGetValue(laneId, out float speedLimit)) {
                    return null;
                }

                return speedLimit;
            }
        }

        internal static IDictionary<uint, float> GetAllLaneSpeedLimits() {
            IDictionary<uint, float> ret;
            lock(laneSpeedLimitLock) {
                ret = new Dictionary<uint, float>(laneSpeedLimit);
            }

            return ret;
        }

        internal static IDictionary<uint, ExtVehicleType> GetAllLaneAllowedVehicleTypes() {
            IDictionary<uint, ExtVehicleType> ret = new Dictionary<uint, ExtVehicleType>();

            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                // Begin local function
                bool ForEachLane(ushort segId, ref NetSegment segment) {
                    if ((segment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) !=
                        NetSegment.Flags.Created) {
                        return true;
                    }

                    ExtVehicleType?[] allowedTypes = laneAllowedVehicleTypesArray[segId];
                    if (allowedTypes == null) {
                        return true;
                    }

                    Constants.ServiceFactory.NetService.IterateSegmentLanes(
                        segId,
                        ref segment,
                        (uint laneId,
                         ref NetLane lane,
                         NetInfo.Lane laneInfo,
                         ushort sId,
                         ref NetSegment seg,
                         byte laneIndex) =>
                        {
                            if (laneInfo.m_vehicleType == VehicleInfo.VehicleType.None) {
                                return true;
                            }

                            if (laneIndex >= allowedTypes.Length) {
                                return true;
                            }

                            ExtVehicleType? allowedType = allowedTypes[laneIndex];

                            if (allowedType == null) {
                                return true;
                            }

                            ret.Add(laneId, (ExtVehicleType)allowedType);
                            return true;
                        });
                    return true;
                }

                // ↑↑↑
                // end local function
                ForEachLane(segmentId, ref segmentId.ToSegment());
            }

            return ret;
        }

        public static LaneArrows? GetLaneArrowFlags(uint laneId) {
            return laneArrowFlags[laneId];
        }

        public static LaneArrows? GetHighwayLaneArrowFlags(uint laneId) {
            return highwayLaneArrowFlags[laneId];
        }

        public static void RemoveHighwayLaneArrowFlags(uint laneId) {
#if DEBUGFLAGS
            Log._Debug(
                $"Flags.removeHighwayLaneArrowFlags: Removing highway arrows of lane {laneId}");
#endif
            if (highwayLaneArrowFlags[laneId] != null) {
                highwayLaneArrowFlags[laneId] = null;
                ApplyLaneArrowFlags(laneId, false);
            }
        }

        public static void ApplyAllFlags() {
            for (uint i = 0; i < laneArrowFlags.Length; ++i) {
                ApplyLaneArrowFlags(i);
            }
        }

        public static bool ApplyLaneArrowFlags(uint laneId, bool check = true) {
#if DEBUGFLAGS
            Log._Debug($"Flags.applyLaneArrowFlags({laneId}, {check}) called");
#endif

            if (laneId <= 0) {
                return true;
            }

            if (check && !CanHaveLaneArrows(laneId)) {
                RemoveLaneArrowFlags(laneId);
                return false;
            }

            LaneArrows? hwArrows = highwayLaneArrowFlags[laneId];
            LaneArrows? arrows = laneArrowFlags[laneId];
            uint laneFlags = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags;

            if (hwArrows != null) {
                laneFlags &= ~lfr; // remove all arrows
                laneFlags |= (uint)hwArrows; // add highway arrows
            } else if (arrows != null) {
                LaneArrows flags = (LaneArrows)arrows;
                laneFlags &= ~lfr; // remove all arrows
                laneFlags |= (uint)flags; // add desired arrows
            }

#if DEBUGFLAGS
            Log._Debug($"Flags.applyLaneArrowFlags: Setting lane flags of lane {laneId} to " +
                       $"{(NetLane.Flags)laneFlags}");
#endif
            Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags = Convert.ToUInt16(laneFlags);
            return true;
        }

        public static LaneArrows GetFinalLaneArrowFlags(uint laneId, bool check = true) {
            if (!CanHaveLaneArrows(laneId)) {
#if DEBUGFLAGS
                Log._Debug($"Lane {laneId} may not have lane arrows");
#endif
                return LaneArrows.None;
            }

            uint ret = 0;
            LaneArrows? hwArrows = highwayLaneArrowFlags[laneId];
            LaneArrows? arrows = laneArrowFlags[laneId];

            if (hwArrows != null) {
                ret &= ~lfr; // remove all arrows
                ret |= (uint)hwArrows; // add highway arrows
            } else if (arrows != null) {
                LaneArrows flags = (LaneArrows)arrows;
                ret &= ~lfr; // remove all arrows
                ret |= (uint)flags; // add desired arrows
            } else {
                ret = laneId.ToLane().m_flags;
                ret &= (uint)LaneArrows.LeftForwardRight;
            }

            return (LaneArrows)ret;
        }

        public static void RemoveLaneArrowFlags(uint laneId) {
            if (laneId <= 0) {
                return;
            }

            if (highwayLaneArrowFlags[laneId] != null) {
                return; // modification of arrows in highway rule mode is forbidden
            }

            laneArrowFlags[laneId] = null;

            // uint laneFlags = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags;
            if (((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags &
                 (NetLane.Flags.Created | NetLane.Flags.Deleted)) == NetLane.Flags.Created) {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags &= (ushort)~lfr;
            }
        }

        internal static void RemoveHighwayLaneArrowFlagsAtSegment(ushort segmentId) {
            NetSegment[] segmentsBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;

            if ((segmentsBuffer[segmentId].m_flags &
                 (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                return;
            }

            int i = 0;
            uint curLaneId = segmentsBuffer[segmentId].m_lanes;
            NetLane[] lanesBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;

            int segmentLanesCount = segmentsBuffer[segmentId].Info.m_lanes.Length;
            while (i < segmentLanesCount && curLaneId != 0u) {
                RemoveHighwayLaneArrowFlags(curLaneId);
                curLaneId = lanesBuffer[curLaneId].m_nextLane;
                ++i;
            } // foreach lane
        }

        public static void ClearHighwayLaneArrows() {
            uint lanesCount = Singleton<NetManager>.instance.m_lanes.m_size;
            for (uint i = 0; i < lanesCount; ++i) {
                highwayLaneArrowFlags[i] = null;
            }
        }

        public static void ResetSpeedLimits() {
            lock(laneSpeedLimitLock) {
                laneSpeedLimit.Clear();

                uint segmentsCount = Singleton<NetManager>.instance.m_segments.m_size;
                for (int i = 0; i < segmentsCount; ++i) {
                    laneSpeedLimitArray[i] = null;
                }
            }
        }

        internal static void OnLevelUnloading() {
            for (uint i = 0; i < laneConnections.Length; ++i) {
                laneConnections[i] = null;
            }

            for (uint i = 0; i < laneSpeedLimitArray.Length; ++i) {
                laneSpeedLimitArray[i] = null;
            }

            lock(laneSpeedLimitLock) {
                laneSpeedLimit.Clear();
            }

            for (uint i = 0; i < laneAllowedVehicleTypesArray.Length; ++i) {
                laneAllowedVehicleTypesArray[i] = null;
            }

            for (uint i = 0; i < laneArrowFlags.Length; ++i) {
                laneArrowFlags[i] = null;
            }

            for (uint i = 0; i < highwayLaneArrowFlags.Length; ++i) {
                highwayLaneArrowFlags[i] = null;
            }
        }

        public static void OnBeforeLoadData() { }
    }
}