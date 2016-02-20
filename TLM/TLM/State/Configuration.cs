using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml.Serialization;

// TODO this class should be moved to TrafficManager.State, but the deserialization fails if we just do that now. Anyway, we should get rid of these crazy lists of arrays. So let's move the class when we decide rework the load/save system.
namespace TrafficManager {
	[Serializable]
	public class Configuration {
		[Serializable]
		public class LaneSpeedLimit {
			public uint laneId;
			public ushort speedLimit;

			public LaneSpeedLimit(uint laneId, ushort speedLimit) {
				this.laneId = laneId;
				this.speedLimit = speedLimit;
			}
		}

		[Serializable]
		public class LaneVehicleTypes {
			public uint laneId;
			public Traffic.ExtVehicleType vehicleTypes;

			public LaneVehicleTypes(uint laneId, Traffic.ExtVehicleType vehicleTypes) {
				this.laneId = laneId;
				this.vehicleTypes = vehicleTypes;
			}
		}

		[Serializable]
		public class TimedTrafficLights {
			public ushort nodeId;
			public List<ushort> nodeGroup;
			public bool started;
			public List<TimedTrafficLightsStep> timedSteps;
		}

		[Serializable]
		public class TimedTrafficLightsStep {
			public int minTime;
			public int maxTime;
			public float waitFlowBalance;
			public Dictionary<ushort, CustomSegmentLights> segmentLights;
		}

		[Serializable]
		public class CustomSegmentLights {
			public ushort nodeId;
			public ushort segmentId;
			public Dictionary<Traffic.ExtVehicleType, CustomSegmentLight> customLights;
			public RoadBaseAI.TrafficLightState? pedestrianLightState;
			public bool manualPedestrianMode;
		}

		[Serializable]
		public class CustomSegmentLight {
			public ushort nodeId;
			public ushort segmentId;
			public int currentMode;
			public RoadBaseAI.TrafficLightState leftLight;
			public RoadBaseAI.TrafficLightState mainLight;
			public RoadBaseAI.TrafficLightState rightLight;
		}

		[Serializable]
		public class SegmentNodeConf {
			public ushort segmentId;
			public SegmentNodeFlags startNodeFlags = null;
			public SegmentNodeFlags endNodeFlags = null;

			public SegmentNodeConf(ushort segmentId) {
				this.segmentId = segmentId;
			}
		}

		[Serializable]
		public class SegmentNodeFlags {
			public bool? uturnAllowed = null;
			public bool? straightLaneChangingAllowed = null;
			public bool? enterWhenBlockedAllowed = null; 
		}

		public string NodeTrafficLights = ""; // TODO rework
		public string NodeCrosswalk = ""; // TODO rework
		public string LaneFlags = ""; // TODO rework

		/// <summary>
		/// Stored lane speed limits
		/// </summary>
		public List<LaneSpeedLimit> LaneSpeedLimits = new List<LaneSpeedLimit>();

		/// <summary>
		/// Stored vehicle restrictions
		/// </summary>
		public List<LaneVehicleTypes> LaneAllowedVehicleTypes = new List<LaneVehicleTypes>();

		/// <summary>
		/// Timed traffic lights
		/// </summary>
		public List<TimedTrafficLights> TimedLights = new List<Configuration.TimedTrafficLights>();

		/// <summary>
		/// Segment-at-Node configurations
		/// </summary>
		public List<SegmentNodeConf> SegmentNodeConfs = new List<SegmentNodeConf>();

		public List<int[]> PrioritySegments = new List<int[]>(); // TODO rework
		public List<int[]> NodeDictionary = new List<int[]>(); // TODO rework
		public List<int[]> ManualSegments = new List<int[]>(); // TODO rework

		public List<int[]> TimedNodes = new List<int[]>(); // TODO rework
		public List<ushort[]> TimedNodeGroups = new List<ushort[]>(); // TODO rework
		public List<int[]> TimedNodeSteps = new List<int[]>(); // TODO rework
		public List<int[]> TimedNodeStepSegments = new List<int[]>(); // TODO rework
	}
}
