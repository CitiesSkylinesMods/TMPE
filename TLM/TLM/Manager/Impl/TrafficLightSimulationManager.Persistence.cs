using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TrafficManager.API.Traffic.Enums;
using TrafficManager.Persistence;
using TrafficManager.TrafficLight.Impl;
using TrafficManager.Util;
using TrafficManager.Util.Extensions;
using static RoadBaseAI;

namespace TrafficManager.Manager.Impl {

    partial class TrafficLightSimulationManager {

        internal class Persistence : GlobalPersistentObject<Persistence.TtlFeature> {

            public enum TtlFeature {

                None = 0,
            }

            public override Type DependencyTarget => typeof(TrafficLightSimulationManager);

            public override XName ElementName => "TimedTrafficLights";

            private static readonly XName ttlNodeName = "TtlNode";
            private static readonly XName stepName = "Step";
            private static readonly XName segLightsName = "SegmentLights";
            private static readonly XName lightName = "Light";
            private static readonly XName vehicleTypeName = "VehicleType";

            public override IEnumerable<Type> GetDependencies() => null;

            protected override PersistenceResult OnLoadData(XElement element, ICollection<TtlFeature> featuresRequired, PersistenceContext context) {
                var result = PersistenceResult.Success;

                Log.Info($"Loading timed traffic lights (XML method)");

                var nodesWithSimulation = new HashSet<ushort>();

                foreach (var ttlElement in element.Elements(ttlNodeName)) {
                    nodesWithSimulation.Add(ttlElement.Attribute<ushort>(nameof(TimedTrafficLights.NodeId)));
                }

                var masterNodeIdBySlaveNodeId = new Dictionary<ushort, ushort>();
                var nodeGroupByMasterNodeId = new Dictionary<ushort, List<ushort>>();

                foreach (var ttlElement in element.Elements(ttlNodeName)) {

                    var ttlNodeId = ttlElement.Attribute<ushort>(nameof(TimedTrafficLights.NodeId));

                    try {
                        // TODO most of this should not be necessary at all if the classes around TimedTrafficLights class were properly designed
                        // enforce uniqueness of node ids
                        List<ushort> currentNodeGroup = ttlElement.Elements<ushort>(nameof(TimedTrafficLights.NodeId))
                                                            .Distinct().ToList();

                        if (!currentNodeGroup.Contains(ttlNodeId)) {
                            currentNodeGroup.Add(ttlNodeId);
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
                    }
                    catch (Exception e) {
                        Log.WarningFormat(
                            "Error building timed traffic light group for TimedNode {0} (NodeGroup: {1}): {2}",
                            ttlNodeId,
                            string.Join(", ", ttlElement.Elements(nameof(TimedTrafficLights.NodeId)).Select(e => e.Value).ToArray()),
                            e);
                        result = PersistenceResult.Failure;
                    }
                }

                foreach (var ttlElement in element.Elements(ttlNodeName)) {
                    try {
                        var ttlNodeId = ttlElement.Attribute<ushort>(nameof(TimedTrafficLights.NodeId));

                        if (!masterNodeIdBySlaveNodeId.ContainsKey(ttlNodeId)) {
                            continue;
                        }

                        ushort masterNodeId = masterNodeIdBySlaveNodeId[ttlNodeId];
                        List<ushort> nodeGroup = nodeGroupByMasterNodeId[masterNodeId];

#if DEBUGLOAD
                    Log._Debug($"Adding timed light at node {cnfTimedLights.nodeId}. NodeGroup: "+
                    $"{string.Join(", ", nodeGroup.Select(x => x.ToString()).ToArray())}");
#endif
                        Instance.SetUpTimedTrafficLight(ttlNodeId, nodeGroup);

                        int j = 0;
                        foreach (var stepElement in ttlElement.Elements(nameof(stepName))) {
#if DEBUGLOAD
                        Log._Debug($"Loading timed step {j} at node {cnfTimedLights.nodeId}");
#endif
                            TimedTrafficLightsStep step =
                                Instance.TrafficLightSimulations[ttlNodeId].timedLight.AddStep(
                                    stepElement.Attribute<int>(nameof(TimedTrafficLightsStep.MinTime)),
                                    stepElement.Attribute<int>(nameof(TimedTrafficLightsStep.MaxTime)),
                                    stepElement.Attribute<StepChangeMetric>(nameof(TimedTrafficLightsStep.ChangeMetric)),
                                    stepElement.Attribute<float>(nameof(TimedTrafficLightsStep.WaitFlowBalance)));

                            foreach (var segLightsElement in stepElement.Elements(segLightsName)) {

                                var segmentId = segLightsElement.Attribute<ushort>(nameof(CustomSegmentLights.SegmentId));
                                ref NetSegment netSegment = ref segmentId.ToSegment();

                                if (!netSegment.IsValid()) {
                                    continue;
                                }

                                var nodeId = ttlNodeId;

#if DEBUGLOAD
                            Log._Debug($"Loading timed step {j}, segment {e.Key} at node {cnfTimedLights.nodeId}");
#endif
                                if (!step.CustomSegmentLights.TryGetValue(segmentId, out var lights)) {
#if DEBUGLOAD
                                Log._Debug($"No segment lights found at timed step {j} for segment "+
                                $"{segmentId}, node {nodeId}");
#endif
                                    continue;
                                }

#if DEBUGLOAD
                            Log._Debug($"Loading pedestrian light @ seg. {e.Key}, step {j}: "+
                            $"{cnfLights.pedestrianLightState} {cnfLights.manualPedestrianMode}");
#endif
                                lights.ManualPedestrianMode = segLightsElement.Attribute<bool>(nameof(lights.ManualPedestrianMode));
                                lights.PedestrianLightState = segLightsElement.NullableAttribute<TrafficLightState>(nameof(lights.PedestrianLightState));

                                bool first = true; // v1.10.2 transitional code
                                foreach (var lightElement in segLightsElement.Elements(lightName)) {
#if DEBUGLOAD
                                Log._Debug($"Loading timed step {j}, segment {e.Key}, vehicleType "+
                                $"{e2.Key} at node {cnfTimedLights.nodeId}");
#endif
                                    var vehicleType = lightElement.Attribute<ExtVehicleType>(vehicleTypeName);

                                    if (!lights.CustomLights.TryGetValue(
                                            vehicleType,
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

                                    light.InternalCurrentMode = lightElement.Attribute<LightMode>(nameof(light.CurrentMode)); // TODO improve & remove
                                    light.SetStates(
                                        lightElement.Attribute<TrafficLightState>(nameof(light.LightMain)),
                                        lightElement.Attribute<TrafficLightState>(nameof(light.LightLeft)),
                                        lightElement.Attribute<TrafficLightState>(nameof(light.LightRight)),
                                        false);
                                }
                            }

                            ++j;
                        }
                    }
                    catch (Exception e) {
                        // ignore, as it's probably corrupt save data. it'll be culled on next save
                        Log.Warning($"Error loading data from TimedNode (new method): {e}");
                        result = PersistenceResult.Failure;
                    }
                }

                foreach (var ttlElement in element.Elements(ttlNodeName)) {

                    var nodeId = ttlElement.Attribute<ushort>(nameof(TimedTrafficLights.NodeId));

                    try {
                        TimedTrafficLights timedNode =
                            Instance.TrafficLightSimulations[nodeId].timedLight;

                        timedNode.Housekeeping();
                        if (ttlElement.Attribute<bool>(nameof(TimedTrafficLights.IsStarted))) {
                            timedNode.Start(ttlElement.Attribute<int>(nameof(TimedTrafficLights.CurrentStep)));
                        }
                    }
                    catch (Exception e) {
                        Log.Warning($"Error starting timed light @ {nodeId}: {e}");
                        result = PersistenceResult.Failure;
                    }
                }

                return result;
            }

            protected override PersistenceResult OnSaveData(XElement element, ICollection<TtlFeature> featuresRequired, ICollection<TtlFeature> featuresForbidden, PersistenceContext context) {
                var result = PersistenceResult.Success;

                foreach (var timedNode in Instance.EnumerateTimedTrafficLights()) {

                    try {
#if DEBUGSAVE
                    Log._Debug($"Going to save timed light at node {nodeId}.");
#endif
                        timedNode.OnGeometryUpdate();

                        var ttlNodeElement = new XElement(ttlNodeName);

                        ttlNodeElement.AddAttribute(nameof(timedNode.NodeId), timedNode.NodeId);
                        ttlNodeElement.AddElements(nameof(timedNode.NodeGroup), timedNode.NodeGroup);
                        ttlNodeElement.AddAttribute(nameof(timedNode.IsStarted), timedNode.IsStarted());

                        element.Add(ttlNodeElement);

                        int stepIndex = timedNode.CurrentStep;
                        if (timedNode.IsStarted() &&
                            timedNode.GetStep(timedNode.CurrentStep).IsInEndTransition()) {
                            // if in end transition save the next step
                            stepIndex = (stepIndex + 1) % timedNode.NumSteps();
                        }

                        ttlNodeElement.AddAttribute(nameof(timedNode.CurrentStep), stepIndex);

                        for (var j = 0; j < timedNode.NumSteps(); j++) {
#if DEBUGSAVE
                        Log._Debug($"Saving timed light step {j} at node {nodeId}.");
#endif
                            TimedTrafficLightsStep timedStep = timedNode.GetStep(j);

                            var stepElement = new XElement(stepName);

                            stepElement.AddAttribute(nameof(timedStep.MinTime), timedStep.MinTime);
                            stepElement.AddAttribute(nameof(timedStep.MaxTime), timedStep.MaxTime);
                            stepElement.AddAttribute(nameof(timedStep.ChangeMetric), timedStep.ChangeMetric);
                            stepElement.AddAttribute(nameof(timedStep.WaitFlowBalance), timedStep.WaitFlowBalance);

                            ttlNodeElement.Add(stepElement);

                            foreach (var segLights in timedStep.CustomSegmentLights.Values) {
#if DEBUGSAVE
                            Log._Debug($"Saving timed light step {j}, segment {e.Key} at node {nodeId}.");
#endif

                                var segLightsElement = new XElement(segLightsName);

                                segLightsElement.AddAttribute(nameof(segLights.NodeId), segLights.NodeId);
                                segLightsElement.AddAttribute(nameof(segLights.SegmentId), segLights.SegmentId);
                                segLightsElement.AddAttribute(nameof(segLights.PedestrianLightState), (int?)segLights.PedestrianLightState);
                                segLightsElement.AddAttribute(nameof(segLights.ManualPedestrianMode), segLights.ManualPedestrianMode);

                                if (segLights.NodeId == 0 || segLights.NodeId != timedNode.NodeId) {
                                    Log.Warning(
                                        "Inconsistency detected: Timed traffic light @ node " +
                                        $"{timedNode.NodeId} contains custom traffic lights for the invalid " +
                                        $"segment ({segLights.SegmentId}) at step {j}: nId={segLights.NodeId}");
                                    continue;
                                }

                                stepElement.Add(segLightsElement);

#if DEBUGSAVE
                            Log._Debug($"Saving pedestrian light @ seg. {e.Key}, step {j}: "+
                            $"{cnfSegLights.pedestrianLightState} {cnfSegLights.manualPedestrianMode}");
#endif

                                foreach (var e2 in segLights.CustomLights) {
#if DEBUGSAVE
                                Log._Debug($"Saving timed light step {j}, segment {e.Key}, vehicleType "+
                                $"{e2.Key} at node {nodeId}.");
#endif

                                    var lightElement = new XElement(lightName);
                                    CustomSegmentLight segLight = e2.Value;

                                    lightElement.AddAttribute(nameof(segLights.NodeId), segLights.NodeId);
                                    lightElement.AddAttribute(nameof(segLights.SegmentId), segLights.SegmentId);
                                    lightElement.AddAttribute(nameof(segLight.CurrentMode), segLight.CurrentMode);
                                    lightElement.AddAttribute(nameof(segLight.LightLeft), segLight.LightLeft);
                                    lightElement.AddAttribute(nameof(segLight.LightMain), segLight.LightMain);
                                    lightElement.AddAttribute(nameof(segLight.LightRight), segLight.LightRight);

                                    lightElement.AddAttribute(vehicleTypeName, e2.Key);

                                    segLightsElement.Add(lightElement);
                                }
                            }
                        }
                    }
                    catch (Exception e) {
                        Log.Error(
                            $"Exception occurred while saving timed traffic light @ {timedNode.NodeId}: {e}");
                        result = PersistenceResult.Failure;
                    }
                }

                return result;
            }
        }

    }
}
