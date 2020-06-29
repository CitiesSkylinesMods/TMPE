namespace TrafficManager.State.Asset {
    using System;
    using CSUtil.Commons;
    using ICities;
    using System.Collections.Generic;
    using TrafficManager.Util.Record;
    using static Util.Shortcuts;

    [Serializable]
    public class AssetData {
        private string _version;

        public Version Version {
            get => _version != null ? new Version(_version) : default;
            set => value.ToString();
        }

        public IRecordable Record;

        //TODO [issue #959] record networkIDs.

        public override string ToString() => $"AssetData Version={Version} record={Record}";

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
