using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic;

namespace TrafficManager.Geometry.Impl {
	public class SegmentEndId : ISegmentEndId {
		public virtual ushort SegmentId { get; protected set; } = 0;
		public virtual bool StartNode { get; protected set; } = false;

		public SegmentEndId(ushort segmentId, bool startNode) {
			SegmentId = segmentId;
			StartNode = startNode;
		}

		private SegmentEndId() {

		}

		public virtual bool Relocate(ushort segmentId, bool startNode) {
			SegmentId = segmentId;
			StartNode = startNode;
			return true;
		}

		public override bool Equals(object other) {
			if (other == null) {
				return false;
			}
			if (!(other is ISegmentEndId)) {
				return false;
			}
			return Equals((ISegmentEndId)other);
		}

		public bool Equals(ISegmentEndId otherSegEndId) {
			if (otherSegEndId == null) {
				return false;
			}
			return SegmentId == otherSegEndId.SegmentId && StartNode == otherSegEndId.StartNode;
		}

		public override int GetHashCode() {
			int prime = 31;
			int result = 1;
			result = prime * result + SegmentId.GetHashCode();
			result = prime * result + StartNode.GetHashCode();
			return result;
		}

		public override string ToString() {
			return $"[SegmentEndId {SegmentId} @ {StartNode}]";
		}
	}
}
