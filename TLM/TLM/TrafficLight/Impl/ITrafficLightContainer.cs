using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.TrafficLight.Impl {
    internal interface ITrafficLightContainer {

        CustomSegmentLights GetSegmentLights(ushort nodeId, ushort segmentId);
    }
}
