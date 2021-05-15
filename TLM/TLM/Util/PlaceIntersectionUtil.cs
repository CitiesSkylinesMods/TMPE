namespace TrafficManager.Util {
    using System.Collections.Generic;
    using System.Linq;
    using CSUtil.Commons;
    using TrafficManager.State.Asset;
    using TrafficManager.Util;
    using TrafficManager.Lifecycle;

    public static class PlaceIntersectionUtil {
        ///<summary>maps old netowkr ids to new network ids</summary>
        /// <param name="newSegmentIds">segment list provided by LoadPaths.</param>
        public static void MapSegments(
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
            /*************************
             * Prepration: */

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
                Log.Info("PlaceIntersectionUtil.ApplyTrafficRules(): assetData =" + assetData);
            } else {
                Log.Info("PlaceIntersectionUtil.ApplyTrafficRules(): assetData not found (the asset does not have TMPE data)");
                return;
            }

            var pathNetworkIDs = assetData.PathNetworkIDs;
            if (pathNetworkIDs == null) return;

            /*************************
             * Apply traffic rules: */

            MapSegments(oldSegments: pathNetworkIDs, newSegmentIds: newSegmentIds, map: map);

            foreach (var item in map)
                CalculateNetwork(item.Value);

            assetData.Record.Transfer(map);

            OnPlaceIntersection?.Invoke(intersectionInfo, map);
        }

        /// <summary>
        /// early calculate networks so that we are able to set traffic rules without delay.
        /// </summary>
        public static void CalculateNetwork(InstanceID instanceId) {
            switch (instanceId.Type) {
                case InstanceType.NetNode:
                    ushort nodeId = instanceId.NetNode;
                    nodeId.ToNode().CalculateNode(nodeId);
                    break;
                case InstanceType.NetSegment:
                    ushort segmentId = instanceId.NetSegment;
                    segmentId.ToSegment().CalculateSegment(segmentId);
                    break;
            }
        }

    }
}
