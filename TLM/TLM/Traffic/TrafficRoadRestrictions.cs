using System.Collections.Generic;

namespace TrafficManager.Traffic
{
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
