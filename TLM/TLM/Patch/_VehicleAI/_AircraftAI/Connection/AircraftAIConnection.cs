namespace TrafficManager.Patch._VehicleAI._AircraftAI.Connection {
    using System;
    using ColossalFramework.Math;

    public delegate float CalculateMaxSpeedDelegate(float targetDistance, float targetSpeed, float maxBraking);
    public delegate bool CheckOverlapDelegate(Segment3 segment, ushort ignoreVehicle);
    public delegate bool IsFlightPathAheadDelegate(AircraftAI vehicleAI, Vehicle vehicleData);
    public delegate bool IsOnFlightPathDelegate(AircraftAI vehicleAI, Vehicle vehicleData);
    public delegate void ReserveSpaceDelegate(AircraftAI vehicleAI, ushort vehicleID, ref Vehicle vehicleData, bool moving);

    internal class AircraftAIConnection{

        internal AircraftAIConnection(CalculateMaxSpeedDelegate calculateMaxSpeedDelegate,
                                      CheckOverlapDelegate checkOverlapDelegate,
                                      IsOnFlightPathDelegate isOnFlightPathDelegate,
                                      IsFlightPathAheadDelegate isFlightPathAheadDelegate,
                                      ReserveSpaceDelegate reserveSpaceDelegate) {
            CalculateMaxSpeed = calculateMaxSpeedDelegate ?? throw new ArgumentNullException(nameof(calculateMaxSpeedDelegate));
            CheckOverlap = checkOverlapDelegate ?? throw new ArgumentNullException(nameof(checkOverlapDelegate));
            IsFlightPathAhead = isFlightPathAheadDelegate ?? throw new ArgumentNullException(nameof(isFlightPathAheadDelegate));
            IsOnFlightPath = isOnFlightPathDelegate ?? throw new ArgumentNullException(nameof(isOnFlightPathDelegate));
            ReserveSpace = reserveSpaceDelegate ?? throw new ArgumentNullException(nameof(reserveSpaceDelegate));
        }

        public CheckOverlapDelegate CheckOverlap { get; }
        public CalculateMaxSpeedDelegate CalculateMaxSpeed { get; }
        public IsFlightPathAheadDelegate IsFlightPathAhead { get; }
        public IsOnFlightPathDelegate IsOnFlightPath { get; }
        public ReserveSpaceDelegate ReserveSpace { get; }
    }
}