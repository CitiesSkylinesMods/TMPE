namespace TrafficManager.Patch {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using API.Manager;
    using HarmonyLib;
    using Manager.Impl;
    using State;
    using TrafficManager.Patch._VehicleAI;
    using Util;

    public static class PatchCommons {
        private delegate float CalculateTargetSpeedDelegate(ushort vehicleID,
                                                            ref Vehicle data,
                                                            NetInfo info,
                                                            uint lane,
                                                            float curve);
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
                if (!found && (instruction.opcode.Equals(OpCodes.Brfalse_S) || instruction.opcode.Equals(OpCodes.Brfalse))) {
                    found = true;
                    yield return instruction; // return found instruction
                    yield return new CodeInstruction(OpCodes.Ldsfld, _vehicleBehaviourManagerInstanceField); // loadInstFiled
                    yield return new CodeInstruction(OpCodes.Ldarg_1); // vehicleID
                    yield return new CodeInstruction(OpCodes.Ldarg_2); // loadVehicleData
                    yield return new CodeInstruction(OpCodes.Callvirt, _mayDespawnMethod); // callMayDespawn
                    yield return instruction.Clone(); //brfalse_s || brfalse - clone including label!
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
             IL_0013: ldarg.1     // vehicleID
             IL_0013: ldarg.2      // vehicleData
             IL_0014: callvirt     instance bool TrafficManager.Manager.Impl.VehicleBehaviorManager::MayDespawn(valuetype ['Assembly-CSharp']Vehicle&)
             IL_0019: brfalse.s    IL_0027
             // NON-STOCK CODE STOP

             ...
             */
        }

        private delegate bool TrySpawnDelegate(ushort vehicleID, ref Vehicle vehicleData);

        /// <summary>
        /// Rewrites instructions adding necessary TMPE calls
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
                List<Label> oldLabels = codes[target1].labels.ToList();
                codes[target1].labels.Clear(); // clear labels -> they are moved to new instruction
                List<CodeInstruction> newInstructions = GetUpdatePositionInstructions();
                newInstructions[0].labels.AddRange(oldLabels); // add old labels to redirect here
                codes.InsertRange(target1, newInstructions); // insert new instructions

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

        public static void UpdatePosition(ushort vehicleId, ref Vehicle vehicleData) {
            Constants.ManagerFactory.ExtVehicleManager.UpdateVehiclePosition(vehicleId, ref vehicleData);
            if (SavedGameOptions.Instance.advancedAI && (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0) {
                // Advanced AI traffic measurement
                Constants.ManagerFactory.ExtVehicleManager.LogTraffic(vehicleId, ref vehicleData);
            }
        }

        internal static List<CodeInstruction> GetUpdatePositionInstructions() {
            return new List<CodeInstruction> {
                new CodeInstruction(OpCodes.Ldarg_1), // vehicleID
                new CodeInstruction(OpCodes.Ldarg_2), // ref vehicleData
                new CodeInstruction(OpCodes.Call, typeof(PatchCommons).GetMethod(nameof(UpdatePosition))),
            };
        }

        public static List<CodeInstruction> GetModifiedBlockCounterInstructions(Label retLabel) {
            Type vbm = typeof(VehicleBehaviorManager);
            FieldInfo instance = vbm.GetField(nameof(VehicleBehaviorManager.Instance));
            MethodInfo mayDespawn = vbm.GetMethod(nameof(VehicleBehaviorManager.MayDespawn));

            return new List<CodeInstruction> {
                new CodeInstruction(OpCodes.Ldsfld, instance),
                new CodeInstruction(OpCodes.Ldarg_1), // vehicleID
                new CodeInstruction(OpCodes.Ldarg_2), // ref vehicleData
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
              ldarg.1      // vehicleID
              ldarg.2      // vehicleData
              callvirt     instance bool TrafficManager.Manager.Impl.VehicleBehaviorManager::MayDespawn(valuetype ['Assembly-CSharp']Vehicle&)
              brfalse.s    IL_0248
             */
        }


        /// <summary>
        /// Rewrites instructions adding necessary TMPE calls - Bus and Trolleybus
        /// </summary>
        /// <param name="il"> Il Generator</param>
        /// <param name="instructions">List of instructions</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static IEnumerable<CodeInstruction> TranspileBusTrolleybusCalculateTargetSpeed(
                ILGenerator il,
                IEnumerable<CodeInstruction> instructions
            ) {
            List<CodeInstruction> codes = TranspilerUtil.ToCodeList(instructions);
            MethodInfo calcStopPosAndDir = typeof(NetLane).GetMethod(nameof(NetLane.CalculateStopPositionAndDirection));
            MethodInfo calcTargetSpeedByNetInfo = TranspilerUtil.DeclaredMethod<CalculateTargetSpeedDelegate>(typeof(VehicleAI), "CalculateTargetSpeed");

            CodeInstruction searchInstr = new CodeInstruction(OpCodes.Call, calcStopPosAndDir);
            int index = codes.FindIndex( instr => TranspilerUtil.IsSameInstruction(instr, searchInstr));
            if (index > -1) {
                int targetIndex = index + 1; // var maxSpeed
                int searchIndex = targetIndex + 1;
                if (codes[searchIndex].opcode.Equals(OpCodes.Ldarg_0)) {
                    CodeInstruction callCalcTargetSpeed = new CodeInstruction( OpCodes.Callvirt, calcTargetSpeedByNetInfo);
                    int callvirtIndex = codes.FindIndex(targetIndex, instr => TranspilerUtil.IsSameInstruction(instr, callCalcTargetSpeed));
                    if (callvirtIndex > -1) {
                        // remove all starting from var maxSpeed to stind.r4 after original callvirt
                        codes.RemoveRange(targetIndex, callvirtIndex - targetIndex + 2);
                    } else {
                        throw new Exception("Could not find Callvirt CalculateTargetSpeed instruction or instructions has been patched");
                    }
                } else {
                    throw new Exception("Could not find Ldarg.0 instruction or instructions has been patched");
                }
                codes.InsertRange(targetIndex, GetModifiedBusCalculateTargetSpeedInstructions());
            } else {
                throw new Exception("Could not find CalculateStopPositionAndDirection call or instructions has been patched");
            }

            return codes;


            /*
                -------------------------------------------
                GetCustomSpeed(..., out maxSpeed)//
                -------------------------------------------

                 <insert> ldarg.0      // AI instance
                 <insert> ldarg.1      // VehicleID
                 <insert> ldarg.2      // ref Vehicle data
                 <insert> ldarg.3      // position
                 <insert> ldarg.s      laneID
                 <insert> ldloc.1      // info
                 <insert> ldarg.s      maxSpeed // maxSpeed out variable
                 <insert> call         float32 TrafficManager.Custom.AI.CustomTrolleybusAI::GetCustomSpeed(valuetype ['Assembly-CSharp']PathUnit/Position, unsigned int32, class ['Assembly-CSharp']NetInfo)

                <remove>  IL_00B6: ldarg.s   maxSpeed
                <remove>  IL_00B8: ldarg.0
                <remove>  IL_00B9: ldarg.1
                <remove>  IL_00BA: ldarg.2
                <remove>  IL_00BB: ldloc.1
                <remove>  IL_00BC: ldarga.s  position
                <remove>  IL_00BE: ldfld     uint8 PathUnit/Position::m_lane
                <remove>  IL_00C3: ldloc.0
                <remove>  IL_00C4: ldfld     class Array32`1<valuetype NetLane> NetManager::m_lanes
                <remove>  IL_00C9: ldfld     !0[] class Array32`1<valuetype NetLane>::m_buffer
                <remove>  IL_00CE: ldarg.s   laneID
                <remove>  IL_00D0: conv.u
                <remove>  IL_00D1: ldelema   NetLane
                <remove>  IL_00D6: ldfld     float32 NetLane::m_curve
                <remove>  IL_00DB: callvirt  instance float32 VehicleAI::CalculateTargetSpeed(uint16, valuetype Vehicle&, class NetInfo, uint32, float32)
                <remove>  IL_00E0: stind.r4
                          IL_00E1: ret

             */
        }

        private static List<CodeInstruction> GetModifiedBusCalculateTargetSpeedInstructions() {
            MethodInfo getSpeed = typeof(PatchCommons).GetMethod(nameof(GetCustomSpeed), BindingFlags.Static | BindingFlags.NonPublic);
            return new List<CodeInstruction> {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1), // vehicleID
                new CodeInstruction(OpCodes.Ldarg_2), // ref vehicleData
                new CodeInstruction(OpCodes.Ldarg_3),
                new CodeInstruction(OpCodes.Ldarg_S, 4), // laneID
                new CodeInstruction(OpCodes.Ldloc_1),    //info variable
                new CodeInstruction(OpCodes.Ldarg_S, 8), // out maxSpeed
                new CodeInstruction(OpCodes.Call, getSpeed)
            };
        }

        private static void GetCustomSpeed(VehicleAI ai, ushort vehicleId, ref Vehicle data, PathUnit.Position position, uint laneID, NetInfo info, out float maxSpeed) {
            VehicleAICommons.CustomCalculateTargetSpeed(ai, vehicleId, ref data, position, laneID, info, out maxSpeed);
        }
    }
}