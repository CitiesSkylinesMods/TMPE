namespace TrafficManager.Manager.Impl {
    using CSUtil.Commons;
    using ExtVehicleType = global::TrafficManager.Traffic.ExtVehicleType;
    using static RoadBaseAI;
    using System.Collections.Generic;
    using System.Linq;
    using System;
    using TimedTrafficLights = global::TrafficManager.TrafficLight.Impl.TimedTrafficLights;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State;
    using TrafficManager.Traffic;
    using TrafficManager.TrafficLight.Impl;
    using TrafficManager.Util;
    using ColossalFramework;
    using TrafficManager.Util.Extensions;

    public class TrafficLightSimulationManager
        : AbstractGeometryObservingManager,
          ICustomDataManager<List<Configuration.TimedTrafficLights>>,
          ITrafficLightSimulationManager
    {
        private const int SIM_MOD = 64;

        private TrafficLightSimulationManager() {
            TrafficLightSimulations = new TrafficLightSimulation[NetManager.MAX_NODE_COUNT];

            for (int i = 0; i < TrafficLightSimulations.Length; ++i) {
                TrafficLightSimulations[i] = new TrafficLightSimulation((ushort)i);
            }
        }

        public static readonly TrafficLightSimulationManager Instance =
            new TrafficLightSimulationManager();

        /// <summary>
        /// For each node id: traffic light simulation assigned to the node
        /// </summary>
        public TrafficLightSimulation[] TrafficLightSimulations { get; }

        // public Dictionary<ushort, TrafficLightSimulation> TrafficLightSimulations
        // = new Dictionary<ushort, TrafficLightSimulation>();

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug($"Traffic light simulations:");

            for (int i = 0; i < TrafficLightSimulations.Length; ++i) {
                if (!TrafficLightSimulations[i].HasSimulation()) {
                    continue;
                }

                Log._Debug($"Simulation {i}: {TrafficLightSimulations[i]}");
            }
        }

        public void GetTrafficLightState(
#if DEBUG
            ushort vehicleId,
            ref Vehicle vehicleData,
#endif
            ushort nodeId,
            ushort fromSegmentId,
            byte fromLaneIndex,
            ushort toSegmentId,
            ref NetSegment segmentData,
            uint frame,
            out TrafficLightState vehicleLightState,
            out TrafficLightState pedestrianLightState)
        {
            bool callStockMethod = !SavedGameOptions.Instance.timedLightsEnabled
                || !Instance.TrafficLightSimulations[nodeId].IsSimulationRunning();

            if (callStockMethod) {
                RoadBaseAI.GetTrafficLightState(
                    nodeId,
                    ref segmentData,
                    frame,
                    out vehicleLightState,
                    out pedestrianLightState);
            } else {
                GetCustomTrafficLightState(
#if DEBUG
                    vehicleId,
                    ref vehicleData,
#endif
                    nodeId,
                    fromSegmentId,
                    fromLaneIndex,
                    toSegmentId,
                    out vehicleLightState,
                    out pedestrianLightState,
                    ref Instance.TrafficLightSimulations[nodeId]);
            }
        }

        public void GetTrafficLightState(
#if DEBUG
            ushort vehicleId,
            ref Vehicle vehicleData,
#endif
            ushort nodeId,
            ushort fromSegmentId,
            byte fromLaneIndex,
            ushort toSegmentId,
            ref NetSegment segmentData,
            uint frame,
            out TrafficLightState vehicleLightState,
            out TrafficLightState pedestrianLightState,
            out bool vehicles,
            out bool pedestrians)
        {
            bool callStockMethod = !SavedGameOptions.Instance.timedLightsEnabled
                || !Instance.TrafficLightSimulations[nodeId].IsSimulationRunning();

            if (callStockMethod) {
                RoadBaseAI.GetTrafficLightState(
                    nodeId,
                    ref segmentData,
                    frame,
                    out vehicleLightState,
                    out pedestrianLightState,
                    out vehicles,
                    out pedestrians);
            } else {
                GetCustomTrafficLightState(
#if DEBUG
                    vehicleId,
                    ref vehicleData,
#endif
                    nodeId,
                    fromSegmentId,
                    fromLaneIndex,
                    toSegmentId,
                    out vehicleLightState,
                    out pedestrianLightState,
                    ref Instance.TrafficLightSimulations[nodeId]);

                vehicles = false;
                pedestrians = false;
            }
        }

        // TODO this should be optimized
        protected void GetCustomTrafficLightState(
#if DEBUG
            ushort vehicleId,
            ref Vehicle vehicleData,
#endif
            ushort nodeId,
            ushort fromSegmentId,
            byte fromLaneIndex,
            ushort toSegmentId,
            out TrafficLightState vehicleLightState,
            out TrafficLightState pedestrianLightState,
            ref TrafficLightSimulation nodeSim) {

            // get responsible traffic light
            // Log._Debug($"GetTrafficLightState: Getting custom light for vehicle {vehicleId} @
            //     node {nodeId}, segment {fromSegmentId}, lane {fromLaneIndex}.");
            // SegmentGeometry geometry = SegmentGeometry.Get(fromSegmentId);
            // if (geometry == null) {
            //    Log.Error($"GetTrafficLightState: No geometry information @ node {nodeId}, segment {fromSegmentId}.");
            //    vehicleLightState = TrafficLightState.Green;
            //    pedestrianLightState = TrafficLightState.Green;
            //    return;
            // }

            // determine node position at `fromSegment` (start/end)
            // bool isStartNode = geometry.StartNodeId == nodeId;
            bool? isStartNode = fromSegmentId.ToSegment().GetRelationToNode(nodeId);

            if (!isStartNode.HasValue) {
                Log.Error($"GetTrafficLightState: Invalid node {nodeId} for segment {fromSegmentId}.");
                vehicleLightState = TrafficLightState.Green;
                pedestrianLightState = TrafficLightState.Green;
                return;
            }

            CustomSegmentLights lights =
                CustomSegmentLightsManager.Instance.GetSegmentLights(
                    fromSegmentId,
                    isStartNode.Value,
                    false);

            if (lights != null) {
                // get traffic lights state for pedestrians
                pedestrianLightState = lights.PedestrianLightState ?? TrafficLightState.Green;
            } else {
                pedestrianLightState = TrafficLightState.Green;
                Log._Debug($"GetTrafficLightState: No pedestrian light @ node {nodeId}, " +
                           $"segment {fromSegmentId} found.");
            }

            CustomSegmentLight light = lights?.GetCustomLight(fromLaneIndex);

            if (light == null) {
                // Log.Warning($"GetTrafficLightState: No custom light for vehicle {vehicleId} @ node
                //     {nodeId}, segment {fromSegmentId}, lane {fromLaneIndex} found. lights null?
                //     {lights == null} light null? {light == null}");
                vehicleLightState = TrafficLightState.Green;
                return;
            }

            // get traffic light state from responsible traffic light
            vehicleLightState = light.GetLightState(toSegmentId);
#if DEBUG
            // Log._Debug($"GetTrafficLightState: Getting light for vehicle {vehicleId} @ node {nodeId},
            //     segment {fromSegmentId}, lane {fromLaneIndex}. vehicleLightState={vehicleLightState},
            //     pedestrianLightState={pedestrianLightState}");
#endif
        }

        public void SetVisualState(ushort nodeId,
                                   ref NetSegment segmentData,
                                   uint frame,
                                   TrafficLightState vehicleLightState,
                                   TrafficLightState pedestrianLightState,
                                   bool vehicles,
                                   bool pedestrians) {
            // STOCK-CODE START from RoadBaseAI.SetTrafficLightState
            int num = (int)pedestrianLightState << 2 | (int)vehicleLightState;
            if (segmentData.m_startNode == nodeId) {
                if ((frame >> 8 & 1u) == 0u) {
                    // update 'x' bits (int)[0000 0000 0000 xxxx]
                    // -16 / FFF0 / [1111 1111 1111 0000]
                    segmentData.m_trafficLightState0 =
                        (segmentData.m_trafficLightState0 & -16 | num);
                } else {
                    segmentData.m_trafficLightState1 =
                        (segmentData.m_trafficLightState1 & -16 | num);
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
                    // update 'x' bits (int)[0000 0000 xxxx 0000]
                    // -241 / FF0F / [1111 1111 0000 1111]
                    segmentData.m_trafficLightState0 =
                        (segmentData.m_trafficLightState0 & -241 | num << 4);
                } else {
                    segmentData.m_trafficLightState1 =
                        (segmentData.m_trafficLightState1 & -241 | num << 4);
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
            // STOCK-CODE END
        }

        public static void SetBollardVisualState(ushort nodeID,
                                                 ref NetSegment segmentData,
                                                 uint frame,
                                                 TrafficLightState enterState,
                                                 TrafficLightState exitState,
                                                 bool enter,
                                                 bool exit,
                                                 bool skipEnterUpdate = false)
        {
            int num = ((int)exitState << 2) | (int)enterState;
            if (segmentData.m_startNode == nodeID) {
                if (((frame >> 8) & 1) == 0) {
                    segmentData.m_trafficLightState0 =
                        ((segmentData.m_trafficLightState0 & -3841) | (num << 8));
                } else {
                    segmentData.m_trafficLightState1 =
                        ((segmentData.m_trafficLightState1 & -3841) | (num << 8));
                }
                if (!skipEnterUpdate) {
                    if (enter) {
                        segmentData.m_flags2 |= NetSegment.Flags2.BollardEnterStart;
                    } else {
                        segmentData.m_flags2 &= (NetSegment.Flags2)253;
                    }
                }
                if (exit) {
                    segmentData.m_flags2 |= NetSegment.Flags2.BollardExitStart;
                } else {
                    segmentData.m_flags2 &= (NetSegment.Flags2)247;
                }
            } else {
                if (((frame >> 8) & 1) == 0) {
                    segmentData.m_trafficLightState0 = ((segmentData.m_trafficLightState0 & -61441) | (num << 12));
                } else {
                    segmentData.m_trafficLightState1 = ((segmentData.m_trafficLightState1 & -61441) | (num << 12));
                }
                if (!skipEnterUpdate) {
                    if (enter) {
                        segmentData.m_flags2 |= NetSegment.Flags2.BollardEnterEnd;
                    } else {
                        segmentData.m_flags2 &= (NetSegment.Flags2)251;
                    }
                }
                if (exit) {
                    segmentData.m_flags2 |= NetSegment.Flags2.BollardExitEnd;
                } else {
                    segmentData.m_flags2 &= (NetSegment.Flags2)239;
                }
            }
        }

        public void SimulationStep() {
            var frame = (int)(Singleton<SimulationManager>.instance.m_currentFrameIndex & (SIM_MOD - 1));
            int minIndex = frame * (NetManager.MAX_NODE_COUNT / SIM_MOD);
            int maxIndex = ((frame + 1) * (NetManager.MAX_NODE_COUNT / SIM_MOD)) - 1;

            ushort failedNodeId = 0;
            try {
                for (int nodeId = minIndex; nodeId <= maxIndex; ++nodeId) {
                    failedNodeId = (ushort)nodeId;
                    TrafficLightSimulations[nodeId].SimulationStep();
                }

                failedNodeId = 0;
            }
            catch (Exception ex) {
                Log.Error($"Error occurred while simulating traffic light @ node {failedNodeId}: {ex}");

                if (failedNodeId != 0) {
                    RemoveNodeFromSimulation(failedNodeId);
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
        public bool SetUpTimedTrafficLight(ushort nodeId, IList<ushort> nodeGroup) {
            // TODO improve signature
            if (!SetUpTimedTrafficLight(ref TrafficLightSimulations[nodeId], nodeGroup)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Destroys the traffic light and removes it
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="destroyGroup"></param>
        public void RemoveNodeFromSimulation(ushort nodeId,
                                             bool destroyGroup,
                                             bool removeTrafficLight)
        {
            Log._Debug($"TrafficLightSimulationManager.RemoveNodeFromSimulation({nodeId}, " +
                       $"{destroyGroup}, {removeTrafficLight}) called.");

            if (!TrafficLightSimulations[nodeId].HasSimulation()) {
                return;
            }

            TrafficLightManager tlm = TrafficLightManager.Instance;

            if (TrafficLightSimulations[nodeId].IsTimedLight()) {
                // remove/destroy all timed traffic lights in group
                var oldNodeGroup = new List<ushort>(TrafficLightSimulations[nodeId].timedLight.NodeGroup);

                foreach (ushort timedNodeId in oldNodeGroup) {
                    if (!TrafficLightSimulations[timedNodeId].HasSimulation()) {
                        continue;
                    }

                    if (destroyGroup || timedNodeId == nodeId) {
                        // Log._Debug($"Slave: Removing simulation @ node {timedNodeId}");
                        // TrafficLightSimulations[timedNodeId].Destroy();
                        RemoveNodeFromSimulation(timedNodeId);
                        if (removeTrafficLight) {
                            tlm.RemoveTrafficLight(timedNodeId, ref timedNodeId.ToNode());
                        }
                    } else {
                        if (TrafficLightSimulations[timedNodeId].IsTimedLight()) {
                            TrafficLightSimulations[timedNodeId].timedLight.RemoveNodeFromGroup(nodeId);
                        }
                    }
                }
            }

            // Flags.setNodeTrafficLight(nodeId, false);
            // sim.DestroyTimedTrafficLight();
            // TrafficLightSimulations[nodeId].DestroyManualTrafficLight();
            RemoveNodeFromSimulation(nodeId);

            if (removeTrafficLight) {
                tlm.RemoveTrafficLight(nodeId, ref nodeId.ToNode());
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
            Log._Debug($"TrafficLightSimulationManager.RemoveNodeFromSimulation({nodeId}) called.");
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

            TrafficLightManager.Instance.AddTrafficLight(
                sim.nodeId,
                ref sim.nodeId.ToNode());

            CustomSegmentLightsManager.Instance.AddNodeLights(sim.nodeId);
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
            CustomSegmentLightsManager.Instance.RemoveNodeLights(sim.nodeId);
            return true;
        }

        public bool SetUpTimedTrafficLight(ref TrafficLightSimulation sim,
                                           IEnumerable<ushort> nodeGroup)
        {
            if (sim.IsManualLight()) {
                DestroyManualTrafficLight(ref sim);
            }

            if (sim.IsTimedLight()) {
                return false;
            }

            TrafficLightManager.Instance.AddTrafficLight(
                sim.nodeId,
                ref sim.nodeId.ToNode());

            CustomSegmentLightsManager.Instance.AddNodeLights(sim.nodeId);
            sim.timedLight = new TimedTrafficLights(sim.nodeId, nodeGroup);
            sim.type = TrafficLightSimulationType.Timed;
            return true;
        }

        public bool DestroyTimedTrafficLight(ref TrafficLightSimulation sim) {
            if (!sim.IsTimedLight()) {
                return false;
            }

            sim.type = TrafficLightSimulationType.None;
            TimedTrafficLights timedLight = sim.timedLight;
            sim.timedLight = null;

            timedLight?.Destroy();

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
            bool logTrafficLights = DebugSwitch.TimedTrafficLights.Get()
                         && (DebugSettings.NodeId == 0 || DebugSettings.NodeId == nodeId);
#else
            const bool logTrafficLights = false;
#endif

            if (!TrafficLightSimulations[nodeId].HasSimulation()) {
                if (logTrafficLights) {
                    Log._Debug($"TrafficLightSimulationManager.HandleValidNode({nodeId}): " +
                               "Node is not controlled by a custom traffic light simulation.");
                }

                return;
            }

            if (!Flags.MayHaveTrafficLight(nodeId)) {
                if (logTrafficLights) {
                    Log._Debug(
                        $"TrafficLightSimulationManager.HandleValidNode({nodeId}): Node must not " +
                        "have a traffic light: Removing traffic light simulation.");
                }

                RemoveNodeFromSimulation(nodeId, false, true);
                return;
            }

            for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                ushort segmentId = node.GetSegment(segmentIndex);
                if (segmentId == 0) {
                    continue;
                }

                var startNode = segmentId.ToSegment().IsStartNode(nodeId);

                if (logTrafficLights) {
                    Log._Debug($"TrafficLightSimulationManager.HandleValidNode({nodeId}): Adding " +
                               $"live traffic lights to segment {segmentId}");
                }

                // housekeep timed light
                CustomSegmentLightsManager
                    .Instance
                    .GetSegmentLights(segmentId, startNode)
                    .Housekeeping(true, true);
            }

            // ensure there is a physical traffic light
            TrafficLightManager.Instance.AddTrafficLight(nodeId, ref node);

            TrafficLightSimulations[nodeId].Update();
        }

        public bool LoadData(List<Configuration.TimedTrafficLights> data) {
            bool success = true;
            Log.Info($"Loading {data.Count} timed traffic lights (new method)");

            var nodesWithSimulation = new HashSet<ushort>();

            foreach (Configuration.TimedTrafficLights cnfTimedLights in data) {
                nodesWithSimulation.Add(cnfTimedLights.nodeId);
            }

            var masterNodeIdBySlaveNodeId = new Dictionary<ushort, ushort>();
            var nodeGroupByMasterNodeId = new Dictionary<ushort, List<ushort>>();

            foreach (Configuration.TimedTrafficLights cnfTimedLights in data) {
                try {
                    // TODO most of this should not be necessary at all if the classes around TimedTrafficLights class were properly designed
                    // enforce uniqueness of node ids
                    List<ushort> currentNodeGroup = cnfTimedLights.nodeGroup.Distinct().ToList();

                    if (!currentNodeGroup.Contains(cnfTimedLights.nodeId)) {
                        currentNodeGroup.Add(cnfTimedLights.nodeId);
                    }

                    // remove any nodes that are not configured to have a simulation
                    currentNodeGroup = new List<ushort>(
                        currentNodeGroup.Intersect(nodesWithSimulation));

                    // remove invalid nodes from the group; find if any of the nodes in the group is already a master node
                    ushort masterNodeId = 0;
                    int foundMasterNodes = 0;

                    for (int i = 0; i < currentNodeGroup.Count;) {
                        ushort nodeId = currentNodeGroup[i];
                        ref NetNode netNode = ref nodeId.ToNode();

                        if (!netNode.IsValid()) {
                            currentNodeGroup.RemoveAt(i);
                            continue;
                        }

                        if (nodeGroupByMasterNodeId.ContainsKey(nodeId)) {
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

                    // ensure the master node is the first node in the list (TimedTrafficLights
                    // depends on this at the moment...)
                    currentNodeGroup.Remove(masterNodeId);
                    currentNodeGroup.Insert(0, masterNodeId);

                    // update the saved node group and master-slave info
                    nodeGroupByMasterNodeId[masterNodeId] = currentNodeGroup;

                    foreach (ushort nodeId in currentNodeGroup) {
                        masterNodeIdBySlaveNodeId[nodeId] = masterNodeId;
                    }
                } catch (Exception e) {
                    Log.WarningFormat(
                        "Error building timed traffic light group for TimedNode {0} (NodeGroup: {1}): {2}",
                        cnfTimedLights.nodeId,
                        string.Join(", ", cnfTimedLights.nodeGroup.Select(x => x.ToString()).ToArray()),
                        e);
                    success = false;
                }
            }

            foreach (Configuration.TimedTrafficLights cnfTimedLights in data) {
                try {
                    if (!masterNodeIdBySlaveNodeId.ContainsKey(cnfTimedLights.nodeId)) {
                        continue;
                    }

                    ushort masterNodeId = masterNodeIdBySlaveNodeId[cnfTimedLights.nodeId];
                    List<ushort> nodeGroup = nodeGroupByMasterNodeId[masterNodeId];

#if DEBUGLOAD
                    Log._Debug($"Adding timed light at node {cnfTimedLights.nodeId}. NodeGroup: "+
                    $"{string.Join(", ", nodeGroup.Select(x => x.ToString()).ToArray())}");
#endif
                    SetUpTimedTrafficLight(cnfTimedLights.nodeId, nodeGroup);

                    int j = 0;
                    foreach (Configuration.TimedTrafficLightsStep cnfTimedStep in cnfTimedLights.timedSteps) {
#if DEBUGLOAD
                        Log._Debug($"Loading timed step {j} at node {cnfTimedLights.nodeId}");
#endif
                        TimedTrafficLightsStep step =
                            TrafficLightSimulations[cnfTimedLights.nodeId].timedLight.AddStep(
                                cnfTimedStep.minTime,
                                cnfTimedStep.maxTime,
                                (StepChangeMetric)cnfTimedStep.changeMetric,
                                cnfTimedStep.waitFlowBalance);

                        foreach (KeyValuePair<ushort, Configuration.CustomSegmentLights> e in
                            cnfTimedStep.segmentLights)
                        {
                            ref NetSegment netSegment = ref e.Key.ToSegment();

                            if (!netSegment.IsValid()) {
                                continue;
                            }

                            e.Value.nodeId = cnfTimedLights.nodeId;

#if DEBUGLOAD
                            Log._Debug($"Loading timed step {j}, segment {e.Key} at node {cnfTimedLights.nodeId}");
#endif
                            CustomSegmentLights lights = null;
                            if (!step.CustomSegmentLights.TryGetValue(e.Key, out lights)) {
#if DEBUGLOAD
                                Log._Debug($"No segment lights found at timed step {j} for segment "+
                                $"{e.Key}, node {cnfTimedLights.nodeId}");
#endif
                                continue;
                            }

                            Configuration.CustomSegmentLights cnfLights = e.Value;

#if DEBUGLOAD
                            Log._Debug($"Loading pedestrian light @ seg. {e.Key}, step {j}: "+
                            $"{cnfLights.pedestrianLightState} {cnfLights.manualPedestrianMode}");
#endif
                            lights.ManualPedestrianMode = cnfLights.manualPedestrianMode;
                            lights.PedestrianLightState = cnfLights.pedestrianLightState;

                            bool first = true; // v1.10.2 transitional code
                            foreach (KeyValuePair<ExtVehicleType, Configuration.CustomSegmentLight> e2
                                in cnfLights.customLights) {
#if DEBUGLOAD
                                Log._Debug($"Loading timed step {j}, segment {e.Key}, vehicleType "+
                                $"{e2.Key} at node {cnfTimedLights.nodeId}");
#endif
                                if (!lights.CustomLights.TryGetValue(
                                        LegacyExtVehicleType.ToNew(e2.Key),
                                        out CustomSegmentLight light)) {
#if DEBUGLOAD
                                    Log._Debug($"No segment light found for timed step {j}, segment "+
                                    $"{e.Key}, vehicleType {e2.Key} at node {cnfTimedLights.nodeId}");
#endif
                                    // v1.10.2 transitional code START
                                    if (first) {
                                        first = false;
                                        if (!lights.CustomLights.TryGetValue(
                                                CustomSegmentLights
                                                    .DEFAULT_MAIN_VEHICLETYPE,
                                                out light)) {
#if DEBUGLOAD
                                            Log._Debug($"No segment light found for timed step {j}, "+
                    $"segment {e.Key}, DEFAULT vehicleType {CustomSegmentLights.DEFAULT_MAIN_VEHICLETYPE} "+
                    $"at node {cnfTimedLights.nodeId}");
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
                                light.SetStates(
                                    cnfLight.mainLight,
                                    cnfLight.leftLight,
                                    cnfLight.rightLight,
                                    false);
                            }
                        }

                        ++j;
                    }
                } catch (Exception e) {
                    // ignore, as it's probably corrupt save data. it'll be culled on next save
                    Log.Warning($"Error loading data from TimedNode (new method): {e}");
                    success = false;
                }
            }

            foreach (Configuration.TimedTrafficLights cnfTimedLights in data) {
                try {
                    TimedTrafficLights timedNode =
                        TrafficLightSimulations[cnfTimedLights.nodeId].timedLight;

                    timedNode.Housekeeping();
                    if (cnfTimedLights.started) {
                        timedNode.Start(cnfTimedLights.currentStep);
                    }
                } catch (Exception e) {
                    Log.Warning($"Error starting timed light @ {cnfTimedLights.nodeId}: {e}");
                    success = false;
                }
            }

            return success;
        }

        public List<Configuration.TimedTrafficLights> SaveData(ref bool success) {
            var ret = new List<Configuration.TimedTrafficLights>();

            for (uint nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
                try {
                    if (!TrafficLightSimulations[nodeId].IsTimedLight()) {
                        continue;
                    }

#if DEBUGSAVE
                    Log._Debug($"Going to save timed light at node {nodeId}.");
#endif
                    TimedTrafficLights timedNode = TrafficLightSimulations[nodeId].timedLight;
                    timedNode.OnGeometryUpdate();

                    var cnfTimedLights = new Configuration.TimedTrafficLights {
                        nodeId = timedNode.NodeId,
                        nodeGroup = new List<ushort>(timedNode.NodeGroup),
                        started = timedNode.IsStarted(),
                    };
                    ret.Add(cnfTimedLights);

                    int stepIndex = timedNode.CurrentStep;
                    if (timedNode.IsStarted() &&
                        timedNode.GetStep(timedNode.CurrentStep).IsInEndTransition()) {
                        // if in end transition save the next step
                        stepIndex = (stepIndex + 1) % timedNode.NumSteps();
                    }

                    cnfTimedLights.currentStep = stepIndex;
                    cnfTimedLights.timedSteps = new List<Configuration.TimedTrafficLightsStep>();

                    for (var j = 0; j < timedNode.NumSteps(); j++) {
#if DEBUGSAVE
                        Log._Debug($"Saving timed light step {j} at node {nodeId}.");
#endif
                        TimedTrafficLightsStep timedStep = timedNode.GetStep(j);
                        var cnfTimedStep = new Configuration.TimedTrafficLightsStep {
                            minTime = timedStep.MinTime,
                            maxTime = timedStep.MaxTime,
                            changeMetric = (int)timedStep.ChangeMetric,
                            waitFlowBalance = timedStep.WaitFlowBalance,
                            segmentLights = new Dictionary<ushort, Configuration.CustomSegmentLights>(),
                        };
                        cnfTimedLights.timedSteps.Add(cnfTimedStep);

                        foreach (KeyValuePair<ushort, CustomSegmentLights> e
                            in timedStep.CustomSegmentLights) {
#if DEBUGSAVE
                            Log._Debug($"Saving timed light step {j}, segment {e.Key} at node {nodeId}.");
#endif

                            CustomSegmentLights segLights = e.Value;
                            ushort lightsNodeId = segLights.NodeId;

                            var cnfSegLights = new Configuration.CustomSegmentLights {
                                nodeId = lightsNodeId, // TODO not needed
                                segmentId = segLights.SegmentId, // TODO not needed
                                customLights = new Dictionary<ExtVehicleType, Configuration.CustomSegmentLight>(),
                                pedestrianLightState = segLights.PedestrianLightState,
                                manualPedestrianMode = segLights.ManualPedestrianMode,
                            };

                            if (lightsNodeId == 0 || lightsNodeId != timedNode.NodeId) {
                                Log.Warning(
                                    "Inconsistency detected: Timed traffic light @ node " +
                                    $"{timedNode.NodeId} contains custom traffic lights for the invalid " +
                                    $"segment ({segLights.SegmentId}) at step {j}: nId={lightsNodeId}");
                                continue;
                            }

                            cnfTimedStep.segmentLights.Add(e.Key, cnfSegLights);

#if DEBUGSAVE
                            Log._Debug($"Saving pedestrian light @ seg. {e.Key}, step {j}: "+
                            $"{cnfSegLights.pedestrianLightState} {cnfSegLights.manualPedestrianMode}");
#endif

                            foreach (KeyValuePair<API.Traffic.Enums.ExtVehicleType,
                                         CustomSegmentLight> e2 in segLights.CustomLights) {
#if DEBUGSAVE
                                Log._Debug($"Saving timed light step {j}, segment {e.Key}, vehicleType "+
                                $"{e2.Key} at node {nodeId}.");
#endif
                                CustomSegmentLight segLight = e2.Value;
                                var cnfSegLight = new Configuration.CustomSegmentLight {
                                    nodeId = lightsNodeId, // TODO not needed
                                    segmentId = segLights.SegmentId, // TODO not needed
                                    currentMode = (int)segLight.CurrentMode,
                                    leftLight = segLight.LightLeft,
                                    mainLight = segLight.LightMain,
                                    rightLight = segLight.LightRight,
                                };

                                cnfSegLights.customLights.Add(
                                    LegacyExtVehicleType.ToOld(e2.Key),
                                    cnfSegLight);
                            }
                        }
                    }
                }
                catch (Exception e) {
                    Log.Error(
                        $"Exception occurred while saving timed traffic light @ {nodeId}: {e}");
                    success = false;
                }
            }

            return ret;
        }
    }
}