namespace TrafficManager.State.Asset {
    using System;
    using CSUtil.Commons;
    using ICities;
    using System.Collections.Generic;
    using TrafficManager.Util.Record;
    using static Util.Shortcuts;

    [Serializable]
    public class AssetData {
        private string version_;

        /// Mod version at the time where data was serailized.
        public Version Version {
            get => version_ != null ? new Version(version_) : default;
            set => version_ = value.ToString();
        }

        public IRecordable Record;

        //TODO [issue #959] record networkIDs.

        public override string ToString() => $"AssetData(Version={Version} record={Record})";

        public static AssetData GetAssetData() {
            return new AssetData {
                Version = TrafficManagerMod.ModVersion,
                Record = RecordAll(),
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
    }
}
