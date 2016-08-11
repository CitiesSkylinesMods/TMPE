using System;
using ColossalFramework;
using TrafficManager.Geometry;
using System.Collections.Generic;
using TrafficManager.State;
using TrafficManager.Custom.AI;
using System.Linq;
using TrafficManager.Util;
using TrafficManager.TrafficLight;

namespace TrafficManager.Manager {
	public class TrafficLightSimulationManager : ICustomManager {
		private static TrafficLightSimulationManager instance = null;

		public static TrafficLightSimulationManager Instance() {
			if (instance == null)
				instance = new TrafficLightSimulationManager();
			return instance;
		}

		/// <summary>
		/// For each node id: traffic light simulation assigned to the node
		/// </summary>
		//public TrafficLightSimulation[] TrafficLightSimulations = new TrafficLightSimulation[NetManager.MAX_NODE_COUNT];
		public Dictionary<ushort, TrafficLightSimulation> TrafficLightSimulations = new Dictionary<ushort, TrafficLightSimulation>();

		private TrafficLightSimulationManager() {
			
		}

		public void SimulationStep() {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TrafficLightSimulation.SimulationStep");
#endif
			try {
				foreach (KeyValuePair<ushort, TrafficLightSimulation> e in TrafficLightSimulations) {
					try {
						var nodeSim = e.Value;
						var nodeId = e.Key;
						if (nodeSim.IsTimedLightActive()) {
							Flags.applyNodeTrafficLightFlag(nodeId);
							nodeSim.TimedLight.SimulationStep();
						}
					} catch (Exception ex) {
						Log.Warning($"Error occured while simulating traffic light @ node {e.Key}: {ex.ToString()}");
					}
				}
			} catch (Exception ex) {
				// TODO the dictionary was modified (probably a segment connected to a traffic light was changed/removed). rework this
				Log.Warning($"Error occured while iterating over traffic light simulations: {ex.ToString()}");
			}
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TrafficLightSimulation.SimulationStep");
#endif
		}

		/// <summary>
		/// Adds a traffic light simulation to the node with the given id
		/// </summary>
		/// <param name="nodeId"></param>
		public TrafficLightSimulation AddNodeToSimulation(ushort nodeId) {
			if (TrafficLightSimulations.ContainsKey(nodeId)) {
				return TrafficLightSimulations[nodeId];
			}
			TrafficLightSimulations.Add(nodeId, new TrafficLightSimulation(nodeId));
			return TrafficLightSimulations[nodeId];
		}

		/// <summary>
		/// Destroys the traffic light and removes it
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="destroyGroup"></param>
		public void RemoveNodeFromSimulation(ushort nodeId, bool destroyGroup, bool removeTrafficLight) {
			if (!TrafficLightSimulations.ContainsKey(nodeId))
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
						TrafficLightSimulations.Remove(timedNodeId);
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
			TrafficLightSimulations.Remove(nodeId);
			if (removeTrafficLight)
				Flags.setNodeTrafficLight(nodeId, false);
		}

		public TrafficLightSimulation GetNodeSimulation(ushort nodeId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TrafficLightSimulation.GetNodeSimulation");
#endif

			TrafficLightSimulation ret = null;
			if (TrafficLightSimulations.ContainsKey(nodeId)) {
				ret = TrafficLightSimulations[nodeId];
			}

#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TrafficLightSimulation.GetNodeSimulation");
#endif
			return ret;
		}

		public void OnLevelUnloading() {
			TrafficLightSimulations.Clear();
			/*for (ushort nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				TrafficLightSimulations[nodeId] = null;
			}*/
		}
	}
}
