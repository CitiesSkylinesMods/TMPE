namespace TrafficManager.Patch._CitizenManager {
    using HarmonyLib;
    using JetBrains.Annotations;

    [HarmonyPatch(typeof(CitizenManager), "ReleaseCitizen")]
    [UsedImplicitly]
    public static class ReleaseCitizenPatch {
        /// <summary>
        /// Notifies the extended citizen manager about a released citizen.
        /// </summary>
        [HarmonyPostfix]
        [UsedImplicitly]
        public static void Postfix(CitizenManager __instance, uint citizen) {
            Constants.ManagerFactory.ExtCitizenManager.OnReleaseCitizen(citizen);
        }
    }
}