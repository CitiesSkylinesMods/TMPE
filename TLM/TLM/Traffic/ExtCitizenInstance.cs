using ColossalFramework;
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
			/// 
			/// </summary>
			None,
			/// <summary>
			/// 
			/// </summary>
			Calculating,
			/// <summary>
			/// 
			/// </summary>
			Ready,
			/// <summary>
			/// 
			/// </summary>
			Failed
		}

		public enum ExtPathType {
			None,
			WalkingOnly,
			DrivingOnly
		}

		public enum ExtPathMode {
			None,
			/// <summary>
			/// 
			/// </summary>
			RequiresWalkingPathToParkedCar,
			/// <summary>
			/// 
			/// </summary>
			CalculatingWalkingPathToParkedCar,
			/// <summary>
			/// 
			/// </summary>
			WalkingToParkedCar,
			/// <summary>
			/// 
			/// </summary>
			ReachingParkedCar,
			/// <summary>
			/// 
			/// </summary>
			ParkedCarReached,
			/// <summary>
			/// 
			/// </summary>
			CalculatingCarPathToTarget,
			/// <summary>
			/// 
			/// </summary>
			CalculatingCarPathToKnownParkPos,
			/// <summary>
			/// 
			/// </summary>
			DrivingToTarget,
			/// <summary>
			/// 
			/// </summary>
			DrivingToKnownParkPos,
			/// <summary>
			/// Indicates that the vehicle is being parked on an alternative parking position
			/// </summary>
			ParkingSucceeded,
			/// <summary>
			/// Indicates that parking failed
			/// </summary>
			ParkingFailed,
			/// <summary>
			/// Indicates that a path to an alternative parking position is currently being calculated
			/// </summary>
			CalculatingCarPathToAltParkPos,
			/// <summary>
			/// Indicates that the vehicle is on a path to an alternative parking position
			/// </summary>
			DrivingToAltParkPos,
			/// <summary>
			/// 
			/// </summary>
			CalculatingWalkingPathToTarget,
			/// <summary>
			/// 
			/// </summary>
			WalkingToTarget,
			/// <summary>
			/// 
			/// </summary>
			PublicTransportToTarget
		}

		public enum ExtParkingSpaceLocation {
			None,
			RoadSide,
			Building
		}

		/// <summary>
		/// 
		/// </summary>
		public ExtPathMode PathMode {
			get; internal set;
		}

		/// <summary>
		/// 
		/// </summary>
		public int FailedParkingAttempts {
			get; internal set;
		}

		/// <summary>
		/// 
		/// </summary>
		public ushort ParkingSpaceLocationId {
			get; internal set;
		}

		/// <summary>
		/// 
		/// </summary>
		public ExtParkingSpaceLocation ParkingSpaceLocation {
			get; internal set;
		}

		/// <summary>
		/// 
		/// </summary>
		public PathUnit.Position? ParkingPathStartPosition {
			get; internal set;
		}

		/// <summary>
		/// 
		/// </summary>
		/*public Vector3 ParkedVehiclePosition {
			get; internal set;
		}*/

		/// <summary>
		/// 
		/// </summary>
		public uint ReturnPathId {
			get; internal set;
		}

		/// <summary>
		/// 
		/// </summary>
		public ExtPathState ReturnPathState {
			get; internal set;
		}

		public ExtCitizenInstance(ushort instanceId) {
			this.InstanceId = instanceId;
			Reset();
		}

		public uint GetCitizenId() {
			return Singleton<CitizenManager>.instance.m_instances.m_buffer[InstanceId].m_citizen;
		}

		internal void Reset() {
			//Flags = ExtFlags.None;
			PathMode = ExtPathMode.None;
			FailedParkingAttempts = 0;
			ParkingSpaceLocation = ExtParkingSpaceLocation.None;
			ParkingSpaceLocationId = 0;
			//ParkedVehiclePosition = default(Vector3);
			ReleaseReturnPath();
		}

		internal void ReleaseReturnPath() {
			if (ReturnPathId != 0) {
#if DEBUG
				if (GlobalConfig.Instance().DebugSwitches[2])
					Log._Debug($"Releasing return path {ReturnPathId} of citizen instance {InstanceId}. ReturnPathState={ReturnPathState}");
#endif

				CustomPathManager._instance.ReleasePath(ReturnPathId);
				ReturnPathId = 0;
			}
			ReturnPathState = ExtPathState.None;
		}

		internal void UpdateReturnPathState() {
			if (ReturnPathId != 0 && ReturnPathState == ExtPathState.Calculating) {
				byte returnPathFlags = CustomPathManager._instance.m_pathUnits.m_buffer[ReturnPathId].m_pathFindFlags;
				if ((returnPathFlags & PathUnit.FLAG_READY) != 0) {
					ReturnPathState = ExtPathState.Ready;
#if DEBUG
					if (GlobalConfig.Instance().DebugSwitches[2])
						Log._Debug($"CustomHumanAI.CustomSimulationStep: Return path {ReturnPathId} SUCCEEDED. Flags={returnPathFlags}. Setting ReturnPathState={ReturnPathState}");
#endif
				} else if ((returnPathFlags & PathUnit.FLAG_FAILED) != 0) {
					ReturnPathState = ExtPathState.Failed;
#if DEBUG
					if (GlobalConfig.Instance().DebugSwitches[1])
						Log._Debug($"CustomHumanAI.CustomSimulationStep: Return path {ReturnPathId} FAILED. Flags={returnPathFlags}. Setting ReturnPathState={ReturnPathState}");
#endif
				}
			}
		}

		internal bool CalculateReturnPath(Vector3 parkPos, Vector3 targetPos) {
			ReleaseReturnPath();

			PathUnit.Position parkPathPos;
			PathUnit.Position targetPathPos;
			if (CustomPathManager.FindPathPositionWithSpiralLoop(parkPos, ItemClass.Service.Road, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, false, false, GlobalConfig.Instance().MaxBuildingToPedestrianLaneDistance, out parkPathPos) &&
				CustomPathManager.FindPathPositionWithSpiralLoop(targetPos, ItemClass.Service.Road, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, false, false, GlobalConfig.Instance().MaxBuildingToPedestrianLaneDistance, out targetPathPos)) {

				PathUnit.Position dummyPathPos = default(PathUnit.Position);
				uint pathId;

				if (CustomPathManager._instance.CreatePath(false, ExtVehicleType.None, 0, ExtCitizenInstance.ExtPathType.WalkingOnly, out pathId, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, parkPathPos, dummyPathPos, targetPathPos, dummyPathPos, dummyPathPos, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, 20000f, false, false, false, false, false)) {
#if DEBUG
					if (GlobalConfig.Instance().DebugSwitches[2])
						Log._Debug($"ExtCitizenInstance.CalculateReturnPath: Path-finding starts for return path of citizen instance {InstanceId}, path={pathId}, parkPathPos.segment={parkPathPos.m_segment}, parkPathPos.lane={parkPathPos.m_lane}, targetPathPos.segment={targetPathPos.m_segment}, targetPathPos.lane={targetPathPos.m_lane}");
#endif

					ReturnPathId = pathId;
					ReturnPathState = ExtPathState.Calculating;
					return true;
				}
			}

#if DEBUG
			if (GlobalConfig.Instance().DebugSwitches[1])
				Log._Debug($"ExtCitizenInstance.CalculateReturnPath: Could not find path position(s) for either the parking position or target position of citizen instance {InstanceId}.");
#endif

			return false;
		}

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
