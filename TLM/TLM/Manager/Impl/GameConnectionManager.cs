namespace TrafficManager.Manager.Impl {
    using Connections;
    using Patch._CitizenAI._HumanAI.Connection;
    using Patch._CitizenAI._ResidentAI.Connection;
    using Patch._CitizenAI._TouristAI.Connection;
    using Patch._VehicleAI.Connection;
    using Patch._VehicleAI._PassengerCarAI.Connection;
    using Patch._VehicleAI._TrainAI.Connection;

    internal class GameConnectionManager: IGameConnectionManager {

        internal static GameConnectionManager Instance;
        static GameConnectionManager() {
            Instance = new GameConnectionManager();
        }

        GameConnectionManager() {
            PassengerCarAIConnection = PassengerCarAIHook.GetConnection();
            VehicleAIConnection = VehicleAIHook.GetConnection();
            TrainAIConnection = TrainAIHook.GetConnection();
            HumanAIConnection = HumanAIHook.GetConnection();
            ResidentAIConnection = ResidentAIHook.GetConnection();
            TouristAIConnection = TouristAIHook.GetConnection();
        }

        public IPassengerCarAIConnection PassengerCarAIConnection { get; }
        public IVehicleAIConnection VehicleAIConnection { get; }
        public ITrainAIConnection TrainAIConnection { get; }
        public IHumanAIConnection HumanAIConnection { get; }
        public IResidentAIConnection ResidentAIConnection { get; }
        public ITouristAIConnection TouristAIConnection { get; }
    }
}