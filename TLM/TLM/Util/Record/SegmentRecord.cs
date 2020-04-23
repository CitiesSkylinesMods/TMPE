namespace TrafficManager.Util.Record {
    using ColossalFramework;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;
    using static TrafficManager.Util.Shortcuts;

    public class SegmentRecord : IRecordable {
        public ushort SegmentId { get; private set;}
        public SegmentRecord(ushort segmentId) => SegmentId = segmentId;

        private bool ParkingForward;
        private bool ParkingBackward;
        private List<LaneRecord> Lanes;

        private static ParkingRestrictionsManager pMan => ParkingRestrictionsManager.Instance;

        public void Record() {
            ParkingForward = pMan.IsParkingAllowed(SegmentId, NetInfo.Direction.Forward);
            ParkingBackward = pMan.IsParkingAllowed(SegmentId, NetInfo.Direction.Backward);
            Lanes = LaneRecord.GetLanes(SegmentId);
            foreach (LaneRecord lane in Lanes) {
                lane.Record();
            }
        }

        public void Restore() {
            // TODO fix SetParkingAllowed 
            pMan.SetParkingAllowed(SegmentId, NetInfo.Direction.Forward, ParkingForward);
            pMan.SetParkingAllowed(SegmentId, NetInfo.Direction.Backward, ParkingBackward);
            Lanes = LaneRecord.GetLanes(SegmentId);
            foreach (LaneRecord lane in Lanes) {
                lane.Restore();
            }
        }
    }
}
