namespace TrafficManager.API.TrafficLight {
    using System;
    using System.Collections.Generic;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic;
    using TrafficManager.API.Traffic.Enums;

    public interface ICustomSegmentLights : ICloneable, ISegmentEndId {
        // TODO documentation
        ushort NodeId { get; }
        IDictionary<ExtVehicleType, ICustomSegmentLight> CustomLights { get; }

        RoadBaseAI.TrafficLightState
            AutoPedestrianLightState { get; set; } // TODO should not be writable

        bool InvalidPedestrianLight { get; set; } // TODO improve & remove
        RoadBaseAI.TrafficLightState? PedestrianLightState { get; set; }
        RoadBaseAI.TrafficLightState? InternalPedestrianLightState { get; }
        bool ManualPedestrianMode { get; set; }
        LinkedList<ExtVehicleType> VehicleTypes { get; } // TODO improve & remove
        ExtVehicleType?[] VehicleTypeByLaneIndex { get; }

        void CalculateAutoPedestrianLightState(ref NetNode node, bool propagate = true);
        bool IsAnyGreen();
        bool IsAnyInTransition();
        bool IsAnyLeftGreen();
        bool IsAnyMainGreen();
        bool IsAnyRightGreen();
        bool IsAllLeftRed();
        bool IsAllMainRed();
        bool IsAllRightRed();
        void UpdateVisuals();
        uint LastChange();
        void MakeRed();
        void MakeRedOrGreen();
        void ChangeLightPedestrian();
        void SetLights(RoadBaseAI.TrafficLightState lightState);
        void SetLights(ICustomSegmentLights otherLights);
        ICustomSegmentLight GetCustomLight(byte laneIndex);
        ICustomSegmentLight GetCustomLight(ExtVehicleType vehicleType);
        bool Relocate(ushort segmentId, bool startNode, ICustomSegmentLightsManager lightsManager);

        ICustomSegmentLights Clone(ICustomSegmentLightsManager newLightsManager,
                                   bool performHousekeeping = true);

        void Housekeeping(bool mayDelete, bool calculateAutoPedLight);
    }
}