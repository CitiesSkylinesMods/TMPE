// #define DEBUGFLAGS // uncomment to print verbose log.

namespace TrafficManager.State {
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Manager.Impl.LaneConnection;
    using TrafficManager.Util.Extensions;

    // [Obsolete] // I commented this on out for now to prevent warning spam
    // [issue #1476] make flags obsolete.
    public class Flags {
        public static readonly uint lfr = (uint)NetLane.Flags.LeftForwardRight;

        /// <summary>
        /// For each lane: Defines the lane arrows which are set
        /// </summary>
        private static LaneArrows?[] laneArrowFlags;

        /// <summary>
        /// For each lane: Defines the lane arrows which are set in highway rule mode (they are not saved)
        /// </summary>
        private static LaneArrows?[] highwayLaneArrowFlags;

        /// <summary>
        /// For each lane: Defines the allowed vehicle types
        /// </summary>
        internal static ExtVehicleType?[][] laneAllowedVehicleTypesArray; // for faster, lock-free access, 1st index: segment id, 2nd index: lane index

        static Flags() {
            laneAllowedVehicleTypesArray = new ExtVehicleType?[NetManager.MAX_SEGMENT_COUNT][];
            laneArrowFlags = new LaneArrows?[NetManager.MAX_LANE_COUNT];
            highwayLaneArrowFlags = new LaneArrows?[NetManager.MAX_LANE_COUNT];
        }

