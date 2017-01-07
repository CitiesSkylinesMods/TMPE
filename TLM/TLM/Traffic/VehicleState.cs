#define USEPATHWAITCOUNTERx
#define DEBUGVSTATEx
#define PATHFORECASTx
#define PATHRECALCx
#define DEBUGREGx

using System;
using ColossalFramework;
using UnityEngine;
using System.Collections.Generic;
using TrafficManager.TrafficLight;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using TrafficManager.Custom.AI;

namespace TrafficManager.Traffic {
	public class VehicleState {
#if DEBUGVSTATE
		private static readonly ushort debugVehicleId = 6316;
#endif
		private static readonly VehicleInfo.VehicleType HANDLED_VEHICLE_TYPES = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro;
		public static readonly int STATE_UPDATE_SHIFT = 6;

		private VehicleJunctionTransitState junctionTransitState;
		public VehicleJunctionTransitState JunctionTransitState {
			get { return junctionTransitState; }
			set {
				if (value != junctionTransitState)
					LastStateUpdate = Singleton<SimulationManager>.instance.m_currentFrameIndex >> VehicleState.STATE_UPDATE_SHIFT;
				junctionTransitState = value;
			}
		}

		public uint LastStateUpdate {
			get; private set;
		}

		public uint LastPositionUpdate {
			get; private set;
		}

		public float TotalLength {
			get; private set;
		}

		private ushort VehicleId;
		public int WaitTime = 0;
		public float ReduceSqrSpeedByValueToYield;
		private bool valid = false;

		public bool Valid {
			get {
				if ((Singleton<VehicleManager>.instance.m_vehicles.m_buffer[VehicleId].m_flags & Vehicle.Flags.Created) == 0) {
					return false;
				}
				return valid;
			}
			internal set { valid = value; }
		}
		public bool Emergency {
			get; internal set;
		}

#if PATHRECALC
		public uint LastPathRecalculation = 0;
		public ushort LastPathRecalculationSegmentId = 0;
		public bool PathRecalculationRequested { get; internal set; } = false;
#endif
		public ExtVehicleType VehicleType {
			get; internal set;
		}

		public bool HeavyVehicle {
			get; internal set;
		}

#if PATHFORECAST
		private LinkedList<VehiclePosition> VehiclePositions; // the last element holds the current position
		private LinkedListNode<VehiclePosition> CurrentPosition;
		private int numRegisteredAhead = 0;
#endif
		public SegmentEnd CurrentSegmentEnd {
			get; internal set;
		}
		public ushort PreviousVehicleIdOnSegment {
			get; internal set;
		}
		public ushort NextVehicleIdOnSegment {
			get; internal set;
		}

		//internal ushort DriverInstanceId = 0;

#if USEPATHWAITCOUNTER
		public ushort PathWaitCounter {
			get { return pathWaitCounter; }
			internal set {
				pathWaitCounter = value;
				if (pathWaitCounter >= 32767) {
					Log.Warning($"Path wait counter hit maximum for vehicle {VehicleId}. Check path {Singleton<VehicleManager>.instance.m_vehicles.m_buffer[VehicleId].m_path}!");
				}
			}
		}

		private ushort pathWaitCounter = 0;
#endif

		public VehicleState(ushort vehicleId) {
			this.VehicleId = vehicleId;
			VehicleType = ExtVehicleType.None;
			Reset(false);
		}

		private void Reset(bool unlink=true) { // TODO this is called in wrong places!
			if (unlink)
				Unlink();

			Valid = false;
			TotalLength = 0f;
			//VehicleType = ExtVehicleType.None;
			WaitTime = 0;
			JunctionTransitState = VehicleJunctionTransitState.None;
			LastStateUpdate = 0;
		}

