namespace TrafficManager.Patch._VehicleAI._CargoTruckAI{
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._RoadBaseAI;
    using System.Reflection;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using ColossalFramework;
    using System.Reflection.Emit;
    using System.Collections.Generic;
    using CSUtil.Commons;
    using static TrafficManager.Util.TranspilerUtil;
    using TrafficManager.UI.MainMenu.OSD;
    using TrafficManager.Util;

    //[HarmonyPatch] // TODO complete transpiler
    public class StartPathFindPatch {

        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod<CargoTruckAI>();

        // TODO this might not be necessary if we replace endpos with start pos.
        const float _vanilaMaxPos = 4800f; // changed from 4800 in vanilla code for reasons known to pcfantacy and krzychu
        const float _newMaxPos = 8000f; // changed from 4800 in vanilla code for reasons known to pcfantacy and krzychu

        /// <summary>
        /// Notifies the extended citizen manager about a citizen that arrived at their destination if the Parking AI is active.
        /// </summary>
        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData) {
            ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, null);
            CreatePathPatch.ExtVehicleType = ExtVehicleType.CargoVehicle; // TODO [issue #] why not cargo truck? why not store the return value from method above?
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.VehicleID = vehicleID;
        }

        [UsedImplicitly] 
        public static IEnumerable<CodeInstruction> Transpiler(ILGenerator il, IEnumerable<CodeInstruction> instructions) {
            // TODO replace startPos with endpod if krzychu said it is necessary.
            int n = 0;
            foreach (var instruction in instructions) {
                yield return instruction;
                bool is_ldfld_minCornerOffset =
                    instruction.opcode == OpCodes.Ldc_R4 && (float)instruction.operand == _vanilaMaxPos;
                if (is_ldfld_minCornerOffset) {
                    n++;
                    yield return new CodeInstruction(OpCodes.Ldc_R4, operand: _newMaxPos);
                }
            }

            Shortcuts.Assert(n > 0, "n>0");
            Log._Debug($"CargoTruckAI.StartPathFindPatch.Transpiler() successfully " +
                $"replaced {n} instances of ldc.r4 {_vanilaMaxPos} with {_newMaxPos}");
            yield break;
        }


    }
    }
