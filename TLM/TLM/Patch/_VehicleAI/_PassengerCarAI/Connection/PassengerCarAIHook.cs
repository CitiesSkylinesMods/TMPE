namespace TrafficManager.Patch._VehicleAI._PassengerCarAI.Connection {
    using System;
    using System.Reflection;
    using API.Manager.Connections;
    using CSUtil.Commons;
    using HarmonyLib;
    using Util;

    public static class PassengerCarAIHook {
        private static MethodInfo TargetMethod() =>
            TranspilerUtil.DeclaredMethod<FindParkingSpaceDelegate>(typeof(PassengerCarAI), "FindParkingSpace");

        private static MethodInfo TargetMethodProp() =>
            TranspilerUtil.DeclaredMethod<FindParkingSpacePropDelegate>(typeof(PassengerCarAI), "FindParkingSpaceProp");

        private static MethodInfo TargetMethodRoadSide() =>
            TranspilerUtil.DeclaredMethod<FindParkingSpaceRoadSideDelegate>(typeof(PassengerCarAI), "FindParkingSpaceRoadSide");

        internal static IPassengerCarAIConnection GetConnection() {
            try {
                FindParkingSpaceDelegate findParkingSpaceDelegate =
                    AccessTools.MethodDelegate<FindParkingSpaceDelegate>(TargetMethod());
                FindParkingSpacePropDelegate findParkingSpacePropDelegate =
                    AccessTools.MethodDelegate<FindParkingSpacePropDelegate>(TargetMethodProp());
                FindParkingSpaceRoadSideDelegate findParkingSpaceRoadSideDelegate =
                    AccessTools.MethodDelegate<FindParkingSpaceRoadSideDelegate>(TargetMethodRoadSide());

                return new PassengerCarAIConnection(findParkingSpaceDelegate,
                                                    findParkingSpacePropDelegate,
                                                    findParkingSpaceRoadSideDelegate);
            } catch (Exception e) {
                Log.Error(e.Message);
                return null;
            }
        }
    }
}