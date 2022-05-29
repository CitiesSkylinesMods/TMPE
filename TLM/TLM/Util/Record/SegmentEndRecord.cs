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
            uturnAllowed_ = ToTernaryBool(JRMan.GetValueOrDefault(SegmentId, StartNode, JunctionRestrictionsFlags.AllowUTurn));
            nearTurnOnRedAllowed_ = ToTernaryBool(JRMan.GetValueOrDefault(SegmentId, StartNode, JunctionRestrictionsFlags.AllowNearTurnOnRed));
            farTurnOnRedAllowed_ = ToTernaryBool(JRMan.GetValueOrDefault(SegmentId, StartNode, JunctionRestrictionsFlags.AllowFarTurnOnRed));
            laneChangingAllowedWhenGoingStraight_ = ToTernaryBool(JRMan.GetValueOrDefault(SegmentId, StartNode, JunctionRestrictionsFlags.AllowForwardLaneChange));
            enteringBlockedJunctionAllowed_ = ToTernaryBool(JRMan.GetValueOrDefault(SegmentId, StartNode, JunctionRestrictionsFlags.AllowEnterWhenBlocked));
            pedestrianCrossingAllowed_ = ToTernaryBool(JRMan.GetValueOrDefault(SegmentId, StartNode, JunctionRestrictionsFlags.AllowPedestrianCrossing));

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
            JRMan.SetValue(SegmentId, StartNode, JunctionRestrictionsFlags.AllowUTurn, ToOptBool(uturnAllowed_));
            JRMan.SetValue(SegmentId, StartNode, JunctionRestrictionsFlags.AllowNearTurnOnRed, ToOptBool(nearTurnOnRedAllowed_));
            JRMan.SetValue(SegmentId, StartNode, JunctionRestrictionsFlags.AllowFarTurnOnRed, ToOptBool(farTurnOnRedAllowed_));
            JRMan.SetValue(SegmentId, StartNode, JunctionRestrictionsFlags.AllowForwardLaneChange, ToOptBool(laneChangingAllowedWhenGoingStraight_));
            JRMan.SetValue(SegmentId, StartNode, JunctionRestrictionsFlags.AllowEnterWhenBlocked, ToOptBool(enteringBlockedJunctionAllowed_));
            JRMan.SetValue(SegmentId, StartNode, JunctionRestrictionsFlags.AllowPedestrianCrossing, ToOptBool(pedestrianCrossingAllowed_));
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
            JRMan.SetValue(segmentId, StartNode, JunctionRestrictionsFlags.AllowUTurn, ToOptBool(uturnAllowed_));
            JRMan.SetValue(segmentId, StartNode, JunctionRestrictionsFlags.AllowNearTurnOnRed, ToOptBool(nearTurnOnRedAllowed_));
            JRMan.SetValue(segmentId, StartNode, JunctionRestrictionsFlags.AllowFarTurnOnRed, ToOptBool(farTurnOnRedAllowed_));
            JRMan.SetValue(segmentId, StartNode, JunctionRestrictionsFlags.AllowForwardLaneChange, ToOptBool(laneChangingAllowedWhenGoingStraight_));
            JRMan.SetValue(segmentId, StartNode, JunctionRestrictionsFlags.AllowEnterWhenBlocked, ToOptBool(enteringBlockedJunctionAllowed_));
            JRMan.SetValue(segmentId, StartNode, JunctionRestrictionsFlags.AllowPedestrianCrossing, ToOptBool(pedestrianCrossingAllowed_));
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
