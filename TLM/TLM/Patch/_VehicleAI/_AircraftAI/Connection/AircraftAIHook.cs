namespace TrafficManager.Patch._VehicleAI._AircraftAI.Connection {
    using System;
    using System.Reflection;
    using CSUtil.Commons;
    using HarmonyLib;
    using Util;

    public static class AircraftAIHook {
        private static MethodInfo TargetMethod() =>
            TranspilerUtil.DeclaredMethod<CheckOverlapDelegate>(typeof(AircraftAI), "CheckOverlap");

        internal static AircraftAIConnection GetConnection() {
            try {
                CheckOverlapDelegate checkOverlapDelegate = AccessTools.MethodDelegate<CheckOverlapDelegate>(TargetMethod());

                return new AircraftAIConnection(checkOverlapDelegate);
            } catch (Exception e) {
                Log.Error(e.Message);
                return null;
            }
        }
    }
}