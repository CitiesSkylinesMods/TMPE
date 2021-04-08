namespace TrafficManager.Manager {
    using Connections;

    internal interface IGameConnectionManager {
        IPassengerCarAIConnection PassengerCarAIConnection { get; }
        IVehicleAIConnection VehicleAIConnection { get; }
        ITrainAIConnection TrainAIConnection { get; }
        IHumanAIConnection HumanAIConnection { get; }
        IResidentAIConnection ResidentAIConnection { get; }
        ITouristAIConnection TouristAIConnection { get; }
    }
}