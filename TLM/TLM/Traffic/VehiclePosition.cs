using ColossalFramework;

namespace TrafficManager.Traffic {
	public class VehiclePosition {
		private VehicleJunctionTransitState carState;

		public VehicleJunctionTransitState CarState {
			get { return carState; }
			set {
				//if (value != carState)
					LastCarStateUpdate = Singleton<SimulationManager>.instance.m_currentFrameIndex;
				carState = value;
			}
		}

		public uint LastCarStateUpdate = 0;

		public int WaitTime = 0;

		public ushort ToNode;
		public ushort FromSegment;
		public ushort ToSegment;
		//public uint ToLaneId;
		public uint ToLaneIndex;
		//public uint FromLaneId;
		public uint FromLaneIndex;
		public float ReduceSpeedByValueToYield;
		public bool Stopped = false;
		public bool Valid = false;
		public bool OnEmergency;
		public uint LastFrame;
		public ExtVehicleType VehicleType;

		public VehiclePosition() {
			ResetCar();
		}

		public void ResetCar() {
			Valid = false;
			ToNode = 0;
			FromSegment = 0;
			ToSegment = 0;
			//ToLaneId = 0;
			ToLaneIndex = 0;
			//FromLaneId = 0;
			FromLaneIndex = 0;
			VehicleType = ExtVehicleType.None;
			Stopped = false;
			WaitTime = 0;
			CarState = VehicleJunctionTransitState.None;
			OnEmergency = false;
		}
	}
}
