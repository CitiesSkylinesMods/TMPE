namespace GenericGameBridge.Service {
    using ColossalFramework.Math;
    using System;
    using UnityEngine;

    public interface ISimulationService {

        [Obsolete]
        bool LeftHandDrive { get; } // remove as part of Issue #577

        bool TrafficDrivesOnLeft { get; }

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