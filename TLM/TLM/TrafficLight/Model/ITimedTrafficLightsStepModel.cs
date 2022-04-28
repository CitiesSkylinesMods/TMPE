using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Traffic.Enums;

namespace TrafficManager.TrafficLight.Model {
    internal interface ITimedTrafficLightsStepModel {

        int MinTime { get; set; }

        int MaxTime { get; set; }

        StepChangeMetric ChangeMetric { get; set; }

        float WaitFlowBalance { get; set; }

        IEnumerable<ICustomSegmentLightsModel> EnumerateCustomSegmentLights();
    }
}
