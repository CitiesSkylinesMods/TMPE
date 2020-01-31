namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Enums;

    public interface ITrafficLightManager {
        // TODO documentation

        bool AddTrafficLight(ushort nodeId, ref NetNode node);

        bool AddTrafficLight(ushort nodeId,
                             ref NetNode node,
                             out ToggleTrafficLightError reason);

        bool HasTrafficLight(ushort nodeId, ref NetNode node);

        bool CanEnableTrafficLight(ushort nodeId,
                                     ref NetNode node,
                                     out ToggleTrafficLightError reason);

        bool CanToggleTrafficLight(ushort nodeId,
                                      bool flag,
                                      ref NetNode node,
                                      out ToggleTrafficLightError reason);

        bool RemoveTrafficLight(ushort nodeId, ref NetNode node);

        bool RemoveTrafficLight(ushort nodeId,
                                ref NetNode node,
                                out ToggleTrafficLightError reason);

        void RemoveAllExistingTrafficLights();

        bool SetTrafficLight(ushort nodeId, bool flag, ref NetNode node);

        bool SetTrafficLight(ushort nodeId,
                             bool flag,
                             ref NetNode node,
                             out ToggleTrafficLightError reason);

        bool ToggleTrafficLight(ushort nodeId, ref NetNode node);

        bool ToggleTrafficLight(ushort nodeId,
                                ref NetNode node,
                                out ToggleTrafficLightError reason);
    }
}