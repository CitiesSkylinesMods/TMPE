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
using CSUtil.Commons;
using TrafficManager.TrafficLight.Impl;
using TrafficManager.Geometry.Impl;
using CSUtil.Commons.Benchmark;
using TrafficManager.TrafficLight.Data;

namespace TrafficManager.Manager.Impl {
	public class TrafficLightSimulationManager : AbstractGeometryObservingManager, ICustomDataManager<List<Configuration.TimedTrafficLights>>, ITrafficLightSimulationManager {
		public static readonly TrafficLightSimulationManager Instance = new TrafficLightSimulationManager();
		public const int SIM_MOD = 64;
	
		/// <summary>
		/// For each node id: traffic light simulation assigned to the node
		/// </summary>
		public TrafficLightSimulation[] TrafficLightSimulations;
		//public Dictionary<ushort, TrafficLightSimulation> TrafficLightSimulations = new Dictionary<ushort, TrafficLightSimulation>();

		private TrafficLightSimulationManager() {
			TrafficLightSimulations = new TrafficLightSimulation[NetManager.MAX_NODE_COUNT];
			for (int i = 0; i < TrafficLightSimulations.Length; ++i) {
				TrafficLightSimulations[i] = new TrafficLightSimulation((ushort)i);
			}
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Traffic light simulations:");
			for (int i = 0; i < TrafficLightSimulations.Length; ++i) {
				if (! TrafficLightSimulations[i].HasSimulation()) {
					continue;
				}
				Log._Debug($"Simulation {i}: {TrafficLightSimulations[i]}");
			}
		}

		public void SimulationStep() {
			int frame = (int)(Services.SimulationService.CurrentFrameIndex & (SIM_MOD - 1));
			int minIndex = frame * (NetManager.MAX_NODE_COUNT / SIM_MOD);
			int maxIndex = (frame + 1) * (NetManager.MAX_NODE_COUNT / SIM_MOD) - 1;

			ushort failedNodeId = 0;
			try {
				for (int nodeId = minIndex; nodeId <= maxIndex; ++nodeId) {
					failedNodeId = (ushort)nodeId;
					TrafficLightSimulations[nodeId].SimulationStep();
				}
				failedNodeId = 0;
			} catch (Exception ex) {
				Log.Error($"Error occured while simulating traffic light @ node {failedNodeId}: {ex.ToString()}");
				if (failedNodeId != 0) {
					RemoveNodeFromSimulation((ushort)failedNodeId);
				}
			}
		}

		/// <summary>
		/// Adds a manual traffic light simulation to the node with the given id
		/// </summary>
		/// <param name="nodeId"></param>
		public bool SetUpManualTrafficLight(ushort nodeId) {
			return TrafficLightSimulations[nodeId].SetUpManualTrafficLight();
		}

		/// <summary>
		/// Adds a timed traffic light simulation to the node with the given id
		/// </summary>
		/// <param name="nodeId"></param>
		public bool SetUpTimedTrafficLight(ushort nodeId, IList<ushort> nodeGroup) { // TODO improve signature
			if (! TrafficLightSimulations[nodeId].SetUpTimedTrafficLight(nodeGroup)) {
				return false;
			}

			return true;
		}

