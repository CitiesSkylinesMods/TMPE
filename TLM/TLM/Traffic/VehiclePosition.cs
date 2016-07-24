using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic {
	public struct VehiclePosition {
		/// <summary>
		/// Vehicle is coming from this segment/lane ...
		/// </summary>
		public ushort SourceSegmentId;
		public byte SourceLaneIndex;

		/// <summary>
		/// ... goes over this node ...
		/// </summary>
		public ushort TransitNodeId;

		/// <summary>
		/// ... and goes to this segment/lane
		/// </summary>
		public ushort TargetSegmentId;
		public byte TargetLaneIndex;

		public VehiclePosition(ushort sourceSegmentId, byte sourceLaneIndex, ushort targetNodeId, ushort targetSegmentId, byte targetLaneIndex) {
			SourceSegmentId = sourceSegmentId;
			SourceLaneIndex = sourceLaneIndex;
			TransitNodeId = targetNodeId;
			TargetSegmentId = targetSegmentId;
			TargetLaneIndex = targetLaneIndex;
		}
	}
}
