using System;
using System.Collections.Generic;
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

        public ushort nodeid;
        public int segmentid;


        public PriorityType type = PriorityType.Main;

        public int numCars = 0;

        public List<ushort> cars = new List<ushort>(); 

        public int[] carsOnLanes = new int[24]; 

        public PrioritySegment(ushort nodeid, int segmentid, PriorityType type)
        {
            this.nodeid = nodeid;
            this.segmentid = segmentid;
            this.type = type;
        }

        public void AddCar(ushort vehicleID)
        {
            if (!cars.Contains(vehicleID))
            {
                cars.Add(vehicleID);
                numCars++;
                _numCarsOnLane();
            }
        }

        public void RemoveCar(ushort vehicleID)
        {
            if (cars.Contains(vehicleID))
            {
                cars.Remove(vehicleID);
                numCars--;
                _numCarsOnLane();
            }
        }

        public bool HasCar(ushort vehicleID)
        {
            return cars.Contains(vehicleID);
        }

        public int getCarsOnLane(int lane)
        {
            return carsOnLanes[lane];
        }

        private void _numCarsOnLane()
        {
            NetManager instance = Singleton<NetManager>.instance;

            var segment = instance.m_segments.m_buffer[this.segmentid];
            var info = segment.Info;

            uint num2 = segment.m_lanes;
            int num3 = 0;

            carsOnLanes = new int[16];

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian) {
                    for (var i = 0; i < cars.Count; i++)
                    {
                        if (TrafficPriority.vehicleList[cars[i]].fromLaneID == num2)
                        {
                            carsOnLanes[num3]++;
                        }
                    }
                }

                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num3++;
            }
        }
    }
}