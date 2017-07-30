using System.Collections.Generic;
using TrafficManager.Geometry;

namespace TrafficManager.TrafficLight.Impl {
    class CustomSegment {
        public ICustomSegmentLights StartNodeLights;
        public ICustomSegmentLights EndNodeLights;

		public override string ToString() {
			return "[CustomSegment \n" +
			"\t" + $"StartNodeLights: {StartNodeLights}\n" +
			"\t" + $"EndNodeLights: {EndNodeLights}\n" +
			"CustomSegment]";
		}
	}
}
