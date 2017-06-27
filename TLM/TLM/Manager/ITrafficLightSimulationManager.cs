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

		/// <summary>
		/// Checks if space reservation at <paramref name="targetPos"/> is allowed. When a custom traffic light is active at the transit node
		/// space reservation is only allowed if the light is not red.
		/// </summary>
		/// <param name="transitNodeId">transition node id</param>
		/// <param name="sourcePos">source path position</param>
		/// <param name="targetPos">target path position</param>
		/// <returns></returns>
		bool IsSpaceReservationAllowed(ushort transitNodeId, PathUnit.Position sourcePos, PathUnit.Position targetPos);
	}
}
