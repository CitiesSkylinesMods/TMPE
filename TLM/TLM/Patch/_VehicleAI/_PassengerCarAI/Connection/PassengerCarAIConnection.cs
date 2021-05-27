namespace TrafficManager.Patch._VehicleAI._PassengerCarAI.Connection {
    using System;
    using UnityEngine;

    public delegate bool FindParkingSpaceRoadSideDelegate(ushort ignoreParked,
                                                          ushort requireSegment,
                                                          Vector3 refPos,
                                                          float width,
                                                          float length,
                                                          out Vector3 parkPos,
                                                          out Quaternion parkRot,
                                                          out float parkOffset);

    public delegate bool FindParkingSpaceDelegate(bool isElectric,
                                                  ushort homeId,
                                                  Vector3 refPos,
                                                  Vector3 searchDir,
                                                  ushort segment,
                                                  float width,
                                                  float length,
                                                  out Vector3 parkPos,
                                                  out Quaternion parkRot,
                                                  out float parkOffset);

    public delegate bool FindParkingSpacePropDelegate(bool isElectric,
                                                      ushort ignoreParked,
                                                      PropInfo info,
                                                      Vector3 position,
                                                      float angle,
                                                      bool fixedHeight,
                                                      Vector3 refPos,
                                                      float width,
                                                      float length,
                                                      ref float maxDistance,
                                                      ref Vector3 parkPos,
                                                      ref Quaternion parkRot);

    public delegate ushort GetDriverInstanceDelegate(PassengerCarAI passengerCarAI,
                                                     ushort vehicleID,
                                                     ref Vehicle vehicleData);
    internal class PassengerCarAIConnection{

        internal PassengerCarAIConnection(FindParkingSpaceDelegate findParkingSpaceDelegate,
                                          FindParkingSpacePropDelegate findParkingSpacePropDelegate,
                                          FindParkingSpaceRoadSideDelegate findParkingSpaceRoadSideDelegate,
                                          GetDriverInstanceDelegate getDriverInstanceDelegate) {
            FindParkingSpace = findParkingSpaceDelegate ?? throw new ArgumentNullException(nameof(findParkingSpaceDelegate));
            FindParkingSpaceProp = findParkingSpacePropDelegate ?? throw new ArgumentNullException(nameof(findParkingSpacePropDelegate));
            FindParkingSpaceRoadSide = findParkingSpaceRoadSideDelegate ?? throw new ArgumentNullException(nameof(findParkingSpaceRoadSideDelegate));
            GetDriverInstance = getDriverInstanceDelegate ?? throw new ArgumentNullException(nameof(getDriverInstanceDelegate));
        }

        public FindParkingSpaceDelegate FindParkingSpace { get; }
        public FindParkingSpacePropDelegate FindParkingSpaceProp { get; }
        public FindParkingSpaceRoadSideDelegate FindParkingSpaceRoadSide { get; }
        public GetDriverInstanceDelegate GetDriverInstance { get; }
    }
}