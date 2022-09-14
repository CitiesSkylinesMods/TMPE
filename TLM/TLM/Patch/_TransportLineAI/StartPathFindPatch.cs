namespace TrafficManager.Patch._TransportLineAI {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using Custom.PathFinding;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch(typeof(TransportLineAI), nameof(TransportLineAI.StartPathFind))]
    public static class StartPathFindPatch {
        private delegate void CheckSegmentProblemsDelegate(ushort segmentID, ref NetSegment data);
        private static MethodBase CheckSegmentProblemsMethod() => TranspilerUtil.DeclaredMethod<CheckSegmentProblemsDelegate>( typeof(TransportLineAI), "CheckSegmentProblems");

        private delegate void CreatePathDelegate(out uint unit,
                                                 ref Randomizer randomizer,
                                                 uint buildIndex,
                                                 PathUnit.Position startPosA,
                                                 PathUnit.Position startPosB,
                                                 PathUnit.Position endPosA,
                                                 PathUnit.Position endPosB,
                                                 NetInfo.LaneType laneTypes,
                                                 VehicleInfo.VehicleType vehicleTypes,
                                                 VehicleInfo.VehicleCategory vehicleCategories,
                                                 float maxLength,
                                                 bool isHeavyVehicle,
                                                 bool ignoreBlocked,
                                                 bool stablePath,
                                                 bool skipQueue);

        private static MethodBase CreatePathMethod() => TranspilerUtil.DeclaredMethod<CreatePathDelegate>(typeof(PathManager), "CreatePath");

        [UsedImplicitly]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(ILGenerator il, IEnumerable<CodeInstruction> instructions) {
            List<CodeInstruction> codes = TranspilerUtil.ToCodeList(instructions);
            int searchInstruction = TranspilerUtil.SearchInstruction(codes, new CodeInstruction(OpCodes.Call, CheckSegmentProblemsMethod()), 0, 1, 3);
            if (searchInstruction != -1
                && codes[searchInstruction + 1].opcode.Equals(OpCodes.Ldc_I4_1)
                && codes[searchInstruction + 2].opcode.Equals(OpCodes.Ret)) {

                int startTarget = searchInstruction + 3; //first instruction to remove, contains labels to copy
                int createPathCallIndex = TranspilerUtil.SearchInstruction( codes, new CodeInstruction(OpCodes.Callvirt, CreatePathMethod()), startTarget);

                if (createPathCallIndex != -1 && codes[createPathCallIndex + 1].opcode.Equals(OpCodes.Brfalse)) {
                    List<Label> labels = codes[startTarget].labels;
                    codes.RemoveRange(startTarget, createPathCallIndex + 1 - startTarget);
                    codes.InsertRange(startTarget, GetInjectInstructions(labels));
                } else {
                    throw new Exception("Could not find CreatePath call or instructions has been patched");
                }
            } else {
                throw new Exception("Could not find 3rd. CheckSegmentProblems call or instructions has been patched");
            }

            return codes;
        }

        private static List<CodeInstruction> GetInjectInstructions(List<Label> labels) {
            FieldInfo instanceField = typeof(CustomPathManager).GetField(nameof(CustomPathManager._instance));
            MethodBase createTransportPath = typeof(CustomPathManager).GetMethod( nameof(CustomPathManager.CreateTransportLinePath));

            CodeInstruction firstInstruction = new CodeInstruction(OpCodes.Ldsfld, instanceField);
            firstInstruction.labels.AddRange(labels);
            return new List<CodeInstruction> {
                firstInstruction,
                new CodeInstruction(OpCodes.Ldloca_S,  21),
                new CodeInstruction(OpCodes.Ldloc_S,7),
                new CodeInstruction(OpCodes.Ldloc_S, 8),
                new CodeInstruction(OpCodes.Ldloc_S, 9),
                new CodeInstruction(OpCodes.Ldloc_S, 10),
                new CodeInstruction(OpCodes.Ldarg_S, 4),
                new CodeInstruction(OpCodes.Ldarg_S, 5),
                new CodeInstruction(OpCodes.Ldarg_S, 6),
                new CodeInstruction(OpCodes.Callvirt, createTransportPath),
            };
        }

        /*

         ---------------------------
         if(CustomPathManager._instance.CreateTransportLinePath(
                out uint path,
                startPosA,
                startPosB,
                endPosA,
                endPosB,
                vehicleType,
                vehicleCategory,
                skipQueue)) {
         ---------------------------
            ...
            IL_02c9: ldsfld       class TrafficManager.Custom.PathFinding.CustomPathManager TrafficManager.Custom.PathFinding.CustomPathManager::_instance
            IL_02ce: ldloca.s     path
            IL_02d0: ldloc.s      startPosA
            IL_02d1: ldloc.s      startPosB
            IL_02d3: ldloc.s      endPosA
            IL_02d5: ldloc.s      endPosB
            IL_02d7: ldarg.s      vehicleType
            IL_02d9: ldarg.s      vehicleCategory
            IL_02db: ldarg.s      skipQueue
            IL_02dd: callvirt     instance bool TrafficManager.Custom.PathFinding.CustomPathManager::CreateTransportLinePath(unsigned int32&, valuetype ['Assembly-CSharp']PathUnit/Position, valuetype ['Assembly-CSharp']PathUnit/Position, valuetype ['Assembly-CSharp']PathUnit/Position, valuetype ['Assembly-CSharp']PathUnit/Position, valuetype ['Assembly-CSharp']VehicleInfo/VehicleType, bool)
            ...

            .locals init (
              Local var 0: NetManager
              Local var 1: System.Int32
              Local var 2: System.UInt16
              Local var 3: System.Int32
              Local var 4: System.UInt16
              Local var 5: UnityEngine.Vector3
              Local var 6: UnityEngine.Vector3
              Local var 7: PathUnit/Position
              Local var 8: PathUnit/Position
              Local var 9: PathUnit/Position
              Local var 10: PathUnit/Position
              Local var 11: System.Single
              Local var 12: System.Single
              Local var 13: System.Single
              Local var 14: System.Single
              Local var 15: System.Boolean
              Local var 16: System.Boolean
              Local var 17: System.Boolean
              Local var 18: System.Boolean
              Local var 19: NetInfo/LaneType
              Local var 20: System.Single
              Local var 21: System.UInt32
            )
         */
    }
}