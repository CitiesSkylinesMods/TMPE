namespace TrafficManager.API.Manager {
    public interface ICustomDataManager<T> {
        // TODO documentation
        bool LoadData(T data);

        T SaveData(ref bool success);
    }
}