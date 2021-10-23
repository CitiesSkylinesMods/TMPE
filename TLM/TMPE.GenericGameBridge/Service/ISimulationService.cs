namespace GenericGameBridge.Service {
    using ColossalFramework.Math;
    using System;

    public interface ISimulationService {
        AsyncAction AddAction(Action action);
    }
}