namespace CitiesGameBridge.Service {
    using System;
    using ColossalFramework;
    using ColossalFramework.Math;
    using GenericGameBridge.Service;
    using UnityEngine;

    public class SimulationService : ISimulationService {
        public static readonly ISimulationService Instance = new SimulationService();

        private SimulationService() { }

        public bool TrafficDrivesOnLeft =>
            Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic
            == SimulationMetaData.MetaBool.True;

        public uint CurrentBuildIndex {
            get => Singleton<SimulationManager>.instance.m_currentBuildIndex;
            set => Singleton<SimulationManager>.instance.m_currentBuildIndex = value;
        }

        public uint CurrentFrameIndex => Singleton<SimulationManager>.instance.m_currentFrameIndex;

        public Vector3 CameraPosition =>
            Singleton<SimulationManager>.instance.m_simulationView.m_position;

        public Randomizer Randomizer => Singleton<SimulationManager>.instance.m_randomizer;

        public bool SimulationPaused => Singleton<SimulationManager>.instance.SimulationPaused;

        public bool ForcedSimulationPaused =>
            Singleton<SimulationManager>.instance.ForcedSimulationPaused;

        public AsyncAction AddAction(Action action) {
            return Singleton<SimulationManager>.instance.AddAction(action);
        }
    }
}