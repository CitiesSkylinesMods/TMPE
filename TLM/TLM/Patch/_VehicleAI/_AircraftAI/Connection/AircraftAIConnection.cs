namespace TrafficManager.Patch._VehicleAI._AircraftAI.Connection {
    using System;
    using ColossalFramework.Math;

    public delegate bool CheckOverlapDelegate(Segment3 segment, ushort ignoreVehicle);
    internal class AircraftAIConnection{

        internal AircraftAIConnection(CheckOverlapDelegate checkOverlapDelegate) {
            CheckOverlap = checkOverlapDelegate ?? throw new ArgumentNullException(nameof(checkOverlapDelegate));
        }

        public CheckOverlapDelegate CheckOverlap { get; }
    }
}