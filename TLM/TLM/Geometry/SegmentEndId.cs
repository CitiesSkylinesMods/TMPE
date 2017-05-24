using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Geometry {
	public class SegmentEndId : IEquatable<SegmentEndId> {
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
			if (!(other is SegmentEndId)) {
				return false;
			}
			return Equals((SegmentEndId)other);
		}

		public bool Equals(SegmentEndId otherSegEndId) {
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
