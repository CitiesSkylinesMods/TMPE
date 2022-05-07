using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TrafficManager.API.Traffic.Enums;
using TrafficManager.Persistence;
using TrafficManager.TrafficLight.Impl;
using TrafficManager.TrafficLight.Model;
using TrafficManager.Util;
using TrafficManager.Util.Extensions;
using static RoadBaseAI;

namespace TrafficManager.Manager.Impl {

    partial class TrafficLightSimulationManager {

        internal class Persistence : PersistentObject<Persistence.TtlFeature> {

            public enum TtlFeature {

                None = 0,
            }

            public override Type DependencyTarget => typeof(TrafficLightSimulationManager);

            public override XName ElementName => "TimedTrafficLights";

            private static readonly XName ttlNodeElementName = "TtlNode";
            private static readonly XName stepElementName = "Step";
            private static readonly XName segLightsElementName = "SegmentLights";
            private static readonly XName lightElementName = "Light";

            public override IEnumerable<Type> GetDependencies() => null;

            protected override PersistenceResult OnLoadData(XElement element, ICollection<TtlFeature> featuresRequired, PersistenceContext context) {

                Log.Info($"Loading timed traffic lights (XML method)");

                // This is really terrible. The way grouped traffic lights are modeled, there are situations
                // where the data we've saved can't be used to load grouped traffic lights properly unless
                // we scan for and build all the traffic light groups in advance.

                // TODO Legacy code just keeps going if this step fails. Is that okay?
                var result = BuildTtlGroups(element, out var masterNodeLookup, out var ttlGroups);

                // Now we can load the traffic lights

                foreach (var ttlElement in element.Elements(ttlNodeElementName)) {
                    try {
                        var ttlNodeId = ttlElement.Attribute<ushort>(nameof(ITimedTrafficLightsModel.NodeId));

                        if (!masterNodeLookup.ContainsKey(ttlNodeId)) {
                            continue;
                        }

                        ushort masterNodeId = masterNodeLookup[ttlNodeId];
                        List<ushort> nodeGroup = ttlGroups[masterNodeId];

#if DEBUGLOAD
                        Log._Debug($"Adding timed light at node {ttlNodeId}. NodeGroup: "
                                    + $"{string.Join(", ", nodeGroup.Select(x => x.ToString()).ToArray())}");
#endif

                        Instance.SetUpTimedTrafficLight(ttlNodeId, nodeGroup);

                        int step = 0;
                        foreach (var stepElement in ttlElement.Elements(stepElementName)) {
#if DEBUGLOAD
                            Log._Debug($"Loading timed step {step} at node {ttlNodeId}");
#endif
                            LoadStep(stepElement, ttlNodeId);
                            ++step;
                        }
                    }
                    catch (Exception e) {
                        // ignore, as it's probably corrupt save data. it'll be culled on next save
                        Log.Warning($"Error loading data from TimedNode (new method): {e}");
                        result = PersistenceResult.Failure;
                    }
                }

                // Since grouped traffic lights are stored in pieces, we can't start
                // any of the traffic lights until all of them have been loaded.

                foreach (var ttlElement in element.Elements(ttlNodeElementName)) {

                    var nodeId = ttlElement.Attribute<ushort>(nameof(ITimedTrafficLightsModel.NodeId));

                    try {
                        TimedTrafficLights timedNode =
                            Instance.TrafficLightSimulations[nodeId].timedLight;

                        timedNode.Housekeeping();
                        if (ttlElement.Attribute<bool>(nameof(ITimedTrafficLightsModel.IsStarted))) {
                            timedNode.Start(ttlElement.Attribute<int>(nameof(ITimedTrafficLightsModel.CurrentStep)));
                        }
                    }
                    catch (Exception e) {
                        Log.Warning($"Error starting timed light @ {nodeId}: {e}");
                        result = PersistenceResult.Failure;
                    }
                }

                return result;
            }

            private static void LoadStep(XElement stepElement, ushort ttlNodeId) {

                TimedTrafficLightsStep step =
                    Instance.TrafficLightSimulations[ttlNodeId].timedLight.AddStep(
                        stepElement.Attribute<int>(nameof(ITimedTrafficLightsStepModel.MinTime)),
                        stepElement.Attribute<int>(nameof(ITimedTrafficLightsStepModel.MaxTime)),
                        stepElement.Attribute<StepChangeMetric>(nameof(ITimedTrafficLightsStepModel.ChangeMetric)),
                        stepElement.Attribute<float>(nameof(ITimedTrafficLightsStepModel.WaitFlowBalance)));

                foreach (var segLightsElement in stepElement.Elements(segLightsElementName)) {
                    LoadSegLights(segLightsElement, step);
                }
            }

