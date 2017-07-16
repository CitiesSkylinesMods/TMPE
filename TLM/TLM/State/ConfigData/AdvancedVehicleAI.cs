using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.State.ConfigData {
	public class AdvancedVehicleAI {
		/// <summary>
		/// Junction randomization for randomized lane selection
		/// </summary>
		public uint LaneSelectionJunctionRandomization = 5;

		/// <summary>
		/// base lane changing cost factor on city streets
		/// </summary>
		public float LaneChangingBaseCost = 1.5f;

		/// <summary>
		/// heavy vehicle lane changing cost factor
		/// </summary>
		public float HeavyVehicleLaneChangingCostFactor = 1.5f;

		/// <summary>
		/// > 1 lane changing cost factor
		/// </summary>
		public float MoreThanOneLaneChangingCostFactor = 2f;

		/// <summary>
		/// Relative factor for lane traffic cost calculation
		/// </summary>
		public float TrafficCostFactor = 4f;

		/// <summary>
		/// lane density random interval
		/// </summary>
		public float LaneDensityRandInterval = 50f;

		/// <summary>
		/// Threshold for resetting traffic buffer
		/// </summary>
		public uint MaxTrafficBuffer = 500;
	}
}
