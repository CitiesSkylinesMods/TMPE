namespace TrafficManager.API.Manager {
    using Connections;

    public interface IGameConnectionManager {
        IPassengerCarAIConnection PassengerCarAIConnection { get; }
        IVehicleAIConnection VehicleAIConnection { get; }
    }
}