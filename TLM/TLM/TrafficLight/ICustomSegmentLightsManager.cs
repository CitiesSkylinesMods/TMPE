using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;

namespace TrafficManager.TrafficLight {
	public interface ICustomSegmentLightsManager {
		CustomSegmentLights GetSegmentLights(ushort nodeId, ushort segmentId);
		bool SetSegmentLights(ushort nodeId, ushort segmentId, CustomSegmentLights lights);
		short ClockwiseIndexOfSegmentEnd(SegmentEndId endId);
	}
}
