using System;
using System.Collections.Generic;
using System.Text;

namespace TrafficManager
{
    class LaneRestrictions
    {
        public uint laneID;
        public int laneNum;
        public NetInfo.Direction direction;

        //if (vehicleService == ItemClass.Service.Commercial)
        //    vehicleFlag |= 128;
        //else if (vehicleService == ItemClass.Service.FireDepartment)
        //    vehicleFlag |= 130;
        //else if (vehicleService == ItemClass.Service.Garbage)
        //    vehicleFlag |= 132;
        //else if (vehicleService == ItemClass.Service.HealthCare)
        //    vehicleFlag |= 134;
        //else if (vehicleService == ItemClass.Service.Industrial)
        //    vehicleFlag |= 136;
        //else if (vehicleService == ItemClass.Service.PoliceDepartment)
        //    vehicleFlag |= 138;
        //else if (vehicleService == ItemClass.Service.PublicTransport)
        //    vehicleFlag |= 140;
        public bool enableCars = true;
        public bool enableService = true;
        public bool enableTransport = true;
        public bool enableCargo = true;

        public int enabledTypes = 4;
        public readonly int maxAllowedTypes = 4;

        public LaneRestrictions(uint laneid, int laneNum, NetInfo.Direction dir)
        {
            this.laneID = laneid;
            this.laneNum = laneNum;
            this.direction = dir;
        }

        public void toggleCars()
        {
            enableCars = !enableCars;

            enabledTypes = enableCars ? enabledTypes + 1 : enabledTypes - 1;
        }

        public void toggleCargo()
        {
            enableCargo = !enableCargo;

            enabledTypes = enableCargo ? enabledTypes + 1 : enabledTypes - 1;
        }

        public void toggleService()
        {
            enableService = !enableService;

            enabledTypes = enableService ? enabledTypes + 1 : enabledTypes - 1;
        }

        public void toggleTransport()
        {
            enableTransport = !enableTransport;

            enabledTypes = enableTransport ? enabledTypes + 1 : enabledTypes - 1;
        }
    }
    class SegmentRestrictions
    {
        private int segmentID;

        public float[] speedLimits = new float[16] {0f, 0f, 0f, 0f, 0f, 0f,0f,0f,0f,0f,0f,0f,0f,0f,0f,0f};

        public List<LaneRestrictions> lanes = new List<LaneRestrictions>();

        public List<int> segmentGroup; 

        public SegmentRestrictions(int segmentid, List<int> segmentGroup )
        {
            this.segmentID = segmentid;
            this.segmentGroup = new List<int>(segmentGroup);
        }

        public void addLane(uint lane, int lanenum, NetInfo.Direction dir)
        {
            lanes.Add(new LaneRestrictions(lane, lanenum, dir));
        }

        public LaneRestrictions getLane(int lane)
        {
            return lanes[lane];
        }

        public LaneRestrictions getLaneByNum(int laneNum)
        {
            for (var i = 0; i < lanes.Count; i++)
            {
                if (lanes[i].laneNum == laneNum)
                {
                    return lanes[i];
                }
            }

            return null;
        }
    }
    class TrafficRoadRestrictions
    {
        public enum VehicleType
        {
            Car,
            Service,
            Transport,
            Cargo
        }

        public static Dictionary<int, SegmentRestrictions> segments = new Dictionary<int, SegmentRestrictions>();

        public static void addSegment(int segmentid, List<int> segmentGroup)
        {
            segments.Add(segmentid, new SegmentRestrictions(segmentid, segmentGroup));
        }

        public static void removeSegment(int segmentid)
        {
            segments.Remove(segmentid);
        }

        public static SegmentRestrictions getSegment(int segmentid)
        {
            return segments[segmentid];
        }

        public static bool isSegment(int segmentid)
        {
            return segments.ContainsKey(segmentid);
        }

        public static VehicleType vehicleType(byte simulationFlags)
        {
            if ((simulationFlags & 140) == 140)
                return VehicleType.Transport;
            if ((simulationFlags & 138) == 138)
                return VehicleType.Service;
            if ((simulationFlags & 136) == 136)
                return VehicleType.Cargo;
            if ((simulationFlags & 134) == 134)
                return VehicleType.Service;
            if ((simulationFlags & 132) == 132)
                return VehicleType.Service;
            if ((simulationFlags & 130) == 130)
                return VehicleType.Service;
            if ((simulationFlags & 128) == 128)
                return VehicleType.Car;
            
            return VehicleType.Car;
        }
    }
}
