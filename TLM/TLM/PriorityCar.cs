namespace TrafficManager
{
    class PriorityCar
    {
        public CarState CarState = CarState.None;

        public int WaitTime = 0;

        public ushort ToNode;
        public int FromSegment;
        public int ToSegment;
        public uint ToLaneId;
        public uint FromLaneId;
        public ushort FromLaneFlags;
        public float LastSpeed;
        public float ReduceSpeedByValueToYield;
        public bool Stopped = false;

        public uint LastFrame;

        public void ResetCar()
        {
            ToNode = 0;
            FromSegment = 0;
            ToSegment = 0;
            ToLaneId = 0;
            FromLaneId = 0;
            FromLaneFlags = 0;
            Stopped = false;

            WaitTime = 0;
            CarState = CarState.None;
        }
    }
}
