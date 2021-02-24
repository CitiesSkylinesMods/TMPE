namespace TrafficManager.Patch._VehicleAI._PassengerCarAI.Connection {
    using System;
    using API.Manager.Connections;

    internal class PassengerCarAIConnection: IPassengerCarAIConnection {
        internal PassengerCarAIConnection(
            FindParkingSpaceDelegate findParkingSpaceDelegate,
                FindParkingSpacePropDelegate findParkingSpacePropDelegate,
                FindParkingSpaceRoadSideDelegate findParkingSpaceRoadSideDelegate) {
            FindParkingSpace = findParkingSpaceDelegate ?? throw new ArgumentNullException(nameof(findParkingSpaceDelegate));
            FindParkingSpaceProp = findParkingSpacePropDelegate ?? throw new ArgumentNullException(nameof(findParkingSpacePropDelegate));
            FindParkingSpaceRoadSide = findParkingSpaceRoadSideDelegate ?? throw new ArgumentNullException(nameof(findParkingSpaceRoadSideDelegate));
        }

        public FindParkingSpaceDelegate FindParkingSpace { get; }
        public FindParkingSpacePropDelegate FindParkingSpaceProp { get; }
        public FindParkingSpaceRoadSideDelegate FindParkingSpaceRoadSide { get; }
    }
}