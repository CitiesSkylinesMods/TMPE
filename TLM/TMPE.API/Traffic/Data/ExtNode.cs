using CSUtil.Commons;
using System;
using System.Collections.Generic;

namespace TrafficManager.Traffic.Data {
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

		public override string ToString() {
			return $"[ExtNode {base.ToString()}\n" +
				"\t" + $"nodeId={nodeId}\n" +
				"\t" + $"segmentIds={segmentIds.CollectionToString()}\n" +
				"\t" + $"removedSegmentEndId={removedSegmentEndId}\n" +
				"ExtNode]";
		}

		public ExtNode(ushort nodeId) {
			this.nodeId = nodeId;
			segmentIds = new HashSet<ushort>();
			removedSegmentEndId = null;
		}

		public void Reset() {
			segmentIds.Clear();
			removedSegmentEndId = null;
		}

		public bool Equals(ExtNode otherNode) {
			return nodeId == otherNode.nodeId;
		}

		public override bool Equals(object other) {
			if (other == null) {
				return false;
			}
			if (!(other is ExtNode)) {
				return false;
			}
			return Equals((ExtNode)other);
		}

		public override int GetHashCode() {
			int prime = 31;
			int result = 1;
			result = prime * result + nodeId.GetHashCode();
			return result;
		}
	}
}
