namespace TrafficManager.API.Manager {
    using GenericGameBridge.Factory;

    public interface ICustomManager {
        // TODO documentation
        IServiceFactory Services { get; }
        void OnBeforeLoadData();
        void OnAfterLoadData();
        void OnBeforeSaveData();
        void OnAfterSaveData();
        void OnLevelLoading();
        void OnLevelUnloading();
        void PrintDebugInfo();
    }
}