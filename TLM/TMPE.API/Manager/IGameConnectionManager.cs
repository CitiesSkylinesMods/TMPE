namespace TrafficManager.API.Manager {
    using Connections;

    public interface IGameConnectionManager {
        IPassengerCarAIConnection PassengerCarAIConnection { get; }
        IVehicleAIConnection VehicleAIConnection { get; }
        ITrainAIConnection TrainAIConnection { get; }
        IHumanAIConnection HumanAIConnection { get; }
        IResidentAIConnection ResidentAIConnection { get; }
        ITouristAIConnection TouristAIConnection { get; }
    }
}