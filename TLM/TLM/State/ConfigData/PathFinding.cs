using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.State.ConfigData {
	public class PathFinding {
		/// <summary>
		/// penalty for busses not driving on bus lanes
		/// </summary>
		public float PublicTransportLanePenalty = 10f;

		/// <summary>
		/// reward for public transport staying on transport lane
		/// </summary>
		public float PublicTransportLaneReward = 0.1f;

		/// <summary>
		/// maximum penalty for heavy vehicles driving on an inner lane (in %)
		/// </summary>
		public float HeavyVehicleMaxInnerLanePenalty = 1f;

		/// <summary>
		/// artifical lane distance for vehicles that change to lanes which have an incompatible lane arrow configuration
		/// </summary>
		public byte IncompatibleLaneDistance = 1;

		/// <summary>
		/// artifical lane distance for u-turns
		/// </summary>
		public int UturnLaneDistance = 2;

		/// <summary>
		/// Maximum walking distance
		/// </summary>
		public float MaxWalkingDistance = 1000f;

		/// <summary>
		/// Minimum penalty for entering public transport vehicles
		/// </summary>
		public float PublicTransportTransitionMinPenalty = 250f;

		/// <summary>
		/// Maximum penalty for entering public transport vehicles
		/// </summary>
		public float PublicTransportTransitionMaxPenalty = 500f;
	}
}
