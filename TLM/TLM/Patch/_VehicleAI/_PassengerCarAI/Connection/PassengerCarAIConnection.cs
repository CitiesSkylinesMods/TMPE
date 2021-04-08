namespace TrafficManager.Patch._VehicleAI._PassengerCarAI.Connection {
    using System;
    using Manager.Connections;

    internal class PassengerCarAIConnection: IPassengerCarAIConnection {
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