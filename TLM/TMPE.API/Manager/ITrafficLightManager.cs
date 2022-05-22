namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Enums;

    public interface ITrafficLightManager {
        bool HasTrafficLight(ushort nodeId);

        bool CanSetTrafficLight(ushort nodeId, bool enabled);

        bool SetTrafficLight(ushort nodeId, bool enabled);
    }
}