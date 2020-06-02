namespace CitiesGameBridge.Factory {
    using GenericGameBridge.Factory;
    using GenericGameBridge.Service;

    public class ServiceFactory : IServiceFactory {
        public static readonly IServiceFactory Instance = new ServiceFactory();

        private ServiceFactory() { }

        public IBuildingService BuildingService => Service.BuildingService.Instance;

        public ICitizenService CitizenService => Service.CitizenService.Instance;

        public INetService NetService => Service.NetService.Instance;

        public ISimulationService SimulationService => Service.SimulationService.Instance;

        public IVehicleService VehicleService => Service.VehicleService.Instance;
    }
}