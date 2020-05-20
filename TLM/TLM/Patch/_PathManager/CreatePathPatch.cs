namespace TrafficManager.Patch._RoadBaseAI {
    using API.Traffic.Enums;
    using ColossalFramework;
    using ColossalFramework.Math;
    using HarmonyLib;
    using JetBrains.Annotations;
    using System;
    using System.Reflection;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.Util;

    [HarmonyPatch]
    [UsedImplicitly]
    public class CreatePathPatch {
        // public bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, PathUnit.Position vehiclePosition, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue, bool randomParking, bool ignoreFlooded, bool combustionEngine, bool ignoreCost)
        delegate bool TargetDelegate(out uint unit, ref Randomizer randomizer, uint buildIndex,
            PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, PathUnit.Position vehiclePosition,
            NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle,
            bool ignoreBlocked, bool stablePath, bool skipQueue, bool randomParking, bool ignoreFlooded, bool combustionEngine, bool ignoreCost);

        [UsedImplicitly]
        public static MethodBase TargetMethod() {
            return HarmonyLib.AccessTools.DeclaredMethod(
                typeof(PathManager),
                nameof(PathManager.CreatePath),
                TranspilerUtil.GetGenericArguments<TargetDelegate>()) ??
                throw new Exception("CreatePathPatch failed to find TargetMethod");
        }

        public static PathCreationArgs Args;

        [UsedImplicitly]
        public static bool Prefix(ref bool __result, out uint unit, ref Randomizer randomizer, 
            PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB,
            VehicleInfo.VehicleType vehicleTypes, bool isHeavyVehicle, bool ignoreBlocked, bool combustionEngine)
        {
            Args.startPosA = startPosA;
            Args.startPosB = startPosB;
            Args.endPosA = endPosA;
            Args.endPosB = endPosB;

            Args.vehicleTypes = vehicleTypes;
            Args.isHeavyVehicle = isHeavyVehicle;
            Args.hasCombustionEngine = combustionEngine;
            Args.ignoreBlocked = ignoreBlocked;

            __result = CustomPathManager._instance.CustomCreatePath(out unit, ref randomizer, Args);

            Args = default;

            return false; //CustomCreatePath replaces CreatePath
        }
    }
}