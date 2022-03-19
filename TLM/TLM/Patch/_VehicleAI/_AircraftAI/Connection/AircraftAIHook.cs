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
                CalculateMaxSpeedDelegate calculateMaxSpeedDelegate = TranspilerUtil.CreateDelegate<CalculateMaxSpeedDelegate>(typeof(AircraftAI), "CalculateMaxSpeed", false);
                CheckOverlapDelegate checkOverlapDelegate = AccessTools.MethodDelegate<CheckOverlapDelegate>(TargetMethod());
                IsOnFlightPathDelegate isOnFlightPathDelegate = TranspilerUtil.CreateDelegate<IsOnFlightPathDelegate>(typeof(AircraftAI), "IsOnFlightPath", true);
                IsFlightPathAheadDelegate isFlightPathAheadDelegate = TranspilerUtil.CreateDelegate<IsFlightPathAheadDelegate>(typeof(AircraftAI), "IsFlightPathAhead", true);
                ReserveSpaceDelegate reserveSpaceDelegate = TranspilerUtil.CreateDelegate<ReserveSpaceDelegate>(typeof(AircraftAI), "ReserveSpace", true);

                return new AircraftAIConnection(calculateMaxSpeedDelegate,
                                                checkOverlapDelegate,
                                                isOnFlightPathDelegate,
                                                isFlightPathAheadDelegate,
                                                reserveSpaceDelegate);
            } catch (Exception e) {
                Log.Error(e.Message);
                return null;
            }
        }
    }
}