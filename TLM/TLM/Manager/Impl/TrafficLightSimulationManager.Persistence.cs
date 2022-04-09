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

namespace TrafficManager.Manager.Impl {

    partial class TrafficLightSimulationManager {

        internal class Persistence : GlobalPersistentObject<Persistence.TtlFeature> {

            public enum TtlFeature {

                None = 0,
            }

            public override Type DependencyTarget => typeof(TrafficLightSimulationManager);

            public override string ElementName => "TimedTrafficLights";

            public override IEnumerable<Type> GetDependencies() => null;

            private readonly TtlPersistence ttlPersistence = new TtlPersistence();

            protected override PersistenceResult OnLoadData(XElement element, ICollection<TtlFeature> featuresRequired, PersistenceContext context) {
                return PersistenceResult.Skip;
            }

            protected override PersistenceResult OnSaveData(XElement element, ICollection<TtlFeature> featuresRequired, ICollection<TtlFeature> featuresForbidden, PersistenceContext context) {

                var result = PersistenceResult.Success;

                using (ttlPersistence.PrepareForSave()) {
                    foreach (var ttl in Instance.EnumerateTimedTrafficLights()) {
                        try {
#if DEBUGSAVE
                            Log._Debug($"Going to save timed light at node {ttl.NodeId}.");
#endif
                            ttl.OnGeometryUpdate();

                            ttlPersistence.SaveData(element, ttl, featuresForbidden, context);
                        }
                        catch (Exception e) {
                            Log.Error(
                                $"Exception occurred while saving timed traffic light @ {ttl.NodeId}: {e}");
                            result = PersistenceResult.Failure;
                        }
                    }
                }

                return result;
            }

            internal class TtlPersistence : PersistentObject<TimedTrafficLights, TtlFeature> {

                public override string ElementName => "Ttl";

                private Dictionary<ushort, ushort> masterNodeIdBySlaveNodeId = null;
                private Dictionary<ushort, List<ushort>> nodeGroupByMasterNodeId = null;

                /// <summary>
                /// TODO Is this obscenity really necessary?
                /// </summary>
                /// <returns></returns>
                public IDisposable PrepareForSave() {
                    var nodesWithSimulation = new HashSet<ushort>();

                    foreach (var timedLights in Instance.EnumerateTimedTrafficLights()) {
                        nodesWithSimulation.Add(timedLights.NodeId);
                    }

                    masterNodeIdBySlaveNodeId = new Dictionary<ushort, ushort>();
                    nodeGroupByMasterNodeId = new Dictionary<ushort, List<ushort>>();

                    foreach (var timedLights in Instance.EnumerateTimedTrafficLights()) {
                        // TODO most of this should not be necessary at all if the classes around TimedTrafficLights class were properly designed
                        // enforce uniqueness of node ids
                        List<ushort> currentNodeGroup = timedLights.NodeGroup.Distinct().ToList();

                        if (!currentNodeGroup.Contains(timedLights.NodeId)) {
                            currentNodeGroup.Add(timedLights.NodeId);
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

                    return new AnonymousDisposable(() => {
                        masterNodeIdBySlaveNodeId = null;
                        nodeGroupByMasterNodeId = null;
                    });
                }

                protected override PersistenceResult OnLoadData(XElement element, out TimedTrafficLights obj, ICollection<TtlFeature> featuresRequired, PersistenceContext context) {
                    throw new NotImplementedException();
                }

                protected override PersistenceResult OnSaveData(XElement element, TimedTrafficLights ttl, ICollection<TtlFeature> featuresRequired, ICollection<TtlFeature> featuresForbidden, PersistenceContext context) {

                    if (masterNodeIdBySlaveNodeId == null || nodeGroupByMasterNodeId == null) {
                        throw new InvalidOperationException("Must call PrepareForSave() before SaveData in TtlPersistence");
                    }

                    if (!masterNodeIdBySlaveNodeId.ContainsKey(ttl.NodeId)) {
                        return PersistenceResult.Skip;
                    }

                    element.Add(new XAttribute(nameof(ttl.NodeId), ttl.NodeId));

                    ushort masterNodeId = masterNodeIdBySlaveNodeId[ttl.NodeId];
                    foreach (var groupNodeId in nodeGroupByMasterNodeId[masterNodeId]) {
                        element.Add(new XElement(nameof(ttl.NodeGroup), groupNodeId));
                    }
                    element.Add(new XAttribute(nameof(ttl.IsStarted), ttl.IsStarted()));

                    int currentStep = ttl.CurrentStep;
                    if (ttl.IsStarted() &&
                        ttl.GetStep(ttl.CurrentStep).IsInEndTransition()) {
                        // if in end transition save the next step
                        currentStep = (currentStep + 1) % ttl.NumSteps();
                    }

                    element.Add(new XAttribute(nameof(ttl.CurrentStep), currentStep));

                    foreach (var step in Enumerable.Range(0, ttl.NumSteps()).Select(i => ttl.GetStep(i))) {

                        var stepElement = new XElement("Step");

                        stepElement.Add(new XAttribute(nameof(step.MinTime), step.MinTime));
                        stepElement.Add(new XAttribute(nameof(step.MaxTime), step.MaxTime));
                        stepElement.Add(new XAttribute(nameof(step.ChangeMetric), (int)step.ChangeMetric));
                        stepElement.Add(new XAttribute(nameof(step.WaitFlowBalance), step.WaitFlowBalance));

                        foreach (var segmentLights in step.CustomSegmentLights.Values) {

                            var segLightElement = new XElement("SegmentLights");

                            segLightElement.Add(new XAttribute(nameof(segmentLights.NodeId), segmentLights.NodeId));
                            segLightElement.Add(new XAttribute(nameof(segmentLights.SegmentId), segmentLights.SegmentId));

                            foreach (var light in segmentLights.CustomLights) {

                                var lightElement = new XElement("Light");

                                lightElement.Add(new XAttribute(nameof(ExtVehicleType), light.Key));
                                lightElement.Add(new XAttribute(nameof(light.Value.CurrentMode), light.Value.CurrentMode));
                                lightElement.Add(new XAttribute(nameof(light.Value.LightLeft), (int)light.Value.LightLeft));
                                lightElement.Add(new XAttribute(nameof(light.Value.LightMain), (int)light.Value.LightMain));
                                lightElement.Add(new XAttribute(nameof(light.Value.LightRight), (int)light.Value.LightRight));

                                segLightElement.Add(lightElement);
                            }

                            segLightElement.Add(new XAttribute(nameof(segmentLights.PedestrianLightState), segmentLights.PedestrianLightState));
                            segLightElement.Add(new XAttribute(nameof(segmentLights.ManualPedestrianMode), segmentLights.ManualPedestrianMode));

                            stepElement.Add(segLightElement);
                        }

                        element.Add(stepElement);
                    }
                    return PersistenceResult.Success;
                }
            }
        }

    }
}
