namespace TrafficManager.State.Asset {
    using System;
    using CSUtil.Commons;
    using ICities;
    using System.Collections.Generic;
    using TrafficManager.Util.Record;
    using static Util.Shortcuts;
    using TrafficManager.Util;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Util.Extensions;

    [Serializable]
    public class AssetData {
        private string version_;

        /// <summary>Mod version at the time where data was serialized.</summary>
        public Version Version {
            get => version_ != null ? new Version(version_) : default;
            set => version_ = value.ToString();
        }

        public IRecordable Record;

        /// <summary>
        /// there is a 1:1 correspondence between returned array and BuildingInfo.m_paths.
        /// Does not store nodes that have no segments (they are at the end of BuildingInfo.m_paths so
        /// this does not interfere with array indeces).
        /// </summary>
        public SegmentNetworkIDs[] PathNetworkIDs;

        public override string ToString() => $"AssetData(Version={Version} record={Record} ids={PathNetworkIDs})";

        /// <summary>
        /// gathers all data for the given asset.
        /// </summary>
        public static AssetData GetAssetData(BuildingInfo prefab) {
            if (!HasPaths(prefab)) {
                return null;
            }
            var record = RecordAll();
            if (record == null || record.IsDefault()) {
                return null;
            }

            return new AssetData {
                Version = VersionUtil.ModVersion,
                Record = record,
                PathNetworkIDs = GetPathsNetworkIDs(prefab),
            };
        }

        public static IRecordable RecordAll() {
            TrafficRulesRecord record = new TrafficRulesRecord();
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;

            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                ref NetSegment netSegment = ref segmentId.ToSegment();

                bool valid = netSegment.IsValid() && netSegment.Info;
                if (!valid)
                    continue;
                record.AddCompleteSegment(segmentId);
            }
            record.Record();
            return record;
        }

        /// <summary>
        /// creates an array of SegmentNetworkIDs.
        /// there is a 1:1 correspondence between returned array and BuildingInfo.m_paths.
        /// Does not store nodes that have no segments (they are at the end of BuildingInfo.m_paths so
        /// this does not interfere with array indeces).
        /// </summary>
        public static SegmentNetworkIDs [] GetPathsNetworkIDs(BuildingInfo prefab) {
            // Code based on BuildingDecorations.SavePaths()
            if (!HasPaths(prefab)) {
                // null guard
                return null;
            }
            List<ushort> assetSegmentIds = new List<ushort>();
            List<ushort> buildingIds = new List<ushort>(prefab.m_paths.Length);
            var ret = new List<SegmentNetworkIDs>();
            for (ushort buildingId = 1; buildingId < BuildingManager.MAX_BUILDING_COUNT; buildingId += 1) {
                ref Building building = ref buildingId.ToBuilding();
                if (building.m_flags != Building.Flags.None) {
                    assetSegmentIds.AddRange(BuildingDecoration.GetBuildingSegments(ref building));
                    buildingIds.Add(buildingId);
                }
            }

            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; segmentId++) {
                ref NetSegment netSegment = ref segmentId.ToSegment();

                if (netSegment.IsValid()) {
                    if (!assetSegmentIds.Contains(segmentId)) {
                        ret.Add(new SegmentNetworkIDs(segmentId));
                    }
                }
            }
            return ret.ToArray();
        }

        private static bool HasPaths(BuildingInfo prefab) => prefab.m_paths != null && prefab.m_paths.Length > 0;
    }
}
