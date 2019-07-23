﻿namespace TrafficManager.API.Manager {
    using UnityEngine;

    /// <summary>
    /// Provides path functions
    /// </summary>
    public interface IExtPathManager {
        // TODO documentation

        bool FindPathPositionWithSpiralLoop(Vector3 position,
                                            ItemClass.Service service,
                                            NetInfo.LaneType laneType,
                                            VehicleInfo.VehicleType vehicleType,
                                            NetInfo.LaneType otherLaneType,
                                            VehicleInfo.VehicleType otherVehicleType,
                                            bool allowUnderground,
                                            bool requireConnect,
                                            float maxDistance,
                                            out PathUnit.Position pathPos);

        bool FindPathPositionWithSpiralLoop(Vector3 position,
                                            Vector3? secondaryPosition,
                                            ItemClass.Service service,
                                            NetInfo.LaneType laneType,
                                            VehicleInfo.VehicleType vehicleType,
                                            NetInfo.LaneType otherLaneType,
                                            VehicleInfo.VehicleType otherVehicleType,
                                            bool allowUnderground,
                                            bool requireConnect,
                                            float maxDistance,
                                            out PathUnit.Position pathPos);

        bool FindPathPositionWithSpiralLoop(Vector3 position,
                                            ItemClass.Service service,
                                            NetInfo.LaneType laneType,
                                            VehicleInfo.VehicleType vehicleType,
                                            NetInfo.LaneType otherLaneType,
                                            VehicleInfo.VehicleType otherVehicleType,
                                            bool allowUnderground,
                                            bool requireConnect,
                                            float maxDistance,
                                            out PathUnit.Position pathPosA,
                                            out PathUnit.Position pathPosB,
                                            out float distanceSqrA,
                                            out float distanceSqrB);

        bool FindPathPositionWithSpiralLoop(Vector3 position,
                                            Vector3? secondaryPosition,
                                            ItemClass.Service service,
                                            NetInfo.LaneType laneType,
                                            VehicleInfo.VehicleType vehicleType,
                                            NetInfo.LaneType otherLaneType,
                                            VehicleInfo.VehicleType otherVehicleType,
                                            bool allowUnderground,
                                            bool requireConnect,
                                            float maxDistance,
                                            out PathUnit.Position pathPosA,
                                            out PathUnit.Position pathPosB,
                                            out float distanceSqrA,
                                            out float distanceSqrB);

        bool FindPathPositionWithSpiralLoop(Vector3 position,
                                            ItemClass.Service service,
                                            NetInfo.LaneType laneType,
                                            VehicleInfo.VehicleType vehicleType,
                                            NetInfo.LaneType otherLaneType,
                                            VehicleInfo.VehicleType otherVehicleType,
                                            VehicleInfo.VehicleType stopType,
                                            bool allowUnderground,
                                            bool requireConnect,
                                            float maxDistance,
                                            out PathUnit.Position pathPosA,
                                            out PathUnit.Position pathPosB,
                                            out float distanceSqrA,
                                            out float distanceSqrB);

        bool FindPathPositionWithSpiralLoop(Vector3 position,
                                            Vector3? secondaryPosition,
                                            ItemClass.Service service,
                                            NetInfo.LaneType laneType,
                                            VehicleInfo.VehicleType vehicleType,
                                            NetInfo.LaneType otherLaneType,
                                            VehicleInfo.VehicleType otherVehicleType,
                                            VehicleInfo.VehicleType stopType,
                                            bool allowUnderground,
                                            bool requireConnect,
                                            float maxDistance,
                                            out PathUnit.Position pathPosA,
                                            out PathUnit.Position pathPosB,
                                            out float distanceSqrA,
                                            out float distanceSqrB);
    }
}