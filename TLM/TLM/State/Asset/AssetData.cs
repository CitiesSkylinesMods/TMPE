namespace TrafficManager.State.Asset {
    using System;
    using CSUtil.Commons;
    using ICities;
    using System.Collections.Generic;
    using TrafficManager.Util.Record;
    using static Util.Shortcuts;
    using TrafficManager.Util;

    [Serializable]
    public class AssetData {
        private string version_;

        /// <summary>Mod version at the time where data was serailized.</summary>
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
            return new AssetData {
                Version = VersionUtil.ModVersion,
                Record = RecordAll(),
                PathNetworkIDs = GetPathsNetworkIDs(prefab),
            };
        }

        public static IRecordable RecordAll() {
            NetSegment[] segmentBuffer = NetManager.instance.m_segments.m_buffer;
            TrafficRulesRecord record = new TrafficRulesRecord();
            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                bool valid = netService.IsSegmentValid(segmentId) && segmentBuffer[segmentId].Info;
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
            Building[] buildingBuffer = BuildingManager.instance.m_buildings.m_buffer;
            List<ushort> assetSegmentIds = new List<ushort>();
            List<ushort> buildingIds = new List<ushort>(prefab.m_paths.Length);
            var ret = new List<SegmentNetworkIDs>();
            for (ushort buildingId = 1; buildingId < BuildingManager.MAX_BUILDING_COUNT; buildingId += 1) {
                if (buildingBuffer[buildingId].m_flags != Building.Flags.None) {
                    assetSegmentIds.AddRange(BuildingDecoration.GetBuildingSegments(ref buildingBuffer[buildingId]));
                    buildingIds.Add(buildingId);
                }
            }

            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; segmentId++) {
                if (netService.IsSegmentValid(segmentId)) {
                    if (!assetSegmentIds.Contains(segmentId)) {
                        ret.Add(new SegmentNetworkIDs(segmentId));
                    }
                }
            }
            return ret.ToArray();

        }
    }
}
