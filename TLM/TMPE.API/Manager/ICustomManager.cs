namespace TrafficManager.API.Manager {
    public interface ICustomManager {
        // TODO documentation
        void OnBeforeLoadData();
        void OnAfterLoadData();
        void OnBeforeSaveData();
        void OnAfterSaveData();
        void OnLevelLoading();
        void OnLevelUnloading();
        void PrintDebugInfo();
    }
}