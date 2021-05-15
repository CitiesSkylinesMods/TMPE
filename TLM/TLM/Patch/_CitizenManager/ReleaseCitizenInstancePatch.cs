namespace TrafficManager.Patch._CitizenManager {
    using HarmonyLib;
    using JetBrains.Annotations;

    [HarmonyPatch(typeof(CitizenManager), "ReleaseCitizenInstance")]
    [UsedImplicitly]
    public static class ReleaseCitizenInstancePatch {
        /// <summary>
        /// Notifies the extended citizen instance manager about a released citizen instance.
        /// </summary>
        [HarmonyPostfix]
        [UsedImplicitly]
        public static void Postfix(CitizenManager __instance, ushort instance) {
            Constants.ManagerFactory.ExtCitizenInstanceManager.OnReleaseInstance(instance);
        }
    }
}