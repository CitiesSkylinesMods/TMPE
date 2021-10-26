namespace CitiesGameBridge.Factory {
    using GenericGameBridge.Factory;
    using GenericGameBridge.Service;

    public class ServiceFactory : IServiceFactory {
        public static readonly IServiceFactory Instance = new ServiceFactory();

        private ServiceFactory() { }

        public INetService NetService => Service.NetService.Instance;

        public IVehicleService VehicleService => Service.VehicleService.Instance;
    }
}