		public ExtCitizenInstance GetDriverExtInstance() {
			/*if (DriverInstanceId == 0) {
				DriverInstanceId = CustomPassengerCarAI.GetDriverInstance(VehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[VehicleId]);
			}
#if DEBUG
			else {
				ushort liveDriverInstId = CustomPassengerCarAI.GetDriverInstance(VehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[VehicleId]);
				if (liveDriverInstId != DriverInstanceId) {
					Log.Error($"Driver citizen instance mismatch for vehicle {VehicleId}: True driver: {liveDriverInstId} Stored driver: {DriverInstanceId}");
					DriverInstanceId = liveDriverInstId;
				}
			}
#endif
			*/

			ushort driverInstanceId = CustomPassengerCarAI.GetDriverInstance(VehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[VehicleId]);
			if (driverInstanceId != 0) {
				return ExtCitizenInstanceManager.Instance.GetExtInstance(driverInstanceId);
			}
			return null;
		}

		internal void Unlink() {
			VehicleStateManager vehStateManager = VehicleStateManager.Instance;

			if (PreviousVehicleIdOnSegment != 0) {
				vehStateManager._GetVehicleState(PreviousVehicleIdOnSegment).NextVehicleIdOnSegment = NextVehicleIdOnSegment;
			} else if (CurrentSegmentEnd != null && CurrentSegmentEnd.FirstRegisteredVehicleId == VehicleId) {
				CurrentSegmentEnd.FirstRegisteredVehicleId = NextVehicleIdOnSegment;
			}

			if (NextVehicleIdOnSegment != 0) {
				vehStateManager._GetVehicleState(NextVehicleIdOnSegment).PreviousVehicleIdOnSegment = PreviousVehicleIdOnSegment;
			}

			NextVehicleIdOnSegment = 0;
			PreviousVehicleIdOnSegment = 0;
			CurrentSegmentEnd = null;
		}

		private void Link(SegmentEnd end) {
			ushort oldFirstRegVehicleId = end.FirstRegisteredVehicleId;
			if (oldFirstRegVehicleId != 0) {
				VehicleStateManager.Instance._GetVehicleState(oldFirstRegVehicleId).PreviousVehicleIdOnSegment = VehicleId;
				NextVehicleIdOnSegment = oldFirstRegVehicleId;
			}
			end.FirstRegisteredVehicleId = VehicleId;
			CurrentSegmentEnd = end;
		}

		public delegate void PathPositionProcessor(ref PathUnit.Position pos);
		public delegate void DualPathPositionProcessor(ref Vehicle vehicleData, ref PathUnit.Position firstPos, ref PathUnit.Position secondPos);
		public delegate void QuadPathPositionProcessor(ref Vehicle firstVehicleData, ref PathUnit.Position firstVehicleFirstPos, ref PathUnit.Position firstVehicleSecondPos, ref Vehicle secondVehicleData, ref PathUnit.Position secondVehicleFirstPos, ref PathUnit.Position secondVehicleSecondPos);

		public bool ProcessCurrentPathPosition(ref Vehicle vehicleData, PathPositionProcessor processor) {
			return ProcessPathUnit(ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path], (byte)(vehicleData.m_pathPositionIndex >> 1), processor);
		}

