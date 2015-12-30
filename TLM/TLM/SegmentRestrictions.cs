using System.Collections.Generic;
using System.Linq;

namespace TrafficManager
{
    class SegmentRestrictions
    {
        public ushort SegmentId { get; }

        public readonly float[] SpeedLimits = {0f, 0f, 0f, 0f, 0f, 0f,0f,0f,0f,0f,0f,0f,0f,0f,0f,0f};

        private readonly List<LaneRestrictions> _lanes = new List<LaneRestrictions>();

        public readonly List<ushort> SegmentGroup;

        public SegmentRestrictions(ushort segmentid, IEnumerable<ushort> segmentGroup )
        {
            SegmentId = segmentid;
            SegmentGroup = new List<ushort>(segmentGroup);
        }

        public void AddLane(uint lane, int lanenum, NetInfo.Direction dir)
        {
            _lanes.Add(new LaneRestrictions(lane, lanenum, dir));
        }

        public LaneRestrictions GetLane(int lane)
        {
            return _lanes[lane];
        }

        public LaneRestrictions GetLaneByNum(int laneNum)
        {
            return _lanes.FirstOrDefault(laneRestriction => laneRestriction.LaneNum == laneNum);
        }
    }
}
