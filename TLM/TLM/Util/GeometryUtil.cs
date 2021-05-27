namespace TrafficManager.Util {
    using System.Collections.Generic;
    using ColossalFramework;
    using TrafficManager.U;
    using TrafficManager.UI;
    using UnityEngine;

    /// <summary>
    /// Helper functions to handle coordinates and transformations, as well as road geometry
    /// calculations.
    /// </summary>
    public static class GeometryUtil {
        /// <summary>Transforms a world point into a screen point.</summary>
        /// <param name="worldPos">Position in the world.</param>
        /// <param name="screenPos">2d position on screen.</param>
        /// <returns>
        /// Screen point in pixels. Note: For use in UI transform to GUI coords
        /// use <see cref="UIScaler.ScreenPointToGuiPoint"/>.
        /// </returns>
        internal static bool WorldToScreenPoint(Vector3 worldPos, out Vector3 screenPos) {
            screenPos = InGameUtil.Instance.CachedMainCamera.WorldToScreenPoint(worldPos);
            screenPos.y = Screen.height - screenPos.y;

            return screenPos.z >= 0;
        }

        /// <summary>
        /// Calculates the center of each group of lanes in the same directions.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="segmentCenterByDir">output dictionary of (direction,center) pairs</param>
        /// <param name="minDistance">minimum distance allowed between
        /// centers of forward and backward directions.
        internal static void CalculateSegmentCenterByDir(ushort segmentId,
                                                         Dictionary<NetInfo.Direction, Vector3>
                                                             segmentCenterByDir,
                                                         float minDistance = 0f)
        {
            segmentCenterByDir.Clear();
            NetManager netManager = Singleton<NetManager>.instance;

            NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;
            uint curLaneId = netManager.m_segments.m_buffer[segmentId].m_lanes;
            var numCentersByDir =
                new Dictionary<NetInfo.Direction, int>();
            uint laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                if ((segmentInfo.m_lanes[laneIndex].m_laneType &
                     (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) ==
                    NetInfo.LaneType.None) {
                    goto nextIter;
                }

                NetInfo.Direction dir = segmentInfo.m_lanes[laneIndex].m_finalDirection;
                Vector3 bezierCenter =
                    netManager.m_lanes.m_buffer[curLaneId].m_bezier.Position(0.5f);

                if (!segmentCenterByDir.ContainsKey(dir)) {
                    segmentCenterByDir[dir] = bezierCenter;
                    numCentersByDir[dir] = 1;
                } else {
                    segmentCenterByDir[dir] += bezierCenter;
                    numCentersByDir[dir]++;
                }

                nextIter:

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                laneIndex++;
            }

            foreach (KeyValuePair<NetInfo.Direction, int> e in numCentersByDir) {
                segmentCenterByDir[e.Key] /= (float)e.Value;
            }

            if (minDistance > 0) {
                bool b1 = segmentCenterByDir.TryGetValue(
                    NetInfo.Direction.Forward,
                    out Vector3 pos1);
                bool b2 = segmentCenterByDir.TryGetValue(
                    NetInfo.Direction.Backward,
                    out Vector3 pos2);
                Vector3 diff = pos1 - pos2;
                float distance = diff.magnitude;
                if (b1 && b2 && distance < minDistance) {
                    Vector3 move = diff * ((0.5f * minDistance) / distance);
                    segmentCenterByDir[NetInfo.Direction.Forward] = pos1 + move;
                    segmentCenterByDir[NetInfo.Direction.Backward] = pos2 - move;
                }
            }
        }

        internal static int GetSegmentNumVehicleLanes(ushort segmentId,
                                                      ushort? nodeId,
                                                      out int numDirections,
                                                      VehicleInfo.VehicleType vehicleTypeFilter)
        {
            NetManager netManager = Singleton<NetManager>.instance;

            NetInfo info = netManager.m_segments.m_buffer[segmentId].Info;
            uint curLaneId = netManager.m_segments.m_buffer[segmentId].m_lanes;
            var laneIndex = 0;

            NetInfo.Direction? dir2 = null;
            // NetInfo.Direction? dir3 = null;

            numDirections = 0;
            HashSet<NetInfo.Direction> directions = new HashSet<NetInfo.Direction>();

            if (nodeId != null) {
                NetInfo.Direction? dir = netManager.m_segments.m_buffer[segmentId].m_startNode == nodeId
                                             ? NetInfo.Direction.Backward
                                             : NetInfo.Direction.Forward;
                dir2 =
                    (netManager.m_segments.m_buffer[segmentId].m_flags &
                     NetSegment.Flags.Invert) == NetSegment.Flags.None
                        ? dir
                        : NetInfo.InvertDirection((NetInfo.Direction)dir);

                // dir3 = TrafficPriorityManager.IsLeftHandDrive()
                //      ? NetInfo.InvertDirection((NetInfo.Direction)dir2) : dir2;
            }

            var numLanes = 0;

            while (laneIndex < info.m_lanes.Length && curLaneId != 0u) {
                if ((info.m_lanes[laneIndex].m_laneType &
                     (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None
                    && (info.m_lanes[laneIndex].m_vehicleType & vehicleTypeFilter) != VehicleInfo.VehicleType.None
                    && (dir2 == null || info.m_lanes[laneIndex].m_finalDirection == dir2))
                {
                    if (!directions.Contains(info.m_lanes[laneIndex].m_finalDirection)) {
                        directions.Add(info.m_lanes[laneIndex].m_finalDirection);
                        ++numDirections;
                    }

                    numLanes++;
                }

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                laneIndex++;
            }

            return numLanes;
        }
    } // end class
}