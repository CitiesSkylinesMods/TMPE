namespace TrafficManager.Util.Record {
    using CSUtil.Commons;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;
    using static Shortcuts;
    using TrafficManager.State;

    [Serializable]
    public class TrafficRulesRecord : IRecordable {
        [NonSerialized] public HashSet<ushort> NodeIDs = new HashSet<ushort>();
        [NonSerialized] public HashSet<ushort> SegmentIDs = new HashSet<ushort>();
        [NonSerialized] public HashSet<int> SegmentEndIndeces = new HashSet<int>();

        public List<IRecordable> Records = new List<IRecordable>();

        /// <summary>
        /// Records segment and both segment ends  but not nodes.
        /// </summary>
        public void AddSegmentWithBothEnds(ushort segmentId) {
            ushort node0 = segmentId.ToSegment().m_startNode;
            ushort node1 = segmentId.ToSegment().m_endNode;
            SegmentIDs.Add(segmentId);
            AddNodeAndSegmentEnds(node0);
            AddNodeAndSegmentEnds(node1);
        }

        /// <summary>
        /// Records segment and both node ends. but not the segment ends.
        /// </summary>
        public void AddSegmentAndNodes(ushort segmentId) {
            SegmentIDs.Add(segmentId);
            int index1 = SegmentEndManager.Instance.GetIndex(segmentId, true);
            int index2 = SegmentEndManager.Instance.GetIndex(segmentId, false);
            SegmentEndIndeces.Add(index1);
            SegmentEndIndeces.Add(index2);
        }

        /// <summary>
        /// Adds the input segment, both node ends, and all segment ends attached to the nodes.
        /// </summary>
        public void AddCompleteSegment(ushort segmentId) {
            ushort node0 = segmentId.ToSegment().m_startNode;
            ushort node1 = segmentId.ToSegment().m_endNode;
            SegmentIDs.Add(segmentId);
            AddNodeAndSegmentEnds(node0);
            AddNodeAndSegmentEnds(node1);
        }

        /// <summary>
        /// Adds the input node and all attached segmentEnds.
        /// </summary>
        public void AddNodeAndSegmentEnds(ushort nodeId) {
            NodeIDs.Add(nodeId);
            ref NetNode node = ref nodeId.ToNode();
            for(int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0) continue;
                bool startNode = (bool)netService.IsStartNode(segmentId, nodeId);
                int index = SegmentEndManager.Instance.GetIndex(segmentId, startNode);
                SegmentEndIndeces.Add(index);
            }
        }

        public void Record() {
            foreach (ushort nodeId in NodeIDs)
                Records.Add(new NodeRecord(nodeId));
            foreach(ushort segmentId in SegmentIDs) 
                Records.Add(new SegmentRecord(segmentId));
            foreach (int segmentEndIndex in SegmentEndIndeces)
                Records.Add(new SegmentEndRecord(segmentEndIndex));
            foreach (IRecordable record in Records)
                record.Record();
        }

        public void Restore() {
            foreach (IRecordable record in Records)
                record.Restore();
        }

        public void Transfer(Dictionary<InstanceID,InstanceID> map) {
            foreach (IRecordable record in Records) {
                try {
                    record.Transfer(map);
                }
                catch {
                    Log.Error($"could not transfer {record}");
                }
            }
        }

        public byte[] Serialize() => SerializationUtil.Serialize(this);
    }
}
