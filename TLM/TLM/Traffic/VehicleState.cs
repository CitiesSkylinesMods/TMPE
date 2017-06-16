#define USEPATHWAITCOUNTERx
#define DEBUGVSTATEx
#define DEBUGREGx

using System;
using ColossalFramework;
using UnityEngine;
using System.Collections.Generic;
using TrafficManager.TrafficLight;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using CSUtil.Commons;

namespace TrafficManager.Traffic {
	public struct VehicleState {
		//public static readonly int STATE_UPDATE_SHIFT = 4;

		public VehicleJunctionTransitState JunctionTransitState {
			get { return junctionTransitState; }
			set {
				if (value != junctionTransitState) {
					lastTransitStateUpdate = Now();
				}
				junctionTransitState = value;
			}
		}

		/*public bool Valid {
			get {
				if ((Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId].m_flags & Vehicle.Flags.Created) == 0) {
					return false;
				}
				return valid;
			}
			internal set { valid = value; }
		}*/

		public ushort vehicleId;
		public uint lastPathId;
		public byte lastPathPositionIndex;
		public uint lastTransitStateUpdate;
		public uint lastPositionUpdate;
		public float totalLength;
		public float sqrVelocity;
		public int waitTime;
		public float reduceSqrSpeedByValueToYield;
		public bool spawned;
		public ExtVehicleType vehicleType;
		public bool heavyVehicle;
		public ushort currentSegmentId;
		public bool currentStartNode;
		public byte currentLaneIndex;
		public ushort nextSegmentId;
		public byte nextLaneIndex;
		public ushort previousVehicleIdOnSegment;
		public ushort nextVehicleIdOnSegment;
		private VehicleJunctionTransitState junctionTransitState;

		public override string ToString() {
			return $"[VehicleState\n" +
				"\t" + $"vehicleId = {vehicleId}\n" +
				"\t" + $"lastPathId = {lastPathId}\n" +
				"\t" + $"lastPathPositionIndex = {lastPathPositionIndex}\n" +
				"\t" + $"JunctionTransitState = {JunctionTransitState}\n" +
				"\t" + $"lastTransitStateUpdate = {lastTransitStateUpdate}\n" +
				"\t" + $"lastPositionUpdate = {lastPositionUpdate}\n" +
				"\t" + $"totalLength = {totalLength}\n" +
				"\t" + $"sqrVelocity = {sqrVelocity}\n" +
				"\t" + $"waitTime = {waitTime}\n" +
				"\t" + $"reduceSqrSpeedByValueToYield = {reduceSqrSpeedByValueToYield}\n" +
				//"\t" + $"Valid = {Valid}\n" +
				//"\t" + $"valid = {valid}\n" +
				"\t" + $"spawned = {spawned}\n" +
				"\t" + $"vehicleType = {vehicleType}\n" +
				"\t" + $"heavyVehicle = {heavyVehicle}\n" +
				"\t" + $"currentSegmentId = {currentSegmentId}\n" +
				"\t" + $"currentStartNode = {currentStartNode}\n" +
				"\t" + $"currentLaneIndex = {currentLaneIndex}\n" +
				"\t" + $"nextSegmentId = {nextSegmentId}\n" +
				"\t" + $"nextLaneIndex = {nextLaneIndex}\n" +
				"\t" + $"previousVehicleIdOnSegment = {previousVehicleIdOnSegment}\n" +
				"\t" + $"nextVehicleIdOnSegment = {nextVehicleIdOnSegment}\n" +
				"\t" + $"junctionTransitState = {junctionTransitState}\n" +
				"VehicleState]";
		}

		internal VehicleState(ushort vehicleId) {
			this.vehicleId = vehicleId;
			lastPathId = 0;
			lastPathPositionIndex = 0;
			lastTransitStateUpdate = Now();
			lastPositionUpdate = Now();
			totalLength = 0;
			waitTime = 0;
			reduceSqrSpeedByValueToYield = 0;
			spawned = false;
			vehicleType = ExtVehicleType.None;
			heavyVehicle = false;
			currentSegmentId = 0;
			currentStartNode = false;
			currentLaneIndex = 0;
			nextSegmentId = 0;
			nextLaneIndex = 0;
			previousVehicleIdOnSegment = 0;
			nextVehicleIdOnSegment = 0;
			sqrVelocity = 0;
			junctionTransitState = VehicleJunctionTransitState.None;
		}

