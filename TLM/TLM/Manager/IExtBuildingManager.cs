using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic.Data;

namespace TrafficManager.Manager {
	public interface IExtBuildingManager {
		// TODO define me!

		/// <summary>
		/// Extended building data
		/// </summary>
		ExtBuilding[] ExtBuildings { get; }

		/// <summary>
		/// Handles a building before a simulation step is performed.
		/// </summary>
		/// <param name="buildingId">building id</param>
		/// <param name="data">building data</param>
		void OnBeforeSimulationStep(ushort buildingId, ref Building data);
	}
}
