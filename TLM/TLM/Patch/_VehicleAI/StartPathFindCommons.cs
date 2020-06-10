namespace TrafficManager.Patch._VehicleAI {
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using UnityEngine;
    using TrafficManager.Patch._RoadBaseAI;
    using System.Reflection;
    using TrafficManager.Util;
    using System;
    using ColossalFramework.Math;
    using CSUtil.Commons;

    public static class StartPathFindCommons {
        // public bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, PathUnit.Position vehiclePosition, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue, bool randomParking, bool ignoreFlooded, bool combustionEngine, bool ignoreCost)
        delegate bool TargetDelegate(out uint unit, ref Randomizer randomizer, uint buildIndex,
            PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, PathUnit.Position vehiclePosition,
            NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle,
            bool ignoreBlocked, bool stablePath, bool skipQueue, bool randomParking, bool ignoreFlooded, bool combustionEngine, bool ignoreCost);

        public static MethodBase TargetMethod<T>() {
            return HarmonyLib.AccessTools.DeclaredMethod(
                typeof(T),
                "StartPathFind",
                TranspilerUtil.GetGenericArguments<TargetDelegate>()) ??
                throw new Exception("StartPathFind failed to find TargetMethod");
        }
    }
}
