namespace TrafficManager.Patch {
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using HarmonyLib;
    using Manager.Impl;
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
    }
}