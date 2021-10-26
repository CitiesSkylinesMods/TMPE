namespace TrafficManager.Manager {
    using ColossalFramework;
    using TrafficManager.API.Manager;

    /// <summary>
    /// Helper class to ensure that events are always handled in the simulation thread
    /// </summary>
    public abstract class AbstractFeatureManager : AbstractCustomManager, IFeatureManager {
        public void OnDisableFeature() {
            Singleton<SimulationManager>.instance.AddAction(() => {
                OnDisableFeatureInternal();
            });
        }

        public void OnEnableFeature() {
            Singleton<SimulationManager>.instance.AddAction(() => {
                OnEnableFeatureInternal();
            });
        }

        /// <summary>
        /// Executes whenever the associated feature is disabled. Guaranteed to run in the simulation thread.
        /// </summary>
        protected abstract void OnDisableFeatureInternal();

        /// <summary>
        /// Executes whenever the associated feature is enabled. Guaranteed to run in the simulation thread.
        /// </summary>
        protected abstract void OnEnableFeatureInternal();
    }
}