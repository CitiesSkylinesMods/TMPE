namespace TrafficManager.Patch._VehicleAI.Connection {
    using System;
    using CSUtil.Commons;
    using Util;

    public class VehicleAIHook {
        internal static VehicleAIConnection GetConnection() {
            try {
                CalculateTargetSpeedDelegate calculateTargetSpeedDelegate = TranspilerUtil.CreateDelegate<CalculateTargetSpeedDelegate>(typeof(VehicleAI), "CalculateTargetSpeed", true);
                CalculateTargetSpeedByNetInfoDelegate calculateTargetSpeedByNetInfoDelegate = TranspilerUtil.CreateDelegate<CalculateTargetSpeedByNetInfoDelegate>(typeof(VehicleAI), "CalculateTargetSpeed", true);
                PathfindFailureDelegate pathfindFailureDelegate = TranspilerUtil.CreateDelegate<PathfindFailureDelegate>(typeof(CarAI), "PathfindFailure", true);
                PathfindSuccessDelegate pathfindSuccessDelegate = TranspilerUtil.CreateDelegate<PathfindSuccessDelegate>(typeof(CarAI), "PathfindSuccess", true);
                InvalidPathDelegate invalidPathDelegate = TranspilerUtil.CreateDelegate<InvalidPathDelegate>(typeof(VehicleAI), "InvalidPath", true);
                ParkVehicleDelegate parkVehicleDelegate = TranspilerUtil.CreateDelegate<ParkVehicleDelegate>(typeof(VehicleAI), "ParkVehicle", true);
                NeedChangeVehicleTypeDelegate needChangeVehicleTypeDelegate = TranspilerUtil.CreateDelegate<NeedChangeVehicleTypeDelegate>(typeof(VehicleAI), "NeedChangeVehicleType", true);
                CalculateSegmentPositionDelegate calculateSegmentPosition = TranspilerUtil.CreateDelegate<CalculateSegmentPositionDelegate>(typeof(VehicleAI), "CalculateSegmentPosition", true);
                CalculateSegmentPositionDelegate2 calculateSegmentPosition2 = TranspilerUtil.CreateDelegate<CalculateSegmentPositionDelegate2>(typeof(VehicleAI), "CalculateSegmentPosition", true);
                ChangeVehicleTypeDelegate changeVehicleTypeDelegate = TranspilerUtil.CreateDelegate<ChangeVehicleTypeDelegate>(typeof(VehicleAI), "ChangeVehicleType", true);
                UpdateNodeTargetPosDelegate updateNodeTargetPosDelegate = TranspilerUtil.CreateDelegate<UpdateNodeTargetPosDelegate>(typeof(VehicleAI), "UpdateNodeTargetPos", true);
                ArrivingToDestinationDelegate arrivingToDestinationDelegate = TranspilerUtil.CreateDelegate<ArrivingToDestinationDelegate>(typeof(VehicleAI), "ArrivingToDestination", true);
                LeftHandDriveDelegate leftHandDriveDelegate = TranspilerUtil.CreateDelegate<LeftHandDriveDelegate>(typeof(VehicleAI), "LeftHandDrive", true);
                NeedStopAtNodeDelegate needStopAtNodeDelegate = TranspilerUtil.CreateDelegate<NeedStopAtNodeDelegate>(typeof(VehicleAI), "NeedStopAtNode", true);

                return new VehicleAIConnection(calculateTargetSpeedDelegate,
                                               calculateTargetSpeedByNetInfoDelegate,
                                               pathfindFailureDelegate,
                                               pathfindSuccessDelegate,
                                               invalidPathDelegate,
                                               parkVehicleDelegate,
                                               needChangeVehicleTypeDelegate,
                                               calculateSegmentPosition,
                                               calculateSegmentPosition2,
                                               changeVehicleTypeDelegate,
                                               updateNodeTargetPosDelegate,
                                               arrivingToDestinationDelegate,
                                               leftHandDriveDelegate,
                                               needStopAtNodeDelegate);
            } catch (Exception e) {
                Log.Error(e.Message);
                return null;
            }
        }
    }
}