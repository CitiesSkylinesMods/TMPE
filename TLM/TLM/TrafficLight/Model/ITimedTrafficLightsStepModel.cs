using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Traffic.Enums;

namespace TrafficManager.TrafficLight.Model {
    internal interface ITimedTrafficLightsStepModel {

        int MinTime { get; }

        int MaxTime { get; }

        StepChangeMetric ChangeMetric { get; }

        float WaitFlowBalance { get; }

        IEnumerable<ICustomSegmentLightsModel> EnumerateCustomSegmentLights();
    }
}
