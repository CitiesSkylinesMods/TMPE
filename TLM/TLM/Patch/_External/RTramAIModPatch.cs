/// <summary>
/// Patching SimulationStepPatch1 in Reversible Tram AI mod
/// https://steamcommunity.com/sharedfiles/filedetails/?id=2740907672
/// https://github.com/sway2020/ReversibleTramAI
/// </summary>

namespace TrafficManager.Patch._External._RTramAIModPatch {
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using HarmonyLib;
    using JetBrains.Annotations;
    using UnityEngine;
    using Util;
    using API.Manager;
    using Manager.Impl;
    using State;

    [UsedImplicitly]
    public static class RTramAIModPatch {

        private delegate void TargetDelegate(VehicleAI __instance, ushort vehicleID, ref Vehicle data, Vector3 physicsLodRefPos, ref VehicleInfo ___m_info);
        private delegate bool TrySpawnDelegate(ushort vehicleID, ref Vehicle vehicleData);

        public static bool ApplyPatch(Harmony harmonyInstance, Type simulationStepPatch1Type) {
            try {
                var original = TranspilerUtil.DeclaredMethod<TargetDelegate>(simulationStepPatch1Type, "Prefix");
                if (original == null) return false;

                var transpiler = typeof(RTramAIModPatch).GetMethod("TranspileRTramSimulationStepPatch1");
                harmonyInstance.Patch(original, transpiler: new HarmonyMethod(transpiler));
                return true;
            }
            catch (Exception ex) {
                ex.LogException();
                return false;
            }
        }

        /// <summary>
        /// Revrites instructions adding necessary TMPE calls. Same purpose as TranspileTramTrainSimulationStep()
        /// </summary>
        /// <param name="il"> Il Generator</param>
        /// <param name="instructions">List of instructions</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static IEnumerable<CodeInstruction> TranspileRTramSimulationStepPatch1(ILGenerator il, IEnumerable<CodeInstruction> instructions) {
            List<CodeInstruction> codes = TranspilerUtil.ToCodeList(instructions);

            MethodBase trySpawnCall = TranspilerUtil.DeclaredMethod<TrySpawnDelegate>(typeof(TramBaseAI), "TrySpawn");

            CodeInstruction searchInstruction = new CodeInstruction(OpCodes.Callvirt, trySpawnCall);
            int index = codes.FindIndex(instruction => TranspilerUtil.IsSameInstruction(instruction, searchInstruction));
            if (index > -1) {
                int target1 = index + 2;
                Label label = il.DefineLabel();
                List<Label> oldLabels = codes[target1].labels.ToList();
                codes[target1].labels.Clear(); // clear labels -> they are moved to new instruction
                codes[target1].labels.Add(label);//add label to next instruction (if() false jump)
                List<CodeInstruction> newInstructions = GetUpdatePositionInstructions(label);
                newInstructions[0].labels.AddRange(oldLabels); // add old labels to redirect here
                codes.InsertRange(target1, newInstructions); // insert new instructions

                CodeInstruction searchInstruction2 = new CodeInstruction(OpCodes.Ldfld, typeof(Vehicle).GetField(nameof(Vehicle.m_blockCounter)));
                int index2 = codes.FindIndex(instruction => TranspilerUtil.IsSameInstruction(instruction, searchInstruction2));
                if (index2 > -1 && codes[index2 + 1].opcode.Equals(OpCodes.Ldc_I4) && codes[index2 + 2].opcode.Equals(OpCodes.Ceq)) { // codes[index2 + 2] is different
                    int target2 = index2 + 7; // target2 index is different
                    Label retLabel = (Label)codes[target2].operand;
                    codes.InsertRange(target2 + 1, GetModifiedBlockCounterInstructions(retLabel));
                } else {
                    throw new Exception("Could not find m_blockCounter field usage or instructions has been patched");
                }
            } else {
                throw new Exception("Could not find TrySpawn call or instructions has been patched");
            }

            return codes;
        }

        /// <summary>
        /// same as PatchCommons.GetUpdatePositionInstructions()
        /// </summary>
        private static List<CodeInstruction> GetUpdatePositionInstructions(Label label) {
            PropertyInfo getFactory = typeof(Constants).GetProperty(nameof(Constants.ManagerFactory));
            PropertyInfo getExtVehMan = typeof(IManagerFactory).GetProperty(nameof(IManagerFactory.ExtVehicleManager));
            MethodInfo updateVehPos = typeof(IExtVehicleManager).GetMethod(nameof(IExtVehicleManager.UpdateVehiclePosition));
            FieldInfo advAIField = typeof(Options).GetField(nameof(Options.advancedAI));
            FieldInfo vehFlags = typeof(Vehicle).GetField(nameof(Vehicle.m_flags));
            MethodInfo logTraffic = typeof(IExtVehicleManager).GetMethod(nameof(IExtVehicleManager.LogTraffic));

            return new List<CodeInstruction> {
                new CodeInstruction(OpCodes.Call, getFactory.GetGetMethod()),
                new CodeInstruction(OpCodes.Callvirt, getExtVehMan.GetGetMethod()),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Callvirt, updateVehPos),
                new CodeInstruction(OpCodes.Ldsfld, advAIField),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldfld, vehFlags),
                new CodeInstruction(OpCodes.Ldc_I4_4),
                new CodeInstruction(OpCodes.And),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Call, getFactory.GetGetMethod()),
                new CodeInstruction(OpCodes.Callvirt, getExtVehMan.GetGetMethod()),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Callvirt, logTraffic),
            };
        }

        /// <summary>
        /// same as PatchCommons.GetModifiedBlockCounterInstructions()
        /// </summary>
        private static List<CodeInstruction> GetModifiedBlockCounterInstructions(Label retLabel) {
            Type vbm = typeof(VehicleBehaviorManager);
            FieldInfo instance = vbm.GetField(nameof(VehicleBehaviorManager.Instance));
            MethodInfo mayDespawn = vbm.GetMethod(nameof(VehicleBehaviorManager.MayDespawn));

            return new List<CodeInstruction> {
                new CodeInstruction(OpCodes.Ldsfld, instance),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Callvirt, mayDespawn),
                new CodeInstruction(OpCodes.Brfalse_S, retLabel)
            };
        }

    }
}