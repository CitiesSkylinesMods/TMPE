namespace TrafficManager.Patch {
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using API.Manager;
    using HarmonyLib;
    using Manager.Impl;
    using State;
    using Util;

    public static class PatchCommons {
        private static FieldInfo _vehicleBehaviourManagerInstanceField => typeof(VehicleBehaviorManager).GetField(nameof(VehicleBehaviorManager.Instance));
        private static MethodBase _mayDespawnMethod => typeof(VehicleBehaviorManager).GetMethod(nameof(VehicleBehaviorManager.MayDespawn));

        /// <summary>
        /// Inserts MayDespawn call into the instruction chain
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        public static IEnumerable<CodeInstruction> ReplaceSimulationStepIsCongestedCheck(IEnumerable<CodeInstruction> instructions) {
            List<CodeInstruction> codes = TranspilerUtil.ToCodeList(instructions);

            bool found = false;
            foreach (CodeInstruction instruction in codes) {
                if (!found && instruction.opcode.Equals(OpCodes.Brfalse_S)) {
                    found = true;
                    yield return instruction; // return found instruction
                    yield return new CodeInstruction(OpCodes.Ldsfld, _vehicleBehaviourManagerInstanceField); // loadInstFiled
                    yield return new CodeInstruction(OpCodes.Ldarg_2); // loadVehicleData
                    yield return new CodeInstruction(OpCodes.Callvirt, _mayDespawnMethod); // callMayDespawn
                    yield return instruction.Clone(); //brfalse_s clone including label!
                } else {
                    yield return instruction;
                }
            }

            /*
             -----------------------------------------------------------------------------------
             SimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos)
             -----------------------------------------------------------------------------------

             IL_0000: ldarg.2      // vehicleData
             IL_0001: ldfld        valuetype ['Assembly-CSharp']Vehicle/Flags ['Assembly-CSharp']Vehicle::m_flags
             IL_0006: ldc.i4       67108864 // 0x04000000
             IL_000b: and
             IL_000c: brfalse.s    IL_0027

             // NON-STOCK CODE START
             IL_000e: ldsfld       class TrafficManager.Manager.Impl.VehicleBehaviorManager TrafficManager.Manager.Impl.VehicleBehaviorManager::Instance
             IL_0013: ldarg.2      // vehicleData
             IL_0014: callvirt     instance bool TrafficManager.Manager.Impl.VehicleBehaviorManager::MayDespawn(valuetype ['Assembly-CSharp']Vehicle&)
             IL_0019: brfalse.s    IL_0027
             // NON-STOCK CODE STOP

             ...
             */
        }

        private delegate bool TrySpawnDelegate(ushort vehicleID, ref Vehicle vehicleData);

        /// <summary>
        /// Revrites instructions adding necessary TMPE calls
        /// </summary>
        /// <param name="il"> Il Generator</param>
        /// <param name="instructions">List of instructions</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static IEnumerable<CodeInstruction> TranspileTramTrainSimulationStep(ILGenerator il, IEnumerable<CodeInstruction> instructions) {
            List<CodeInstruction> codes = TranspilerUtil.ToCodeList(instructions);

            MethodBase trySpawnCall = TranspilerUtil.DeclaredMethod<TrySpawnDelegate>(typeof(VehicleAI), "TrySpawn");

            CodeInstruction searchInstruction = new CodeInstruction(OpCodes.Callvirt, trySpawnCall);
            int index = codes.FindIndex( instruction => TranspilerUtil.IsSameInstruction(instruction, searchInstruction));
            if (index > -1) {
                int target1 = index + 2;
                Label label = il.DefineLabel();
                codes[target1].labels.Add(label);//add label to next instruction (if() false jump)
                codes.InsertRange(target1,GetUpdatePositionInstructions(label));

                CodeInstruction searchInstruction2 = new CodeInstruction(OpCodes.Ldfld, typeof(Vehicle).GetField(nameof(Vehicle.m_blockCounter)));
                int index2 = codes.FindIndex( instruction => TranspilerUtil.IsSameInstruction(instruction, searchInstruction2));
                if (index2 > -1 && codes[index2 +1].opcode.Equals(OpCodes.Ldc_I4) && codes[index2 + 2].opcode.Equals(OpCodes.Bne_Un)) {
                    int target2 = index2 + 2;
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

        private static List<CodeInstruction> GetUpdatePositionInstructions(Label label) {
            PropertyInfo getFactory = typeof(Constants).GetProperty(nameof(Constants.ManagerFactory));
            PropertyInfo getExtVehMan = typeof(IManagerFactory).GetProperty(nameof(IManagerFactory.ExtVehicleManager));
            MethodInfo updateVehPos = typeof(IExtVehicleManager).GetMethod( nameof(IExtVehicleManager.UpdateVehiclePosition));
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

            /*
             --------------------------------------
                Constants.ManagerFactory.ExtVehicleManager.UpdateVehiclePosition(vehicleId, ref vehicleData);
                if (Options.advancedAI && (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0)
                {
                    // Advanced AI traffic measurement
                    Constants.ManagerFactory.ExtVehicleManager..LogTraffic(vehicleId, ref vehicleData);
                }
             --------------------------------------

              call         class [TMPE.API]TrafficManager.API.Manager.IManagerFactory TrafficManager.Constants::get_ManagerFactory()
              callvirt     instance class [TMPE.API]TrafficManager.API.Manager.IExtVehicleManager [TMPE.API]TrafficManager.API.Manager.IManagerFactory::get_ExtVehicleManager()
              ldarg.1      // vehicleId
              ldarg.2      // vehicleData
              callvirt     instance void [TMPE.API]TrafficManager.API.Manager.IExtVehicleManager::UpdateVehiclePosition(unsigned int16, valuetype ['Assembly-CSharp']Vehicle&)

              ldsfld       bool TrafficManager.State.Options::advancedAI
              brfalse.s    IL_0129
              ldarg.2      // vehicleData
              ldfld        valuetype ['Assembly-CSharp']Vehicle/Flags ['Assembly-CSharp']Vehicle::m_flags
              ldc.i4.4
              and
              brfalse.s    IL_0129

              call         class [TMPE.API]TrafficManager.API.Manager.IManagerFactory TrafficManager.Constants::get_ManagerFactory()
              callvirt     instance class [TMPE.API]TrafficManager.API.Manager.IExtVehicleManager [TMPE.API]TrafficManager.API.Manager.IManagerFactory::get_ExtVehicleManager()
              ldarg.1      // vehicleId
              ldarg.2      // vehicleData
              callvirt     instance void [TMPE.API]TrafficManager.API.Manager.IExtVehicleManager::LogTraffic(unsigned int16, valuetype ['Assembly-CSharp']Vehicle&)

             */
        }

        private static List<CodeInstruction> GetModifiedBlockCounterInstructions(Label retLabel) {
            Type vbm = typeof(VehicleBehaviorManager);
            FieldInfo instance = vbm.GetField(nameof(VehicleBehaviorManager.Instance));
            MethodInfo mayDespawn = vbm.GetMethod(nameof(VehicleBehaviorManager.MayDespawn));

            return new List<CodeInstruction> {
                new CodeInstruction(OpCodes.Ldsfld, instance),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Callvirt, mayDespawn),
                new CodeInstruction(OpCodes.Brfalse_S, retLabel)
            };
            /*
             ------------------------------
                if (VehicleBehaviorManager.Instance.MayDespawn(ref vehicleData)) {
                    Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
                }
             ------------------------------

              ldsfld       class TrafficManager.Manager.Impl.VehicleBehaviorManager TrafficManager.Manager.Impl.VehicleBehaviorManager::Instance
              ldarg.2      // vehicleData
              callvirt     instance bool TrafficManager.Manager.Impl.VehicleBehaviorManager::MayDespawn(valuetype ['Assembly-CSharp']Vehicle&)
              brfalse.s    IL_0248
             */
        }
    }
}