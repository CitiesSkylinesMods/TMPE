namespace TrafficManager.Util;
using ColossalFramework.Math;
using UnityEngine;
using TrafficManager.Manager.Impl;
using TrafficManager.Util.Extensions;
using TrafficManager.API.Traffic.Enums;
using TrafficManager.State;

public static class StopLineUtil {
    public static NetInfo.LaneType LANE_TYPES = RoutingManager.ROUTED_LANE_TYPES;
    public static VehicleInfo.VehicleType VEHICLE_TYPES = RoutingManager.ROUTED_VEHICLE_TYPES | VehicleInfo.VehicleType.Bicycle;

    public static float ToFloatOffset(this byte offset) => offset * (1/255);
    public static byte ToByteOffset(this float t) => (byte)Mathf.Clamp(t * 255, 0, 255);

    public static bool CanConflict(NetInfo.Lane laneInfo) => laneInfo.Matches(LANE_TYPES, VEHICLE_TYPES);

    public static bool NeedsStopLineOffset(ushort segmentId, ushort nodeId) {
        if (Options.simulationAccuracy < SimulationAccuracy.VeryHigh) {
            return false;
        }

        if (nodeId.ToNode().m_flags.IsFlagSet(NetNode.Flags.TrafficLights)) {
            return false;
        }

        bool startNode = segmentId.ToSegment().IsStartNode(nodeId);
        if(JunctionRestrictionsManager.Instance.IsPedestrianCrossingAllowed(
            segmentId: segmentId,
            startNode: startNode)) {
            return false;
        }

        return nodeId.ToNode().CountSegments() == 3;
    }

    public static float CalculateStopOffset(uint laneId, uint targetLaneId, ushort nodeId, out Bezier3 trajectory, out Bezier3 stopLine) {
        trajectory = CalculateTransitionBezier(laneId, targetLaneId, nodeId);
        return CalculateStopOffset(laneId, nodeId, trajectory,  out stopLine);
    }

    public static float CalculateStopOffset(uint laneId, ushort nodeId, Bezier3 trajectory, out Bezier3 stopLine) {
        ushort segmentId = laneId.ToLane().m_segment;
        if (!GetLeftAndRightLanes(
            segmentId: segmentId,
            nodeId: nodeId,
            leftLaneId: out uint laneId1,
            leftLaneInfo: out var laneInfo1,
            rightLaneId: out uint laneId2,
            rightLaneinfo: out NetInfo.Lane laneInfo2)) {
            stopLine = default;
            return 0f;
        }
        float hw = Mathf.Max(laneInfo1.m_width, laneInfo2.m_width) * 0.5f;

        stopLine = CalculateTransitionBezier(laneId1, laneId2, nodeId);

        bool intersected = Bezier2.XZ(trajectory).Intersect(Bezier2.XZ(stopLine), out float t, out float t2, iterations: 4);
        t = trajectory.Travel(t, -.5f - hw); // stop 0.5 meter behind the stop line.
        return t;
    }

    public static bool GetLeftAndRightLanes(
        ushort segmentId, ushort nodeId, out uint leftLaneId, out NetInfo.Lane leftLaneInfo, out uint rightLaneId, out NetInfo.Lane rightLaneinfo) {
        segmentId.ToSegment().GetLeftAndRightSegments(nodeId, out ushort leftSegmentId, out ushort rightSegmentId);
        leftLaneId = GetOutermostConflictingLaneID(leftSegmentId, nodeId, true, out leftLaneInfo);
        rightLaneId = GetOutermostConflictingLaneID(rightSegmentId, nodeId, false, out rightLaneinfo);
        return leftLaneId != 0 && rightLaneId != 0;
    }

    public static uint GetOutermostConflictingLaneID(ushort segmentId, ushort nodeId, bool left, out NetInfo.Lane laneInfo) {
        ref NetSegment segment = ref segmentId.ToSegment();
        NetInfo info = segment.Info;
        bool startNode = segment.IsStartNode(nodeId);
        bool invert = segment.m_flags.IsFlagSet(NetSegment.Flags.Invert);
        bool right = left ^ startNode ^ invert;

        if (!right) {
            for (int i = 0; i < info.m_sortedLanes.Length; ++i) {
                int laneIndex = info.m_sortedLanes[i];
                laneInfo = info.m_lanes[laneIndex];
                if (CanConflict(laneInfo)) {
                    return ExtSegmentManager.Instance.GetLaneId(segmentId, laneIndex);
                }
            }
        } else {
            for (int i = info.m_sortedLanes.Length - 1; i >= 0; --i) {
                int laneIndex = info.m_sortedLanes[i];
                laneInfo = info.m_lanes[laneIndex];
                if (CanConflict(laneInfo)) {
                    laneInfo = info.m_lanes[laneIndex];
                    return ExtSegmentManager.Instance.GetLaneId(segmentId, laneIndex);
                }
            }
        }

        laneInfo = null;
        return 0;
    }

    /// <param name="direction">direction is normalized and going toward the node.</param>
    public static void GetLaneEndPositionAndDirection(uint laneId, ushort nodeID, out Vector3 position, out Vector3 direction) {
        ref NetLane lane = ref laneId.ToLane();
        if (lane.IsStartNode(nodeID)) {
            position = lane.m_bezier.a;
            direction = position - lane.m_bezier.b;
        } else {
            position = lane.m_bezier.d;
            direction = position - lane.m_bezier.c;
        }
        direction.Normalize();
    }

    public static Bezier3 CalculateTransitionBezier(uint laneId1, uint laneId2, ushort nodeId) {
        Bezier3 res = default;
        GetLaneEndPositionAndDirection(laneId1, nodeId, out res.a, out Vector3 dira);
        GetLaneEndPositionAndDirection(laneId2, nodeId, out res.d, out Vector3 dird);
        NetSegment.CalculateMiddlePoints(
            startPos: res.a,
            startDir: dira,
            endPos: res.d,
            endDir: dird,
            smoothStart: true,
            smoothEnd: true,
            middlePos1: out res.b,
            middlePos2: out res.c);
        return res;
    }

}