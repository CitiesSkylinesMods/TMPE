namespace TrafficManager.Manager.Impl {
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Auto)]
    public readonly struct LaneIdAndIndex {
        public readonly uint laneId;
        public readonly int laneIndex;

        public LaneIdAndIndex(uint laneId, int laneIndex) {
            this.laneId = laneId;
            this.laneIndex = laneIndex;
        }
    }
}