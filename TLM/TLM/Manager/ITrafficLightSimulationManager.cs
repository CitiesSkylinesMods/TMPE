using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.TrafficLight;

namespace TrafficManager.Manager {
	public interface ITrafficLightSimulationManager {
		// TODO documentation
		ITrafficLightSimulation AddNodeToSimulation(ushort nodeId);
		ITrafficLightSimulation GetNodeSimulation(ushort nodeId);
		bool HasActiveSimulation(ushort nodeId);
		bool HasActiveTimedSimulation(ushort nodeId);
		bool HasSimulation(ushort nodeId);
		bool HasTimedSimulation(ushort nodeId);
		void RemoveNodeFromSimulation(ushort nodeId, bool destroyGroup, bool removeTrafficLight);
		void SimulationStep();
	}
}
