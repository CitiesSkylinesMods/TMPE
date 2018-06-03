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
		/// <param name="citizenData">citizen data</param>
		/// <param name="instanceData">citizen instance data</param>
		void OnArriveAtDestination(uint citizenId, ref Citizen citizenData, ref CitizenInstance instanceData);

		/// <summary>
		/// Handles a released citizen.
		/// </summary>
		/// <param name="citizenId">citizen id</param>
		void OnReleaseCitizen(uint citizenId);

		/// <summary>
		/// Calculates the age group for the given age phase.
		/// </summary>
		/// <param name="agePhase">age phase</param>
		/// <returns>age group</returns>
		Citizen.AgeGroup GetAgeGroup(Citizen.AgePhase agePhase);
	}
}
