using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.TrafficLight.Impl {
    public interface ITrafficLightContainer {

        CustomSegmentLights GetSegmentLights(ushort nodeId, ushort segmentId);
    }
}
