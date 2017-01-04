using System;
using ColossalFramework;
using TrafficManager.Geometry;
using System.Collections.Generic;
using TrafficManager.State;
using TrafficManager.Custom.AI;
using TrafficManager.Util;
using TrafficManager.TrafficLight;
using TrafficManager.Traffic;
using System.Linq;

namespace TrafficManager.Manager {
	public class TrafficLightSimulationManager : AbstractNodeGeometryObservingManager, ICustomDataManager<List<Configuration.TimedTrafficLights>> {
		public static TrafficLightSimulationManager Instance { get; private set; } = null;
		public const int SIM_MOD = 64;

		static TrafficLightSimulationManager() {
			Instance = new TrafficLightSimulationManager();
		}

		/// <summary>
		/// For each node id: traffic light simulation assigned to the node
		/// </summary>
		internal TrafficLightSimulation[] TrafficLightSimulations = new TrafficLightSimulation[NetManager.MAX_NODE_COUNT];
		//public Dictionary<ushort, TrafficLightSimulation> TrafficLightSimulations = new Dictionary<ushort, TrafficLightSimulation>();

		private TrafficLightSimulationManager() {
			
		}

		public void SimulationStep() {
			int frame = (int)(Singleton<SimulationManager>.instance.m_currentFrameIndex & (SIM_MOD - 1));
			int minIndex = frame * (NetManager.MAX_NODE_COUNT / SIM_MOD);
			int maxIndex = (frame + 1) * (NetManager.MAX_NODE_COUNT / SIM_MOD) - 1;

			for (int nodeId = minIndex; nodeId <= maxIndex; ++nodeId) {
				try {
					TrafficLightSimulation nodeSim = TrafficLightSimulations[nodeId];

					if (nodeSim != null && nodeSim.IsTimedLightActive()) {
						//Flags.applyNodeTrafficLightFlag((ushort)nodeId);
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
			TrafficLightSimulations[nodeId] = new TrafficLightSimulation(nodeId);
			SubscribeToNodeGeometry(nodeId);
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
			TrafficLightManager tlm = TrafficLightManager.Instance;

			if (sim.TimedLight != null) {
				// remove/destroy all timed traffic lights in group
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
							tlm.RemoveTrafficLight(timedNodeId);
					} else {
						otherNodeSim.TimedLight.RemoveNodeFromGroup(nodeId);
					}
				}
			}

			//Flags.setNodeTrafficLight(nodeId, false);
			//sim.DestroyTimedTrafficLight();
			sim.DestroyManualTrafficLight();
			sim.NodeGeoUnsubscriber?.Dispose();
			RemoveNodeFromSimulation(nodeId);
			if (removeTrafficLight)
				tlm.RemoveTrafficLight(nodeId);
		}

		public bool HasSimulation(ushort nodeId) {
			return GetNodeSimulation(nodeId) != null;
		}

		private void RemoveNodeFromSimulation(ushort nodeId) {
			TrafficLightSimulations[nodeId]?.Destroy();
			TrafficLightSimulations[nodeId] = null;
			UnsubscribeFromNodeGeometry(nodeId);
		}

		public TrafficLightSimulation GetNodeSimulation(ushort nodeId) {
			return TrafficLightSimulations[nodeId];
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (uint nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				TrafficLightSimulations[nodeId] = null;
			}
		}

		protected override void HandleInvalidNode(NodeGeometry geometry) {
			RemoveNodeFromSimulation(geometry.NodeId, false, true);
		}

		protected override void HandleValidNode(NodeGeometry geometry) {
			TrafficPriorityManager.Instance.AddPriorityNode(geometry.NodeId, true);
		}

		public bool LoadData(List<Configuration.TimedTrafficLights> data) {
			bool success = true;
			Log.Info($"Loading {data.Count} timed traffic lights (new method)");

			TrafficLightManager tlm = TrafficLightManager.Instance;

			HashSet<ushort> nodesWithSimulation = new HashSet<ushort>();
			foreach (Configuration.TimedTrafficLights cnfTimedLights in data) {
				nodesWithSimulation.Add(cnfTimedLights.nodeId);
			}

			Dictionary<ushort, ushort> masterNodeIdBySlaveNodeId = new Dictionary<ushort, ushort>();
			Dictionary<ushort, List<ushort>> nodeGroupByMasterNodeId = new Dictionary<ushort, List<ushort>>();
			foreach (Configuration.TimedTrafficLights cnfTimedLights in data) {
				try {
					// TODO most of this should not be necessary at all if the classes around TimedTrafficLights class were properly designed
					List<ushort> currentNodeGroup = cnfTimedLights.nodeGroup.Distinct().ToList(); // enforce uniqueness of node ids
					if (!currentNodeGroup.Contains(cnfTimedLights.nodeId))
						currentNodeGroup.Add(cnfTimedLights.nodeId);
					// remove any nodes that are not configured to have a simulation
					currentNodeGroup = new List<ushort>(currentNodeGroup.Intersect(nodesWithSimulation));

					// remove invalid nodes from the group; find if any of the nodes in the group is already a master node
					ushort masterNodeId = 0;
					int foundMasterNodes = 0;
					for (int i = 0; i < currentNodeGroup.Count;) {
						ushort nodeId = currentNodeGroup[i];
						if (!NetUtil.IsNodeValid(currentNodeGroup[i])) {
							currentNodeGroup.RemoveAt(i);
							continue;
						} else if (nodeGroupByMasterNodeId.ContainsKey(nodeId)) {
							// this is a known master node
							if (foundMasterNodes > 0) {
								// we already found another master node. ignore this node.
								currentNodeGroup.RemoveAt(i);
								continue;
							}
							// we found the first master node
							masterNodeId = nodeId;
							++foundMasterNodes;
						}
						++i;
					}

					if (masterNodeId == 0) {
						// no master node defined yet, set the first node as a master node
						masterNodeId = currentNodeGroup[0];
					}

					// ensure the master node is the first node in the list (TimedTrafficLights depends on this at the moment...)
					currentNodeGroup.Remove(masterNodeId);
					currentNodeGroup.Insert(0, masterNodeId);

					// update the saved node group and master-slave info
					nodeGroupByMasterNodeId[masterNodeId] = currentNodeGroup;
					foreach (ushort nodeId in currentNodeGroup) {
						masterNodeIdBySlaveNodeId[nodeId] = masterNodeId;
					}
				} catch (Exception e) {
					Log.Warning($"Error building timed traffic light group for TimedNode {cnfTimedLights.nodeId} (NodeGroup: {string.Join(", ", cnfTimedLights.nodeGroup.Select(x => x.ToString()).ToArray())}): " + e.ToString());
					success = false;
				}
			}

			foreach (Configuration.TimedTrafficLights cnfTimedLights in data) {
				try {
					if (!masterNodeIdBySlaveNodeId.ContainsKey(cnfTimedLights.nodeId))
						continue;
					ushort masterNodeId = masterNodeIdBySlaveNodeId[cnfTimedLights.nodeId];
					List<ushort> nodeGroup = nodeGroupByMasterNodeId[masterNodeId];

					Log._Debug($"Adding timed light at node {cnfTimedLights.nodeId}. NodeGroup: {string.Join(", ", nodeGroup.Select(x => x.ToString()).ToArray())}");

					TrafficLightSimulation sim = AddNodeToSimulation(cnfTimedLights.nodeId);
					sim.SetupTimedTrafficLight(nodeGroup);
					var timedNode = sim.TimedLight;

					int j = 0;
					foreach (Configuration.TimedTrafficLightsStep cnfTimedStep in cnfTimedLights.timedSteps) {
						Log._Debug($"Loading timed step {j} at node {cnfTimedLights.nodeId}");
						TimedTrafficLightsStep step = timedNode.AddStep(cnfTimedStep.minTime, cnfTimedStep.maxTime, cnfTimedStep.waitFlowBalance);

						foreach (KeyValuePair<ushort, Configuration.CustomSegmentLights> e in cnfTimedStep.segmentLights) {
							if (!NetUtil.IsSegmentValid(e.Key))
								continue;

							Log._Debug($"Loading timed step {j}, segment {e.Key} at node {cnfTimedLights.nodeId}");
							CustomSegmentLights lights = null;
							if (!step.segmentLights.TryGetValue(e.Key, out lights)) {
								Log._Debug($"No segment lights found at timed step {j} for segment {e.Key}, node {cnfTimedLights.nodeId}");
								continue;
							}
							Configuration.CustomSegmentLights cnfLights = e.Value;

							Log._Debug($"Loading pedestrian light @ seg. {e.Key}, step {j}: {cnfLights.pedestrianLightState} {cnfLights.manualPedestrianMode}");

							lights.ManualPedestrianMode = cnfLights.manualPedestrianMode;
							lights.PedestrianLightState = cnfLights.pedestrianLightState;

							foreach (KeyValuePair<ExtVehicleType, Configuration.CustomSegmentLight> e2 in cnfLights.customLights) {
								Log._Debug($"Loading timed step {j}, segment {e.Key}, vehicleType {e2.Key} at node {cnfTimedLights.nodeId}");
								CustomSegmentLight light = null;
								if (!lights.CustomLights.TryGetValue(e2.Key, out light)) {
									Log._Debug($"No segment light found for timed step {j}, segment {e.Key}, vehicleType {e2.Key} at node {cnfTimedLights.nodeId}");
									continue;
								}
								Configuration.CustomSegmentLight cnfLight = e2.Value;

								light.currentMode = (CustomSegmentLight.Mode)cnfLight.currentMode;
								light.SetStates(cnfLight.mainLight, cnfLight.leftLight, cnfLight.rightLight, false);
							}
						}
						++j;
					}
				} catch (Exception e) {
					// ignore, as it's probably corrupt save data. it'll be culled on next save
					Log.Warning("Error loading data from TimedNode (new method): " + e.ToString());
					success = false;
				}
			}

			foreach (Configuration.TimedTrafficLights cnfTimedLights in data) {
				try {
					TrafficLightSimulation sim = GetNodeSimulation(cnfTimedLights.nodeId);
					if (sim == null || sim.TimedLight == null)
						continue;

					var timedNode = sim.TimedLight;

					timedNode.housekeeping();
					if (cnfTimedLights.started)
						timedNode.Start();
				} catch (Exception e) {
					Log.Warning($"Error starting timed light @ {cnfTimedLights.nodeId}: " + e.ToString());
					success = false;
				}
			}

			return success;
		}

		public List<Configuration.TimedTrafficLights> SaveData(ref bool success) {
			List<Configuration.TimedTrafficLights> ret = new List<Configuration.TimedTrafficLights>();
			for (ushort nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				try {
					TrafficLightSimulation sim = GetNodeSimulation(nodeId);
					if (sim == null || !sim.IsTimedLight()) {
						continue;
					}

					Log._Debug($"Going to save timed light at node {nodeId}.");

					var timedNode = sim.TimedLight;
					timedNode.handleNewSegments();

					Configuration.TimedTrafficLights cnfTimedLights = new Configuration.TimedTrafficLights();
					ret.Add(cnfTimedLights);

					cnfTimedLights.nodeId = timedNode.NodeId;
					cnfTimedLights.nodeGroup = timedNode.NodeGroup;
					cnfTimedLights.started = timedNode.IsStarted();
					cnfTimedLights.timedSteps = new List<Configuration.TimedTrafficLightsStep>();

					for (var j = 0; j < timedNode.NumSteps(); j++) {
						Log._Debug($"Saving timed light step {j} at node {nodeId}.");
						TimedTrafficLightsStep timedStep = timedNode.Steps[j];
						Configuration.TimedTrafficLightsStep cnfTimedStep = new Configuration.TimedTrafficLightsStep();
						cnfTimedLights.timedSteps.Add(cnfTimedStep);

						cnfTimedStep.minTime = timedStep.minTime;
						cnfTimedStep.maxTime = timedStep.maxTime;
						cnfTimedStep.waitFlowBalance = timedStep.waitFlowBalance;
						cnfTimedStep.segmentLights = new Dictionary<ushort, Configuration.CustomSegmentLights>();
						foreach (KeyValuePair<ushort, CustomSegmentLights> e in timedStep.segmentLights) {
							Log._Debug($"Saving timed light step {j}, segment {e.Key} at node {nodeId}.");

							CustomSegmentLights segLights = e.Value;
							Configuration.CustomSegmentLights cnfSegLights = new Configuration.CustomSegmentLights();
							cnfTimedStep.segmentLights.Add(e.Key, cnfSegLights);

							cnfSegLights.nodeId = segLights.NodeId;
							cnfSegLights.segmentId = segLights.SegmentId;
							cnfSegLights.customLights = new Dictionary<ExtVehicleType, Configuration.CustomSegmentLight>();
							cnfSegLights.pedestrianLightState = segLights.PedestrianLightState;
							cnfSegLights.manualPedestrianMode = segLights.ManualPedestrianMode;

							Log._Debug($"Saving pedestrian light @ seg. {e.Key}, step {j}: {cnfSegLights.pedestrianLightState} {cnfSegLights.manualPedestrianMode}");

							foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e2 in segLights.CustomLights) {
								Log._Debug($"Saving timed light step {j}, segment {e.Key}, vehicleType {e2.Key} at node {nodeId}.");

								CustomSegmentLight segLight = e2.Value;
								Configuration.CustomSegmentLight cnfSegLight = new Configuration.CustomSegmentLight();
								cnfSegLights.customLights.Add(e2.Key, cnfSegLight);

								cnfSegLight.nodeId = segLight.NodeId;
								cnfSegLight.segmentId = segLight.SegmentId;
								cnfSegLight.currentMode = (int)segLight.CurrentMode;
								cnfSegLight.leftLight = segLight.LightLeft;
								cnfSegLight.mainLight = segLight.LightMain;
								cnfSegLight.rightLight = segLight.LightRight;
							}
						}
					}
				} catch (Exception e) {
					Log.Error($"Exception occurred while saving timed traffic light @ {nodeId}: {e.ToString()}");
					success = false;
				}
			}
			return ret;
		}
	}
}
