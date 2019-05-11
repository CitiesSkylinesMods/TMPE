using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using TrafficManager.State;

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
			public int currentStep;
			public List<TimedTrafficLightsStep> timedSteps;
		}

		[Serializable]
		public class TimedTrafficLightsStep {
			public int minTime;
			public int maxTime;
			public int changeMetric;
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
		public class ParkingRestriction {
			public ushort segmentId;
			public bool forwardParkingAllowed;
			public bool backwardParkingAllowed;

			public ParkingRestriction(ushort segmentId) {
				this.segmentId = segmentId;
				forwardParkingAllowed = true;
				backwardParkingAllowed = true;
			}
		}
		
		[Serializable]
		public class SegmentNodeFlags {
			public bool? uturnAllowed = null;
            public bool? turnOnRedAllowed = null; // controls near turns // TODO fix naming when the serialization system is updated
			public bool? farTurnOnRedAllowed = null;
			public bool? straightLaneChangingAllowed = null;
			public bool? enterWhenBlockedAllowed = null;
			public bool? pedestrianCrossingAllowed = null;

			public bool IsDefault() {
				bool uturnIsDefault = uturnAllowed == null || (bool)uturnAllowed == Options.allowUTurns;
                bool turnOnRedIsDefault = turnOnRedAllowed == null || (bool)turnOnRedAllowed;
				bool farTurnOnRedIsDefault = farTurnOnRedAllowed == null || (bool)farTurnOnRedAllowed;
				bool straightChangeIsDefault = straightLaneChangingAllowed == null || (bool)straightLaneChangingAllowed == Options.allowLaneChangesWhileGoingStraight;
				bool enterWhenBlockedIsDefault = enterWhenBlockedAllowed == null || (bool)enterWhenBlockedAllowed == Options.allowEnterBlockedJunctions;
				bool pedCrossingIsDefault = pedestrianCrossingAllowed == null || (bool)pedestrianCrossingAllowed;

				return uturnIsDefault && turnOnRedIsDefault && farTurnOnRedIsDefault && straightChangeIsDefault && enterWhenBlockedIsDefault && pedCrossingIsDefault;
			}

			public override string ToString() {
				return $"uturnAllowed={uturnAllowed}, turnOnRedAllowed={turnOnRedAllowed}, farTurnOnRedAllowed={farTurnOnRedAllowed}, straightLaneChangingAllowed={straightLaneChangingAllowed}, enterWhenBlockedAllowed={enterWhenBlockedAllowed}, pedestrianCrossingAllowed={pedestrianCrossingAllowed}";
			}
		}

		[Serializable]
		public class LaneConnection {
			public uint lowerLaneId;
			public uint higherLaneId;
			public bool lowerStartNode;

			public LaneConnection(uint lowerLaneId, uint higherLaneId, bool lowerStartNode) {
				if (lowerLaneId >= higherLaneId)
					throw new ArgumentException();
				this.lowerLaneId = lowerLaneId;
				this.higherLaneId = higherLaneId;
				this.lowerStartNode = lowerStartNode;
			}
		}

		[Serializable]
		public class LaneArrowData {
			public uint laneId;
			public uint arrows;

			public LaneArrowData(uint laneId, uint arrows) {
				this.laneId = laneId;
				this.arrows = arrows;
			}
		}

		[Serializable]
		public class PrioritySegment {
			public ushort segmentId;
			public ushort nodeId;
			public int priorityType;

			public PrioritySegment(ushort segmentId, ushort nodeId, int priorityType) {
				this.segmentId = segmentId;
				this.nodeId = nodeId;
				this.priorityType = priorityType;
			}
		}

		[Serializable]
		public class NodeTrafficLight {
			public ushort nodeId;
			public bool trafficLight;

			public NodeTrafficLight(ushort nodeId, bool trafficLight) {
				this.nodeId = nodeId;
				this.trafficLight = trafficLight;
			}
		}

		[Serializable]
		public class ExtCitizenInstanceData {
			public uint instanceId;
			public int pathMode;
			public int failedParkingAttempts;
			public ushort parkingSpaceLocationId;
			public int parkingSpaceLocation;
			public ushort parkingPathStartPositionSegment;
			public byte parkingPathStartPositionLane;
			public byte parkingPathStartPositionOffset;
			public uint returnPathId;
			public int returnPathState;
			public float lastDistanceToParkedCar;

			public ExtCitizenInstanceData(uint instanceId) {
				this.instanceId = instanceId;
				pathMode = 0;
				failedParkingAttempts = 0;
				parkingSpaceLocationId = 0;
				parkingSpaceLocation = 0;
				parkingPathStartPositionSegment = 0;
				parkingPathStartPositionLane = 0;
				parkingPathStartPositionOffset = 0;
				returnPathId = 0;
				returnPathState = 0;
				lastDistanceToParkedCar = 0;
			}
		}

		[Serializable]
		public class ExtCitizenData {
			public uint citizenId;
			public int lastTransportMode;

			public ExtCitizenData(uint citizenId) {
				this.citizenId = citizenId;
				lastTransportMode = 0;
			}
		}

		/// <summary>
		/// Stored ext. citizen data
		/// </summary>
		public List<ExtCitizenData> ExtCitizens = new List<ExtCitizenData>();

		/// <summary>
		/// Stored ext. citizen instance data
		/// </summary>
		public List<ExtCitizenInstanceData> ExtCitizenInstances = new List<ExtCitizenInstanceData>();

		/// <summary>
		/// Stored toggled traffic lights
		/// </summary>
		public List<NodeTrafficLight> ToggledTrafficLights = new List<NodeTrafficLight>();

		/// <summary>
		/// Stored lane connections
		/// </summary>
		public List<LaneConnection> LaneConnections = new List<LaneConnection>();

		/// <summary>
		/// Stored lane arrows
		/// </summary>
		public List<LaneArrowData> LaneArrows = new List<LaneArrowData>();

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

		/// <summary>
		/// Custom default speed limits (in game speed units)
		/// </summary>
		public Dictionary<string, float> CustomDefaultSpeedLimits = new Dictionary<string, float>();

		/// <summary>
		/// Priority segments
		/// </summary>
		public List<PrioritySegment> CustomPrioritySegments = new List<PrioritySegment>();

		/// <summary>
		/// Parking restrictions
		/// </summary>
		public List<ParkingRestriction> ParkingRestrictions = new List<ParkingRestriction>();

		[Obsolete]
		public string NodeTrafficLights = "";
		[Obsolete]
		public string NodeCrosswalk = "";
		[Obsolete]
		public string LaneFlags = "";

		[Obsolete]
		public List<int[]> PrioritySegments = new List<int[]>();
		[Obsolete]
		public List<int[]> NodeDictionary = new List<int[]>();
		[Obsolete]
		public List<int[]> ManualSegments = new List<int[]>();

		[Obsolete]
		public List<int[]> TimedNodes = new List<int[]>();
		[Obsolete]
		public List<ushort[]> TimedNodeGroups = new List<ushort[]>();
		[Obsolete]
		public List<int[]> TimedNodeSteps = new List<int[]>();
		[Obsolete]
		public List<int[]> TimedNodeStepSegments = new List<int[]>();
	}
}