            private static void LoadSegLights(XElement segLightsElement, TimedTrafficLightsStep step) {

                var segmentId = segLightsElement.Attribute<ushort>(nameof(ICustomSegmentLightsModel.SegmentId));
                ref NetSegment netSegment = ref segmentId.ToSegment();

                if (netSegment.IsValid()) {

#if DEBUGLOAD
                    Log._Debug($"Loading segment {segmentId} of for ttl step");
#endif

                    if (step.CustomSegmentLights.TryGetValue(segmentId, out var lights)) {

                        var manualPedestrianMode = segLightsElement.Attribute<bool>(nameof(ICustomSegmentLightsModel.ManualPedestrianMode));
                        var pedestrianLightState = segLightsElement.NullableAttribute<TrafficLightState>(nameof(ICustomSegmentLightsModel.PedestrianLightState));

#if DEBUGLOAD
                        Log._Debug($"Loading pedestrian light at segment {segmentId}: " +
                        $"{pedestrianLightState} {manualPedestrianMode}");
#endif

                        lights.ManualPedestrianMode = manualPedestrianMode;
                        lights.PedestrianLightState = pedestrianLightState;

                        bool first = true; // v1.10.2 transitional code (dark arts that no one understands)
                        foreach (var lightElement in segLightsElement.Elements(lightElementName)) {
                            LoadLight(lightElement, lights, ref first);
                        }
                    } else {
#if DEBUGLOAD
                        Log._Debug($"No segment lights found for segment {segmentId}");
#endif

                    }
                } else {
#if DEBUGLOAD
                    Log._Debug($"Invalid segment {segmentId} for ttl step");
#endif
                }
            }

            private static void LoadLight(XElement lightElement, CustomSegmentLights lights, ref bool first) {

                var vehicleType = lightElement.Attribute<ExtVehicleType>(nameof(CustomSegmentLightModel.VehicleType));

#if DEBUGLOAD
                Log._Debug($"Loading light: vehicleType={vehicleType}");
#endif

                if (!lights.CustomLights.TryGetValue(
                        vehicleType,
                        out CustomSegmentLight light)) {

                    // BEGIN dark arts that no one understands
                    // TODO learn the dark arts
#if DEBUGLOAD
                    Log._Debug($"No segment light found for vehicleType {vehicleType}");
#endif
                    // v1.10.2 transitional code START
                    if (first) {
                        first = false;
                        if (!lights.CustomLights.TryGetValue(
                                CustomSegmentLights
                                    .DEFAULT_MAIN_VEHICLETYPE,
                                out light)) {
#if DEBUGLOAD
                            Log._Debug($"No segment light found for DEFAULT vehicleType {CustomSegmentLights.DEFAULT_MAIN_VEHICLETYPE} ");
#endif
                            return;
                        }
                    } else {
                        // v1.10.2 transitional code END
                        return;

                        // v1.10.2 transitional code START
                    }

                    // v1.10.2 transitional code END

                    // END dark arts
                }

                light.InternalCurrentMode = lightElement.Attribute<LightMode>(nameof(CustomSegmentLightModel.CurrentMode)); // TODO improve & remove
                light.SetStates(
                    lightElement.Attribute<TrafficLightState>(nameof(CustomSegmentLightModel.LightMain)),
                    lightElement.Attribute<TrafficLightState>(nameof(CustomSegmentLightModel.LightLeft)),
                    lightElement.Attribute<TrafficLightState>(nameof(CustomSegmentLightModel.LightRight)),
                    false);
            }

