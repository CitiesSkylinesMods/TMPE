using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace TrafficManager.Persistence {

    internal abstract class PersistentObject<TObject, TFeature>
            : AbstractPersistentObject<TFeature>
            where TFeature : struct {

        /// <summary>
        /// Override to load data
        /// </summary>
        /// <param name="element">An XML element that was filled by <see cref="SaveData(XElement, TObject, ICollection{TFeature}, ICollection{TFeature}, PersistenceContext)"/></param>
        /// <param name="obj">The object that was loaded</param>
        /// <param name="featuresRequired">A read-only collection of features that were known to the build that created this element.
        /// When a particular feature caused a breaking change in the save data, the absence of that feature from this collection
        /// means that the data must be read the old way.</param>
        /// <param name="context">The persistence context of this load operation</param>
        /// <returns></returns>
        protected abstract PersistenceResult OnLoadData(XElement element, out TObject obj, ICollection<TFeature> featuresRequired, PersistenceContext context);

        /// <summary>
        /// Verifies feature compatibility and conditionally calls the overridable
        /// <see cref="OnLoadData(XElement, out TObject, ICollection{TFeature}, PersistenceContext)"/>.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="obj"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public PersistenceResult LoadData(XElement element, out TObject obj, PersistenceContext context) {
            TObject result = default;
            try {
                return LoadData(element, r => OnLoadData(element, out result, r, context));
            }
            finally {
                obj = result;
            }
        }

        /// <summary>
        /// Override to save data
        /// </summary>
        /// <param name="element">An XML element to be filled with save data</param>
        /// <param name="obj">The object to be saved</param>
        /// <param name="featuresRequired">A collection of features this save data depends on. When a new feature introduces a
        /// breaking change, the feature must be added to this collection to avoid data loss when if the player reverts to an earlier build.</param>
        /// <param name="featuresForbidden">A collection of features that are to be omitted from the save data for backward compatibility.
        /// If the implementation cannot write backward-compatible save data that omits the features in this collection,
        /// it must return <see cref="PersistenceResult.Skip"/>.</param>
        /// <param name="context">The persistence context of this save operation</param>
        /// <returns></returns>
        protected abstract PersistenceResult OnSaveData(XElement element, TObject obj, ICollection<TFeature> featuresRequired, ICollection<TFeature> featuresForbidden, PersistenceContext context);

        /// <summary>
        /// Calls <see cref="OnSaveData(XElement, TObject, ICollection{TFeature}, ICollection{TFeature}, PersistenceContext)"/>.
        /// When that method reports the use of features that introduced breaking changes, successive calls are made to request
        /// backward-compatible save data.
        /// </summary>
        /// <param name="container"></param>
        /// <param name="obj"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public PersistenceResult SaveData(XElement container, TObject obj, ICollection<TFeature> featuresForbidden, PersistenceContext context) {
            return SaveData(container, featuresForbidden, (e, r, f) => OnSaveData(e, obj, r, f, context));
        }
    }
}
