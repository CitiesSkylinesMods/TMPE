namespace TrafficManager.API.Manager {
    using JetBrains.Annotations;

    public interface ICustomDataManager<T> {
        /// <summary>Loads data from a configuration field into the class which implements this interface.
        /// </summary>
        /// <param name="data">Data comes from here.</param>
        /// <returns>Success.</returns>
        [UsedImplicitly] // While this seems to be not called from anywhere, it is in fact called
        bool LoadData(T data);

        T SaveData(ref bool success);
    }
}