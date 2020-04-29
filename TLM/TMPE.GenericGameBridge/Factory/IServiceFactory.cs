namespace GenericGameBridge.Factory {
    using GenericGameBridge.Service;

    public interface IServiceFactory {
        IBuildingService BuildingService { get; }

        ICitizenService CitizenService { get; }

        INetService NetService { get; }

        ISimulationService SimulationService { get; }

        IVehicleService VehicleService { get; }
    }
}