namespace GenericGameBridge.Service {
    using ColossalFramework.Math;
    using System;

    public interface ISimulationService {
        bool TrafficDrivesOnLeft { get; }

        uint CurrentBuildIndex { get; set; }

        uint CurrentFrameIndex { get; }

        AsyncAction AddAction(Action action);
    }
}