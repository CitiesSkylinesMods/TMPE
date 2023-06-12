namespace TrafficManager.Custom.PathFinding {
    using ColossalFramework.Math;
    using ColossalFramework.UI;
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Diagnostics;
    using System.Reflection;
    using System.Threading;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Manager;
    using TrafficManager.State;
    using UnityEngine;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using TrafficManager.TrafficLight.Impl;

#if DEBUG
    using System.Collections.Generic;
    using State.ConfigData;
#endif

    /// <summary>
    /// This replaces game PathFind class
    /// This is ALL targets except Benchmark
    /// </summary>
    public class CustomPathFind : PathFind {
        private const float BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR = Constants.BYTE_TO_FLOAT_SCALE;

        private const float TICKET_COST_CONVERSION_FACTOR =
            BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * 0.0001f;

#if ROUTING
        private readonly RoutingManager routingManager = RoutingManager.Instance;
#endif

#if JUNCTIONRESTRICTIONS
        private readonly JunctionRestrictionsManager junctionManager =
            JunctionRestrictionsManager.Instance;
#endif

#if VEHICLERESTRICTIONS
        private readonly VehicleRestrictionsManager vehicleRestrictionsManager =
            VehicleRestrictionsManager.Instance;
#endif

#if SPEEDLIMITS
        private readonly SpeedLimitManager speedLimitManager = SpeedLimitManager.Instance;
#endif

#if CUSTOMTRAFFICLIGHTS
        private readonly CustomSegmentLightsManager customTrafficLightsManager =
            CustomSegmentLightsManager.Instance;
#endif

#if ADVANCEDAI && ROUTING
        private readonly TrafficMeasurementManager trafficMeasurementManager =
            TrafficMeasurementManager.Instance;
#endif
        //Cached value (vanilla uses property to access it)
        private VehicleInfo.VehicleCategory vehicleCategory_;

        private GlobalConfig globalConf_;

        private SavedGameOptions savedGameOptions_;

        private struct BufferItem {
            public PathUnit.Position Position;
            public float ComparisonValue;
            public float MethodDistance;
            public float Duration;
            public uint LaneId;
            public NetInfo.Direction Direction;
            public NetInfo.LaneType LanesUsed;
            public VehicleInfo.VehicleType VehiclesUsed;

#if ADVANCEDAI && ROUTING
            public float TrafficRand;
#endif

            public override string ToString() {
                return "BufferItem {\n" +
                       $"\tposition=(s#({Position.m_segment}), l#({Position.m_lane}), o#({Position.m_offset}))\n" +
                       $"\tlaneID={LaneId}\n" +
                       $"\tcomparisonValue={ComparisonValue}\n" +
                       $"\tmethodDistance={MethodDistance}\n" +
                       $"\tduration={Duration}\n" +
                       $"\tdirection={Direction}\n" +
                       $"\tlanesUsed={LanesUsed}\n" +
                       $"\tvehiclesUsed={VehiclesUsed}\n" +
#if ADVANCEDAI && ROUTING
                       $"\ttrafficRand={TrafficRand}\n" +
#endif
                       "}";
            }
        }

        // private stock fields
        private Array32<PathUnit> pathUnits_;
        private uint queueFirst_;
        private uint queueLast_;
        private uint calculating_;
        private object queueLock_;
        private Thread customPathFindThread_;
        private bool terminated_;

        public bool IsAvailable => customPathFindThread_.IsAlive;

        // stock fields
        public ThreadProfiler m_pathfindProfiler;
        private object bufferLock_;
        private int bufferMinPos_;
        private int bufferMaxPos_;
        private uint[] laneLocation_;
        private PathUnit.Position[] laneTarget_;
        private BufferItem[] buffer_;
        private int[] bufferMin_;
        private int[] bufferMax_;
        private float maxLength_;
        private uint startLaneA_;
        private uint startLaneB_;
        private uint endLaneA_;
        private uint endLaneB_;
        private uint vehicleLane_;
        private byte startOffsetA_;
        private byte startOffsetB_;
        private byte vehicleOffset_;
        private NetSegment.Flags carBanMask_;
        private bool ignoreBlocked_;
        private bool stablePath_;
        private bool randomParking_;
        private bool transportVehicle_;
        private bool ignoreCost_;
        private NetSegment.Flags disableMask_;
        private Randomizer pathRandomizer_;
        private uint pathFindIndex_;
        private NetInfo.LaneType laneTypes_;
        private VehicleInfo.VehicleType vehicleTypes_;

        // custom fields
        private PathUnitQueueItem queueItem_;
        /// <summary>
        /// Contains allowed extVehicle types, in normal conditions just QueueIten.vehicleType,
        /// when path may include CargoVehicle lanes then it contains also respective cargo vehicle types
        /// </summary>
        private ExtVehicleType allowedPathVehicleTypes_;
        private bool isHeavyVehicle_;

        private bool debugLog_; // this will be false in non-Debug

#if DEBUG
        public uint m_failedPathFinds;
        public uint m_succeededPathFinds;
        private IDictionary<ushort, IList<ushort>> debugPositions_;
#endif

        private ushort startSegmentA_;
        private ushort startSegmentB_;

#if ROUTING
        private bool isRoadVehicle_;
        private bool isLaneArrowObeyingEntity_;
        // private bool m_isLaneConnectionObeyingEntity;
#endif

        private void Awake() {
            Log.Info("Pathfinder logic: Using CustomPathFind_Current");

            Type stockPathFindType = typeof(PathFind);
            const BindingFlags FIELD_FLAGS = BindingFlags.NonPublic | BindingFlags.Instance;

            queueFirst_ = 0;
            queueLast_ = 0;
            terminated_ = false;
            calculating_ = 0u;

            m_pathfindProfiler = new ThreadProfiler();
            laneLocation_ = new uint[262144];
            laneTarget_ = new PathUnit.Position[262144];
            buffer_ = new BufferItem[65536];
            bufferMin_ = new int[1024];
            bufferMax_ = new int[1024];
            queueLock_ = new object();
            bufferLock_ = Singleton<PathManager>.instance.m_bufferLock;
            pathUnits_ = Singleton<PathManager>.instance.m_pathUnits;
            customPathFindThread_= new Thread(PathFindThread);
            customPathFindThread_.Name = "Pathfind";
            customPathFindThread_.Priority = SimulationManager.SIMULATION_PRIORITY;
            customPathFindThread_.Start();

            stockPathFindType.GetField("m_queueLock", FIELD_FLAGS) .SetValue(this, queueLock_);
            stockPathFindType.GetField("m_pathFindThread", FIELD_FLAGS).SetValue(this, customPathFindThread_);
            if (!customPathFindThread_.IsAlive) {
                CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find thread failed to start!");
            }
        }

        private void OnDestroy() {
            Log.Info("Destroing CustomPathFind");
            lock (queueLock_) {
                terminated_ = true;
                Monitor.PulseAll(queueLock_);
            }
        }

        public new void WaitForAllPaths() {
            while (!Monitor.TryEnter(queueLock_, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }

            try {
                while ((queueFirst_ != 0U || calculating_ != 0U) && !terminated_)
                    Monitor.Wait(queueLock_);
            } finally {
                Monitor.Exit(queueLock_);
            }
        }

        /// <summary>
        /// Used internally to overwrite pathUnits array reference
        /// </summary>
        /// <param name="pathUnits"></param>
        internal void SetPathUnits(Array32<PathUnit> pathUnits) {
            pathUnits_ = pathUnits;
        }

        public new bool CalculatePath(uint unit, bool skipQueue) {
            return ExtCalculatePath(unit, skipQueue);
        }

        private bool ExtCalculatePath(uint unit, bool skipQueue) {
            if (!CustomPathManager._instance.AddPathReference(unit)) {
                return false;
            }

            lock(queueLock_) {

                if (skipQueue) {
                    if (queueLast_ == 0) {
                        queueLast_ = unit;
                    } else {
                        // NON-STOCK CODE START
                        CustomPathManager._instance.QueueItems[unit].nextPathUnitId = queueFirst_;
                        // NON-STOCK CODE END
                        // PathUnits.m_buffer[unit].m_nextPathUnit = queueFirst_; // stock code commented
                    }
                    queueFirst_ = unit;
                } else {
                    if (queueLast_ == 0) {
                        queueFirst_ = unit;
                    } else {
                        // NON-STOCK CODE START
                        CustomPathManager._instance.QueueItems[queueLast_].nextPathUnitId = unit;
                        // NON-STOCK CODE END
                        // PathUnits.m_buffer[queueLast_].m_nextPathUnit = unit; // stock code commented
                    }
                    queueLast_ = unit;
                }

                pathUnits_.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_CREATED;
                m_queuedPathFindCount++;

                Monitor.Pulse(queueLock_);
            }

            return true;
        }

        private void PathFindImplementation(uint unit, ref PathUnit data) {

            globalConf_ = GlobalConfig.Instance; // NON-STOCK CODE
            savedGameOptions_ = SavedGameOptions.Instance;

            NetManager netManager = Singleton<NetManager>.instance;

            laneTypes_ = (NetInfo.LaneType)pathUnits_.m_buffer[unit].m_laneTypes;
            vehicleTypes_ = (VehicleInfo.VehicleType)pathUnits_.m_buffer[unit].m_vehicleTypes;
            vehicleCategory_ = (VehicleInfo.VehicleCategory)pathUnits_.m_buffer[unit].m_vehicleCategories;
            maxLength_ = pathUnits_.m_buffer[unit].m_length;
            pathFindIndex_ = pathFindIndex_ + 1 & 0x7FFF;
            pathRandomizer_ = new Randomizer(unit);
            carBanMask_ = NetSegment.Flags.CarBan;

            isHeavyVehicle_ =
                (pathUnits_.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_IS_HEAVY) !=
                0; // NON-STOCK CODE (refactored)

            if (isHeavyVehicle_) {
                carBanMask_ |= NetSegment.Flags.HeavyBan;
            }

            if ((pathUnits_.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_READY) != 0) {
                carBanMask_ |= NetSegment.Flags.WaitingPath;
            }

            ignoreBlocked_ = (pathUnits_.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_IGNORE_BLOCKED) != 0;
            stablePath_ = (pathUnits_.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_STABLE_PATH) != 0;
            randomParking_ = (pathUnits_.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_RANDOM_PARKING) != 0;
            transportVehicle_ = (laneTypes_ & NetInfo.LaneType.TransportVehicle) != NetInfo.LaneType.None;
            ignoreCost_ = stablePath_ ||
                          (pathUnits_.m_buffer[unit].m_simulationFlags &
                           PathUnit.FLAG_IGNORE_COST) != 0;
            disableMask_ = NetSegment.Flags.Collapsed | NetSegment.Flags.PathFailed;

            if ((pathUnits_.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_IGNORE_FLOODED) == 0) {
                disableMask_ |= NetSegment.Flags.Flooded;
            }

            allowedPathVehicleTypes_ = queueItem_.vehicleType;
            if ((laneTypes_ & NetInfo.LaneType.Vehicle) != NetInfo.LaneType.None) {
                laneTypes_ |= NetInfo.LaneType.TransportVehicle;

                // allow mixed vehicle path when requested path is Vehicle + CargoVehicle (e.g.: CargoTruck and PostVanAI)
                bool allowMixedCargoPath = (laneTypes_ & NetInfo.LaneType.CargoVehicle) != NetInfo.LaneType.None;
                if (allowMixedCargoPath) {
                    allowedPathVehicleTypes_ |= VehicleTypesToMixedCargoExtVehicle(vehicleTypes_);
                }
            }

#if ROUTING
            isRoadVehicle_ =
                (queueItem_.vehicleType & ExtVehicleType.RoadVehicle) != ExtVehicleType.None;

            isLaneArrowObeyingEntity_ =
                (!savedGameOptions_.relaxedBusses || queueItem_.vehicleType != ExtVehicleType.Bus) &&
                (vehicleTypes_ & LaneArrowManager.VEHICLE_TYPES) != VehicleInfo.VehicleType.None &&
                (queueItem_.vehicleType & LaneArrowManager.EXT_VEHICLE_TYPES) != ExtVehicleType.None;
#if DEBUG
            isLaneArrowObeyingEntity_ &= !savedGameOptions_.allRelaxed;
#endif
#endif

#if DEBUG
            debugLog_ = DebugSwitch.PathFindingLog.Get()
                      && (GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.None
                          || queueItem_.vehicleType == GlobalConfig.Instance.Debug.ApiExtVehicleType)
                      && (DebugSettings.StartSegmentId == 0
                          || data.m_position00.m_segment == DebugSettings.StartSegmentId
                          || data.m_position02.m_segment == DebugSettings.StartSegmentId)
                      && (DebugSettings.EndSegmentId == 0
                          || data.m_position01.m_segment == DebugSettings.EndSegmentId
                          || data.m_position03.m_segment == DebugSettings.EndSegmentId)
                      && (DebugSettings.VehicleId == 0
                          || queueItem_.vehicleId == DebugSettings.VehicleId);
            if (debugLog_) {
                debugPositions_ = new Dictionary<ushort, IList<ushort>>();
            }
#endif

            int posCount = pathUnits_.m_buffer[unit].m_positionCount & 0xF;
            int vehiclePosIndicator = pathUnits_.m_buffer[unit].m_positionCount >> 4;
            BufferItem bufferItemStartA = default(BufferItem);

            if (data.m_position00.m_segment != 0 && posCount >= 1) {
                startSegmentA_ = data.m_position00.m_segment; // NON-STOCK CODE
                startLaneA_ = PathManager.GetLaneID(data.m_position00);
                startOffsetA_ = data.m_position00.m_offset;
                bufferItemStartA.LaneId = startLaneA_;
                bufferItemStartA.Position = data.m_position00;

                GetLaneDirection(
                    data.m_position00,
                    out bufferItemStartA.Direction,
                    out bufferItemStartA.LanesUsed,
                    out bufferItemStartA.VehiclesUsed);

                bufferItemStartA.ComparisonValue = 0f;
                bufferItemStartA.Duration = 0f;
            } else {
                startSegmentA_ = 0; // NON-STOCK CODE
                startLaneA_ = 0u;
                startOffsetA_ = 0;
            }

            BufferItem bufferItemStartB = default(BufferItem);
            if (data.m_position02.m_segment != 0 && posCount >= 3) {
                startSegmentB_ = data.m_position02.m_segment; // NON-STOCK CODE
                startLaneB_ = PathManager.GetLaneID(data.m_position02);
                startOffsetB_ = data.m_position02.m_offset;
                bufferItemStartB.LaneId = startLaneB_;
                bufferItemStartB.Position = data.m_position02;

                GetLaneDirection(
                    data.m_position02,
                    out bufferItemStartB.Direction,
                    out bufferItemStartB.LanesUsed,
                    out bufferItemStartB.VehiclesUsed);

                bufferItemStartB.ComparisonValue = 0f;
                bufferItemStartB.Duration = 0f;
            } else {
                startSegmentB_ = 0; // NON-STOCK CODE
                startLaneB_ = 0u;
                startOffsetB_ = 0;
            }

            BufferItem bufferItemEndA = default(BufferItem);
            if (data.m_position01.m_segment != 0 && posCount >= 2) {
                endLaneA_ = PathManager.GetLaneID(data.m_position01);
                bufferItemEndA.LaneId = endLaneA_;
                bufferItemEndA.Position = data.m_position01;

                GetLaneDirection(
                    data.m_position01,
                    out bufferItemEndA.Direction,
                    out bufferItemEndA.LanesUsed,
                    out bufferItemEndA.VehiclesUsed);

                bufferItemEndA.MethodDistance = 0.01f;
                bufferItemEndA.ComparisonValue = 0f;
                bufferItemEndA.Duration = 0f;
            } else {
                endLaneA_ = 0u;
            }

            BufferItem bufferItemEndB = default(BufferItem);
            if (data.m_position03.m_segment != 0 && posCount >= 4) {
                endLaneB_ = PathManager.GetLaneID(data.m_position03);
                bufferItemEndB.LaneId = endLaneB_;
                bufferItemEndB.Position = data.m_position03;

                GetLaneDirection(
                    data.m_position03,
                    out bufferItemEndB.Direction,
                    out bufferItemEndB.LanesUsed,
                    out bufferItemEndB.VehiclesUsed);

                bufferItemEndB.MethodDistance = 0.01f;
                bufferItemEndB.ComparisonValue = 0f;
                bufferItemEndB.Duration = 0f;
            } else {
                endLaneB_ = 0u;
            }

            if (data.m_position11.m_segment != 0 && vehiclePosIndicator >= 1) {
                vehicleLane_ = PathManager.GetLaneID(data.m_position11);
                vehicleOffset_ = data.m_position11.m_offset;
            } else {
                vehicleLane_ = 0u;
                vehicleOffset_ = 0;
            }

#if DEBUG
            bool detourMissing =
                (vehicleTypes_ & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train |
                                  VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Monorail |
                                  VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Trolleybus)) != VehicleInfo.VehicleType.None &&
                !queueItem_.queued;

            if (detourMissing) {
                Log.Warning("Path-finding for unhandled vehicle requested!");
            }

            if (debugLog_ || detourMissing) {
                DebugLog(
                        unit,
                        "PathFindImplementation: Preparing calculation:\n" +
                        $"\tbufferItemStartA: segment={bufferItemStartA.Position.m_segment} " +
                        $"lane={bufferItemStartA.Position.m_lane} off={bufferItemStartA.Position.m_offset} " +
                        $"laneId={bufferItemStartA.LaneId}\n" +
                        $"\tbufferItemStartB: segment={bufferItemStartB.Position.m_segment} " +
                        $"lane={bufferItemStartB.Position.m_lane} " +
                        $"off={bufferItemStartB.Position.m_offset} " +
                        $"laneId={bufferItemStartB.LaneId}\n" +
                        $"\tbufferItemEndA: segment={bufferItemEndA.Position.m_segment} " +
                        $"lane={bufferItemEndA.Position.m_lane} off={bufferItemEndA.Position.m_offset} " +
                        $"laneId={bufferItemEndA.LaneId}\n" +
                        $"\tbufferItemEndB: segment={bufferItemEndB.Position.m_segment} " +
                        $"lane={bufferItemEndB.Position.m_lane} off={bufferItemEndB.Position.m_offset} " +
                        $"laneId={bufferItemEndB.LaneId}\n" +
                        $"\tvehicleItem: segment={data.m_position11.m_segment} " +
                        $"lane={data.m_position11.m_lane} off={data.m_position11.m_offset} " +
                        $"laneId={vehicleLane_} vehiclePosIndicator={vehiclePosIndicator}\n" +
                        "Properties:\n" +
                        $"\tm_maxLength={maxLength_}\n" +
                        $"\tm_startLaneA={startLaneA_}\n" +
                        $"\tm_startLaneB={startLaneB_}\n" +
                        $"\tm_endLaneA={endLaneA_}\n" +
                        $"\tm_endLaneB={endLaneB_}\n" +
                        $"\tm_startOffsetA={startOffsetA_}\n" +
                        $"\tm_startOffsetB={startOffsetB_}\n" +
                        $"\tm_vehicleLane={vehicleLane_}\n" +
                        $"\tm_vehicleOffset={vehicleOffset_}\n" +
                        $"\tm_carBanMask={carBanMask_}\n" +
                        $"\tm_disableMask={disableMask_}\n" +
                        $"\tm_ignoreBlocked={ignoreBlocked_}\n" +
                        $"\tm_stablePath={stablePath_}\n" +
                        $"\tm_randomParking={randomParking_}\n" +
                        $"\tm_transportVehicle={transportVehicle_}\n" +
                        $"\tm_ignoreCost={ignoreCost_}\n" +
                        $"\tm_pathFindIndex={pathFindIndex_}\n" +
                        $"\tm_laneTypes={laneTypes_}\n" +
                        $"\tm_vehicleTypes={vehicleTypes_}\n" +
                        $"\tm_queueItem={queueItem_}\n" +
                        $"\tm_isHeavyVehicle={isHeavyVehicle_}\n" +
                        $"\tm_failedPathFinds={m_failedPathFinds}\n" +
                        $"\tm_succeededPathFinds={m_succeededPathFinds}\n" +
                        $"\tm_startSegmentA={startSegmentA_}\n" +
                        $"\tm_startSegmentB={startSegmentB_}\n"
#if ROUTING
                        + $"\tm_isRoadVehicle={isRoadVehicle_}\n"
                        + $"\tm_isLaneArrowObeyingEntity={isLaneArrowObeyingEntity_}\n"
                        + $"\tvehicleCategory={vehicleCategory_}"
#endif
                    );
            }
#endif

            BufferItem finalBufferItem = default(BufferItem);
            byte startOffset = 0;
            bufferMinPos_ = 0;
            bufferMaxPos_ = -1;

            if (pathFindIndex_ == 0) {
                const uint NUM3 = 4294901760u;
                for (int i = 0; i < 262144; i++) {
                    laneLocation_[i] = NUM3;
                }
            }

            for (int j = 0; j < 1024; j++) {
                bufferMin_[j] = 0;
                bufferMax_[j] = -1;
            }

            if (bufferItemEndA.Position.m_segment != 0) {
                bufferMax_[0]++;
                buffer_[++bufferMaxPos_] = bufferItemEndA;
            }

            if (bufferItemEndB.Position.m_segment != 0) {
                bufferMax_[0]++;
                buffer_[++bufferMaxPos_] = bufferItemEndB;
            }

            bool canFindPath = false;
            while (bufferMinPos_ <= bufferMaxPos_) {
                int bufMin = bufferMin_[bufferMinPos_];
                int bufMax = bufferMax_[bufferMinPos_];

                if (bufMin > bufMax) {
                    bufferMinPos_++;
                } else {
                    bufferMin_[bufferMinPos_] = bufMin + 1;
                    BufferItem candidateItem = buffer_[(bufferMinPos_ << 6) + bufMin];
                    if (candidateItem.Position.m_segment == bufferItemStartA.Position.m_segment &&
                        candidateItem.Position.m_lane == bufferItemStartA.Position.m_lane) {
                        if ((candidateItem.Direction & NetInfo.Direction.Forward) !=
                            NetInfo.Direction.None &&
                            candidateItem.Position.m_offset >= startOffsetA_) {
                            finalBufferItem = candidateItem;
                            startOffset = startOffsetA_;
                            canFindPath = true;
                            break;
                        }

                        if ((candidateItem.Direction & NetInfo.Direction.Backward) !=
                            NetInfo.Direction.None &&
                            candidateItem.Position.m_offset <= startOffsetA_) {
                            finalBufferItem = candidateItem;
                            startOffset = startOffsetA_;
                            canFindPath = true;
                            break;
                        }
                    }

                    if (candidateItem.Position.m_segment == bufferItemStartB.Position.m_segment &&
                        candidateItem.Position.m_lane == bufferItemStartB.Position.m_lane) {
                        if ((candidateItem.Direction & NetInfo.Direction.Forward) !=
                            NetInfo.Direction.None &&
                            candidateItem.Position.m_offset >= startOffsetB_) {
                            finalBufferItem = candidateItem;
                            startOffset = startOffsetB_;
                            canFindPath = true;
                            break;
                        }

                        if ((candidateItem.Direction & NetInfo.Direction.Backward) !=
                            NetInfo.Direction.None &&
                            candidateItem.Position.m_offset <= startOffsetB_) {
                            finalBufferItem = candidateItem;
                            startOffset = startOffsetB_;
                            canFindPath = true;
                            break;
                        }
                    }

                    ref NetSegment candidateItemPositionSegment = ref candidateItem.Position.m_segment.ToSegment();
                    ref NetLane candidateItemNetLane = ref candidateItem.LaneId.ToLane();

                    ushort startNodeId = candidateItemPositionSegment.m_startNode;
                    ref NetNode startNode = ref startNodeId.ToNode();

                    ushort endNodeId = candidateItemPositionSegment.m_endNode;
                    ref NetNode endNode = ref endNodeId.ToNode();

                    if ((candidateItem.Direction & NetInfo.Direction.Forward) !=
                        NetInfo.Direction.None) {
                        ProcessItemMain(
#if DEBUG
                            unit,
#endif
                            candidateItem,
                            ref candidateItemPositionSegment,
                            ref candidateItem.LaneId.ToLane(),
                            startNodeId,
                            ref startNode,
                            0,
                            false);
                    }

                    if ((candidateItem.Direction & NetInfo.Direction.Backward) !=
                        NetInfo.Direction.None) {
                        ProcessItemMain(
#if DEBUG
                            unit,
#endif
                            candidateItem,
                            ref candidateItemPositionSegment,
                            ref candidateItemNetLane,
                            endNodeId,
                            ref endNode,
                            255,
                            false);
                    }

                    int numIter = 0;
                    ushort specialNodeId = candidateItemNetLane.m_nodes;

                    if (specialNodeId == 0) {
                        continue;
                    }

                    bool nodesDisabled = ((startNode.m_flags | endNode.m_flags) & NetNode.Flags.Disabled) != NetNode.Flags.None;

                    while (specialNodeId != 0) {
                        ref NetNode specialNode = ref specialNodeId.ToNode();
                        NetInfo.Direction direction = NetInfo.Direction.None;
                        byte laneOffset = specialNode.m_laneOffset;

                        if (laneOffset <= candidateItem.Position.m_offset) {
                            direction |= NetInfo.Direction.Forward;
                        }

                        if (laneOffset >= candidateItem.Position.m_offset) {
                            direction |= NetInfo.Direction.Backward;
                        }

                        if ((candidateItem.Direction & direction) != NetInfo.Direction.None
                            && (!nodesDisabled || (specialNode.m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None)) {
#if DEBUG
                            if (debugLog_ && (DebugSettings.NodeId <= 0 || specialNodeId == DebugSettings.NodeId)) {
                                DebugLog(
                                    unit,
                                    "PathFindImplementation: Handling special node for " +
                                    $"path unit {unit}, type {queueItem_.vehicleType}:\n" +
                                    $"\tcandidateItem.Position.m_segment={candidateItem.Position.m_segment}\n" +
                                    $"\tcandidateItem.Position.m_lane={candidateItem.Position.m_lane}\n" +
                                    $"\tcandidateItem.m_laneID={candidateItem.LaneId}\n" +
                                    $"\tspecialNodeId={specialNodeId}\n" +
                                    $"\tstartNodeId={startNodeId}\n" +
                                    $"\tendNodeId={endNodeId}\n");
                            }
#endif
                            ProcessItemMain(
#if DEBUG
                                unit,
#endif
                                candidateItem,
                                ref candidateItemPositionSegment,
                                ref candidateItemNetLane,
                                specialNodeId,
                                ref specialNode,
                                laneOffset,
                                true);
                        }

                        specialNodeId = specialNode.m_nextLaneNode;

                        if (++numIter == 32768) {
                            break;
                        }
                    }
                }
            }

            if (!canFindPath) {
                pathUnits_.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;

                // NON-STOCK CODE START
#if DEBUG
                ++m_failedPathFinds;

                if (!debugLog_) {
                    return;
                }

                DebugLog(unit, "PathFindImplementation: Path-find failed: Could not find path");
                string reachableBuf = string.Empty;
                string unreachableBuf = string.Empty;

                // TODO: optimize: Building a list of positions is faster than string concat
                foreach (KeyValuePair<ushort, IList<ushort>> e in debugPositions_) {
                    string buf = $"{e.Key} -> {e.Value.CollectionToString()}\n";
                    if (e.Value.Count <= 0) {
                        unreachableBuf += buf;
                    } else {
                        reachableBuf += buf;
                    }
                }

                DebugLog(
                    unit,
                    "PathFindImplementation: Reachability graph:\n== REACHABLE ==\n" +
                    reachableBuf + "\n== UNREACHABLE ==\n" + unreachableBuf);
#endif

                // NON-STOCK CODE END
            } else {
                float duration =
                    laneTypes_ != NetInfo.LaneType.Pedestrian &&
                    (laneTypes_ & NetInfo.LaneType.Pedestrian) != NetInfo.LaneType.None
                        ? finalBufferItem.Duration
                        : finalBufferItem.MethodDistance;
                pathUnits_.m_buffer[unit].m_length = duration;
                pathUnits_.m_buffer[unit].m_speed = (byte)Mathf.Clamp(
                    finalBufferItem.MethodDistance * 100f / Mathf.Max(
                        0.01f,
                        finalBufferItem.Duration),
                    0f,
                    255f);

                pathUnits_.m_buffer[unit].m_laneTypes = (byte)finalBufferItem.LanesUsed;
                pathUnits_.m_buffer[unit].m_vehicleTypes = (uint)finalBufferItem.VehiclesUsed;

                uint currentPathUnitId = unit;
                int currentItemPositionCount = 0;
                int sumOfPositionCounts = 0;
                PathUnit.Position currentPosition = finalBufferItem.Position;

                if ((currentPosition.m_segment != bufferItemEndA.Position.m_segment ||
                     currentPosition.m_lane != bufferItemEndA.Position.m_lane ||
                     currentPosition.m_offset != bufferItemEndA.Position.m_offset) &&
                    (currentPosition.m_segment != bufferItemEndB.Position.m_segment ||
                     currentPosition.m_lane != bufferItemEndB.Position.m_lane ||
                     currentPosition.m_offset != bufferItemEndB.Position.m_offset)) {
                    if (startOffset != currentPosition.m_offset) {
                        PathUnit.Position position2 = currentPosition;
                        position2.m_offset = startOffset;
                        pathUnits_.m_buffer[currentPathUnitId].SetPosition(
                            currentItemPositionCount++,
                            position2);
                    }

                    pathUnits_.m_buffer[currentPathUnitId].SetPosition(
                        currentItemPositionCount++,
                        currentPosition);
                    currentPosition = laneTarget_[finalBufferItem.LaneId];
                }

                for (int k = 0; k < 262144; k++) {
                    pathUnits_.m_buffer[currentPathUnitId].SetPosition(
                        currentItemPositionCount++,
                        currentPosition);

                    if ((currentPosition.m_segment == bufferItemEndA.Position.m_segment &&
                         currentPosition.m_lane == bufferItemEndA.Position.m_lane &&
                         currentPosition.m_offset == bufferItemEndA.Position.m_offset) ||
                        (currentPosition.m_segment == bufferItemEndB.Position.m_segment &&
                         currentPosition.m_lane == bufferItemEndB.Position.m_lane &&
                         currentPosition.m_offset == bufferItemEndB.Position.m_offset))
                    {
                        pathUnits_.m_buffer[currentPathUnitId].m_positionCount =
                            (byte)currentItemPositionCount;
                        sumOfPositionCounts += currentItemPositionCount;

                        if (sumOfPositionCounts != 0) {
                            currentPathUnitId = pathUnits_.m_buffer[unit].m_nextPathUnit;
                            currentItemPositionCount = pathUnits_.m_buffer[unit].m_positionCount;
                            int numIter = 0;
                            while (currentPathUnitId != 0) {
                                pathUnits_.m_buffer[currentPathUnitId].m_length =
                                    duration * (sumOfPositionCounts - currentItemPositionCount) /
                                    sumOfPositionCounts;
                                currentItemPositionCount +=
                                    pathUnits_.m_buffer[currentPathUnitId].m_positionCount;
                                currentPathUnitId =
                                    pathUnits_.m_buffer[currentPathUnitId].m_nextPathUnit;
                                if (++numIter >= 262144) {
                                    CODebugBase<LogChannel>.Error(
                                        LogChannel.Core,
                                        "Invalid list detected!\n" + Environment.StackTrace);
                                    break;
                                }
                            }
                        }

                        pathUnits_.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_READY;

                        // NON-STOCK CODE START
#if DEBUG
                        ++m_succeededPathFinds;

                        if (debugLog_) {
                            DebugLog(unit, "PathFindImplementation: Path-find succeeded");
                        }
#endif
                        // NON-STOCK CODE END
                        return;
                    }

                    if (currentItemPositionCount == 12) {
                        uint createdPathUnitId;
                        lock(bufferLock_) {

                            if (!pathUnits_.CreateItem(out createdPathUnitId, ref pathRandomizer_)) {
                                pathUnits_.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;
                                // NON-STOCK CODE START
#if DEBUG
                                ++m_failedPathFinds;

                                if (debugLog_) {
                                    DebugLog(unit, "Path-finding failed: Could not create path unit");
                                }
#endif
                                // NON-STOCK CODE END
                                return;
                            }

                            pathUnits_.m_buffer[createdPathUnitId] = pathUnits_.m_buffer[currentPathUnitId];
                            pathUnits_.m_buffer[createdPathUnitId].m_referenceCount = 1;
                            pathUnits_.m_buffer[createdPathUnitId].m_pathFindFlags = PathUnit.FLAG_READY;
                            pathUnits_.m_buffer[currentPathUnitId].m_nextPathUnit = createdPathUnitId;
                            pathUnits_.m_buffer[currentPathUnitId].m_positionCount = (byte)currentItemPositionCount;

                            // (this is not accurate!)
                            pathUnits_.m_buffer[currentPathUnitId].m_laneTypes =
                                (byte)finalBufferItem.LanesUsed;
                            pathUnits_.m_buffer[currentPathUnitId].m_vehicleTypes =
                                (uint)finalBufferItem.VehiclesUsed;

                            sumOfPositionCounts += currentItemPositionCount;
                            Singleton<PathManager>.instance.m_pathUnitCount =
                                (int)(pathUnits_.ItemCount() - 1);
                        }

                        currentPathUnitId = createdPathUnitId;
                        currentItemPositionCount = 0;
                    }

                    uint laneId = PathManager.GetLaneID(currentPosition);
                    currentPosition = laneTarget_[laneId];
                }

                pathUnits_.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;
                // NON-STOCK CODE START
#if DEBUG
                ++m_failedPathFinds;

                if (debugLog_) {
                    DebugLog(unit, "Path-finding failed: Internal loop break error");
                }
#endif

                // NON-STOCK CODE END
            }
        }

        [Conditional("DEBUG")]
        private void DebugLog(uint unit, string message) {
            Log._Debug(
                    $"PF T#({Thread.CurrentThread.ManagedThreadId}) IDX#({pathFindIndex_}):\n"
                    + $"UNIT({unit})\n"
                    + message);
        }

        [Conditional("DEBUG")]
        private void DebugLog(uint unit, BufferItem item, string message) {
            Log._Debug(
                    $"PF T#({Thread.CurrentThread.ManagedThreadId}) IDX#({pathFindIndex_}):\n"
                    + $"UNIT({unit}): s#({item.Position.m_segment}), l#({item.Position.m_lane})\n"
                    + $"ITEM({item})\n"
                    + message);
        }

        [Conditional("DEBUG")]
        private void DebugLog(uint unit, BufferItem item, ushort nextSegmentId, string message) {
            Log._Debug(
                $"PF T#({Thread.CurrentThread.ManagedThreadId}) IDX#({pathFindIndex_}):\n"
                + $"UNIT({unit}): s#({item.Position.m_segment}), l#({item.Position.m_lane}) -> " +
                $"s#({nextSegmentId})\nITEM({item})\n"
                + message);
        }

        [Conditional("DEBUG")]
        private void DebugLog(uint unit,
                              BufferItem item,
                              ushort nextSegmentId,
                              int nextLaneIndex,
                              uint nextLaneId,
                              string message) {
            Log._Debug(
                    $"PF T#({Thread.CurrentThread.ManagedThreadId}) IDX#({pathFindIndex_}):\n"
                    + $"UNIT({unit}): s#({item.Position.m_segment}), l#({item.Position.m_lane}) -> " +
                    $"s#({nextSegmentId}), l#({nextLaneIndex}), lid#({nextLaneId})\n"
                    + $"ITEM({item})\n" + message);
        }

        // be aware:
        //   (1) path-finding works from target to start. the "next" segment is always the previous and the "previous" segment is always the next segment on the path!
        //   (2) when I use the term "lane index from outer" this means outer right lane for right-hand traffic systems and outer-left lane for left-hand traffic systems.
        //
        // main item processor
        // 1
        private void ProcessItemMain(
#if DEBUG
            uint unitId,
#endif
            BufferItem item,
            ref NetSegment prevSegment,
            ref NetLane prevLane,
            ushort nextNodeId,
            ref NetNode nextNode,
            byte connectOffset,
            bool isMiddle) {
#if DEBUG
            bool isLogEnabled = debugLog_ &&
                        (DebugSettings.NodeId <= 0 || nextNodeId == DebugSettings.NodeId);
            if (isLogEnabled) {
                if (!debugPositions_.ContainsKey(item.Position.m_segment)) {
                    debugPositions_[item.Position.m_segment] = new List<ushort>();
                }
            }

            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    "ProcessItemMain called.\n" +
                    $"\tnextNodeId={nextNodeId}\n" +
                    $"\tconnectOffset={connectOffset}\n" +
                    $"\tisMiddle={isMiddle}");
            }
#else
            const bool isLogEnabled = false;
            const uint unitId = 0;
#endif

            NetManager netManager = Singleton<NetManager>.instance;
            ushort prevSegmentId = item.Position.m_segment;
            byte prevLaneIndex = item.Position.m_lane;
            bool prevIsPedestrianLane = false;
            bool prevIsBicycleLane = false;
            bool prevIsCenterPlatform = false;
            bool prevIsElevated = false;
#if ADVANCEDAI && ROUTING
            // NON-STOCK CODE START
            bool prevIsCarLane = false;

            // NON-STOCK CODE END
#endif
            int prevRelSimilarLaneIndex = 0;

            // NON-STOCK CODE START
            float prevMaxSpeed = 1f;
            float prevLaneSpeed = 1f;

            // NON-STOCK CODE END
            NetInfo prevSegmentInfo = prevSegment.Info;
            if (prevLaneIndex < prevSegmentInfo.m_lanes.Length) {
                NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[item.Position.m_lane];
                prevIsPedestrianLane = prevLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian;
                prevIsBicycleLane = prevLaneInfo.m_laneType == NetInfo.LaneType.Vehicle &&
                                    (prevLaneInfo.m_vehicleType & vehicleTypes_) ==
                                    VehicleInfo.VehicleType.Bicycle;
                prevIsCenterPlatform = prevLaneInfo.m_centerPlatform;
                prevIsElevated = prevLaneInfo.m_elevated;

#if ADVANCEDAI && ROUTING
                // NON-STOCK CODE START
                prevIsCarLane =
                    (prevLaneInfo.m_laneType &
                     (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                    NetInfo.LaneType.None &&
                    (prevLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) !=
                    VehicleInfo.VehicleType.None;

                // NON-STOCK CODE END
#endif

                // NON-STOCK CODE START
#if SPEEDLIMITS
                prevMaxSpeed = speedLimitManager.GetGameSpeedLimit(
                    prevSegmentId,
                    prevLaneIndex,
                    item.LaneId,
                    prevLaneInfo);

                if (!(prevMaxSpeed > 0f)) {
                    prevMaxSpeed = 1f;
                }
#else
		prevMaxSpeed = !(prevLaneInfo.m_speedLimit > 0f) ? 1f : prevLaneInfo.m_speedLimit;
#endif
                prevLaneSpeed = CalculateLaneSpeed(
                    prevMaxSpeed,
                    connectOffset,
                    item.Position.m_offset,
                    ref prevSegment,
                    prevLaneInfo);

                // NON-STOCK CODE END
                prevRelSimilarLaneIndex =
                    (prevLaneInfo.m_finalDirection & NetInfo.Direction.Forward) ==
                    NetInfo.Direction.None
                        ? prevLaneInfo.m_similarLaneCount - prevLaneInfo.m_similarLaneIndex - 1
                        : prevLaneInfo.m_similarLaneIndex;
            }

            if (isMiddle) {
                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        "ProcessItemMain: middle: Exploring middle node\n" +
                        $"\tnextNodeId={nextNodeId}");
                }

                for (int i = 0; i < 8; i++) {
                    ushort nextSegmentId = nextNode.GetSegment(i);
                    if (nextSegmentId == 0) {
                        continue;
                    }

                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            "ProcessItemMain: middle: Exploring next segment behind " +
                            $"middle node\n\tnextSegmentId={nextSegmentId}");
                    }

                    ProcessItemCosts(
#if DEBUG
                        isLogEnabled,
                        unitId,
#endif
                        item,
                        ref prevSegment,
                        ref prevLane,
                        prevMaxSpeed,
                        prevLaneSpeed,
                        nextNodeId,
                        ref nextNode,
                        true,
                        nextSegmentId,
                        ref nextSegmentId.ToSegment(),
                        ref prevRelSimilarLaneIndex,
                        connectOffset,
                        !prevIsPedestrianLane,
                        prevIsPedestrianLane);
                }
            } else if (prevIsPedestrianLane) {
                // we are going to a pedestrian lane
                if (!prevIsElevated) {
                    bool isPedZoneRoad = prevSegmentInfo.IsPedestrianZoneOrPublicTransportRoad();
                    if (nextNode.Info.m_class.m_service != ItemClass.Service.Beautification) {
                        bool canCrossStreet =
                            (nextNode.m_flags &
                             (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) !=
                            NetNode.Flags.None;
                        bool isOnCenterPlatform =
                            prevIsCenterPlatform &&
                            (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Junction)) ==
                            NetNode.Flags.None;
                        ushort nextLeftSegmentId = prevSegmentId;
                        ushort nextRightSegmentId = prevSegmentId;

                        prevSegment.GetLeftAndRightLanes(
                            nextNodeId,
                            NetInfo.LaneType.Pedestrian,
                            VehicleInfo.VehicleType.None,
                            VehicleInfo.VehicleCategory.None,
                            prevLaneIndex,
                            isOnCenterPlatform,
                            out int leftLaneIndex,
                            out int rightLaneIndex,
                            out uint leftLaneId,
                            out uint rightLaneId);

                        if (leftLaneId == 0 || rightLaneId == 0) {
                            prevSegment.GetLeftAndRightSegments(
                                nextNodeId,
                                out ushort leftSegmentId,
                                out ushort rightSegmentId);

                            int numIter = 0;
                            while (leftSegmentId != 0 && leftSegmentId != prevSegmentId &&
                                   leftLaneId == 0) {
                                ref NetSegment leftSegment = ref leftSegmentId.ToSegment();
                                leftSegment.GetLeftAndRightLanes(
                                    nextNodeId,
                                    NetInfo.LaneType.Pedestrian,
                                    VehicleInfo.VehicleType.None,
                                    VehicleInfo.VehicleCategory.None,
                                    -1,
                                    isOnCenterPlatform,
                                    out _,
                                    out int someRightLaneIndex,
                                    out _,
                                    out uint someRightLaneId);

                                if (someRightLaneId != 0) {
                                    nextLeftSegmentId = leftSegmentId;
                                    leftLaneIndex = someRightLaneIndex;
                                    leftLaneId = someRightLaneId;
                                    break; // NON-STOCK CODE
                                }
#if JUNCTIONRESTRICTIONS
                                // next segment does not have pedestrian lanes but cims need to
                                // cross it to reach the next segment
                                if (!junctionManager.IsPedestrianCrossingAllowed(
                                        leftSegmentId,
                                        leftSegment.m_startNode ==
                                        nextNodeId)) {
                                    break;
                                }
#endif
                                leftSegmentId = leftSegment.GetLeftSegment(nextNodeId);

                                if (++numIter == 8) {
                                    break;
                                }
                            }

                            numIter = 0;
                            while (rightSegmentId != 0 && rightSegmentId != prevSegmentId &&
                                   rightLaneId == 0) {
                                ref NetSegment rightSegment = ref rightSegmentId.ToSegment();
                                rightSegment.GetLeftAndRightLanes(
                                    nextNodeId,
                                    NetInfo.LaneType.Pedestrian,
                                    VehicleInfo.VehicleType.None,
                                    VehicleInfo.VehicleCategory.None,
                                    -1,
                                    isOnCenterPlatform,
                                    out int someLeftLaneIndex,
                                    out _,
                                    out uint someLeftLaneId,
                                    out _);

                                if (someLeftLaneId != 0) {
                                    nextRightSegmentId = rightSegmentId;
                                    rightLaneIndex = someLeftLaneIndex;
                                    rightLaneId = someLeftLaneId;
                                    break; // NON-STOCK CODE
                                }
#if JUNCTIONRESTRICTIONS
                                // next segment does not have pedestrian lanes but cims need to
                                // cross it to reach the next segment
                                if (!junctionManager.IsPedestrianCrossingAllowed(
                                        rightSegmentId,
                                        rightSegment.m_startNode ==
                                        nextNodeId)) {
                                    break;
                                }
#endif
                                rightSegmentId = rightSegment.GetRightSegment(nextNodeId);

                                if (++numIter == 8) {
                                    break;
                                }
                            }
                        }

                        if (leftLaneId != 0 &&
                            (nextLeftSegmentId != prevSegmentId || canCrossStreet || isOnCenterPlatform) &&
                            (!isPedZoneRoad || !netManager.m_segments.m_buffer[nextLeftSegmentId].Info.IsPedestrianZoneOrPublicTransportRoad())) {
                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    "ProcessItemMain: ped -> ped: Exploring left " +
                                    "pedestrian lane\n" +
                                    $"\tleftLaneId={leftLaneId}\n" +
                                    $"\tnextLeftSegmentId={nextLeftSegmentId}\n" +
                                    $"\tcanCrossStreet={canCrossStreet}\n" +
                                    $"\tisOnCenterPlatform={isOnCenterPlatform}");
                            }

                            ProcessItemPedBicycle(
#if DEBUG
                                isLogEnabled,
                                unitId,
#endif
                                item,
                                ref prevSegment,
                                ref prevLane,
                                prevMaxSpeed,
                                prevLaneSpeed,
                                nextLeftSegmentId,
                                ref nextLeftSegmentId.ToSegment(),
                                nextNodeId,
                                ref nextNode,
                                leftLaneIndex,
                                leftLaneId,
                                ref leftLaneId.ToLane(),
                                connectOffset,
                                connectOffset);
                        }

                        if (rightLaneId != 0 && rightLaneId != leftLaneId &&
                            (nextRightSegmentId != prevSegmentId || canCrossStreet || isOnCenterPlatform) &&
                            (!isPedZoneRoad || !netManager.m_segments.m_buffer[nextRightSegmentId].Info.IsPedestrianZoneOrPublicTransportRoad())) {
                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    "ProcessItemMain: ped -> ped: Exploring right " +
                                    "pedestrian lane\n" +
                                    $"\tleftLaneId={leftLaneId}\n" +
                                    $"\trightLaneId={rightLaneId}\n" +
                                    $"\tnextRightSegmentId={nextRightSegmentId}\n" +
                                    $"\tcanCrossStreet={canCrossStreet}\n" +
                                    $"\tisOnCenterPlatform={isOnCenterPlatform}");
                            }

                            ProcessItemPedBicycle(
#if DEBUG
                                isLogEnabled,
                                unitId,
#endif
                                item,
                                ref prevSegment,
                                ref prevLane,
                                prevMaxSpeed,
                                prevLaneSpeed,
                                nextRightSegmentId,
                                ref nextRightSegmentId.ToSegment(),
                                nextNodeId,
                                ref nextNode,
                                rightLaneIndex,
                                rightLaneId,
                                ref rightLaneId.ToLane(),
                                connectOffset,
                                connectOffset);
                        }

                        // switch from bicycle lane to pedestrian lane
                        if ((vehicleTypes_ & VehicleInfo.VehicleType.Bicycle) !=
                            VehicleInfo.VehicleType.None &&
                            prevSegment.GetClosestLane(
                                item.Position.m_lane,
                                NetInfo.LaneType.Vehicle,
                                VehicleInfo.VehicleType.Bicycle,
                                VehicleInfo.VehicleCategory.All,
                                out int nextLaneIndex,
                                out uint nextLaneId)) {
                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    "ProcessItemMain: bicycle -> ped: Exploring bicycle " +
                                    "switch\n" +
                                    $"\tleftLaneId={leftLaneId}\n\trightLaneId={rightLaneId}\n" +
                                    $"\tnextRightSegmentId={nextRightSegmentId}\n" +
                                    $"\tcanCrossStreet={canCrossStreet}\n" +
                                    $"\tisOnCenterPlatform={isOnCenterPlatform}");
                            }

                            ProcessItemPedBicycle(
#if DEBUG
                                isLogEnabled,
                                unitId,
#endif
                                item,
                                ref prevSegment,
                                ref prevLane,
                                prevMaxSpeed,
                                prevLaneSpeed,
                                prevSegmentId,
                                ref prevSegment,
                                nextNodeId,
                                ref nextNode,
                                nextLaneIndex,
                                nextLaneId,
                                ref nextLaneId.ToLane(),
                                connectOffset,
                                connectOffset);
                        }

                        if (isPedZoneRoad &&
                            // NON-STOCK CODE START skip execution when driving-only path for passenger car
                            !(savedGameOptions_.parkingAI &&
                              queueItem_.vehicleType == ExtVehicleType.PassengerCar &&
                              queueItem_.pathType == ExtPathType.DrivingOnly)
                            // NON-STOCK CODE END
                        ) {
                            for (int i = 0; i < 8; i++)
                            {
                                ushort nextPedZoneSegmentId = nextNode.GetSegment(i);
                                if (nextPedZoneSegmentId != 0 && nextPedZoneSegmentId != prevSegmentId)
                                {
                                    ref NetSegment nextPedZoneSegment = ref nextPedZoneSegmentId.ToSegment();
                                    if (nextPedZoneSegment.Info.IsPedestrianZoneOrPublicTransportRoad())
                                    {
                                    ProcessItemCosts(
#if DEBUG
                                        isLogEnabled,
                                        unitId,
#endif
                                        item,
                                        ref prevSegment,
                                        ref prevLane,
                                        prevMaxSpeed,
                                        prevLaneSpeed,
                                        nextNodeId,
                                        ref nextNode,
                                        false,
                                        nextPedZoneSegmentId,
                                            ref nextPedZoneSegment,
                                        ref prevRelSimilarLaneIndex,
                                        connectOffset,
                                        false,
                                        true);
                                }
                            }
                        }
                        }
                    } else {
                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                "ProcessItemMain: beautification -> ped: Exploring " +
                                $"pedestrian lane to beautification node\n\tnextNodeId={nextNodeId}");
                        }

                        // we are going from pedestrian lane to a beautification node
                        for (int j = 0; j < 8; j++) {
                            ushort nextSegmentId = nextNode.GetSegment(j);
                            if (nextSegmentId == 0 || nextSegmentId == prevSegmentId) {
                                continue;
                            }
#if DEBUG
                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    "ProcessItemMain: beautification -> ped: Exploring " +
                                    "next segment behind beautification node\n" +
                                    $"\tnextSegmentId={nextSegmentId}");
                            }
