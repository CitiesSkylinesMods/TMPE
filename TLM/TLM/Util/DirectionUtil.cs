using TrafficManager.Manager.Impl;
using ColossalFramework;
using CSUtil.Commons;
using TrafficManager.API.Manager;
using TrafficManager.API.Traffic.Data;

namespace TrafficManager.Util {
    public static class DirectionUtil {
        /// <summary>
        /// returns the number of all target lanes from input segment toward the secified direction.
        /// </summary>
        public static int CountTargetLanesTowardDirection(ushort segmentId, ushort nodeId, ArrowDirection dir) {
            int count = 0;

            LaneArrowManager.Instance.Services.NetService.IterateNodeSegments(
                nodeId,
                (ushort otherSegmentId, ref NetSegment otherSeg) => {
                    ArrowDirection dir2 = GetDirection(segmentId, otherSegmentId, nodeId);
                    if (dir == dir2) {
                        int forward = 0, backward = 0;
                        otherSeg.CountLanes(
                            otherSegmentId,
                            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                            VehicleInfo.VehicleType.Car,
                            ref forward,
                            ref backward);

                        bool startNode2 = otherSeg.m_startNode == nodeId;

                        if (startNode2) {
                            count += backward;
                        } else {
                            count += forward;
                        }
                    }
                    return true;
                });

            return count;
        }

        public static ArrowDirection GetDirection(ushort segment1Id, ushort segment2Id, ushort nodeId) {
            ref NetSegment segment1 = ref Singleton<NetManager>.instance.m_segments.m_buffer[segment1Id];
            bool startNode = segment1.m_startNode == nodeId;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segment1Id, startNode)];
            return segEndMan.GetDirection(ref segEnd, segment2Id);
        }

        public static bool IsOneWay(ushort segmentId) {
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

        // returns true if both roads are onewy and in the same direction.
        public static bool IsOneWay(ushort segmentId1, ushort segmentId2) {
            if(segmentId1 == 0 || segmentId2 == 0) {
                return false;
            }

            NetSegment seg1 = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId1];
            NetSegment seg2 = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId2];

            int forward1 = 0, backward1 = 0, forward2 = 0, backward2 = 0;
            seg1.CountLanes(
                    segmentId1,
                    NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                    VehicleInfo.VehicleType.Car,
                    ref forward1,
                    ref backward1);

            seg2.CountLanes(
                segmentId2,
                NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                VehicleInfo.VehicleType.Car,
                ref forward2,
                ref backward2);

            bool invert1 = (seg1.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
            bool invert2 = (seg1.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
            bool isOneWay1 = forward1 == 0 || backward1 == 0;
            bool isOneWay2 = forward2 == 0 || backward2 == 0;
            bool ret = isOneWay1 && isOneWay2;

            if (ret) {
                if (invert1 ^ invert2) {
                    ret &= backward1 == forward2 && forward1 == backward2;
                } else {
                    ret &= forward1 == forward2 && backward1 == backward2;
                }
            }

            return ret;
        }
    }
}
