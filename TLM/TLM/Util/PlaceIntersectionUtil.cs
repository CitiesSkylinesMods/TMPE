namespace TrafficManager.Util {
    using System.Collections.Generic;
    using System.Linq;
    using CSUtil.Commons;
    using Manager.Impl;
    using TrafficManager.State.Asset;
    using TrafficManager.Util;
    using TrafficManager.Lifecycle;
    using TrafficManager.Util.Extensions;

    public static class PlaceIntersectionUtil {
        ///<summary>maps old network ids to new network ids</summary>
        /// <param name="oldSegments">source segment id array created by on asset serialization</param>
        /// <param name="newSegmentIds">segment list provided by LoadPaths.</param>
        /// <param name="map">segment map to fill in with pairs (old;new)</param>
        private static void FillSegmentsMap(
            SegmentNetworkIDs[] oldSegments,
            ushort[] newSegmentIds,
            Dictionary<InstanceID, InstanceID> map) {
            Shortcuts.Assert(oldSegments.Length == newSegmentIds.Length);
            for (int i = 0; i < newSegmentIds.Length; ++i) {
                // load paths load segments in the same order as they were stored.
                oldSegments[i].MapInstanceIDs(newSegmentId: newSegmentIds[i], map: map);
            }
        }

        public delegate void Handler(BuildingInfo info, Dictionary<InstanceID, InstanceID> map);

        /// <summary>
        /// invoked when networkIDs are mapped and pre-calculated.
        /// provides the user with dictionary of oldNetworkIds->newNetworkIds
        /// </summary>
        public static event Handler OnPlaceIntersection;

        /// <summary>
        /// start mapping for <paramref name="intersectionInfo"/>
        /// </summary>
        /// <param name="newSegmentIds">segment list provided by LoadPaths.</param>
        public static void ApplyTrafficRules(BuildingInfo intersectionInfo, ushort[] newSegmentIds) {
            /****************/
            /* Preparation: */
            /****************/

            Log._Debug($"PlaceIntersectionUtil.ApplyTrafficRules({intersectionInfo?.ToString() ?? "null"})");

            if (!Shortcuts.InSimulationThread()) {
                Log.Error("must be called from simulation thread");
                return;
            }
            if (intersectionInfo == null) {
                Log.Error("intersectionInfo is null");
                return;
            }

            var map = new Dictionary<InstanceID, InstanceID>();

            var Asset2Data = TMPELifecycle.Instance.Asset2Data;
            Log._Debug("PlaceIntersectionUtil.ApplyTrafficRules(): Asset2Data.keys=" +
                Asset2Data.Select(item => item.Key).ToSTR());

            if (Asset2Data.TryGetValue(intersectionInfo, out var assetData)) {
                Log._Debug("PlaceIntersectionUtil.ApplyTrafficRules(): assetData =" + assetData);
            } else {
                Log._Debug("PlaceIntersectionUtil.ApplyTrafficRules(): assetData not found (the asset does not have TMPE data)");
                return;
            }

            var pathNetworkIDs = assetData.PathNetworkIDs;
            if (pathNetworkIDs == null) return;

            /***********************/
            /* Create segment map: */
            /***********************/
            Shortcuts.AssertNotNull(newSegmentIds, "newSegmentIds");
            FillSegmentsMap(oldSegments: pathNetworkIDs, newSegmentIds: newSegmentIds, map: map);

            // Note to previous solution:
            // Node/segment calculation shouldn't be performed at this state!
            // Forcing it here may trigger another 'fake' LoadPaths call breaking other mods attached to that method!

            /*****************************************/
            /* Queue traffic rules transfer process: */
            /*****************************************/
            UtilityManager.Instance.QueueTransferRecordable(assetData.Record, map);

            OnPlaceIntersection?.Invoke(intersectionInfo, map);
        }
    }
}
