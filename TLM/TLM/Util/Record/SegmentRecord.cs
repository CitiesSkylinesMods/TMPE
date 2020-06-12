namespace TrafficManager.Util.Record {
    using CSUtil.Commons;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;

    // TODO record vehicle restrictions.
    public class SegmentRecord : IRecordable {
        public SegmentRecord(ushort segmentId) => SegmentId = segmentId;

        public ushort SegmentId { get; private set; }
        
        private bool parkingForward_;
        private bool parkingBackward_;

        private List<SpeedLimitLaneRecord> lanes_;

        private static ParkingRestrictionsManager pMan => ParkingRestrictionsManager.Instance;

        public void Record() {
            parkingForward_ = pMan.IsParkingAllowed(SegmentId, NetInfo.Direction.Forward);
            parkingBackward_ = pMan.IsParkingAllowed(SegmentId, NetInfo.Direction.Backward);
            lanes_ = SpeedLimitLaneRecord.GetLanes(SegmentId);
            foreach (var lane in lanes_)
                lane.Record();
        }

        public void Restore() {
            // TODO fix SetParkingAllowed 
            pMan.SetParkingAllowed(SegmentId, NetInfo.Direction.Forward, parkingForward_);
            pMan.SetParkingAllowed(SegmentId, NetInfo.Direction.Backward, parkingBackward_);
            foreach (var lane in lanes_)
                lane.Restore();
        }
    }
}
