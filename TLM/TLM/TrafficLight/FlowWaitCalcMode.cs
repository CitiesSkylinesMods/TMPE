using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.TrafficLight {
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
