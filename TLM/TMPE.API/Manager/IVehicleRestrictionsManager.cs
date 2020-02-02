namespace TrafficManager.API.Manager {
    using System.Collections.Generic;
    using TrafficManager.API.Traffic.Enums;

    public interface IVehicleRestrictionsManager {
        // TODO documentation
        void AddAllowedType(ushort segmentId,
                            NetInfo segmentInfo,
                            uint laneIndex,
                            uint laneId,
                            NetInfo.Lane laneInfo,
                            ExtVehicleType vehicleType);

        ExtVehicleType GetAllowedVehicleTypes(ushort segmentId,
                                              ushort nodeId,
                                              VehicleRestrictionsMode busLaneMode);

        ExtVehicleType GetAllowedVehicleTypes(ushort segmentId,
                                              NetInfo segmentInfo,
                                              uint laneIndex,
                                              NetInfo.Lane laneInfo,
                                              VehicleRestrictionsMode busLaneMode);

        IDictionary<byte, ExtVehicleType> GetAllowedVehicleTypesAsDict(
            ushort segmentId,
            ushort nodeId,
            VehicleRestrictionsMode busLaneMode);

        HashSet<ExtVehicleType> GetAllowedVehicleTypesAsSet(
            ushort segmentId,
            ushort nodeId,
            VehicleRestrictionsMode busLaneMode);

        ExtVehicleType GetBaseMask(uint laneId, VehicleRestrictionsMode includeBusLanes);
        ExtVehicleType GetBaseMask(NetInfo.Lane laneInfo, VehicleRestrictionsMode includeBusLanes);

        ExtVehicleType GetDefaultAllowedVehicleTypes(NetInfo.Lane laneInfo,
                                                     VehicleRestrictionsMode busLaneMode);

        ExtVehicleType GetDefaultAllowedVehicleTypes(ushort segmentId,
                                                     NetInfo segmentInfo,
                                                     uint laneIndex,
                                                     NetInfo.Lane laneInfo,
                                                     VehicleRestrictionsMode busLaneMode);

        bool IsAllowed(ExtVehicleType? allowedTypes, ExtVehicleType vehicleType);
        bool IsBicycleAllowed(ExtVehicleType? allowedTypes);
        bool IsBlimpAllowed(ExtVehicleType? allowedTypes);
        bool IsBusAllowed(ExtVehicleType? allowedTypes);
        bool IsCableCarAllowed(ExtVehicleType? allowedTypes);
        bool IsCargoTrainAllowed(ExtVehicleType? allowedTypes);
        bool IsCargoTruckAllowed(ExtVehicleType? allowedTypes);
        bool IsEmergencyAllowed(ExtVehicleType? allowedTypes);
        bool IsFerryAllowed(ExtVehicleType? allowedTypes);
        bool IsMonorailSegment(NetInfo segmentInfo);
        bool IsPassengerCarAllowed(ExtVehicleType? allowedTypes);
        bool IsPassengerTrainAllowed(ExtVehicleType? allowedTypes);
        bool IsRailLane(NetInfo.Lane laneInfo);
        bool IsRailSegment(NetInfo segmentInfo);
        bool IsRailVehicleAllowed(ExtVehicleType? allowedTypes);
        bool IsRoadLane(NetInfo.Lane laneInfo);
        bool IsRoadSegment(NetInfo segmentInfo);
        bool IsRoadVehicleAllowed(ExtVehicleType? allowedTypes);
        bool IsServiceAllowed(ExtVehicleType? allowedTypes);
        bool IsTaxiAllowed(ExtVehicleType? allowedTypes);
        bool IsTramAllowed(ExtVehicleType? allowedTypes);
        bool IsTramLane(NetInfo.Lane laneInfo);
        void NotifyStartEndNode(ushort segmentId);
        void OnLevelUnloading();

        void RemoveAllowedType(ushort segmentId,
                               NetInfo segmentInfo,
                               uint laneIndex,
                               uint laneId,
                               NetInfo.Lane laneInfo,
                               ExtVehicleType vehicleType);

        void ToggleAllowedType(ushort segmentId,
                               NetInfo segmentInfo,
                               uint laneIndex,
                               uint laneId,
                               NetInfo.Lane laneInfo,
                               ExtVehicleType vehicleType,
                               bool add);
    }
}