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

        public PrioritySegment(ushort nodeid, int segmentid, PriorityType type)
        {
            Nodeid = nodeid;
            Segmentid = segmentid;
            Type = type;
        }

        public void AddCar(ushort vehicleId)
        {
            if (!Cars.Contains(vehicleId))
            {
                Cars.Add(vehicleId);
                NumCars++;
                _numCarsOnLane();
            }
        }

        public void RemoveCar(ushort vehicleId)
        {
            if (Cars.Contains(vehicleId))
            {
                Cars.Remove(vehicleId);
                NumCars--;
                _numCarsOnLane();
            }
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
            NetManager instance = Singleton<NetManager>.instance;

            var segment = instance.m_segments.m_buffer[Segmentid];
            var info = segment.Info;

            uint[] num2 = {segment.m_lanes};
            var num3 = 0;

            CarsOnLanes = new int[16];

            while (num3 < info.m_lanes.Length && num2[0] != 0u)
            {
                if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian) {
                    foreach (var car in Cars.Where(car => TrafficPriority.VehicleList[car].fromLaneID == num2[0]))
                    {
                        CarsOnLanes[num3]++;
                    }
                }

                num2[0] = instance.m_lanes.m_buffer[(int)((UIntPtr)num2[0])].m_nextLane;
                num3++;
            }
        }
    }
}
