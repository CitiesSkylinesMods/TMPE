namespace TrafficManager.State.Asset {
    using System;
    using System.Collections.Generic;
    using CSUtil.Commons;
    using TrafficManager.Util;

    [Serializable]
    public class SegmentNetworkIDs {
        public ushort SegmentId;
        public ushort StartNodeId;
        public ushort EndNodeId;
        public uint[] laneIDs;

        public SegmentNetworkIDs(ushort segmentId) {
            SegmentId = segmentId;
            StartNodeId = segmentId.ToSegment().m_startNode;
            EndNodeId = segmentId.ToSegment().m_endNode;

            var lanes = SegmentId.ToSegment().Info.m_lanes;
            uint laneId = SegmentId.ToSegment().m_lanes;
            laneIDs = new uint[lanes.Length];
            for (int laneIndex = 0; laneIndex < lanes.Length && laneId != 0; ++laneIndex) {
                laneIDs[laneIndex] = laneId;
                laneId = laneId.ToLane().m_nextLane;
            }
        }

        /// <summary>
        /// maps old networkids of the old segment to new segment and appends them to the given map dictionary.
        /// </summary>
        public void MapInstanceIDs(ushort newSegmentId, Dictionary<InstanceID, InstanceID> map) {
            map[new InstanceID { NetSegment = SegmentId }] =
                new InstanceID { NetSegment = newSegmentId };

            map[new InstanceID { NetNode = StartNodeId }] =
                new InstanceID { NetNode = newSegmentId.ToSegment().m_startNode };

            map[new InstanceID { NetNode = EndNodeId }] =
                new InstanceID { NetNode = newSegmentId.ToSegment().m_endNode };

            var lanes = SegmentId.ToSegment().Info.m_lanes;
            uint laneId = SegmentId.ToSegment().m_lanes;
            for (int laneIndex = 0; laneIndex < lanes.Length && laneId != 0; ++laneIndex) {
                map[new InstanceID { NetLane = laneIDs[laneIndex] }] =
                    new InstanceID { NetLane = laneId };
                laneId = laneId.ToLane().m_nextLane;
            }

            Log._Debug($"pathInfoExt.MapInstanceIDs: " +
                $"[{StartNodeId} .- {SegmentId} -. {EndNodeId}] -> " +
                $"[{newSegmentId.ToSegment().m_startNode} .- {newSegmentId} -. {newSegmentId.ToSegment().m_endNode}]");
        }

        public override string ToString() =>
            $"pathInfoExt[start:{StartNodeId} segment:{SegmentId} end:{EndNodeId} lanes={laneIDs.ToSTR()}]";
    }
}
