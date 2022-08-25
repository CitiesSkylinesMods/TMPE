using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Network.Data {

    /// <summary>
    /// To help facilitate future migration of this struct into the API, please put
    /// implementation-specific methods in <see cref="TrafficManager.Util.Extensions.SegmentEndIdExtensions"/>.
    /// </summary>
    public struct SegmentEndId {

        public ushort SegmentId;

        public bool StartNode;

        public SegmentEndId(ushort segmentId, bool startNode) {
            SegmentId = segmentId;
            StartNode = startNode;
        }

        public static bool operator ==(SegmentEndId x, SegmentEndId y) => x.Equals(y);

        public static bool operator !=(SegmentEndId x, SegmentEndId y) => !x.Equals(y);

        public static implicit operator ushort(SegmentEndId segmentEndId) => segmentEndId.SegmentId;

        public override string ToString() => $"[SegmentId={SegmentId}, StartNode={StartNode}]";
    }
}
