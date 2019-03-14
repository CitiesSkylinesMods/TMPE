using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.TrafficLight {
	// TODO this enum should be moved to TrafficManager.Traffic.Enums but deserialization fails if we just do that now.
	public enum FlowWaitCalcMode {
		/// <summary>
		/// traffic measurements are averaged
		/// </summary>
		Mean,
		/// <summary>
		/// traffic measurements are summed up
		/// </summary>
		Total
	}
}
