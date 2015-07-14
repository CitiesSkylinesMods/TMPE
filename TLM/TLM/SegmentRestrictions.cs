using System.Collections.Generic;

namespace TrafficManager
{
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
}