#endif

                            ProcessItemCosts(
#if DEBUG
                                isLogEnabled,
                                unitId,
#endif
                                item,
                                ref prevSegment,
                                ref prevLane,
                                prevMaxSpeed,
                                prevLaneSpeed,
                                nextNodeId,
                                ref nextNode,
                                false,
                                nextSegmentId,
                                ref nextSegmentId.ToSegment(),
                                ref prevRelSimilarLaneIndex,
                                connectOffset,
                                false,
                                true);
                        }
                    }

                    // prepare switching from a vehicle to pedestrian lane
                    NetInfo.LaneType nextLaneType = laneTypes_ & ~NetInfo.LaneType.Pedestrian;
                    VehicleInfo.VehicleType nextVehicleType = vehicleTypes_ & ~VehicleInfo.VehicleType.Bicycle;
                    if ((item.LanesUsed &
                         (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                        NetInfo.LaneType.None)
                    {
                        nextLaneType &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
                    }

                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            "ProcessItemMain: vehicle -> ped: Prepared parameters\n" +
                            $"\tm_queueItem.vehicleType={queueItem_.vehicleType}\n" +
                            $"\tnextVehicleType={nextVehicleType}\n\tnextLaneType={nextLaneType}");
                    }

                    // NON-STOCK CODE START
                    bool parkingAllowed = true;

                    // Parking AI: Determine if parking is allowed
                    if (savedGameOptions_.parkingAI) {
                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                "ProcessItemMain: vehicle -> ped: Parking AI: Determining " +
                                "if parking is allowed here\n" +
                                $"\tm_queueItem.vehicleType={queueItem_.vehicleType}\n" +
                                $"\tnextVehicleType={nextVehicleType}\n" +
                                $"\tnextLaneType={nextLaneType}\n" +
                                $"\titem.m_lanesUsed={item.LanesUsed}\n" +
                                $"\tm_endLaneA={endLaneA_}\n\tm_endLaneB={endLaneB_}");
                        }

                        if (queueItem_.vehicleType == ExtVehicleType.PassengerCar &&
                            (nextVehicleType & VehicleInfo.VehicleType.Car) !=
                            VehicleInfo.VehicleType.None &&
                            ((nextLaneType &
                              (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                             NetInfo.LaneType.None))
                        {
                            if ((item.LanesUsed &
                                 (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                                NetInfo.LaneType.None)
                            {
                                /* if pocket cars are prohibited, a citizen may only park their car once per path */
                                parkingAllowed = false;
                            } else if ((item.LanesUsed & NetInfo.LaneType.PublicTransport) ==
                                       NetInfo.LaneType.None) {
                                /* if the citizen is walking to their target (= no public transport used), the passenger car must be parked in the very last moment */
                                parkingAllowed = item.LaneId == endLaneA_ || item.LaneId == endLaneB_;
                            }
                        }

                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                "ProcessItemMain: vehicle -> ped: Parking AI: Parking " +
                                $"allowed here? {parkingAllowed}");
                        }
                    }

                    // NON-STOCK CODE END

                    if (parkingAllowed && // NON-STOCK CODE
                        nextLaneType != NetInfo.LaneType.None &&
                        nextVehicleType != VehicleInfo.VehicleType.None &&
                        prevSegment.GetClosestLane(
                            prevLaneIndex,
                            nextLaneType,
                            nextVehicleType,
                            vehicleCategory_,
                            out int sameSegLaneIndex,
                            out uint sameSegLaneId))
                    {
                        NetInfo.Lane sameSegLaneInfo = prevSegmentInfo.m_lanes[sameSegLaneIndex];
                        byte sameSegConnectOffset =
                            (byte)((prevSegment.m_flags & NetSegment.Flags.Invert) !=
                                   NetSegment.Flags.None ==
                                   ((sameSegLaneInfo.m_finalDirection &
                                     NetInfo.Direction.Backward) != NetInfo.Direction.None)
                                       ? 1
                                       : 254);
                        BufferItem nextItem = item;
                        if (randomParking_) {
                            nextItem.ComparisonValue += pathRandomizer_.Int32(300u) / maxLength_;
                        }

                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                "ProcessItemMain: vehicle -> ped: Exploring parking\n" +
                                $"\tnextLaneType={nextLaneType}\n" +
                                $"\tnextVehicleType={nextVehicleType}\n" +
                                $"\tnextLaneType={nextLaneType}\n" +
                                $"\tsameSegConnectOffset={sameSegConnectOffset}\n" +
                                $"\tm_randomParking={randomParking_}");
                        }

                        ProcessItemPedBicycle(
#if DEBUG
                            isLogEnabled,
                            unitId,
#endif
                            nextItem,
                            ref prevSegment,
                            ref prevLane,
                            prevMaxSpeed,
                            prevLaneSpeed,
                            prevSegmentId,
                            ref prevSegment,
                            nextNodeId,
                            ref nextNode,
                            sameSegLaneIndex,
                            sameSegLaneId,
                            ref sameSegLaneId.ToLane(),
                            sameSegConnectOffset,
                            128);
                    }
                }
            } else {
                // We are going to a non-pedestrian lane
                // NON-STOCK CODE (refactored)
                bool nextIsBeautificationNode = nextNode.Info.m_class.m_service == ItemClass.Service.Beautification;

                // allow switching from pedestrian lane to a non-pedestrian lane?
                bool allowPedestrian = (laneTypes_ & NetInfo.LaneType.Pedestrian) != NetInfo.LaneType.None;
                bool allowBicycle = false; // allow switching from a pedestrian lane to a bike lane?
                byte switchConnectOffset = 0; // lane switching offset

                if (allowPedestrian) {
                    if (prevIsBicycleLane) {
                        // we are going to a bicycle lane
                        switchConnectOffset = connectOffset;
                        allowBicycle = nextIsBeautificationNode;
                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                "ProcessItemMain: ped -> vehicle: Switching to a bicycle " +
                                "may be allowed here\n" +
                                $"\tswitchConnectOffset={switchConnectOffset}\n" +
                                $"\tallowBicycle={allowBicycle}");
                        }
                    } else if (vehicleLane_ != 0) {
                        // there is a parked vehicle position
                        if (vehicleLane_ != item.LaneId) {
                            // we have not reached the parked vehicle yet
                            allowPedestrian = false;
                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    "ProcessItemMain: ped -> vehicle: Entering a parked " +
                                    "vehicle is not allowed here");
                            }
                        } else {
                            // pedestrian switches to parked vehicle
                            switchConnectOffset = vehicleOffset_;
                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    "ProcessItemMain: ped -> vehicle: Entering a parked " +
                                    "vehicle is allowed here\n" +
                                    $"\tswitchConnectOffset={switchConnectOffset}");
                            }
                        }
                    } else if (stablePath_) {
                        // enter a bus
                        switchConnectOffset = 128;
                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                "ProcessItemMain: ped -> vehicle: Entering a bus is " +
                                $"allowed here\n\tswitchConnectOffset={switchConnectOffset}");
                        }
                    } else {
                        // pocket car spawning
                        if (savedGameOptions_.parkingAI) {
                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    "ProcessItemMain: ped -> vehicle: Parking AI: " +
                                    "Determining if spawning pocket cars is allowed\n" +
                                    $"\tm_queueItem.pathType={queueItem_.pathType}\n" +
                                    $"\tprevIsCarLane={prevIsCarLane}\n" +
                                    $"\tm_queueItem.vehicleType={queueItem_.vehicleType}\n" +
                                    $"\tm_startSegmentA={startSegmentA_}\n" +
                                    $"\tm_startSegmentB={startSegmentB_}");
                            }

                            if ((queueItem_.pathType == ExtPathType.WalkingOnly && prevIsCarLane)
                                || (queueItem_.pathType == ExtPathType.DrivingOnly &&
                                    queueItem_.vehicleType == ExtVehicleType.PassengerCar &&
                                    ((item.Position.m_segment != startSegmentA_
                                      && item.Position.m_segment != startSegmentB_)
                                     || !prevIsCarLane))) {
                                /* allow pocket cars only if an instant driving path is required
                                 and we are at the start segment */
                                /* disallow pocket cars on walking paths */
                                allowPedestrian = false;

                                if (isLogEnabled) {
                                    DebugLog(
                                        unitId,
                                        item,
                                        "ProcessItemMain: ped -> vehicle: Parking AI: " +
                                        "Spawning pocket cars is not allowed here");
                                }
                            } else {
                                switchConnectOffset = (byte)pathRandomizer_.UInt32(1u, 254u);

                                if (isLogEnabled) {
                                    DebugLog(
                                        unitId,
                                        item,
                                        "ProcessItemMain: ped -> vehicle: Parking AI: " +
                                        "Spawning pocket cars is allowed here\n" +
                                        $"\tswitchConnectOffset={switchConnectOffset}");
                                }
                            }
                        } else {
                            switchConnectOffset = (byte)pathRandomizer_.UInt32(1u, 254u);
                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    "ProcessItemMain: ped -> vehicle: Spawning pocket " +
                                    $"cars is allowed here\n\tswitchConnectOffset={switchConnectOffset}");
                            }
                        }
                    }
                }

                ushort nextSegmentId;
                if (((laneTypes_ & NetInfo.LaneType.CargoVehicle) == NetInfo.LaneType.None || BelongsToFerryNetwork(ref nextNode)) &&
                    (vehicleTypes_ & VehicleInfo.VehicleType.Ferry) != VehicleInfo.VehicleType.None) {
                    // ferry (/ monorail)
                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            "ProcessItemMain: vehicle -> vehicle: Exploring ferry routes");
                    }

                    bool isUturnAllowedHere =
                        (nextNode.m_flags &
                         (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) !=
                        NetNode.Flags.None;

                    for (int k = 0; k < 8; k++) {
                        nextSegmentId = nextNode.GetSegment(k);
                        if (nextSegmentId == 0 || nextSegmentId == prevSegmentId) {
                            continue;
                        }

                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                "ProcessItemMain: vehicle -> vehicle: Exploring ferry " +
                                $"route\n\tnextSegmentId={nextSegmentId}");
                        }

                        ProcessItemCosts(
#if DEBUG
                            isLogEnabled,
                            unitId,
#endif
                            item,
                            ref prevSegment,
                            ref prevLane,
                            prevMaxSpeed,
                            prevLaneSpeed,
                            nextNodeId,
                            ref nextNode,
                            false,
                            nextSegmentId,
                            ref nextSegmentId.ToSegment(),
                            ref prevRelSimilarLaneIndex,
                            connectOffset,
                            true,
                            allowBicycle);
                    }

