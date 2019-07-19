namespace TrafficManager.Manager.Impl {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using API.Traffic.Enums;
    using API.TrafficLight;
    using CSUtil.Commons;
    using State;
    using Traffic;
    using TrafficLight;
    using TrafficLight.Data;
    using TrafficLight.Impl;
    using static RoadBaseAI;

    public class TrafficLightSimulationManager : AbstractGeometryObservingManager, ICustomDataManager<List<Configuration.TimedTrafficLights>>, ITrafficLightSimulationManager {
        public static readonly TrafficLightSimulationManager Instance = new TrafficLightSimulationManager();
        public const int SIM_MOD = 64;

        /// <summary>
        /// For each node id: traffic light simulation assigned to the node
        /// </summary>
        public TrafficLightSimulation[] TrafficLightSimulations { get; private set; } = null;
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

        public void GetTrafficLightState(
#if DEBUG
            ushort vehicleId, ref Vehicle vehicleData,
#endif
            ushort nodeId, ushort fromSegmentId, byte fromLaneIndex, ushort toSegmentId, ref NetSegment segmentData, uint frame, out RoadBaseAI.TrafficLightState vehicleLightState, out RoadBaseAI.TrafficLightState pedestrianLightState) {

            bool callStockMethod = true;
#if BENCHMARK
			using (var bm = new Benchmark(null, "callStockMethod")) {
#endif
            callStockMethod = !Options.timedLightsEnabled || !TrafficLightSimulationManager.Instance.TrafficLightSimulations[nodeId].IsSimulationRunning();
#if BENCHMARK
			}
#endif

            if (callStockMethod) {
                RoadBaseAI.GetTrafficLightState(nodeId, ref segmentData, frame, out vehicleLightState, out pedestrianLightState);
            } else {
#if BENCHMARK
				using (var bm = new Benchmark(null, "GetCustomTrafficLightState")) {
#endif
                GetCustomTrafficLightState(
#if DEBUG
                    vehicleId, ref vehicleData,
#endif
                    nodeId, fromSegmentId, fromLaneIndex, toSegmentId, out vehicleLightState, out pedestrianLightState, ref TrafficLightSimulationManager.Instance.TrafficLightSimulations[nodeId]);
#if BENCHMARK
				}
#endif
            }
        }

        public void GetTrafficLightState(
#if DEBUG
            ushort vehicleId, ref Vehicle vehicleData,
#endif
            ushort nodeId, ushort fromSegmentId, byte fromLaneIndex, ushort toSegmentId, ref NetSegment segmentData, uint frame, out RoadBaseAI.TrafficLightState vehicleLightState, out RoadBaseAI.TrafficLightState pedestrianLightState, out bool vehicles, out bool pedestrians) {


            bool callStockMethod = true;

#if BENCHMARK
			using (var bm = new Benchmark(null, "callStockMethod")) {
#endif
            callStockMethod = !Options.timedLightsEnabled || !TrafficLightSimulationManager.Instance.TrafficLightSimulations[nodeId].IsSimulationRunning();
#if BENCHMARK
			}
#endif

            if (callStockMethod) {
                RoadBaseAI.GetTrafficLightState(nodeId, ref segmentData, frame, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians);
            } else {
#if BENCHMARK
				using (var bm = new Benchmark(null, "GetCustomTrafficLightState")) {
#endif
                GetCustomTrafficLightState(
#if DEBUG
                    vehicleId, ref vehicleData,
#endif
                    nodeId, fromSegmentId, fromLaneIndex, toSegmentId, out vehicleLightState, out pedestrianLightState, ref TrafficLightSimulationManager.Instance.TrafficLightSimulations[nodeId]);
#if BENCHMARK
				}
#endif
                vehicles = false;
                pedestrians = false;
            }
        }

        // TODO this should be optimized
        protected void GetCustomTrafficLightState(
#if DEBUG
            ushort vehicleId, ref Vehicle vehicleData,
#endif
            ushort nodeId, ushort fromSegmentId, byte fromLaneIndex, ushort toSegmentId, out RoadBaseAI.TrafficLightState vehicleLightState, out RoadBaseAI.TrafficLightState pedestrianLightState, ref TrafficLightSimulation nodeSim) {

            // get responsible traffic light
            //Log._Debug($"GetTrafficLightState: Getting custom light for vehicle {vehicleId} @ node {nodeId}, segment {fromSegmentId}, lane {fromLaneIndex}.");
            //SegmentGeometry geometry = SegmentGeometry.Get(fromSegmentId);
            //if (geometry == null) {
            //	Log.Error($"GetTrafficLightState: No geometry information @ node {nodeId}, segment {fromSegmentId}.");
            //	vehicleLightState = TrafficLightState.Green;
            //	pedestrianLightState = TrafficLightState.Green;
            //	return;
            //}

            // determine node position at `fromSegment` (start/end)
            //bool isStartNode = geometry.StartNodeId == nodeId;
            bool? isStartNode = Services.NetService.IsStartNode(fromSegmentId, nodeId);
            if (isStartNode == null) {
                Log.Error($"GetTrafficLightState: Invalid node {nodeId} for segment {fromSegmentId}.");
                vehicleLightState = TrafficLightState.Green;
                pedestrianLightState = TrafficLightState.Green;
                return;
            }

            ICustomSegmentLights lights = CustomSegmentLightsManager.Instance.GetSegmentLights(fromSegmentId, (bool)isStartNode, false);

            if (lights != null) {
                // get traffic lights state for pedestrians
                pedestrianLightState = (lights.PedestrianLightState != null) ? (RoadBaseAI.TrafficLightState)lights.PedestrianLightState : RoadBaseAI.TrafficLightState.Green;
            } else {
                pedestrianLightState = TrafficLightState.Green;
                Log._Debug($"GetTrafficLightState: No pedestrian light @ node {nodeId}, segment {fromSegmentId} found.");
            }

            ICustomSegmentLight light = lights == null ? null : lights.GetCustomLight(fromLaneIndex);
            if (lights == null || light == null) {
                //Log.Warning($"GetTrafficLightState: No custom light for vehicle {vehicleId} @ node {nodeId}, segment {fromSegmentId}, lane {fromLaneIndex} found. lights null? {lights == null} light null? {light == null}");
                vehicleLightState = RoadBaseAI.TrafficLightState.Green;
                return;
            }

            // get traffic light state from responsible traffic light
            vehicleLightState = light.GetLightState(toSegmentId);
#if DEBUG
            //Log._Debug($"GetTrafficLightState: Getting light for vehicle {vehicleId} @ node {nodeId}, segment {fromSegmentId}, lane {fromLaneIndex}. vehicleLightState={vehicleLightState}, pedestrianLightState={pedestrianLightState}");
#endif
        }

        public void SetVisualState(ushort nodeId, ref NetSegment segmentData, uint frame, RoadBaseAI.TrafficLightState vehicleLightState, RoadBaseAI.TrafficLightState pedestrianLightState, bool vehicles, bool pedestrians) {
            // stock code from RoadBaseAI.SetTrafficLightState

            int num = (int)pedestrianLightState << 2 | (int)vehicleLightState;
            if (segmentData.m_startNode == nodeId) {
                if ((frame >> 8 & 1u) == 0u) {
                    segmentData.m_trafficLightState0 = (byte)((int)(segmentData.m_trafficLightState0 & 240) | num);
                } else {
                    segmentData.m_trafficLightState1 = (byte)((int)(segmentData.m_trafficLightState1 & 240) | num);
                }
                if (vehicles) {
                    segmentData.m_flags |= NetSegment.Flags.TrafficStart;
                } else {
                    segmentData.m_flags &= ~NetSegment.Flags.TrafficStart;
                }
                if (pedestrians) {
                    segmentData.m_flags |= NetSegment.Flags.CrossingStart;
                } else {
                    segmentData.m_flags &= ~NetSegment.Flags.CrossingStart;
                }
            } else {
                if ((frame >> 8 & 1u) == 0u) {
                    segmentData.m_trafficLightState0 = (byte)((int)(segmentData.m_trafficLightState0 & 15) | num << 4);
                } else {
                    segmentData.m_trafficLightState1 = (byte)((int)(segmentData.m_trafficLightState1 & 15) | num << 4);
                }
                if (vehicles) {
                    segmentData.m_flags |= NetSegment.Flags.TrafficEnd;
                } else {
                    segmentData.m_flags &= ~NetSegment.Flags.TrafficEnd;
                }
                if (pedestrians) {
                    segmentData.m_flags |= NetSegment.Flags.CrossingEnd;
                } else {
                    segmentData.m_flags &= ~NetSegment.Flags.CrossingEnd;
                }
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
            return SetUpManualTrafficLight(ref TrafficLightSimulations[nodeId]);
        }

        /// <summary>
        /// Adds a timed traffic light simulation to the node with the given id
        /// </summary>
        /// <param name="nodeId"></param>
        public bool SetUpTimedTrafficLight(ushort nodeId, IList<ushort> nodeGroup) { // TODO improve signature
            if (! SetUpTimedTrafficLight(ref TrafficLightSimulations[nodeId], nodeGroup)) {
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
                List<ushort> oldNodeGroup = new List<ushort>(TrafficLightSimulations[nodeId].timedLight.NodeGroup);
                foreach (ushort timedNodeId in oldNodeGroup) {
                    if (!TrafficLightSimulations[timedNodeId].HasSimulation()) {
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
                        if (TrafficLightSimulations[nodeId].IsTimedLight()) {
                            TrafficLightSimulations[timedNodeId].timedLight.RemoveNodeFromGroup(nodeId);
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

            Destroy(ref TrafficLightSimulations[nodeId]);
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();
            for (uint nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
                Destroy(ref TrafficLightSimulations[nodeId]);
            }
        }

        public bool SetUpManualTrafficLight(ref TrafficLightSimulation sim) {
            if (sim.IsTimedLight()) {
                return false;
            }

            Constants.ServiceFactory.NetService.ProcessNode(sim.nodeId, delegate (ushort nId, ref NetNode node) {
                Constants.ManagerFactory.TrafficLightManager.AddTrafficLight(nId, ref node);
                return true;
            });

            Constants.ManagerFactory.CustomSegmentLightsManager.AddNodeLights(sim.nodeId);
            sim.type = TrafficLightSimulationType.Manual;
            return true;
        }

        public bool DestroyManualTrafficLight(ref TrafficLightSimulation sim) {
            if (sim.IsTimedLight()) {
                return false;
            }
            if (!sim.IsManualLight()) {
                return false;
            }

            sim.type = TrafficLightSimulationType.None;
            Constants.ManagerFactory.CustomSegmentLightsManager.RemoveNodeLights(sim.nodeId);
            return true;
        }

        public bool SetUpTimedTrafficLight(ref TrafficLightSimulation sim, IList<ushort> nodeGroup) {
            if (sim.IsManualLight()) {
                DestroyManualTrafficLight(ref sim);
            }

            if (sim.IsTimedLight()) {
                return false;
            }

            Constants.ServiceFactory.NetService.ProcessNode(sim.nodeId, delegate (ushort nId, ref NetNode node) {
                Constants.ManagerFactory.TrafficLightManager.AddTrafficLight(nId, ref node);
                return true;
            });

            Constants.ManagerFactory.CustomSegmentLightsManager.AddNodeLights(sim.nodeId);
            sim.timedLight = new TimedTrafficLights(sim.nodeId, nodeGroup);
            sim.type = TrafficLightSimulationType.Timed;
            return true;
        }

        public bool DestroyTimedTrafficLight(ref TrafficLightSimulation sim) {
            if (!sim.IsTimedLight()) {
                return false;
            }

            sim.type = TrafficLightSimulationType.None;
            ITimedTrafficLights timedLight = sim.timedLight;
            sim.timedLight = null;

            if (timedLight != null) {
                timedLight.Destroy();
            }
            return true;
        }

        public void Destroy(ref TrafficLightSimulation sim) {
            DestroyTimedTrafficLight(ref sim);
            DestroyManualTrafficLight(ref sim);
        }

        protected override void HandleInvalidNode(ushort nodeId, ref NetNode node) {
            RemoveNodeFromSimulation(nodeId, false, true);
        }

        protected override void HandleValidNode(ushort nodeId, ref NetNode node) {
#if DEBUG
            bool debug = GlobalConfig.Instance.Debug.Switches[7] && (GlobalConfig.Instance.Debug.NodeId == 0 || GlobalConfig.Instance.Debug.NodeId == nodeId);
#endif

            if (!TrafficLightSimulations[nodeId].HasSimulation()) {
#if DEBUG
                if (debug)
                    Log._Debug($"TrafficLightSimulationManager.HandleValidNode({nodeId}): Node is not controlled by a custom traffic light simulation.");
#endif
                return;
            }

            if (! Flags.mayHaveTrafficLight(nodeId)) {
#if DEBUG
                if (debug)
                    Log._Debug($"TrafficLightSimulationManager.HandleValidNode({nodeId}): Node must not have a traffic light: Removing traffic light simulation.");
#endif
                RemoveNodeFromSimulation(nodeId, false, true);
                return;
            }

            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0) {
                    continue;
                }

                bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);

#if DEBUG
                if (debug)
                    Log._Debug($"TrafficLightSimulationManager.HandleValidNode({nodeId}): Adding live traffic lights to segment {segmentId}");
#endif
                // housekeep timed light
                CustomSegmentLightsManager.Instance.GetSegmentLights(segmentId, startNode).Housekeeping(true, true);
            }

            // ensure there is a physical traffic light
            Constants.ManagerFactory.TrafficLightManager.AddTrafficLight(nodeId, ref node);

            TrafficLightSimulations[nodeId].Update();
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

#if DEBUGLOAD
					Log._Debug($"Adding timed light at node {cnfTimedLights.nodeId}. NodeGroup: {string.Join(", ", nodeGroup.Select(x => x.ToString()).ToArray())}");
#endif

                    SetUpTimedTrafficLight(cnfTimedLights.nodeId, nodeGroup);

                    int j = 0;
                    foreach (Configuration.TimedTrafficLightsStep cnfTimedStep in cnfTimedLights.timedSteps) {
#if DEBUGLOAD
						Log._Debug($"Loading timed step {j} at node {cnfTimedLights.nodeId}");
#endif
                        ITimedTrafficLightsStep step = TrafficLightSimulations[cnfTimedLights.nodeId].timedLight.AddStep(cnfTimedStep.minTime, cnfTimedStep.maxTime, (StepChangeMetric)cnfTimedStep.changeMetric, cnfTimedStep.waitFlowBalance);

                        foreach (KeyValuePair<ushort, Configuration.CustomSegmentLights> e in cnfTimedStep.segmentLights) {
                            if (!Services.NetService.IsSegmentValid(e.Key))
                                continue;
                            e.Value.nodeId = cnfTimedLights.nodeId;

#if DEBUGLOAD
							Log._Debug($"Loading timed step {j}, segment {e.Key} at node {cnfTimedLights.nodeId}");
#endif
                            ICustomSegmentLights lights = null;
                            if (!step.CustomSegmentLights.TryGetValue(e.Key, out lights)) {
#if DEBUGLOAD
								Log._Debug($"No segment lights found at timed step {j} for segment {e.Key}, node {cnfTimedLights.nodeId}");
#endif
                                continue;
                            }
                            Configuration.CustomSegmentLights cnfLights = e.Value;

#if DEBUGLOAD
							Log._Debug($"Loading pedestrian light @ seg. {e.Key}, step {j}: {cnfLights.pedestrianLightState} {cnfLights.manualPedestrianMode}");
#endif

                            lights.ManualPedestrianMode = cnfLights.manualPedestrianMode;
                            lights.PedestrianLightState = cnfLights.pedestrianLightState;

                            bool first = true; // v1.10.2 transitional code
                            foreach (KeyValuePair<Traffic.ExtVehicleType, Configuration.CustomSegmentLight> e2 in cnfLights.customLights) {
#if DEBUGLOAD
								Log._Debug($"Loading timed step {j}, segment {e.Key}, vehicleType {e2.Key} at node {cnfTimedLights.nodeId}");
#endif
                                ICustomSegmentLight light = null;
                                if (!lights.CustomLights.TryGetValue(LegacyExtVehicleType.ToNew(e2.Key), out light)) {
#if DEBUGLOAD
									Log._Debug($"No segment light found for timed step {j}, segment {e.Key}, vehicleType {e2.Key} at node {cnfTimedLights.nodeId}");
#endif
                                    // v1.10.2 transitional code START
                                    if (first) {
                                        first = false;
                                        if (!lights.CustomLights.TryGetValue(CustomSegmentLights.DEFAULT_MAIN_VEHICLETYPE, out light)) {
#if DEBUGLOAD
											Log._Debug($"No segment light found for timed step {j}, segment {e.Key}, DEFAULT vehicleType {CustomSegmentLights.DEFAULT_MAIN_VEHICLETYPE} at node {cnfTimedLights.nodeId}");
#endif
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

                                light.InternalCurrentMode = (LightMode)cnfLight.currentMode; // TODO improve & remove
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
                    ITimedTrafficLights timedNode = TrafficLightSimulations[cnfTimedLights.nodeId].timedLight;

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

#if DEBUGSAVE
					Log._Debug($"Going to save timed light at node {nodeId}.");
#endif

                    ITimedTrafficLights timedNode = TrafficLightSimulations[nodeId].timedLight;
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

                    for (int j = 0; j < timedNode.NumSteps(); j++) {
#if DEBUGSAVE
						Log._Debug($"Saving timed light step {j} at node {nodeId}.");
#endif
                        ITimedTrafficLightsStep timedStep = timedNode.GetStep(j);
                        Configuration.TimedTrafficLightsStep cnfTimedStep = new Configuration.TimedTrafficLightsStep();
                        cnfTimedLights.timedSteps.Add(cnfTimedStep);

                        cnfTimedStep.minTime = timedStep.MinTime;
                        cnfTimedStep.maxTime = timedStep.MaxTime;
                        cnfTimedStep.changeMetric = (int)timedStep.ChangeMetric;
                        cnfTimedStep.waitFlowBalance = timedStep.WaitFlowBalance;
                        cnfTimedStep.segmentLights = new Dictionary<ushort, Configuration.CustomSegmentLights>();
                        foreach (KeyValuePair<ushort, ICustomSegmentLights> e in timedStep.CustomSegmentLights) {
#if DEBUGSAVE
							Log._Debug($"Saving timed light step {j}, segment {e.Key} at node {nodeId}.");
#endif

                            ICustomSegmentLights segLights = e.Value;
                            Configuration.CustomSegmentLights cnfSegLights = new Configuration.CustomSegmentLights();

                            ushort lightsNodeId = segLights.NodeId;
                            if (lightsNodeId == 0 || lightsNodeId != timedNode.NodeId) {
                                Log.Warning($"Inconsistency detected: Timed traffic light @ node {timedNode.NodeId} contains custom traffic lights for the invalid segment ({segLights.SegmentId}) at step {j}: nId={lightsNodeId}");
                                continue;
                            }

                            cnfSegLights.nodeId = lightsNodeId; // TODO not needed
                            cnfSegLights.segmentId = segLights.SegmentId; // TODO not needed
                            cnfSegLights.customLights = new Dictionary<Traffic.ExtVehicleType, Configuration.CustomSegmentLight>();
                            cnfSegLights.pedestrianLightState = segLights.PedestrianLightState;
                            cnfSegLights.manualPedestrianMode = segLights.ManualPedestrianMode;

                            cnfTimedStep.segmentLights.Add(e.Key, cnfSegLights);

#if DEBUGSAVE
							Log._Debug($"Saving pedestrian light @ seg. {e.Key}, step {j}: {cnfSegLights.pedestrianLightState} {cnfSegLights.manualPedestrianMode}");
#endif

                            foreach (KeyValuePair<API.Traffic.Enums.ExtVehicleType, ICustomSegmentLight> e2 in segLights.CustomLights) {
#if DEBUGSAVE
								Log._Debug($"Saving timed light step {j}, segment {e.Key}, vehicleType {e2.Key} at node {nodeId}.");
#endif

                                ICustomSegmentLight segLight = e2.Value;
                                Configuration.CustomSegmentLight cnfSegLight = new Configuration.CustomSegmentLight();
                                cnfSegLights.customLights.Add(LegacyExtVehicleType.ToOld(e2.Key), cnfSegLight);

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