		public bool ProcessCurrentPathPosition(ref Vehicle vehicleData, byte index, PathPositionProcessor processor) {
			return ProcessPathUnit(ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path], index, processor);
		}

		public bool ProcessCurrentAndNextPathPosition(ref Vehicle vehicleData, DualPathPositionProcessor processor) {
			return ProcessCurrentAndNextPathPosition(ref vehicleData, (byte)(vehicleData.m_pathPositionIndex >> 1), processor);
		}

		public bool ProcessTrailerCurrentAndNextPathPosition(ref Vehicle vehicleData, ref Vehicle trailerData, DualPathPositionProcessor processor) {
			return ProcessTrailerCurrentAndNextPathPosition(ref vehicleData, ref trailerData, (byte)(vehicleData.m_pathPositionIndex >> 1), processor);
		}

		public bool ProcessCurrentAndNextPathPosition(ref Vehicle vehicleData, byte index, DualPathPositionProcessor processor) {
			if (index < 11)
				return ProcessPathUnitPair(ref vehicleData, ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path], ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path], index, processor);
			else
				return ProcessPathUnitPair(ref vehicleData, ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path], ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path].m_nextPathUnit], index, processor);
		}

		public bool ProcessTrailerCurrentAndNextPathPosition(ref Vehicle vehicleData, ref Vehicle trailerData, byte index, DualPathPositionProcessor processor) {
			if (index < 11)
				return ProcessPathUnitPair(ref trailerData, ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path], ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path], index, processor);
			else
				return ProcessPathUnitPair(ref trailerData, ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path], ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path].m_nextPathUnit], index, processor);
		}

		public bool ProcessCurrentAndNextPathPositionAndOtherVehicleCurrentAndNextPathPosition(ref Vehicle vehicleData, ref PathUnit.Position otherVehicleCurPos, ref PathUnit.Position otherVehicleNextPos, ref Vehicle otherVehicleData, QuadPathPositionProcessor processor) {
			return ProcessCurrentAndNextPathPositionAndOtherVehicleCurrentAndNextPathPosition(ref vehicleData, (byte)(vehicleData.m_pathPositionIndex >> 1), ref otherVehicleCurPos, ref otherVehicleNextPos, ref otherVehicleData, processor);
		}

		public bool ProcessCurrentAndNextPathPositionAndOtherVehicleCurrentAndNextPathPosition(ref Vehicle vehicleData, byte index, ref PathUnit.Position otherVehicleCurPos, ref PathUnit.Position otherVehicleNextPos, ref Vehicle otherVehicleData, QuadPathPositionProcessor processor) {
			if (index < 11)
				return ProcessPathUnitQuad(ref vehicleData, ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path], ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path], index, ref otherVehicleData, ref otherVehicleCurPos, ref otherVehicleNextPos, processor);
			else
				return ProcessPathUnitQuad(ref vehicleData, ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path], ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path].m_nextPathUnit], index, ref otherVehicleData, ref otherVehicleCurPos, ref otherVehicleNextPos, processor);
		}

		public PathUnit.Position GetCurrentPathPosition(ref Vehicle vehicleData) {
			return Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path].GetPosition((byte)(vehicleData.m_pathPositionIndex >> 1));
		}

		public PathUnit.Position GetNextPathPosition(ref Vehicle vehicleData) {
			byte index = (byte)((vehicleData.m_pathPositionIndex >> 1) + 1);
			if (index <= 11)
				return Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path].GetPosition(index);
			else
				return Singleton<PathManager>.instance.m_pathUnits.m_buffer[Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path].m_nextPathUnit].GetPosition(0);
		}

		private bool ProcessPathUnitPair(ref Vehicle vehicleData, ref PathUnit unit1, ref PathUnit unit2, byte index, DualPathPositionProcessor processor) {
			if ((unit1.m_pathFindFlags & PathUnit.FLAG_READY) == 0) {
				return false;
			}

			if ((unit2.m_pathFindFlags & PathUnit.FLAG_READY) == 0) {
				return false;
			}

			switch (index) {
				case 0:
					processor(ref vehicleData, ref unit1.m_position00, ref unit1.m_position01);
					break;
				case 1:
					processor(ref vehicleData, ref unit1.m_position01, ref unit1.m_position02);
					break;
				case 2:
					processor(ref vehicleData, ref unit1.m_position02, ref unit1.m_position03);
					break;
				case 3:
					processor(ref vehicleData, ref unit1.m_position03, ref unit1.m_position04);
					break;
				case 4:
					processor(ref vehicleData, ref unit1.m_position04, ref unit1.m_position05);
					break;
				case 5:
					processor(ref vehicleData, ref unit1.m_position05, ref unit1.m_position06);
					break;
				case 6:
					processor(ref vehicleData, ref unit1.m_position06, ref unit1.m_position07);
					break;
				case 7:
					processor(ref vehicleData, ref unit1.m_position07, ref unit1.m_position08);
					break;
				case 8:
					processor(ref vehicleData, ref unit1.m_position08, ref unit1.m_position09);
					break;
				case 9:
					processor(ref vehicleData, ref unit1.m_position09, ref unit1.m_position10);
					break;
				case 10:
					processor(ref vehicleData, ref unit1.m_position10, ref unit1.m_position11);
					break;
				case 11:
					processor(ref vehicleData, ref unit1.m_position11, ref unit2.m_position00);
					break;
				default:
					return false;
			}

			return true;
		}

		private bool ProcessPathUnitQuad(ref Vehicle firstVehicleData, ref PathUnit unit1, ref PathUnit unit2, byte index, ref Vehicle secondVehicleData, ref PathUnit.Position secondPos1, ref PathUnit.Position secondPos2, QuadPathPositionProcessor processor) {
			if ((unit1.m_pathFindFlags & PathUnit.FLAG_READY) == 0) {
				return false;
			}

			if ((unit2.m_pathFindFlags & PathUnit.FLAG_READY) == 0) {
				return false;
			}

			switch (index) {
				case 0:
					processor(ref firstVehicleData, ref unit1.m_position00, ref unit1.m_position01, ref secondVehicleData, ref secondPos1, ref secondPos2);
					break;
				case 1:
					processor(ref firstVehicleData, ref unit1.m_position01, ref unit1.m_position02, ref secondVehicleData, ref secondPos1, ref secondPos2);
					break;
				case 2:
					processor(ref firstVehicleData, ref unit1.m_position02, ref unit1.m_position03, ref secondVehicleData, ref secondPos1, ref secondPos2);
					break;
				case 3:
					processor(ref firstVehicleData, ref unit1.m_position03, ref unit1.m_position04, ref secondVehicleData, ref secondPos1, ref secondPos2);
					break;
				case 4:
					processor(ref firstVehicleData, ref unit1.m_position04, ref unit1.m_position05, ref secondVehicleData, ref secondPos1, ref secondPos2);
					break;
				case 5:
					processor(ref firstVehicleData, ref unit1.m_position05, ref unit1.m_position06, ref secondVehicleData, ref secondPos1, ref secondPos2);
					break;
				case 6:
					processor(ref firstVehicleData, ref unit1.m_position06, ref unit1.m_position07, ref secondVehicleData, ref secondPos1, ref secondPos2);
					break;
				case 7:
					processor(ref firstVehicleData, ref unit1.m_position07, ref unit1.m_position08, ref secondVehicleData, ref secondPos1, ref secondPos2);
					break;
				case 8:
					processor(ref firstVehicleData, ref unit1.m_position08, ref unit1.m_position09, ref secondVehicleData, ref secondPos1, ref secondPos2);
					break;
				case 9:
					processor(ref firstVehicleData, ref unit1.m_position09, ref unit1.m_position10, ref secondVehicleData, ref secondPos1, ref secondPos2);
					break;
				case 10:
					processor(ref firstVehicleData, ref unit1.m_position10, ref unit1.m_position11, ref secondVehicleData, ref secondPos1, ref secondPos2);
					break;
				case 11:
					processor(ref firstVehicleData, ref unit1.m_position11, ref unit2.m_position00, ref secondVehicleData, ref secondPos1, ref secondPos2);
					break;
				default:
					return false;
			}

			return true;
		}

		internal bool HasPath(ref Vehicle vehicleData) {
			return (Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path].m_pathFindFlags & PathUnit.FLAG_READY) != 0;
		}

		private bool ProcessPathUnit(ref PathUnit unit, byte index, PathPositionProcessor processor) {
			if ((unit.m_pathFindFlags & PathUnit.FLAG_READY) == 0) {
				return false;
			}

			switch (index) {
				case 0:
					processor(ref unit.m_position00);
					break;
				case 1:
					processor(ref unit.m_position01);
					break;
				case 2:
					processor(ref unit.m_position02);
					break;
				case 3:
					processor(ref unit.m_position03);
					break;
				case 4:
					processor(ref unit.m_position04);
					break;
				case 5:
					processor(ref unit.m_position05);
					break;
				case 6:
					processor(ref unit.m_position06);
					break;
				case 7:
					processor(ref unit.m_position07);
					break;
				case 8:
					processor(ref unit.m_position08);
					break;
				case 9:
					processor(ref unit.m_position09);
					break;
				case 10:
					processor(ref unit.m_position10);
					break;
				case 11:
					processor(ref unit.m_position11);
					break;
				default:
					return false;
			}
			return true;
		}

		internal void UpdatePosition(ref Vehicle vehicleData, ref PathUnit.Position curPos, ref PathUnit.Position nextPos, bool skipCheck=false) {
			if (! skipCheck && ! CheckValidity(ref vehicleData)) {
				return;
			}

			LastPositionUpdate = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			SegmentEnd end = TrafficPriorityManager.Instance.GetPrioritySegment(GetTransitNodeId(ref curPos, ref nextPos), curPos.m_segment);
			
			if (CurrentSegmentEnd != end) {
				if (CurrentSegmentEnd != null) {
					Unlink();
				}

				WaitTime = 0;
				if (end != null) {
					Link(end);
					JunctionTransitState = VehicleJunctionTransitState.Enter;
				} else {
					JunctionTransitState = VehicleJunctionTransitState.None;
				}
			}
		}

		internal void UpdatePosition(ref Vehicle vehicleData) {
			if (!CheckValidity(ref vehicleData)) {
				return;
			}

			ProcessCurrentAndNextPathPosition(ref vehicleData, delegate (ref Vehicle vehData, ref PathUnit.Position currentPosition, ref PathUnit.Position nextPosition) {
				UpdatePosition(ref vehData, ref currentPosition, ref nextPosition);
			});

			// update position of trailers
			ushort trailerId = vehicleData.m_trailingVehicle;
			while (trailerId != 0) {
				VehicleStateManager.Instance.UpdateTrailerPos(trailerId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[trailerId], VehicleId, ref vehicleData);
				trailerId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[trailerId].m_trailingVehicle;
			}
		}

		internal void UpdatePosition(ref Vehicle trailerData, ushort vehicleId, ref Vehicle vehicleData) {
			ProcessTrailerCurrentAndNextPathPosition(ref vehicleData, ref trailerData, delegate (ref Vehicle trailData, ref PathUnit.Position currentPosition, ref PathUnit.Position nextPosition) {
				UpdatePosition(ref trailData, ref currentPosition, ref nextPosition, true);
			});
		}

		internal bool CheckValidity(ref Vehicle vehicleData, bool skipCached=false) {
#if DEBUG
			//bool debug = skipCached;
			//byte pfFlags = Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path].m_pathFindFlags;
#endif

#if DEBUG
			//if (debug)
			//Log._Debug($"VehicleState.CheckValidity({VehicleId}) called. created: {(vehicleData.m_flags & Vehicle.Flags.Created) != 0} ({vehicleData.m_flags}) handled: {(vehicleData.Info.m_vehicleType & HANDLED_VEHICLE_TYPES) != VehicleInfo.VehicleType.None} ({vehicleData.Info.m_vehicleType}) has path unit: {vehicleData.m_path != 0} path ready: {(pfFlags & PathUnit.FLAG_READY) != 0} ({pfFlags})");
#endif

			if (!skipCached && !Valid)
				return false;

			if ((vehicleData.m_flags & Vehicle.Flags.Created) == 0 || (vehicleData.Info.m_vehicleType & HANDLED_VEHICLE_TYPES) == VehicleInfo.VehicleType.None || vehicleData.m_path <= 0) {
				Valid = false;
				return false;
			}

			
			if ((Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path].m_pathFindFlags & PathUnit.FLAG_READY) == 0) {
				Valid = false;
				return false;
			}
			return true;
		}

		internal static ushort GetTransitNodeId(ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
			// note: does not check if curPos and nextPos are successive path positions
			NetManager netManager = Singleton<NetManager>.instance;
			ushort transitNodeId;
			if (curPos.m_offset == 0) {
				transitNodeId = netManager.m_segments.m_buffer[curPos.m_segment].m_startNode;
			} else if (curPos.m_offset == 255) {
				transitNodeId = netManager.m_segments.m_buffer[curPos.m_segment].m_endNode;
			} else if (nextPos.m_offset == 0) {
				transitNodeId = netManager.m_segments.m_buffer[nextPos.m_segment].m_startNode;
			} else {
				transitNodeId = netManager.m_segments.m_buffer[nextPos.m_segment].m_endNode;
			}
			return transitNodeId;
		}

		internal void OnVehicleSpawned(ref Vehicle vehicleData) {
#if DEBUG
			//Log._Debug($"VehicleState.OnPathFindReady called for vehicle {VehicleId} ({VehicleType}");
#endif

			Reset();
			//DriverInstanceId = 0;

			if (!CheckValidity(ref vehicleData, true)) {
				return;
			}

			// enforce updating trailers (they are not always present at path-finding time)
			ApplyVehicleTypeToTrailers();

			ReduceSqrSpeedByValueToYield = UnityEngine.Random.Range(256f, 784f);
			try {
				TotalLength = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[VehicleId].CalculateTotalLength(VehicleId);
			} catch (Exception) {
				TotalLength = 0;
#if DEBUG
				Log._Debug($"Error occurred while calculating total length of vehicle {VehicleId} ({VehicleType}).");
#endif
				return;
			}
			
			Valid = true;
		}

		private void ApplyVehicleTypeToTrailers() {
			VehicleManager vehManager = Singleton<VehicleManager>.instance;
			VehicleStateManager vehStateManager = VehicleStateManager.Instance;

#if DEBUG
			//Log._Debug($"Applying VehicleType to trailes of vehicle {VehicleId} to {VehicleType}.");
#endif

			// apply vehicle type to all leading/trailing vehicles
			ushort otherVehicleId = vehManager.m_vehicles.m_buffer[VehicleId].m_leadingVehicle;
			while (otherVehicleId != 0) {
#if DEBUG
				//Log._Debug($"    Setting VehicleType of leader {otherVehicleId} to {VehicleType}.");
#endif
				VehicleState otherState = vehStateManager._GetVehicleState(otherVehicleId);
				otherState.Valid = true;
				otherState.VehicleType = VehicleType;
				otherVehicleId = vehManager.m_vehicles.m_buffer[otherVehicleId].m_leadingVehicle;
			}

			otherVehicleId = vehManager.m_vehicles.m_buffer[VehicleId].m_trailingVehicle;
			while (otherVehicleId != 0) {
#if DEBUG
				//Log._Debug($"    Setting VehicleType of trailer {otherVehicleId} to {VehicleType}.");
#endif
				VehicleState otherState = vehStateManager._GetVehicleState(otherVehicleId);
				otherState.Valid = true;
				otherState.VehicleType = VehicleType;
				otherVehicleId = vehManager.m_vehicles.m_buffer[otherVehicleId].m_trailingVehicle;
			}
		}

		/// <summary>
		/// Determines if the junction transit state has been recently modified
		/// </summary>
		/// <returns></returns>
		internal bool IsJunctionTransitStateNew() {
			uint frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			return (LastStateUpdate >> VehicleState.STATE_UPDATE_SHIFT) >= (frame >> VehicleState.STATE_UPDATE_SHIFT);
		}
	}
}
