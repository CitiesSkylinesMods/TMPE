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

            if (Options.timedLightsEnabled) {
                TrafficLightSimulationManager.Instance.SimulationStep();
            }
        }
    } // end class
}