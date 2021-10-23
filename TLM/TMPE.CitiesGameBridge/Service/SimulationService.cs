namespace CitiesGameBridge.Service {
    using System;
    using ColossalFramework;
    using ColossalFramework.Math;
    using GenericGameBridge.Service;

    public class SimulationService : ISimulationService {
        public static readonly ISimulationService Instance = new SimulationService();

        private SimulationService() { }

        public bool TrafficDrivesOnLeft =>
            Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic
            == SimulationMetaData.MetaBool.True;

        public AsyncAction AddAction(Action action) {
            return Singleton<SimulationManager>.instance.AddAction(action);
        }
    }
}