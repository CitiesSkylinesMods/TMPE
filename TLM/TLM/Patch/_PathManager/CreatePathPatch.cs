namespace TrafficManager.Patch._RoadBaseAI {
    using API.Traffic.Enums;
    using ColossalFramework;
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using HarmonyLib;
    using JetBrains.Annotations;
    using System;
    using System.Reflection;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.State.ConfigData;
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

        public static ExtVehicleType ExtVehicleType;
        public static ExtPathType ExtPathType;
        public static ushort VehicleID;

        public static bool? StablePath;
        public static NetInfo.LaneType? LaneTypes;

        /// <summary>
        /// precondition: Args.extVehicleType, Args.extPathType, Args.vehicleId, and Args.stablePath are initialized.
        /// </summary>
        [UsedImplicitly]
        public static bool Prefix(ref bool __result, out uint unit, ref Randomizer randomizer, uint buildIndex,
            PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, PathUnit.Position vehiclePosition,
            NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle,
            bool ignoreBlocked, bool stablePath, bool skipQueue, bool randomParking, bool ignoreFlooded, bool combustionEngine, bool ignoreCost)
        {
            var vehicleBuffer = VehicleManager.instance.m_vehicles.m_buffer;
            ref Vehicle vehicleData = ref vehicleBuffer[VehicleID];
            var info = vehicleData.Info;
            var ai = info.m_vehicleAI;

            PathCreationArgs args;
            args.vehicleId = VehicleID;
            args.extPathType = ExtPathType;
            args.extVehicleType = ExtVehicleType;
            args.spawned = vehicleData.m_flags.IsFlagSet(Vehicle.Flags.Spawned);

            // vanilla values
            args.buildIndex = buildIndex;
            args.vehiclePosition = vehiclePosition;
            args.stablePath = stablePath;
            args.randomParking = randomParking;
            args.ignoreFlooded = ignoreFlooded;
            args.ignoreCosts = ignoreCost;

            args.laneTypes = laneTypes;
            args.vehicleTypes = vehicleTypes;
            args.isHeavyVehicle = isHeavyVehicle;
            args.hasCombustionEngine = combustionEngine;
            args.ignoreBlocked = ignoreBlocked;
            args.maxLength = maxLength;

            args.startPosA = startPosA;
            args.startPosB = startPosB;
            args.endPosA = endPosA;
            args.endPosB = endPosB;

            // overridden vanilla values:
            args.skipQueue = args.spawned;
            args.stablePath = StablePath ?? stablePath;
            args.laneTypes = LaneTypes ?? laneTypes;

            __result = CustomPathManager._instance.CustomCreatePath(out unit, ref randomizer, args);
            if (__result) {
#if DEBUG
                bool vehDebug = DebugSettings.VehicleId == 0 || DebugSettings.VehicleId == VehicleID;
                bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && vehDebug;
#else
                var logParkingAi = false;
#endif
                var path = unit;
                Log._DebugIf(
                    logParkingAi,
                    () => $"CreatePathPatch.Prefix({args.vehicleId}): " +
                    $"Path-finding starts for vehicle {args.vehicleId}, path={path}, " +
                    $"extVehicleType={ExtVehicleType}, startPosA.segment={startPosA.m_segment}, " +
                    $"startPosA.lane={startPosA.m_lane}, info.m_vehicleType={info.m_vehicleType}, " +
                    $"endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}");
            }

            return false; // CustomCreatePath replaces CreatePath
        }
    }
}