using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.TrafficLight {
	public interface ICustomSegmentLightManager {
		CustomSegmentLights GetSegmentLights(ushort nodeId, ushort segmentId);
	}
}