#if ROUTING
                    if (isUturnAllowedHere) {
#else
                    if (isUturnAllowedHere &&
                        (m_vehicleTypes & VehicleInfo.VehicleType.Monorail) ==
                        VehicleInfo.VehicleType.None) {
#endif

                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                "ProcessItemMain: vehicle -> vehicle: Exploring ferry u-turn");
                        }

                        nextSegmentId = prevSegmentId;
                        ProcessItemCosts(
#if DEBUG
                            isLogEnabled,
                            unitId,
#endif
                            item,
                            ref prevSegment,
                            ref prevLane,
                            prevMaxSpeed,
                            prevLaneSpeed,
                            nextNodeId,
                            ref nextNode,
                            false,
                            nextSegmentId,
                            ref nextSegmentId.ToSegment(),
                            ref prevRelSimilarLaneIndex,
                            connectOffset,
                            true,
                            false);
                    }
                } else {
                    // road vehicles / trams / trains / metros (/ monorails) / etc.
                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            "ProcessItemMain: vehicle -> vehicle: Exploring vehicle routes");
                    }

#if ROUTING
                    bool exploreUturn = false;
#else
                    bool exploreUturn =
                        (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) !=
                        NetNode.Flags.None;
#endif

#if ROUTING
                    bool prevIsRouted = false;
                    uint laneRoutingIndex = 0;
                    bool nextIsStartNode = nextNodeId == prevSegment.m_startNode;
                    if (nextIsStartNode || nextNodeId == prevSegment.m_endNode) {
                        laneRoutingIndex = routingManager.GetLaneEndRoutingIndex(item.LaneId, nextIsStartNode);
                        prevIsRouted = routingManager.LaneEndBackwardRoutings[laneRoutingIndex].routed;
                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                "ProcessItemMain: vehicle -> vehicle: Is previous " +
                                $"segment routed? {prevIsRouted}");
                        }
                    }

                    VehicleInfo.VehicleCategory currentVehicleCategories = VehicleInfo.VehicleCategory.None;

                    if (allowBicycle || !prevIsRouted) {
                        // pedestrian to bicycle lane switch or no routing information available:
                        // if pedestrian lanes should be explored (allowBicycle == true): do this here
                        // if previous segment has custom routing (prevIsRouted == true): do NOT
                        //     explore vehicle lanes here, else: vanilla exploration of vehicle lanes

                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                "ProcessItemMain: bicycle -> vehicle / stock vehicle " +
                                "routing\n" +
                                $"\tprevIsRouted={prevIsRouted}\n" +
                                $"\tallowBicycle={allowBicycle}");
                        }

                        // NON-STOCK CODE END
