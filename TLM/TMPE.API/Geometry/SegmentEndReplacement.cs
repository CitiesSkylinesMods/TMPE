using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic;

namespace TrafficManager.Geometry {
	public struct SegmentEndReplacement {
		public ISegmentEndId oldSegmentEndId;
		public ISegmentEndId newSegmentEndId;

		public override string ToString() {
			return $"[SegmentEndReplacement\n" +
				"\t" + $"oldSegmentEndId = {oldSegmentEndId}\n" +
				"\t" + $"newSegmentEndId = {newSegmentEndId}\n" +
				"SegmentEndReplacement]";
		}

		public bool IsDefined() {
			return oldSegmentEndId != null && newSegmentEndId != null;
		}
	}
}
