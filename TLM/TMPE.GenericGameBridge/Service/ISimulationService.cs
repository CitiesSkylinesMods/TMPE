namespace GenericGameBridge.Service {
    using ColossalFramework.Math;
    using System;
    using UnityEngine;

    public interface ISimulationService {
        bool TrafficDrivesOnLeft { get; }

        uint CurrentBuildIndex { get; set; }

        uint CurrentFrameIndex { get; }

        Vector3 CameraPosition { get; }

        Randomizer Randomizer { get; }

        AsyncAction AddAction(System.Action action);
    }
}