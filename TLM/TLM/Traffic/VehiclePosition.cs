namespace TrafficManager.Traffic {
	public class VehiclePosition {
		public CarState CarState = CarState.None;

		public int WaitTime = 0;

		public ushort ToNode;
		public ushort FromSegment;
		public ushort ToSegment;
		public uint ToLaneId;
		public uint ToLaneIndex;
		public uint FromLaneId;
		public uint FromLaneIndex;
		public float ReduceSpeedByValueToYield;
		public bool Stopped = false;

		public uint LastFrame;

		public void ResetCar() {
			ToNode = 0;
			FromSegment = 0;
			ToSegment = 0;
			ToLaneId = 0;
			ToLaneIndex = 0;
			FromLaneId = 0;
			FromLaneIndex = 0;
			Stopped = false;

			WaitTime = 0;
			CarState = CarState.None;
		}
	}
}
