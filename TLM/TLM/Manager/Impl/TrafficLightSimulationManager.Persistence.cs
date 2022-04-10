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

            public override IEnumerable<Type> GetDependencies() => null;

            private readonly TtlPersistence ttlPersistence = new TtlPersistence();

            protected override PersistenceResult OnLoadData(XElement element, ICollection<TtlFeature> featuresRequired, PersistenceContext context) {
                return PersistenceResult.Skip;
            }

            protected override PersistenceResult OnSaveData(XElement element, ICollection<TtlFeature> featuresRequired, ICollection<TtlFeature> featuresForbidden, PersistenceContext context) {

                var result = PersistenceResult.Success;

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

                return result;
            }

            internal class TtlPersistence : PersistentObject<TimedTrafficLights, TtlFeature> {

                public override XName ElementName => "Ttl";

                private static readonly XName StepName = "Step";
                private static readonly XName CustomLightsName = "CustomLight";
                private static readonly XName VehicleTypeName = "VehicleType";


                protected override PersistenceResult OnLoadData(XElement element, out TimedTrafficLights ttl, ICollection<TtlFeature> featuresRequired, PersistenceContext context) {

                    var result = PersistenceResult.Success;

                    ushort nodeId = element.Attribute(nameof(ttl.NodeId)).AsUInt16();
                    var nodeGroup = element.Attributes(nameof(ttl.NodeGroup)).Select(a => a.AsUInt16());
                    var isStarted = (bool)element.Attribute(nameof(ttl.IsStarted));

                    ttl = TimedTrafficLights.CreateLoadingInstance(nodeId, nodeGroup, isStarted);

                    ttl.CurrentStep = (int)element.Attribute(nameof(ttl.CurrentStep));

                    foreach (var stepElement in element.Elements(StepName)) {

                        var step = TimedTrafficLightsStep.CreateLoadingInstance();

                        step.MinTime = (int)stepElement.Attribute(nameof(step.MinTime));
                        step.MaxTime = (int)stepElement.Attribute(nameof(step.MaxTime));
                        step.ChangeMetric = stepElement.Attribute(nameof(step.ChangeMetric)).AsEnum<StepChangeMetric>();
                        step.WaitFlowBalance = (float)stepElement.Attribute(nameof(step.WaitFlowBalance));

                        foreach (var segLightElement in stepElement.Elements(nameof(step.CustomSegmentLights))) {

                            var segLightNodeId = segLightElement.Attribute(nameof(CustomSegmentLights.NodeId)).AsUInt16();
                            var segmentId = segLightElement.Attribute(nameof(CustomSegmentLights.SegmentId)).AsUInt16();
                            var startNode = segmentId.ToSegment().IsStartNode(segLightNodeId);
                            var segmentLights = new CustomSegmentLights(step, segmentId, startNode, false, false);

                            foreach (var lightElement in segLightElement.Elements(CustomLightsName)) {

                                var vehicleType = lightElement.Attribute(VehicleTypeName).AsEnum<ExtVehicleType>();

                                var lightLeft = lightElement.Attribute(nameof(CustomSegmentLight.LightLeft)).AsEnum<TrafficLightState>();
                                var lightMain = lightElement.Attribute(nameof(CustomSegmentLight.LightMain)).AsEnum<TrafficLightState>();
                                var lightRight = lightElement.Attribute(nameof(CustomSegmentLight.LightRight)).AsEnum<TrafficLightState>();

                                var light = new CustomSegmentLight(segmentLights, lightMain, lightLeft, lightRight);

                                light.CurrentMode = lightElement.Attribute(nameof(light.CurrentMode)).AsEnum<LightMode>();

                                segmentLights.CustomLights.Add(vehicleType, light);
                            }

                            step.AddLoadingSegmentLights(segmentLights);
                        }

                        ttl.AddLoadingStep(step);
                    }

                    return result;
                }

                protected override PersistenceResult OnSaveData(XElement element, TimedTrafficLights ttl, ICollection<TtlFeature> featuresRequired, ICollection<TtlFeature> featuresForbidden, PersistenceContext context) {

                    element.Add(new XAttribute(nameof(ttl.NodeId), ttl.NodeId));

                    foreach (var node in ttl.NodeGroup) {
                        element.Add(new XAttribute(nameof(ttl.NodeGroup), node));
                    }

                    element.Add(new XAttribute(nameof(ttl.IsStarted), ttl.IsStarted()));
                    element.Add(new XAttribute(nameof(ttl.CurrentStep), ttl.CurrentStep));

                    foreach (var step in ttl.EnumerateSteps()) {
                        var stepElement = new XElement(StepName);

                        stepElement.Add(new XAttribute(nameof(step.MinTime), step.MinTime));
                        stepElement.Add(new XAttribute(nameof(step.MaxTime), step.MaxTime));
                        stepElement.Add(new XAttribute(nameof(step.ChangeMetric), (int)step.ChangeMetric));
                        stepElement.Add(new XAttribute(nameof(step.WaitFlowBalance), step.WaitFlowBalance));

                        foreach (var segmentLights in step.CustomSegmentLights.Select(s => s.Value)) {

                            var segLightElement = new XElement(nameof(step.CustomSegmentLights));

                            segLightElement.Add(new XAttribute(nameof(segmentLights.NodeId), segmentLights.NodeId));
                            segLightElement.Add(new XAttribute(nameof(segmentLights.SegmentId), segmentLights.SegmentId));

                            foreach (var light in segmentLights.CustomLights) {

                                var lightElement = new XElement(CustomLightsName);

                                lightElement.Add(new XAttribute(VehicleTypeName, light.Key));
                                lightElement.Add(new XAttribute(nameof(light.Value.CurrentMode), (int)light.Value.CurrentMode));
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
