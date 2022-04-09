using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace TrafficManager.Persistence {

    internal abstract class AbstractPersistentObject<TFeature>
            where TFeature : struct {

        public abstract string ElementName { get; }

        /// <summary>
        /// Moves one or more of <paramref name="featureFilter"/>'s required features to the forbidden list,
        /// then clears any remaining required features.
        /// </summary>
        /// <param name="featureFilter"></param>
        protected virtual void ChooseVictim(FeatureFilter<TFeature> featureFilter) {
            featureFilter.Forbid(featureFilter.GetRequired().First());
            featureFilter.GetRequiredCollection(false).Clear();
        }

        /// <summary>
        /// Checks whether the element is feature-compatible with this build.
        /// </summary>
        /// <param name="element"></param>
        /// <returns>true if feature-compatible, otherwise false</returns>
        public bool CanLoad(XElement element) => FeatureFilter<TFeature>.CanLoad(element, out var _);

        protected PersistenceResult LoadData(XElement element, Func<ICollection<TFeature>, PersistenceResult> loadData) {
            return FeatureFilter<TFeature>.CanLoad(element, out var featureFilter)
                    ? loadData(featureFilter.GetRequiredCollection(true))
                    : PersistenceResult.Skip;
        }

        protected PersistenceResult SaveData(XElement container, ICollection<TFeature> featuresForbidden, Func<XElement, ICollection<TFeature>, ICollection<TFeature>, PersistenceResult> saveData) {

            var featureFilter = new FeatureFilter<TFeature>();
            if (featuresForbidden != null) {
                foreach (var feature in featuresForbidden) {
                    featureFilter.Forbid(feature);
                }
            }

            var element = new XElement(ElementName);
            var result = saveData(element,
                                    featureFilter.GetRequiredCollection(false),
                                    featureFilter.GetForbiddenCollection());

            if (result == PersistenceResult.Success) {

                element.Add(featureFilter.GetAttributes());
                container.Add(element);

                while (result == PersistenceResult.Success && featureFilter.IsAnyRequired()) {

                    ChooseVictim(featureFilter);

                    if (featureFilter.GetForbiddenCollection().Count == Enum.GetValues(typeof(TFeature)).Length)
                        break;

                    element = new XElement(ElementName);
                    result = saveData(element,
                                        featureFilter.GetRequiredCollection(false),
                                        featureFilter.GetForbiddenCollection());

                    if (result == PersistenceResult.Success) {
                        element.Add(featureFilter.GetAttributes());
                        container.Add(element);
                    }
                }
                if (result == PersistenceResult.Skip)
                    result = PersistenceResult.Success;
            }

            return result;
        }
    }
}
