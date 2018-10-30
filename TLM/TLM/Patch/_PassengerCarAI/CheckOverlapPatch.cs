using ColossalFramework;
using ColossalFramework.Math;
using CSUtil.Commons;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;

namespace TrafficManager.Patch._PassengerCarAI {
	// [Harmony] Manually patched because struct references are used
	public class CheckOverlapPatch {
		/// <summary>
		/// Determines whether
		/// (1) a parked vehicle is present (stock code) or
		/// (2) a regular vehicle evades an emergency vehicle on the parking lane (custom)
		/// </summary>
		public static bool Prefix(ushort ignoreParked, ref Bezier3 bezier, float offset, float length, ref float minPos, ref float maxPos, ref bool __result) {
			// TODO implement me
			/*if (Options.emergencyAI) {
				if (Constants.ManagerFactory.EmergencyBehaviorManager.CheckOverlap(0, ref bezier, offset, length)) {
					__result = true; // overlap detected
					return false;
				}
			}*/

			// use stock code to find parked vehicles
			return true;
		}
	}
}
