using ColossalFramework;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;

namespace TrafficManager.Patch._HumanAI {
	[HarmonyPatch(typeof(HumanAI), "ArriveAtDestination")]
	public class ArriveAtDestinationPatch {
		/// <summary>
		/// Notifies the extended citizen manager about a citizen that arrived at their destination if the Parking AI is active.
		/// </summary>
		[HarmonyPrefix]
		public static void Prefix(HumanAI __instance, ushort instanceID, ref CitizenInstance citizenData, bool success) {
			if (! Options.parkingAI) {
				return;
			}

			if (success && citizenData.m_citizen != 0 && (citizenData.m_flags & CitizenInstance.Flags.TargetIsNode) == CitizenInstance.Flags.None) {
				Constants.ManagerFactory.ExtCitizenManager.OnArriveAtDestination(citizenData.m_citizen, ref Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen], ref citizenData);
			}
		}
	}
}
