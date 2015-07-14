using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Traffic;

namespace TrafficManager.TrafficLight
{
    class TrafficLightsManual
    {

        public static Dictionary<int, ManualSegment> ManualSegments =
            new Dictionary<int, ManualSegment>();

        public static bool segmentIsIncomingOneWay(int segmentid, ushort nodeID)
        {
            NetManager instance = Singleton<NetManager>.instance;

            var segment = instance.m_segments.m_buffer[segmentid];
            var info = segment.Info;

            uint num2 = segment.m_lanes;
            int num3 = 0;

            NetInfo.Direction dir = NetInfo.Direction.Forward;
            if (segment.m_startNode == nodeID)
                dir = NetInfo.Direction.Backward;
            var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
            var dir3 = TrafficPriority.LeftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

            var isOneWay = true;

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian &&
                    (info.m_lanes[num3].m_direction == dir3))
                {
                    isOneWay = false;
                }

                num2 = instance.m_lanes.m_buffer[(int) ((UIntPtr) num2)].m_nextLane;
                num3++;
            }

            return isOneWay;
        }

        public static bool segmentIsOneWay(int segmentid)
        {
            NetManager instance = Singleton<NetManager>.instance;

            var segment = instance.m_segments.m_buffer[segmentid];
            var info = segment.Info;

            uint num2 = segment.m_lanes;
            int num3 = 0;

            var isOneWay = true;
            var hasForward = false;
            var hasBackward = false;

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian &&
                    (info.m_lanes[num3].m_direction == NetInfo.Direction.Forward))
                {
                    hasForward = true;
                }

                if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian &&
                    (info.m_lanes[num3].m_direction == NetInfo.Direction.Backward))
                {
                    hasBackward = true;
                }

                if (hasForward && hasBackward)
                {
                    isOneWay = false;
                    return isOneWay;
                }

                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num3++;
            }

            return isOneWay;
        }

        public static void AddSegmentLight(ushort nodeID, int segmentID, RoadBaseAI.TrafficLightState light)
        {
            if (ManualSegments.ContainsKey(segmentID))
            {
                ManualSegments[segmentID].node_2 = nodeID;
                ManualSegments[segmentID].instance_2 = new ManualSegmentLight(nodeID, segmentID, light);
            }
            else
            {
                ManualSegments.Add(segmentID, new ManualSegment());
                ManualSegments[segmentID].node_1 = nodeID;
                ManualSegments[segmentID].instance_1 = new ManualSegmentLight(nodeID, segmentID, light);
            }
        }

        public static void RemoveSegmentLight(ushort nodeID, int segmentID)
        {
            if (ManualSegments[segmentID].node_1 == nodeID)
            {
                ManualSegments[segmentID].node_1 = 0;
                ManualSegments[segmentID].instance_1 = null;
            }
            else
            {
                ManualSegments[segmentID].node_2 = 0;
                ManualSegments[segmentID].instance_2 = null;
            }

            if (ManualSegments[segmentID].node_1 == 0 && ManualSegments[segmentID].node_2 == 0)
            {
                ManualSegments.Remove(segmentID);
            }
        }

        public static bool IsSegmentLight(ushort nodeID, int segmentID)
        {
            if (ManualSegments.ContainsKey(segmentID))
            {
                var manualSegment = ManualSegments[segmentID];

                if (manualSegment.node_1 == nodeID || manualSegment.node_2 == nodeID)
                {
                    return true;
                }
            }

            return false;
        }

        public static ManualSegmentLight GetSegmentLight(ushort nodeID, int segmentID)
        {
            if (ManualSegments.ContainsKey(segmentID))
            {
                var manualSegment = ManualSegments[segmentID];

                if (manualSegment.node_1 == nodeID)
                {
                    return manualSegment.instance_1;
                }
                if (manualSegment.node_2 == nodeID)
                {
                    return manualSegment.instance_2;
                }
            }

            return null;
        }

        public static void ClearSegment(ushort nodeID, int segmentID)
        {
            var manualSegment = ManualSegments[segmentID];

            if (manualSegment.node_1 == nodeID)
            {
                manualSegment.node_1 = 0;
                manualSegment.instance_1 = null;
            }

            if (manualSegment.node_2 == nodeID)
            {
                manualSegment.node_2 = 0;
                manualSegment.instance_2 = null;
            }

            if (manualSegment.node_1 == 0 && manualSegment.node_2 == 0)
            {
                ManualSegments.Remove(segmentID);
            }
        }
    }
}
