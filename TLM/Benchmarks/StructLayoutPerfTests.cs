namespace Benchmarks {
    using System;
    using System.Runtime.InteropServices;
    using BenchmarkDotNet.Attributes;
    using TrafficManager.API.Traffic.Enums;

    [StructLayout(LayoutKind.Sequential)]
    public struct ExtBuildingSequential {
        public ushort buildingId;
        public byte parkingSpaceDemand;
        public byte incomingPublicTransportDemand;
        public byte outgoingPublicTransportDemand;
    }

    [StructLayout(LayoutKind.Auto)]
    public struct ExtBuildingAuto {
        public ushort buildingId;
        public byte parkingSpaceDemand;
        public byte incomingPublicTransportDemand;
        public byte outgoingPublicTransportDemand;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ExtCitizenSequentialInt {
        public uint citizenId;
        public ExtTransportModeInt transportMode;
        public ExtTransportModeInt lastTransportMode;
        public Citizen.Location lastLocation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ExtCitizenSequentialByte {
        public uint citizenId;
        public ExtTransportModeByte transportMode;
        public ExtTransportModeByte lastTransportMode;
        public Citizen.Location lastLocation;
    }

    [StructLayout(LayoutKind.Auto)]
    public struct ExtCitizenAutoInt {
        public uint citizenId;
        public ExtTransportModeInt transportMode;
        public ExtTransportModeInt lastTransportMode;
        public Citizen.Location lastLocation;
    }

    [StructLayout(LayoutKind.Auto)]
    public struct ExtCitizenAutoByte {
        public uint citizenId;
        public ExtTransportModeByte transportMode;
        public ExtTransportModeByte lastTransportMode;
        public Citizen.Location lastLocation;
    }

    [Flags]
    public enum ExtTransportModeInt {
        None = 0,
        Car = 1,
        PublicTransport = 2,
    }

    [Flags]
    public enum ExtTransportModeByte : byte {
        None = 0,
        Car = 1,
        PublicTransport = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ExtVehicleSequential {
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
        public bool dlsReady;
        public float maxReservedSpace;
        public float laneSpeedRandInterval;
        public int maxOptLaneChanges;
        public float maxUnsafeSpeedDiff;
        public float minSafeSpeedImprovement;
        public float minSafeTrafficImprovement;
    }

    [StructLayout(LayoutKind.Auto)]
    public struct ExtVehicleAuto {
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
        public bool dlsReady;
        public float maxReservedSpace;
        public float laneSpeedRandInterval;
        public int maxOptLaneChanges;
        public float maxUnsafeSpeedDiff;
        public float minSafeSpeedImprovement;
        public float minSafeTrafficImprovement;
    }

    public class StructLayoutPerfTests {
        private const int ITERATIONS = 100_000;
        private ExtBuildingSequential[] _extBuildingSequential;
        private ExtBuildingAuto[] _extBuildingAuto;
        private ExtCitizenSequentialInt[] _extCitizenSequentialInt;
        private ExtCitizenSequentialByte[] _extCitizenSequentialByte;
        private ExtCitizenAutoInt[] _extCitizenAutoInt;
        private ExtCitizenAutoByte[] _extCitizenAutoByte;
        private ExtVehicleSequential[] _extVehicleSequential;
        private ExtVehicleAuto[] _extVehicleAuto;

        [GlobalSetup]
        public void GlobalSetup() {
            _extBuildingSequential = new ExtBuildingSequential[ITERATIONS];
            _extBuildingAuto = new ExtBuildingAuto[ITERATIONS];
            _extCitizenSequentialInt = new ExtCitizenSequentialInt[ITERATIONS];
            _extCitizenSequentialByte = new ExtCitizenSequentialByte[ITERATIONS];
            _extCitizenAutoInt = new ExtCitizenAutoInt[ITERATIONS];
            _extCitizenAutoByte = new ExtCitizenAutoByte[ITERATIONS];
            _extVehicleSequential = new ExtVehicleSequential[ITERATIONS];
            _extVehicleAuto = new ExtVehicleAuto[ITERATIONS];
        }

        [Benchmark]
        public void ExtBuildingSequentialTest() {
            for (int i = 0; i < _extBuildingSequential.Length; i++) {
                _extBuildingSequential[i].parkingSpaceDemand = 0;
            }
        }

        [Benchmark]
        public void ExtBuildingAutoTest() {
            for (int i = 0; i < _extBuildingAuto.Length; i++) {
                _extBuildingAuto[i].parkingSpaceDemand = 0;
            }
        }

        [Benchmark]
        public void ExtCitizenSequentialIntTest() {
            for (int i = 0; i < _extCitizenSequentialInt.Length; i++) {
                _extCitizenSequentialInt[i].lastTransportMode = ExtTransportModeInt.Car;
            }
        }

        [Benchmark]
        public void ExtCitizenSequentialByteTest() {
            for (int i = 0; i < _extCitizenSequentialByte.Length; i++) {
                _extCitizenSequentialByte[i].lastTransportMode = ExtTransportModeByte.Car;
            }
        }

        [Benchmark]
        public void ExtCitizenAutoIntTest() {
            for (int i = 0; i < _extCitizenAutoInt.Length; i++) {
                _extCitizenAutoInt[i].lastTransportMode = ExtTransportModeInt.Car;
            }
        }

        [Benchmark]
        public void ExtCitizenAutoByteTest() {
            for (int i = 0; i < _extCitizenAutoByte.Length; i++) {
                _extCitizenAutoByte[i].lastTransportMode = ExtTransportModeByte.Car;
            }
        }

        [Benchmark]
        public void ExtVehicleSequentialTest() {
            for (int i = 0; i < _extVehicleSequential.Length; i++) {
                _extVehicleSequential[i].lastPathId = 0;
            }
        }

        [Benchmark]
        public void ExtVehicleAutoTest() {
            for (int i = 0; i < _extVehicleAuto.Length; i++) {
                _extVehicleAuto[i].lastPathId = 0;
            }
        }
    }
}