            private static PersistenceResult BuildTtlGroups(XElement element, out Dictionary<ushort, ushort> masterNodeLookup, out Dictionary<ushort, List<ushort>> ttlGroups) {

                var result = PersistenceResult.Success;

                var nodesWithSimulation = new HashSet<ushort>();

                foreach (var ttlElement in element.Elements(ttlNodeElementName)) {
                    nodesWithSimulation.Add(ttlElement.Attribute<ushort>(nameof(TimedTrafficLights.NodeId)));
                }

#if DEBUGLOAD
                Log._Debug($"TrafficLightSimulationManager.Persistence.BuildTtlGroups: "
                            + $"nodesWithSimulation=[{string.Join(",", nodesWithSimulation.Select(n => n.ToString()).ToArray())}]");
#endif

                masterNodeLookup = new Dictionary<ushort, ushort>();
                ttlGroups = new Dictionary<ushort, List<ushort>>();
                foreach (var ttlElement in element.Elements(ttlNodeElementName)) {

                    var ttlNodeId = ttlElement.Attribute<ushort>(nameof(TimedTrafficLights.NodeId));

                    try {
                        // TODO most of this should not be necessary at all if the classes around TimedTrafficLights class were properly designed
                        // enforce uniqueness of node ids
                        List<ushort> currentNodeGroup = ttlElement.Elements<ushort>(nameof(TimedTrafficLights.NodeGroup))
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

                            if (ttlGroups.ContainsKey(nodeId)) {
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
                        ttlGroups[masterNodeId] = currentNodeGroup;

                        foreach (ushort nodeId in currentNodeGroup) {
                            masterNodeLookup[nodeId] = masterNodeId;
                        }

#if DEBUGLOAD
                        Log._Debug($"Node {ttlNodeId}: masterNodeId={masterNodeId}, "
                                    + $"currentNodeGroup=[{string.Join(",", currentNodeGroup.Select(n => n.ToString()).ToArray())}]");
#endif
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

                return result;
            }

            protected override PersistenceResult OnSaveData(XElement element, ICollection<TtlFeature> featuresRequired, ICollection<TtlFeature> featuresForbidden, PersistenceContext context) {
                var result = PersistenceResult.Success;

                foreach (var timedNodeImpl in Instance.EnumerateTimedTrafficLights()) {

                    ITimedTrafficLightsModel timedNode = timedNodeImpl;

                    try {
                        // prepare ttl for write

                        timedNodeImpl.OnGeometryUpdate();

                        // we don't save transition states; instead, we save the next step
                        int currentStep = timedNode.CurrentStep;
                        if (timedNode.IsStarted &&
                            timedNodeImpl.GetStep(timedNode.CurrentStep).IsInEndTransition()) {
                            currentStep = (currentStep + 1) % timedNodeImpl.NumSteps();
                        }

                        // build ttl element

                        var ttlNodeElement = new XElement(ttlNodeElementName);

                        ttlNodeElement.AddAttribute(nameof(timedNode.NodeId), timedNode.NodeId);
                        ttlNodeElement.AddElements(nameof(timedNode.NodeGroup), timedNode.NodeGroup);
                        ttlNodeElement.AddAttribute(nameof(timedNode.IsStarted), timedNode.IsStarted);
                        ttlNodeElement.AddAttribute(nameof(timedNode.CurrentStep), currentStep);

                        element.Add(ttlNodeElement);

                        // add steps to the saved ttl

                        for (var stepIndex = 0; stepIndex < timedNodeImpl.NumSteps(); stepIndex++) {
                            SaveStep(ttlNodeElement, timedNodeImpl.GetStep(stepIndex));
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

            private static void SaveStep(XElement ttlNodeElement, ITimedTrafficLightsStepModel timedStep) {

                var stepElement = new XElement(stepElementName);

                stepElement.AddAttribute(nameof(timedStep.MinTime), timedStep.MinTime);
                stepElement.AddAttribute(nameof(timedStep.MaxTime), timedStep.MaxTime);
                stepElement.AddAttribute(nameof(timedStep.ChangeMetric), timedStep.ChangeMetric);
                stepElement.AddAttribute(nameof(timedStep.WaitFlowBalance), timedStep.WaitFlowBalance);

                ttlNodeElement.Add(stepElement);

                foreach (var segLights in timedStep.EnumerateCustomSegmentLights()) {
                    SaveSegLights(stepElement, segLights);
                }
            }

            private static void SaveSegLights(XElement stepElement, ICustomSegmentLightsModel segLights) {

                var segLightsElement = new XElement(segLightsElementName);

                // build segment lights element

                segLightsElement.AddAttribute(nameof(segLights.NodeId), segLights.NodeId);
                segLightsElement.AddAttribute(nameof(segLights.SegmentId), segLights.SegmentId);
                segLightsElement.AddAttribute(nameof(segLights.PedestrianLightState), (int?)segLights.PedestrianLightState);
                segLightsElement.AddAttribute(nameof(segLights.ManualPedestrianMode), segLights.ManualPedestrianMode);

                stepElement.Add(segLightsElement);

                // add lights to the saved segment lights collection

                foreach (var segLight in segLights) {
                    SaveLight(segLightsElement, segLight);
                }
            }

            private static void SaveLight(XElement segLightsElement, CustomSegmentLightModel segLight) {

                var lightElement = new XElement(lightElementName);

                lightElement.AddAttribute(nameof(segLight.VehicleType), segLight.VehicleType);

                lightElement.AddAttribute(nameof(segLight.CurrentMode), segLight.CurrentMode);
                lightElement.AddAttribute(nameof(segLight.LightLeft), segLight.LightLeft);
                lightElement.AddAttribute(nameof(segLight.LightMain), segLight.LightMain);
                lightElement.AddAttribute(nameof(segLight.LightRight), segLight.LightRight);

                segLightsElement.Add(lightElement);
            }
        }

    }
}
