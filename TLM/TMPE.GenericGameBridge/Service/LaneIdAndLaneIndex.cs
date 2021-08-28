namespace GenericGameBridge.Service {
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Auto)]
    public readonly struct LaneIdAndLaneIndex {
        public readonly uint laneId;
        public readonly int laneIndex;

        public LaneIdAndLaneIndex(uint laneId, int laneIndex) {
            this.laneId = laneId;
            this.laneIndex = laneIndex;
        }
    }
}