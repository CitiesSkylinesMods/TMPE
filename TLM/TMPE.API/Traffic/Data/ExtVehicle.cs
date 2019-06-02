using TrafficManager.Traffic.Enums;
using UnityEngine;

namespace TrafficManager.Traffic.Data {
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

		public override string ToString() {
			return $"[VehicleState\n" +
				"\t" + $"vehicleId = {vehicleId}\n" +
				"\t" + $"lastPathId = {lastPathId}\n" +
				"\t" + $"lastPathPositionIndex = {lastPathPositionIndex}\n" +
				"\t" + $"junctionTransitState = {junctionTransitState}\n" +
				"\t" + $"lastTransitStateUpdate = {lastTransitStateUpdate}\n" +
				"\t" + $"lastPositionUpdate = {lastPositionUpdate}\n" +
				"\t" + $"totalLength = {totalLength}\n" +
				"\t" + $"waitTime = {waitTime}\n" +
				"\t" + $"flags = {flags}\n" +
				"\t" + $"vehicleType = {vehicleType}\n" +
				"\t" + $"heavyVehicle = {heavyVehicle}\n" +
				"\t" + $"recklessDriver = {recklessDriver}\n" +
				"\t" + $"currentSegmentId = {currentSegmentId}\n" +
				"\t" + $"currentStartNode = {currentStartNode}\n" +
				"\t" + $"currentLaneIndex = {currentLaneIndex}\n" +
				"\t" + $"nextSegmentId = {nextSegmentId}\n" +
				"\t" + $"nextLaneIndex = {nextLaneIndex}\n" +
				"\t" + $"previousVehicleIdOnSegment = {previousVehicleIdOnSegment}\n" +
				"\t" + $"nextVehicleIdOnSegment = {nextVehicleIdOnSegment}\n" +
				"\t" + $"lastAltLaneSelSegmentId = {lastAltLaneSelSegmentId}\n" +
				"\t" + $"junctionTransitState = {junctionTransitState}\n" +
				"\t" + $"timedRand = {timedRand}\n" +
				"\t" + $"dlsReady = {dlsReady}\n" +
				"\t" + $"maxReservedSpace = {maxReservedSpace}\n" +
				"\t" + $"laneSpeedRandInterval = {laneSpeedRandInterval}\n" +
				"\t" + $"maxOptLaneChanges = {maxOptLaneChanges}\n" +
				"\t" + $"maxUnsafeSpeedDiff = {maxUnsafeSpeedDiff}\n" +
				"\t" + $"minSafeSpeedImprovement = {minSafeSpeedImprovement}\n" +
				"\t" + $"minSafeTrafficImprovement = {minSafeTrafficImprovement}\n" +
				"VehicleState]";
		}

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
	}
}
