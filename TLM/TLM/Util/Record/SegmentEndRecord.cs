namespace TrafficManager.Util.Record {
    using CSUtil.Commons;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;

    class SegmentEndRecord : IRecordable {
        public ushort SegmentId { get; private set; }
        public bool StartNode { get; private set; }
        public SegmentEndRecord(int segmentEndIndex) {
            var segmentEnd = SegmentEndManager.Instance.GetSegmentEnd(segmentEndIndex);
            SegmentId = segmentEnd.SegmentId;
            StartNode = segmentEnd.StartNode;
        }

        private TernaryBool UturnAllowed;
        private TernaryBool NearTurnOnRedAllowed;
        private TernaryBool FarTurnOnRedAllowed;
        private TernaryBool LaneChangingAllowedWhenGoingStraight;
        private TernaryBool EnteringBlockedJunctionAllowed;
        private TernaryBool PedestrianCrossingAllowed;

        private PriorityType PrioirtySign;

        private static TrafficPriorityManager priorityMan => TrafficPriorityManager.Instance;
        private static JunctionRestrictionsManager JRMan => JunctionRestrictionsManager.Instance;

        public void Record() {
            PrioirtySign = priorityMan.GetPrioritySign(SegmentId, StartNode);
            UturnAllowed = JRMan.GetUturnAllowed(SegmentId, StartNode);
            NearTurnOnRedAllowed = JRMan.GetNearTurnOnRedAllowed(SegmentId, StartNode);
            FarTurnOnRedAllowed = JRMan.GetFarTurnOnRedAllowed(SegmentId, StartNode);
            LaneChangingAllowedWhenGoingStraight = JRMan.GetLaneChangingAllowedWhenGoingStraight(SegmentId, StartNode);
            EnteringBlockedJunctionAllowed = JRMan.GetEnteringBlockedJunctionAllowed(SegmentId, StartNode);
            PedestrianCrossingAllowed = JRMan.GetPedestrianCrossingAllowed(SegmentId, StartNode);
        }

        public void Restore() {
            if (priorityMan.MaySegmentHavePrioritySign(SegmentId, StartNode) &&
                PrioirtySign != priorityMan.GetPrioritySign(SegmentId, StartNode)) {
                //TODO fix manager code.
                priorityMan.SetPrioritySign(SegmentId, StartNode, PrioirtySign);
            }

            // all necessary checks are performed internally.
            JRMan.SetUturnAllowed(SegmentId, StartNode, UturnAllowed);
            JRMan.SetNearTurnOnRedAllowed(SegmentId, StartNode, NearTurnOnRedAllowed);
            JRMan.SetFarTurnOnRedAllowed(SegmentId, StartNode, FarTurnOnRedAllowed);
            JRMan.SetLaneChangingAllowedWhenGoingStraight(SegmentId, StartNode, LaneChangingAllowedWhenGoingStraight);
            JRMan.SetEnteringBlockedJunctionAllowed(SegmentId, StartNode, EnteringBlockedJunctionAllowed);
            JRMan.SetPedestrianCrossingAllowed(SegmentId, StartNode, PedestrianCrossingAllowed);
        }
    }
}
