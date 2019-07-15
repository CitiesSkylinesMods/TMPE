using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic.Enums;
using UnityEngine;

namespace TrafficManager.Traffic.Data {
	using API.Traffic.Enums;

	public struct ExtCitizenInstance {
		public ushort instanceId;

		/// <summary>
		/// Citizen path mode (used for Parking AI)
		/// </summary>
		public ExtPathMode pathMode;

		/// <summary>
		/// Number of times a formerly found parking space is already occupied after reaching its position
		/// </summary>
		public int failedParkingAttempts;

		/// <summary>
		/// Segment id / Building id where a parking space has been found
		/// </summary>
		public ushort parkingSpaceLocationId;

		/// <summary>
		/// Type of object (segment/building) where a parking space has been found
		/// </summary>
		public ExtParkingSpaceLocation parkingSpaceLocation;

		/// <summary>
		/// Path position that is used as a start position when parking fails
		/// </summary>
		public PathUnit.Position? parkingPathStartPosition;

		/// <summary>
		/// Walking path from (alternative) parking spot to target (only used to check if there is a valid walking path, not actually used at the moment)
		/// </summary>
		public uint returnPathId;

		/// <summary>
		/// State of the return path
		/// </summary>
		public ExtPathState returnPathState;

		/// <summary>
		/// Last known distance to the citizen's parked car
		/// </summary>
		public float lastDistanceToParkedCar;

		/// <summary>
		/// Specifies whether the last path-finding started at an outside connection
		/// </summary>
		public bool atOutsideConnection;

		public override string ToString() {
			return $"[ExtCitizenInstance\n" +
				"\t" + $"instanceId = {instanceId}\n" +
				"\t" + $"pathMode = {pathMode}\n" +
				"\t" + $"failedParkingAttempts = {failedParkingAttempts}\n" +
				"\t" + $"parkingSpaceLocationId = {parkingSpaceLocationId}\n" +
				"\t" + $"parkingSpaceLocation = {parkingSpaceLocation}\n" +
				"\t" + $"parkingPathStartPosition = {parkingPathStartPosition}\n" +
				"\t" + $"returnPathId = {returnPathId}\n" +
				"\t" + $"returnPathState = {returnPathState}\n" +
				"\t" + $"lastDistanceToParkedCar = {lastDistanceToParkedCar}\n" +
				"\t" + $"atOutsideConnection = {atOutsideConnection}\n" +
				"ExtCitizenInstance]";
		}

		public ExtCitizenInstance(ushort instanceId) {
			this.instanceId = instanceId;
			pathMode = ExtPathMode.None;
			failedParkingAttempts = 0;
			parkingSpaceLocationId = 0;
			parkingSpaceLocation = ExtParkingSpaceLocation.None;
			parkingPathStartPosition = null;
			returnPathId = 0;
			returnPathState = ExtPathState.None;
			lastDistanceToParkedCar = 0;
			atOutsideConnection = false;
		}

		/// <summary>
		/// Determines the path type through evaluating the current path mode.
		/// </summary>
		/// <returns></returns>
		public ExtPathType GetPathType() {
			switch (pathMode) {
				case ExtPathMode.CalculatingCarPathToAltParkPos:
				case ExtPathMode.CalculatingCarPathToKnownParkPos:
				case ExtPathMode.CalculatingCarPathToTarget:
				case ExtPathMode.DrivingToAltParkPos:
				case ExtPathMode.DrivingToKnownParkPos:
				case ExtPathMode.DrivingToTarget:
				case ExtPathMode.RequiresCarPath:
				case ExtPathMode.RequiresMixedCarPathToTarget:
				case ExtPathMode.ParkingFailed:
					return ExtPathType.DrivingOnly;
				case ExtPathMode.CalculatingWalkingPathToParkedCar:
				case ExtPathMode.CalculatingWalkingPathToTarget:
				case ExtPathMode.RequiresWalkingPathToParkedCar:
				case ExtPathMode.RequiresWalkingPathToTarget:
				case ExtPathMode.ApproachingParkedCar:
				case ExtPathMode.WalkingToParkedCar:
				case ExtPathMode.WalkingToTarget:
					return ExtPathType.WalkingOnly;
				default:
					return ExtPathType.None;
			}
		}

		/// <summary>
		/// Converts an ExtPathState to a ExtSoftPathState.
		/// </summary>
		/// <param name="state"></param>
		/// <returns></returns>
		public static ExtSoftPathState ConvertPathStateToSoftPathState(ExtPathState state) {
			return (ExtSoftPathState)((int)state);
		}
	}
}
