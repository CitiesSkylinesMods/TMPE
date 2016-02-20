using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using TrafficManager.Custom.AI;

/// <summary>
/// A priority segment describes a directional traffic segment connected to a controlled
/// node (with traffic lights, priority settings).
/// </summary>
namespace TrafficManager.Traffic {
	public class SegmentEnd {
		public enum PriorityType {
			None = 0,
			Main = 1,
			Stop = 2,
			Yield = 3
		}

		public ushort NodeId {
			get; private set;
		}
		public ushort SegmentId {
			get; private set;
		}

		public PriorityType Type;

		private Dictionary<ushort, VehiclePosition> Vehicles = new Dictionary<ushort, VehiclePosition>();

		public SegmentEnd(ushort nodeId, ushort segmentId, PriorityType type) {
			NodeId = nodeId;
			SegmentId = segmentId;
			Type = type;
		}

		~SegmentEnd() {
			RemoveAllCars();
		}

		public void AddVehicle(ushort vehicleId, VehiclePosition carPos) {
			if (carPos.ToNode != NodeId || carPos.FromSegment != SegmentId) {
				Log.Warning($"Refusing to add vehicle {vehicleId} to PrioritySegment {SegmentId} @ {NodeId} (given: {carPos.FromSegment} @ {carPos.ToNode}).");
                return;
			}
			Vehicles[vehicleId] = carPos;
			TrafficPriority.MarkVehicleInSegment(vehicleId, SegmentId);
		}
		
		public bool RemoveVehicle(ushort vehicleId) {
			Vehicles.Remove(vehicleId);
			TrafficPriority.UnmarkVehicleInSegment(vehicleId, SegmentId);
			return true;
		}

		public void RemoveAllCars() {
			List<ushort> vehicleIds = new List<ushort>(Vehicles.Keys);
			foreach (ushort vehicleId in vehicleIds) {
				RemoveVehicle(vehicleId);
			}
		}

		public bool HasVehicle(ushort vehicleId) {
			return Vehicles.ContainsKey(vehicleId);
		}

		internal VehiclePosition GetVehicle(ushort vehicleId) {
			VehiclePosition ret = null;
			Vehicles.TryGetValue(vehicleId, out ret);
			return ret;
		}

