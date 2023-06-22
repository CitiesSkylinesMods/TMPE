namespace TrafficManager.API.Traffic.Data {
    using System.Runtime.InteropServices;
    using TrafficManager.API.Traffic.Enums;

    [StructLayout(LayoutKind.Auto)]
    public struct ExtVehicle {
        public ushort vehicleId;
        public uint lastPathId;
        public byte lastPathPositionIndex;
        public uint lastTransitStateUpdate;
        public uint lastPositionUpdate;
        public float totalLength;
        public int waitTime;
        public ExtVehicleFlags flags;
        public ExtVehicleType vehicleType;
        public bool heavyVehicle;
        public bool recklessDriver;
        public ushort currentSegmentId;
        public bool currentStartNode;
        public byte currentLaneIndex;
        public ushort nextSegmentId;
        public byte nextLaneIndex;
        public ushort previousVehicleIdOnSegment;
        public ushort nextVehicleIdOnSegment;
        public ushort lastAltLaneSelSegmentId;
        public byte timedRand;
        public VehicleJunctionTransitState junctionTransitState;
        // Dynamic Lane Selection
        public bool dlsReady;
        public float maxReservedSpace;
        public float laneSpeedRandInterval;
        public int maxOptLaneChanges;
        public float maxUnsafeSpeedDiff;
        public float minSafeSpeedImprovement;
        public float minSafeTrafficImprovement;

        public ExtVehicle(ushort vehicleId) {
            this.vehicleId = vehicleId;
            lastPathId = 0;
            lastPathPositionIndex = 0;
            lastTransitStateUpdate = 0;
            lastPositionUpdate = 0;
            totalLength = 0;
            waitTime = 0;
            flags = ExtVehicleFlags.None;
            vehicleType = ExtVehicleType.None;
            heavyVehicle = false;
            recklessDriver = false;
            currentSegmentId = 0;
            currentStartNode = false;
            currentLaneIndex = 0;
            nextSegmentId = 0;
            nextLaneIndex = 0;
            previousVehicleIdOnSegment = 0;
            nextVehicleIdOnSegment = 0;
            lastAltLaneSelSegmentId = 0;
            junctionTransitState = VehicleJunctionTransitState.None;
            timedRand = 0;
            dlsReady = false;
            maxReservedSpace = 0;
            laneSpeedRandInterval = 0;
            maxOptLaneChanges = 0;
            maxUnsafeSpeedDiff = 0;
            minSafeSpeedImprovement = 0;
            minSafeTrafficImprovement = 0;
        }

        public override string ToString() {
            return string.Format(
                "[VehicleState\n\tvehicleId = {0}\n\tlastPathId = {1}\n" +
                "\tlastPathPositionIndex = {2}\n\tjunctionTransitState = {3}\n" +
                "\tlastTransitStateUpdate = {4}\n\tlastPositionUpdate = {5}\n\ttotalLength = {6}\n" +
                "\twaitTime = {7}\n\tflags = {8}\n\tvehicleType = {9}\n\theavyVehicle = {10}\n" +
                "\trecklessDriver = {11}\n\tcurrentSegmentId = {12}\n\tcurrentStartNode = {13}\n" +
                "\tcurrentLaneIndex = {14}\n\tnextSegmentId = {15}\n\tnextLaneIndex = {16}\n" +
                "\tpreviousVehicleIdOnSegment = {17}\n\tnextVehicleIdOnSegment = {18}\n\t" +
                "lastAltLaneSelSegmentId = {19}\n\tjunctionTransitState = {20}\n" +
                "\ttimedRand = {21}\n\tdlsReady = {22}\n\tmaxReservedSpace = {23}\n" +
                "\tlaneSpeedRandInterval = {24}\n\tmaxOptLaneChanges = {25}\n" +
                "\tmaxUnsafeSpeedDiff = {26}\n\tminSafeSpeedImprovement = {27}\n" +
                "\tminSafeTrafficImprovement = {28}\nVehicleState]",
                vehicleId,
                lastPathId,
                lastPathPositionIndex,
                junctionTransitState,
                lastTransitStateUpdate,
                lastPositionUpdate,
                totalLength,
                waitTime,
                flags,
                vehicleType,
                heavyVehicle,
                recklessDriver,
                currentSegmentId,
                currentStartNode,
                currentLaneIndex,
                nextSegmentId,
                nextLaneIndex,
                previousVehicleIdOnSegment,
                nextVehicleIdOnSegment,
                lastAltLaneSelSegmentId,
                junctionTransitState,
                timedRand,
                dlsReady,
                maxReservedSpace,
                laneSpeedRandInterval,
                maxOptLaneChanges,
                maxUnsafeSpeedDiff,
                minSafeSpeedImprovement,
                minSafeTrafficImprovement);
        }
    }
}