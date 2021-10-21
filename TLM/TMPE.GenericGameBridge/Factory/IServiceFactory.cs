namespace GenericGameBridge.Factory {
    using GenericGameBridge.Service;

    public interface IServiceFactory {
        INetService NetService { get; }

        ISimulationService SimulationService { get; }

        IVehicleService VehicleService { get; }
    }
}