using System.Collections.Generic;
using TrafficManager.Geometry;

namespace TrafficManager.TrafficLight {
    class CustomSegment {
        public CustomSegmentLights StartNodeLights;
        public CustomSegmentLights EndNodeLights;

		public override string ToString() {
			return "[CustomSegment \n" +
			"\t" + $"StartNodeLights: {StartNodeLights}\n" +
			"\t" + $"EndNodeLights: {EndNodeLights}\n" +
			"CustomSegment]";
		}
	}
}
