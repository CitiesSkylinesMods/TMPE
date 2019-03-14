using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic.Enums {
	/// <summary>
	/// Represents vehicle restrictions effect strength
	/// </summary>
	public enum VehicleRestrictionsAggression {
		/// <summary>
		/// Low aggression
		/// </summary>
		Low = 0,
		/// <summary>
		/// Medium aggression
		/// </summary>
		Medium = 1,
		/// <summary>
		/// High aggression
		/// </summary>
		High = 2,
		/// <summary>
		/// Strict aggression
		/// </summary>
		Strict = 3
	}
}
