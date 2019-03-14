using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic.Enums {
	/// <summary>
	/// Represents the reason why a parked car could not be spawned
	/// </summary>
	public enum ParkingUnableReason {
		/// <summary>
		/// Parked car could be spawned
		/// </summary>
		None,
		/// <summary>
		/// No free parking space was found
		/// </summary>
		NoSpaceFound,
		/// <summary>
		/// The maximum allowed number of parked vehicles has been reached
		/// </summary>
		LimitHit
	}
}
