using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager {
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

	public enum ParkedCarApproachState {
		/// <summary>
		/// Citizen is not approaching their parked car
		/// </summary>
		None,
		/// <summary>
		/// Citizen is currently approaching their parked car
		/// </summary>
		Approaching,
		/// <summary>
		/// Citizen has approaching their parked car
		/// </summary>
		Approached,
		/// <summary>
		/// Citizen failed to approach their parked car
		/// </summary>
		Failure
	}

	public interface IAdvancedParkingManager {
		// TODO define me!
	}
}
