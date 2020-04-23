namespace TrafficManager.Util.Record {
    using System.Collections;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;
    using static Shortcuts;

    public class TrafficRulesRecord : IRecordable {
        public HashSet<ushort> NodeIDs = new HashSet<ushort>();
        public HashSet<ushort> SegmentIDs = new HashSet<ushort>();
        public HashSet<int> SegmentEndIndeces = new HashSet<int>();

        public void AddSegmentAndNodes(ushort segmentId) {
            ushort node0 = segmentId.ToSegment().m_startNode;
            ushort node1 = segmentId.ToSegment().m_endNode;
            SegmentIDs.Add(segmentId);
            NodeIDs.Add(node0);
            NodeIDs.Add(node1);
        }

        public void AddCompleteSegment(ushort segmentId) {
            ushort node0 = segmentId.ToSegment().m_startNode;
            ushort node1 = segmentId.ToSegment().m_endNode;
            SegmentIDs.Add(segmentId);
            AddNodeAndSegmentEnds(node0);
            AddNodeAndSegmentEnds(node1);
        }

        // add all segment ends attached to nodeId
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

        private List<IRecordable> records_ = new List<IRecordable>();

        public void Record() {
            foreach (ushort nodeId in NodeIDs)
                records_.Add(new NodeRecord(nodeId));
            foreach(ushort segmentId in SegmentIDs) 
                records_.Add(new SegmentRecord(segmentId));
            foreach (int segmentEndIndex in SegmentEndIndeces)
                records_.Add(new SegmentEndRecord(segmentEndIndex));
            foreach (IRecordable record in records_)
                record.Record();
        }

        public void Restore() {
            foreach (IRecordable record in records_)
                record.Restore();
        }
    }
}
