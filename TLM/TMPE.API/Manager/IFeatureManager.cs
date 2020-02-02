namespace TrafficManager.API.Manager {
    /// <summary>
    /// Represents a manager that handles logic bound to a certain feature
    /// </summary>
    public interface IFeatureManager {
        /// <summary>
        /// Handles disabling the managed feature
        /// </summary>
        void OnDisableFeature();

        /// <summary>
        /// Handles enabling the managed feature
        /// </summary>
        void OnEnableFeature();
    }
}