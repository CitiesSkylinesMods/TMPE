using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using UnityEngine;

namespace TrafficManager.Traffic {
	public class ExtCitizenInstance {
		public ushort InstanceId { get; private set; }

		public enum ExtPathState {
			/// <summary>
			/// No path
			/// </summary>
			None,
			/// <summary>
			/// Path is currently being calculated
			/// </summary>
			Calculating,
			/// <summary>
			/// Path-finding has succeeded
			/// </summary>
			Ready,
			/// <summary>
			/// Path-finding has failed
			/// </summary>
			Failed
		}

		public enum ExtPathType {
			/// <summary>
			/// Mixed path
			/// </summary>
			None,
			/// <summary>
			/// Walking path
			/// </summary>
			WalkingOnly,
			/// <summary>
			/// Driving path
			/// </summary>
			DrivingOnly
		}

		public enum ExtPathMode {
			None,
			/// <summary>
			/// Indicates that the citizen requires a walking path to their parked car
			/// </summary>
			RequiresWalkingPathToParkedCar,
			/// <summary>
			/// Indicates that a walking path to the parked car is being calculated
			/// </summary>
			CalculatingWalkingPathToParkedCar,
			/// <summary>
			/// Indicates that the citizen is walking to their parked car
			/// </summary>
			WalkingToParkedCar,
			/// <summary>
			/// Indicates that the citizen is close to their parked car
			/// </summary>
			ReachingParkedCar,
			/// <summary>
			/// Indicates that the citizen has reached their parked car
			/// </summary>
			ParkedCarReached,
			/// <summary>
			/// Indicates that a direct car path to the target is being calculated
			/// </summary>
			CalculatingCarPathToTarget,
			/// <summary>
			/// Indicates that a car path to a known parking spot near the target is being calculated
			/// </summary>
			CalculatingCarPathToKnownParkPos,
			/// <summary>
			/// Indicates that the citizen is currently driving on a direct path to target
			/// </summary>
			DrivingToTarget,
			/// <summary>
			/// Indiciates that the citizen is currently driving to a known parking spot near the target
			/// </summary>
			DrivingToKnownParkPos,
			/// <summary>
			/// Indicates that the vehicle is being parked on an alternative parking position
			/// </summary>
			ParkingSucceeded,
			/// <summary>
			/// Indicates that parking has failed
			/// </summary>
			ParkingFailed,
			/// <summary>
			/// Indicates that a path to an alternative parking position is being calculated
			/// </summary>
			CalculatingCarPathToAltParkPos,
			/// <summary>
			/// Indicates that the vehicle is on a path to an alternative parking position
			/// </summary>
			DrivingToAltParkPos,
			/// <summary>
			/// Indicates that a walking path to target is being calculated
			/// </summary>
			CalculatingWalkingPathToTarget,
			/// <summary>
			/// Indicates that the citizen is currently walking to the target
			/// </summary>
			WalkingToTarget,
			/// <summary>
			/// Indicates that the citizen is using public transport (bus/train/tram/subway) to reach the target
			/// </summary>
			PublicTransportToTarget,
			/// <summary>
			/// Indicates that the citizen is using a taxi to reach the target
			/// </summary>
			TaxiToTarget
		}

		public enum ExtParkingSpaceLocation {
			/// <summary>
			/// No parking space location
			/// </summary>
			None,
			/// <summary>
			/// Road-side parking space
			/// </summary>
			RoadSide,
			/// <summary>
			/// Building parking space
			/// </summary>
			Building
		}

		/// <summary>
		/// Citizen path mode (used for Parking AI)
		/// </summary>
		public ExtPathMode PathMode {
			get {
				return pathMode;
			}
			internal set {
#if DEBUG
				/*if (GlobalConfig.Instance.DebugSwitches[7]) {
					Log.Warning($"Changing PathMode for citizen instance {InstanceId}: {pathMode} -> {value}");
				}*/
#endif
				pathMode = value;
			}
		}
		private ExtPathMode pathMode;

