namespace TrafficManager.State {
    using MoveItIntegration;
    using System;
    using System.Collections.Generic;
    using TrafficManager.Util.Record;
    using CSUtil.Commons;
    using Manager.Impl;
    using TrafficManager.Util;

    public class TMPEMoveItIntegrationFactory : IMoveItIntegrationFactory {
        public MoveItIntegrationBase GetInstance() => new TMPEMoveItIntegration();
    }
    public class TMPEMoveItIntegration : MoveItIntegrationBase {
        public override string ID => "me.tmpe";

        public override Version DataVersion => VersionUtil.ModVersion;

        public override string Name => "TMPE";

        public override string Description => "Traffic rules";

        public override object Copy(InstanceID sourceInstanceID) {
            switch(sourceInstanceID.Type) {
                case InstanceType.NetNode:
                    var nodeRecord = new NodeRecord(sourceInstanceID.NetNode);
                    nodeRecord.Record();
                    return nodeRecord;
                case InstanceType.NetSegment:
                    var segmentRecord = new TrafficRulesRecord();
                    segmentRecord.AddSegmentWithBothEnds(sourceInstanceID.NetSegment);
                    segmentRecord.Record();
                    return segmentRecord;
                default:
                    Log.Info( $"instance type {sourceInstanceID.Type} is not supported.");
                    return null;
            }
        }

        public override void Paste(InstanceID targetInstanceID, object record, Dictionary<InstanceID, InstanceID> map) {
            if(record is IRecordable recordable) {
                UtilityManager.Instance.QueueTransferRecordable(recordable, map);
            }
        }

        public override string Encode64(object record) {
            if (record is IRecordable r) {
                return Convert.ToBase64String(r.Serialize());
            }
            return null;
        }

        public override object Decode64(string base64Data, Version dataVersion) {
            return SerializationUtil.Deserialize(Convert.FromBase64String(base64Data));
        }

        public override void Mirror(InstanceID targetInstanceID, object record, Dictionary<InstanceID, InstanceID> map) {
            // TODO [issue #] better mirror support.
            // no special considerations for now.
            Paste(targetInstanceID, record, map);  
        }
    }
}