		/*private void Reset(bool unlink=true) { // TODO this is called in wrong places!
			if (unlink)
				Unlink();

			Valid = false;
			totalLength = 0f;
			//VehicleType = ExtVehicleType.None;
			waitTime = 0;
			JunctionTransitState = VehicleJunctionTransitState.None;
			lastStateUpdate = 0;
		}*/

		/*public ExtCitizenInstance GetDriverExtInstance() {
			ushort driverInstanceId = CustomPassengerCarAI.GetDriverInstance(vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId]);
			if (driverInstanceId != 0) {
				return ExtCitizenInstanceManager.Instance.GetExtInstance(driverInstanceId);
			}
			return null;
		}*/

		internal void Unlink() {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleState.Unlink({vehicleId}) called: Unlinking vehicle from all segment ends\nstate:{this}");
#endif

			VehicleStateManager vehStateManager = VehicleStateManager.Instance;

			lastPositionUpdate = Now();

			if (previousVehicleIdOnSegment != 0) {
				vehStateManager.VehicleStates[previousVehicleIdOnSegment].nextVehicleIdOnSegment = nextVehicleIdOnSegment;
			} else if (currentSegmentId != 0) {
				SegmentEnd curEnd = SegmentEndManager.Instance.GetSegmentEnd(currentSegmentId, currentStartNode);
				if (curEnd != null && curEnd.FirstRegisteredVehicleId == vehicleId) {
					curEnd.FirstRegisteredVehicleId = nextVehicleIdOnSegment;
				}
			}

			if (nextVehicleIdOnSegment != 0) {
				vehStateManager.VehicleStates[nextVehicleIdOnSegment].previousVehicleIdOnSegment = previousVehicleIdOnSegment;
			}

			nextVehicleIdOnSegment = 0;
			previousVehicleIdOnSegment = 0;

			currentSegmentId = 0;
			currentStartNode = false;
			currentLaneIndex = 0;

#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleState.Unlink({vehicleId}) finished: Unlinked vehicle from all segment ends\nstate:{this}");
#endif
		}

		private void Link(SegmentEnd end) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleState.Link({vehicleId}) called: Linking vehicle to segment end {end}\nstate:{this}");
#endif

			ushort oldFirstRegVehicleId = end.FirstRegisteredVehicleId;
			if (oldFirstRegVehicleId != 0) {
				VehicleStateManager.Instance.VehicleStates[oldFirstRegVehicleId].previousVehicleIdOnSegment = vehicleId;
				nextVehicleIdOnSegment = oldFirstRegVehicleId;
			}
			end.FirstRegisteredVehicleId = vehicleId;

#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleState.Link({vehicleId}) finished: Linked vehicle to segment end {end}\nstate:{this}");
#endif
		}

		internal void OnCreate(ref Vehicle vehicleData) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleState.OnCreate called for vehicle {vehicleId}: {this}");
#endif
			DetermineVehicleType(ref vehicleData);
			reduceSqrSpeedByValueToYield = UnityEngine.Random.Range(256f, 784f);
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleState.OnCreate finished for vehicle {vehicleId}: {this}");
#endif
		}

		internal void OnSpawn(ref Vehicle vehicleData) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleState.OnSpawn called for vehicle {vehicleId}: {this}");
#endif
			Unlink();

			try {
				totalLength = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId].CalculateTotalLength(vehicleId);
			} catch (Exception
#if DEBUG
			e
#endif
			) {
				totalLength = 0;
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleState.OnSpawn: Error occurred while calculating total length of vehicle {vehicleId}: {e}\nstate: {this}");
#endif
				return;
			}

			spawned = true;

#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleState.OnSpawn finished for vehicle {vehicleId}: {this}");
#endif
		}

		internal void UpdatePosition(ref Vehicle vehicleData, ref PathUnit.Position curPos, ref PathUnit.Position nextPos, bool skipCheck = false) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleState.UpdatePosition called for vehicle {vehicleId}: {this}");
