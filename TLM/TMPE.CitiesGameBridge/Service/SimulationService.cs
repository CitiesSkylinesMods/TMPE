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

        [Obsolete]
        public bool LeftHandDrive =>
            TrafficDrivesOnLeft;

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

        public void PauseSimulation(bool forced) {
            if (forced) {
                Singleton<SimulationManager>.instance.ForcedSimulationPaused = true;
            } else {
                Singleton<SimulationManager>.instance.SimulationPaused = true;
            }
        }

        public void ResumeSimulation(bool forced) {
            if (forced) {
                Singleton<SimulationManager>.instance.ForcedSimulationPaused = false;
            } else {
                Singleton<SimulationManager>.instance.SimulationPaused = false;
            }
        }
    }
}