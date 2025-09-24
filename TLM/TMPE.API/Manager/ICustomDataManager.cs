namespace TrafficManager.API.Manager {
    using JetBrains.Annotations;

    public interface ICustomDataManager<T> {
        /// <summary>Loads data from a configuration field into the class which implements this interface.
        /// </summary>
        /// <param name="data">Data comes from here.</param>
        /// <returns>Success.</returns>
        bool LoadData(T data);

        T SaveData(ref bool success);
    }
}