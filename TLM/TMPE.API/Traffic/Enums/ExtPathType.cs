using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic.Enums {
	public enum ExtPathType {
		/// <summary>
		/// Mixed path
		/// </summary>
		None = 0,
		/// <summary>
		/// Walking path
		/// </summary>
		WalkingOnly = 1,
		/// <summary>
		/// Driving path
		/// </summary>
		DrivingOnly = 2
	}
}
