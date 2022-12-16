// TODO this class should be moved to TrafficManager.State, but the deserialization fails if we just do that now. Anyway, we should get rid of these crazy lists of arrays. So let's move the class when we decide rework the load/save system.
namespace TrafficManager {
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State;
    using TrafficManager.Traffic;
    using System.Runtime.Serialization;
    using TrafficManager.Lifecycle;
    using Util;
    using LaneEndTransitionGroup = TrafficManager.API.Traffic.Enums.LaneEndTransitionGroup;

    [Serializable]
    public class Configuration {

        public const int CURRENT_VERSION = 3;

        /// <summary>
        /// version at which data was saved
        /// </summary>
        public int Version;

        [Serializable]
        public class LaneSpeedLimit {
            public uint laneId;

            /// <summary>
            /// Unit: km/h.
            /// </summary>
            public ushort speedLimit;

            public LaneSpeedLimit(uint laneId, SpeedValue speed) {
                this.laneId = laneId;
                this.speedLimit = speed.ToKmphPrecise().Kmph;
            }
        }

        [Serializable]
        public class LaneVehicleTypes {
            public uint laneId;

            public LaneVehicleTypes(uint laneId, ExtVehicleType vehicleTypes) {
                this.laneId = laneId;
                this.vehicleTypes = vehicleTypes;
            }

            /// <summary>
            /// Do not use this, for save compatibility only.
            /// </summary>
            [Obsolete]
            public ExtVehicleType vehicleTypes;

            /// <summary>
            /// Use this to access new ExtVehicleType, from TMPE.API
            /// </summary>
            // Property will not be serialized, permit use of obsolete symbol
#pragma warning disable 612
            public API.Traffic.Enums.ExtVehicleType ApiVehicleTypes
                => LegacyExtVehicleType.ToNew(vehicleTypes);
#pragma warning restore 612
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

            /// <summary>
            /// This is using old type for save compatibility.
            /// Use LegacyExtVehicleType helper class to convert between old/new
            /// </summary>
            [Obsolete]
            public Dictionary<ExtVehicleType, CustomSegmentLight> customLights;

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
            public SegmentNodeFlags startNodeFlags;
            public SegmentNodeFlags endNodeFlags;

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
            // TODO fix naming when the serialization system is updated
            public bool? uturnAllowed;
            // controls near turns
            public bool? turnOnRedAllowed;
            public bool? farTurnOnRedAllowed;
            public bool? straightLaneChangingAllowed;
            public bool? enterWhenBlockedAllowed;
            public bool? pedestrianCrossingAllowed;

            [UsedImplicitly]
            public bool IsDefault() {
                // TODO v1.11.0: check this
                bool uturnIsDefault =
                    uturnAllowed == null || (bool)uturnAllowed == SavedGameOptions.Instance.allowUTurns;
                bool turnOnRedIsDefault =
                    turnOnRedAllowed == null || (bool)turnOnRedAllowed;
                bool farTurnOnRedIsDefault =
                    farTurnOnRedAllowed == null || (bool)farTurnOnRedAllowed;
                bool straightChangeIsDefault
                    = straightLaneChangingAllowed == null
                      || (bool)straightLaneChangingAllowed == SavedGameOptions.Instance.allowLaneChangesWhileGoingStraight;
                bool enterWhenBlockedIsDefault =
                    enterWhenBlockedAllowed == null
                    || (bool)enterWhenBlockedAllowed == SavedGameOptions.Instance.allowEnterBlockedJunctions;
                bool pedCrossingIsDefault =
                    pedestrianCrossingAllowed == null || (bool)pedestrianCrossingAllowed;

                return uturnIsDefault && turnOnRedIsDefault && farTurnOnRedIsDefault &&
                       straightChangeIsDefault && enterWhenBlockedIsDefault && pedCrossingIsDefault;
            }

            public override string ToString() {
                return string.Format(
                    "uturnAllowed={0}, turnOnRedAllowed={1}, farTurnOnRedAllowed={2}, " +
                    "straightLaneChangingAllowed={3}, enterWhenBlockedAllowed={4}, " +
                    "pedestrianCrossingAllowed={5}",
                    uturnAllowed,
                    turnOnRedAllowed,
                    farTurnOnRedAllowed,
                    straightLaneChangingAllowed,
                    enterWhenBlockedAllowed,
                    pedestrianCrossingAllowed);
            }
        }

        /// <summary>
        /// in legacy mode connections are always bi-directional.
        /// </summary>
        [Serializable]
        public class LaneConnection : ISerializable {
            public uint sourceLaneId;

            public uint targetLaneId;

            public bool sourceStartNode;

            public LaneEndTransitionGroup group = LaneEndTransitionGroup.Vehicle;

            public bool LegacyBidirectional => SerializableDataExtension.Version < 2;

            public LaneConnection(uint sourceLaneId, uint targetLaneId, bool sourceStartNode, LaneEndTransitionGroup group) {
                this.sourceLaneId = sourceLaneId;
                this.targetLaneId = targetLaneId;
                this.sourceStartNode = sourceStartNode;
                this.group = group;
            }

            //serialization
            public void GetObjectData(SerializationInfo info, StreamingContext context) {
                info.GetObjectFields(this);
            }

            // deserialization
            public LaneConnection(SerializationInfo info, StreamingContext context) {
                foreach(var item in info) {
                    switch(item.Name) {
                        case "lowerLaneId": //legacy
                        case nameof(sourceLaneId):
                            sourceLaneId = (uint)item.Value;
                            break;
                        case "higherLaneId": //legacy
                        case nameof(targetLaneId):
                            targetLaneId = (uint)item.Value;
                            break;
                        case "lowerStartNode": //legacy
                        case nameof(sourceStartNode):
                            sourceStartNode = (bool)item.Value;
                            break;
                        case nameof(group):
                            group = (LaneEndTransitionGroup)item.Value;
                            break;
                    }
                }
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
        public List<TimedTrafficLights> TimedLights = new List<TimedTrafficLights>();

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
        public string NodeTrafficLights = string.Empty;

        [Obsolete("Not used anymore.")]
        [UsedImplicitly]
        public string NodeCrosswalk = string.Empty;

        [Obsolete]
        public string LaneFlags = string.Empty;

        [Obsolete]
        public List<int[]> PrioritySegments = new List<int[]>();

        [Obsolete("Not used anymore.")]
        [UsedImplicitly]
        public List<int[]> NodeDictionary = new List<int[]>();

        [Obsolete("Not used anymore.")]
        [UsedImplicitly]
        public List<int[]> ManualSegments = new List<int[]>();

        [Obsolete("Not used anymore.")]
        [UsedImplicitly]
        public List<int[]> TimedNodes = new List<int[]>();

        [Obsolete("Not used anymore.")]
        [UsedImplicitly]
        public List<ushort[]> TimedNodeGroups = new List<ushort[]>();

        [Obsolete("Not used anymore.")]
        [UsedImplicitly]
        public List<int[]> TimedNodeSteps = new List<int[]>();

        [Obsolete("Not used anymore.")]
        [UsedImplicitly]
        public List<int[]> TimedNodeStepSegments = new List<int[]>();
    }
}