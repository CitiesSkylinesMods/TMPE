using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.State.ConfigData {
	public class PriorityRules {
		/// <summary>
		/// maximum incoming vehicle square distance to junction for priority signs
		/// </summary>
		public float MaxPriorityCheckSqrDist = 225f;

		/// <summary>
		/// maximum junction approach time for priority signs
		/// </summary>
		public float MaxPriorityApproachTime = 15f;

		/// <summary>
		/// maximum waiting time at priority signs
		/// </summary>
		public uint MaxPriorityWaitTime = 100;
	}
}
