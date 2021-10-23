namespace GenericGameBridge.Service {
    using ColossalFramework.Math;
    using System;

    public interface ISimulationService {
        bool TrafficDrivesOnLeft { get; }

        uint CurrentFrameIndex { get; }

        AsyncAction AddAction(Action action);
    }
}