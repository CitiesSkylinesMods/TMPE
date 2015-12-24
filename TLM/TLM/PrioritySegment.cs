using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using TrafficManager.Traffic;

namespace TrafficManager
{
    public class PrioritySegment
    {
        public enum PriorityType
        {
            None = 0,
            Main = 1,
            Stop = 2,
            Yield = 3
        }

        public ushort Nodeid;
        public int Segmentid;


        public PriorityType Type;

        public int NumCars;

        public List<ushort> Cars = new List<ushort>();

        public int[] CarsOnLanes = new int[24];
		public int numLanes = 0;

        public PrioritySegment(ushort nodeid, int segmentid, PriorityType type)
        {
            Nodeid = nodeid;
            Segmentid = segmentid;
            Type = type;
			_numCarsOnLane();
        }

        public bool AddCar(ushort vehicleId)
        {
            if (!Cars.Contains(vehicleId))
            {
                Cars.Add(vehicleId);
                NumCars = Cars.Count;
                _numCarsOnLane();
				return true;
            }
			return false;
        }

        public bool RemoveCar(ushort vehicleId)
        {
            if (Cars.Contains(vehicleId))
            {
                Cars.Remove(vehicleId);
				NumCars = Cars.Count;
				_numCarsOnLane();
				return true;
            }
			return false;
        }

        public bool HasCar(ushort vehicleId)
        {
            return Cars.Contains(vehicleId);
        }

        public int GetCarsOnLane(int lane)
        {
            return CarsOnLanes[lane];
        }

        private void _numCarsOnLane()
        {
            var instance = Singleton<NetManager>.instance;

            var segment = instance.m_segments.m_buffer[Segmentid];
            var segmentInfo = segment.Info;

            uint[] laneId = {segment.m_lanes};
            var currentLane = 0;

            CarsOnLanes = new int[16];

			numLanes = segmentInfo.m_lanes.Length;
			while (currentLane < segmentInfo.m_lanes.Length && laneId[0] != 0u)
            {
                if (segmentInfo.m_lanes[currentLane].m_laneType != NetInfo.LaneType.Pedestrian)
                {
                    foreach (var car in Cars.Where(car => TrafficPriority.VehicleList[car].FromLaneId == laneId[0]))
                    {
                        CarsOnLanes[currentLane]++;
                    }
                }

                laneId[0] = instance.m_lanes.m_buffer[(int)((UIntPtr)laneId[0])].m_nextLane;
                currentLane++;
            }
        }
    }
}
