using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using TrafficManager.Traffic;

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

		public int NumCars;

		public Dictionary<ushort, float> Cars = new Dictionary<ushort, float>();

		public int[] CarsOnLanes = new int[24];
		public int numLanes = 0;

		/// <summary>
		/// For each segment id: number of cars going to this segment
		/// </summary>
		public Dictionary<ushort, float> numCarsGoingToSegmentId;

		public PrioritySegment(ushort nodeid, int segmentid, PriorityType type) {
			numCarsGoingToSegmentId = new Dictionary<ushort, float>();
			Nodeid = nodeid;
			Segmentid = segmentid;
			Type = type;
			housekeeping();
		}

		public bool AddCar(ushort vehicleId, float laneLength) {
			if (!Cars.ContainsKey(vehicleId)) {
				Cars.Add(vehicleId, laneLength);
				NumCars = Cars.Count;
				housekeeping();
				return true;
			}
			return false;
		}
		
		public bool RemoveCar(ushort vehicleId) {
			return RemoveCar(vehicleId, true);
		}

		private bool RemoveCar(ushort vehicleId, bool hk) {
			if (Cars.ContainsKey(vehicleId)) {
				Cars.Remove(vehicleId);
				NumCars = Cars.Count;

				if (hk)
					housekeeping();
				return true;
			}
			return false;
		}

		private void housekeeping() {
			calcNumCarsGoingToSegment();
			_numCarsOnLane();
		}

		private void calcNumCarsGoingToSegment() {
			VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
			numCarsGoingToSegmentId.Clear();
			List<ushort> invalidVehicleIds = new List<ushort>();
			foreach (KeyValuePair<ushort, float> e in Cars) {
				var vehicleId = e.Key;
				var normLength = vehicleManager.m_vehicles.m_buffer[vehicleId].CalculateTotalLength(vehicleId) / e.Value;

				if (!TrafficPriority.VehicleList.ContainsKey(vehicleId)) {
					invalidVehicleIds.Add(vehicleId);
					continue;
				}

				PriorityCar vehicle = TrafficPriority.VehicleList[vehicleId];
				if (!numCarsGoingToSegmentId.ContainsKey(vehicle.ToSegment))
					numCarsGoingToSegmentId[vehicle.ToSegment] = normLength;
				else
					numCarsGoingToSegmentId[vehicle.ToSegment] += normLength;
			}

			foreach (var vehicleId in invalidVehicleIds) {
				RemoveCar(vehicleId, false);
			}
		}

		public bool HasCar(ushort vehicleId) {
			return Cars.ContainsKey(vehicleId);
		}

		public int GetCarsOnLane(int lane) {
			return CarsOnLanes[lane];
		}

		private void _numCarsOnLane() {
			var instance = Singleton<NetManager>.instance;

			var segment = instance.m_segments.m_buffer[Segmentid];
			var segmentInfo = segment.Info;

			uint[] laneId = { segment.m_lanes };
			var currentLane = 0;

			CarsOnLanes = new int[16];

			numLanes = segmentInfo.m_lanes.Length;
			while (currentLane < segmentInfo.m_lanes.Length && laneId[0] != 0u) {
				if (segmentInfo.m_lanes[currentLane].m_laneType != NetInfo.LaneType.Pedestrian) {
					foreach (KeyValuePair<ushort, float> entry in Cars.Where(entry => TrafficPriority.VehicleList[entry.Key].FromLaneId == laneId[0])) {
						CarsOnLanes[currentLane]++;
					}
				}

				laneId[0] = instance.m_lanes.m_buffer[(int)((UIntPtr)laneId[0])].m_nextLane;
				currentLane++;
			}
		}
	}
}
