namespace TrafficManager.Lifecycle {
    using ICities;
    using JetBrains.Annotations;
    using TrafficManager.State;
    using TrafficManager.Manager.Impl;

    [UsedImplicitly]
    public sealed class ThreadingExtension : ThreadingExtensionBase {
        public override void OnBeforeSimulationTick() {
            base.OnBeforeSimulationTick();

            GeometryManager.Instance.SimulationStep();
            RoutingManager.Instance.SimulationStep();
        }

        public override void OnBeforeSimulationFrame() {
            base.OnBeforeSimulationFrame();

            if (SavedGameOptions.Instance.timedLightsEnabled) {
                TrafficLightSimulationManager.Instance.SimulationStep();
            }
        }

        public override void OnAfterSimulationTick() {
            base.OnAfterSimulationTick();

            UtilityManager.Instance.ProcessTransferRecordableQueue();
        }
    } // end class
}