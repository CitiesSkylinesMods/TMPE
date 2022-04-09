using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace TrafficManager.Persistence {

    internal abstract class GlobalPersistentObject<TFeature>
            : AbstractPersistentObject<TFeature>,
                IGlobalPersistentObject,
                IComparable<GlobalPersistentObject<TFeature>>
            where TFeature : struct {

        public abstract Type DependencyTarget { get; }

        int IComparable.CompareTo(object obj) => CompareTo((GlobalPersistentObject<TFeature>)obj);

        public int CompareTo(GlobalPersistentObject<TFeature> other) {

            bool thisDependsOnOther = GetDependencies()?.Contains(other.DependencyTarget) == true;
            bool otherDependsOnThis = other.GetDependencies()?.Contains(DependencyTarget) == true;

            return
                thisDependsOnOther == otherDependsOnThis ? ElementName.CompareTo(other.ElementName)
                : thisDependsOnOther ? 1
                : -1;
        }

        public abstract IEnumerable<Type> GetDependencies();

        /// <summary>
        /// Override to load data
        /// </summary>
        /// <param name="element">An XML element that was filled by <see cref="SaveData(XElement, ICollection{TFeature}, ICollection{TFeature}, PersistenceContext)"/></param>
        /// <param name="featuresRequired">A read-only collection of features that were known to the build that created this element.
        /// When a particular feature caused a breaking change in the save data, the absence of that feature from this collection
        /// means that the data must be read the old way.</param>
        /// <param name="context">The persistence context of this load operation</param>
        /// <returns></returns>
        protected abstract PersistenceResult LoadData(XElement element, ICollection<TFeature> featuresRequired, PersistenceContext context);

        /// <summary>
        /// Verifies feature compatibility and conditionally calls the overridable
        /// <see cref="LoadData(XElement, ICollection{TFeature}, PersistenceContext)"/>.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public PersistenceResult LoadData(XElement element, PersistenceContext context) {
            return LoadData(element, f => LoadData(element, f, context));
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
        protected abstract PersistenceResult SaveData(XElement element, ICollection<TFeature> featuresRequired, ICollection<TFeature> featuresForbidden, PersistenceContext context);

        /// <summary>
        /// Calls <see cref="SaveData(XElement, ICollection{TFeature}, ICollection{TFeature}, PersistenceContext)"/>.
        /// When that method reports the use of features that introduced breaking changes, successive calls are made to request
        /// backward-compatible save data.
        /// </summary>
        /// <param name="container"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public PersistenceResult SaveData(XElement container, PersistenceContext context) {
            return SaveData(container, (e, r, f) => SaveData(e, r, f, context));
        }
    }
}