#endif
                        nextSegmentId = prevSegment.GetRightSegment(nextNodeId);
                        for (int l = 0; l < 8; l++) {
                            if (nextSegmentId == 0) {
                                break;
                            }

                            if (nextSegmentId == prevSegmentId) {
                                break;
                            }

                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    "ProcessItemMain: bicycle -> vehicle / stock vehicle " +
                                    "routing: exploring next segment\n" +
                                    $"\tnextSegmentId={nextSegmentId}");
                            }

                            if (ProcessItemCosts(
#if DEBUG
                                    isLogEnabled,
                                    unitId,
#endif
                                    item,
                                    ref prevSegment,
                                    ref prevLane,
                                    prevMaxSpeed,
                                    prevLaneSpeed,
                                    nextNodeId,
                                    ref nextNode,
                                    false,
                                    nextSegmentId,
                                    ref nextSegmentId.ToSegment(),
                                    ref prevRelSimilarLaneIndex,
                                    connectOffset,
#if ROUTING
                                    !prevIsRouted, // NON-STOCK CODE
#else
                                    true,
#endif
                                    allowBicycle))
                            {
                                exploreUturn = true; // allow exceptional u-turns
#if DEBUG
                                if (isLogEnabled) {
                                    DebugLog(
                                        unitId,
                                        item,
                                        "ProcessItemMain: bicycle -> vehicle / stock vehicle routing: "
                                        + $"exceptional u-turn allowed\n\tnextSegmentId={nextSegmentId}");
                                }
#endif
                            }

                            currentVehicleCategories |= (netManager.m_segments.m_buffer[nextSegmentId].Info.m_vehicleCategories & VehicleInfo.VehicleCategory.RoadTransport);
                            nextSegmentId = nextSegmentId.ToSegment().GetRightSegment(nextNodeId);
                        }
#if ROUTING
                    } // NON-STOCK CODE
#endif

                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            "ProcessItemMain: vehicle -> vehicle: Custom routing\n" +
                            $"\tOptions.advancedAI={savedGameOptions_.advancedAI}\n" +
                            $"\tprevIsRouted={prevIsRouted}\n" +
                            $"\tm_isRoadVehicle={isRoadVehicle_}\n" +
                            $"\tprevIsCarLane={prevIsCarLane}\n" +
                            $"\tm_stablePath={savedGameOptions_.advancedAI}");
                    }

                    // NON-STOCK CODE START
                    float segmentSelectionCost = 1f;
                    float laneSelectionCost = 1f;
                    float laneChangingCost = 1f;
                    bool enableAdvancedAI = false;

                    // NON-STOCK CODE END
#if ADVANCEDAI && ROUTING
                    /*
                     * =============================================================================
                     * Calculate Advanced Vehicle AI cost factors
                     * =============================================================================
                     */
                    if (savedGameOptions_.advancedAI
                        && prevIsRouted
                        && isRoadVehicle_
                        && prevIsCarLane )
                    {
                        enableAdvancedAI = true;
                        if (!stablePath_) {
                            CalculateAdvancedAiCostFactors(
#if DEBUG
                                isLogEnabled,
                                unitId,
#endif
                                ref item,
                                ref prevSegment,
                                ref prevLane,
                                nextNodeId,
                                ref nextNode,
                                ref segmentSelectionCost,
                                ref laneSelectionCost,
                                ref laneChangingCost);

                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    "ProcessItemMain: vehicle -> vehicle: Custom routing " +
                                    "with activated Advanced Vehicle AI: Calculated cost factors\n" +
                                    $"\tsegmentSelectionCost={segmentSelectionCost}\n" +
                                    $"\tlaneSelectionCost={laneSelectionCost}\n" +
                                    $"\tlaneChangingCost={laneChangingCost}");
                            }
                        } else {
                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    "ProcessItemMain: vehicle -> vehicle: Custom routing " +
                                    "with activated Advanced Vehicle AI and stable path: Using " +
                                    "default cost factors\n" +
                                    $"\tsegmentSelectionCost={segmentSelectionCost}\n" +
                                    $"\tlaneSelectionCost={laneSelectionCost}\n" +
                                    $"\tlaneChangingCost={laneChangingCost}");
                            }
                        }
                    }
#endif

#if ROUTING
                    if (prevIsRouted) {
                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                "ProcessItemMain: vehicle -> vehicle: Custom routing: " +
                                "Exploring custom routes");
                        }

                        exploreUturn = false; // custom routing processes regular u-turns
                        if (ProcessItemRouted(
#if DEBUG
                            isLogEnabled,
                            unitId,
#endif
                            item,
                            ref prevSegment,
                            ref prevLane,
                            prevMaxSpeed,
                            prevLaneSpeed,
#if ADVANCEDAI
                            enableAdvancedAI,
                            laneChangingCost,
#endif
                            segmentSelectionCost,
                            laneSelectionCost,
                            nextNodeId,
                            ref nextNode,
                            false,
                            routingManager.SegmentRoutings[prevSegmentId],
                            routingManager.LaneEndBackwardRoutings[laneRoutingIndex],
                            connectOffset,
                            prevRelSimilarLaneIndex)) {
                            exploreUturn = true; // allow exceptional u-turns
                        }
                    } else {
                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                "ProcessItemMain: vehicle -> vehicle: Custom routing: " +
                                "No custom routing present");
                        }

                        if (!exploreUturn) {
                            // no exceptional u-turns allowed: allow regular u-turns
                            exploreUturn =
                                (nextNode.m_flags &
                                 (NetNode.Flags.End | NetNode.Flags.OneWayOut)) !=
                                NetNode.Flags.None;

                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    "ProcessItemMain: vehicle -> vehicle: Custom routing: " +
                                    "Allowing regular u-turns:\n" +
                                    $"\texploreUturn={exploreUturn}\n");
                            }
                        }
                    }
#endif
                    VehicleInfo.VehicleCategory segmentVehicleCategories = netManager.m_segments.m_buffer[item.Position.m_segment].Info.m_vehicleCategories & VehicleInfo.VehicleCategory.RoadTransport;
                    if (!prevIsRouted && (currentVehicleCategories & segmentVehicleCategories) != segmentVehicleCategories) {
                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                $"ProcessItemMain: Different segment vehicle categories. Force explore U-turn. Previous value: {exploreUturn} | [{currentVehicleCategories}] != [{segmentVehicleCategories}]");
                        }
                        exploreUturn = true;
                    }

                    if (exploreUturn
                        && (vehicleTypes_ & (VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Trolleybus)) == VehicleInfo.VehicleType.None)
                    {
                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                "ProcessItemMain: vehicle -> vehicle: Exploring stock u-turn\n" +
                                $"\texploreUturn={exploreUturn}\n");
                        }

                        ProcessItemCosts(
#if DEBUG
                            isLogEnabled,
                            unitId,
#endif
                            item,
                            ref prevSegment,
                            ref prevLane,
                            prevMaxSpeed,
                            prevLaneSpeed,
#if ADVANCEDAI && ROUTING
                            false,
                            0f,
#endif
                            nextNodeId,
                            ref nextNode,
                            false,
                            prevSegmentId,
                            ref prevSegment,
#if ROUTING
                            segmentSelectionCost,
                            laneSelectionCost,
                            null,
#endif
                            ref prevRelSimilarLaneIndex,
                            connectOffset,
                            true,
                            false);
                    }
                }

                if (allowPedestrian) {
                    if (prevSegment.GetClosestLane(
                        item.Position.m_lane,
                        NetInfo.LaneType.Pedestrian,
                        vehicleTypes_,
                        vehicleCategory_,
                        out int nextLaneIndex,
                        out uint nextLaneId)) {
                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                "ProcessItemMain: ped -> vehicle: Exploring switch\n"
                                + "\t" + $"nextLaneIndex={nextLaneIndex}\n"
                                + "\t" + $"nextLaneId={nextLaneId}");
                        }

                        ProcessItemPedBicycle(
#if DEBUG
                            isLogEnabled,
                            unitId,
#endif
                            item,
                            ref prevSegment,
                            ref prevLane,
                            prevMaxSpeed,
                            prevLaneSpeed,
                            prevSegmentId,
                            ref prevSegment,
                            nextNodeId,
                            ref nextNode,
                            nextLaneIndex,
                            nextLaneId,
                            ref nextLaneId.ToLane(),
                            switchConnectOffset,
                            switchConnectOffset);
                    }
                }
            }

            if (nextNode.m_lane == 0) {
                return;
            }

            bool targetDisabled =
                (nextNode.m_flags & (NetNode.Flags.Disabled | NetNode.Flags.DisableOnlyMiddle)) ==
                NetNode.Flags.Disabled;
            ushort nextSegmentId2 = nextNode.m_lane.ToLane().m_segment;

            if (nextSegmentId2 == 0 || nextSegmentId2 == prevSegmentId) {
                return;
            }

            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    "ProcessItemMain: transport -> *: Exploring special node\n" +
                    $"\tnextSegmentId={nextSegmentId2}\n" +
                    $"\tnextNode.m_lane={nextNode.m_lane}\n" +
                    $"\ttargetDisabled={targetDisabled}\n\tnextNodeId={nextNodeId}");
            }

            ProcessItemPublicTransport(
#if DEBUG
                isLogEnabled,
                unitId,
#endif
                item,
                ref prevSegment,
                ref prevLane,
                prevMaxSpeed,
                prevLaneSpeed,
                nextNodeId,
                targetDisabled,
                nextSegmentId2,
                ref nextSegmentId2.ToSegment(),
                nextNode.m_lane,
                nextNode.m_laneOffset,
                connectOffset);
        }

        // 2
        private void ProcessItemPublicTransport(
#if DEBUG
            bool isLogEnabled,
            uint unitId,
#endif
            BufferItem item,
            ref NetSegment prevSegment,
            ref NetLane prevLane,
            float prevMaxSpeed,
            float prevLaneSpeed,
            ushort nextNodeId,
            bool targetDisabled,
            ushort nextSegmentId,
            ref NetSegment nextSegment,
            uint nextLaneId,
            byte offset,
            byte connectOffset) {
#if !DEBUG
            const bool isLogEnabled = false;
            const uint unitId = 0;
#endif
            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    nextSegmentId,
                    "ProcessItemPublicTransport called.\n" +
                    $"\tprevMaxSpeed={prevMaxSpeed}\n" +
                    $"\tprevLaneSpeed={prevLaneSpeed}\n" +
                    $"\tnextNodeId={nextNodeId}\n" +
                    $"\ttargetDisabled={targetDisabled}\n" +
                    $"\tnextLaneId={nextLaneId}\n" +
                    $"\toffset={offset}\n" +
                    $"\tconnectOffset={connectOffset}");
            }

            if ((nextSegment.m_flags & disableMask_) != NetSegment.Flags.None) {
                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        nextSegmentId,
                        "ProcessItemPublicTransport: Aborting: Disable mask\n" +
                        $"\tm_disableMask={disableMask_}\n" +
                        $"\tnextSegment.m_flags={nextSegment.m_flags}\n");
                }

                return;
            }

            NetManager netManager = Singleton<NetManager>.instance;
            if (targetDisabled
                && (
                    (nextSegment.m_startNode.ToNode().m_flags
                    | nextSegment.m_endNode.ToNode().m_flags)
                    & NetNode.Flags.Disabled) == NetNode.Flags.None) {
                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        nextSegmentId,
                        "ProcessItemPublicTransport: Aborting: Target disabled");
                }
                return;
            }

            NetInfo nextSegmentInfo = nextSegment.Info;
            NetInfo prevSegmentInfo = prevSegment.Info;
            int nextNumLanes = nextSegmentInfo.m_lanes.Length;

            // float prevMaxSpeed = 1f; // stock code commented
            // float prevLaneSpeed = 1f; // stock code commented
            NetInfo.LaneType prevLaneType = NetInfo.LaneType.None;
            if (item.Position.m_lane < prevSegmentInfo.m_lanes.Length) {
                NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[item.Position.m_lane];

                // prevMaxSpeed = prevLaneInfo.m_speedLimit; // stock code commented
                // prevLaneSpeed = CalculateLaneSpeed(prevMaxSpeed, connectOffset,
                // item.Position.m_offset, ref prevSegment, prevLaneInfo); // stock code commented
                prevLaneType = prevLaneInfo.m_laneType;
                if ((prevLaneType &
                     (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                    NetInfo.LaneType.None) {
                    prevLaneType = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
                }
            }

            float prevLength = prevLaneType != NetInfo.LaneType.PublicTransport
                                 ? prevSegment.m_averageLength
                                 : prevLane.m_length;
            float offsetLength = Mathf.Abs(connectOffset - item.Position.m_offset) *
                               BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * prevLength;
            float methodDistance = item.MethodDistance + offsetLength;
            float comparisonValue = item.ComparisonValue + offsetLength / (prevLaneSpeed * maxLength_);
            float duration = item.Duration + offsetLength / prevMaxSpeed;
            Vector3 b = prevLane.CalculatePosition(
                connectOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);

            if (!ignoreCost_) {
                int ticketCost = prevLane.m_ticketCost;
                if (ticketCost != 0) {
                    comparisonValue += ticketCost * pathRandomizer_.Int32(2000u) *
                                       TICKET_COST_CONVERSION_FACTOR;
                }
            }

            int nextLaneIndex = 0;
            uint curLaneId = nextSegment.m_lanes;
            while (true) {
                if (nextLaneIndex < nextNumLanes && curLaneId != 0) {
                    if (nextLaneId != curLaneId) {
                        curLaneId = curLaneId.ToLane().m_nextLane;
                        nextLaneIndex++;
                        continue;
                    }

                    break;
                }

                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        nextSegmentId,
                        "ProcessItemPublicTransport: Aborting: Next lane not found");
                }
                return;
            }

            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    nextSegmentId,
                    nextLaneIndex,
                    curLaneId,
                    "ProcessItemPublicTransport: Exploring next lane\n" +
                    $"\tnextLaneIndex={nextLaneIndex}\n" +
                    $"\tnextLaneId={nextLaneId}");
            }

            NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];
            if (!nextLaneInfo.CheckType(laneTypes_, vehicleTypes_, vehicleCategory_)) {
                return;
            }

            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    nextSegmentId,
                    nextLaneIndex,
                    curLaneId,
                    "ProcessItemPublicTransport: Next lane compatible\n" +
                    $"\tnextLaneInfo.m_vehicleType={nextLaneInfo.m_vehicleType}\n" +
                    $"\tnextLaneInfo.m_laneType={nextLaneInfo.m_laneType}");
            }

            Vector3 a = nextLaneId.ToLane().CalculatePosition(offset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
            float distance = Vector3.Distance(a, b);
            BufferItem nextItem = default(BufferItem);

            nextItem.Position.m_segment = nextSegmentId;
            nextItem.Position.m_lane = (byte)nextLaneIndex;
            nextItem.Position.m_offset = offset;

            if ((nextLaneInfo.m_laneType & prevLaneType) == NetInfo.LaneType.None) {
                nextItem.MethodDistance = 0f;
            } else {
                nextItem.MethodDistance = methodDistance + distance;
            }

            if (nextLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian &&
                !(nextItem.MethodDistance < globalConf_.PathFinding.MaxWalkingDistance) &&
                !stablePath_) {
                // NON-STOCK CODE (custom walking distance)
                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        nextSegmentId,
                        nextLaneIndex,
                        curLaneId,
                        "ProcessItemPublicTransport: Aborting: Max. walking distance exceeded\n" +
                        $"\tnextItem.m_methodDistance={nextItem.MethodDistance}");
                }
                return;
            }

