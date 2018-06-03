using CSUtil.Commons;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Patch._CommonBuildingAI {
	// [Harmony] Manually patched because struct references are used
	public class SimulationStepPatch {
		/// <summary>
		/// Decreases parking space and public transport demand before each simulation step if the Parking AI is active.
		/// </summary>
		public static void Prefix(ushort buildingID, ref Building data) {
			Constants.ManagerFactory.ExtBuildingManager.OnBeforeSimulationStep(buildingID, ref data);
		}
	}
}
