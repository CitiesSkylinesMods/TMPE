using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Traffic.Enums;

namespace TrafficManager.TrafficLight.Model {
    internal class CustomSegmentLightModel {

        public ExtVehicleType VehicleType { get; set; }

        public LightMode CurrentMode { get; set; }

        public RoadBaseAI.TrafficLightState LightLeft { get; set; }

        public RoadBaseAI.TrafficLightState LightMain { get; set; }

        public RoadBaseAI.TrafficLightState LightRight { get; set; }
    }
}