#if SPEEDLIMITS
            // NON-STOCK CODE START
            float nextMaxSpeed = speedLimitManager.GetGameSpeedLimit(
                nextSegmentId,
                (byte)nextLaneIndex,
                nextLaneId,
                nextLaneInfo);

            if (!(nextMaxSpeed > 0f)) {
                nextMaxSpeed = 1f; // prevent divide by zero which result in NaN value
            }

            // NON-STOCK CODE END
#else
            var nextMaxSpeed = !(nextLaneInfo.m_speedLimit > 0f) ? 1f : nextLaneInfo.m_speedLimit;
#endif
            float newDistance = distance;
            if (!stablePath_ && (nextLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Plane) != VehicleInfo.VehicleType.None)
            {
                Randomizer randomizer = new Randomizer(pathFindIndex_ ^ nextLaneId);
                newDistance *= randomizer.Int32(10000U) * (1f / 1000f);
            }
            float currentComparisonValue = (newDistance /
                                       ((prevMaxSpeed + nextMaxSpeed) * 0.5f * maxLength_));
            float currentDuration = (distance / ((prevMaxSpeed + nextMaxSpeed) * 0.5f));

            nextItem.Direction =
                (nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None
                    ? NetInfo.InvertDirection(nextLaneInfo.m_finalDirection)
                    : nextLaneInfo.m_finalDirection;

            if (nextLaneId == startLaneA_) {
                if (((nextItem.Direction & NetInfo.Direction.Forward) == NetInfo.Direction.None ||
                     nextItem.Position.m_offset < startOffsetA_) &&
                    ((nextItem.Direction & NetInfo.Direction.Backward) == NetInfo.Direction.None ||
                     nextItem.Position.m_offset > startOffsetA_)) {
                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            nextSegmentId,
                            nextLaneIndex,
                            curLaneId,
                            "ProcessItemPublicTransport: Aborting: Invalid offset/direction " +
                            "on start lane A\n" +
                            $"\tnextItem.m_direction={nextItem.Direction}\n" +
                            $"\tnextItem.Position.m_offset={nextItem.Position.m_offset}\n" +
                            $"\tm_startOffsetA={startOffsetA_}");
                    }

                    return;
                }

                float nextSpeed = CalculateLaneSpeed(
                    nextMaxSpeed,
                    startOffsetA_,
                    nextItem.Position.m_offset,
                    ref nextSegment,
                    nextLaneInfo);
                float nextOffset = Mathf.Abs(nextItem.Position.m_offset - startOffsetA_) *
                                 BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

                currentComparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * maxLength_);
                currentDuration += nextOffset * nextSegment.m_averageLength / nextSpeed;
            }

            if (nextLaneId == startLaneB_) {
                if (((nextItem.Direction & NetInfo.Direction.Forward) == NetInfo.Direction.None ||
                     nextItem.Position.m_offset < startOffsetB_) &&
                    ((nextItem.Direction & NetInfo.Direction.Backward) == NetInfo.Direction.None ||
                     nextItem.Position.m_offset > startOffsetB_)) {
                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            nextSegmentId,
                            nextLaneIndex,
                            curLaneId,
                            "ProcessItemPublicTransport: Aborting: Invalid offset/direction " +
                            "on start lane B\n" +
                            $"\tnextItem.m_direction={nextItem.Direction}\n" +
                            $"\tnextItem.Position.m_offset={nextItem.Position.m_offset}\n" +
                            $"\tm_startOffsetB={startOffsetB_}");
                    }

                    return;
                }

                float nextSpeed = CalculateLaneSpeed(
                    nextMaxSpeed,
                    startOffsetB_,
                    nextItem.Position.m_offset,
                    ref nextSegment,
                    nextLaneInfo);
                float nextOffset = Mathf.Abs(nextItem.Position.m_offset - startOffsetB_) *
                                 BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

                currentComparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * maxLength_);
                currentDuration += nextOffset * nextSegment.m_averageLength / nextSpeed;
            }

            nextItem.ComparisonValue = comparisonValue + currentComparisonValue;
            nextItem.Duration = duration + currentDuration;
            nextItem.LaneId = nextLaneId;
            nextItem.LanesUsed = item.LanesUsed | nextLaneInfo.m_laneType;
            nextItem.VehiclesUsed = item.VehiclesUsed | nextLaneInfo.m_vehicleType;

#if ADVANCEDAI && ROUTING
            // NON-STOCK CODE START
            nextItem.TrafficRand = item.TrafficRand;

            // NON-STOCK CODE END
#endif

            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    nextSegmentId,
                    nextLaneIndex,
                    curLaneId,
                    $"ProcessItemPublicTransport: Adding next item\n\tnextItem={nextItem}");
            }

            AddBufferItem(
#if DEBUG
                isLogEnabled,
#endif
                nextItem,
                item.Position);
        }

#if ADVANCEDAI && ROUTING
        // 3a (non-routed, no adv. AI)
        private bool ProcessItemCosts(
#if DEBUG
            bool isLogEnabled,
            uint unitId,
#endif
            BufferItem item,
            ref NetSegment prevSegment,
            ref NetLane prevLane,
            float prevMaxSpeed,
            float prevLaneSpeed,
            ushort nextNodeId,
            ref NetNode nextNode,
            bool isMiddle,
            ushort nextSegmentId,
            ref NetSegment nextSegment,
            ref int laneIndexFromInner,
            byte connectOffset,
            bool enableVehicle,
            bool enablePedestrian) {
            return ProcessItemCosts(
#if DEBUG
                isLogEnabled,
                unitId,
#endif
                item,
                ref prevSegment,
                ref prevLane,
                prevMaxSpeed,
                prevLaneSpeed,
#if ADVANCEDAI && ROUTING
                false,
                0f,
#endif
                nextNodeId,
                ref nextNode,
                isMiddle,
                nextSegmentId,
                ref nextSegment,
#if ROUTING
                1f,
                1f,
                null,
#endif
                ref laneIndexFromInner,
                connectOffset,
                enableVehicle,
                enablePedestrian);
        }
#endif

        // 3b
        private bool ProcessItemCosts(
#if DEBUG
            bool isLogEnabled,
            uint unitId,
#endif
            BufferItem item,
            ref NetSegment prevSegment,
            ref NetLane prevLane,
            float prevMaxSpeed,
            float prevLaneSpeed,
#if ADVANCEDAI && ROUTING
            bool enableAdvancedAI,
            float laneChangingCost,
#endif
            ushort nextNodeId,
            ref NetNode nextNode,
            bool isMiddle,
            ushort nextSegmentId,
            ref NetSegment nextSegment,
#if ROUTING
            float segmentSelectionCost,
            float laneSelectionCost,
            LaneTransitionData? transition,
#endif
            ref int laneIndexFromInner,
            byte connectOffset,
            bool enableVehicle,
            bool enablePedestrian) {
#if !DEBUG
            const bool isLogEnabled = false;
            const uint unitId = 0;
#endif

            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    nextSegmentId,
                    "ProcessItemCosts called.\n"
                    + $"\tprevMaxSpeed={prevMaxSpeed}\n"
                    + $"\tprevLaneSpeed={prevLaneSpeed}\n"
#if ADVANCEDAI && ROUTING
                    + $"\tenableAdvancedAI={enableAdvancedAI}\n"
                    + $"\tlaneChangingCost={laneChangingCost}\n"
#endif
                    + $"\tnextNodeId={nextNodeId}\n"
                    + $"\tisMiddle={isMiddle}\n"
                    + $"\tnextSegmentId={nextSegmentId}\n"
#if ROUTING
                    + $"\tsegmentSelectionCost={segmentSelectionCost}\n"
                    + $"\tlaneSelectionCost={laneSelectionCost}\n"
                    + $"\ttransition={transition}\n"
#endif
                    + $"\tlaneIndexFromInner={laneIndexFromInner}\n"
                    + $"\tconnectOffset={connectOffset}\n"
                    + $"\tenableVehicle={enableVehicle}\n"
                    + $"\tenablePedestrian={enablePedestrian}");
            }

            bool blocked = false;
            if ((nextSegment.m_flags & disableMask_) != NetSegment.Flags.None) {
                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        nextSegmentId,
                        $"ProcessItemCosts: Aborting: Disable mask\n" +
                        $"\tm_disableMask={disableMask_}\n" +
                        $"\tnextSegment.m_flags={nextSegment.m_flags}\n");
                }

                return blocked;
            }

            NetManager netManager = Singleton<NetManager>.instance;
            NetInfo nextSegmentInfo = nextSegment.Info;
            NetInfo prevSegmentInfo = prevSegment.Info;
            int nextNumLanes = nextSegmentInfo.m_lanes.Length;
            NetInfo.Direction nextDir = nextNodeId != nextSegment.m_startNode
                              ? NetInfo.Direction.Forward
                              : NetInfo.Direction.Backward;
            NetInfo.Direction nextFinalDir =
                (nextSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None
                    ? nextDir
                    : NetInfo.InvertDirection(nextDir);

            // float prevMaxSpeed = 1f; // stock code commented
            // float prevLaneSpeed = 1f; // stock code commented
            NetInfo.LaneType prevLaneType = NetInfo.LaneType.None;
            VehicleInfo.VehicleType prevVehicleType = VehicleInfo.VehicleType.None;
            VehicleInfo.VehicleCategory prevVehicleCategory = VehicleInfo.VehicleCategory.None;

            if (item.Position.m_lane < prevSegmentInfo.m_lanes.Length) {
                NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[item.Position.m_lane];
                prevLaneType = prevLaneInfo.m_laneType;
                prevVehicleType = prevLaneInfo.m_vehicleType;
                prevVehicleCategory = prevLaneInfo.vehicleCategory;
                // prevMaxSpeed = prevLaneInfo.m_speedLimit; // stock code commented
                // prevLaneSpeed = CalculateLaneSpeed(prevMaxSpeed, connectOffset,
                // item.Position.m_offset, ref prevSegment, prevLaneInfo); // stock code commented
            }

            bool acuteTurningAngle = false;
            if (prevLaneType == NetInfo.LaneType.Vehicle &&
                (prevVehicleType & VehicleInfo.VehicleType.Car) == VehicleInfo.VehicleType.None) {
                acuteTurningAngle = !TrackUtils.CheckSegmentsTurnAngle(ref prevSegment, ref nextSegment, nextNodeId);
            }

            float prevLength = prevLaneType != NetInfo.LaneType.PublicTransport
                                 ? prevSegment.m_averageLength
                                 : prevLane.m_length;
            float offsetLength = Mathf.Abs(connectOffset - item.Position.m_offset) *
                               BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * prevLength;
            float methodDistance = item.MethodDistance + offsetLength;
            float duration = item.Duration + offsetLength / prevMaxSpeed;

            if (!stablePath_) {
#if ADVANCEDAI && ROUTING
                if (!enableAdvancedAI) {
#endif
                    offsetLength *=
                        (new Randomizer(pathFindIndex_ << 16 | item.Position.m_segment)
                             .Int32(900, 1000 + (prevSegment.m_trafficDensity * 10)) +
                         pathRandomizer_.Int32(20u)) * 0.001f;

                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            nextSegmentId,
                            "ProcessItemCosts: Applied stock segment randomization cost factor\n"
                            + $"\toffsetLength={offsetLength}");
                    }
#if ADVANCEDAI && ROUTING
                }
#endif
            }

            if ((prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                NetInfo.LaneType.None &&
                (prevVehicleType & vehicleTypes_) == VehicleInfo.VehicleType.Car &&
                (prevSegment.m_flags & carBanMask_) != NetSegment.Flags.None) {

                // is vehicle type Car, in the district with Old Town Policy, skip penalty if bus or taxi when allowed
                if ((!globalConf_.PathFinding.AllowTaxiInOldTownDistricts || (queueItem_.vehicleType & ExtVehicleType.Taxi) == 0) &&
                    (!globalConf_.PathFinding.AllowBusInOldTownDistricts || (queueItem_.vehicleType & ExtVehicleType.Bus) == 0)) {
                    offsetLength *= 7.5f;

                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            nextSegmentId,
                            $"ProcessItemCosts: Applied stock car ban cost factor\n" +
                            $"\toffsetLength={offsetLength}");
                    }
                } else if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        nextSegmentId,
                        $"ProcessItemCosts: Skipped car ban, vehicle {queueItem_.vehicleType}");
                }
            }

            if (transportVehicle_ && prevLaneType == NetInfo.LaneType.TransportVehicle) {
                offsetLength *= 0.95f;

                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        nextSegmentId,
                        $"ProcessItemCosts: Applied stock transport vehicle cost factor\n" +
                        $"\toffsetLength={offsetLength}");
                }
            }

#if ROUTING
            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    nextSegmentId,
                    $"ProcessItemCosts: Applying custom selection cost factors\n" +
                    $"\toffsetLength={offsetLength}\n" +
                    $"\tsegmentSelectionCost={segmentSelectionCost}\n" +
                    $"\tlaneSelectionCost={laneSelectionCost}\n");
            }

            offsetLength *= segmentSelectionCost;
            offsetLength *= laneSelectionCost;
            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    nextSegmentId,
                    $"ProcessItemCosts: Applied custom selection cost factors\n" +
                    $"\toffsetLength={offsetLength}");
            }
#endif
            float baseLength = offsetLength / (prevLaneSpeed * maxLength_); // NON-STOCK CODE
            float comparisonValue = item.ComparisonValue; // NON-STOCK CODE
#if ROUTING
            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    nextSegmentId,
                    $"ProcessItemCosts: Calculated base length\n\tbaseLength={baseLength}");
            }

            if (
#if ADVANCEDAI
                !enableAdvancedAI &&
#endif
                !stablePath_) {
                comparisonValue += baseLength;
            }
#endif
            int ticketCost = prevLane.m_ticketCost;
            if (!ignoreCost_ && ticketCost != 0) {
                comparisonValue += ticketCost * pathRandomizer_.Int32(2000u) *
                                   TICKET_COST_CONVERSION_FACTOR;
            }

            if ((prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                NetInfo.LaneType.None) {
                prevLaneType = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
            }

            Vector3 b = prevLane.CalculatePosition(
                connectOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
            int newLaneIndexFromInner = laneIndexFromInner;
            bool isTransition = (nextNode.m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;

            NetInfo.LaneType allowedLaneTypes = laneTypes_;
            VehicleInfo.VehicleType allowedVehicleTypes = vehicleTypes_;
            VehicleInfo.VehicleCategory currentVehicleCategory = vehicleCategory_;
            if (!enableVehicle) {
                allowedVehicleTypes &= VehicleInfo.VehicleType.Bicycle;
                if (allowedVehicleTypes == VehicleInfo.VehicleType.None) {
                    allowedLaneTypes &=
                        ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
                }
            }

            if (!enablePedestrian) {
                allowedLaneTypes &= ~NetInfo.LaneType.Pedestrian;
            }

            // NON-STOCK CODE START
            float pfPublicTransportTransitionMinPenalty =
                globalConf_.PathFinding.PublicTransportTransitionMinPenalty;
            float pfPublicTransportTransitionMaxPenalty =
                globalConf_.PathFinding.PublicTransportTransitionMaxPenalty;

            bool applyTransportTransferPenalty =
                    savedGameOptions_.realisticPublicTransport &&
                    !stablePath_ &&
                    (allowedLaneTypes &
                     (NetInfo.LaneType.PublicTransport | NetInfo.LaneType.Pedestrian)) ==
                    (NetInfo.LaneType.PublicTransport | NetInfo.LaneType.Pedestrian) &&
                    pfPublicTransportTransitionMinPenalty >= 0 &&
                    pfPublicTransportTransitionMaxPenalty >
                    pfPublicTransportTransitionMinPenalty;

            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    nextSegmentId,
                    $"ProcessItemCosts: Shall apply transport transfer penalty?\n" +
                    $"\tapplyTransportTransferPenalty={applyTransportTransferPenalty}\n" +
                    $"\tOptions.realisticPublicTransport={savedGameOptions_.realisticPublicTransport}\n" +
                    $"\tallowedLaneTypes={allowedLaneTypes}\n" +
                    $"\tallowedVehicleTypes={allowedVehicleTypes}\n" +
                    $"\tconf.pf.PubTranspTransitionMinPenalty={pfPublicTransportTransitionMinPenalty}\n" +
                    $"\tconf.pf.PubTranspTransitionMaxPenalty={pfPublicTransportTransitionMaxPenalty}");
            }

            int nextLaneIndex = 0;
            uint nextLaneId = nextSegment.m_lanes;
            int maxNextLaneIndex = nextNumLanes - 1;
#if ADVANCEDAI && ROUTING
            byte laneDist = 0;
#endif
#if ROUTING
            if (transition != null) {
                LaneTransitionData trans = (LaneTransitionData)transition;
                if (trans.laneIndex >= 0 && trans.laneIndex <= maxNextLaneIndex) {
                    nextLaneIndex = trans.laneIndex;
                    nextLaneId = trans.laneId;
                    maxNextLaneIndex = nextLaneIndex;
                } else {
                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            nextSegmentId,
                            "ProcessItemCosts: Invalid transition detected. Skipping.");
                    }

                    return blocked;
                }

                laneDist = trans.distance;
                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        nextSegmentId,
                        $"ProcessItemCosts: Custom transition given\n" +
                        $"\tnextLaneIndex={nextLaneIndex}\n" +
                        $"\tnextLaneId={nextLaneId}\n" +
                        $"\tmaxNextLaneIndex={maxNextLaneIndex}\n" +
                        $"\tlaneDist={laneDist}");
                }
            } else {
                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        nextSegmentId,
                        "ProcessItemCosts: No custom transition given");
                }
            }
#endif

            // NON-STOCK CODE END
            for (; nextLaneIndex <= maxNextLaneIndex && nextLaneId != 0; nextLaneIndex++) {
                NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];
                if ((nextLaneInfo.m_finalDirection & nextFinalDir) != NetInfo.Direction.None) {
                    if (nextLaneInfo.CheckType(allowedLaneTypes, allowedVehicleTypes, currentVehicleCategory) &&
                        (nextSegmentId != item.Position.m_segment ||
                         nextLaneIndex != item.Position.m_lane)) {
                        if (acuteTurningAngle &&
                            nextLaneInfo.m_laneType == NetInfo.LaneType.Vehicle &&
                            (nextLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) ==
                            VehicleInfo.VehicleType.None) {
                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    nextSegmentId,
                                    $"ProcessItemCosts: Skipping acute turning angle for {nextLaneInfo.m_laneType} {nextLaneInfo.m_vehicleType}");
                            }
                            continue;
                        }

                        BufferItem nextItem = default(BufferItem);
                        ref NetLane nextLane = ref nextLaneId.ToLane();

                        Vector3 a = (nextDir & NetInfo.Direction.Forward) == NetInfo.Direction.None
                                    ? nextLane.m_bezier.a
                                    : nextLane.m_bezier.d;
                        float transitionCost = Vector3.Distance(a, b);
                        if (isTransition) {
                            transitionCost *= 2f;
                        }

                        if (ticketCost != 0 && nextLane.m_ticketCost != 0) {
                            transitionCost *= 10f;
                        }

                        float nextMaxSpeed;
#if SPEEDLIMITS
                        // NON-STOCK CODE START
                        nextMaxSpeed = speedLimitManager.GetGameSpeedLimit(
                            nextSegmentId,
                            (byte)nextLaneIndex,
                            nextLaneId,
                            nextLaneInfo);
                        // NON-STOCK CODE END

                        if (!(nextMaxSpeed > 0f)) {
                            nextMaxSpeed = 1f;
                        }
