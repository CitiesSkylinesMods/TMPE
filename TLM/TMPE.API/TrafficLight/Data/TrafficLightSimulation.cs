using CSUtil.Commons;
using TrafficManager.Traffic.Enums;

namespace TrafficManager.TrafficLight.Data {
	using API.Traffic.Enums;
	using API.TrafficLight;

	public struct TrafficLightSimulation {
		/// <summary>
		/// Timed traffic light by node id
		/// </summary>
		public ITimedTrafficLights timedLight;
		public ushort nodeId;
		public TrafficLightSimulationType type;

		public override string ToString() {
			return $"[TrafficLightSimulation\n" +
				"\t" + $"nodeId = {nodeId}\n" +
				"\t" + $"type = {type}\n" +
				"\t" + $"timedLight = {timedLight}\n" +
				"TrafficLightSimulation]";
		}

		public TrafficLightSimulation(ushort nodeId) {
			//Log._Debug($"TrafficLightSimulation: Constructor called @ node {nodeId}");
			this.nodeId = nodeId;
			timedLight = null;
			type = TrafficLightSimulationType.None;
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

		public void Housekeeping() { // TODO improve & remove
			timedLight?.Housekeeping(); // removes unused step lights
		}
	}
}
