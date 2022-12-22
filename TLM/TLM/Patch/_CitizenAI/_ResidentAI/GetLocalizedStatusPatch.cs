namespace TrafficManager.Patch._CitizenAI._ResidentAI {
    using System.Reflection;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch]
    public class GetLocalizedStatusPatch {
        private delegate string TargetDelegate(ushort instanceID,
                                               ref CitizenInstance data,
                                               out InstanceID target);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<TargetDelegate>(typeof(ResidentAI), nameof(ResidentAI.GetLocalizedStatus));

        [UsedImplicitly]
        public static void Postfix(ref string __result, ushort instanceID, ref CitizenInstance data) {
            if (SavedGameOptions.Instance.parkingAI
                && data.m_targetBuilding != 0
                && !ExtCitizenInstanceManager.IsSweaptAway(ref data)
                && !ExtCitizenInstanceManager.IsHangingAround(ref data)) {

                __result = AdvancedParkingManager.Instance.EnrichLocalizedCitizenStatus(
                    __result,
                    ref ExtCitizenInstanceManager.Instance.ExtInstances[instanceID],
                    ref ExtCitizenManager.Instance.ExtCitizens[data.m_citizen]);
            }
        }
    }
}