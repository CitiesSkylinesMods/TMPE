namespace TrafficManager.Patch._CitizenAI._HumanAI {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.State;
    using TrafficManager.Util.Extensions;

    [HarmonyPatch(typeof(HumanAI), "ArriveAtDestination")]
    public class ArriveAtDestinationPatch {
        /// <summary>
        /// Notifies the extended citizen manager about a citizen that arrived at their destination if the Parking AI is active.
        /// </summary>
        [HarmonyPrefix]
        [UsedImplicitly]
        public static void Prefix(HumanAI __instance,
                                  ushort instanceID,
                                  ref CitizenInstance citizenData,
                                  bool success) {
            if (!SavedGameOptions.Instance.parkingAI) {
                return;
            }

            if (success && citizenData.m_citizen != 0 &&
                (citizenData.m_flags & CitizenInstance.Flags.TargetIsNode) == CitizenInstance.Flags.None)
            {
                Constants.ManagerFactory.ExtCitizenManager.OnArriveAtDestination(
                    citizenData.m_citizen,
                    ref CitizenManager.instance.m_citizens.m_buffer[citizenData.m_citizen],
                    ref citizenData);
            }
        }
    }
}