#endif
			/*if (! skipCheck && ! CheckValidity(ref vehicleData)) {
				return;
			}*/

			if (nextSegmentId != nextPos.m_segment || nextLaneIndex != nextPos.m_lane) {
				nextSegmentId = nextPos.m_segment;
				nextLaneIndex = nextPos.m_lane;
			}

			bool startNode = IsTransitNodeCurStartNode(ref curPos, ref nextPos);
			SegmentEnd end = SegmentEndManager.Instance.GetSegmentEnd(curPos.m_segment, startNode);

			if (end == null) {
				Unlink();

				currentSegmentId = curPos.m_segment;
				currentStartNode = startNode;
				currentLaneIndex = curPos.m_lane;

				JunctionTransitState = VehicleJunctionTransitState.None;
				this.sqrVelocity = 0;
				return;
			}

			if (currentSegmentId != end.SegmentId || currentStartNode != end.StartNode || currentLaneIndex != curPos.m_lane) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleState.UpdatePosition({vehicleId}): Current segment end changed. seg. {currentSegmentId}, start {currentStartNode}, lane {currentLaneIndex} -> seg. {end.SegmentId}, start {end.StartNode}, lane {curPos.m_lane}");
#endif
				if (currentSegmentId != 0) {
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[9])
						Log._Debug($"VehicleState.UpdatePosition({vehicleId}): Unlinking from current segment end");
#endif
					Unlink();
				}

				currentSegmentId = curPos.m_segment;
				currentStartNode = startNode;
				currentLaneIndex = curPos.m_lane;

				waitTime = 0;
				if (end != null) {
#if DEBUGVSTATE
					if (GlobalConfig.Instance.DebugSwitches[9])
						Log._Debug($"VehicleState.UpdatePosition({vehicleId}): Linking vehicle to segment end {end.SegmentId} @ {end.StartNode} ({end.NodeId}). Current position: Seg. {curPos.m_segment}, lane {curPos.m_lane}, offset {curPos.m_offset} / Next position: Seg. {nextPos.m_segment}, lane {nextPos.m_lane}, offset {nextPos.m_offset}");
#endif
					Link(end);
					JunctionTransitState = VehicleJunctionTransitState.Approach;
				} else {
					JunctionTransitState = VehicleJunctionTransitState.None;
				}
			}
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleState.UpdatePosition finshed for vehicle {vehicleId}: {this}");
#endif
		}

		internal void OnDespawn() {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleState.OnDespawn called for vehicle {vehicleId}: {this}");
#endif
			ExtCitizenInstance driverExtInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(CustomPassengerCarAI.GetDriverInstance(vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId]));
			if (driverExtInstance != null) {
				driverExtInstance.Reset();
			}

			Unlink();

			currentSegmentId = 0;
			currentStartNode = false;
			currentLaneIndex = 0;

			nextSegmentId = 0;
			nextLaneIndex = 0;

			totalLength = 0;
			spawned = false;
			
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleState.OnDespawn finished for vehicle {vehicleId}: {this}");
#endif
		}

		internal void OnRelease(ref Vehicle vehicleData) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleState.OnRelease called for vehicle {vehicleId}: {this}");
#endif
			ExtCitizenInstance driverExtInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(CustomPassengerCarAI.GetDriverInstance(vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId]));
			if (driverExtInstance != null) {
				driverExtInstance.Reset();
			}

			Unlink();
			lastPathId = 0;
			lastPathPositionIndex = 0;
			lastTransitStateUpdate = Now();
			lastPositionUpdate = Now();
			totalLength = 0;
			waitTime = 0;
			reduceSqrSpeedByValueToYield = 0;
			spawned = false;
			vehicleType = ExtVehicleType.None;
			heavyVehicle = false;
			currentSegmentId = 0;
			currentLaneIndex = 0;
			nextSegmentId = 0;
			nextLaneIndex = 0;
			previousVehicleIdOnSegment = 0;
			nextVehicleIdOnSegment = 0;
			sqrVelocity = 0;
			junctionTransitState = VehicleJunctionTransitState.None;
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleState.OnRelease finished for vehicle {vehicleId}: {this}");
#endif
		}

		/// <summary>
		/// Determines if the junction transit state has been recently modified
		/// </summary>
		/// <returns></returns>
		internal bool IsJunctionTransitStateNew() {
			uint frame = Constants.ServiceFactory.SimulationService.CurrentFrameIndex;
			return (lastTransitStateUpdate >> GlobalConfig.Instance.VehicleStateUpdateShift) >= (frame >> GlobalConfig.Instance.VehicleStateUpdateShift);
		}

		private static ushort GetTransitNodeId(ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
			bool startNode = IsTransitNodeCurStartNode(ref curPos, ref nextPos);
			ushort transitNodeId1 = 0;
			Constants.ServiceFactory.NetService.ProcessSegment(curPos.m_segment, delegate (ushort segmentId, ref NetSegment segment) {
				transitNodeId1 = startNode ? segment.m_startNode : segment.m_endNode;
				return true;
			});

			ushort transitNodeId2 = 0;
			Constants.ServiceFactory.NetService.ProcessSegment(nextPos.m_segment, delegate (ushort segmentId, ref NetSegment segment) {
				transitNodeId2 = startNode ? segment.m_startNode : segment.m_endNode;
				return true;
			});

			if (transitNodeId1 != transitNodeId2) {
				return 0;
			}
			return transitNodeId1;
		}

		private static bool IsTransitNodeCurStartNode(ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
			// note: does not check if curPos and nextPos are successive path positions
			bool startNode;
			if (curPos.m_offset == 0) {
				startNode = true;
			} else if (curPos.m_offset == 255) {
				startNode = false;
			} else if (nextPos.m_offset == 0) {
				startNode = true;
			} else {
				startNode = false;
			}
			return startNode;
		}

		private static uint Now() {
			return Constants.ServiceFactory.SimulationService.CurrentFrameIndex;
		}

		private void DetermineVehicleType(ref Vehicle vehicleData) {
			VehicleAI ai = vehicleData.Info.m_vehicleAI;

			if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
				vehicleType = ExtVehicleType.Emergency;
			} else {
				ExtVehicleType? type = DetermineVehicleTypeFromAIType(ai, false);
				if (type != null) {
					vehicleType = (ExtVehicleType)type;
				} else {
					vehicleType = ExtVehicleType.None;
				}
			}

			if (vehicleType == ExtVehicleType.CargoTruck) {
				heavyVehicle = ((CargoTruckAI)ai).m_isHeavyVehicle;
			} else {
				heavyVehicle = false;
			}

