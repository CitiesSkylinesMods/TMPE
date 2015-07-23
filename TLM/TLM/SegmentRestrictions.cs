using System.Collections.Generic;
using System.Linq;

namespace TrafficManager
{
    class SegmentRestrictions
    {
        private int _segmentId;

        public float[] SpeedLimits = new float[16] {0f, 0f, 0f, 0f, 0f, 0f,0f,0f,0f,0f,0f,0f,0f,0f,0f,0f};

        public List<LaneRestrictions> Lanes = new List<LaneRestrictions>();

        public List<int> SegmentGroup;

        public SegmentRestrictions(int segmentid, List<int> segmentGroup )
        {
            _segmentId = segmentid;
            SegmentGroup = new List<int>(segmentGroup);
        }

        public void AddLane(uint lane, int lanenum, NetInfo.Direction dir)
        {
            Lanes.Add(new LaneRestrictions(lane, lanenum, dir));
        }

        public LaneRestrictions GetLane(int lane)
        {
            return Lanes[lane];
        }

        public LaneRestrictions GetLaneByNum(int laneNum)
        {
            return Lanes.FirstOrDefault(laneRestriction => laneRestriction.LaneNum == laneNum);
        }
    }
}
