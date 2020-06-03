namespace TrafficManager.Util.Record {
    using CSUtil.Commons;
    using System.Collections.Generic;
    using TrafficManager.API.Traffic;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;

    class SegmentEndRecord : IRecordable {
        public ushort SegmentId { get; private set; }
        public bool StartNode { get; private set; }

        public SegmentEndRecord(int segmentEndIndex) {
            SegmentEndManager.Instance.
                GetSegmentAndNodeFromIndex(segmentEndIndex, out ushort segmentId, out bool startNode);
            SegmentId = segmentId;
            StartNode = startNode;
        }

        private TernaryBool uturnAllowed_;
        private TernaryBool nearTurnOnRedAllowed_;
        private TernaryBool farTurnOnRedAllowed_;
        private TernaryBool laneChangingAllowedWhenGoingStraight_;
        private TernaryBool enteringBlockedJunctionAllowed_;
        private TernaryBool pedestrianCrossingAllowed_;

        private PriorityType prioirtySign_;
        private List<LaneArrowsRecord> lanes_;

        private static TrafficPriorityManager priorityMan => TrafficPriorityManager.Instance;
        private static JunctionRestrictionsManager JRMan => JunctionRestrictionsManager.Instance;

        public void Record() {
            uturnAllowed_ = JRMan.GetUturnAllowed(SegmentId, StartNode);
            nearTurnOnRedAllowed_ = JRMan.GetNearTurnOnRedAllowed(SegmentId, StartNode);
            farTurnOnRedAllowed_ = JRMan.GetFarTurnOnRedAllowed(SegmentId, StartNode);
            laneChangingAllowedWhenGoingStraight_ = JRMan.GetLaneChangingAllowedWhenGoingStraight(SegmentId, StartNode);
            enteringBlockedJunctionAllowed_ = JRMan.GetEnteringBlockedJunctionAllowed(SegmentId, StartNode);
            pedestrianCrossingAllowed_ = JRMan.GetPedestrianCrossingAllowed(SegmentId, StartNode);

            prioirtySign_ = priorityMan.GetPrioritySign(SegmentId, StartNode);

            lanes_ = LaneArrowsRecord.GetLanes(SegmentId, StartNode);
            foreach(IRecordable lane in lanes_) 
                lane.Record();
        }

        public void Restore() {
            foreach (IRecordable lane in lanes_)
                lane.Restore();

            if (priorityMan.MaySegmentHavePrioritySign(SegmentId, StartNode) &&
                prioirtySign_ != priorityMan.GetPrioritySign(SegmentId, StartNode)) {
                //TODO fix manager code.
                priorityMan.SetPrioritySign(SegmentId, StartNode, prioirtySign_);
            }

            // all necessary checks are performed internally.
            JRMan.SetUturnAllowed(SegmentId, StartNode, uturnAllowed_);
            JRMan.SetNearTurnOnRedAllowed(SegmentId, StartNode, nearTurnOnRedAllowed_);
            JRMan.SetFarTurnOnRedAllowed(SegmentId, StartNode, farTurnOnRedAllowed_);
            JRMan.SetLaneChangingAllowedWhenGoingStraight(SegmentId, StartNode, laneChangingAllowedWhenGoingStraight_);
            JRMan.SetEnteringBlockedJunctionAllowed(SegmentId, StartNode, enteringBlockedJunctionAllowed_);
            JRMan.SetPedestrianCrossingAllowed(SegmentId, StartNode, pedestrianCrossingAllowed_);
        }
    }
}
