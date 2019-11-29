namespace GenericGameBridge.Service {
    using ColossalFramework.Math;
    using System;
    using UnityEngine;

    public interface ISimulationService {
        /// <summary>
        /// The implementation of this property confuses Left hand drive and left hand traffic.
        /// </summary>
        [Obsolete]
        bool LeftHandDrive { get; } // Issue #577

        bool LeftHandTraffic { get; }

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