		/// <summary>
		/// Number of times a formerly found parking space is already occupied after reaching its position
		/// </summary>
		public int FailedParkingAttempts {
			get; internal set;
		}

		/// <summary>
		/// Segment id / Building id where a parking space has been found
		/// </summary>
		public ushort ParkingSpaceLocationId {
			get; internal set;
		}

		/// <summary>
		/// Type of object (segment/building) where a parking space has been found
		/// </summary>
		public ExtParkingSpaceLocation ParkingSpaceLocation {
			get; internal set;
		}

		/// <summary>
		/// Path position that is used as a start position when parking fails
		/// </summary>
		public PathUnit.Position? ParkingPathStartPosition {
			get; internal set;
		}

		/// <summary>
		/// Walking path from (alternative) parking spot to target (only used to check if there is a valid walking path, not actually used at the moment)
		/// </summary>
		public uint ReturnPathId {
			get; internal set;
		}

		/// <summary>
		/// State of the return path
		/// </summary>
		public ExtPathState ReturnPathState {
			get; internal set;
		}

		public float LastDistanceToParkedCar {
			get; internal set;
		}

		public override string ToString() {
			return $"[ExtCitizenInstance\n" +
				"\t" + $"InstanceId = {InstanceId}\n" +
				"\t" + $"PathMode = {PathMode}\n" +
				"\t" + $"FailedParkingAttempts = {FailedParkingAttempts}\n" +
				"\t" + $"ParkingSpaceLocationId = {ParkingSpaceLocationId}\n" +
				"\t" + $"ParkingSpaceLocation = {ParkingSpaceLocation}\n" +
				"\t" + $"ParkingPathStartPosition = {ParkingPathStartPosition}\n" +
				"\t" + $"ReturnPathId = {ReturnPathId}\n" +
				"\t" + $"ReturnPathState = {ReturnPathState}\n" +
				"\t" + $"LastDistanceToParkedCar = {LastDistanceToParkedCar}\n" +
				"ExtCitizenInstance]";
		}

		internal ExtCitizenInstance(ushort instanceId) {
			this.InstanceId = instanceId;
			Reset();
		}

		private ExtCitizenInstance() {

		}

		internal bool IsValid() {
			return Constants.ServiceFactory.CitizenService.IsCitizenInstanceValid(InstanceId);
		}

		public uint GetCitizenId() {
			return Singleton<CitizenManager>.instance.m_instances.m_buffer[InstanceId].m_citizen;
		}

		internal void Reset() {
#if DEBUG
			/*if (GlobalConfig.Instance.DebugSwitches[7]) {
				Log.Warning($"Resetting PathMode for citizen instance {InstanceId}");
			}*/
#endif
			//Flags = ExtFlags.None;
			PathMode = ExtPathMode.None;
			FailedParkingAttempts = 0;
			ParkingSpaceLocation = ExtParkingSpaceLocation.None;
			ParkingSpaceLocationId = 0;
			LastDistanceToParkedCar = float.MaxValue;
			//ParkedVehiclePosition = default(Vector3);
			ReleaseReturnPath();
		}

		/// <summary>
		/// Releases the return path
		/// </summary>
		internal void ReleaseReturnPath() {
			if (ReturnPathId != 0) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"Releasing return path {ReturnPathId} of citizen instance {InstanceId}. ReturnPathState={ReturnPathState}");
#endif

