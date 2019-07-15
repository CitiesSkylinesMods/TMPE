using ColossalFramework;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;
using static RoadBaseAI;

namespace TrafficManager.Patch._RoadBaseAI {
	[HarmonyPatch(typeof(RoadBaseAI), "SetTrafficLightState")]
	public class SetTrafficLightStatePatch {
		/// <summary>
		/// Prevents vanilla code from updating traffic light visuals for custom traffic lights.
		/// </summary>
		[HarmonyPrefix]
		public static bool Prefix(ushort nodeID, ref NetSegment segmentData, uint frame, TrafficLightState vehicleLightState, TrafficLightState pedestrianLightState, bool vehicles, bool pedestrians) {
			return !Options.timedLightsEnabled || !Constants.ManagerFactory.TrafficLightSimulationManager.TrafficLightSimulations[nodeID].IsSimulationRunning();
		}
	}
}
