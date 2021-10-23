namespace GenericGameBridge.Service {
    using ColossalFramework.Math;
    using System;

    public interface ISimulationService {
        bool TrafficDrivesOnLeft { get; }

        AsyncAction AddAction(Action action);
    }
}