		/// <summary>
		/// Calculates for each segment the number of cars going to this segment.
		/// We use integer arithmetic for better performance.
		/// </summary>
		public Dictionary<ushort, uint> GetVehicleMetricGoingToSegment(float? minSpeed, ExtVehicleType? vehicleTypes=null, ExtVehicleType separateVehicleTypes=ExtVehicleType.None, bool debug = false) {
			Dictionary<ushort, uint> numCarsGoingToSegmentId = new Dictionary<ushort, uint>();
			VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
			NetManager netManager = Singleton<NetManager>.instance;

			for (var s = 0; s < 8; s++) {
				var segmentId = netManager.m_nodes.m_buffer[NodeId].GetSegment(s);

				if (segmentId == 0 || segmentId == SegmentId)
					continue;

				if (CustomRoadAI.GetSegmentGeometry(segmentId).IsIncomingOneWay(NodeId))
					continue;

				numCarsGoingToSegmentId[segmentId] = 0;
			}

			List<ushort> vehicleIdsToReHandle = new List<ushort>();

			foreach (KeyValuePair<ushort, VehiclePosition> e in Vehicles) {
				var vehicleId = e.Key;
				var carPos = e.Value;

				if (vehicleId <= 0 || carPos.ToSegment <= 0)
					continue;
				if ((vehicleManager.m_vehicles.m_buffer[vehicleId].m_flags & Vehicle.Flags.Created) == Vehicle.Flags.None) {
					vehicleIdsToReHandle.Add(vehicleId);
					continue;
				}
				if (minSpeed != null && vehicleManager.m_vehicles.m_buffer[vehicleId].GetLastFrameVelocity().magnitude < minSpeed)
					continue;
				VehiclePosition globalPos = TrafficPriority.GetVehiclePosition(vehicleId);
				if (globalPos == null || !globalPos.Valid || globalPos.LastFrame >> 7 < Singleton<SimulationManager>.instance.m_currentFrameIndex >> 7) { // ~64 sec.
					vehicleIdsToReHandle.Add(vehicleId);
					continue;
				}
				if (vehicleTypes != null) {
					if (vehicleTypes == ExtVehicleType.None) {
						if ((globalPos.VehicleType & separateVehicleTypes) != ExtVehicleType.None) {
							// we want all vehicles that do not have separate traffic lights
							continue;
						}
					} else {
						if ((globalPos.VehicleType & vehicleTypes) == ExtVehicleType.None) {
							continue;
						}
					}
				}

				debug = vehicleManager.m_vehicles.m_buffer[vehicleId].Info.m_vehicleType == VehicleInfo.VehicleType.Tram;
#if DEBUG
				if (debug) {
					Log._Debug($"getNumCarsGoingToSegment: Handling vehicle {vehicleId} going from {carPos.FromSegment}/{SegmentId} to {carPos.ToSegment}. carState={globalPos.CarState}. lastUpdate={globalPos.LastCarStateUpdate}");
                }
#endif

				uint avgSegmentLength = (uint)Singleton<NetManager>.instance.m_segments.m_buffer[SegmentId].m_averageLength;
				uint normLength = (uint)(vehicleManager.m_vehicles.m_buffer[vehicleId].CalculateTotalLength(vehicleId) * 100u) / avgSegmentLength;

#if DEBUG
				/*if (debug) {
					Log._Debug($"getNumCarsGoingToSegment: NormLength of vehicle {vehicleId} going to {carPos.ToSegment}: {avgSegmentLength} -> {normLength}");
				}*/
#endif

				if (numCarsGoingToSegmentId.ContainsKey(carPos.ToSegment)) {
					/*if (carPos.OnEmergency)
						numCarsGoingToSegmentId[carPos.ToSegment] += 10000f;
					else*/
						numCarsGoingToSegmentId[carPos.ToSegment] += normLength;
				}
				// "else" must not happen (incoming one-way)
			}

			foreach (ushort vehicleId in vehicleIdsToReHandle) {
				CustomVehicleAI.HandleVehicle(vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId], false, false);
			}
			return numCarsGoingToSegmentId;
		}

		internal int getNumCars() {
			return Vehicles.Count;
		}

		internal int getNumApproachingVehicles() {
			return Vehicles.Where(e =>
				(e.Value.CarState != VehicleJunctionTransitState.Leave || e.Value.LastCarStateUpdate >> 6 >= Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6) &&
				(Singleton<VehicleManager>.instance.m_vehicles.m_buffer[e.Key].m_flags & Vehicle.Flags.Created) != Vehicle.Flags.None &&
				Singleton<VehicleManager>.instance.m_vehicles.m_buffer[e.Key].GetLastFrameVelocity().magnitude > TrafficPriority.maxStopVelocity).Count();
		}

		internal Dictionary<ushort, VehiclePosition> getCars() {
			return Vehicles;
		}

		internal Dictionary<ushort, VehiclePosition> getApproachingVehicles() {
			return Vehicles.Where(e => 
				(e.Value.CarState != VehicleJunctionTransitState.Leave || e.Value.LastCarStateUpdate >> 6 >= Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6) &&
				(Singleton<VehicleManager>.instance.m_vehicles.m_buffer[e.Key].m_flags & Vehicle.Flags.Created) != Vehicle.Flags.None &&
				Singleton<VehicleManager>.instance.m_vehicles.m_buffer[e.Key].GetLastFrameVelocity().magnitude > TrafficPriority.maxStopVelocity).ToDictionary(e => e.Key, e => e.Value);
		}
	}
}