        /// <summary>Called from Debug Panel.</summary>
        internal static void PrintDebugInfo() {
            Log.Info("------------------------");
            Log.Info("--- LANE ARROW FLAGS ---");
            Log.Info("------------------------");
            for (uint i = 0; i < laneArrowFlags.Length; ++i) {
                ref NetLane netLane = ref i.ToLane();

                if (highwayLaneArrowFlags[i] != null || laneArrowFlags[i] != null) {
                    Log.Info($"Lane {i}: valid? {netLane.IsValidWithSegment()}");
                }

                if (highwayLaneArrowFlags[i] != null) {
                    Log.Info($"\thighway arrows: {highwayLaneArrowFlags[i]}");
                }

                if (laneArrowFlags[i] != null) {
                    Log.Info($"\tcustom arrows: {laneArrowFlags[i]}");
                }
            }

            Log.Info("---------------------------------");
            Log.Info("--- LANE VEHICLE RESTRICTIONS ---");
            Log.Info("---------------------------------");
            for (uint i = 0; i < laneAllowedVehicleTypesArray.Length; ++i) {
                ref NetSegment netSegment = ref ((ushort)i).ToSegment();

                if (laneAllowedVehicleTypesArray[i] == null)
                    continue;
                Log.Info($"Segment {i}: valid? {netSegment.IsValid()}");
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

            ref NetNode node = ref nodeId.ToNode();

            if ((node.m_flags &
                 (NetNode.Flags.Created | NetNode.Flags.Deleted)) != NetNode.Flags.Created)
            {
                // Log._Debug($"Flags: Node {nodeId} may not have a traffic light (not created).
                // flags={node.m_flags}");
                node.m_flags &= ~NetNode.Flags.TrafficLights;
                return false;
            }

            if (node.CountSegments() < 2) {
                // ignore dead-ends.
                return false;
            }


            if ((node.m_flags & NetNode.Flags.Untouchable) != NetNode.Flags.None
                && (node.m_flags & NetNode.Flags.LevelCrossing) != NetNode.Flags.None){
                // untouchable & level_crossing - Movable Bridges mod nodes, not allowed to be controlled by TMPE
                return false;
            }

            ItemClass connectionClass = node.Info.GetConnectionClass();
            if ((node.m_flags & NetNode.Flags.Junction) == NetNode.Flags.None &&
                connectionClass.m_service != ItemClass.Service.PublicTransport)
            {
                // Log._Debug($"Flags: Node {nodeId} may not have a traffic light (no junction or
                // not public transport). flags={node.m_flags}
                // connectionClass={connectionClass?.m_service}");
                node.m_flags &= ~NetNode.Flags.TrafficLights;
                return false;
            }

            if (connectionClass == null ||
                (connectionClass.m_service != ItemClass.Service.Road &&
                 connectionClass.m_service != ItemClass.Service.PublicTransport))
            {
                // Log._Debug($"Flags: Node {nodeId} may not have a traffic light (no connection class).
                // connectionClass={connectionClass?.m_service}");
                node.m_flags &= ~NetNode.Flags.TrafficLights;
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
                Log._Debug($"Adding traffic light @ node {nodeId}");
#endif
                flags |= NetNode.Flags.TrafficLights;
            } else {
#if DEBUGFLAGS
                Log._Debug($"Removing traffic light @ node {nodeId}");
#endif
                flags &= ~NetNode.Flags.TrafficLights;
            }

            node.m_flags = flags;

            return true;
        }

        internal static bool CheckLane(uint laneId) {
            // TODO refactor
            if (laneId <= 0) {
                return false;
            }

            ref NetLane netLane = ref laneId.ToLane();

            if (((NetLane.Flags)netLane.m_flags & (NetLane.Flags.Created | NetLane.Flags.Deleted)) != NetLane.Flags.Created) {
                return false;
            }

            ushort segmentId = netLane.m_segment;
            if (segmentId <= 0) {
                return false;
            }

            ref NetSegment netSegment = ref segmentId.ToSegment();

            return (netSegment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) == NetSegment.Flags.Created;
        }

        public static void SetLaneAllowedVehicleTypes(uint laneId, ExtVehicleType vehicleTypes) {
            if (laneId <= 0) {
                return;
            }

            ref NetLane netLane = ref laneId.ToLane();

            if (((NetLane.Flags)netLane.m_flags & (NetLane.Flags.Created | NetLane.Flags.Deleted)) != NetLane.Flags.Created) {
                return;
            }

            ushort segmentId = netLane.m_segment;

            if (segmentId <= 0) {
                return;
            }

            ref NetSegment netSegment = ref segmentId.ToSegment();

            if ((netSegment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                return;
            }

            NetInfo segmentInfo = netSegment.Info;
            uint curLaneId = netSegment.m_lanes;
            uint laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                if (curLaneId == laneId) {
                    SetLaneAllowedVehicleTypes(segmentId, laneIndex, laneId, vehicleTypes);
                    return;
                }

                laneIndex++;
                curLaneId = curLaneId.ToLane().m_nextLane;
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

            ref NetSegment netSegment = ref segmentId.ToSegment();

            if ((netSegment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                return;
            }

            ref NetLane netLane = ref laneId.ToLane();

            if (((NetLane.Flags)netLane.m_flags & (NetLane.Flags.Created | NetLane.Flags.Deleted)) != NetLane.Flags.Created) {
                return;
            }

            NetInfo segmentInfo = netSegment.Info;

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

            ref NetSegment netSegment = ref segmentId.ToSegment();
#if DEBUGFLAGS
            Log._Debug($"Flags.resetSegmentArrowFlags: Resetting lane arrows of segment {segmentId}.");
#endif
            NetManager netManager = Singleton<NetManager>.instance;
            NetInfo segmentInfo = netSegment.Info;
            uint curLaneId = netSegment.m_lanes;
            int numLanes = segmentInfo.m_lanes.Length;
            int laneIndex = 0;

            while (laneIndex < numLanes && curLaneId != 0u) {
#if DEBUGFLAGS
                Log._Debug($"Flags.resetSegmentArrowFlags: Resetting lane arrows of segment {segmentId}: " +
                           $"Resetting lane {curLaneId}.");
#endif
                laneArrowFlags[curLaneId] = null;

                curLaneId = curLaneId.ToLane().m_nextLane;
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
            bool? hasOutgoingConnections = null;
            try {
                hasOutgoingConnections = LaneConnectionManager.Instance.Road.HasOutgoingConnections(laneId);
            } catch (Exception _) {
                // temporary solution, HasOutgoingConnections may throw exception if some flag is incorrect
                // It does not affect what it was, since we are performing action only on success
            }

            if (hasOutgoingConnections.HasValue && hasOutgoingConnections.Value) {
                LaneArrows? arrows = LaneConnectionManager.Instance.Road.GetArrowsForOutgoingConnections(laneId);
#if DEBUGFLAGS
                Log._Debug($"Flags.resetLaneArrowFlags: Lane {laneId} has outgoing connections. Calculated Arrows: {(arrows.HasValue ? arrows.Value : "<none>")}");
#endif
                laneArrowFlags[laneId] = arrows.HasValue ? arrows.Value : null;
                return true;
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

            if (LaneConnectionManager.Instance.Road.HasOutgoingConnections(laneId, startNode)) {
                // TODO refactor
                res = SetLaneArrow_Result.LaneConnection;
                return false; // custom lane connection present
            }

            ref NetLane netLane = ref laneId.ToLane();

            LaneArrows? arrows = laneArrowFlags[laneId];
            if (arrows == null) {
                // read currently defined arrows
                uint laneFlags = netLane.m_flags;
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

            ref NetLane netLane = ref laneId.ToLane();

            if (((NetLane.Flags)netLane.m_flags & (NetLane.Flags.Created | NetLane.Flags.Deleted)) != NetLane.Flags.Created) {
                return false;
            }

            ref NetSegment netSegment = ref netLane.m_segment.ToSegment();

            const NetInfo.Direction dir = NetInfo.Direction.Forward;
            NetInfo.Direction dir2 = ((netSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None)
                ? dir
                : NetInfo.InvertDirection(dir);

            NetInfo segmentInfo = netSegment.Info;
            uint curLaneId = netSegment.m_lanes;
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
                        ? netSegment.m_startNode
                        : netSegment.m_endNode;
                    ref NetNode netNode = ref nodeId.ToNode();

                    return (netNode.m_flags & (NetNode.Flags.Created | NetNode.Flags.Deleted)) == NetNode.Flags.Created
                        && (netNode.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
                }

                curLaneId = curLaneId.ToLane().m_nextLane;
                ++laneIndex;
            }

            return false;
        }

        internal static IDictionary<uint, ExtVehicleType> GetAllLaneAllowedVehicleTypes() {
            IDictionary<uint, ExtVehicleType> ret = new Dictionary<uint, ExtVehicleType>();
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;

            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                ref NetSegment segment = ref segmentId.ToSegment();
                if ((segment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                    continue;
                }

                ExtVehicleType?[] allowedTypes = laneAllowedVehicleTypesArray[segmentId];
                if (allowedTypes == null) {
                    continue;
                }

                foreach (LaneIdAndIndex laneIdAndIndex in extSegmentManager.GetSegmentLaneIdsAndLaneIndexes(segmentId)) {
                    NetInfo.Lane laneInfo = segment.Info.m_lanes[laneIdAndIndex.laneIndex];

                    if (laneInfo.m_vehicleType == VehicleInfo.VehicleType.None) {
                        continue;
                    }

                    if (laneIdAndIndex.laneIndex >= allowedTypes.Length) {
                        continue;
                    }

                    ExtVehicleType? allowedType = allowedTypes[laneIdAndIndex.laneIndex];

                    if (allowedType == null) {
                        continue;
                    }

                    ret.Add(laneIdAndIndex.laneId, (ExtVehicleType)allowedType);
                }
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
            uint laneFlags = laneId.ToLane().m_flags;

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
            laneId.ToLane().m_flags = Convert.ToUInt16(laneFlags);
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

            ref NetLane netLane = ref laneId.ToLane();

            if (highwayLaneArrowFlags[laneId] != null) {
                return; // modification of arrows in highway rule mode is forbidden
            }

            laneArrowFlags[laneId] = null;

            // uint laneFlags = netLane.m_flags;
            if (((NetLane.Flags)netLane.m_flags & (NetLane.Flags.Created | NetLane.Flags.Deleted)) == NetLane.Flags.Created) {
                netLane.m_flags &= (ushort)~lfr;
            }
        }

        public static void ValidateLaneCustomArrows(uint laneId) {
            LaneArrows? laneArrowFlag = laneArrowFlags[laneId];
            if (!laneArrowFlag.HasValue)
                return;

            LaneArrows? outgoingAvailableLaneArrows = ExtSegmentEndManager.Instance.GetOutgoingAvailableLaneArrows(laneId);
            if (!outgoingAvailableLaneArrows.HasValue) {
                LaneArrowManager.Instance.ResetLaneArrows(laneId);
                return;
            }

            // check is laneArrowFlag is any different than available
            if ((laneArrowFlag.Value & ~outgoingAvailableLaneArrows.Value) != LaneArrows.None) {
#if DEBUGFLAGS
                Log._Debug($"Invalid custom Lane Arrows. Resetting for lane {laneId}! Available: {outgoingAvailableLaneArrows.Value} Custom: {laneArrowFlag.Value}");
#endif
                LaneArrowManager.Instance.ResetLaneArrows(laneId);
            }
        }

        internal static void RemoveHighwayLaneArrowFlagsAtSegment(ushort segmentId) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if ((netSegment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                return;
            }

            int i = 0;
            uint curLaneId = netSegment.m_lanes;

            int segmentLanesCount = netSegment.Info.m_lanes.Length;
            while (i < segmentLanesCount && curLaneId != 0u) {
                RemoveHighwayLaneArrowFlags(curLaneId);
                curLaneId = curLaneId.ToLane().m_nextLane;
                ++i;
            } // foreach lane
        }

        public static void ClearHighwayLaneArrows() {
            uint lanesCount = Singleton<NetManager>.instance.m_lanes.m_size;
            for (uint i = 0; i < lanesCount; ++i) {
                highwayLaneArrowFlags[i] = null;
            }
        }

        internal static void OnLevelUnloading() {
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