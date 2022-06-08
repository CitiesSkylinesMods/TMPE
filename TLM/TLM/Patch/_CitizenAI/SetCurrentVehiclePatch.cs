// Credit: https://github.com/chronofanz/CrazyTouristFix/blob/3132af53f4aa30e45302cc1a3a8166a4f37cd8b4/CrazyTouristFixMod.cs#L58

namespace TrafficManager.Patch._CitizenAI {
    using HarmonyLib;
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using System.Reflection;
    using TrafficManager.Util.Extensions;

    [HarmonyPatch]
    [UsedImplicitly]
    class SetCurrentVehiclePatch {

        [UsedImplicitly]
        static IEnumerable<MethodBase> TargetMethods() {
            yield return AccessTools.DeclaredMethod(typeof(ResidentAI), nameof(ResidentAI.SetCurrentVehicle));
            yield return AccessTools.DeclaredMethod(typeof(TouristAI), nameof(TouristAI.SetCurrentVehicle));
        }

        [UsedImplicitly]
        static void Prefix(ref CitizenInstance citizenData) {
            // Prevent the citizen from adjusting their target position by leaving a vehicle if they are at an outside connection!
            // Following code motivated by logic of HumanAI::SetCurrentVehicle.
            if (citizenData.m_path == 0 &&
                citizenData.m_targetBuilding.ToBuilding().Info?.m_buildingAI is OutsideConnectionAI) {
                uint citizenId = citizenData.m_citizen;
                citizenId.ToCitizen().SetVehicle(citizenId, 0, 0);
            }
        }
    }
}