using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;

namespace TrafficManager.Manager {
	public interface ISegmentEndManager {
		// TODO documentation
		ISegmentEnd GetOrAddSegmentEnd(ISegmentEndId endId);
		ISegmentEnd GetOrAddSegmentEnd(ushort segmentId, bool startNode);
		ISegmentEnd GetSegmentEnd(ISegmentEndId endId);
		ISegmentEnd GetSegmentEnd(ushort segmentId, bool startNode);
		void RemoveSegmentEnd(ISegmentEndId endId);
		void RemoveSegmentEnd(ushort segmentId, bool startNode);
		void RemoveSegmentEnds(ushort segmentId);
		bool UpdateSegmentEnd(ISegmentEndId endId);
		bool UpdateSegmentEnd(ushort segmentId, bool startNode);
	}
}
