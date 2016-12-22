using System;
using ColossalFramework;
using TrafficManager.Geometry;
using System.Collections.Generic;
using TrafficManager.State;
using TrafficManager.Custom.AI;
using TrafficManager.Util;
using TrafficManager.TrafficLight;

namespace TrafficManager.Manager {
	public class TrafficLightSimulationManager : ICustomManager {
		public static TrafficLightSimulationManager Instance { get; private set; } = null;

		static TrafficLightSimulationManager() {
			Instance = new TrafficLightSimulationManager();
		}

		/// <summary>
		/// For each node id: traffic light simulation assigned to the node
		/// </summary>
		internal TrafficLightSimulation[] TrafficLightSimulations = new TrafficLightSimulation[NetManager.MAX_NODE_COUNT];
		internal ushort[] NodesWithTrafficLightSimulation = new ushort[0];
		//public Dictionary<ushort, TrafficLightSimulation> TrafficLightSimulations = new Dictionary<ushort, TrafficLightSimulation>();

		private TrafficLightSimulationManager() {
			
		}

		public void SimulationStep() {
			foreach (ushort nodeId in NodesWithTrafficLightSimulation) {
				try {
					TrafficLightSimulation nodeSim = TrafficLightSimulations[nodeId];

					if (nodeSim.IsTimedLightActive()) {
						//Flags.applyNodeTrafficLightFlag(nodeId);
						nodeSim.TimedLight.SimulationStep();
					}
				} catch (Exception ex) {
					Log.Warning($"Error occured while simulating traffic light @ node {nodeId}: {ex.ToString()}");
				}
			}
		}

		/// <summary>
		/// Adds a traffic light simulation to the node with the given id
		/// </summary>
		/// <param name="nodeId"></param>
		public TrafficLightSimulation AddNodeToSimulation(ushort nodeId) {
			if (HasSimulation(nodeId)) {
				return TrafficLightSimulations[nodeId];
			}
			ushort[] newNodesWithSim = new ushort[NodesWithTrafficLightSimulation.Length + 1];
			Array.Copy(NodesWithTrafficLightSimulation, newNodesWithSim, NodesWithTrafficLightSimulation.Length);
			newNodesWithSim[newNodesWithSim.Length-1] = nodeId;
			TrafficLightSimulations[nodeId] = new TrafficLightSimulation(nodeId);
			NodesWithTrafficLightSimulation = newNodesWithSim;

			return TrafficLightSimulations[nodeId];
		}

		/// <summary>
		/// Destroys the traffic light and removes it
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="destroyGroup"></param>
		public void RemoveNodeFromSimulation(ushort nodeId, bool destroyGroup, bool removeTrafficLight) {
			if (!HasSimulation(nodeId))
				return;

			TrafficLightSimulation sim = TrafficLightSimulations[nodeId];

			if (sim.TimedLight != null) {
				// remove/destroy other timed traffic lights in group
				List<ushort> oldNodeGroup = new List<ushort>(sim.TimedLight.NodeGroup);
				foreach (var timedNodeId in oldNodeGroup) {
					var otherNodeSim = GetNodeSimulation(timedNodeId);
					if (otherNodeSim == null) {
						continue;
					}

					if (destroyGroup || timedNodeId == nodeId) {
						//Log._Debug($"Slave: Removing simulation @ node {timedNodeId}");
						otherNodeSim.DestroyTimedTrafficLight();
						otherNodeSim.DestroyManualTrafficLight();
						otherNodeSim.NodeGeoUnsubscriber?.Dispose();
						RemoveNodeFromSimulation(timedNodeId);
						if (removeTrafficLight)
							Flags.setNodeTrafficLight(timedNodeId, false);
					} else {
						otherNodeSim.TimedLight.RemoveNodeFromGroup(nodeId);
					}
				}
			}

			//Flags.setNodeTrafficLight(nodeId, false);
			sim.DestroyTimedTrafficLight();
			sim.DestroyManualTrafficLight();
			sim.NodeGeoUnsubscriber?.Dispose();
			RemoveNodeFromSimulation(nodeId);
			if (removeTrafficLight)
				Flags.setNodeTrafficLight(nodeId, false);
		}

		private bool HasSimulation(ushort nodeId) {
			foreach (ushort nId in NodesWithTrafficLightSimulation)
				if (nId == nodeId)
					return true;
			return false;
		}

		private void RemoveNodeFromSimulation(ushort nodeId) {
			// find index
			int index = -1;
			for (int i = 0; i < NodesWithTrafficLightSimulation.Length; ++i) {
				if (NodesWithTrafficLightSimulation[i] == nodeId) {
					index = i;
					break;
				}
			}

			if (index < 0)
				return;

			// splice array
			ushort[] newNodesWithSim = new ushort[NodesWithTrafficLightSimulation.Length - 1];
			if (index > 0)
				Array.Copy(NodesWithTrafficLightSimulation, 0, newNodesWithSim, 0, index);
			int remainingLength = NodesWithTrafficLightSimulation.Length - index - 1;
			if (remainingLength > 0)
				Array.Copy(NodesWithTrafficLightSimulation, index+1, newNodesWithSim, index, remainingLength);

			// remove simulation
			NodesWithTrafficLightSimulation = newNodesWithSim;
			TrafficLightSimulations[nodeId] = null;
		}

		public TrafficLightSimulation GetNodeSimulation(ushort nodeId) {
			TrafficLightSimulation ret = null;
			if (HasSimulation(nodeId)) {
				ret = TrafficLightSimulations[nodeId];
			}

			return ret;
		}

		public void OnLevelUnloading() {
			NodesWithTrafficLightSimulation = new ushort[0];
			for (uint nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				TrafficLightSimulations[nodeId] = null;
			}
		}
	}
}
