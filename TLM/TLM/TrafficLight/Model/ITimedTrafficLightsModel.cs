using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.TrafficLight.Model {
    internal interface ITimedTrafficLightsModel {

        int CurrentStep { get; }

        bool IsStarted { get; }

        ushort NodeId { get; }

        IList<ushort> NodeGroup { get; }
    }
}
