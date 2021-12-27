namespace TrafficManager.Util {
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class RoadFamilyUtil {
        public static IEnumerable<NetInfo> LoadedNetInfos() {
            int n = PrefabCollection<NetInfo>.LoadedCount();
            for(uint i = 0; i < n; ++i) {
                NetInfo netinfo = PrefabCollection<NetInfo>.GetLoaded(i);
                if (netinfo)
                    yield return netinfo;
            }
        }

        public static IEnumerable<NetInfo> GetElevations(this NetInfo basic) {
            if (basic == null) yield break;

            NetInfo elevated = AssetEditorRoadUtils.TryGetElevated(basic);
            NetInfo bridge = AssetEditorRoadUtils.TryGetBridge(basic);
            NetInfo slope = AssetEditorRoadUtils.TryGetSlope(basic);
            NetInfo tunnel = AssetEditorRoadUtils.TryGetTunnel(basic);
            // TODO: add outside connections.

            yield return basic;
            if (elevated != null) yield return elevated;
            if (bridge != null) yield return bridge;
            if (slope != null) yield return slope;
            if (tunnel != null) yield return tunnel;
        }

        /// <summary>
        /// returns a list of all elevations that are directly or indirectly linked to <c>currentInfo</c>.
        /// sometimes one elevation belongs to multiple basic elevations.
        /// the return family will contain all of them.
        /// </summary>
        public static IEnumerable<NetInfo> GetRelatives(this NetInfo netinfo) {
            try {
                if (netinfo == null) return null;

                HashSet<NetInfo> family = new HashSet<NetInfo>(netinfo.GetDirectlyRelated());

                // get elevations that are indirectly linked to currentInfo.
                foreach (var netinfo2 in family.ToArray()) {
                    family.AddRange(netinfo2.GetDirectlyRelated());
                }

                return family;
            } catch(Exception ex) {
                ex.LogException();
                return new[] { netinfo };
            }
        }

        /// <summary>
        /// returns a list containing <c>currentInfo</c>, base elevation, and all its other elevations.
        /// </summary>
        private static IEnumerable<NetInfo> GetDirectlyRelated(this NetInfo netinfo) {
            foreach (var netinfo2 in LoadedNetInfos()) {
                var elevations = netinfo2.GetElevations();
                if (elevations.Contains(netinfo)) {
                    foreach (var elevation in elevations) {
                        yield return elevation;
                    }
                }
            }
        }

        private static void AddRange(this HashSet<NetInfo> hashset, IEnumerable<NetInfo> elements) {
            foreach (var element in elements) {
                hashset.Add(element);
            }
        }
    }
}
