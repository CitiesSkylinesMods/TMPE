namespace GenericGameBridge.Service {
    using ColossalFramework.Math;
    using UnityEngine;

    public interface ISimulationService {
        bool LeftHandDrive { get; }

        uint CurrentBuildIndex { get; set; }

        uint CurrentFrameIndex { get; }

        Vector3 CameraPosition { get; }

        Randomizer Randomizer { get; }

        bool SimulationPaused { get; }

        bool ForcedSimulationPaused { get; }

        AsyncAction AddAction(System.Action action);

        void PauseSimulation(bool forced);

        void ResumeSimulation(bool forced);
    }
}