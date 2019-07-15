using ColossalFramework;
using CSUtil.Commons;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;

namespace TrafficManager.Patch._TrainTrackBase {
	// [Harmony] Manually patched because struct references are used
	public class LevelCrossingSimulationStepPatch {
		/// <summary>
		/// Decides whether the stock simulation step for traffic lights should run.
		/// </summary>
		public static bool Prefix(TrainTrackBaseAI __instance, ushort nodeID, ref NetNode data) {
			return !Options.timedLightsEnabled || !Constants.ManagerFactory.TrafficLightSimulationManager.TrafficLightSimulations[nodeID].IsSimulationRunning();
		}
	}
}
