using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;

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

		public ushort Nodeid;
		public int Segmentid;

		public PriorityType Type;

		private Dictionary<ushort, VehiclePosition> Vehicles = new Dictionary<ushort, VehiclePosition>();

		public PrioritySegment(ushort nodeid, int segmentid, PriorityType type) {
			Nodeid = nodeid;
			Segmentid = segmentid;
			Type = type;
		}

		public void AddCar(ushort vehicleId, VehiclePosition carPos) {
			Vehicles[vehicleId] = carPos;
		}
		
		public bool RemoveCar(ushort vehicleId) {
			if (Vehicles.ContainsKey(vehicleId)) {
				Vehicles.Remove(vehicleId);
				return true;
			}
			return false;
		}

		public bool HasVehicle(ushort vehicleId) {
			return Vehicles.ContainsKey(vehicleId);
		}

		/// <summary>
		/// Calculatres for each segment the number of cars going to this segment
		/// </summary>
		public Dictionary<ushort, float> getNumCarsGoingToSegment(float? minSpeed) {
			Dictionary<ushort, float> numCarsGoingToSegmentId = new Dictionary<ushort, float>();
			VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
			NetManager netManager = Singleton<NetManager>.instance;

			NetNode node = netManager.m_nodes.m_buffer[Nodeid];
			for (var s = 0; s < 8; s++) {
				var segmentId = node.GetSegment(s);

				if (segmentId == 0 || segmentId == Segmentid)
					continue;

				if (TrafficLightsManual.SegmentIsIncomingOneWay(segmentId, Nodeid))
					continue;

				numCarsGoingToSegmentId[segmentId] = 0;
			}
			
			foreach (KeyValuePair<ushort, VehiclePosition> e in Vehicles) {
				var vehicleId = e.Key;
				var carPos = e.Value;

				if (vehicleId <= 0 || carPos.ToSegment <= 0)
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
			return Vehicles.Where(e => e.Value.CarState != CarState.Leave || e.Value.LastCarStateUpdate >> 9 >= Singleton<SimulationManager>.instance.m_currentFrameIndex >> 9).Count();
		}

		internal Dictionary<ushort, VehiclePosition> getCars() {
			return Vehicles;
		}

		internal Dictionary<ushort, VehiclePosition> getApproachingVehicles() {
			return Vehicles.Where(e => e.Value.CarState != CarState.Leave || e.Value.LastCarStateUpdate >> 9 >= Singleton<SimulationManager>.instance.m_currentFrameIndex >> 9).ToDictionary(e => e.Key, e => e.Value);
		}
	}
}
