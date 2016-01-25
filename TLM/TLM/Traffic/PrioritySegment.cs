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
	public class PrioritySegment {
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

		public PrioritySegment(ushort nodeId, ushort segmentId, PriorityType type) {
			NodeId = nodeId;
			SegmentId = segmentId;
			Type = type;
		}

		~PrioritySegment() {
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
		
		public bool RemoveCar(ushort vehicleId) {
			if (!Vehicles.ContainsKey(vehicleId))
				return false;
			Vehicles.Remove(vehicleId);
			TrafficPriority.UnmarkVehicleInSegment(vehicleId, SegmentId);
			return true;
		}

		public void RemoveAllCars() {
			List<ushort> vehicleIds = new List<ushort>(Vehicles.Keys);
			foreach (ushort vehicleId in vehicleIds) {
				RemoveCar(vehicleId);
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
		/// Calculatres for each segment the number of cars going to this segment
		/// </summary>
		public Dictionary<ushort, float> getNumCarsGoingToSegment(float? minSpeed) {
			Dictionary<ushort, float> numCarsGoingToSegmentId = new Dictionary<ushort, float>();
			VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
			NetManager netManager = Singleton<NetManager>.instance;

			NetNode node = netManager.m_nodes.m_buffer[NodeId];
			for (var s = 0; s < 8; s++) {
				var segmentId = node.GetSegment(s);

				if (segmentId == 0 || segmentId == SegmentId)
					continue;

				if (CustomRoadAI.GetSegmentGeometry(segmentId).IsIncomingOneWay(NodeId))
					continue;

				numCarsGoingToSegmentId[segmentId] = 0;
			}
			
			foreach (KeyValuePair<ushort, VehiclePosition> e in Vehicles) {
				var vehicleId = e.Key;
				var carPos = e.Value;

				if (vehicleId <= 0 || carPos.ToSegment <= 0)
					continue;
				if ((vehicleManager.m_vehicles.m_buffer[vehicleId].m_flags & Vehicle.Flags.Created) == Vehicle.Flags.None)
					continue;
				float speed = vehicleManager.m_vehicles.m_buffer[vehicleId].GetLastFrameVelocity().magnitude;
				if (minSpeed != null && speed < minSpeed)
					continue;

				float avgSegmentLength = Singleton<NetManager>.instance.m_segments.m_buffer[carPos.ToSegment].m_averageLength;
				var normLength = vehicleManager.m_vehicles.m_buffer[vehicleId].CalculateTotalLength(vehicleId) / avgSegmentLength;

				if (numCarsGoingToSegmentId.ContainsKey(carPos.ToSegment))
					numCarsGoingToSegmentId[carPos.ToSegment] += normLength;
				// "else" must not happen (incoming one-way)
			}
			return numCarsGoingToSegmentId;
		}

		internal int getNumCars() {
			return Vehicles.Count;
		}

		internal int getNumApproachingVehicles() {
			return Vehicles.Where(e =>
				(e.Value.CarState != CarState.Leave || e.Value.LastCarStateUpdate >> 6 >= Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6) &&
				(Singleton<VehicleManager>.instance.m_vehicles.m_buffer[e.Key].m_flags & Vehicle.Flags.Created) != Vehicle.Flags.None &&
				Singleton<VehicleManager>.instance.m_vehicles.m_buffer[e.Key].GetLastFrameVelocity().magnitude > TrafficPriority.maxStopVelocity).Count();
		}

		internal Dictionary<ushort, VehiclePosition> getCars() {
			return Vehicles;
		}

		internal Dictionary<ushort, VehiclePosition> getApproachingVehicles() {
			return Vehicles.Where(e => 
				(e.Value.CarState != CarState.Leave || e.Value.LastCarStateUpdate >> 6 >= Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6) &&
				(Singleton<VehicleManager>.instance.m_vehicles.m_buffer[e.Key].m_flags & Vehicle.Flags.Created) != Vehicle.Flags.None &&
				Singleton<VehicleManager>.instance.m_vehicles.m_buffer[e.Key].GetLastFrameVelocity().magnitude > TrafficPriority.maxStopVelocity).ToDictionary(e => e.Key, e => e.Value);
		}
	}
}
