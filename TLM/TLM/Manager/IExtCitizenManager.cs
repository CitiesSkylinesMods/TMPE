using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager {
	public interface IExtCitizenManager {
		// TODO define me!
		void ResetCitizen(uint citizenId);

		/// <summary>
		/// Called whenever a citizen reaches their destination building.
		/// </summary>
		/// <param name="citizenId">citizen id</param>
		/// <param name="citizen">citizen data</param>
		void OnArriveAtDestination(uint citizenId, ref Citizen citizen);
	}
}