#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleState.DetermineVehicleType({vehicleId}): vehicleType={vehicleType}, heavyVehicle={heavyVehicle}. Info={vehicleData.Info?.name}");
#endif
		}

		private ExtVehicleType? DetermineVehicleTypeFromAIType(VehicleAI ai, bool emergencyOnDuty) {
			if (emergencyOnDuty)
				return ExtVehicleType.Emergency;

			switch (ai.m_info.m_vehicleType) {
				case VehicleInfo.VehicleType.Bicycle:
					return ExtVehicleType.Bicycle;
				case VehicleInfo.VehicleType.Car:
					if (ai is PassengerCarAI)
						return ExtVehicleType.PassengerCar;
					if (ai is AmbulanceAI || ai is FireTruckAI || ai is PoliceCarAI || ai is HearseAI || ai is GarbageTruckAI || ai is MaintenanceTruckAI || ai is SnowTruckAI) {
						return ExtVehicleType.Service;
					}
					if (ai is CarTrailerAI)
						return ExtVehicleType.None;
					if (ai is BusAI)
						return ExtVehicleType.Bus;
					if (ai is TaxiAI)
						return ExtVehicleType.Taxi;
					if (ai is CargoTruckAI)
						return ExtVehicleType.CargoTruck;
					break;
				case VehicleInfo.VehicleType.Metro:
				case VehicleInfo.VehicleType.Train:
				case VehicleInfo.VehicleType.Monorail:
					if (ai is CargoTrainAI)
						return ExtVehicleType.CargoTrain;
					return ExtVehicleType.PassengerTrain;
				case VehicleInfo.VehicleType.Tram:
					return ExtVehicleType.Tram;
				case VehicleInfo.VehicleType.Ship:
					if (ai is PassengerShipAI)
						return ExtVehicleType.PassengerShip;
					//if (ai is CargoShipAI)
					return ExtVehicleType.CargoShip;
				//break;
				case VehicleInfo.VehicleType.Plane:
					//if (ai is PassengerPlaneAI)
					return ExtVehicleType.PassengerPlane;
				//break;
				case VehicleInfo.VehicleType.Helicopter:
					//if (ai is PassengerPlaneAI)
					return ExtVehicleType.Helicopter;
				//break;
				case VehicleInfo.VehicleType.Ferry:
					return ExtVehicleType.Ferry;
				case VehicleInfo.VehicleType.Blimp:
					return ExtVehicleType.Blimp;
				case VehicleInfo.VehicleType.CableCar:
					return ExtVehicleType.CableCar;
			}
#if DEBUGVSTATE
			Log._Debug($"Could not determine vehicle type from ai type: {ai.GetType().ToString()}");
#endif
			return null;
		}
	}
}
