namespace TrafficManager.Manager {
    using TrafficManager.API.Manager;

    /// <summary>
    /// Helper class to ensure that events are always handled in the simulation thread
    /// </summary>
    public abstract class AbstractFeatureManager : AbstractCustomManager, IFeatureManager {
        public void OnDisableFeature() {
            Services.SimulationService.AddAction(() => {
                OnDisableFeatureInternal();
            });
        }

        public void OnEnableFeature() {
            Services.SimulationService.AddAction(() => {
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