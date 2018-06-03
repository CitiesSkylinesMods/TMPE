using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic.Enums {
	/// <summary>
	/// Indicates if a private car [may]/[shall]/[must not] be used
	/// </summary>
	public enum CarUsagePolicy {
		/// <summary>
		/// Citizens may use their own car
		/// </summary>
		Allowed,
		/// <summary>
		/// Citizens are forced to use their car
		/// </summary>
		Forced,
		/// <summary>
		/// Citizens are forbidden to use their car
		/// </summary>
		Forbidden
	}
}
