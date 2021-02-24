namespace TrafficManager.API.Manager.Connections {
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

    public interface IPassengerCarAIConnection {
        FindParkingSpaceDelegate FindParkingSpace { get; }
        FindParkingSpacePropDelegate FindParkingSpaceProp { get; }
        FindParkingSpaceRoadSideDelegate FindParkingSpaceRoadSide { get; }
    }
}