namespace TrafficManager.Traffic
{
    class TrafficSegment
    {
        public ushort Node1 = 0;
        public ushort Node2 = 0;

        public int Segment = 0;

        public PrioritySegment Instance1;
        public PrioritySegment Instance2;
    }
}