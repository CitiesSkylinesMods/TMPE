namespace TrafficManager.API.Traffic.Data {
    public struct SegmentDirTrafficData {
        public ushort meanSpeed;

        public override string ToString() {
            return string.Format(
                "[SegmentDirTrafficData\n\tmeanSpeed = {0}\nSegmentDirTrafficData]",
                meanSpeed);
        }
    }
}