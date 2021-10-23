namespace CitiesGameBridge.Service {
    using System;
    using ColossalFramework;
    using ColossalFramework.Math;
    using GenericGameBridge.Service;

    public class SimulationService : ISimulationService {
        public static readonly ISimulationService Instance = new SimulationService();

        private SimulationService() { }
    }
}