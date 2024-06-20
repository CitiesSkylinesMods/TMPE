namespace TrafficManager.API.Traffic.Data {
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Auto)]
    public struct LaneTrafficData {
        /// <summary>
        /// Number of seen vehicles since last speed measurement
        /// </summary>
        public ushort trafficBuffer;

        /// <summary>
        /// Number of seen vehicles before last speed measurement
        /// </summary>
        public ushort lastTrafficBuffer;

        /// <summary>
        /// All-time max. traffic buffer
        /// </summary>
        public ushort maxTrafficBuffer;

        /// <summary>
        /// Accumulated speeds since last traffic measurement
        /// </summary>
        public uint accumulatedSpeeds;

        /// <summary>
        /// Current lane mean speed, per ten thousands
        /// </summary>
        public ushort meanSpeed;

        public override string ToString() {
            return string.Format(
                "[LaneTrafficData\n\ttrafficBuffer = {0}\n\tlastTrafficBuffer = {1}\n" +
                "\tmaxTrafficBuffer = {2}\n\ttrafficBuffer = {3}\n\taccumulatedSpeeds = {4}\n" +
                "\tmeanSpeed = {5}\nLaneTrafficData]",
                trafficBuffer,
                lastTrafficBuffer,
                maxTrafficBuffer,
                trafficBuffer,
                accumulatedSpeeds,
                meanSpeed);
        }
    }
}