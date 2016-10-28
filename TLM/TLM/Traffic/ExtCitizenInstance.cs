using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager.Traffic {
	public class ExtCitizenInstance {
		public ushort InstanceId { get; private set; }

		/*[Flags]
		public enum ExtFlags {
			None,
			CannotUsePassengerCar
		}*/

		public enum PathMode {
			None,
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
			ParkedCarReached,
			/// <summary>
			/// 
			/// </summary>
			CalculatingCarPath,
			/// <summary>
			/// 
			/// </summary>
			CalculatingKnownCarPath,
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
			CalculatingPathToAltParkPos,
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
			WalkingToTarget
		}

		public enum ParkingSpaceLocation {
			None,
			RoadSide,
			Building
		}

		/*public ExtFlags Flags {
			get; internal set;
		}*/

		public PathMode CurrentPathMode {
			get; internal set;
		}
		
		public int FailedParkingAttempts {
			get; internal set;
		}

		public ushort AltParkingSpaceLocationId {
			get; internal set;
		}

		public ParkingSpaceLocation AltParkingSpaceLocation {
			get; internal set;
		}

		public PathUnit.Position? AltParkingPathStartPosition {
			get; internal set;
		}

		public Vector3 ParkedVehicleTargetPosition {
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
			CurrentPathMode = PathMode.None;
			FailedParkingAttempts = 0;
			AltParkingSpaceLocation = ParkingSpaceLocation.None;
			AltParkingSpaceLocationId = 0;
			ParkedVehicleTargetPosition = default(Vector3);
		}
	}
}
