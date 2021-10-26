namespace GenericGameBridge.Factory {
    using GenericGameBridge.Service;

    public interface IServiceFactory {
        INetService NetService { get; }

        IVehicleService VehicleService { get; }
    }
}