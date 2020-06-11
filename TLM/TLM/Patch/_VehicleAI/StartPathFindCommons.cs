namespace TrafficManager.Patch._VehicleAI {
    using System.Reflection;
    using TrafficManager.Util;
    using System;
    using ColossalFramework.Math;
    using UnityEngine;

    public static class StartPathFindCommons {
        // protected override bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget)        delegate bool TargetDelegate(out uint unit, ref Randomizer randomizer, uint buildIndex,
        delegate bool TargetDelegate(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget);
        public static MethodBase TargetMethod<T>() {
            return TranspilerUtil.DeclaredMethod<TargetDelegate>(typeof(T), "StartPathFind");
        }
    }
}
