namespace TrafficManager.Util.Record {
    using CitiesGameBridge.Service;
    using GenericGameBridge.Service;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;

    [Serializable]
    public class SegmentRecord : IRecordable {
        public SegmentRecord(ushort segmentId) => SegmentId = segmentId;

        public ushort SegmentId { get; private set; }
        InstanceID InstanceID => new InstanceID { NetSegment = SegmentId };

        private bool parkingForward_;
        private bool parkingBackward_;

        private List<SpeedLimitLaneRecord> speedLanes_; 
        private List<VehicleRestrictionsLaneRecord> vehicleRestrictionsLanes_; 
        private List<uint> allLaneIds_; // store lane ids to help with transfering lanes.

        private static ParkingRestrictionsManager pMan => ParkingRestrictionsManager.Instance;

        public void Record() {
            parkingForward_ = pMan.IsParkingAllowed(SegmentId, NetInfo.Direction.Forward);
            parkingBackward_ = pMan.IsParkingAllowed(SegmentId, NetInfo.Direction.Backward);
            speedLanes_ = SpeedLimitLaneRecord.GetLanes(SegmentId);
            foreach (var lane in speedLanes_)
                lane.Record();
            vehicleRestrictionsLanes_ = VehicleRestrictionsLaneRecord.GetLanes(SegmentId);
            foreach (var lane in vehicleRestrictionsLanes_)
                lane.Record();
            allLaneIds_ = GetAllLanes(SegmentId);
        }

        public void Restore() {
            // TODO fix SetParkingAllowed 
            pMan.SetParkingAllowed(SegmentId, NetInfo.Direction.Forward, parkingForward_);
            pMan.SetParkingAllowed(SegmentId, NetInfo.Direction.Backward, parkingBackward_);
            foreach (var lane in speedLanes_)
                lane.Restore();
            foreach (var lane in vehicleRestrictionsLanes_)
                lane.Restore();
        }

        public void Transfer(Dictionary<InstanceID, InstanceID> map){
            ushort segmentId = map[InstanceID].NetSegment;
            pMan.SetParkingAllowed(segmentId, NetInfo.Direction.Forward, parkingForward_);
            pMan.SetParkingAllowed(segmentId, NetInfo.Direction.Backward, parkingBackward_);
            foreach (var lane in speedLanes_)
                lane.Transfer(map);
            foreach (var lane in vehicleRestrictionsLanes_)
                lane.Transfer(map);
        }

        public byte[] Serialize() => SerializationUtil.Serialize(this);

        /// <summary>
        /// creates 1:1 map between lanes of original segment and new segment.
        /// Precondition: SegmentInfos must match.
        /// Precondition: segment must have been recorded.
        /// </summary>
        /// <param name="target">required to exit</param>
        public void MapLanes(Dictionary<InstanceID, InstanceID> map, ushort target) {
            var mappedLanes = GetAllLanes(target);
            Shortcuts.Assert(allLaneIds_ != null && allLaneIds_.Count == mappedLanes.Count);
            for (int i = 0; i < mappedLanes.Count; ++i) {
                var instaceID0 = new InstanceID { NetLane = allLaneIds_[i]};
                var instaceID = new InstanceID { NetLane = mappedLanes[i]};
                map[instaceID0] = instaceID;
            }
        }

        public static List<uint> GetAllLanes(ushort segmentId) {
            var lanes =  NetService.Instance.GetSortedLanes(
                segmentId,
                ref segmentId.ToSegment(),
                startNode: null,
                sort: false);
            return lanes.Select(lane => lane.laneId).ToList();
        }
    }
}
