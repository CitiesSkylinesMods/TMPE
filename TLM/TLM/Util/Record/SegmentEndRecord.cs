namespace TrafficManager.Util.Record {
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using TrafficManager.API.Traffic;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Util.Extensions;

    [Serializable]
    public class SegmentEndRecord : IRecordable {
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
        private InstanceID InstanceID => new InstanceID { NetSegment = SegmentId };

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

        public bool IsDefault() {
            return
                uturnAllowed_ == TernaryBool.Undefined &&
                nearTurnOnRedAllowed_ == TernaryBool.Undefined &&
                farTurnOnRedAllowed_ == TernaryBool.Undefined &&
                laneChangingAllowedWhenGoingStraight_ == TernaryBool.Undefined &&
                enteringBlockedJunctionAllowed_ == TernaryBool.Undefined &&
                pedestrianCrossingAllowed_ == TernaryBool.Undefined &&
                prioirtySign_ == PriorityType.None &&
                arrowLanes_.AreDefault();
        }

        public void Record() {
            uturnAllowed_ = JRMan.GetUturnAllowed(SegmentId, StartNode);
            nearTurnOnRedAllowed_ = JRMan.GetNearTurnOnRedAllowed(SegmentId, StartNode);
            farTurnOnRedAllowed_ = JRMan.GetFarTurnOnRedAllowed(SegmentId, StartNode);
            laneChangingAllowedWhenGoingStraight_ = JRMan.GetLaneChangingAllowedWhenGoingStraight(SegmentId, StartNode);
            enteringBlockedJunctionAllowed_ = JRMan.GetEnteringBlockedJunctionAllowed(SegmentId, StartNode);
            pedestrianCrossingAllowed_ = JRMan.GetPedestrianCrossingAllowed(SegmentId, StartNode);

            prioirtySign_ = priorityMan.GetPrioritySign(SegmentId, StartNode);

            arrowLanes_ = LaneArrowsRecord.GetLanes(SegmentId, StartNode);
            foreach(IRecordable lane in arrowLanes_.EmptyIfNull()) 
                lane?.Record();
        }

        public void Restore() {
            foreach (IRecordable lane in arrowLanes_.EmptyIfNull())
                lane?.Restore();

            if (priorityMan.MaySegmentHavePrioritySign(SegmentId, StartNode) &&
                prioirtySign_ != priorityMan.GetPrioritySign(SegmentId, StartNode)) {
                //TODO fix manager code so that all necessary checks are performed internally. 
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

        public void Transfer(Dictionary<InstanceID, InstanceID> map) {
            ushort segmentId = map[InstanceID].NetSegment;
            foreach (IRecordable lane in arrowLanes_.EmptyIfNull())
                lane?.Transfer(map);

            if (priorityMan.MaySegmentHavePrioritySign(segmentId, StartNode) &&
                prioirtySign_ != priorityMan.GetPrioritySign(segmentId, StartNode)) {
                priorityMan.SetPrioritySign(segmentId, StartNode, prioirtySign_);
            }

            // all necessary checks are performed internally.
            JRMan.SetUturnAllowed(segmentId, StartNode, uturnAllowed_);
            JRMan.SetNearTurnOnRedAllowed(segmentId, StartNode, nearTurnOnRedAllowed_);
            JRMan.SetFarTurnOnRedAllowed(segmentId, StartNode, farTurnOnRedAllowed_);
            JRMan.SetLaneChangingAllowedWhenGoingStraight(segmentId, StartNode, laneChangingAllowedWhenGoingStraight_);
            JRMan.SetEnteringBlockedJunctionAllowed(segmentId, StartNode, enteringBlockedJunctionAllowed_);
            JRMan.SetPedestrianCrossingAllowed(segmentId, StartNode, pedestrianCrossingAllowed_);
        }

        public void Transfer(uint mappedId) {
            ushort segmentId = (ushort)mappedId;

            var mappedLanes = SpeedLimitLaneRecord.GetLanes(segmentId);
            int n = arrowLanes_?.Count ?? 0;
            for (int i = 0; i == n; ++i) {
                arrowLanes_[i]?.Transfer(mappedLanes[i].LaneId);
            }
        }

        public byte[] Serialize() => SerializationUtil.Serialize(this);
    }
}
