using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace TrafficManager.Persistence {

    internal abstract class PersistentObject<TFeature>
            : IPersistentObject
            where TFeature : struct {

        public abstract XName ElementName { get; }

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

        public abstract Type DependencyTarget { get; }

        int IComparable.CompareTo(object obj) => CompareTo((IPersistentObject)obj);

        public int CompareTo(IPersistentObject other) {

            bool thisDependsOnOther = GetDependencies()?.Contains(other.DependencyTarget) == true;
            bool otherDependsOnThis = other.GetDependencies()?.Contains(DependencyTarget) == true;

            return
                thisDependsOnOther == otherDependsOnThis ? ElementName.ToString().CompareTo(other.ElementName.ToString())
                : thisDependsOnOther ? 1
                : -1;
        }

        public abstract IEnumerable<Type> GetDependencies();

        /// <summary>
        /// Override to load data
        /// </summary>
        /// <param name="element">An XML element that was filled by <see cref="OnSaveData(XElement, ICollection{TFeature}, ICollection{TFeature}, PersistenceContext)"/></param>
        /// <param name="featuresRequired">A read-only collection of features that were known to the build that created this element.
        /// When a particular feature caused a breaking change in the save data, the absence of that feature from this collection
        /// means that the data must be read the old way.</param>
        /// <param name="context">The persistence context of this load operation</param>
        /// <returns></returns>
        protected abstract PersistenceResult OnLoadData(XElement element, ICollection<TFeature> featuresRequired, PersistenceContext context);

        /// <summary>
        /// Verifies feature compatibility and conditionally calls the overridable
        /// <see cref="OnLoadData(XElement, ICollection{TFeature}, PersistenceContext)"/>.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public PersistenceResult LoadData(XElement element, PersistenceContext context) {
            return LoadData(element, f => OnLoadData(element, f, context));
        }

        /// <summary>
        /// Override to save data
        /// </summary>
        /// <param name="element">An XML element to be filled with save data</param>
        /// <param name="featuresRequired">A collection of features this save data depends on. When a new feature introduces a
        /// breaking change, the feature must be added to this collection to avoid data loss when if the player reverts to an earlier build.</param>
        /// <param name="featuresForbidden">A collection of features that are to be omitted from the save data for backward compatibility.
        /// If the implementation cannot write backward-compatible save data that omits the features in this collection,
        /// it must return <see cref="PersistenceResult.Skip"/>.</param>
        /// <param name="context">The persistence context of this save operation</param>
        /// <returns></returns>
        protected abstract PersistenceResult OnSaveData(XElement element, ICollection<TFeature> featuresRequired, ICollection<TFeature> featuresForbidden, PersistenceContext context);

        /// <summary>
        /// Calls <see cref="OnSaveData(XElement, ICollection{TFeature}, ICollection{TFeature}, PersistenceContext)"/>.
        /// When that method reports the use of features that introduced breaking changes, successive calls are made to request
        /// backward-compatible save data.
        /// </summary>
        /// <param name="container"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public PersistenceResult SaveData(XElement container, PersistenceContext context) {
            return SaveData(container, null, (e, r, f) => OnSaveData(e, r, f, context));
        }
    }
}
