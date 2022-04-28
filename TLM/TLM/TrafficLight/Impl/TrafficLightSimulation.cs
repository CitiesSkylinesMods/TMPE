namespace TrafficManager.TrafficLight.Impl {
    using CSUtil.Commons;
    using System;
    using TrafficManager.API.Traffic.Enums;

    public struct TrafficLightSimulation {
        /// <summary>
        /// Timed traffic light by node id
        /// </summary>
        public TimedTrafficLights timedLight;

        public ushort nodeId;
        public TrafficLightSimulationType type;

        public TrafficLightSimulation(ushort nodeId) {
            // Log._Debug($"TrafficLightSimulation: Constructor called @ node {nodeId}");
            this.nodeId = nodeId;
            timedLight = null;
            type = TrafficLightSimulationType.None;
        }

        public override string ToString() {
            return string.Format(
                "[TrafficLightSimulation\n\tnodeId = {0}\n\ttype = {1}\n\ttimedLight = {2}\n" +
                "TrafficLightSimulation]",
                nodeId,
                type,
                timedLight);
        }

        public bool IsTimedLight() {
            return type == TrafficLightSimulationType.Timed && timedLight != null;
        }

        public bool IsManualLight() {
            return type == TrafficLightSimulationType.Manual;
        }

        public bool IsTimedLightRunning() {
            return IsTimedLight() && timedLight.IsStarted();
        }

        public bool IsSimulationRunning() {
            return IsManualLight() || IsTimedLightRunning();
        }

        public bool HasSimulation() {
            return IsManualLight() || IsTimedLight();
        }

        public void SimulationStep() {
            if (!HasSimulation()) {
                return;
            }

            if (IsTimedLightRunning()) {
                timedLight.SimulationStep();
            }
        }

        public void Update() {
            Log._Trace($"TrafficLightSimulation.Update(): called for node {nodeId}");

            if (IsTimedLight()) {
                timedLight.OnGeometryUpdate();
                timedLight.Housekeeping();
            }
        }

        public void Housekeeping() {
            // TODO improve & remove
            timedLight?.Housekeeping(); // removes unused step lights
        }
    }
}