#else
			nextMaxSpeed = !(nextLaneInfo.m_speedLimit > 0f) ? 1f : nextLaneInfo.m_speedLimit;
#endif

                        float transitionCostOverMeanMaxSpeed =
                            transitionCost / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * maxLength_);
#if ADVANCEDAI && ROUTING
                        if (!enableAdvancedAI) {
#endif
                            if (!stablePath_ && (nextLane.m_flags & (ushort)NetLane.Flags.Merge) != 0)
                            {
                                int firstTarget = nextLane.m_firstTarget;
                                int lastTarget = nextLane.m_lastTarget;

                                transitionCostOverMeanMaxSpeed *=
                                    new Randomizer(pathFindIndex_ ^ nextLaneId).Int32(
                                        1000,
                                        (lastTarget - firstTarget + 2) * 1000) * 0.001f;
                            }
#if ADVANCEDAI && ROUTING
                        }
#endif
                        if (!stablePath_ && (nextLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Plane) != 0)
                        {
                            transitionCostOverMeanMaxSpeed *= (float)new Randomizer(pathFindIndex_ ^ nextLaneId).Int32(10000u) * 0.001f;
                        }
                        nextItem.Position.m_segment = nextSegmentId;
                        nextItem.Position.m_lane = (byte)nextLaneIndex;
                        nextItem.Position.m_offset =
                            (byte)((nextDir & NetInfo.Direction.Forward) != NetInfo.Direction.None
                                       ? 255
                                       : 0);
                        if ((nextLaneInfo.m_laneType & prevLaneType) == NetInfo.LaneType.None) {
                            nextItem.MethodDistance = 0f;
                        } else {
                            nextItem.MethodDistance = methodDistance + transitionCost;
                        }

                        if (nextLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian &&
                            !(nextItem.MethodDistance <
                              globalConf_.PathFinding.MaxWalkingDistance) && !stablePath_) {
                            // NON-STOCK CODE (custom walking distance)
                            nextLaneId = nextLane.m_nextLane;
                            continue;
                        }

                        // NON-STOCK CODE START
                        if (applyTransportTransferPenalty) {
                            if (isMiddle &&
                                (nextLaneInfo.m_laneType & prevLaneType) == NetInfo.LaneType.None &&
                                (item.LanesUsed & NetInfo.LaneType.PublicTransport) !=
                                NetInfo.LaneType.None && nextLaneInfo.m_laneType ==
                                NetInfo.LaneType.PublicTransport)
                            {
                                // apply penalty when switching between public transport lines
                                float transportTransitionPenalty =
                                    (pfPublicTransportTransitionMinPenalty +
                                     nextNode.m_maxWaitTime *
                                     BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR *
                                     (pfPublicTransportTransitionMaxPenalty -
                                      pfPublicTransportTransitionMinPenalty)) / (0.5f * maxLength_);
                                transitionCostOverMeanMaxSpeed += transportTransitionPenalty;

                                if (isLogEnabled) {
                                    DebugLog(
                                        unitId,
                                        item,
                                        nextSegmentId,
                                        nextLaneIndex,
                                        nextLaneId,
                                        $"ProcessItemCosts: Applied transport transfer " +
                                        $"penalty on PT change\n" +
                                        $"\ttransportTransitionPenalty={transportTransitionPenalty}\n" +
                                        $"\ttransitionCostOverMeanMaxSpeed={transitionCostOverMeanMaxSpeed}\n" +
                                        $"\tisMiddle={isMiddle}\n" +
                                        $"\tnextLaneInfo.m_laneType={nextLaneInfo.m_laneType}\n" +
                                        $"\tprevLaneType={prevLaneType}\n" +
                                        $"\titem.m_lanesUsed={item.LanesUsed}\n" +
                                        $"\tnextLaneInfo.m_laneType={nextLaneInfo.m_laneType}");
                                }
                            } else if ((nextLaneId == startLaneA_ || nextLaneId == startLaneB_) &&
                                       (item.LanesUsed &
                                        (NetInfo.LaneType.Pedestrian |
                                         NetInfo.LaneType.PublicTransport)) ==
                                       NetInfo.LaneType.Pedestrian) {
                                // account for public transport transition costs on non-PT paths
                                float transportTransitionPenalty =
                                    (2f * pfPublicTransportTransitionMaxPenalty) /
                                    (0.5f * maxLength_);
                                transitionCostOverMeanMaxSpeed += transportTransitionPenalty;
                                if (isLogEnabled) {
                                    DebugLog(
                                        unitId,
                                        item,
                                        nextSegmentId,
                                        nextLaneIndex,
                                        nextLaneId,
                                        $"ProcessItemCosts: Applied transport transfer " +
                                        $"penalty on non-PT path\n" +
                                        $"\ttransportTransitionPenalty={transportTransitionPenalty}\n" +
                                        $"\ttransitionCostOverMeanMaxSpeed={transitionCostOverMeanMaxSpeed}");
                                }
                            }
                        }

                        // NON-STOCK CODE END
                        float currentComparisonValue = transitionCostOverMeanMaxSpeed;
                        float currentDuration = transitionCost / ((prevMaxSpeed + nextMaxSpeed) * 0.5f);
                        nextItem.Direction = nextDir;

                        if (nextLaneId == startLaneA_) {
                            if (((nextItem.Direction & NetInfo.Direction.Forward) ==
                                 NetInfo.Direction.None ||
                                 nextItem.Position.m_offset < startOffsetA_) &&
                                ((nextItem.Direction & NetInfo.Direction.Backward) ==
                                 NetInfo.Direction.None ||
                                 nextItem.Position.m_offset > startOffsetA_)) {
                                if (isLogEnabled) {
                                    DebugLog(
                                        unitId,
                                        item,
                                        nextSegmentId,
                                        nextLaneIndex,
                                        nextLaneId,
                                        $"ProcessItemCosts: Skipping: Invalid " +
                                        $"offset/direction on start lane A\n" +
                                        $"\tnextItem.m_direction={nextItem.Direction}\n" +
                                        $"\tnextItem.Position.m_offset={nextItem.Position.m_offset}\n" +
                                        $"\tm_startOffsetA={startOffsetA_}");
                                }

                                nextLaneId = nextLane.m_nextLane;
                                continue;
                            }

                            float nextLaneSpeed = CalculateLaneSpeed(
                                nextMaxSpeed,
                                startOffsetA_,
                                nextItem.Position.m_offset,
                                ref nextSegment,
                                nextLaneInfo);
                            float nextOffset = Mathf.Abs(nextItem.Position.m_offset - startOffsetA_) *
                                             BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

                            currentComparisonValue +=
                                nextOffset * nextSegment.m_averageLength /
                                (nextLaneSpeed * maxLength_);
                            currentDuration +=
                                nextOffset * nextSegment.m_averageLength / nextLaneSpeed;
                        }

                        if (nextLaneId == startLaneB_) {
                            if (((nextItem.Direction & NetInfo.Direction.Forward) ==
                                 NetInfo.Direction.None ||
                                 nextItem.Position.m_offset < startOffsetB_) &&
                                ((nextItem.Direction & NetInfo.Direction.Backward) ==
                                 NetInfo.Direction.None ||
                                 nextItem.Position.m_offset > startOffsetB_)) {
                                if (isLogEnabled) {
                                    DebugLog(
                                        unitId,
                                        item,
                                        nextSegmentId,
                                        nextLaneIndex,
                                        nextLaneId,
                                        $"ProcessItemCosts: Skipping: Invalid " +
                                        $"offset/direction on start lane B\n" +
                                        $"\tnextItem.m_direction={nextItem.Direction}\n" +
                                        $"\tnextItem.Position.m_offset={nextItem.Position.m_offset}\n" +
                                        $"\tm_startOffsetB={startOffsetB_}");
                                }

                                nextLaneId = nextLane.m_nextLane;
                                continue;
                            }

                            float nextLaneSpeed = CalculateLaneSpeed(
                                nextMaxSpeed,
                                startOffsetB_,
                                nextItem.Position.m_offset,
                                ref nextSegment,
                                nextLaneInfo);
                            float nextOffset = Mathf.Abs(nextItem.Position.m_offset - startOffsetB_) *
                                             BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

                            currentComparisonValue +=
                                nextOffset * nextSegment.m_averageLength /
                                (nextLaneSpeed * maxLength_);
                            currentDuration +=
                                nextOffset * nextSegment.m_averageLength / nextLaneSpeed;
                        }

                        if (!ignoreBlocked_ &&
                            (nextSegment.m_flags & NetSegment.Flags.Blocked) !=
                            NetSegment.Flags.None &&
                            (nextLaneInfo.m_laneType &
                             (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                            NetInfo.LaneType.None) {
                            currentComparisonValue += 0.1f;
                            blocked = true;
                            if ((allowedVehicleTypes & VehicleInfo.VehicleType.Plane) != 0)
                            {
                                continue;
                            }
                        }

                        nextItem.LaneId = nextLaneId;
                        nextItem.LanesUsed = item.LanesUsed | nextLaneInfo.m_laneType;
                        nextItem.VehiclesUsed = item.VehiclesUsed | nextLaneInfo.m_vehicleType;

#if ADVANCEDAI && ROUTING
                        // NON-STOCK CODE START
                        nextItem.TrafficRand = item.TrafficRand;

                        // NON-STOCK CODE END
#endif

#if ROUTING
#if ADVANCEDAI
                        if (enableAdvancedAI) {
                            float adjustedBaseLength = baseLength;
                            if (queueItem_.spawned ||
                                (nextLaneId != startLaneA_ && nextLaneId != startLaneB_)) {
                                if (laneDist != 0) {
                                    // apply lane changing costs
                                    adjustedBaseLength *=
                                        1f + laneDist * laneChangingCost *
                                        (laneDist > 1
                                             ? globalConf_
                                               .AdvancedVehicleAI.MoreThanOneLaneChangingCostFactor
                                             : 1f);
                                    // additional costs for changing multiple lanes at once
                                }
                            }

                            currentComparisonValue += adjustedBaseLength;

                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    nextSegmentId,
                                    nextLaneIndex,
                                    nextLaneId,
                                    $"ProcessItemCosts: Applied Advanced Vehicle AI\n" +
                                    $"\tbaseLength={baseLength}\n" +
                                    $"\tadjustedBaseLength={adjustedBaseLength}\n" +
                                    $"\tlaneDist={laneDist}\n\tlaneChangingCost={laneChangingCost}");
                            }
                        } else
#endif
                        if (stablePath_) {
                            // all non-road vehicles with stable paths (trains, trams, etc.):
                            // apply lane distance factor
                            float adjustedBaseLength = baseLength;
                            adjustedBaseLength *= 1 + laneDist;
                            currentComparisonValue += adjustedBaseLength;

                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    nextSegmentId,
                                    nextLaneIndex,
                                    nextLaneId,
                                    $"ProcessItemCosts: Applied stable path lane distance costs\n" +
                                    $"\tbaseLength={baseLength}\n" +
                                    $"\tadjustedBaseLength={adjustedBaseLength}\n\tlaneDist={laneDist}");
                            }
                        }
#endif

                        if ((nextLaneInfo.m_laneType & prevLaneType) != NetInfo.LaneType.None &&
                            (nextLaneInfo.m_vehicleType & vehicleTypes_) != VehicleInfo.VehicleType.None &&
                            (nextLaneInfo.vehicleCategory & vehicleCategory_) != VehicleInfo.VehicleCategory.None) {

#if ADVANCEDAI && ROUTING
                            if (!enableAdvancedAI) {
#endif
                                int firstTarget = nextLane.m_firstTarget;
                                int lastTarget = nextLane.m_lastTarget;
                                if (laneIndexFromInner < firstTarget ||
                                    laneIndexFromInner >= lastTarget) {
                                    currentComparisonValue +=
                                        Mathf.Max(1f, (transitionCost * 3f) - 3f) /
                                        ((prevMaxSpeed + nextMaxSpeed) * 0.5f * maxLength_);
                                }

                                if (isLogEnabled) {
                                    DebugLog(
                                        unitId,
                                        item,
                                        nextSegmentId,
                                        nextLaneIndex,
                                        nextLaneId,
                                        $"ProcessItemCosts: stock lane change costs\n" +
                                        $"\tfirstTarget={firstTarget}\n" +
                                        $"\tlastTarget={lastTarget}\n" +
                                        $"\tlaneIndexFromInner={laneIndexFromInner}");
                                }
#if ADVANCEDAI && ROUTING
                            }
#endif

                            if (!transportVehicle_ && nextLaneInfo.m_laneType ==
                                NetInfo.LaneType.TransportVehicle) {
                                currentComparisonValue +=
                                    20f / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * maxLength_);
                            }
                        }

                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                nextSegmentId,
                                nextLaneIndex,
                                nextLaneId,
                                $"ProcessItemCosts: Adding next item\n\tnextItem={nextItem}");
                        }

                        if ((nextLaneInfo.vehicleCategory & VehicleInfo.VehicleCategory.PublicTransportRoad) != 0 &&
                            (nextLaneInfo.vehicleCategory & ~(VehicleInfo.VehicleCategory.Bus | VehicleInfo.VehicleCategory.Trolleybus | VehicleInfo.VehicleCategory.Taxi)) == 0) {
                            currentComparisonValue -= baseLength * 0.25f;
                        }

                        nextItem.ComparisonValue = comparisonValue + currentComparisonValue;
                        nextItem.Duration = duration + currentDuration;
                        AddBufferItem(
#if DEBUG
                            isLogEnabled,
#endif
                            nextItem,
                            item.Position);
                    } else {
                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                nextSegmentId,
                                nextLaneIndex,
                                nextLaneId,
                                $"ProcessItemCosts: Lane type and/or vehicle type mismatch or " +
                                $"same segment/lane. Skipping." + // no newline
                                $"\tallowedLaneTypes={allowedLaneTypes}\n" +
                                $"\tallowedVehicleTypes={allowedVehicleTypes}\n" +
                                $"\tcurrentVehicleCategory={currentVehicleCategory}");
                        }
                    }
                } else {
                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            nextSegmentId,
                            nextLaneIndex,
                            nextLaneId,
                            $"ProcessItemCosts: Lane direction mismatch. Skipping." + // no newline
                            $"\tnextLaneInfo.m_finalDirection={nextLaneInfo.m_finalDirection}\n" +
                            $"\tnextFinalDir={nextFinalDir}");
                    }

                    if ((nextLaneInfo.m_laneType & prevLaneType) != NetInfo.LaneType.None &&
                        (nextLaneInfo.m_vehicleType & prevVehicleType) != VehicleInfo.VehicleType.None &&
                        (nextLaneInfo.vehicleCategory & prevVehicleCategory) != VehicleInfo.VehicleCategory.None) {
                        newLaneIndexFromInner++;
                    }
                }

                nextLaneId = netManager.m_lanes.m_buffer[nextLaneId].m_nextLane;
            }

            laneIndexFromInner = newLaneIndexFromInner;
            return blocked;
        }

        // 4
        private void ProcessItemPedBicycle(
#if DEBUG
            bool isLogEnabled,
            uint unitId,
#endif
            BufferItem item,
            ref NetSegment prevSegment,
            ref NetLane prevLane,
            float prevMaxSpeed,
            float prevLaneSpeed,
            ushort nextSegmentId,
            ref NetSegment nextSegment,
            ushort nextNodeId,
            ref NetNode nextNode,
            int nextLaneIndex,
            uint nextLaneId,
            ref NetLane nextLane,
            byte connectOffset,
            byte laneSwitchOffset) {
#if !DEBUG
            const bool isLogEnabled = false;
            const uint unitId = 0;
#endif

            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    nextSegmentId,
                    nextLaneIndex,
                    nextLaneId,
                    "ProcessItemPedBicycle called.\n" +
                    $"\tprevMaxSpeed={prevMaxSpeed}\n" +
                    $"\tprevLaneSpeed={prevLaneSpeed}\n" +
                    $"\tnextSegmentId={nextSegmentId}\n" +
                    $"\tnextNodeId={nextNodeId}\n" +
                    $"\tnextLaneIndex={nextLaneIndex}\n" +
                    $"\tnextLaneId={nextLaneId}\n" +
                    $"\tconnectOffset={connectOffset}\n" +
                    $"\tlaneSwitchOffset={laneSwitchOffset}");
            }

            if ((nextSegment.m_flags & disableMask_) != NetSegment.Flags.None) {
                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        nextSegmentId,
                        nextLaneIndex,
                        nextLaneId,
                        $"ProcessItemPedBicycle: Aborting: Disable mask\n" +
                        $"\tm_disableMask={disableMask_}\n" +
                        $"\tnextSegment.m_flags={nextSegment.m_flags}\n");
                }
                return;
            }

            // NON-STOCK CODE START
            bool mayCross = true;
#if JUNCTIONRESTRICTIONS || CUSTOMTRAFFICLIGHTS
            if (savedGameOptions_.junctionRestrictionsEnabled || savedGameOptions_.timedLightsEnabled) {
                bool nextIsStartNode = nextNodeId == nextSegment.m_startNode;

                if (nextIsStartNode || nextNodeId == nextSegment.m_endNode) {
#if JUNCTIONRESTRICTIONS
                    if (savedGameOptions_.junctionRestrictionsEnabled &&
                        item.Position.m_segment == nextSegmentId) {
                        // check if pedestrians are not allowed to cross here
                        if (!junctionManager.IsPedestrianCrossingAllowed(
                                nextSegmentId,
                                nextIsStartNode)) {
                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    nextSegmentId,
                                    nextLaneIndex,
                                    nextLaneId,
                                    "ProcessItemPedBicycle: Pedestrian crossing prohibited");
                            }
                            mayCross = false;
                        }
                    }
#endif

#if CUSTOMTRAFFICLIGHTS
                    if (savedGameOptions_.timedLightsEnabled) {
                        // check if pedestrian light won't change to green
                        CustomSegmentLights lights = customTrafficLightsManager.GetSegmentLights(nextSegmentId, nextIsStartNode, false);

                        if (lights != null && lights.InvalidPedestrianLight) {
                            if (isLogEnabled) {
                                DebugLog(
                                    unitId,
                                    item,
                                    nextSegmentId,
                                    nextLaneIndex,
                                    nextLaneId,
                                    "ProcessItemPedBicycle: Aborting: Invalid pedestrian light");
                            }

                            return;
                        }
                    }
#endif
                }
            }
