namespace TrafficManager.API.Manager {
    using System.Collections.Generic;

    public interface ITrafficLightSimulationManager {
        bool HasTimedSimulation(ushort nodeId);

        bool HasActiveTimedSimulation(ushort nodeId);
    }
}