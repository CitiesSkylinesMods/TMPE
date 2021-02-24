namespace TrafficManager.Manager.Impl {
    using API.Manager;
    using API.Manager.Connections;
    using Patch._VehicleAI._PassengerCarAI.Connection;

    internal class GameConnectionManager: IGameConnectionManager {
        internal GameConnectionManager() {
            PassengerCarAIConnection = PassengerCarAIHook.GetConnection();
        }

        public IPassengerCarAIConnection PassengerCarAIConnection { get; }
    }
}