namespace TrafficManager.Patch._VehicleAI {
    using Connection;
    using Manager.Impl;
    using State;
    using TrafficManager.Util;
    using UnityEngine;

    public class VehicleAICommons {

        private static CalculateTargetSpeedDelegate CalculateTargetSpeed = GameConnectionManager.Instance.VehicleAIConnection.CalculateTargetSpeed;
        private static NetManager _netManager = NetManager.instance;
        public static void CustomCalculateSegmentPosition(VehicleAI instance,
                                                   ushort vehicleId,
                                                   ref Vehicle vehicleData,
                                                   PathUnit.Position position,
                                                   uint laneId,
                                                   byte offset,
                                                   out Vector3 pos,
                                                   out Vector3 dir,
                                                   out float maxSpeed) {
            _netManager.m_lanes.m_buffer[laneId].CalculatePositionAndDirection(Constants.ByteToFloat(offset), out pos, out dir);
            CustomCalculateTargetSpeed(
                instance,
                vehicleId,
                ref vehicleData,
                position,
                laneId,
                position.m_segment.ToSegment().Info,
                out maxSpeed);
        }

        public static void CustomCalculateTargetSpeed(VehicleAI instance,
                                                      ushort vehicleId,
                                                      ref Vehicle vehicleData,
                                                      PathUnit.Position position,
                                                      uint laneId,
                                                      NetInfo info,
                                                      out float maxSpeed) {
            if (info.m_lanes != null && info.m_lanes.Length > position.m_lane) {
                float laneSpeedLimit = SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(
                    position.m_segment,
                    position.m_lane,
                    laneId,
                    info.m_lanes[position.m_lane]);
                maxSpeed = CalculateTargetSpeed(
                    instance,
                    vehicleId,
                    ref vehicleData,
                    laneSpeedLimit,
                    _netManager.m_lanes.m_buffer[laneId].m_curve);
            } else {
                maxSpeed = CalculateTargetSpeed(instance, vehicleId, ref vehicleData, 1f, 0f);
            }
        }
    }
}