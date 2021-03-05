namespace TrafficManager.Patch._VehicleAI._TrolleybusAI {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using CSUtil.Commons;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using UnityEngine;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch]
    public class CalculateSegmentPositionPatch {

        private delegate void CalculatePositionDelegate(ushort vehicleID,
                                                        ref Vehicle vehicleData,
                                                        PathUnit.Position position,
                                                        uint laneID,
                                                        byte offset,
                                                        out Vector3 pos,
                                                        out Vector3 dir,
                                                        out float maxSpeed);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<CalculatePositionDelegate>( typeof(TrolleybusAI), "CalculateSegmentPosition");

        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            List<CodeInstruction> codes = TranspilerUtil.ToCodeList(instructions);
            MethodInfo calcStopPosAndDir = typeof(NetLane).GetMethod(nameof(NetLane.CalculateStopPositionAndDirection));

            CodeInstruction searchInstr = new CodeInstruction(OpCodes.Call, calcStopPosAndDir);
            int index = codes.FindIndex( instr => TranspilerUtil.IsSameInstruction(instr, searchInstr));
            if (index > -1) {
                int targetIndex = index + 1;
                int replace1stIndex = targetIndex + 3;
                if (codes[replace1stIndex].opcode.Equals(OpCodes.Ldarg_2)) {
                    codes.RemoveRange(replace1stIndex + 1, 6);
                    codes.Insert(replace1stIndex + 1, new CodeInstruction(OpCodes.Ldloc_3));
                } else {
                    throw new Exception("Could not find Ldarg.2 instruction or instructions has been patched");
                }
                codes.InsertRange(targetIndex, GetInstructions());
            } else {
                throw new Exception("Could not find CalculateStopPositionAndDirection call or instructions has been patched");
            }

            return codes;
        }

        private static List<CodeInstruction> GetInstructions() {
            MethodInfo getSpeed = typeof(CalculateSegmentPositionPatch).GetMethod(nameof(GetCustomSpeed), BindingFlags.Static | BindingFlags.NonPublic);
            return new List<CodeInstruction> {
                new CodeInstruction(OpCodes.Ldarg_3),
                new CodeInstruction(OpCodes.Ldarg_S, 4),
                new CodeInstruction(OpCodes.Ldloc_1),//info variable
                new CodeInstruction(OpCodes.Call, getSpeed),
                new CodeInstruction(OpCodes.Stloc_3)//store in local var
            };
        }

        private static float GetCustomSpeed(PathUnit.Position position, uint laneID, NetInfo info) {
            return Options.customSpeedLimitsEnabled
                       ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(
                           position.m_segment,
                           position.m_lane,
                           laneID,
                           info.m_lanes[position.m_lane])
                       : info.m_lanes[position.m_lane].m_speedLimit;
        }

        /*
            -------------------------------------------
            float speedLimit = Speed(position, laneID, info);
            maxSpeed = CalculateTargetSpeed(
                vehicleID,
                ref vehicleData,
                speedLimit, // STOCK-CODE: info.m_lanes[(int)position.m_lane].m_speedLimit
                instance.m_lanes.m_buffer[laneID].m_curve);
            -------------------------------------------

    <insert> ldarg.3      // position
    <insert> ldarg.s      laneID
    <insert> ldloc.1      // info
    <insert> call         float32 TrafficManager.Custom.AI.CustomTrolleybusAI::Speed(valuetype ['Assembly-CSharp']PathUnit/Position, unsigned int32, class ['Assembly-CSharp']NetInfo)
    <insert> stloc.3      // speedLimit

   <remove> ldarg.s      maxSpeed
            ldarg.0      // this
            ldarg.1      // vehicleID
            ldarg.2      // vehicleData
            ldloc.3      // speedLimit
   <remove> ldloc.1      // info
   <remove> ldfld        class ['Assembly-CSharp']NetInfo/Lane[] ['Assembly-CSharp']NetInfo::m_lanes
   <remove> ldarg.3      // position
   <remove> ldfld        unsigned int8 ['Assembly-CSharp']PathUnit/Position::m_lane
   <remove> ldelem.ref
   <remove> ldfld        float32 ['Assembly-CSharp']NetInfo/Lane::m_speedLimit
            ldloc.0      // 'instance'
            ldfld        class ['Assembly-CSharp']Array32`1<valuetype ['Assembly-CSharp']NetLane> ['Assembly-CSharp']NetManager::m_lanes
            ldfld        !0valuetype ['Assembly-CSharp']NetLane[] class ['Assembly-CSharp']Array32`1<valuetype ['Assembly-CSharp']NetLane>::m_buffer
            ldarg.s      laneID
            ldelema      ['Assembly-CSharp']NetLane
            ldfld        float32 ['Assembly-CSharp']NetLane::m_curve
            callvirt     instance float32 ['Assembly-CSharp']VehicleAI::CalculateTargetSpeed(unsigned int16, valuetype ['Assembly-CSharp']Vehicle&, float32, float32)
            stind.r4

         */
    }
}