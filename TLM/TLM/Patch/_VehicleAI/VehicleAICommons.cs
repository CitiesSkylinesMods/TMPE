namespace TrafficManager.Patch._VehicleAI {
    using Connection;
    using Manager.Impl;
    using State;
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
            NetInfo info = _netManager.m_segments.m_buffer[position.m_segment].Info;
            CustomCalculateTargetSpeed(instance, vehicleId, ref vehicleData, position, laneId, info, out maxSpeed);
        }

        public static void CustomCalculateTargetSpeed(VehicleAI instance,
                                                      ushort vehicleId,
                                                      ref Vehicle vehicleData,
                                                      PathUnit.Position position,
                                                      uint laneId,
                                                      NetInfo info,
                                                      out float maxSpeed) {
            if (info.m_lanes != null && info.m_lanes.Length > position.m_lane) {
                float laneSpeedLimit = Options.customSpeedLimitsEnabled
                                           ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(
                                               position.m_segment,
                                               position.m_lane,
                                               laneId,
                                               info.m_lanes[position.m_lane])
                                           : info.m_lanes[position.m_lane].m_speedLimit; // NON-STOCK CODE
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