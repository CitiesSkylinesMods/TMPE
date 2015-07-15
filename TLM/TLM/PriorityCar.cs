namespace TrafficManager
{
    class PriorityCar
    {
        public enum CarState
        {
            None,
            Enter,
            Transit,
            Stop,
            Leave
        }

        public CarState carState = CarState.None;

        public int waitTime = 0;

        public ushort toNode;
        public int fromSegment;
        public int toSegment;
        public uint toLaneID;
        public uint fromLaneID;
        public ushort fromLaneFlags;
        public float lastSpeed;
        public float yieldSpeedReduce;
        public bool stopped = false;

        public uint lastFrame;

        public void resetCar()
        {
            toNode = 0;
            fromSegment = 0;
            toSegment = 0;
            toLaneID = 0;
            fromLaneID = 0;
            fromLaneFlags = 0;
            stopped = false;

            waitTime = 0;
            carState = CarState.None;
        }
    }
}
