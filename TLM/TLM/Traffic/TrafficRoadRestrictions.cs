using System.Collections.Generic;

namespace TrafficManager.Traffic
{
    class TrafficRoadRestrictions
    {
        public static Dictionary<int, SegmentRestrictions> Segments = new Dictionary<int, SegmentRestrictions>();

        public static void AddSegment(int segmentid, List<int> segmentGroup)
        {
            Segments.Add(segmentid, new SegmentRestrictions(segmentid, segmentGroup));
        }

        public static void RemoveSegment(int segmentid)
        {
            Segments.Remove(segmentid);
        }

        public static SegmentRestrictions GetSegment(int segmentid)
        {
            return Segments[segmentid];
        }

        public static bool IsSegment(int segmentid)
        {
            return Segments.ContainsKey(segmentid);
        }

        public static VehicleType VehicleType(byte simulationFlags)
        {
            if ((simulationFlags & 140) == 140)
                return Traffic.VehicleType.Transport;
            if ((simulationFlags & 138) == 138)
                return Traffic.VehicleType.Service;
            if ((simulationFlags & 136) == 136)
                return Traffic.VehicleType.Cargo;
            if ((simulationFlags & 134) == 134)
                return Traffic.VehicleType.Service;
            if ((simulationFlags & 132) == 132)
                return Traffic.VehicleType.Service;
            if ((simulationFlags & 130) == 130)
                return Traffic.VehicleType.Service;
            if ((simulationFlags & 128) == 128)
                return Traffic.VehicleType.Car;
            
            return Traffic.VehicleType.Car;
        }
    }
}
