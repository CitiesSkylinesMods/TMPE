using System;

namespace TrafficManager.Traffic.Data {
	public struct ExtSegmentEnd : IEquatable<ExtSegmentEnd> {
		/// <summary>
		/// Segment id
		/// </summary>
		public ushort segmentId;

		/// <summary>
		/// At start node?
		/// </summary>
		public bool startNode;

		/// <summary>
		/// Node id
		/// </summary>
		public ushort nodeId;

		/// <summary>
		/// Can vehicles leave the node via this segment end?
		/// </summary>
		public bool outgoing;

		/// <summary>
		/// Can vehicles enter the node via this segment end?
		/// </summary>
		public bool incoming;

		public override string ToString() {
			return $"[ExtSegmentEnd {base.ToString()}\n" +
				"\t" + $"segmentId={segmentId}\n" +
				"\t" + $"startNode={startNode}\n" +
				"\t" + $"nodeId={nodeId}\n" +
				"\t" + $"outgoing={outgoing}\n" +
				"\t" + $"incoming={incoming}\n" +
				"ExtSegmentEnd]";
		}

		public ExtSegmentEnd(ushort segmentId, bool startNode) {
			this.segmentId = segmentId;
			this.startNode = startNode;
			nodeId = 0;
			outgoing = false;
			incoming = false;
		}

		public void Reset() {
			nodeId = 0;
			outgoing = false;
			incoming = false;
		}

		public bool Equals(ExtSegmentEnd otherSegEnd) {
			return segmentId == otherSegEnd.segmentId && startNode == otherSegEnd.startNode;
		}

		public override bool Equals(object other) {
			if (other == null) {
				return false;
			}
			if (!(other is ExtSegmentEnd)) {
				return false;
			}
			return Equals((ExtSegmentEnd)other);
		}

		public override int GetHashCode() {
			int prime = 31;
			int result = 1;
			result = prime * result + segmentId.GetHashCode();
			result = prime * result + startNode.GetHashCode();
			return result;
		}
	}
}
