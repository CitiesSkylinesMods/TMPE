using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager {
	public interface ITrafficMeasurementManager {
		// TODO define me!

		/// <summary>
		/// Handles a segment before a simulation step is performed.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="segment">segment data</param>
		void OnBeforeSimulationStep(ushort segmentId, ref NetSegment segment);
	}
}
