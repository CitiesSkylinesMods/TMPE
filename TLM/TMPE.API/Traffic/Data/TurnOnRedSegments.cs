namespace TrafficManager.API.Traffic.Data {
    using System.Runtime.InteropServices;

    /// <summary>
    /// Holds left/right turn-on-red candidate segments
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct TurnOnRedSegments {
        /// <summary>
        /// Left segment id (or 0 if no left turn-on-red candidate segment)
        /// </summary>
        public ushort leftSegmentId;

        /// <summary>
        /// Right segment id (or 0 if no right turn-on-red candidate segment)
        /// </summary>
        public ushort rightSegmentId;

        public void Reset() {
            leftSegmentId = 0;
            rightSegmentId = 0;
        }

        public override string ToString() {
            return string.Format(
                "[TurnOnRedSegments {0}\n\tleftSegmentId = {1}\n\trightSegmentId = {2}\nSegmentEnd]",
                base.ToString(),
                leftSegmentId,
                rightSegmentId);
        }
    }
}