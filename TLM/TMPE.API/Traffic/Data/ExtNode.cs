namespace TrafficManager.API.Traffic.Data {
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using CSUtil.Commons;

    [StructLayout(LayoutKind.Auto)]
    public struct ExtNode : IEquatable<ExtNode> {
        /// <summary>
        /// Node id
        /// </summary>
        public ushort nodeId;

        /// <summary>
        /// Connected segment ids
        /// </summary>
        public HashSet<ushort> segmentIds;

        /// <summary>
        /// Last removed segment id
        /// </summary>
        public ISegmentEndId removedSegmentEndId;

        public ExtNode(ushort nodeId) {
            this.nodeId = nodeId;
            segmentIds = new HashSet<ushort>();
            removedSegmentEndId = null;
        }

        public override string ToString() {
            return string.Format(
                "[ExtNode {0}\n\tnodeId={1}\n\tsegmentIds={2}\n\tremovedSegmentEndId={3}\nExtNode]",
                base.ToString(),
                nodeId,
                segmentIds.CollectionToString(),
                removedSegmentEndId);
        }

        public void Reset() {
            segmentIds.Clear();
            removedSegmentEndId = null;
        }

        public bool Equals(ExtNode otherNode) {
            return nodeId == otherNode.nodeId;
        }

        public override bool Equals(object other) {
            return other is ExtNode node
                   && Equals(node);
        }

        public override int GetHashCode() {
            int prime = 31;
            int result = 1;
            result = prime * result + nodeId.GetHashCode();
            return result;
        }
    }
}