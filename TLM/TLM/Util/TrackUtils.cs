namespace TrafficManager.Util {
    using ColossalFramework.Math;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Util.Extensions;
    using UnityEngine;

    internal static class TrackUtils {
        internal const NetInfo.LaneType TRACK_LANE_TYPES =
            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;

        internal const VehicleInfo.VehicleType TRACK_VEHICLE_TYPES =
            VehicleInfo.VehicleType.Metro |
            VehicleInfo.VehicleType.Train |
            VehicleInfo.VehicleType.Tram |
            VehicleInfo.VehicleType.Monorail;

        internal const NetInfo.LaneType ROAD_LANE_TYPES = LaneArrowManager.LANE_TYPES;

        internal const VehicleInfo.VehicleType ROAD_VEHICLE_TYPES = LaneArrowManager.VEHICLE_TYPES | VehicleInfo.VehicleType.Trolleybus;

        internal static bool IsTrackOnly(this NetInfo.Lane laneInfo) {
            return
                laneInfo != null &&
                laneInfo.m_laneType.IsFlagSet(TRACK_LANE_TYPES) &&
                !laneInfo.m_laneType.IsFlagSet(~TRACK_LANE_TYPES) &&
                laneInfo.m_vehicleType.IsFlagSet(TRACK_VEHICLE_TYPES) &&
                !laneInfo.m_vehicleType.IsFlagSet(~TRACK_VEHICLE_TYPES);
        }

        /// <summary>
        /// checks if vehicles move backward or bypass backward (considers LHT)
        /// </summary>
        /// <returns>true if vehicles move backward including AvoidForward,
        /// false if vehicles going forward, bi-directional, or non-directional</returns>
        internal static bool IsGoingBackward(this NetInfo.Direction direction) =>
            (direction & NetInfo.Direction.Both) == NetInfo.Direction.Backward ||
            (direction & NetInfo.Direction.AvoidBoth) == NetInfo.Direction.AvoidForward;

        /// <summary>
        /// checks if vehicles move forward or bypass forward (considers LHT)
        /// </summary>
        /// <returns>true if vehicles move forward including AvoidBackward,
        /// false if vehicles going backward, bi-directional, or non-directional</returns>
        internal static bool IsGoingForward(this NetInfo.Direction direction) =>
        (direction & NetInfo.Direction.Both) == NetInfo.Direction.Forward ||
            (direction & NetInfo.Direction.AvoidBoth) == NetInfo.Direction.AvoidBackward;

        /// <summary>
        /// checks if vehicles move backward or bypass backward (considers LHT)
        /// </summary>
        /// <returns>true if vehicles move backward including AvoidForward,
        /// false if vehicles going ward, bi-directional, or non-directional</returns>
        internal static bool IsGoingBackward(this NetInfo.Lane laneInfo) =>
            laneInfo.m_finalDirection.IsGoingBackward();

        /// <summary>
        /// checks if vehicles move forward or bypass forward (considers LHT)
        /// </summary>
        /// <returns>true if vehicles move forward including AvoidBackward,
        /// false if vehicles going backward, bi-directional, or non-directional</returns>
        internal static bool IsGoingForward(this NetInfo.Lane laneInfo) =>
            laneInfo.m_finalDirection.IsGoingForward();

        /// <summary>
        /// Checks if the turning angle between two segments at the given node is within bounds.
        /// </summary>
        internal static bool CheckSegmentsTurnAngle(
            ref NetSegment sourceSegment,
            ref NetSegment targetSegment,
            ushort nodeId) {
            float turningAngle = 1f;
            if (!targetSegment.m_overridePathFindDirectionLimit) {
                turningAngle = 0.01f - Mathf.Min(sourceSegment.Info.m_maxTurnAngleCos, targetSegment.Info.m_maxTurnAngleCos);
            }
            if ((targetSegment.m_flags2 & NetSegment.Flags2.ForbidTurn) != 0 &&
                !nodeId.ToNode().m_flags.IsFlagSet(NetNode.Flags.End)) {
                turningAngle = -0.99f;
            }
            if (turningAngle < 1f) {
                Vector3 sourceDirection = sourceSegment.GetDirection(nodeId);
                Vector3 targetDirection = targetSegment.GetDirection(nodeId);
                float dot = VectorUtils.DotXZ(sourceDirection, targetDirection);
                return dot < turningAngle;
            }

            return true;
        }

        public static LaneEndTransitionGroup GetLaneEndTransitionGroup(VehicleInfo.VehicleType vehicleType) {
            LaneEndTransitionGroup ret = 0;
            if (vehicleType.IsFlagSet(ROAD_VEHICLE_TYPES))
                ret |= LaneEndTransitionGroup.Road;
            if (vehicleType.IsFlagSet(TRACK_VEHICLE_TYPES))
                ret |= LaneEndTransitionGroup.Track;
            return ret;
        }
    }
}
