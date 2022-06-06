using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace TrafficManager.Persistence {
    internal sealed class FeatureFilter<TFeature>
            where TFeature : struct {

        private const string featuresRequiredAttributeName = "featuresRequired";
        private const string featuresForbiddenAttributeName = "featuresForbidden";

        private enum FeatureStatus {
            Forbidden,
            Required,
        }

        static FeatureFilter() {
            Type tFeature = typeof(TFeature);
            if (!tFeature.IsEnum)
                throw new PersistenceException($"FeatureSet<{tFeature.FullName}>: Type {tFeature.Name} is not an enum");
        }

        private readonly Dictionary<TFeature, FeatureStatus> filter;

        public static bool CanLoad(XElement element, out FeatureFilter<TFeature> result) {
            return CanLoad(element.Attribute(featuresRequiredAttributeName)?.Value,
                            element.Attribute(featuresForbiddenAttributeName)?.Value,
                            out result);
        }

        private static bool CanLoad(string requiredAttribute, string forbiddenAttribute, out FeatureFilter<TFeature> result) {

            result = null;

            if (!string.IsNullOrEmpty(forbiddenAttribute)) {
                foreach (var featureName in forbiddenAttribute.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
                    try {
                        Enum.Parse(typeof(TFeature), featureName);
                        return false;
                    }
                    catch {
                    }
                }
            }
            var filter = new Dictionary<TFeature, FeatureStatus>();
            if (!string.IsNullOrEmpty(requiredAttribute)) {
                foreach (var featureName in requiredAttribute.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
                    try {
                        filter[(TFeature)Enum.Parse(typeof(TFeature), featureName)] = FeatureStatus.Required;
                    }
                    catch {
                        return false;
                    }
                }
            }
            result = new FeatureFilter<TFeature>(filter);
            return true;
        }

        public FeatureFilter() => filter = new Dictionary<TFeature, FeatureStatus>();

        public FeatureFilter(IEnumerable<TFeature> required, IEnumerable<TFeature> forbidden)
                : this()
                {

            if (required != null) {
                if (forbidden != null) {
                    IEnumerable<TFeature> conflictingFeatures = required.Intersect(forbidden);
                    if (conflictingFeatures.Any()) {
                        throw new ArgumentException($"{conflictingFeatures.Count()} features including {conflictingFeatures.First()} were found in both the required and forbidden collections");
                    }
                }

                foreach (var f in required) {
                    Require(f);
                }
            }

            if (forbidden != null) {
                foreach (var f in forbidden) {
                    Forbid(f);
                }
            }
        }

        private FeatureFilter(Dictionary<TFeature, FeatureStatus> filter) => this.filter = filter;

        public void Clear() => filter.Clear();

        public void Require(TFeature feature) => filter[feature] = FeatureStatus.Required;

        public void Forbid(TFeature feature) => filter[feature] = FeatureStatus.Forbidden;

        public void Disregard(TFeature feature) => filter.Remove(feature);

        public bool IsRequired(TFeature feature) {
            return filter.TryGetValue(feature, out var status)
                    && status == FeatureStatus.Required;
        }

        public bool IsForbidden(TFeature feature) {
            return filter.TryGetValue(feature, out var status)
                    && status == FeatureStatus.Forbidden;
        }

        public bool IsAnyRequired() => filter.ContainsValue(FeatureStatus.Required);

        public bool IsAnyForbidden() => filter.ContainsValue(FeatureStatus.Forbidden);

        private IEnumerable<TFeature> Enumerate(FeatureStatus status)
            => filter.Where(e => e.Value == status).Select(e => e.Key).OrderByDescending(f => f);

        public IEnumerable<TFeature> GetRequired()
            => Enumerate(FeatureStatus.Required);

        public IEnumerable<TFeature> GetForbidden()
            => Enumerate(FeatureStatus.Forbidden);

        public ICollection<TFeature> GetRequiredCollection(bool readOnly)
            => new FeatureCollection(this, FeatureStatus.Required, readOnly);

        public ICollection<TFeature> GetForbiddenCollection()
            => new FeatureCollection(this, FeatureStatus.Forbidden, true);

        private class FeatureCollection : ICollection<TFeature> {
            private readonly FeatureFilter<TFeature> featureFilter;
            private readonly FeatureStatus collectionFeatureStatus;
            private readonly bool isReadOnly;

            public FeatureCollection(FeatureFilter<TFeature> featureFilter, FeatureStatus collectionFeatureStatus, bool isReadOnly) {
                this.featureFilter = featureFilter;
                this.collectionFeatureStatus = collectionFeatureStatus;
                this.isReadOnly = isReadOnly;
            }

            public int Count => featureFilter.filter.Values.Where(v => v == collectionFeatureStatus).Count();

            public bool IsReadOnly => isReadOnly;

            public void Add(TFeature item) {
                if (isReadOnly)
                    throw new NotSupportedException();
                if (featureFilter.filter.TryGetValue(item, out var existingStatus) && existingStatus != collectionFeatureStatus)
                    throw new PersistenceException($"{item} could not be added to the required collection because it is already forbidden.");
                featureFilter.filter[item] = collectionFeatureStatus;
            }

            public void Clear() {
                if (isReadOnly)
                    throw new NotSupportedException();
                foreach (var e in featureFilter.filter.Where(e => e.Value == collectionFeatureStatus).ToArray())
                    featureFilter.filter.Remove(e.Key);
            }

            public bool Contains(TFeature item) {
                return featureFilter.filter.TryGetValue(item, out var status)
                        && status == collectionFeatureStatus;
            }

            public void CopyTo(TFeature[] array, int arrayIndex) {
                throw new NotImplementedException();
            }

            public IEnumerator<TFeature> GetEnumerator()
                => featureFilter.Enumerate(collectionFeatureStatus).GetEnumerator();

            public bool Remove(TFeature item) {
                if (isReadOnly)
                    throw new NotSupportedException();

                return featureFilter.filter.TryGetValue(item, out var status)
                        && status == collectionFeatureStatus
                        && featureFilter.filter.Remove(item);
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public object[] GetAttributes() {
            var result = new ArrayList();

            if (IsAnyRequired()) {
                result.Add(new XAttribute(featuresRequiredAttributeName,
                                            string.Join(" ", GetRequired().Select(f => f.ToString()).ToArray())));
            }

            if (IsAnyForbidden()) {
                result.Add(new XAttribute(featuresForbiddenAttributeName,
                                            string.Join(" ", GetForbidden().Select(f => f.ToString()).ToArray())));
            }

            return result.ToArray();
        }
    }
}
