namespace TrafficManager.Manager.Impl.LaneConnection {
    using System;
    using System.Collections.Generic;
    using TrafficManager.Util.Extensions;

    internal readonly struct LaneEnd : IEquatable<LaneEnd> {
        private readonly uint laneId_;
        private readonly bool startNode_;
        internal readonly uint LaneId => laneId_;
        internal readonly bool StartNode => startNode_;

        public LaneEnd(uint laneId, bool startNode) {
            laneId_ = laneId;
            startNode_ = startNode;
        }

        public LaneEnd(uint laneId, ushort nodeId) {
            laneId_ = laneId;
            startNode_ = laneId.ToLane().IsStartNode(nodeId);
        }

        public override int GetHashCode() {
            unchecked {
                return ((int)laneId_ * 397) ^ startNode_.GetHashCode();
            }
        }

        public bool Equals(LaneEnd other) {
            return laneId_ == other.laneId_ && startNode_ == other.startNode_;
        }

        public static IEqualityComparer<LaneEnd> LaneIdStartNodeComparer { get; } = new LaneIdStartNodeEqualityComparer();

        private sealed class LaneIdStartNodeEqualityComparer : IEqualityComparer<LaneEnd> {
            public bool Equals(LaneEnd x, LaneEnd y) => x.Equals(y);

            public int GetHashCode(LaneEnd obj) => obj.GetHashCode();

        }
    }
}
