namespace TrafficManager.Util {
    using System.Collections.Generic;
    using ColossalFramework;
    using ColossalFramework.Math;
    using JetBrains.Annotations;
    using TrafficManager.U;
    using TrafficManager.UI;
    using UnityEngine;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;

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

        /// <summary>Extension method: Calculates bezier center for a segment.</summary>
        internal static Vector3 GetCenter(this ref NetSegment segment) {
            bool IsMiddle(ushort nodeId) => (nodeId.ToNode().m_flags & NetNode.Flags.Middle) != 0;

            Bezier3 bezier;
            bezier.a = segment.m_startNode.ToNode().GetPositionOnTerrain();
            bezier.d = segment.m_endNode.ToNode().GetPositionOnTerrain();

            NetSegment.CalculateMiddlePoints(
                startPos: bezier.a,
                startDir: segment.m_startDirection,
                endPos: bezier.d,
                endDir: segment.m_endDirection,
                smoothStart: IsMiddle(segment.m_startNode),
                smoothEnd: IsMiddle(segment.m_endNode),
                middlePos1: out bezier.b,
                middlePos2: out bezier.c);

            // Return middle point between 0.0f and 1.0f
            return bezier.Position(0.5f);
        }

        internal static bool IsBothEndsUnderground(this ref NetSegment segment) {
            return segment.m_startNode.IsUndergroundNode() && segment.m_endNode.IsUndergroundNode();
        }

        [UsedImplicitly]
        internal static bool IsEitherEndUnderground(this ref NetSegment segment) {
            return segment.m_startNode.IsUndergroundNode() || segment.m_endNode.IsUndergroundNode();
        }

        /// <summary>Gets the coordinates of the given node taking the terrain into account.</summary>
        internal static Vector3 GetPositionOnTerrain(this ref NetNode node) {
            Vector3 pos = node.m_position;
            float terrainY = Singleton<TerrainManager>.instance.SampleDetailHeightSmooth(pos);
            if (terrainY > pos.y) {
                pos.y = terrainY;
            }

            return pos;
        }

        /// <summary>
        /// Calculates the center of each group of lanes in the same directions.
        /// </summary>
        /// <param name="segmentId">The segment.</param>
        /// <param name="outputDict">output dictionary of (direction,center) pairs</param>
        /// <param name="minDistance">minimum distance allowed between
        /// centers of forward and backward directions.</param>
        internal static void CalculateSegmentCenterByDir(
            ushort segmentId,
            [NotNull] Dictionary<NetInfo.Direction, Vector3> outputDict,
            float minDistance = 0f)
        {
            outputDict.Clear();
            NetManager netManager = Singleton<NetManager>.instance;
            ref NetSegment netSegment = ref segmentId.ToSegment();

            NetInfo segmentInfo = netSegment.Info;
            uint curLaneId = netSegment.m_lanes;
            var numCentersByDir = new Dictionary<NetInfo.Direction, int>();
            uint laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                if ((segmentInfo.m_lanes[laneIndex].m_laneType &
                     (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) ==
                    NetInfo.LaneType.None) {
                    goto nextIter; //LOL
                }

                NetInfo.Direction dir = segmentInfo.m_lanes[laneIndex].m_finalDirection;
                Vector3 bezierCenter = curLaneId.ToLane().m_bezier.Position(0.5f);

                if (!outputDict.ContainsKey(dir)) {
                    outputDict[dir] = bezierCenter;
                    numCentersByDir[dir] = 1;
                } else {
                    outputDict[dir] += bezierCenter;
                    numCentersByDir[dir]++;
                }

                nextIter:

                curLaneId = curLaneId.ToLane().m_nextLane;
                laneIndex++;
            }

            foreach (KeyValuePair<NetInfo.Direction, int> e in numCentersByDir) {
                outputDict[e.Key] /= (float)e.Value;
            }

            if (minDistance > 0) {
                bool b1 = outputDict.TryGetValue(
                    key: NetInfo.Direction.Forward,
                    value: out Vector3 pos1);
                bool b2 = outputDict.TryGetValue(
                    key: NetInfo.Direction.Backward,
                    value: out Vector3 pos2);
                Vector3 diff = pos1 - pos2;
                float distance = diff.magnitude;
                if (b1 && b2 && distance < minDistance) {
                    Vector3 move = diff * ((0.5f * minDistance) / distance);
                    outputDict[NetInfo.Direction.Forward] = pos1 + move;
                    outputDict[NetInfo.Direction.Backward] = pos2 - move;
                }
            }
        }

        internal static int GetSegmentNumVehicleLanes(ushort segmentId,
                                                      ushort? nodeId,
                                                      out int numDirections,
                                                      VehicleInfo.VehicleType vehicleTypeFilter)
        {
            NetManager netManager = Singleton<NetManager>.instance;
            ref NetSegment netSegment = ref segmentId.ToSegment();

            NetInfo info = netSegment.Info;
            uint curLaneId = netSegment.m_lanes;
            var laneIndex = 0;
            NetInfo.Direction? dir2 = null;

            numDirections = 0;
            HashSet<NetInfo.Direction> directions = new HashSet<NetInfo.Direction>();

            if (nodeId != null) {
                NetInfo.Direction? dir = netSegment.m_startNode == nodeId
                    ? NetInfo.Direction.Backward
                    : NetInfo.Direction.Forward;
                dir2 = (netSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None
                    ? dir
                    : NetInfo.InvertDirection((NetInfo.Direction)dir);
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

                curLaneId = curLaneId.ToLane().m_nextLane;
                laneIndex++;
            }

            return numLanes;
        }
    } // end class
}