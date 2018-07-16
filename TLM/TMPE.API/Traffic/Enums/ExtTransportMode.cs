using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic.Enums {
	[Flags]
	public enum ExtTransportMode {
		/// <summary>
		/// No information about which mode of transport is used
		/// </summary>
		None = 0,
		/// <summary>
		/// Travelling by car
		/// </summary>
		Car = 1,
		/// <summary>
		/// Travelling by means of public transport
		/// </summary>
		PublicTransport = 2
	}
}
