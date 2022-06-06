using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using TrafficManager.Manager.Model;
using TrafficManager.Persistence;

namespace TrafficManager.Manager.Impl {
    partial class JunctionRestrictionsManager {

        internal class Persistence : PersistentObject<Persistence.JunctionRestrictionsFeature> {
            public override XName ElementName => "JunctionRestrictions";

            public static readonly XName segmentElementName = "Segment";

            public override Type DependencyTarget => typeof(JunctionRestrictionsManager);

            public override IEnumerable<Type> GetDependencies() {
                yield return typeof(LaneConnection.LaneConnectionManager);
            }

            protected override PersistenceResult OnLoadData(XElement element, ICollection<JunctionRestrictionsFeature> featuresRequired, PersistenceContext context) {

                foreach (var segmentElement in element.Elements(segmentElementName)) {
                    Instance.AddSegmentModel(segmentElement.XDeserialize<SegmentEndPair<JunctionRestrictionsModel>>());
                }

                return PersistenceResult.Success;
            }

            protected override PersistenceResult OnSaveData(XElement element, ICollection<JunctionRestrictionsFeature> featuresRequired, ICollection<JunctionRestrictionsFeature> featuresForbidden, PersistenceContext context) {
                var result = PersistenceResult.Success;

                foreach (var segment in Instance.EnumerateSegmentModels()) {
                    element.Add(segment.XSerialize(segmentElementName));
                }

                return result;
            }

            public enum JunctionRestrictionsFeature {

                None = 0,
            }

        }
    }
}