		/// <summary>
		/// Destroys the traffic light and removes it
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="destroyGroup"></param>
		public void RemoveNodeFromSimulation(ushort nodeId, bool destroyGroup, bool removeTrafficLight) {
#if DEBUG
			Log._Debug($"TrafficLightSimulationManager.RemoveNodeFromSimulation({nodeId}, {destroyGroup}, {removeTrafficLight}) called.");
#endif

			if (! TrafficLightSimulations[nodeId].HasSimulation()) {
				return;
			}
			TrafficLightManager tlm = TrafficLightManager.Instance;

			if (TrafficLightSimulations[nodeId].IsTimedLight()) {
				// remove/destroy all timed traffic lights in group
				List<ushort> oldNodeGroup = new List<ushort>(TrafficLightSimulations[nodeId].TimedLight.NodeGroup);
				foreach (var timedNodeId in oldNodeGroup) {
					if (! TrafficLightSimulations[timedNodeId].HasSimulation()) {
						continue;
					}

					if (destroyGroup || timedNodeId == nodeId) {
						//Log._Debug($"Slave: Removing simulation @ node {timedNodeId}");
						//TrafficLightSimulations[timedNodeId].Destroy();
						RemoveNodeFromSimulation(timedNodeId);
						if (removeTrafficLight) {
							Constants.ServiceFactory.NetService.ProcessNode(timedNodeId, delegate (ushort nId, ref NetNode node) {
								tlm.RemoveTrafficLight(timedNodeId, ref node);
								return true;
							});
						}
					} else {
						if (TrafficLightSimulations[timedNodeId].IsTimedLight()) {
							TrafficLightSimulations[timedNodeId].TimedLight.RemoveNodeFromGroup(nodeId);
						}
					}
				}
			}

			//Flags.setNodeTrafficLight(nodeId, false);
			//sim.DestroyTimedTrafficLight();
			//TrafficLightSimulations[nodeId].DestroyManualTrafficLight();
			RemoveNodeFromSimulation(nodeId);
			if (removeTrafficLight) {
				Constants.ServiceFactory.NetService.ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
					tlm.RemoveTrafficLight(nodeId, ref node);
					return true;
				});
			}
		}

		public bool HasSimulation(ushort nodeId) {
			return TrafficLightSimulations[nodeId].HasSimulation();
		}

		public bool HasManualSimulation(ushort nodeId) {
			return TrafficLightSimulations[nodeId].IsManualLight();
		}

		public bool HasTimedSimulation(ushort nodeId) {
			return TrafficLightSimulations[nodeId].IsTimedLight();
		}

		public bool HasActiveTimedSimulation(ushort nodeId) {
			return TrafficLightSimulations[nodeId].IsTimedLightRunning();
		}

		public bool HasActiveSimulation(ushort nodeId) {
			return TrafficLightSimulations[nodeId].IsSimulationRunning();
		}

		private void RemoveNodeFromSimulation(ushort nodeId) {
#if DEBUG
			Log._Debug($"TrafficLightSimulationManager.RemoveNodeFromSimulation({nodeId}) called.");
#endif

			TrafficLightSimulations[nodeId].Destroy();
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (uint nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				TrafficLightSimulations[nodeId].Destroy();
			}
		}

		protected override void HandleInvalidNode(NodeGeometry geometry) {
			RemoveNodeFromSimulation(geometry.NodeId, false, true);
		}

		protected override void HandleValidNode(NodeGeometry geometry) {
			if (!TrafficLightSimulations[geometry.NodeId].HasSimulation()) {
				//Log._Debug($"TrafficLightSimulationManager.HandleValidNode({geometry.NodeId}): Node is not controlled by a custom traffic light simulation.");
				return;
			}

			if (! Flags.mayHaveTrafficLight(geometry.NodeId)) {
				Log._Debug($"TrafficLightSimulationManager.HandleValidNode({geometry.NodeId}): Node must not have a traffic light: Removing traffic light simulation.");
				RemoveNodeFromSimulation(geometry.NodeId, false, true);
				return;
			}

			foreach (SegmentEndGeometry end in geometry.SegmentEndGeometries) {
				if (end == null)
					continue;

				Log._Debug($"TrafficLightSimulationManager.HandleValidNode({geometry.NodeId}): Adding live traffic lights to segment {end.SegmentId}");

				// housekeep timed light
				CustomSegmentLightsManager.Instance.GetSegmentLights(end.SegmentId, end.StartNode).Housekeeping(true, true);
			}

			// ensure there is a physical traffic light
			Constants.ServiceFactory.NetService.ProcessNode(geometry.NodeId, delegate (ushort nodeId, ref NetNode node) {
				Constants.ManagerFactory.TrafficLightManager.AddTrafficLight(geometry.NodeId, ref node);
				return true;
			});

			TrafficLightSimulations[geometry.NodeId].Update();
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
						if (!Services.NetService.IsNodeValid(currentNodeGroup[i])) {
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

					SetUpTimedTrafficLight(cnfTimedLights.nodeId, nodeGroup);

					int j = 0;
					foreach (Configuration.TimedTrafficLightsStep cnfTimedStep in cnfTimedLights.timedSteps) {
						Log._Debug($"Loading timed step {j} at node {cnfTimedLights.nodeId}");
						ITimedTrafficLightsStep step = TrafficLightSimulations[cnfTimedLights.nodeId].TimedLight.AddStep(cnfTimedStep.minTime, cnfTimedStep.maxTime, (TrafficLight.StepChangeMetric)cnfTimedStep.changeMetric, cnfTimedStep.waitFlowBalance);

						foreach (KeyValuePair<ushort, Configuration.CustomSegmentLights> e in cnfTimedStep.segmentLights) {
							if (!Services.NetService.IsSegmentValid(e.Key))
								continue;
							e.Value.nodeId = cnfTimedLights.nodeId;

							Log._Debug($"Loading timed step {j}, segment {e.Key} at node {cnfTimedLights.nodeId}");
							ICustomSegmentLights lights = null;
							if (!step.CustomSegmentLights.TryGetValue(e.Key, out lights)) {
								Log._Debug($"No segment lights found at timed step {j} for segment {e.Key}, node {cnfTimedLights.nodeId}");
								continue;
							}
							Configuration.CustomSegmentLights cnfLights = e.Value;

							Log._Debug($"Loading pedestrian light @ seg. {e.Key}, step {j}: {cnfLights.pedestrianLightState} {cnfLights.manualPedestrianMode}");

							lights.ManualPedestrianMode = cnfLights.manualPedestrianMode;
							lights.PedestrianLightState = cnfLights.pedestrianLightState;

							bool first = true; // v1.10.2 transitional code
							foreach (KeyValuePair<ExtVehicleType, Configuration.CustomSegmentLight> e2 in cnfLights.customLights) {
								Log._Debug($"Loading timed step {j}, segment {e.Key}, vehicleType {e2.Key} at node {cnfTimedLights.nodeId}");
								ICustomSegmentLight light = null;
								if (!lights.CustomLights.TryGetValue(e2.Key, out light)) {
									Log._Debug($"No segment light found for timed step {j}, segment {e.Key}, vehicleType {e2.Key} at node {cnfTimedLights.nodeId}");
									// v1.10.2 transitional code START
									if (first) {
										first = false;
										if (!lights.CustomLights.TryGetValue(CustomSegmentLights.DEFAULT_MAIN_VEHICLETYPE, out light)) {
											Log._Debug($"No segment light found for timed step {j}, segment {e.Key}, DEFAULT vehicleType {CustomSegmentLights.DEFAULT_MAIN_VEHICLETYPE} at node {cnfTimedLights.nodeId}");
											continue;
										}
									} else {
										// v1.10.2 transitional code END
										continue;
										// v1.10.2 transitional code START
									}
									// v1.10.2 transitional code END
								}
								Configuration.CustomSegmentLight cnfLight = e2.Value;

								light.InternalCurrentMode = (TrafficLight.LightMode)cnfLight.currentMode; // TODO improve & remove
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
					var timedNode = TrafficLightSimulations[cnfTimedLights.nodeId].TimedLight;

					timedNode.Housekeeping();
					if (cnfTimedLights.started) {
						timedNode.Start(cnfTimedLights.currentStep);
					}
				} catch (Exception e) {
					Log.Warning($"Error starting timed light @ {cnfTimedLights.nodeId}: " + e.ToString());
					success = false;
				}
			}

			return success;
		}

		public List<Configuration.TimedTrafficLights> SaveData(ref bool success) {
			List<Configuration.TimedTrafficLights> ret = new List<Configuration.TimedTrafficLights>();
			for (uint nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				try {
					if (! TrafficLightSimulations[nodeId].IsTimedLight()) {
						continue;
					}

					Log._Debug($"Going to save timed light at node {nodeId}.");

					var timedNode = TrafficLightSimulations[nodeId].TimedLight;
					timedNode.OnGeometryUpdate();

					Configuration.TimedTrafficLights cnfTimedLights = new Configuration.TimedTrafficLights();
					ret.Add(cnfTimedLights);

					cnfTimedLights.nodeId = timedNode.NodeId;
					cnfTimedLights.nodeGroup = new List<ushort>(timedNode.NodeGroup);
					cnfTimedLights.started = timedNode.IsStarted();
					int stepIndex = timedNode.CurrentStep;
					if (timedNode.IsStarted() && timedNode.GetStep(timedNode.CurrentStep).IsInEndTransition()) {
						// if in end transition save the next step
						stepIndex = (stepIndex + 1) % timedNode.NumSteps();
					}
					cnfTimedLights.currentStep = stepIndex;
					cnfTimedLights.timedSteps = new List<Configuration.TimedTrafficLightsStep>();

					for (var j = 0; j < timedNode.NumSteps(); j++) {
						Log._Debug($"Saving timed light step {j} at node {nodeId}.");
						ITimedTrafficLightsStep timedStep = timedNode.GetStep(j);
						Configuration.TimedTrafficLightsStep cnfTimedStep = new Configuration.TimedTrafficLightsStep();
						cnfTimedLights.timedSteps.Add(cnfTimedStep);

						cnfTimedStep.minTime = timedStep.MinTime;
						cnfTimedStep.maxTime = timedStep.MaxTime;
						cnfTimedStep.changeMetric = (int)timedStep.ChangeMetric;
						cnfTimedStep.waitFlowBalance = timedStep.WaitFlowBalance;
						cnfTimedStep.segmentLights = new Dictionary<ushort, Configuration.CustomSegmentLights>();
						foreach (KeyValuePair<ushort, ICustomSegmentLights> e in timedStep.CustomSegmentLights) {
							Log._Debug($"Saving timed light step {j}, segment {e.Key} at node {nodeId}.");

							ICustomSegmentLights segLights = e.Value;
							Configuration.CustomSegmentLights cnfSegLights = new Configuration.CustomSegmentLights();

							ushort lightsNodeId = segLights.NodeId;
							if (lightsNodeId == 0 || lightsNodeId != timedNode.NodeId) {
								Log.Warning($"Inconsistency detected: Timed traffic light @ node {timedNode.NodeId} contains custom traffic lights for the invalid segment ({segLights.SegmentId}) at step {j}: nId={lightsNodeId}");
								continue;
							}

							cnfSegLights.nodeId = lightsNodeId; // TODO not needed
							cnfSegLights.segmentId = segLights.SegmentId; // TODO not needed
							cnfSegLights.customLights = new Dictionary<ExtVehicleType, Configuration.CustomSegmentLight>();
							cnfSegLights.pedestrianLightState = segLights.PedestrianLightState;
							cnfSegLights.manualPedestrianMode = segLights.ManualPedestrianMode;

							cnfTimedStep.segmentLights.Add(e.Key, cnfSegLights);

							Log._Debug($"Saving pedestrian light @ seg. {e.Key}, step {j}: {cnfSegLights.pedestrianLightState} {cnfSegLights.manualPedestrianMode}");

							foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e2 in segLights.CustomLights) {
								Log._Debug($"Saving timed light step {j}, segment {e.Key}, vehicleType {e2.Key} at node {nodeId}.");

								ICustomSegmentLight segLight = e2.Value;
								Configuration.CustomSegmentLight cnfSegLight = new Configuration.CustomSegmentLight();
								cnfSegLights.customLights.Add(e2.Key, cnfSegLight);

								cnfSegLight.nodeId = lightsNodeId; // TODO not needed
								cnfSegLight.segmentId = segLights.SegmentId; // TODO not needed
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