#endif

            // NON-STOCK CODE END
            NetInfo nextSegmentInfo = nextSegment.Info;
            NetInfo prevSegmentInfo = prevSegment.Info;
            int nextNumLanes = nextSegmentInfo.m_lanes.Length;
            float distance;
            byte offset;
            if (nextSegmentId == item.Position.m_segment) {
                Vector3 b = prevLane.CalculatePosition(
                    laneSwitchOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
                Vector3 a = nextLane.CalculatePosition(
                    connectOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
                distance = Vector3.Distance(a, b);
                offset = connectOffset;
            } else {
                NetInfo.Direction direction = (NetInfo.Direction)(nextNodeId != nextSegment.m_startNode ? 1 : 2);
                Vector3 b = prevLane.CalculatePosition(
                    laneSwitchOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
                Vector3 a = (direction & NetInfo.Direction.Forward) == NetInfo.Direction.None
                            ? nextLane.m_bezier.a
                            : nextLane.m_bezier.d;
                distance = Vector3.Distance(a, b);
                offset = (byte)((direction & NetInfo.Direction.Forward) != NetInfo.Direction.None
                                    ? 255
                                    : 0);
            }

            // float prevMaxSpeed = 1f; // stock code commented
            // float prevLaneSpeed = 1f; // stock code commented
            NetInfo.LaneType prevLaneType = NetInfo.LaneType.None;

            if (item.Position.m_lane < prevSegmentInfo.m_lanes.Length) {
                NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[item.Position.m_lane];
                // prevMaxSpeed = prevLaneInfo.m_speedLimit; // stock code commented
                // prevLaneSpeed = CalculateLaneSpeed(prevMaxSpeed, laneSwitchOffset,
                // item.Position.m_offset, ref prevSegment, prevLaneInfo); // stock code commented
                prevLaneType = prevLaneInfo.m_laneType;
                if ((prevLaneType &
                     (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                    NetInfo.LaneType.None) {
                    prevLaneType = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
                }
            }

            float prevLength = prevLaneType != NetInfo.LaneType.PublicTransport
                                 ? prevSegment.m_averageLength
                                 : prevLane.m_length;
            float offsetLength = Mathf.Abs(laneSwitchOffset - item.Position.m_offset) *
                               BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * prevLength;
            float methodDistance = item.MethodDistance + offsetLength;
            float comparisonValue =
                item.ComparisonValue + (offsetLength / (prevLaneSpeed * maxLength_));
            float duration = item.Duration + (offsetLength / prevMaxSpeed);

            if (!ignoreCost_) {
                int ticketCost = prevLane.m_ticketCost;
                if (ticketCost != 0) {
                    comparisonValue += ticketCost * pathRandomizer_.Int32(2000u) * TICKET_COST_CONVERSION_FACTOR;
                }
            }

            if (nextLaneIndex >= nextNumLanes) {
                return;
            }

            NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];
            BufferItem nextItem = default(BufferItem);

            nextItem.Position.m_segment = nextSegmentId;
            nextItem.Position.m_lane = (byte)nextLaneIndex;
            nextItem.Position.m_offset = offset;

            // NON-STOCK CODE START
            if (!mayCross && nextLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian) {
                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        nextSegmentId,
                        nextLaneIndex,
                        nextLaneId,
                        "ProcessItemPedBicycle: Aborting: Crossing prohibited");
                }

                return;
            }

            // NON-STOCK CODE END
            if ((nextLaneInfo.m_laneType & prevLaneType) == NetInfo.LaneType.None) {
                nextItem.MethodDistance = 0f;
            } else {
                if (FloatUtil.IsZero(item.MethodDistance)) {
                    comparisonValue += 100f / (0.25f * maxLength_);
                }

                nextItem.MethodDistance = methodDistance + distance;
            }

            if (nextLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian &&
                !(nextItem.MethodDistance < globalConf_.PathFinding.MaxWalkingDistance) &&
                !stablePath_) {
                // NON-STOCK CODE (custom walking distance)
#if DEBUG
                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        nextSegmentId,
                        nextLaneIndex,
                        nextLaneId,
                        $"ProcessItemPedBicycle: Aborting: Max. walking distance exceeded\n" +
                        $"\tnextItem.m_methodDistance={nextItem.MethodDistance}");
                }
#endif
                return;
            }

#if SPEEDLIMITS
            // NON-STOCK CODE START
            float nextMaxSpeed = speedLimitManager.GetGameSpeedLimit(nextSegmentId, (byte)nextLaneIndex, nextLaneId, nextLaneInfo);

            if (!(nextMaxSpeed > 0f)) {
                nextMaxSpeed = 1f;
            }

            // NON-STOCK CODE END
#else
            var nextMaxSpeed = nextLaneInfo.m_speedLimit;
#endif

            float currentComparisonValue = distance /
                                       ((prevMaxSpeed + nextMaxSpeed) * 0.25f * maxLength_);
            float currentDuration = (distance / ((prevMaxSpeed + nextMaxSpeed) * 0.5f));

            nextItem.Direction =
                (nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None
                    ? NetInfo.InvertDirection(nextLaneInfo.m_finalDirection)
                    : nextLaneInfo.m_finalDirection;

            if (nextLaneId == startLaneA_) {
                if (((nextItem.Direction & NetInfo.Direction.Forward) == NetInfo.Direction.None ||
                     nextItem.Position.m_offset < startOffsetA_) &&
                    ((nextItem.Direction & NetInfo.Direction.Backward) == NetInfo.Direction.None ||
                     nextItem.Position.m_offset > startOffsetA_)) {
                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            nextSegmentId,
                            nextLaneIndex,
                            nextLaneId,
                            $"ProcessItemPedBicycle: Aborting: Invalid offset/direction on start lane A\n" +
                            $"\tnextItem.m_direction={nextItem.Direction}\n" +
                            $"\tnextItem.Position.m_offset={nextItem.Position.m_offset}\n" +
                            $"\tm_startOffsetA={startOffsetA_}");
                    }

                    return;
                }

                float nextSpeed = CalculateLaneSpeed(
                    nextMaxSpeed,
                    startOffsetA_,
                    nextItem.Position.m_offset,
                    ref nextSegment,
                    nextLaneInfo);
                float nextOffset = Mathf.Abs(nextItem.Position.m_offset - startOffsetA_) *
                                 BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

                currentComparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * maxLength_);
                currentDuration += nextOffset * nextSegment.m_averageLength / nextSpeed;
            }

            if (nextLaneId == startLaneB_) {
                if (((nextItem.Direction & NetInfo.Direction.Forward) == NetInfo.Direction.None ||
                     nextItem.Position.m_offset < startOffsetB_) &&
                    ((nextItem.Direction & NetInfo.Direction.Backward) == NetInfo.Direction.None ||
                     nextItem.Position.m_offset > startOffsetB_)) {
                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            nextSegmentId,
                            nextLaneIndex,
                            nextLaneId,
                            $"ProcessItemPedBicycle: Aborting: Invalid offset/direction on start lane B\n" +
                            $"\tnextItem.m_direction={nextItem.Direction}\n" +
                            $"\tnextItem.Position.m_offset={nextItem.Position.m_offset}\n" +
                            $"\tm_startOffsetB={startOffsetB_}");
                    }
                    return;
                }

                float nextSpeed = CalculateLaneSpeed(
                    nextMaxSpeed,
                    startOffsetB_,
                    nextItem.Position.m_offset,
                    ref nextSegment,
                    nextLaneInfo);
                float nextOffset = Mathf.Abs(nextItem.Position.m_offset - startOffsetB_) *
                                 BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

                currentComparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * maxLength_);
                currentDuration += nextOffset * nextSegment.m_averageLength / nextSpeed;
            }

            nextItem.ComparisonValue = comparisonValue + currentComparisonValue;
            nextItem.Duration = duration + currentDuration;
            nextItem.LaneId = nextLaneId;
            nextItem.LanesUsed = item.LanesUsed | nextLaneInfo.m_laneType;
            nextItem.VehiclesUsed = item.VehiclesUsed | nextLaneInfo.m_vehicleType;

#if ADVANCEDAI && ROUTING
            // NON-STOCK CODE START
            nextItem.TrafficRand = item.TrafficRand;

            // NON-STOCK CODE END
#endif

            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    nextSegmentId,
                    nextLaneIndex,
                    nextLaneId,
                    $"ProcessItemPedBicycle: Adding next item\n\tnextItem={nextItem}");
            }

            AddBufferItem(
#if DEBUG
                isLogEnabled,
#endif
                nextItem,
                item.Position);
        }

#if ROUTING
        // 5 (custom: process routed vehicle paths)
        private bool ProcessItemRouted(
#if DEBUG
            bool isLogEnabled,
            uint unitId,
#endif
            BufferItem item,
            ref NetSegment prevSegment,
            ref NetLane prevLane,
            float prevMaxSpeed,
            float prevLaneSpeed,
#if ADVANCEDAI
            bool enableAdvancedAI,
            float laneChangingCost,
#endif
            float segmentSelectionCost,
            float laneSelectionCost,
            ushort nextNodeId,
            ref NetNode nextNode,
            bool isMiddle,
            SegmentRoutingData prevSegmentRouting,
            LaneEndRoutingData prevLaneEndRouting,
            byte connectOffset,
            int prevInnerSimilarLaneIndex) {
#if !DEBUG
            const bool isLogEnabled = false;
            const uint unitId = 0;
#endif

            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    "ProcessItemRouted called.\n"
                    + "\t" + $"prevMaxSpeed={prevMaxSpeed}\n"
                    + "\t" + $"prevLaneSpeed={prevLaneSpeed}\n"
#if ADVANCEDAI
                    + "\t" + $"enableAdvancedAI={enableAdvancedAI}\n"
                    + "\t" + $"laneChangingCost={laneChangingCost}\n"
#endif
                    + "\t" + $"segmentSelectionCost={segmentSelectionCost}\n"
                    + "\t" + $"laneSelectionCost={laneSelectionCost}\n"
                    + "\t" + $"nextNodeId={nextNodeId}\n"
                    + "\t" + $"isMiddle={isMiddle}\n"
                    + "\t" + $"prevSegmentRouting={prevSegmentRouting}\n"
                    + "\t" + $"prevLaneEndRouting={prevLaneEndRouting}\n"
                    + "\t" + $"connectOffset={connectOffset}\n"
                    + "\t" + $"prevInnerSimilarLaneIndex={prevInnerSimilarLaneIndex}\n");
            }

            /*
             * =====================================================================================
             * Fetch lane end transitions, check if there are any present
             * =====================================================================================
             */
            LaneTransitionData[] laneTransitions = prevLaneEndRouting.transitions;
            if (laneTransitions == null) {
                if (isLogEnabled) {
                    DebugLog(unitId, item, "ProcessItemRouted: Aborting: No lane transitions");
                }

                return false;
            }

            ushort prevSegmentId = item.Position.m_segment;
            int prevLaneIndex = item.Position.m_lane;
            NetInfo prevSegmentInfo = prevSegment.Info;
            if (prevLaneIndex >= prevSegmentInfo.m_lanes.Length) {
                if (isLogEnabled) {
                    DebugLog(unitId, item, "ProcessItemRouted: Aborting: Invalid lane index");
                }

                return false;
            }

            NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[item.Position.m_lane];

#if VEHICLERESTRICTIONS
            /*
             * =====================================================================================
             * Check vehicle restrictions, especially bans
             * =====================================================================================
             */
            bool canUseLane = CanUseLane(prevSegmentId, prevSegmentInfo, prevLaneIndex, prevLaneInfo, item.LaneId);
            if (!canUseLane && savedGameOptions_.vehicleRestrictionsAggression ==
                VehicleRestrictionsAggression.Strict) {
                // vehicle is strictly prohibited to use this lane
                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        "ProcessItemRouted: Vehicle restrictions: Aborting: Strict vehicle restrictions active");
                }
                return false;
            }
#endif

            bool strictLaneRouting = isLaneArrowObeyingEntity_ &&
                                    nextNode.Info.m_class.m_service !=
                                    ItemClass.Service.Beautification &&
                                    (nextNode.m_flags & NetNode.Flags.Untouchable) ==
                                    NetNode.Flags.None;
            bool prevIsCarLane =
                (prevLaneInfo.m_laneType &
                 (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                NetInfo.LaneType.None &&
                (prevLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) !=
                VehicleInfo.VehicleType.None;

            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    $"ProcessItemRouted: Strict lane routing? {strictLaneRouting}\n" +
                    $"\tm_isLaneArrowObeyingEntity={isLaneArrowObeyingEntity_}\n" +
                    $"\tnextNode.Info.m_class.m_service={nextNode.Info.m_class.m_service}\n" +
                    $"\tnextNode.m_flags={nextNode.m_flags}\n\tprevIsCarLane={prevIsCarLane}");
            }

            /*
             * =====================================================================================
             * Check if u-turns may be performed
             * =====================================================================================
             */
            bool isUturnAllowedHere = false; // is u-turn allowed at this place?
            if ((vehicleTypes_ &
                 (VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Monorail | VehicleInfo.VehicleType.Trolleybus)) ==
                VehicleInfo.VehicleType.None) {
                // is vehicle able to perform a u-turn?
                bool isStockUturnPoint = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;

#if JUNCTIONRESTRICTIONS
                if (savedGameOptions_.junctionRestrictionsEnabled) {
                    bool nextIsStartNode = nextNodeId == prevSegment.m_startNode;
                    bool prevIsOutgoingOneWay =
                        nextIsStartNode
                            ? prevSegmentRouting.startNodeOutgoingOneWay
                            : prevSegmentRouting.endNodeOutgoingOneWay;

                    // determine if the vehicle may u-turn at the target node, according to customization
                    isUturnAllowedHere =
                        isRoadVehicle_ && // only road vehicles may perform u-turns
                        prevIsCarLane && // u-turns for road vehicles only
                        (!isHeavyVehicle_ || isStockUturnPoint) && // only small vehicles may perform u-turns OR everyone at stock u-turn points
                        !prevIsOutgoingOneWay && // do not u-turn on one-ways
                        junctionManager.IsUturnAllowed(prevSegmentId, nextIsStartNode);

                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            $"ProcessItemRouted: Junction restrictions: Is u-turn allowed here? {isUturnAllowedHere}\n" +
                            $"\tm_isRoadVehicle={isRoadVehicle_}\n" +
                            $"\tprevIsCarLane={prevIsCarLane}\n" +
                            $"\tm_isHeavyVehicle={isHeavyVehicle_}\n" +
                            $"\tisStockUturnPoint={isStockUturnPoint}\n" +
                            $"\tprevIsOutgoingOneWay={prevIsOutgoingOneWay}\n" +
                            $"\tjManager.IsUturnAllowed(prevSegmentId, " +
                            $"nextIsStartNode)={junctionManager.IsUturnAllowed(prevSegmentId, nextIsStartNode)}\n" +
                            $"\tm_queueItem.vehicleId={queueItem_.vehicleId}\n" +
                            $"\tm_queueItem.spawned={queueItem_.spawned}\n" +
                            $"\tprevSegmentId={prevSegmentId}\n" +
                            $"\tm_startSegmentA={startSegmentA_}\n" +
                            $"\tm_startSegmentB={startSegmentB_}");
                    }
                } else {
#endif
                    isUturnAllowedHere = isStockUturnPoint;

                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            $"ProcessItemRouted: Junction restrictions disabled: Is u-turn allowed here? {isUturnAllowedHere}");
                    }

#if JUNCTIONRESTRICTIONS
                }
#endif
            }

#if VEHICLERESTRICTIONS
            /*
             * =====================================================================================
             * Apply vehicle restriction costs
             * =====================================================================================
             */
            if (!canUseLane) {
                laneSelectionCost *=
                    VehicleRestrictionsManager.PATHFIND_PENALTIES[
                        (int)savedGameOptions_.vehicleRestrictionsAggression];
                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        $"ProcessItemRouted: Vehicle restrictions: Applied lane costs\n\tlaneSelectionCost={laneSelectionCost}");
                }
            }
#endif

            /*
             * =======================================================================================================
             * Apply costs for large vehicles using inner lanes on highways
             * =======================================================================================================
             */
            if (savedGameOptions_.preferOuterLane &&
                isHeavyVehicle_ &&
                isRoadVehicle_ &&
                prevIsCarLane &&
                prevSegmentRouting.highway &&
                prevLaneInfo.m_similarLaneCount > 1 &&
                pathRandomizer_.Int32(globalConf_.PathFinding.HeavyVehicleInnerLanePenaltySegmentSel) == 0) {

                int prevOuterSimilarLaneIndex = routingManager.CalcOuterSimilarLaneIndex(prevLaneInfo);
                float prevRelOuterLane = prevOuterSimilarLaneIndex / (float)(prevLaneInfo.m_similarLaneCount - 1);
                laneSelectionCost *= 1f + globalConf_.PathFinding.HeavyVehicleMaxInnerLanePenalty * prevRelOuterLane;

                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        "ProcessItemRouted: Heavy trucks prefer outer lanes on highways: Applied lane costs\n"
                        + "\t" + $"laneSelectionCost={laneSelectionCost}\n"
                        + "\t" + $"savedGameOptions_.preferOuterLane={savedGameOptions_.preferOuterLane}\n"
                        + "\t" + $"m_isHeavyVehicle={isHeavyVehicle_}\n"
                        + "\t" + $"m_isRoadVehicle={isRoadVehicle_}\n"
                        + "\t" + $"prevIsCarLane={prevIsCarLane}\n"
                        + "\t" + $"prevSegmentRouting.highway={prevSegmentRouting.highway}\n"
                        + "\t" +
                        $"prevLaneInfo.m_similarLaneCount={prevLaneInfo.m_similarLaneCount}\n"
                        + "\t" + $"prevOuterSimilarLaneIndex={prevOuterSimilarLaneIndex}\n"
                        + "\t" + $"prevRelOuterLane={prevRelOuterLane}");
                }
            }

            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    "ProcessItemRouted: Final cost factors:\n"
                    + "\t" + $"segmentSelectionCost={segmentSelectionCost}\n"
                    + "\t" + $"laneSelectionCost={laneSelectionCost}\n"
                    + "\t" + $"laneChangingCost={laneChangingCost}");
            }

            /*
             * =======================================================================================================
             * Explore available lane end routings
             * =======================================================================================================
             */
            NetManager netManager = Singleton<NetManager>.instance;
            bool blocked = false;
            bool uturnExplored = false;
            for (int k = 0; k < laneTransitions.Length; ++k) {
                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        laneTransitions[k].segmentId,
                        laneTransitions[k].laneIndex,
                        laneTransitions[k].laneId,
                        $"ProcessItemRouted: Exploring lane transition #{k}: {laneTransitions[k]}");
                }

                ushort nextSegmentId = laneTransitions[k].segmentId;

                if (nextSegmentId == 0) {
                    continue;
                }

                if (laneTransitions[k].type == LaneEndTransitionType.Invalid) {
                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            laneTransitions[k].segmentId,
                            laneTransitions[k].laneIndex,
                            laneTransitions[k].laneId,
                            "ProcessItemRouted: Skipping transition: Transition is invalid");
                    }

                    continue;
                }

                LaneEndTransitionGroup laneEndTransitionGroup = TrackUtils.GetLaneEndTransitionGroup(vehicleTypes_);
                if ((laneTransitions[k].group & laneEndTransitionGroup) == 0) {
                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            laneTransitions[k].segmentId,
                            laneTransitions[k].laneIndex,
                            laneTransitions[k].laneId,
                            $"ProcessItemRouted: Skipping transition: Transition '{laneTransitions[k].group}' does not match '{vehicleTypes_}'");
                    }
                    continue;
                }

                if (nextSegmentId == prevSegmentId) {
                    if (!isUturnAllowedHere) {
                        if (isLogEnabled) {
                            DebugLog(
                                unitId,
                                item,
                                laneTransitions[k].segmentId,
                                laneTransitions[k].laneIndex,
                                laneTransitions[k].laneId,
                                "ProcessItemRouted: Skipping transition: U-turn is not allowed here");
                        }

                        // prevent double/forbidden exploration of previous segment by vanilla code during this method execution
                        continue;
                    }

                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            laneTransitions[k].segmentId,
                            laneTransitions[k].laneIndex,
                            laneTransitions[k].laneId,
                            "ProcessItemRouted: Processing transition: Exploring u-turn");
                    }

                    // we are going to explore a regular u-turn
                    uturnExplored = true;
                }

                // allow vehicles to ignore strict lane routing when moving off
                bool relaxedLaneRouting = isRoadVehicle_ &&
                                         (!queueItem_.spawned ||
                                          (queueItem_.vehicleType &
                                           (ExtVehicleType.PublicTransport |
                                            ExtVehicleType.Emergency)) != ExtVehicleType.None) &&
                                         (laneTransitions[k].laneId == startLaneA_ ||
                                          laneTransitions[k].laneId == startLaneB_);

                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        laneTransitions[k].segmentId,
                        laneTransitions[k].laneIndex,
                        laneTransitions[k].laneId,
                        $"ProcessItemRouted: Relaxed lane routing? {relaxedLaneRouting}\n" +
                        $"\trelaxedLaneRouting={relaxedLaneRouting}\n" +
                        $"\tm_isRoadVehicle={isRoadVehicle_}\n" +
                        $"\tm_queueItem.spawned={queueItem_.spawned}\n" +
                        $"\tm_queueItem.vehicleType={queueItem_.vehicleType}\n" +
                        $"\tm_queueItem.vehicleId={queueItem_.vehicleId}\n" +
                        $"\tm_startLaneA={startLaneA_}\n" +
                        $"\tm_startLaneB={startLaneB_}");
                }

                if (!relaxedLaneRouting && strictLaneRouting &&
                    laneTransitions[k].type == LaneEndTransitionType.Relaxed) {
                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            laneTransitions[k].segmentId,
                            laneTransitions[k].laneIndex,
                            laneTransitions[k].laneId,
                            $"ProcessItemRouted: Aborting: Cannot explore relaxed lane\n" +
                            $"\trelaxedLaneRouting={relaxedLaneRouting}\n" +
                            $"\tstrictLaneRouting={strictLaneRouting}\n" +
                            $"\tlaneTransitions[k].type={laneTransitions[k].type}");
                    }
                    continue;
                }

                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        laneTransitions[k].segmentId,
                        laneTransitions[k].laneIndex,
                        laneTransitions[k].laneId,
                        $"ProcessItemRouted: Exploring lane transition now\n" +
                        $"\tenableAdvancedAI={enableAdvancedAI}\n" +
                        $"\tlaneChangingCost={laneChangingCost}\n" +
                        $"\tsegmentSelectionCost={segmentSelectionCost}\n" +
                        $"\tlaneSelectionCost={laneSelectionCost}");
                }

                if (ProcessItemCosts(
#if DEBUG
                        isLogEnabled,
                        unitId,
#endif
                        item,
                        ref prevSegment,
                        ref prevLane,
                        prevMaxSpeed,
                        prevLaneSpeed,
#if ADVANCEDAI
                        enableAdvancedAI,
                        laneChangingCost,
#endif
                        nextNodeId,
                        ref nextNode,
                        isMiddle,
                        nextSegmentId,
                        ref nextSegmentId.ToSegment(),
                        segmentSelectionCost,
                        laneSelectionCost,
                        laneTransitions[k],
                        ref prevInnerSimilarLaneIndex,
                        connectOffset,
                        true,
                        false))
                {
                    blocked = true;
                }
            }

            return blocked && !uturnExplored;
        }
