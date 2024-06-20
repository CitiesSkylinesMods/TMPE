namespace TrafficManager.API.Traffic.Data {
    using System.Runtime.InteropServices;
    using TrafficManager.API.Traffic.Enums;

    [StructLayout(LayoutKind.Auto)]
    public struct PathUnitQueueItem {
        public uint nextPathUnitId; // access requires acquisition of CustomPathFind.QueueLock
        public ExtVehicleType vehicleType; // access requires acquisition of m_bufferLock
        public ExtPathType pathType; // access requires acquisition of m_bufferLock
        public ushort vehicleId; // access requires acquisition of m_bufferLock
        public bool queued; // access requires acquisition of m_bufferLock
        public bool spawned; // access requires acquisition of m_bufferLock

        //public void Reset() {
        //	vehicleType = ExtVehicleType.None;
        //	pathType = ExtPathType.None;
        //	vehicleId = 0;
        //}

        public override string ToString() {
            return string.Format(
                "[PathUnitQueueItem\n\tnextPathUnitId={0}\n\tvehicleType={1}\n\tpathType={2}\n\t" +
                "vehicleId={3}\n\tqueued={4}\n\tspawned={5}\nPathUnitQueueItem]",
                nextPathUnitId,
                vehicleType,
                pathType,
                vehicleId,
                queued,
                spawned);
        }
    }
}