using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic {
	public interface ISegmentEndId : IEquatable<ISegmentEndId> {
		// TODO documentation
		ushort SegmentId { get; }
		bool StartNode { get; }

		bool Relocate(ushort segmentId, bool startNode);
	}
}
