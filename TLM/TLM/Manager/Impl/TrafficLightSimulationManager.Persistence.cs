using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TrafficManager.API.TrafficLight;
using TrafficManager.Persistence;

namespace TrafficManager.Manager.Impl {

    partial class TrafficLightSimulationManager {

        internal class Persistence : GlobalPersistentObject<Persistence.TtlFeature> {

            public override Type DependencyTarget => typeof(TrafficLightSimulationManager);

            public override string ElementName => "TimedTrafficLights";

            public override IEnumerable<Type> GetDependencies() {
                yield break;
            }

            protected override PersistenceResult LoadData(XElement element, ICollection<TtlFeature> featuresRequired, PersistenceContext context) {
                return PersistenceResult.Skip;
            }

            protected override PersistenceResult SaveData(XElement element, ICollection<TtlFeature> featuresRequired, ICollection<TtlFeature> featuresForbidden, PersistenceContext context) {
                return PersistenceResult.Skip;
            }

            public enum TtlFeature {

                XmlPersistence,
            }
        }

    }
}
