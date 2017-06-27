using System.Collections.Generic;
using TrafficManager.Geometry.Impl;

namespace TrafficManager.TrafficLight {
	public interface ITrafficLightSimulation {
		ushort NodeId { get; }
		ITimedTrafficLights TimedLight { get; }

		bool IsManualLight();
		bool IsSimulationActive();
		bool IsTimedLight();
		bool IsTimedLightActive();
		void SetupManualTrafficLight();
		void SetupTimedTrafficLight(IList<ushort> nodeGroup);
		void DestroyTimedTrafficLight();
		void DestroyManualTrafficLight();
		void Destroy();
		void Housekeeping(); // TODO improve & remove
	}
}