#endif

        private void AddBufferItem(
#if DEBUG
            bool isLogEnabled,
#endif
            BufferItem item,
            PathUnit.Position target) {
#if DEBUG
            if (isLogEnabled) {
                debugPositions_[target.m_segment].Add(item.Position.m_segment);
            }
#endif

            uint laneLocation = laneLocation_[item.LaneId];
            uint locPathFindIndex = laneLocation >> 16; // upper 16 bit, expected (?) path find index
            int bufferIndex = (int)(laneLocation & 65535u); // lower 16 bit
            int comparisonBufferPos;

            if (locPathFindIndex == pathFindIndex_) {
                if (item.ComparisonValue >= buffer_[bufferIndex].ComparisonValue) {
                    return;
                }

                int bufferPosIndex = bufferIndex >> 6; // arithmetic shift (sign stays), upper 10 bit
                int bufferPos = bufferIndex & -64; // upper 10 bit (no shift)

                if (bufferPosIndex < bufferMinPos_ ||
                    (bufferPosIndex == bufferMinPos_ && bufferPos < bufferMin_[bufferPosIndex])) {
                    return;
                }

                comparisonBufferPos = Mathf.Max(Mathf.RoundToInt(item.ComparisonValue * 1024f), bufferMinPos_);

                if (comparisonBufferPos == bufferPosIndex) {
                    buffer_[bufferIndex] = item;
                    laneTarget_[item.LaneId] = target;
                    return;
                }

                int newBufferIndex = bufferPosIndex << 6 | bufferMax_[bufferPosIndex]--;
                BufferItem bufferItem = buffer_[newBufferIndex];
                laneLocation_[bufferItem.LaneId] = laneLocation;
                buffer_[bufferIndex] = bufferItem;
            } else {
                comparisonBufferPos = Mathf.Max(Mathf.RoundToInt(item.ComparisonValue * 1024f), bufferMinPos_);
            }

            if (comparisonBufferPos >= 1024 || comparisonBufferPos < 0) {
                return;
            }

            while (bufferMax_[comparisonBufferPos] == 63) {
                ++comparisonBufferPos;
                if (comparisonBufferPos == 1024) {
                    return;
                }
            }

            if (comparisonBufferPos > bufferMaxPos_) {
                bufferMaxPos_ = comparisonBufferPos;
            }

            bufferIndex = comparisonBufferPos << 6 | ++bufferMax_[comparisonBufferPos];
            buffer_[bufferIndex] = item;
            laneLocation_[item.LaneId] = pathFindIndex_ << 16 | (uint)bufferIndex;
            laneTarget_[item.LaneId] = target;
        }

        /// <summary>
        /// Calculated Lane speed based on lane direction and ensures value > 0f
        /// </summary>
        private float CalculateLaneSpeed(float maxSpeed,
                                         byte startOffset,
                                         byte endOffset,
                                         ref NetSegment segment,
                                         NetInfo.Lane laneInfo) {
            NetInfo.Direction direction = (segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None
                                ? laneInfo.m_finalDirection
                                : NetInfo.InvertDirection(laneInfo.m_finalDirection);

            if ((direction & NetInfo.Direction.Avoid) == NetInfo.Direction.None) {
                return !(maxSpeed > 0f) ? 1f : maxSpeed;
            }

            if (endOffset > startOffset && direction == NetInfo.Direction.AvoidForward) {
                return !(maxSpeed > 0f) ? 0.1f : maxSpeed * 0.1f;
            }

            if (endOffset < startOffset && direction == NetInfo.Direction.AvoidBackward) {
                return !(maxSpeed > 0f) ? 0.1f : maxSpeed * 0.1f;
            }

            return !(maxSpeed > 0f) ? 0.2f : maxSpeed * 0.2f;
        }

        private void GetLaneDirection(
            PathUnit.Position pathPos,
            out NetInfo.Direction direction,
            out NetInfo.LaneType laneType,
            out VehicleInfo.VehicleType vehicleType) {
            ref NetSegment netSegment = ref pathPos.m_segment.ToSegment();
            NetInfo info = netSegment.Info;
            if (info.m_lanes.Length > pathPos.m_lane) {
                direction = info.m_lanes[pathPos.m_lane].m_finalDirection;
                laneType = info.m_lanes[pathPos.m_lane].m_laneType;
                vehicleType = info.m_lanes[pathPos.m_lane].m_vehicleType;

                if ((netSegment.m_flags &
                     NetSegment.Flags.Invert) != NetSegment.Flags.None) {
                    direction = NetInfo.InvertDirection(direction);
                }
            } else {
                direction = NetInfo.Direction.None;
                laneType = NetInfo.LaneType.None;
                vehicleType = VehicleInfo.VehicleType.None;
            }
        }

#if VEHICLERESTRICTIONS
        /// <summary>
        /// Check if lane can be used by vehicle requesting path
        /// </summary>
        /// <param name="segmentId">segment ID</param>
        /// <param name="segmentInfo">">segment NetInfo</param>
        /// <param name="laneIndex">lane index</param>
        /// <param name="laneInfo">lane LaneInfo</param>
        /// <param name="laneId">lane ID</param>
        /// <returns>true if vehicle restrictions is disabled,
        /// lane vehicle restriction allow vehicle type or lane is start/end of the path
        /// otherwise false</returns>
        private bool CanUseLane(ushort segmentId,
                                NetInfo segmentInfo,
                                int laneIndex,
                                NetInfo.Lane laneInfo,
                                uint laneId) {
            if (!savedGameOptions_.vehicleRestrictionsEnabled ||
                queueItem_.vehicleType == ExtVehicleType.None ||
                queueItem_.vehicleType == ExtVehicleType.Tram ||
                queueItem_.vehicleType == ExtVehicleType.Trolleybus ||
                (laneInfo.m_vehicleType &
                 (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train)) ==
                VehicleInfo.VehicleType.None) {
                return true;
            }

            if (startLaneA_ == laneId || startLaneB_ == laneId ||
                endLaneA_ == laneId || endLaneB_ == laneId) {
                return true;
            }

            ExtVehicleType allowedLaneVehicleTypes = vehicleRestrictionsManager.GetAllowedVehicleTypes(
                segmentId,
                segmentInfo,
                (uint)laneIndex,
                laneInfo,
                VehicleRestrictionsMode.Configured);
            return (allowedLaneVehicleTypes & allowedPathVehicleTypes_) != ExtVehicleType.None;
        }

        /// <summary>
        /// Maps all allowed vehicle types to mixed ExtVehicleType
        /// Later used to correctly test lane for match with configured allowed vehicle types
        /// </summary>
        /// <param name="vehicleTypes">Allowed additional vehicle types</param>
        /// <returns>Combined ExtVehicleType of all alowed cargo vehicle lane types</returns>
        private ExtVehicleType VehicleTypesToMixedCargoExtVehicle(VehicleInfo.VehicleType vehicleTypes) {
            ExtVehicleType extVehicleType = ExtVehicleType.None;
            if ((vehicleTypes & VehicleInfo.VehicleType.Train) != VehicleInfo.VehicleType.None) {
                extVehicleType |= ExtVehicleType.CargoTrain;
            }
            if ((vehicleTypes & VehicleInfo.VehicleType.Plane) != VehicleInfo.VehicleType.None) {
                extVehicleType |= ExtVehicleType.CargoPlane;
            }
            if ((vehicleTypes & VehicleInfo.VehicleType.Ship) != VehicleInfo.VehicleType.None) {
                extVehicleType |= ExtVehicleType.CargoShip;
            }

            return extVehicleType;
        }

#endif

#if ADVANCEDAI && ROUTING
        private void CalculateAdvancedAiCostFactors(
#if DEBUG
            bool isLogEnabled,
            uint unitId,
#endif
            ref BufferItem item,
            ref NetSegment prevSegment,
            ref NetLane prevLane,
            ushort nextNodeId,
            ref NetNode nextNode,
            ref float segmentSelectionCost,
            ref float laneSelectionCost,
            ref float laneChangingCost)
        {
#if !DEBUG
            const bool isLogEnabled = false;
            const uint unitId = 0;
#endif
            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    $"CalculateAdvancedAiCostFactors called.\n" +
                    $"\tnextNodeId={nextNodeId}\n" +
                    $"\tsegmentSelectionCost={segmentSelectionCost}\n" +
                    $"\tlaneSelectionCost={laneSelectionCost}\n" +
                    $"\tlaneChangingCost={laneChangingCost}");
            }

            NetInfo prevSegmentInfo = prevSegment.Info;
            bool nextIsJunction = (nextNode.m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) == NetNode.Flags.Junction;

            if (nextIsJunction) {
                /*
                 * =================================================================================
                 * Calculate costs for randomized lane selection behind junctions and highway transitions
                 * =================================================================================
                 */
                // TODO check if highway transitions are actually covered by this code
                if (!isHeavyVehicle_ &&
                    globalConf_.AdvancedVehicleAI.LaneRandomizationJunctionSel > 0 &&
                    pathRandomizer_.Int32(globalConf_.AdvancedVehicleAI.LaneRandomizationJunctionSel) == 0 &&
                    pathRandomizer_.Int32((uint)prevSegmentInfo.m_lanes.Length) == 0)
                {
                    // randomized lane selection at junctions
                    laneSelectionCost *= 1f + globalConf_.AdvancedVehicleAI.LaneRandomizationCostFactor;

                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            $"CalculateAdvancedAiCostFactors: Calculated randomized lane selection costs\n" +
                            $"\tlaneSelectionCost={laneSelectionCost}");
                    }
                }

                /*
                 * =================================================================================
                 * Calculate junction costs
                 * =================================================================================
                 */
                // TODO if (prevSegmentRouting.highway) ?
                segmentSelectionCost *= 1f + globalConf_.AdvancedVehicleAI.JunctionBaseCost;

                if (isLogEnabled) {
                    DebugLog(
                        unitId,
                        item,
                        "CalculateAdvancedAiCostFactors: Calculated junction costs\n" +
                        "\t" + $"segmentSelectionCost={segmentSelectionCost}");
                }
            }

            bool nextIsStartNode = prevSegment.m_startNode == nextNodeId;
            bool nextIsEndNode = nextNodeId == prevSegment.m_endNode;
            if (nextIsStartNode || nextIsEndNode) { // next node is a regular node
                /*
                 * =================================================================================
                 * Calculate traffic measurement costs for segment selection
                 * =================================================================================
                 */
                NetInfo.Direction prevFinalDir = nextIsStartNode
                                       ? NetInfo.Direction.Forward
                                       : NetInfo.Direction.Backward;
                prevFinalDir =
                    (prevSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None
                        ? prevFinalDir
                        : NetInfo.InvertDirection(prevFinalDir);

                float segmentTraffic = Mathf.Clamp(
                    1f - trafficMeasurementManager
                         .SegmentDirTrafficData[trafficMeasurementManager.GetDirIndex(
                                                    item.Position.m_segment,
                                                    prevFinalDir)].meanSpeed /
                    (float)TrafficMeasurementManager.REF_REL_SPEED + item.TrafficRand,
                    0,
                    1f);

                segmentSelectionCost *=
                    1f + globalConf_.AdvancedVehicleAI.TrafficCostFactor * segmentTraffic;

                if (globalConf_.AdvancedVehicleAI.LaneDensityRandInterval > 0 && nextIsJunction &&
                    (nextNode.m_flags & (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)) !=
                    (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)) {
                    item.TrafficRand =
                        0.01f * (pathRandomizer_.Int32(
                                     (uint)globalConf_.AdvancedVehicleAI.LaneDensityRandInterval +
                                     1u) - (globalConf_.AdvancedVehicleAI.LaneDensityRandInterval / 2f));
                }

                // check previous node
                if (globalConf_.AdvancedVehicleAI.LaneChangingJunctionBaseCost > 0 &&
                    (Singleton<NetManager>
                     .instance.m_nodes
                     .m_buffer[nextIsStartNode ? prevSegment.m_endNode : prevSegment.m_startNode]
                     .m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) ==
                    NetNode.Flags.Junction) {
                    /*
                     * =============================================================================
                     * Calculate lane changing base cost factor when in front of junctions
                     * =============================================================================
                     */
                    laneChangingCost *= globalConf_.AdvancedVehicleAI.LaneChangingJunctionBaseCost;

                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            "CalculateAdvancedAiCostFactors: Calculated in-front-of-junction lane changing costs\n" +
                            "\t" + $"laneChangingCost={laneChangingCost}");
                    }
                }

                /*
                 * =================================================================================
                 * Calculate general lane changing base cost factor
                 * =================================================================================
                 */
                if (globalConf_.AdvancedVehicleAI.LaneChangingBaseMinCost > 0 &&
                    globalConf_.AdvancedVehicleAI.LaneChangingBaseMaxCost >
                    globalConf_.AdvancedVehicleAI.LaneChangingBaseMinCost) {
                    float rand = pathRandomizer_.Int32(101u) / 100f;
                    laneChangingCost *= globalConf_.AdvancedVehicleAI.LaneChangingBaseMinCost +
                                        rand * (globalConf_
                                                .AdvancedVehicleAI.LaneChangingBaseMaxCost -
                                                globalConf_.AdvancedVehicleAI.LaneChangingBaseMinCost);

                    if (isLogEnabled) {
                        DebugLog(
                            unitId,
                            item,
                            "CalculateAdvancedAiCostFactors: Calculated base lane changing costs\n" +
                            "\t" + $"laneChangingCost={laneChangingCost}");
                    }
                }
            }

            if (isLogEnabled) {
                DebugLog(
                    unitId,
                    item,
                    "CalculateAdvancedAiCostFactors: Calculated cost factors\n" +
                    "\t" + $"segmentSelectionCost={segmentSelectionCost}\n" +
                    "\t" + $"laneSelectionCost={laneSelectionCost}\n" +
                    "\t" + $"laneChangingCost={laneChangingCost}");
            }
        }
#endif

        /// <summary>
        /// Checks if node belongs to ferry path network
        /// </summary>
        /// <param name="node">tested node</param>
        /// <returns>true if all valid segments allows ferries, otherwise false</returns>
        private static bool BelongsToFerryNetwork(ref NetNode node) {
            for (var index = 0; index < 8; ++index) {
                ushort netSegmentId = node.GetSegment(index);
                if (netSegmentId == 0) {
                    continue;
                }

                var segmentInfo = netSegmentId.ToSegment().Info;
                if (segmentInfo == null) {
                    continue;
                }

                if ((segmentInfo.m_vehicleTypes & VehicleInfo.VehicleType.Ferry) ==
                    VehicleInfo.VehicleType.None) {
                    return false;
                }
            }

            return true;
        }

        private void PathFindThread() {
            while (true) {
                lock(queueLock_) {

                    while (queueFirst_ == 0 && !terminated_) {
                        Monitor.Wait(queueLock_);
                    }

                    if (terminated_) {
                        break;
                    }

                    calculating_ = queueFirst_;

                    // NON-STOCK CODE START
                    queueFirst_ = CustomPathManager._instance.QueueItems[calculating_].nextPathUnitId;

                    // NON-STOCK CODE END
                    // queueFirst_ = pathUnits_.m_buffer[calculating_].m_nextPathUnit; // stock code commented
                    if (queueFirst_ == 0) {
                        queueLast_ = 0u;
                        m_queuedPathFindCount = 0;
                    } else {
                        m_queuedPathFindCount--;
                    }

                    // NON-STOCK CODE START
                    CustomPathManager._instance.QueueItems[calculating_].nextPathUnitId = 0u;

                    // NON-STOCK CODE END
                    // pathUnits_.m_buffer[calculating_].m_nextPathUnit = 0u; // stock code commented
                    pathUnits_.m_buffer[calculating_].m_pathFindFlags =
                        (byte)((pathUnits_.m_buffer[calculating_].m_pathFindFlags &
                                ~PathUnit.FLAG_CREATED) | PathUnit.FLAG_CALCULATING);

                    // NON-STOCK CODE START
                    queueItem_ = CustomPathManager._instance.QueueItems[calculating_];

                    // NON-STOCK CODE END
                }

                try {
                    m_pathfindProfiler.BeginStep();
                    try {
                        PathFindImplementation(calculating_, ref pathUnits_.m_buffer[calculating_]);
                    }
                    finally {
                        m_pathfindProfiler.EndStep();
                    }
                }
                catch (Exception ex) {
                    UIView.ForwardException(ex);
                    CODebugBase<LogChannel>.Error(
                        LogChannel.Core,
                        "Path find error: " + ex.Message + "\n" + ex.StackTrace);
                    pathUnits_.m_buffer[calculating_].m_pathFindFlags |= PathUnit.FLAG_FAILED;

                    // NON-STOCK CODE START
#if DEBUG
                    ++m_failedPathFinds;
#endif

                    // NON-STOCK CODE END
                }

                lock(queueLock_) {

                    pathUnits_.m_buffer[calculating_].m_pathFindFlags =
                        (byte)(pathUnits_.m_buffer[calculating_].m_pathFindFlags &
                               ~PathUnit.FLAG_CALCULATING);

                    // NON-STOCK CODE START
                    lock(bufferLock_) {
                        CustomPathManager._instance.QueueItems[calculating_].queued = false;
                        CustomPathManager._instance.CustomReleasePath(calculating_);
                    }

                    // NON-STOCK CODE END
                    // Singleton<PathManager>.instance.CustomReleasePath(calculating_); // stock code commented
                    calculating_ = 0u;
                    Monitor.Pulse(queueLock_);
                }
            }
        }
    }
}
