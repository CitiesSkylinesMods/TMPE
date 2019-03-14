using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic.Data {
	public struct LaneTrafficData {
		/// <summary>
		/// Number of seen vehicles since last speed measurement
		/// </summary>
		public ushort trafficBuffer;

		/// <summary>
		/// Number of seen vehicles before last speed measurement
		/// </summary>
		public ushort lastTrafficBuffer;

		/// <summary>
		/// All-time max. traffic buffer
		/// </summary>
		public ushort maxTrafficBuffer;

		/// <summary>
		/// Accumulated speeds since last traffic measurement
		/// </summary>
		public uint accumulatedSpeeds;

		/// <summary>
		/// Current lane mean speed, per ten thousands
		/// </summary>
		public ushort meanSpeed;

		public override string ToString() {
			return $"[LaneTrafficData\n" +
				"\t" + $"trafficBuffer = {trafficBuffer}\n" +
				"\t" + $"lastTrafficBuffer = {lastTrafficBuffer}\n" +
				"\t" + $"maxTrafficBuffer = {maxTrafficBuffer}\n" +
				"\t" + $"trafficBuffer = {trafficBuffer}\n" +
				"\t" + $"accumulatedSpeeds = {accumulatedSpeeds}\n" +
				"\t" + $"meanSpeed = {meanSpeed}\n" +
				"LaneTrafficData]";
		}
	}
}
