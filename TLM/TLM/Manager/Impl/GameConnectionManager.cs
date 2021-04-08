namespace TrafficManager.Manager.Impl {
    using Patch._CitizenAI._HumanAI.Connection;
    using Patch._CitizenAI._ResidentAI.Connection;
    using Patch._CitizenAI._TouristAI.Connection;
    using Patch._VehicleAI.Connection;
    using Patch._VehicleAI._PassengerCarAI.Connection;
    using Patch._VehicleAI._TrainAI.Connection;

    internal class GameConnectionManager {

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

        public PassengerCarAIConnection PassengerCarAIConnection { get; }
        public VehicleAIConnection VehicleAIConnection { get; }
        public TrainAIConnection TrainAIConnection { get; }
        public HumanAIConnection HumanAIConnection { get; }
        public ResidentAIConnection ResidentAIConnection { get; }
        public TouristAIConnection TouristAIConnection { get; }
    }
}