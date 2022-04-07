namespace TrafficManager.API.Traffic.Enums {
    using System;

    // draft - this will need refinement
    [Flags]
    public enum EntityType : ushort {
        None = 0,

        Node = 1 << 0,
        Segment = 1 << 1,
        SegmentEnd = Segment | End,
        Lane = 1 << 2,
        LaneEnd = Lane | End,
        Citizen = 1 << 3,
        CitizenInstance = 1 << 4,
        Vehicle = 1 << 5,
        ParkedVehicle = 1 << 6,
        Building = 1 << 7,
        ParkingSpace = 1 << 8,
        Prop = 1 << 9,

        End = 1 << 14,
        IsStartNode = 1 << 15,
    }
}