				CustomPathManager._instance.ReleasePath(ReturnPathId);
				ReturnPathId = 0;
			}
			ReturnPathState = ExtPathState.None;
		}

		/// <summary>
		/// Checks the calculation state of the return path
		/// </summary>
		internal void UpdateReturnPathState() {
			if (ReturnPathId != 0 && ReturnPathState == ExtPathState.Calculating) {
				byte returnPathFlags = CustomPathManager._instance.m_pathUnits.m_buffer[ReturnPathId].m_pathFindFlags;
				if ((returnPathFlags & PathUnit.FLAG_READY) != 0) {
					ReturnPathState = ExtPathState.Ready;
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4])
						Log._Debug($"CustomHumanAI.CustomSimulationStep: Return path {ReturnPathId} SUCCEEDED. Flags={returnPathFlags}. Setting ReturnPathState={ReturnPathState}");
#endif
				} else if ((returnPathFlags & PathUnit.FLAG_FAILED) != 0) {
					ReturnPathState = ExtPathState.Failed;
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"CustomHumanAI.CustomSimulationStep: Return path {ReturnPathId} FAILED. Flags={returnPathFlags}. Setting ReturnPathState={ReturnPathState}");
#endif
				}
			}
		}

		/// <summary>
		/// Starts path-finding of the walking path from parking position <paramref name="parkPos"/> to target position <paramref name="targetPos"/>.
		/// </summary>
		/// <param name="parkPos">Parking position</param>
		/// <param name="targetPos">Target position</param>
		/// <returns></returns>
		internal bool CalculateReturnPath(Vector3 parkPos, Vector3 targetPos) {
			ReleaseReturnPath();

			PathUnit.Position parkPathPos;
			PathUnit.Position targetPathPos;
			if (CustomPathManager.FindPathPositionWithSpiralLoop(parkPos, ItemClass.Service.Road, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, false, false, GlobalConfig.Instance.MaxBuildingToPedestrianLaneDistance, out parkPathPos) &&
				CustomPathManager.FindPathPositionWithSpiralLoop(targetPos, ItemClass.Service.Road, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, false, false, GlobalConfig.Instance.MaxBuildingToPedestrianLaneDistance, out targetPathPos)) {

				PathUnit.Position dummyPathPos = default(PathUnit.Position);
				uint pathId;

				if (CustomPathManager._instance.CreatePath(ExtVehicleType.None, 0, ExtCitizenInstance.ExtPathType.WalkingOnly, out pathId, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, parkPathPos, dummyPathPos, targetPathPos, dummyPathPos, dummyPathPos, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, 20000f, false, false, false, false, false, false)) {
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"ExtCitizenInstance.CalculateReturnPath: Path-finding starts for return path of citizen instance {InstanceId}, path={pathId}, parkPathPos.segment={parkPathPos.m_segment}, parkPathPos.lane={parkPathPos.m_lane}, targetPathPos.segment={targetPathPos.m_segment}, targetPathPos.lane={targetPathPos.m_lane}");
#endif

					ReturnPathId = pathId;
					ReturnPathState = ExtPathState.Calculating;
					return true;
				}
			}

#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2])
				Log._Debug($"ExtCitizenInstance.CalculateReturnPath: Could not find path position(s) for either the parking position or target position of citizen instance {InstanceId}.");
#endif

			return false;
		}

		/// <summary>
		/// Determines the path type through evaluating the current path mode.
		/// </summary>
		/// <returns></returns>
		internal ExtPathType GetPathType() {
			switch (PathMode) {
				case ExtPathMode.CalculatingCarPathToAltParkPos:
				case ExtPathMode.CalculatingCarPathToKnownParkPos:
				case ExtPathMode.CalculatingCarPathToTarget:
				case ExtPathMode.DrivingToAltParkPos:
				case ExtPathMode.DrivingToKnownParkPos:
				case ExtPathMode.DrivingToTarget:
					return ExtPathType.DrivingOnly;
				case ExtPathMode.CalculatingWalkingPathToParkedCar:
				case ExtPathMode.CalculatingWalkingPathToTarget:
				case ExtPathMode.RequiresWalkingPathToParkedCar:
				case ExtPathMode.WalkingToParkedCar:
				case ExtPathMode.WalkingToTarget:
					return ExtPathType.WalkingOnly;
				default:
					return ExtPathType.None;
			}
		}
	}
}
