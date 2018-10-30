using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.State.ConfigData {
	public class EmergencyAI {
		/// <summary>
		/// Max. mean speed in % allowed for vehicles to build a rescue lane on highways
		/// </summary>
		public uint HighwayRescueLaneMaxRelMeanSpeed = 90;

		/// <summary>
		/// Minimum reserved space required for the Emergency AI to start operating
		/// </summary>
		public float MinReservedSpace = 10;

		/// <summary>
		/// Distance to evasion point where vehicles should stop
		/// </summary>
		public float MaxEvasionStopSqrDistance = 16f;

		/// <summary>
		/// Maximum amount of space added between waiting vehicles
		/// </summary>
		public float MaxExtraSqrClearance = 2f;

		/// <summary>
		/// Minimum relative space on parking lane that needs to be available in order to allow for regular vehicles to move to the parking lane when evading a emergency vehicle.
		/// </summary>
		//public float ParkingLaneMinRelSpace = 1.5f;
	}
}
