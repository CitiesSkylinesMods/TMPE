namespace TrafficManager.Util.Record {
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using TrafficManager.API.Traffic;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using static CSUtil.Commons.TernaryBoolUtil;

    [Serializable]
    class SegmentEndRecord : IRecordable {
        public SegmentEndRecord(int segmentEndIndex) {
            SegmentEndManager.Instance.
                GetSegmentAndNodeFromIndex(segmentEndIndex, out ushort segmentId, out bool startNode);
            SegmentId = segmentId;
            StartNode = startNode;
        }

        public SegmentEndRecord(ushort segmentId, bool startNode) {
            SegmentId = segmentId;
            StartNode = startNode;
        }

        public ushort SegmentId { get; private set; }
        public bool StartNode { get; private set; }
        InstanceID InstanceID => new InstanceID { NetSegment = SegmentId };

        private TernaryBool uturnAllowed_;
        private TernaryBool nearTurnOnRedAllowed_;
        private TernaryBool farTurnOnRedAllowed_;
        private TernaryBool laneChangingAllowedWhenGoingStraight_;
        private TernaryBool enteringBlockedJunctionAllowed_;
        private TernaryBool pedestrianCrossingAllowed_;

        private PriorityType prioirtySign_;
        private List<LaneArrowsRecord> arrowLanes_;

        private static TrafficPriorityManager priorityMan => TrafficPriorityManager.Instance;
        private static JunctionRestrictionsManager JRMan => JunctionRestrictionsManager.Instance;

        public void Record() {
            uturnAllowed_ = ToTernaryBool(JRMan.GetUturnAllowed(SegmentId, StartNode));
            nearTurnOnRedAllowed_ = ToTernaryBool(JRMan.GetNearTurnOnRedAllowed(SegmentId, StartNode));
            farTurnOnRedAllowed_ = ToTernaryBool(JRMan.GetFarTurnOnRedAllowed(SegmentId, StartNode));
            laneChangingAllowedWhenGoingStraight_ = ToTernaryBool(JRMan.GetLaneChangingAllowedWhenGoingStraight(SegmentId, StartNode));
            enteringBlockedJunctionAllowed_ = ToTernaryBool(JRMan.GetEnteringBlockedJunctionAllowed(SegmentId, StartNode));
            pedestrianCrossingAllowed_ = ToTernaryBool(JRMan.GetPedestrianCrossingAllowed(SegmentId, StartNode));

            prioirtySign_ = priorityMan.GetPrioritySign(SegmentId, StartNode);

            arrowLanes_ = LaneArrowsRecord.GetLanes(SegmentId, StartNode);
            foreach(IRecordable lane in arrowLanes_) 
                lane.Record();
        }

        public void Restore() {
            foreach (IRecordable lane in arrowLanes_)
                lane.Restore();

            if (priorityMan.MaySegmentHavePrioritySign(SegmentId, StartNode) &&
                prioirtySign_ != priorityMan.GetPrioritySign(SegmentId, StartNode)) {
                //TODO fix manager code so that all necessary checks are performed internally. 
                priorityMan.SetPrioritySign(SegmentId, StartNode, prioirtySign_);
            }

            // all necessary checks are performed internally.
            JRMan.SetUturnAllowed(SegmentId, StartNode, ToOptBool(uturnAllowed_));
            JRMan.SetNearTurnOnRedAllowed(SegmentId, StartNode, ToOptBool(nearTurnOnRedAllowed_));
            JRMan.SetFarTurnOnRedAllowed(SegmentId, StartNode, ToOptBool(farTurnOnRedAllowed_));
            JRMan.SetLaneChangingAllowedWhenGoingStraight(SegmentId, StartNode, ToOptBool(laneChangingAllowedWhenGoingStraight_));
            JRMan.SetEnteringBlockedJunctionAllowed(SegmentId, StartNode, ToOptBool(enteringBlockedJunctionAllowed_));
            JRMan.SetPedestrianCrossingAllowed(SegmentId, StartNode, ToOptBool(pedestrianCrossingAllowed_));
        }

        public void Transfer(Dictionary<InstanceID, InstanceID> map) {
            ushort segmentId = map[InstanceID].NetSegment;
            foreach (IRecordable lane in arrowLanes_)
                lane.Transfer(map);

            if (priorityMan.MaySegmentHavePrioritySign(segmentId, StartNode) &&
                prioirtySign_ != priorityMan.GetPrioritySign(segmentId, StartNode)) {
                priorityMan.SetPrioritySign(segmentId, StartNode, prioirtySign_);
            }

            // all necessary checks are performed internally.
            JRMan.SetUturnAllowed(segmentId, StartNode, ToOptBool(uturnAllowed_));
            JRMan.SetNearTurnOnRedAllowed(segmentId, StartNode, ToOptBool(nearTurnOnRedAllowed_));
            JRMan.SetFarTurnOnRedAllowed(segmentId, StartNode, ToOptBool(farTurnOnRedAllowed_));
            JRMan.SetLaneChangingAllowedWhenGoingStraight(segmentId, StartNode, ToOptBool(laneChangingAllowedWhenGoingStraight_));
            JRMan.SetEnteringBlockedJunctionAllowed(segmentId, StartNode, ToOptBool(enteringBlockedJunctionAllowed_));
            JRMan.SetPedestrianCrossingAllowed(segmentId, StartNode, ToOptBool(pedestrianCrossingAllowed_));
        }

        public void Transfer(uint mappedId) {
            ushort segmentId = (ushort)mappedId;

            var mappedLanes = SpeedLimitLaneRecord.GetLanes(segmentId);
            for (int i = 0; i == arrowLanes_.Count; ++i) {
                arrowLanes_[i].Transfer(mappedLanes[i].LaneId);
            }
        }

        public byte[] Serialize() => SerializationUtil.Serialize(this);
    }
}
