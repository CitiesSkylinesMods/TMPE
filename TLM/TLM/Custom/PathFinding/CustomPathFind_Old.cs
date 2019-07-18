#define DEBUGLOCKSx
#define COUNTSEGMENTSTONEXTJUNCTIONx

namespace TrafficManager.Custom.PathFinding {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reflection;
    using System.Threading;
    using API.Traffic.Enums;
    using ColossalFramework;
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using Manager;
    using Manager.Impl;
    using State;
    using Traffic.Data;
    using Traffic.Enums;
    using UnityEngine;
#if DEBUG
    using State.ConfigData;
#endif

    /// <summary>
    /// This replaces game PathFind class if PF2 is NOT defined
    /// This is only for Benchmark target, others use CustomPathFind2
    /// </summary>
#if !PF2
	[TargetType(typeof(PathFind))]
#endif
    public class CustomPathFind_Old : PathFind {
        private enum LaneChangingCostCalculationMode {
            None,
            [UsedImplicitly]
            ByLaneDistance,
            ByGivenDistance
        }

        private struct BufferItem {
            public PathUnit.Position Position;
            public float ComparisonValue;
            public float MethodDistance;
            public float Duration;
            public uint LaneId;
            public NetInfo.Direction Direction;
            public NetInfo.LaneType LanesUsed;
            public VehicleInfo.VehicleType VehiclesUsed;
            public float TrafficRand;
#if COUNTSEGMENTSTONEXTJUNCTION
			public uint m_numSegmentsToNextJunction;
#endif
        }

        private const float SEGMENT_MIN_AVERAGE_LENGTH = 30f;

        /// <summary>
        /// Copy of a Constants const
        /// </summary>
        private const float BYTE_TO_FLOAT_SCALE = Constants.BYTE_TO_FLOAT_SCALE;

        //Expose the private fields
        private FieldInfo fieldpathUnits_;
        private FieldInfo fieldQueueFirst_;
        private FieldInfo fieldQueueLast_;
        private FieldInfo fieldQueueLock_;
        private FieldInfo fieldCalculating_;
        private FieldInfo fieldTerminated_;
        private FieldInfo fieldPathFindThread_;

        private Array32<PathUnit> PathUnits {
            get => fieldpathUnits_.GetValue(this) as Array32<PathUnit>;
            set => fieldpathUnits_.SetValue(this, value);
        }

        private uint QueueFirst {
            get => (uint)fieldQueueFirst_.GetValue(this);
            set => fieldQueueFirst_.SetValue(this, value);
        }

        private uint QueueLast {
            get => (uint)fieldQueueLast_.GetValue(this);
            set => fieldQueueLast_.SetValue(this, value);
        }

        private uint Calculating {
            get => (uint)fieldCalculating_.GetValue(this);
            set => fieldCalculating_.SetValue(this, value);
        }

        private object QueueLock {
            get => fieldQueueLock_.GetValue(this);
            set => fieldQueueLock_.SetValue(this, value);
        }

        private object bufferLock_;

        private Thread CustomPathFindThread {
            get => (Thread)fieldPathFindThread_.GetValue(this);
            set => fieldPathFindThread_.SetValue(this, value);
        }

        private bool Terminated {
            get => (bool)fieldTerminated_.GetValue(this);
            set => fieldTerminated_.SetValue(this, value);
        }

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
        private ushort startSegmentA_;
        private ushort startSegmentB_;
        private uint endLaneA_;
        private uint endLaneB_;
        private uint vehicleLane_;
        private byte startOffsetA_;
        private byte startOffsetB_;
        private byte vehicleOffset_;
        private NetSegment.Flags carBanMask_;
        private bool isHeavyVehicle_;
        private bool ignoreBlocked_;
        private bool stablePath_;
        private bool randomParking_;
        private bool ignoreCost_;
        private PathUnitQueueItem queueItem_;
        private NetSegment.Flags disableMask_;
        private bool isRoadVehicle_;
        private bool isLaneArrowObeyingEntity_;
        private bool isLaneConnectionObeyingEntity_;
#if DEBUG
        public uint m_failedPathFinds;
        public uint m_succeededPathFinds;
        private bool m_debug;
        private IDictionary<ushort, IList<ushort>> m_debugPositions;
#endif
        public int pfId;
        private Randomizer pathRandomizer_;
        private uint pathFindIndex_;
        private NetInfo.LaneType laneTypes_;
        private VehicleInfo.VehicleType vehicleTypes_;

        private GlobalConfig globalConf_;

        private static readonly CustomSegmentLightsManager CustomTrafficLightsManager =
            CustomSegmentLightsManager.Instance;

        private static readonly JunctionRestrictionsManager JunctionManager =
            JunctionRestrictionsManager.Instance;

        private static readonly VehicleRestrictionsManager VehicleRestrictionsManager =
            VehicleRestrictionsManager.Instance;

        private static readonly SpeedLimitManager SpeedLimitManager = SpeedLimitManager.Instance;

        private static readonly TrafficMeasurementManager
            TrafficMeasurementManager = TrafficMeasurementManager.Instance;

        private static readonly RoutingManager RoutingManager = RoutingManager.Instance;

        public bool IsMasterPathFind;

        protected virtual void Awake() {
            Log.Info("Pathfinder logic: Using CustomPathFind_Old");

            var stockPathFindType = typeof(PathFind);
            const BindingFlags fieldFlags = BindingFlags.NonPublic | BindingFlags.Instance;

            fieldpathUnits_ = stockPathFindType.GetField("m_pathUnits", fieldFlags);
            fieldQueueFirst_ = stockPathFindType.GetField("m_queueFirst", fieldFlags);
            fieldQueueLast_ = stockPathFindType.GetField("m_queueLast", fieldFlags);
            fieldQueueLock_ = stockPathFindType.GetField("m_queueLock", fieldFlags);
            fieldTerminated_ = stockPathFindType.GetField("m_terminated", fieldFlags);
            fieldCalculating_ = stockPathFindType.GetField("m_calculating", fieldFlags);
            fieldPathFindThread_ = stockPathFindType.GetField("m_pathFindThread", fieldFlags);

            buffer_ = new BufferItem[65536]; // 2^16
            bufferLock_ = PathManager.instance.m_bufferLock;
            PathUnits = PathManager.instance.m_pathUnits;
#if DEBUG
            if (QueueLock == null) {
                Log._Debug($"(PF #{pathFindIndex_}, T#{Thread.CurrentThread.ManagedThreadId}, " +
                           $"Id #{pfId}) CustomPathFind.Awake: QueueLock is null. Creating.");
                QueueLock = new object();
            } else {
                Log._Debug($"(PF #{pathFindIndex_}, T#{Thread.CurrentThread.ManagedThreadId}, " +
                           $"Id #{pfId}) CustomPathFind.Awake: QueueLock is NOT null.");
            }
#else
			QueueLock = new object();
#endif
            laneLocation_ = new uint[262144]; // 2^18
            laneTarget_ = new PathUnit.Position[262144]; // 2^18
            bufferMin_ = new int[1024]; // 2^10
            bufferMax_ = new int[1024]; // 2^10

            m_pathfindProfiler = new ThreadProfiler();
            CustomPathFindThread = new Thread(PathFindThread) {
                Name = "Pathfind", Priority = SimulationManager.SIMULATION_PRIORITY
            };
            CustomPathFindThread.Start();
            if (!CustomPathFindThread.IsAlive) {
                // CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find thread failed to start!");
                Log.Error($"(PF #{pathFindIndex_}, T#{Thread.CurrentThread.ManagedThreadId}, " +
                          $"Id #{pfId}) Path find thread failed to start!");
            }

        }

        protected virtual void OnDestroy() {
#if DEBUGLOCKS
			uint lockIter = 0;
#endif
            try {
                Monitor.Enter(QueueLock);
                Terminated = true;
                Monitor.PulseAll(QueueLock);
            } catch (Exception e) {
                Log.Error("CustomPathFind.OnDestroy Error: " + e);
            } finally {
                Monitor.Exit(QueueLock);
            }
        }

#if !PF2
		[RedirectMethod]
#endif
        [UsedImplicitly]
        public new bool CalculatePath(uint unit, bool skipQueue) {
            return ExtCalculatePath(unit, skipQueue);
        }

        private bool ExtCalculatePath(uint unit, bool skipQueue) {
            if (!CustomPathManager._instance.AddPathReference(unit)) {
                return false;
            }

            try {
                Monitor.Enter(QueueLock);

                if (skipQueue) {

                    if (QueueLast == 0u) {
                        QueueLast = unit;
                    } else {
                        CustomPathManager._instance.QueueItems[unit].nextPathUnitId = QueueFirst;
                        // this.PathUnits.m_buffer[unit].m_nextPathUnit = this.QueueFirst;
                    }

                    QueueFirst = unit;
                } else {
                    if (QueueLast == 0u) {
                        QueueFirst = unit;
                    } else {
                        CustomPathManager._instance.QueueItems[QueueLast].nextPathUnitId = unit;
                        // this.PathUnits.m_buffer[this.QueueLast].m_nextPathUnit = unit;
                    }

                    QueueLast = unit;
                }

                PathUnits.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_CREATED;
                ++m_queuedPathFindCount;

                Monitor.Pulse(QueueLock);
            } catch (Exception e) {
                Log.Error($"(PF #{pathFindIndex_}, T#{Thread.CurrentThread.ManagedThreadId}, " +
                          $"Id #{pfId}) CustomPathFind.CalculatePath({unit}, {skipQueue}): Error: {e}");
            } finally {
                Monitor.Exit(QueueLock);
            }

            return true;
        }

        // PathFind
        private void PathFindImplementation(uint unit, ref PathUnit data) {
            globalConf_ = GlobalConfig.Instance; // NON-STOCK CODE

            var netManager = Singleton<NetManager>.instance;
            laneTypes_ = (NetInfo.LaneType)PathUnits.m_buffer[unit].m_laneTypes;
            vehicleTypes_ = (VehicleInfo.VehicleType)PathUnits.m_buffer[unit].m_vehicleTypes;
            maxLength_ = PathUnits.m_buffer[unit].m_length;
            pathFindIndex_ = pathFindIndex_ + 1u & 32767u;
            pathRandomizer_ = new Randomizer(unit);

            carBanMask_ = NetSegment.Flags.CarBan;
            isHeavyVehicle_ = (PathUnits.m_buffer[unit].m_simulationFlags & 16) != 0;
            if (isHeavyVehicle_) {
                carBanMask_ |= NetSegment.Flags.HeavyBan;
            }

            if ((PathUnits.m_buffer[unit].m_simulationFlags & 4) != 0) {
                carBanMask_ |= NetSegment.Flags.WaitingPath;
            }

            ignoreBlocked_ = (PathUnits.m_buffer[unit].m_simulationFlags & 32) != 0;
            stablePath_ = (PathUnits.m_buffer[unit].m_simulationFlags & 64) != 0;
            randomParking_ = (PathUnits.m_buffer[unit].m_simulationFlags & 128) != 0;
            ignoreCost_ = stablePath_ || (PathUnits.m_buffer[unit].m_simulationFlags & 8) != 0;
            disableMask_ = NetSegment.Flags.Collapsed | NetSegment.Flags.PathFailed;
            if ((PathUnits.m_buffer[unit].m_simulationFlags & 2) == 0) {
                disableMask_ |= NetSegment.Flags.Flooded;
            }

            // this._speedRand = 0;
            isRoadVehicle_ = (queueItem_.vehicleType & ExtVehicleType.RoadVehicle) != ExtVehicleType.None;
            isLaneArrowObeyingEntity_ =
                (vehicleTypes_ & LaneArrowManager.VEHICLE_TYPES) != VehicleInfo.VehicleType.None
                && (queueItem_.vehicleType & LaneArrowManager.EXT_VEHICLE_TYPES) != ExtVehicleType.None;
            isLaneConnectionObeyingEntity_ =
                (vehicleTypes_ & LaneConnectionManager.VEHICLE_TYPES) != VehicleInfo.VehicleType.None
                && (queueItem_.vehicleType & LaneConnectionManager.EXT_VEHICLE_TYPES) != ExtVehicleType.None;
#if DEBUGNEWPF && DEBUG
            var logLogic =
                m_debug =
                    DebugSwitch.PathFindingLog.Get() &&
                    ((GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.None
                      && queueItem_.vehicleType == ExtVehicleType.None)
                     || (queueItem_.vehicleType & GlobalConfig.Instance.Debug.ApiExtVehicleType) !=
                     ExtVehicleType.None)
                    && (DebugSettings.StartSegmentId == 0
                        || data.m_position00.m_segment == DebugSettings.StartSegmentId
                        || data.m_position02.m_segment == DebugSettings.StartSegmentId)
                    && (DebugSettings.EndSegmentId == 0
                        || data.m_position01.m_segment == DebugSettings.EndSegmentId
                        || data.m_position03.m_segment == DebugSettings.EndSegmentId)
                    && (DebugSettings.VehicleId == 0
                        || queueItem_.vehicleId == DebugSettings.VehicleId);
            m_debugPositions = new Dictionary<ushort, IList<ushort>>();
#else
            var logLogic = false;
#endif
            Log._DebugIf(
                logLogic,
                () => "CustomPathFind.PathFindImplementation: START calculating " +
                           $"path unit {unit}, type {queueItem_.vehicleType}");

            if ((byte)(laneTypes_ & NetInfo.LaneType.Vehicle) != 0) {
                laneTypes_ |= NetInfo.LaneType.TransportVehicle;
            }

            var posCount = PathUnits.m_buffer[unit].m_positionCount & 15;
            var vehiclePosIndicator = PathUnits.m_buffer[unit].m_positionCount >> 4;
            BufferItem bufferItemStartA;

            if (data.m_position00.m_segment != 0 && posCount >= 1) {
                startLaneA_ = PathManager.GetLaneID(data.m_position00);
                startSegmentA_ = data.m_position00.m_segment; // NON-STOCK CODE
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

#if COUNTSEGMENTSTONEXTJUNCTION
				bufferItemStartA.m_numSegmentsToNextJunction = 0;
#endif
            } else {
                startLaneA_ = 0u;
                startSegmentA_ = 0; // NON-STOCK CODE
                startOffsetA_ = 0;
                bufferItemStartA = default;
            }

            BufferItem bufferItemStartB;
            if (data.m_position02.m_segment != 0 && posCount >= 3) {
                startLaneB_ = PathManager.GetLaneID(data.m_position02);
                startSegmentB_ = data.m_position02.m_segment;
                // NON-STOCK CODE
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

#if COUNTSEGMENTSTONEXTJUNCTION
				bufferItemStartB.m_numSegmentsToNextJunction = 0;
#endif
            } else {
                startLaneB_ = 0u;
                startSegmentB_ = 0; // NON-STOCK CODE
                startOffsetB_ = 0;
                bufferItemStartB = default;
            }

            BufferItem bufferItemEndA;
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
                bufferItemEndA.TrafficRand = 0; // NON-STOCK CODE

#if COUNTSEGMENTSTONEXTJUNCTION
				bufferItemEndA.m_numSegmentsToNextJunction = 0;
#endif
            } else {
                endLaneA_ = 0u;
                bufferItemEndA = default;
            }

            BufferItem bufferItemEndB;
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
                bufferItemEndB.TrafficRand = 0; // NON-STOCK CODE
#if COUNTSEGMENTSTONEXTJUNCTION
				bufferItemEndB.m_numSegmentsToNextJunction = 0;
#endif
            } else {
                endLaneB_ = 0u;
                bufferItemEndB = default;
            }

            if (data.m_position11.m_segment != 0 && vehiclePosIndicator >= 1) {
                vehicleLane_ = PathManager.GetLaneID(data.m_position11);
                vehicleOffset_ = data.m_position11.m_offset;
            } else {
                vehicleLane_ = 0u;
                vehicleOffset_ = 0;
            }

            if (logLogic) {
                Log._Debug(
                    "CustomPathFind.PathFindImplementation: Preparing calculating " +
                    $"path unit {unit}, type {queueItem_.vehicleType}:\n" +
                    $"\tbufferItemStartA: segment={bufferItemStartA.Position.m_segment} " +
                    $"lane={bufferItemStartA.Position.m_lane} " +
                    $"off={bufferItemStartA.Position.m_offset} " +
                    $"laneId={bufferItemStartA.LaneId}\n" +
                    $"\tbufferItemStartB: segment={bufferItemStartB.Position.m_segment} " +
                    $"lane={bufferItemStartB.Position.m_lane} " +
                    $"off={bufferItemStartB.Position.m_offset} laneId={bufferItemStartB.LaneId}\n" +
                    $"\tbufferItemEndA: segment={bufferItemEndA.Position.m_segment} " +
                    $"lane={bufferItemEndA.Position.m_lane} off={bufferItemEndA.Position.m_offset} " +
                    $"laneId={bufferItemEndA.LaneId}\n\tbufferItemEndB: " +
                    $"segment={bufferItemEndB.Position.m_segment} " +
                    $"lane={bufferItemEndB.Position.m_lane} off={bufferItemEndB.Position.m_offset} " +
                    $"laneId={bufferItemEndB.LaneId}\n\tvehicleItem: " +
                    $"segment={data.m_position11.m_segment} lane={data.m_position11.m_lane} " +
                    $"off={data.m_position11.m_offset} laneId={vehicleLane_} " +
                    $"vehiclePosIndicator={vehiclePosIndicator}\n");
            }

            var finalBufferItem = default(BufferItem);
            byte startOffset = 0;
            bufferMinPos_ = 0;
            bufferMaxPos_ = -1;

            if (pathFindIndex_ == 0u) {
                for (var i = 0; i < 262144; ++i) {
                    laneLocation_[i] = uint.MaxValue;
                }
            }

            for (var j = 0; j < 1024; ++j) {
                bufferMin_[j] = 0;
                bufferMax_[j] = -1;
            }

            if (bufferItemEndA.Position.m_segment != 0) {
                ++bufferMax_[0];
                buffer_[++bufferMaxPos_] = bufferItemEndA;
            }

            if (bufferItemEndB.Position.m_segment != 0) {
                ++bufferMax_[0];
                buffer_[++bufferMaxPos_] = bufferItemEndB;
            }

            var canFindPath = false;

            while (bufferMinPos_ <= bufferMaxPos_) {
                var bufMin = bufferMin_[bufferMinPos_];
                var bufMax = bufferMax_[bufferMinPos_];
                if (bufMin > bufMax) {
                    ++bufferMinPos_;
                } else {
                    bufferMin_[bufferMinPos_] = bufMin + 1;
                    var candidateItem = buffer_[(bufferMinPos_ << 6) + bufMin];
                    if (candidateItem.Position.m_segment == bufferItemStartA.Position.m_segment
                        && candidateItem.Position.m_lane == bufferItemStartA.Position.m_lane) {
                        // we reached startA
                        if ((byte)(candidateItem.Direction & NetInfo.Direction.Forward) != 0
                            && candidateItem.Position.m_offset >= startOffsetA_)
                        {
                            finalBufferItem = candidateItem;
                            startOffset = startOffsetA_;
                            canFindPath = true;
                            break;
                        }

                        if ((byte)(candidateItem.Direction & NetInfo.Direction.Backward) != 0
                            && candidateItem.Position.m_offset <= startOffsetA_)
                        {
                            finalBufferItem = candidateItem;
                            startOffset = startOffsetA_;
                            canFindPath = true;
                            break;
                        }
                    }

                    if (candidateItem.Position.m_segment == bufferItemStartB.Position.m_segment
                        && candidateItem.Position.m_lane == bufferItemStartB.Position.m_lane) {
                        // we reached startB
                        if ((byte)(candidateItem.Direction & NetInfo.Direction.Forward) != 0
                            && candidateItem.Position.m_offset >= startOffsetB_)
                        {
                            finalBufferItem = candidateItem;
                            startOffset = startOffsetB_;
                            canFindPath = true;
                            break;
                        }

                        if ((byte)(candidateItem.Direction & NetInfo.Direction.Backward) != 0
                            && candidateItem.Position.m_offset <= startOffsetB_)
                        {
                            finalBufferItem = candidateItem;
                            startOffset = startOffsetB_;
                            canFindPath = true;
                            break;
                        }
                    }

                    // explore the path
                    var segBuffer = netManager.m_segments.m_buffer;
                    if ((byte)(candidateItem.Direction & NetInfo.Direction.Forward) != 0) {
                        var startNode = segBuffer[candidateItem.Position.m_segment]
                                        .m_startNode;
                        var laneRoutingIndex = RoutingManager.GetLaneEndRoutingIndex(
                            candidateItem.LaneId,
                            true);

                        ProcessItemMain(
                            unit,
                            candidateItem,
                            ref segBuffer[candidateItem.Position.m_segment],
                            RoutingManager.SegmentRoutings[candidateItem.Position.m_segment],
                            RoutingManager.LaneEndBackwardRoutings[laneRoutingIndex],
                            startNode,
                            true,
                            ref netManager.m_nodes.m_buffer[startNode],
                            0,
                            false);
                    }

                    if ((byte)(candidateItem.Direction & NetInfo.Direction.Backward) != 0) {
                        var endNode = segBuffer[candidateItem.Position.m_segment].m_endNode;
                        var laneRoutingIndex = RoutingManager.GetLaneEndRoutingIndex(
                            candidateItem.LaneId,
                            false);

                        ProcessItemMain(
                            unit,
                            candidateItem,
                            ref segBuffer[candidateItem.Position.m_segment],
                            RoutingManager.SegmentRoutings[candidateItem.Position.m_segment],
                            RoutingManager.LaneEndBackwardRoutings[laneRoutingIndex],
                            endNode,
                            false,
                            ref netManager.m_nodes.m_buffer[endNode],
                            255,
                            false);
                    }

                    // handle special nodes (e.g. bus stops)
                    var num6 = 0;
                    var laneBuffer = netManager.m_lanes.m_buffer;
                    var specialNodeId = laneBuffer[candidateItem.LaneId].m_nodes;
                    if (specialNodeId == 0) {
                        continue;
                    }

                    var startNode2 = segBuffer[candidateItem.Position.m_segment].m_startNode;
                    var endNode2 = segBuffer[candidateItem.Position.m_segment].m_endNode;
                    var nodesDisabled =
                        ((netManager.m_nodes.m_buffer[startNode2].m_flags |
                          netManager.m_nodes.m_buffer[endNode2].m_flags) &
                         NetNode.Flags.Disabled) != NetNode.Flags.None;

                    while (specialNodeId != 0) {
                        var direction = NetInfo.Direction.None;
                        var laneOffset = netManager.m_nodes.m_buffer[specialNodeId].m_laneOffset;
                        if (laneOffset <= candidateItem.Position.m_offset) {
                            direction |= NetInfo.Direction.Forward;
                        }

                        if (laneOffset >= candidateItem.Position.m_offset) {
                            direction |= NetInfo.Direction.Backward;
                        }

                        if ((byte)(candidateItem.Direction & direction) != 0
                            && (!nodesDisabled
                                || (netManager.m_nodes.m_buffer[specialNodeId].m_flags
                                    & NetNode.Flags.Disabled) != NetNode.Flags.None)) {
#if DEBUGNEWPF && DEBUG
                            if (logLogic && (DebugSettings.NodeId <= 0
                                             || specialNodeId == DebugSettings.NodeId))
                            {
                                Log._Debug("CustomPathFind.PathFindImplementation: Handling " +
                                           $"special node for path unit {unit}, type {queueItem_.vehicleType}:\n" +
                                           "\tcandidateItem.m_position.m_segment" +
                                           $"={candidateItem.Position.m_segment}\n" +
                                           "\tcandidateItem.m_position.m_lane" +
                                           $"={candidateItem.Position.m_lane}\n" +
                                           $"\tcandidateItem.m_laneID={candidateItem.LaneId}\n" +
                                           $"\tspecialNodeId={specialNodeId}\n" +
                                           $"\tstartNode2={startNode2}\n" +
                                           $"\tendNode2={endNode2}\n");
                            }
#endif
                            ProcessItemMain(
                                unit,
                                candidateItem,
                                ref segBuffer[candidateItem.Position.m_segment],
                                RoutingManager.SegmentRoutings[candidateItem.Position.m_segment],
                                RoutingManager.LaneEndBackwardRoutings[0],
                                specialNodeId,
                                false,
                                ref netManager.m_nodes.m_buffer[specialNodeId],
                                laneOffset,
                                true);
                        }

                        specialNodeId = netManager.m_nodes.m_buffer[specialNodeId].m_nextLaneNode;

                        if (++num6 >= NetManager.MAX_NODE_COUNT) {
                            Log.Warning("Special loop: Too many iterations");
                            break;
                        }
                    }
                }
            }

            if (!canFindPath) {
                // we could not find a path
                PathUnits.m_buffer[(int)unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;
#if DEBUG
                ++m_failedPathFinds;

#if DEBUGNEWPF
                if (!logLogic) {
                    return;
                }

                Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} " +
                           $"PF {pathFindIndex_}: Could not find path for unit {unit} " +
                           "-- path-finding failed during process");

                var reachableBuf = string.Empty;
                var unreachableBuf = string.Empty;

                foreach (var e in m_debugPositions) {
                    var buf = $"{e.Key} -> {e.Value.CollectionToString()}\n";
                    if (e.Value.Count <= 0) {
                        unreachableBuf += buf;
                    } else {
                        reachableBuf += buf;
                    }
                }

                Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} " +
                           $"PF {pathFindIndex_}: Reachability graph for unit {unit}:\n" +
                           "== REACHABLE ==\n" + reachableBuf + "\n" +
                           "== UNREACHABLE ==\n" + unreachableBuf);
#endif
#endif

                // CustomPathManager._instance.ResetQueueItem(unit);
                return;
            }

            // we could calculate a valid path
            var duration = laneTypes_ != NetInfo.LaneType.Pedestrian
                               ? finalBufferItem.Duration
                               : finalBufferItem.MethodDistance;
            PathUnits.m_buffer[unit].m_length = duration;
            PathUnits.m_buffer[unit].m_laneTypes = (byte)finalBufferItem.LanesUsed; // NON-STOCK CODE
            PathUnits.m_buffer[unit].m_vehicleTypes = (ushort)finalBufferItem.VehiclesUsed; // NON-STOCK CODE

            var currentPathUnitId = unit;
            var currentItemPositionCount = 0;
            var sumOfPositionCounts = 0;
            var currentPosition = finalBufferItem.Position;

            if ((currentPosition.m_segment != bufferItemEndA.Position.m_segment ||
                 currentPosition.m_lane != bufferItemEndA.Position.m_lane ||
                 currentPosition.m_offset != bufferItemEndA.Position.m_offset) &&
                (currentPosition.m_segment != bufferItemEndB.Position.m_segment ||
                 currentPosition.m_lane != bufferItemEndB.Position.m_lane ||
                 currentPosition.m_offset != bufferItemEndB.Position.m_offset)) {
                // the found starting position differs from the desired end position
                if (startOffset != currentPosition.m_offset) {
                    // the offsets differ: copy the found starting position and modify the
                    // offset to fit the desired offset
                    var position2 = currentPosition;
                    position2.m_offset = startOffset;
                    PathUnits.m_buffer[currentPathUnitId].SetPosition(
                        currentItemPositionCount++,
                        position2);

                    // now we have: [desired starting position]
                }

                // add the found starting position to the path unit
                PathUnits.m_buffer[currentPathUnitId].SetPosition(
                    currentItemPositionCount++,
                    currentPosition);
                currentPosition =
                    laneTarget_[finalBufferItem.LaneId]; // go to the next path position

                // now we have either [desired starting position, found starting position] or
                // [found starting position], depending on if the found starting position
                // matched the desired
            }

            // beginning with the starting position, going to the target position:
            // assemble the path units
            for (var k = 0; k < 262144; ++k) {
                // pfCurrentState = 6;
                // add the next path position to the current unit
                PathUnits.m_buffer[currentPathUnitId].SetPosition(
                    currentItemPositionCount++,
                    currentPosition);

                if ((currentPosition.m_segment == bufferItemEndA.Position.m_segment &&
                     currentPosition.m_lane == bufferItemEndA.Position.m_lane &&
                     currentPosition.m_offset == bufferItemEndA.Position.m_offset)
                    || (currentPosition.m_segment == bufferItemEndB.Position.m_segment &&
                        currentPosition.m_lane == bufferItemEndB.Position.m_lane &&
                        currentPosition.m_offset == bufferItemEndB.Position.m_offset)) {
                    // we have reached the end position
                    PathUnits.m_buffer[currentPathUnitId].m_positionCount = (byte)currentItemPositionCount;
                    sumOfPositionCounts += currentItemPositionCount; // add position count of last unit to sum

                    if (sumOfPositionCounts != 0) {
                        // for each path unit from start to target: calculate length (distance)
                        // to target (we do not need to calculate the length for the starting
                        // unit since this is done before; it's the total path length)
                        currentPathUnitId = PathUnits.m_buffer[unit].m_nextPathUnit;
                        currentItemPositionCount = PathUnits.m_buffer[unit].m_positionCount;
                        var totalIter = 0;

                        while (currentPathUnitId != 0u) {
                            PathUnits.m_buffer[currentPathUnitId].m_length =
                                duration * (sumOfPositionCounts - currentItemPositionCount) /
                                sumOfPositionCounts;
                            currentItemPositionCount += PathUnits.m_buffer[currentPathUnitId].m_positionCount;
                            currentPathUnitId = PathUnits.m_buffer[currentPathUnitId].m_nextPathUnit;

                            if (++totalIter < 262144) {
                                continue;
                            }

                            Log._DebugOnlyError("THREAD #{Thread.CurrentThread.ManagedThreadId} PF " +
                                      "{this._pathFindIndex}: PathFindImplementation: Invalid list detected.");
                            CODebugBase<LogChannel>.Error(
                                LogChannel.Core,
                                $"Invalid list detected!\n{Environment.StackTrace}");
                            break;
                        }
                    }

                    PathUnits.m_buffer[(int)unit].m_pathFindFlags |= PathUnit.FLAG_READY; // Path found
#if DEBUG
                    ++m_succeededPathFinds;

#if DEBUGNEWPF
                    Log._DebugIf(
                        logLogic,
                        () => $"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {pathFindIndex_}: " +
                        $"Path-find succeeded for unit {unit}");
#endif
#endif

                    // CustomPathManager._instance.ResetQueueItem(unit);
                    return;
                }

                // We have not reached the target position yet
                if (currentItemPositionCount == 12) {
                    // the current path unit is full, we need a new one
                    uint createdPathUnitId;
                    try {
                        Monitor.Enter(bufferLock_);
                        if (!PathUnits.CreateItem(out createdPathUnitId, ref pathRandomizer_)) {
                            // we failed to create a new path unit, thus the path-finding also failed
                            PathUnits.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;
#if DEBUG
                            ++m_failedPathFinds;

#if DEBUGNEWPF
                            Log._DebugIf(
                                logLogic,
                                () => $"THREAD #{Thread.CurrentThread.ManagedThreadId} PF " +
                                $"{pathFindIndex_}: Could not find path for unit {unit} " +
                                "-- Could not create path unit");
#endif
#endif

                            // CustomPathManager._instance.ResetQueueItem(unit);
                            return;
                        }

                        PathUnits.m_buffer[createdPathUnitId] = PathUnits.m_buffer[(int)currentPathUnitId];
                        PathUnits.m_buffer[createdPathUnitId].m_referenceCount = 1;
                        PathUnits.m_buffer[createdPathUnitId].m_pathFindFlags = PathUnit.FLAG_READY;
                        PathUnits.m_buffer[currentPathUnitId].m_nextPathUnit = createdPathUnitId;
                        PathUnits.m_buffer[currentPathUnitId].m_positionCount = (byte)currentItemPositionCount;
                        PathUnits.m_buffer[currentPathUnitId].m_laneTypes = (byte)finalBufferItem.LanesUsed;
                        // NON-STOCK CODE (this is not accurate!)

                        PathUnits.m_buffer[currentPathUnitId].m_vehicleTypes = (ushort)finalBufferItem.VehiclesUsed;

                        // NON-STOCK CODE (this is not accurate!)
                        sumOfPositionCounts += currentItemPositionCount;
                        Singleton<PathManager>.instance.m_pathUnitCount = (int)(PathUnits.ItemCount() - 1u);
                    } catch (Exception e) {
                        Log.Error($"(PF #{pathFindIndex_}, T#{Thread.CurrentThread.ManagedThreadId}, " +
                                  $"Id #{pfId}) CustomPathFind.PathFindImplementation Error: {e}");
                        break;
                    } finally {
                        Monitor.Exit(bufferLock_);
                    }

                    currentPathUnitId = createdPathUnitId;
                    currentItemPositionCount = 0;
                }

                var laneId = PathManager.GetLaneID(currentPosition);
#if PFTRAFFICSTATS
                // NON-STOCK CODE START
#if MEASUREDENSITY
                if (!Options.isStockLaneChangerUsed()) {
                    NetInfo.Lane laneInfo = Singleton<NetManager>.instance.m_segments.m_buffer[currentPosition.m_segment].Info.m_lanes[currentPosition.m_lane];
                    if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None)
                        trafficMeasurementManager.AddTraffic(currentPosition.m_segment, currentPosition.m_lane, (ushort)(this._isHeavyVehicle || _extVehicleType == ExtVehicleType.Bus ? 75 : 25), null);
                }
#endif
                if (!Options.isStockLaneChangerUsed()) {
                    NetInfo.Lane laneInfo = Singleton<NetManager>.instance.m_segments.m_buffer[currentPosition.m_segment].Info.m_lanes[currentPosition.m_lane];
                    if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
                        trafficMeasurementManager.AddPathFindTraffic(currentPosition.m_segment, currentPosition.m_lane);
                    }
                }
                // NON-STOCK CODE END
#endif
                currentPosition = laneTarget_[laneId];
            }

            PathUnits.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;
#if DEBUG
            ++m_failedPathFinds;

#if DEBUGNEWPF
            Log._DebugIf(
                logLogic,
                () => $"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {pathFindIndex_}: " +
                $"Could not find path for unit {unit} -- internal error: for loop break");
#endif
#endif

            // CustomPathManager._instance.ResetQueueItem(unit);
#if DEBUG
            // Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}:
            // Cannot find path (pfCurrentState={pfCurrentState}) for unit {unit}");
#endif
        }

        [Conditional("DEBUGNEWPF")]
        private void MemoryLog_Flush_If(bool cond,
                                        List<string> logBuf,
                                        uint unitId,
                                        Func<string> s) {
#if DEBUGNEWPF
            if (cond) {
                logBuf.Add(s());
                FlushMainLog(logBuf, unitId);
            }
#endif
        }

        [Conditional("DEBUGNEWPF")]
        private void MemoryLog_FlushCost_If(bool cond,
                                            List<string> logBuf,
                                            Func<string> s) {
#if DEBUGNEWPF
            if (cond) {
                logBuf.Add(s());
                FlushCostLog(logBuf);
            }
#endif
        }

        [Conditional("DEBUGNEWPF")]
        private void MemoryLog_NoFlush_If(bool cond,
                                         List<string> logBuf,
                                         Func<string> s) {
#if DEBUGNEWPF
            if (cond) {
                logBuf.Add(s());
            }
#endif
        }

        // be aware:
        //   (1) path-finding works from target to start. the "next" segment is
        // always the previous and the "previous" segment is always the next
        // segment on the path!
        //   (2) when I use the term "lane index from outer" this means outer
        // right lane for right-hand traffic systems and outer-left lane for
        // left-hand traffic systems.

        // 1
        private void ProcessItemMain(uint unitId,
                                     BufferItem item,
                                     ref NetSegment prevSegment,
                                     SegmentRoutingData prevSegmentRouting,
                                     LaneEndRoutingData prevLaneEndRouting,
                                     ushort nextNodeId,
                                     bool nextIsStartNode,
                                     ref NetNode nextNode,
                                     byte connectOffset,
                                     bool isMiddle) {
#if DEBUGNEWPF && DEBUG
            var logLogic = m_debug && (DebugSettings.NodeId <= 0
                                    || nextNodeId == DebugSettings.NodeId);
            var debugPed = logLogic && DebugSwitch.PedestrianPathfinding.Get();
            if (logLogic) {
                if (!m_debugPositions.ContainsKey(item.Position.m_segment)) {
                    m_debugPositions[item.Position.m_segment] = new List<ushort>();
                }
            }
#else
            bool logLogic = false;
            bool debugPed = false;
            var oldLaneSelectionCost = 0f;
#endif

            List<string> logBuf = null;
#if DEBUGNEWPF && DEBUG
            if (logLogic) {
                logBuf = new List<string>();
            }
#endif

            var netManager = Singleton<NetManager>.instance;
            var prevIsPedestrianLane = false;

            // bool prevIsBusLane = false; // non-stock
            var prevIsBicycleLane = false;
            var prevIsCenterPlatform = false;
            var prevIsElevated = false;
            var prevIsCarLane = false;
            var prevRelSimilarLaneIndex = 0; // inner/outer similar index

            // similar index, starting with 0 at leftmost lane in right hand traffic
            // int prevInnerSimilarLaneIndex = 0;

            // similar index, starting with 0 at rightmost lane in right hand traffic
            var prevOuterSimilarLaneIndex = 0;

            var prevSegmentInfo = prevSegment.Info;
            NetInfo.Lane prevLaneInfo = null;

            if (item.Position.m_lane < prevSegmentInfo.m_lanes.Length) {
                prevLaneInfo = prevSegmentInfo.m_lanes[item.Position.m_lane];
                prevIsPedestrianLane = prevLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian;
                prevIsBicycleLane = prevLaneInfo.m_laneType == NetInfo.LaneType.Vehicle &&
                                    (prevLaneInfo.m_vehicleType & vehicleTypes_) ==
                                    VehicleInfo.VehicleType.Bicycle;
                prevIsCarLane =
                    (prevLaneInfo.m_laneType &
                     (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                    NetInfo.LaneType.None &&
                    (prevLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) !=
                    VehicleInfo.VehicleType.None;

                // prevIsBusLane = (prevLane.m_laneType == NetInfo.LaneType.TransportVehicle
                // && (prevLane.m_vehicleType & this._vehicleTypes & VehicleInfo.VehicleType.Car)
                // != VehicleInfo.VehicleType.None);
                prevIsCenterPlatform = prevLaneInfo.m_centerPlatform;
                prevIsElevated = prevLaneInfo.m_elevated;

                // prevInnerSimilarLaneIndex = RoutingManager.Instance.CalcInnerSimilarLaneIndex(prevLaneInfo);
                prevOuterSimilarLaneIndex = RoutingManager.Instance.CalcOuterSimilarLaneIndex(prevLaneInfo);
                if ((byte)(prevLaneInfo.m_finalDirection & NetInfo.Direction.Forward) != 0) {
                    prevRelSimilarLaneIndex = prevLaneInfo.m_similarLaneIndex;
                } else {
                    prevRelSimilarLaneIndex = prevLaneInfo.m_similarLaneCount -
                                              prevLaneInfo.m_similarLaneIndex - 1;
                }
            }

            var firstPrevSimilarLaneIndexFromInner = prevRelSimilarLaneIndex;
            var prevSegmentId = item.Position.m_segment;
            if (isMiddle) {
                for (var i = 0; i < 8; ++i) {
                    var nextSegmentId = nextNode.GetSegment(i);
                    if (nextSegmentId <= 0)
                        continue;

#if DEBUGNEWPF
                    if (logLogic) {
                        FlushMainLog(logBuf, unitId);
                    }
#endif

                    ProcessItemCosts(
                        logLogic,
                        item,
                        nextNodeId,
                        nextSegmentId,
                        ref prevSegment, /*prevSegmentRouting,*/
                        ref netManager.m_segments.m_buffer[nextSegmentId],
                        ref prevRelSimilarLaneIndex,
                        connectOffset,
                        !prevIsPedestrianLane,
                        prevIsPedestrianLane,
                        isMiddle);
                }
            } else if (prevIsPedestrianLane) {
                var allowPedSwitch = (laneTypes_ & NetInfo.LaneType.Pedestrian) != 0;
                if (!prevIsElevated) {
                    // explore pedestrian lanes
                    int prevLaneIndex = item.Position.m_lane;
                    if (nextNode.Info.m_class.m_service != ItemClass.Service.Beautification) {
                        if (allowPedSwitch) { // NON-STOCK CODE
                            var canCrossStreet =
                                (nextNode.m_flags &
                                 (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)
                                ) != NetNode.Flags.None;
                            var isOnCenterPlatform =
                                prevIsCenterPlatform &&
                                (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Junction)) ==
                                NetNode.Flags.None;
                            var nextLeftSegment = prevSegmentId;
                            var nextRightSegment = prevSegmentId;

                            prevSegment.GetLeftAndRightLanes(
                                nextNodeId,
                                NetInfo.LaneType.Pedestrian,
                                VehicleInfo.VehicleType.None,
                                prevLaneIndex,
                                isOnCenterPlatform,
                                out var leftLaneIndex,
                                out var rightLaneIndex,
                                out var leftLaneId,
                                out var rightLaneId);

                            if (leftLaneId == 0u || rightLaneId == 0u) {
                                prevSegment.GetLeftAndRightSegments(
                                    nextNodeId,
                                    out var leftSegment,
                                    out var rightSegment);

                                var numIter = 0;

                                while (leftSegment != 0 && leftSegment != prevSegmentId && leftLaneId == 0u) {
                                    netManager
                                        .m_segments.m_buffer[leftSegment].GetLeftAndRightLanes(
                                            nextNodeId,
                                            NetInfo.LaneType.Pedestrian,
                                            VehicleInfo.VehicleType.None,
                                            -1,
                                            isOnCenterPlatform,
                                            out _,
                                            out var someRightLaneIndex,
                                            out _,
                                            out var someRightLaneId);

                                    if (someRightLaneId != 0u) {
                                        nextLeftSegment = leftSegment;
                                        leftLaneIndex = someRightLaneIndex;
                                        leftLaneId = someRightLaneId;
                                    } else {
                                        leftSegment = netManager
                                                      .m_segments.m_buffer[leftSegment]
                                                      .GetLeftSegment(nextNodeId);
                                    }

                                    if (++numIter == 8) {
                                        break;
                                    }
                                }

                                numIter = 0;
                                while (rightSegment != 0 && rightSegment != prevSegmentId &&
                                       rightLaneId == 0u) {
                                    netManager
                                        .m_segments.m_buffer[rightSegment].GetLeftAndRightLanes(
                                            nextNodeId,
                                            NetInfo.LaneType.Pedestrian,
                                            VehicleInfo.VehicleType.None,
                                            -1,
                                            isOnCenterPlatform,
                                            out var someLeftLaneIndex,
                                            out _,
                                            out var someLeftLaneId,
                                            out _);

                                    if (someLeftLaneId != 0u) {
                                        nextRightSegment = rightSegment;
                                        rightLaneIndex = someLeftLaneIndex;
                                        rightLaneId = someLeftLaneId;
                                    } else {
                                        rightSegment = netManager
                                                       .m_segments.m_buffer[rightSegment]
                                                       .GetRightSegment(nextNodeId);
                                    }

                                    if (++numIter == 8) {
                                        break;
                                    }
                                }
                            }

                            if (leftLaneId != 0u && (nextLeftSegment != prevSegmentId
                                                     || canCrossStreet
                                                     || isOnCenterPlatform)) {
                                MemoryLog_Flush_If(
                                    debugPed,
                                    logBuf,
                                    unitId,
                                    () => $"*PED* item: seg. {item.Position.m_segment}, " +
                                    $"lane {item.Position.m_lane}, node {nextNodeId} " +
                                    $"({nextIsStartNode}): Exploring left segment\n" +
                                    $"\t_extPathType={queueItem_.pathType}\n" +
                                    $"\t_vehicleTypes={vehicleTypes_}, _laneTypes={laneTypes_}\n" +
                                    $"\t_extVehicleType={queueItem_.vehicleType}\n" +
                                    $"\t_isRoadVehicle={isRoadVehicle_}\n" +
                                    $"\t_isHeavyVehicle={isHeavyVehicle_}\n" +
                                    $"\t_stablePath={stablePath_}\n" +
                                    "\t_isLaneConnectionObeyingEntity" +
                                    $"={isLaneConnectionObeyingEntity_}\n" +
                                    $"\t_isLaneArrowObeyingEntity={isLaneArrowObeyingEntity_}\n\n" +
                                    $"\tnextIsStartNode={nextIsStartNode}\n" +
                                    $"\tnextLeftSegment={nextLeftSegment}\n" +
                                    $"\tleftLaneId={leftLaneId}\n" +
                                    $"\tmayCrossStreet={canCrossStreet}\n" +
                                    $"\tisOnCenterPlatform={isOnCenterPlatform}\n" +
                                    $"\tnextIsStartNode={nextIsStartNode}\n" +
                                    $"\tnextIsStartNode={nextIsStartNode}\n");

                                ProcessItemPedBicycle(
                                    debugPed,
                                    item,
                                    nextNodeId,
                                    nextLeftSegment,
                                    ref prevSegment,
                                    ref netManager.m_segments.m_buffer[nextLeftSegment],
                                    connectOffset,
                                    connectOffset,
                                    leftLaneIndex,
                                    leftLaneId); // ped
                            }

                            if (rightLaneId != 0u && rightLaneId != leftLaneId &&
                                (nextRightSegment != prevSegmentId || canCrossStreet || isOnCenterPlatform)) {
                                MemoryLog_Flush_If(
                                    debugPed,
                                    logBuf,
                                    unitId,
                                    () => $"*PED* item: seg. {item.Position.m_segment}, " +
                                    $"lane {item.Position.m_lane}, node {nextNodeId} " +
                                    $"({nextIsStartNode}): Exploring right segment\n" +
                                    $"\t_extPathType={queueItem_.pathType}\n" +
                                    $"\t_vehicleTypes={vehicleTypes_}, _laneTypes={laneTypes_}\n" +
                                    $"\t_extVehicleType={queueItem_.vehicleType}\n" +
                                    $"\t_isRoadVehicle={isRoadVehicle_}\n" +
                                    $"\t_isHeavyVehicle={isHeavyVehicle_}\n" +
                                    $"\t_stablePath={stablePath_}\n" +
                                    "\t_isLaneConnectionObeyingEntity" +
                                    $"={isLaneConnectionObeyingEntity_}\n" +
                                    $"\t_isLaneArrowObeyingEntity={isLaneArrowObeyingEntity_}\n\n" +
                                    $"\tnextIsStartNode={nextIsStartNode}\n" +
                                    $"\tnextRightSegment={nextRightSegment}\n" +
                                    $"\trightLaneId={rightLaneId}\n" +
                                    $"\tmayCrossStreet={canCrossStreet}\n" +
                                    $"\tisOnCenterPlatform={isOnCenterPlatform}\n" +
                                    $"\tnextIsStartNode={nextIsStartNode}\n");

                                ProcessItemPedBicycle(
                                    debugPed,
                                    item,
                                    nextNodeId,
                                    nextRightSegment,
                                    ref prevSegment,
                                    ref netManager.m_segments.m_buffer[nextRightSegment],
                                    connectOffset,
                                    connectOffset,
                                    rightLaneIndex,
                                    rightLaneId); // ped
                            }
                        }

                        // switch from bicycle lane to pedestrian lane
                        if ((vehicleTypes_ & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None
                            && prevSegment.GetClosestLane(
                                item.Position.m_lane,
                                NetInfo.LaneType.Vehicle,
                                VehicleInfo.VehicleType.Bicycle,
                                out var nextLaneIndex,
                                out var nextLaneId)) {
                            MemoryLog_Flush_If(
                                debugPed,
                                logBuf,
                                unitId,
                                () => $"*PED* item: seg. {item.Position.m_segment}, " +
                                $"lane {item.Position.m_lane}, node {nextNodeId} " +
                                $"({nextIsStartNode}): Exploring bicycle switch\n" +
                                $"\t_extPathType={queueItem_.pathType}\n" +
                                $"\t_vehicleTypes={vehicleTypes_}, _laneTypes={laneTypes_}\n" +
                                $"\t_extVehicleType={queueItem_.vehicleType}\n" +
                                $"\t_isRoadVehicle={isRoadVehicle_}\n" +
                                $"\t_isHeavyVehicle={isHeavyVehicle_}\n" +
                                $"\t_stablePath={stablePath_}\n" +
                                $"\t_isLaneConnectionObeyingEntity={isLaneConnectionObeyingEntity_}\n" +
                                $"\t_isLaneArrowObeyingEntity={isLaneArrowObeyingEntity_}\n\n" +
                                $"\tnextIsStartNode={nextIsStartNode}\n" +
                                $"\tnextLaneIndex={nextLaneIndex}\n" +
                                $"\tnextLaneId={nextLaneId}\n" +
                                $"\tnextIsStartNode={nextIsStartNode}\n");

                            ProcessItemPedBicycle(
                                debugPed,
                                item,
                                nextNodeId,
                                prevSegmentId,
                                ref prevSegment,
                                ref prevSegment,
                                connectOffset,
                                connectOffset,
                                nextLaneIndex,
                                nextLaneId); // bicycle
                        }
                    } else {
                        // we are going from pedestrian lane to a beautification node
                        for (var j = 0; j < 8; ++j) {
                            var nextSegmentId = nextNode.GetSegment(j);
                            if (nextSegmentId == 0 || nextSegmentId == prevSegmentId) {
                                continue;
                            }
#if DEBUGNEWPF
                            if (logLogic) {
                                FlushMainLog(logBuf, unitId);
                            }
#endif

                            ProcessItemCosts(
                                logLogic,
                                item,
                                nextNodeId,
                                nextSegmentId,
                                ref prevSegment, /*prevSegmentRouting,*/
                                ref netManager.m_segments.m_buffer[nextSegmentId],
                                ref prevRelSimilarLaneIndex,
                                connectOffset,
                                false,
                                true,
                                isMiddle);
                        }
                    }

                    // NON-STOCK CODE START
                    // switch from vehicle to pedestrian lane (parking)
                    var parkingAllowed = true;
                    if (Options.parkingAI) {
                        if (queueItem_.vehicleType == ExtVehicleType.PassengerCar) {
                            if ((item.LanesUsed & (NetInfo.LaneType.Vehicle
                                                     | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None) {
                                // if pocket cars are prohibited, a citizen may only park their car once per path
                                parkingAllowed = false;
                            } else if ((item.LanesUsed & NetInfo.LaneType.PublicTransport)
                                       == NetInfo.LaneType.None) {
                                // if the citizen is walking to their target (= no public transport
                                // used), the passenger car must be parked in the very last moment
                                parkingAllowed = item.LaneId == endLaneA_
                                                 || item.LaneId == endLaneB_;
                                /*if (_conf.Debug.Switches[4]) {
                                        Log._Debug($"Path unit {unitId}: public transport has not been used. ");
                                }*/
                            }
                        }
                    }

                    if (parkingAllowed) {
                        // NON-STOCK CODE END
                        var laneType = laneTypes_ & ~NetInfo.LaneType.Pedestrian;
                        var vehicleType = vehicleTypes_ & ~VehicleInfo.VehicleType.Bicycle;
                        if ((byte)(item.LanesUsed & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
                            laneType &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
                        }

                        if (laneType != NetInfo.LaneType.None
                            && vehicleType != VehicleInfo.VehicleType.None
                            && prevSegment.GetClosestLane(
                                prevLaneIndex,
                                laneType,
                                vehicleType,
                                out var nextLaneIndex2,
                                out var nextlaneId2)) {
                            var lane5 = prevSegmentInfo.m_lanes[nextLaneIndex2];
                            byte connectOffset2;

                            connectOffset2 =
                                (prevSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None ==
                                ((byte)(lane5.m_finalDirection & NetInfo.Direction.Backward) != 0)
                                    ? (byte)1
                                    : (byte)254;

                            var item2 = item;
                            if (randomParking_) {
                                item2.ComparisonValue += pathRandomizer_.Int32(300u) / maxLength_;
                            }

                            MemoryLog_Flush_If(
                                debugPed,
                                logBuf,
                                unitId,
                                () => $"*PED* item: seg. {item.Position.m_segment}, " +
                                $"lane {item.Position.m_lane}, node {nextNodeId} " +
                                $"({nextIsStartNode}): Exploring parking switch\n" +
                                $"\t_extPathType={queueItem_.pathType}\n" +
                                $"\t_vehicleTypes={vehicleTypes_}, _laneTypes={laneTypes_}\n" +
                                $"\t_extVehicleType={queueItem_.vehicleType}\n" +
                                $"\t_isRoadVehicle={isRoadVehicle_}\n" +
                                $"\t_isHeavyVehicle={isHeavyVehicle_}\n" +
                                $"\t_stablePath={stablePath_}\n" +
                                $"\t_isLaneConnectionObeyingEntity={isLaneConnectionObeyingEntity_}\n" +
                                $"\t_isLaneArrowObeyingEntity={isLaneArrowObeyingEntity_}\n\n" +
                                $"\tnextIsStartNode={nextIsStartNode}\n" +
                                $"\tnextLaneIndex2={nextLaneIndex2}\n" +
                                $"\tnextlaneId2={nextlaneId2}\n" +
                                $"\tnextIsStartNode={nextIsStartNode}\n");

                            ProcessItemPedBicycle(
                                debugPed,
                                item2,
                                nextNodeId,
                                prevSegmentId,
                                ref prevSegment,
                                ref prevSegment,
                                connectOffset2,
                                128,
                                nextLaneIndex2,
                                nextlaneId2); // ped
                        }
                    }
                }
            } else {
                // we are going to a non-pedestrian lane
                var allowPedestrian = (byte)(laneTypes_ & NetInfo.LaneType.Pedestrian) != 0; // allow pedestrian switching to vehicle?
                var nextIsBeautificationNode = nextNode.Info.m_class.m_service == ItemClass.Service.Beautification;
                var allowBicycle = false; // is true if cim may switch from a pedestrian lane to a bike lane
                byte parkingConnectOffset = 0;
                if (allowPedestrian) {
                    if (prevIsBicycleLane) {
                        // we are going to a bicycle lane
                        parkingConnectOffset = connectOffset;
                        allowBicycle = nextIsBeautificationNode;
                    } else if (vehicleLane_ != 0u) {
                        // there is a parked vehicle position
                        if (vehicleLane_ != item.LaneId) {
                            // we have not reached the parked vehicle yet
                            allowPedestrian = false;
                        } else {
                            // pedestrian switches to parked vehicle
                            parkingConnectOffset = vehicleOffset_;
                        }
                    } else if (stablePath_) {
                        // enter a bus
                        parkingConnectOffset = 128;
                    } else {
                        // pocket car spawning
                        if (Options.parkingAI &&
                            queueItem_.vehicleType == ExtVehicleType.PassengerCar
                            && (queueItem_.pathType == ExtPathType.WalkingOnly
                             || (queueItem_.pathType == ExtPathType.DrivingOnly
                                 && item.Position.m_segment != startSegmentA_
                                 && item.Position.m_segment != startSegmentB_))) {
                            allowPedestrian = false;
                        } else {
                            parkingConnectOffset = (byte)pathRandomizer_.UInt32(1u, 254u);
                        }
                    }
                }

                if ((vehicleTypes_ & VehicleInfo.VehicleType.Ferry) != VehicleInfo.VehicleType.None) {
                    // monorail / ferry
                    for (var k = 0; k < 8; k++) {
                        var nextSegmentId = nextNode.GetSegment(k);
                        if (nextSegmentId == 0 || nextSegmentId == prevSegmentId) {
                            continue;
                        }

                        ProcessItemCosts(
                            logLogic,
                            item,
                            nextNodeId,
                            nextSegmentId,
                            ref prevSegment, /*prevSegmentRouting,*/
                            ref netManager.m_segments.m_buffer[nextSegmentId],
                            ref prevRelSimilarLaneIndex,
                            connectOffset,
                            true,
                            allowBicycle,
                            isMiddle);
                    }

                    if ((nextNode.m_flags & (NetNode.Flags.End
                                             | NetNode.Flags.Bend
                                             | NetNode.Flags.Junction)) != NetNode.Flags.None /*&&
                        (this._vehicleTypes & VehicleInfo.VehicleType.Monorail) == VehicleInfo.VehicleType.None*/) {
                        ProcessItemCosts(
                            logLogic,
                            item,
                            nextNodeId,
                            prevSegmentId,
                            ref prevSegment, /*prevSegmentRouting,*/
                            ref prevSegment,
                            ref prevRelSimilarLaneIndex,
                            connectOffset,
                            true,
                            false,
                            isMiddle);
                    }
                } else {
                    // road vehicles, trams, trains, metros, monorails, etc.
                    // specifies if vehicles should follow lane arrows
                    var isStrictLaneChangePolicyEnabled = false;

                    // specifies if the entity is allowed to u-turn (in general)
                    var isEntityAllowedToUturn =
                        (vehicleTypes_ & (VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Monorail)) ==
                        VehicleInfo.VehicleType.None;

                    // specifies if thes next node allows for u-turns
                    var isUturnAllowedHere = (nextNode.m_flags & (NetNode.Flags.End
                                                                  | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
                    /*
                     * specifies if u-turns are handled by custom code.
                     * If not (performCustomVehicleUturns == false) AND the vanilla u-turn condition (stockUturn) evaluates to true, then u-turns are handled by the vanilla code
                     */

                    //bool performCustomVehicleUturns = false;
                    var prevIsRouted = prevLaneEndRouting.routed
#if DEBUG
                                        && !DebugSwitch.JunctionRestrictions.Get()
#endif
                        ;

                    if (prevIsRouted) {
                        var prevIsOutgoingOneWay = nextIsStartNode
                                                       ? prevSegmentRouting.startNodeOutgoingOneWay
                                                       : prevSegmentRouting.endNodeOutgoingOneWay;
                        var nextIsUntouchable = (nextNode.m_flags & NetNode.Flags.Untouchable) != NetNode.Flags.None;
                        var nextIsTransitionOrJunction =
                            (nextNode.m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) !=
                            NetNode.Flags.None;
                        var nextIsBend = (nextNode.m_flags & NetNode.Flags.Bend) != NetNode.Flags.None;

                        // determine if the vehicle may u-turn at the target node according to customization
                        isUturnAllowedHere =
                            isUturnAllowedHere || // stock u-turn points
                            (Options.junctionRestrictionsEnabled &&
                             isRoadVehicle_ && // only road vehicles may perform u-turns
                             JunctionManager.IsUturnAllowed(prevSegmentId, nextIsStartNode) && // only do u-turns if allowed
                             !nextIsBeautificationNode && // no u-turns at beautification nodes // TODO refactor to JunctionManager
                             prevIsCarLane && // u-turns for road vehicles only
                             !isHeavyVehicle_ && // only small vehicles may perform u-turns
                             (nextIsTransitionOrJunction || nextIsBend) && // perform u-turns at transitions, junctions and bend nodes // TODO refactor to JunctionManager
                             !prevIsOutgoingOneWay); // do not u-turn on one-ways // TODO refactor to JunctionManager

                        isStrictLaneChangePolicyEnabled =
                            !nextIsBeautificationNode && // do not obey lane arrows at beautification nodes
                            !nextIsUntouchable &&
                            isLaneArrowObeyingEntity_ &&
                            // nextIsTransitionOrJunction && // follow lane arrows only at transitions and junctions
                            !(
#if DEBUG
                                 Options.allRelaxed || // debug option: all vehicle may ignore lane arrows
#endif
                                 (Options.relaxedBusses && queueItem_.vehicleType == ExtVehicleType.Bus)); // option: busses may ignore lane arrows

                        /*if (! performCustomVehicleUturns) {
                                isUturnAllowedHere = false;
                        }*/
                        // isEntityAllowedToUturn = isEntityAllowedToUturn && !performCustomVehicleUturns;

                        MemoryLog_NoFlush_If(
                                debugPed,
                                logBuf,
                                () => $"item: seg. {item.Position.m_segment}, lane {item.Position.m_lane} " +
                                $"(id {item.LaneId}), node {nextNodeId} ({nextIsStartNode}):\n" +
                                $"\t_extPathType={queueItem_.pathType}\n" +
                                $"\t_vehicleTypes={vehicleTypes_}, _laneTypes={laneTypes_}\n" +
                                $"\t_extVehicleType={queueItem_.vehicleType}\n" +
                                $"\t_isRoadVehicle={isRoadVehicle_}\n" +
                                $"\t_isHeavyVehicle={isHeavyVehicle_}\n" +
                                $"\t_vehicleLane={vehicleLane_}\n" +
                                $"\t_stablePath={stablePath_}\n" +
                                $"\t_isLaneConnectionObeyingEntity={isLaneConnectionObeyingEntity_}\n" +
                                $"\t_isLaneArrowObeyingEntity={isLaneArrowObeyingEntity_}\n\n" +
                                $"\tprevIsOutgoingOneWay={prevIsOutgoingOneWay}\n" +
                                $"\tprevIsRouted={prevIsRouted}\n\n" +
                                $"\tnextIsStartNode={nextIsStartNode}\n" +
                                $"\tisNextBeautificationNode={nextIsBeautificationNode}\n" +
                                $"\tnextIsTransitionOrJunction={nextIsTransitionOrJunction}\n" +
                                $"\tnextIsBend={nextIsBend}\n" +
                                $"\tnextIsUntouchable={nextIsUntouchable}\n" +
                                $"\tallowBicycle={allowBicycle}\n" +
                                "\tisCustomUturnAllowed" +
                                $"={JunctionManager.IsUturnAllowed(prevSegmentId, nextIsStartNode)}\n" +
                                $"\tisStrictLaneArrowPolicyEnabled={isStrictLaneChangePolicyEnabled}\n" +
                                $"\tisEntityAllowedToUturn={isEntityAllowedToUturn}\n" +
                                $"\tisUturnAllowedHere={isUturnAllowedHere}\n"
                                //"\t" + $"performCustomVehicleUturns={performCustomVehicleUturns}\n"
                            );
                    } else {
                        MemoryLog_NoFlush_If(
                            logLogic,
                            logBuf,
                            () => $"item: seg. {item.Position.m_segment}, lane {item.Position.m_lane}, " +
                            $"node {nextNodeId} ({nextIsStartNode}):\n" +
                            $"\t_extPathType={queueItem_.pathType}\n" +
                            $"\t_vehicleTypes={vehicleTypes_}, _laneTypes={laneTypes_}\n" +
                            $"\t_extVehicleType={queueItem_.vehicleType}\n" +
                            $"\t_isRoadVehicle={isRoadVehicle_}\n" +
                            $"\t_isHeavyVehicle={isHeavyVehicle_}\n" +
                            $"\t_stablePath={stablePath_}\n" +
                            $"\t_isLaneConnectionObeyingEntity={isLaneConnectionObeyingEntity_}\n" +
                            $"\t_isLaneArrowObeyingEntity={isLaneArrowObeyingEntity_}\n\n" +
                            $"\tprevIsRouted={prevIsRouted}\n\n");
                    }

                    if (allowBicycle || !prevIsRouted) {
                        /*
                         * pedestrian to bicycle lane switch or no routing information available:
                         * if pedestrian lanes should be explored (allowBicycle == true): do this here
                         * if previous segment has custom routing (prevIsRouted == true): do NOT explore
                         * vehicle lanes here, else: vanilla exploration of vehicle lanes
                        */
                        MemoryLog_Flush_If(
                            logLogic,
                            logBuf,
                            unitId,
                            () => $"item: seg. {item.Position.m_segment}, lane {item.Position.m_lane}, " +
                            $"node {nextNodeId}:\n\t-> using DEFAULT exploration mode\n");

                        /*if (performCustomVehicleUturns) {
                                isUturnAllowedHere = true;
                                isEntityAllowedToUturn = true;
                        }*/

                        var nextSegmentId = prevSegment.GetRightSegment(nextNodeId);
                        for (var k = 0; k < 8; ++k) {
                            if (nextSegmentId == 0 || nextSegmentId == prevSegmentId) {
                                break;
                            }

                            if (ProcessItemCosts(
                                logLogic,
                                item,
                                nextNodeId,
                                nextSegmentId,
                                ref prevSegment, /*prevSegmentRouting,*/
                                ref netManager.m_segments.m_buffer[nextSegmentId],
                                ref prevRelSimilarLaneIndex,
                                connectOffset,
                                !prevIsRouted,
                                allowBicycle,
                                isMiddle))
                            {
                                // exceptional u-turns
                                isUturnAllowedHere = true;
                            }

                            nextSegmentId = netManager.m_segments.m_buffer[nextSegmentId].GetRightSegment(nextNodeId);
                        }
                    }

                    if (prevIsRouted) {
                        /* routed vehicle paths */
                        MemoryLog_NoFlush_If(
                            logLogic,
                            logBuf,
                            () => $"item: seg. {item.Position.m_segment}, lane {item.Position.m_lane}, " +
                            $"node {nextNodeId}:\n\t-> using CUSTOM exploration mode\n");

                        var canUseLane = CanUseLane(
                            logLogic,
                            item.Position.m_segment,
                            prevSegmentInfo,
                            item.Position.m_lane,
                            prevLaneInfo);

                        var laneTransitions = prevLaneEndRouting.transitions;
                        if (laneTransitions != null &&
                            (canUseLane || Options.vehicleRestrictionsAggression !=
                             VehicleRestrictionsAggression.Strict)) {
                            MemoryLog_NoFlush_If(
                                logLogic,
                                logBuf,
                                () => $"item: seg. {item.Position.m_segment}, " +
                                $"lane {item.Position.m_lane}, node {nextNodeId}:\n\tCUSTOM exploration\n");

                            // lane changing cost calculation mode to use
                            var laneChangingCostCalculationMode = LaneChangingCostCalculationMode.None;

                            float? segmentSelectionCost = null; // cost for using that particular segment
                            float? laneSelectionCost = null; // cost for using that particular lane

                            /*
                             * =======================================================================================================
                             * (1) Apply vehicle restrictions
                             * =======================================================================================================
                             */

                            if (! canUseLane) {
                                laneSelectionCost = VehicleRestrictionsManager.PATHFIND_PENALTIES[(int)Options.vehicleRestrictionsAggression];

                                MemoryLog_NoFlush_If(
                                    logLogic,
                                    logBuf,
                                    () => $"item: seg. {item.Position.m_segment}, " +
                                    $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                    $"\tapplied vehicle restrictions for vehicle {queueItem_.vehicleId}, " +
                                    $"type {queueItem_.vehicleType}:\n" +
                                    $"\t=> laneSelectionCost={laneSelectionCost}\n");
                            }

                            if (isRoadVehicle_ &&
                                prevLaneInfo != null &&
                                prevIsCarLane) {

                                if (Options.advancedAI) {
                                    laneChangingCostCalculationMode = LaneChangingCostCalculationMode.ByGivenDistance;
                                    MemoryLog_NoFlush_If(
                                        logLogic,
                                        logBuf,
                                        () => $"item: seg. {item.Position.m_segment}, " +
                                        $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                        "\tAI is active, prev is car lane and we are a car\n");
                                }

                                /*
                                 * =======================================================================================================
                                 * (2) Apply car ban district policies
                                 * =======================================================================================================
                                 */

                                // Apply costs for traffic ban policies
                                if ((prevLaneInfo.m_laneType & (NetInfo.LaneType.Vehicle
                                                                | NetInfo.LaneType.TransportVehicle))
                                    != NetInfo.LaneType.None &&
                                    (prevLaneInfo.m_vehicleType & vehicleTypes_) == VehicleInfo.VehicleType.Car
                                    && (netManager.m_segments.m_buffer[item.Position.m_segment].m_flags &
                                     carBanMask_) != NetSegment.Flags.None)
                                {
                                    // heavy vehicle ban / car ban ("Old Town" policy)
                                    if (laneSelectionCost == null) {
                                        laneSelectionCost = 1f;
                                    }
#if DEBUGNEWPF
                                    var oldLaneSelectionCost = laneSelectionCost;
#endif
                                    laneSelectionCost *= 7.5f;

                                    MemoryLog_NoFlush_If(
                                        logLogic,
                                        logBuf,
                                        () => $"item: seg. {item.Position.m_segment}, " +
                                        $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                        "\tapplied heavy vehicle ban / car ban ('Old Town' policy):\n" +
                                        $"\toldLaneSelectionCost={oldLaneSelectionCost}\n" +
                                        $"\t=> laneSelectionCost={laneSelectionCost}\n");
                                }

                                /*
                                 * =======================================================================================================
                                 * (3) Apply costs for using/not using transport lanes
                                 * =======================================================================================================
                                 */

                                /*
                                 * (1) busses should prefer transport lanes
                                 * (2) regular traffic should prefer regular lanes
                                 * (3) taxis, service vehicles and emergency vehicles may choose freely between regular and transport lanes
                                 */
                                if ((prevLaneInfo.m_laneType & NetInfo.LaneType.TransportVehicle)
                                    != NetInfo.LaneType.None)
                                {
                                    // previous lane is a public transport lane
                                    if ((queueItem_.vehicleType & ExtVehicleType.Bus) != ExtVehicleType.None) {
                                        if (laneSelectionCost == null) {
                                            laneSelectionCost = 1f;
                                        }
#if DEBUGNEWPF
                                        var oldLaneSelectionCost = laneSelectionCost;
#endif
                                        laneSelectionCost *= globalConf_.PathFinding.PublicTransportLaneReward; // (1)
                                        MemoryLog_NoFlush_If(
                                            logLogic,
                                            logBuf,
                                            () => $"item: seg. {item.Position.m_segment}, " +
                                            $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                            "\tapplied bus-on-transport lane reward:\n" +
                                            $"\toldLaneSelectionCost={oldLaneSelectionCost}\n" +
                                            $"\t=> laneSelectionCost={laneSelectionCost}\n");
                                    } else if ((queueItem_.vehicleType &
                                                (ExtVehicleType.RoadPublicTransport | ExtVehicleType.Service |
                                                 ExtVehicleType.Emergency)) == ExtVehicleType.None) {
                                        if (laneSelectionCost == null) {
                                            laneSelectionCost = 1f;
                                        }
#if DEBUGNEWPF
                                        var oldLaneSelectionCost = laneSelectionCost;
#endif
                                        laneSelectionCost *= globalConf_.PathFinding.PublicTransportLanePenalty; // (2)
                                        MemoryLog_NoFlush_If(
                                            logLogic,
                                            logBuf,
                                            () => $"item: seg. {item.Position.m_segment}, " +
                                                $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                                "\tapplied car-on-transport lane penalty:\n" +
                                                $"\toldLaneSelectionCost={oldLaneSelectionCost}\n" +
                                                $"\t=> laneSelectionCost={laneSelectionCost}\n");
                                    }
                                }

                                /*
                                 * =======================================================================================================
                                 * (4) Apply costs for large vehicles using inner lanes on highways
                                 * =======================================================================================================
                                 */

                                var nextIsJunction =
                                    (netManager.m_nodes.m_buffer[nextNodeId].m_flags &
                                     (NetNode.Flags.Junction | NetNode.Flags.Transition)) == NetNode.Flags.Junction;
                                var nextIsRealJunction =
                                    nextIsJunction &&
                                    (netManager.m_nodes.m_buffer[nextNodeId].m_flags &
                                     (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)) !=
                                    (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut);
                                var prevNodeId = nextNodeId == prevSegment.m_startNode
                                                     ? prevSegment.m_endNode
                                                     : prevSegment.m_startNode;
                                if (prevLaneInfo.m_similarLaneCount > 1) {
                                    if (isHeavyVehicle_ &&
                                        Options.preferOuterLane &&
                                        prevSegmentRouting.highway &&
                                        pathRandomizer_.Int32(globalConf_.PathFinding.HeavyVehicleInnerLanePenaltySegmentSel) == 0
                                            /* && (netManager.m_nodes.m_buffer[prevNodeId].m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) != NetNode.Flags.None */
                                        ) {
                                        // penalize large vehicles for using inner lanes
                                        if (laneSelectionCost == null) {
                                            laneSelectionCost = 1f;
                                        }
#if DEBUGNEWPF
                                        var oldLaneSelectionCost = laneSelectionCost;
#endif
                                        var prevRelOuterLane = prevOuterSimilarLaneIndex / (float)(prevLaneInfo.m_similarLaneCount - 1);
                                        laneSelectionCost *=
                                            1f + (globalConf_.PathFinding.HeavyVehicleMaxInnerLanePenalty
                                            * prevRelOuterLane);
                                        MemoryLog_NoFlush_If(
                                            logLogic,
                                            logBuf,
                                            () => $"item: seg. {item.Position.m_segment}, " +
                                            $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                            "\tapplied inner lane penalty:\n" +
                                            $"\toldLaneSelectionCost={oldLaneSelectionCost}\n" +
                                            $"\t=> laneSelectionCost={laneSelectionCost}\n");
                                    }

                                    /*
                                     * =======================================================================================================
                                     * (5) Apply costs for randomized lane selection in front of
                                     * junctions and highway transitions
                                     * =======================================================================================================
                                     */
                                    if (Options.advancedAI &&
                                        !stablePath_ &&
                                        !isHeavyVehicle_ &&
                                        nextIsJunction &&
                                        pathRandomizer_.Int32(
                                            globalConf_.AdvancedVehicleAI.LaneRandomizationJunctionSel) == 0)
                                    {
                                        // randomized lane selection at junctions
                                        if (laneSelectionCost == null) {
                                            laneSelectionCost = 1f;
                                        }
#if DEBUGNEWPF
                                        var oldLaneSelectionCost = laneSelectionCost;
#endif
                                        laneSelectionCost *=
                                            1f + (pathRandomizer_.Int32(2) *
                                            globalConf_.AdvancedVehicleAI.LaneRandomizationCostFactor);
                                        MemoryLog_NoFlush_If(
                                            logLogic,
                                            logBuf,
                                            () => $"item: seg. {item.Position.m_segment}, " +
                                            $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                            "\tapplied lane randomizations at junctions:\n" +
                                            $"\toldLaneSelectionCost={oldLaneSelectionCost}\n" +
                                            $"\t=> laneSelectionCost={laneSelectionCost}\n");
                                    }
                                }

                                /*
                                 * =======================================================================================================
                                 * (6) Apply junction costs
                                 * =======================================================================================================
                                 */
                                if (Options.advancedAI && nextIsJunction && prevSegmentRouting.highway) {
                                    if (segmentSelectionCost == null) {
                                        segmentSelectionCost = 1f;
                                    }

                                    segmentSelectionCost *= 1f + globalConf_.AdvancedVehicleAI.JunctionBaseCost;
                                }

                                /*
                                 * =======================================================================================================
                                 * (7) Apply traffic measurement costs for segment selection
                                 * =======================================================================================================
                                 */
                                if (Options.advancedAI &&
                                    (queueItem_.vehicleType & ExtVehicleType.RoadVehicle & ~ExtVehicleType.Bus) !=
                                    ExtVehicleType.None && !stablePath_)
                                {
                                    // segment selection based on segment traffic volume
                                    var prevFinalDir =
                                        nextIsStartNode ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;

                                    prevFinalDir =
                                        (prevSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None
                                            ? prevFinalDir
                                            : NetInfo.InvertDirection(prevFinalDir);

                                    var prevDirTrafficData =
                                        TrafficMeasurementManager.SegmentDirTrafficData[
                                            TrafficMeasurementManager.GetDirIndex(
                                                item.Position.m_segment,
                                                prevFinalDir)];

                                    var segmentTraffic = Mathf.Clamp(
                                        1f - (prevDirTrafficData.meanSpeed /
                                        (float)TrafficMeasurementManager.REF_REL_SPEED) + item.TrafficRand,
                                        0,
                                        1f);

                                    if (segmentSelectionCost == null) {
                                        segmentSelectionCost = 1f;
                                    }

                                    segmentSelectionCost *= 1f +
                                                            (globalConf_.AdvancedVehicleAI.TrafficCostFactor *
                                                            segmentTraffic);
                                    MemoryLog_NoFlush_If(
                                        logLogic,
                                        logBuf,
                                        () => $"item: seg. {item.Position.m_segment}, " +
                                        $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                        "\tapplied traffic measurement costs for segment selection:\n" +
                                        $"\tsegmentTraffic={segmentTraffic}\n" +
                                        $"\t=> segmentSelectionCost={segmentSelectionCost}\n");

                                    if (globalConf_.AdvancedVehicleAI.LaneDensityRandInterval > 0 && nextIsRealJunction) {
                                        item.TrafficRand =
                                            0.01f * (pathRandomizer_.Int32(
                                                         (uint)globalConf_.AdvancedVehicleAI.LaneDensityRandInterval
                                                         + 1u) - (globalConf_.AdvancedVehicleAI.LaneDensityRandInterval
                                                                  / 2f));

                                        MemoryLog_NoFlush_If(
                                            logLogic,
                                            logBuf,
                                            () => $"item: seg. {item.Position.m_segment}, " +
                                            $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                            "\tupdated item.m_trafficRand:\n" +
                                            $"\t=> item.m_trafficRand={item.TrafficRand}\n");
                                    }
                                }

                                MemoryLog_NoFlush_If(
                                    logLogic,
                                    logBuf,
                                    () => $"item: seg. {item.Position.m_segment}, " +
                                    $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                    "\tcalculated traffic stats:\n" +
                                    $"\t_vehicleTypes={vehicleTypes_}, _laneTypes={laneTypes_}\n" +
                                    $"\t_extVehicleType={queueItem_.vehicleType}\n" +
                                    $"\t_isRoadVehicle={isRoadVehicle_}\n" +
                                    $"\t_isHeavyVehicle={isHeavyVehicle_}\n" +
                                    $"\t_isLaneConnectionObeyingEntity={isLaneConnectionObeyingEntity_}\n" +
                                    $"\t_isLaneArrowObeyingEntity={isLaneArrowObeyingEntity_}\n\n" +
                                    $"\tlaneSelectionCost={laneSelectionCost}\n" +
                                    $"\tsegmentSelectionCost={segmentSelectionCost}\n");
                            }

                            for (var k = 0; k < laneTransitions.Length; ++k) {
                                var nextSegmentId = laneTransitions[k].segmentId;

                                if (nextSegmentId == 0) {
                                    MemoryLog_Flush_If(
                                        logLogic,
                                        logBuf,
                                        unitId,
                                        () => $"item: seg. {item.Position.m_segment}, " +
                                              $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                              "\t" + "CUSTOM exploration\n" +
                                              "\t" + $"transition iteration {k}:\n" +
                                              "\t" + $"{laneTransitions[k].ToString()}\n" +
                                              "\t" + "*** SKIPPING *** (nextSegmentId=0)\n");
                                    continue;
                                }

                                var uturn = nextSegmentId == prevSegmentId;
                                if (uturn) {
                                    // prevent double/forbidden exploration of previous segment by vanilla code during this method execution
                                    if (! isEntityAllowedToUturn || ! isUturnAllowedHere) {
                                        MemoryLog_Flush_If(
                                            logLogic,
                                            logBuf,
                                            unitId,
                                            () => $"item: seg. {item.Position.m_segment}, " +
                                                $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                                "\tCUSTOM exploration\n" +
                                                $"\ttransition iteration {k}:\n" +
                                                $"\t{laneTransitions[k].ToString()}\n" +
                                                "\t*** SKIPPING *** (u-turns prohibited)\n");
                                        continue;
                                    }
                                }

                                if (laneTransitions[k].type == LaneEndTransitionType.Invalid) {
                                    MemoryLog_Flush_If(
                                        logLogic,
                                        logBuf,
                                        unitId,
                                        () => $"item: seg. {item.Position.m_segment}, " +
                                        $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                        "\tCUSTOM exploration\n" +
                                        $"\ttransition iteration {k}:\n" +
                                        $"\t{laneTransitions[k].ToString()}\n" +
                                        "\t*** SKIPPING *** (invalid transition)\n");
                                    continue;
                                }

                                // allow vehicles to ignore strict lane routing when moving off
                                var relaxedLaneChanging =
                                    isRoadVehicle_ &&
                                    (queueItem_.vehicleType &
                                     (ExtVehicleType.Service | ExtVehicleType.PublicTransport |
                                      ExtVehicleType.Emergency)) != ExtVehicleType.None &&
                                    queueItem_.vehicleId == 0 &&
                                    (laneTransitions[k].laneId == startLaneA_ ||
                                     laneTransitions[k].laneId == startLaneB_);

                                if (!relaxedLaneChanging && isStrictLaneChangePolicyEnabled &&
                                    laneTransitions[k].type == LaneEndTransitionType.Relaxed) {
                                    MemoryLog_Flush_If(
                                        logLogic,
                                        logBuf,
                                        unitId,
                                        () => $"item: seg. {item.Position.m_segment}, " +
                                        $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                        "\tCUSTOM exploration\n" +
                                        $"\ttransition iteration {k}:\n" +
                                        $"\t{laneTransitions[k].ToString()}\n" +
                                        $"\trelaxedLaneChanging={relaxedLaneChanging}\n" +
                                        $"\tisStrictLaneChangePolicyEnabled={relaxedLaneChanging}\n" +
                                        "\t*** SKIPPING *** (incompatible lane)\n");
                                    continue;
                                }

                                MemoryLog_Flush_If(
                                    logLogic,
                                    logBuf,
                                    unitId,
                                    () => $"item: seg. {item.Position.m_segment}, " +
                                    $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                    "\tCUSTOM exploration\n" +
                                    $"\ttransition iteration {k}:\n" +
                                    $"\t{laneTransitions[k].ToString()}\n" +
                                    "\t> PERFORMING EXPLORATION NOW <\n");

                                var foundForced = false;
                                var prevLaneIndexFromInner = prevRelSimilarLaneIndex;
                                if (ProcessItemCosts(
                                    logLogic,
                                    false,
                                    laneChangingCostCalculationMode,
                                    item,
                                    nextNodeId,
                                    nextSegmentId,
                                    ref prevSegment, /*prevSegmentRouting,*/
                                    ref netManager.m_segments.m_buffer
                                        [nextSegmentId], /*routingManager.segmentRoutings[nextSegmentId],*/
                                    ref prevLaneIndexFromInner,
                                    connectOffset,
                                    true,
                                    false,
                                    laneTransitions[k].laneIndex,
                                    laneTransitions[k].laneId,
                                    laneTransitions[k].distance,
                                    segmentSelectionCost,
                                    laneSelectionCost,
                                    isMiddle,
                                    out foundForced))
                                {
                                    // process exceptional u-turning in vanilla code
                                    isUturnAllowedHere = true;
                                }
                            }
                        }
                    }

                    if (!prevIsRouted && isEntityAllowedToUturn && isUturnAllowedHere) {
                        MemoryLog_Flush_If(
                            logLogic,
                            logBuf,
                            unitId,
                            () => $"path unit {unitId}\nitem: seg. {item.Position.m_segment}, " +
                            $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                            "\t-> exploring DEFAULT u-turn\n");

                        ProcessItemCosts(
                            logLogic,
                            item,
                            nextNodeId,
                            prevSegmentId,
                            ref prevSegment, /*prevSegmentRouting,*/
                            ref prevSegment,
                            ref prevRelSimilarLaneIndex,
                            connectOffset,
                            true,
                            false,
                            isMiddle);
                    }
                }

                if (allowPedestrian) {
                    // switch from walking to driving a car, bus, etc.
                    if (prevSegment.GetClosestLane(
                        item.Position.m_lane,
                        NetInfo.LaneType.Pedestrian,
                        vehicleTypes_,
                        out var nextLaneIndex,
                        out var nextLaneId)) {
                        MemoryLog_Flush_If(
                            debugPed,
                            logBuf,
                            unitId,
                            () => $"*PED* item: seg. {item.Position.m_segment}, " +
                            $"lane {item.Position.m_lane}, node {nextNodeId} ({nextIsStartNode}): " +
                            "Exploring vehicle switch\n" +
                            $"\t_extPathType={queueItem_.pathType}\n" +
                            $"\t_vehicleTypes={vehicleTypes_}, _laneTypes={laneTypes_}\n" +
                            $"\t_extVehicleType={queueItem_.vehicleType}\n" +
                            $"\t_isRoadVehicle={isRoadVehicle_}\n" +
                            $"\t_isHeavyVehicle={isHeavyVehicle_}\n" +
                            $"\t_stablePath={stablePath_}\n" +
                            $"\t_isLaneConnectionObeyingEntity={isLaneConnectionObeyingEntity_}\n" +
                            $"\t_isLaneArrowObeyingEntity={isLaneArrowObeyingEntity_}\n\n" +
                            $"\tnextIsStartNode={nextIsStartNode}\n" +
                            $"\tnextLaneIndex={nextLaneIndex}\n" +
                            $"\tnextLaneId={nextLaneId}\n" +
                            $"\tnextIsStartNode={nextIsStartNode}\n");
                        ProcessItemPedBicycle(
                            debugPed,
                            item,
                            nextNodeId,
                            prevSegmentId,
                            ref prevSegment,
                            ref prevSegment,
                            parkingConnectOffset,
                            parkingConnectOffset,
                            nextLaneIndex,
                            nextLaneId); // ped
                    }
                } // allowPedSwitch
            } // !prevIsPedestrianLane

            // [18/05/06] conditions commented out because cims could not go to an outside connection with path "walk -> public transport -> walk -> car"
            if (nextNode.m_lane != 0u /*&&
                (!Options.parkingAI ||
                queueItem.vehicleType != ExtVehicleType.PassengerCar ||
                (item.m_lanesUsed & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == NetInfo.LaneType.None)*/) {
                // transport lines, cargo lines, etc.
                var targetDisabled = (nextNode.m_flags & (NetNode.Flags.Disabled
                                                          | NetNode.Flags.DisableOnlyMiddle))
                                     == NetNode.Flags.Disabled;
                var nextSegmentId = netManager.m_lanes.m_buffer[nextNode.m_lane].m_segment;

                if (nextSegmentId != 0 && nextSegmentId != item.Position.m_segment) {
                    var nextNodeLane = nextNode.m_lane;
                    MemoryLog_Flush_If(
                        logLogic,
                        logBuf,
                        unitId,
                        () => $"item: seg. {item.Position.m_segment}, lane {item.Position.m_lane}, " +
                        $"node {nextNodeId} ({nextIsStartNode}): " +
                        "Exploring transport segment\n" +
                        $"\t_extPathType={queueItem_.pathType}\n" +
                        $"\t_vehicleTypes={vehicleTypes_}, _laneTypes={laneTypes_}\n" +
                        $"\t_extVehicleType={queueItem_.vehicleType}\n" +
                        $"\t_isRoadVehicle={isRoadVehicle_}\n" +
                        $"\t_isHeavyVehicle={isHeavyVehicle_}\n" +
                        $"\t_stablePath={stablePath_}\n" +
                        $"\t_isLaneConnectionObeyingEntity={isLaneConnectionObeyingEntity_}\n" +
                        $"\t_isLaneArrowObeyingEntity={isLaneArrowObeyingEntity_}\n\n" +
                        $"\tnextNode.m_lane={nextNodeLane}\n" +
                        $"\tnextSegmentId={nextSegmentId}\n" +
                        $"\tnextIsStartNode={nextIsStartNode}\n");
                    ProcessItemPublicTransport(
                        logLogic,
                        item,
                        nextNodeId,
                        targetDisabled,
                        nextSegmentId,
                        ref prevSegment,
                        ref netManager.m_segments.m_buffer[nextSegmentId],
                        nextNode.m_lane,
                        nextNode.m_laneOffset,
                        connectOffset);
                }
            }

#if DEBUGNEWPF
            if (logLogic) {
                FlushMainLog(logBuf, unitId);
            }
#endif
        }

        // 2
        private void ProcessItemPublicTransport(bool debug,
                                                BufferItem item,
                                                ushort nextNodeId,
                                                bool targetDisabled,
                                                ushort nextSegmentId,
                                                ref NetSegment prevSegment,
                                                ref NetSegment nextSegment,
                                                uint nextLaneId,
                                                byte offset,
                                                byte connectOffset) {
            if ((nextSegment.m_flags & disableMask_) != NetSegment.Flags.None) {
                return;
            }

            var netManager = Singleton<NetManager>.instance;
            if (targetDisabled &&
                ((netManager.m_nodes.m_buffer[nextSegment.m_startNode].m_flags |
                  netManager.m_nodes.m_buffer[nextSegment.m_endNode].m_flags) & NetNode.Flags.Disabled) ==
                NetNode.Flags.None) {
                return;
            }

#if COUNTSEGMENTSTONEXTJUNCTION
            bool nextIsRegularNode = nextNodeId == prevSegment.m_startNode || nextNodeId == prevSegment.m_endNode;
            bool prevIsRealJunction = false;
            if (nextIsRegularNode) {
                // no lane changing directly in front of a junction
                ushort prevNodeId = (nextNodeId == prevSegment.m_startNode) ? prevSegment.m_endNode : prevSegment.m_startNode;
                prevIsRealJunction = (netManager.m_nodes.m_buffer[prevNodeId].m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) == NetNode.Flags.Junction &&
                    (netManager.m_nodes.m_buffer[prevNodeId].m_flags & (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)) != (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut);
            }
#endif

            var nextSegmentInfo = nextSegment.Info;
            var prevSegmentInfo = prevSegment.Info;
            var nextNumLanes = nextSegmentInfo.m_lanes.Length;
            var curLaneId = nextSegment.m_lanes;
            var prevMaxSpeed = 1f;
            var prevSpeed = 1f;
            var prevLaneType = NetInfo.LaneType.None;

            if (item.Position.m_lane < prevSegmentInfo.m_lanes.Length) {
                var prevLaneInfo = prevSegmentInfo.m_lanes[item.Position.m_lane];
                prevMaxSpeed = GetLaneSpeedLimit(
                    item.Position.m_segment,
                    item.Position.m_lane,
                    item.LaneId,
                    prevLaneInfo); // NON-STOCK CODE

                prevLaneType = prevLaneInfo.m_laneType;

                if ((prevLaneType &
                     (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                    NetInfo.LaneType.None) {
                    prevLaneType = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
                }

                prevSpeed = CalculateLaneSpeed(
                    prevMaxSpeed,
                    connectOffset,
                    item.Position.m_offset,
                    ref prevSegment,
                    prevLaneInfo); // NON-STOCK CODE
            }

            var segLength = prevLaneType == NetInfo.LaneType.PublicTransport
                                ? netManager.m_lanes.m_buffer[item.LaneId].m_length
                                : Mathf.Max(
                                    SEGMENT_MIN_AVERAGE_LENGTH,
                                    prevSegment.m_averageLength);

            var offsetLength = Mathf.Abs(connectOffset - item.Position.m_offset) *
                               BYTE_TO_FLOAT_SCALE * segLength;
            var methodDistance = item.MethodDistance + offsetLength;
            var comparisonValue = item.ComparisonValue + (offsetLength / (prevSpeed * maxLength_));
            var duration = item.Duration + offsetLength / prevMaxSpeed;
            var b = netManager.m_lanes.m_buffer[item.LaneId]
                              .CalculatePosition(connectOffset * BYTE_TO_FLOAT_SCALE);

            if (!ignoreCost_) {
                int ticketCost = netManager.m_lanes.m_buffer[item.LaneId].m_ticketCost;
                if (ticketCost != 0) {
                    comparisonValue += ticketCost * pathRandomizer_.Int32(2000u)
                                                  * BYTE_TO_FLOAT_SCALE * 0.0001f;
                }
            }

            uint laneIndex = 0;
#if DEBUG
            var wIter = 0;
#endif
            while (laneIndex < nextNumLanes && curLaneId != 0u) {
#if DEBUG
                ++wIter;
                if (wIter >= 20) {
                    Log.Error("Too many iterations in ProcessItem2!");
                    break;
                }
#endif

                if (nextLaneId == curLaneId) {
                    var nextLaneInfo = nextSegmentInfo.m_lanes[laneIndex];
                    if (!nextLaneInfo.CheckType(laneTypes_, vehicleTypes_)) {
                        return;
                    }

                    var a = netManager.m_lanes.m_buffer[nextLaneId].CalculatePosition(offset * BYTE_TO_FLOAT_SCALE);
                    var distance = Vector3.Distance(a, b);
                    BufferItem nextItem;

#if COUNTSEGMENTSTONEXTJUNCTION
                        // NON-STOCK CODE START //
                        if (prevIsRealJunction) {
                            nextItem.m_numSegmentsToNextJunction = 0;
                        } else {
                            nextItem.m_numSegmentsToNextJunction = item.m_numSegmentsToNextJunction + 1;
                        }
                        // NON-STOCK CODE END //
#endif
                    nextItem.Position.m_segment = nextSegmentId;
                    nextItem.Position.m_lane = (byte)laneIndex;
                    nextItem.Position.m_offset = offset;

                    if ((nextLaneInfo.m_laneType & prevLaneType) == NetInfo.LaneType.None) {
                        nextItem.MethodDistance = 0f;
                    } else {
                        nextItem.MethodDistance = methodDistance + distance;
                    }

                    var nextMaxSpeed = GetLaneSpeedLimit(
                        nextSegmentId,
                        (byte)laneIndex,
                        curLaneId,
                        nextLaneInfo); // NON-STOCK CODE

                    if (nextLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian &&
                        !(nextItem.MethodDistance < globalConf_.PathFinding.MaxWalkingDistance) &&
                        !stablePath_) {
                        return;
                    }

                    nextItem.ComparisonValue =
                        comparisonValue +
                        (distance / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * maxLength_));
                    nextItem.Duration =
                        duration + (distance / ((prevMaxSpeed + nextMaxSpeed) * 0.5f));

                    nextItem.Direction =
                        (nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None
                            ? NetInfo.InvertDirection(nextLaneInfo.m_finalDirection)
                            : nextLaneInfo.m_finalDirection;

                    if (nextLaneId == startLaneA_) {
                        if (((nextItem.Direction & NetInfo.Direction.Forward) == NetInfo.Direction.None ||
                             nextItem.Position.m_offset < startOffsetA_) &&
                            ((nextItem.Direction & NetInfo.Direction.Backward) == NetInfo.Direction.None ||
                             nextItem.Position.m_offset > startOffsetA_)) {
                            return;
                        }

                        var nextSpeed = CalculateLaneSpeed(
                            nextMaxSpeed,
                            startOffsetA_,
                            nextItem.Position.m_offset,
                            ref nextSegment,
                            nextLaneInfo); // NON-STOCK CODE
                        var nextOffsetDistance =
                            Mathf.Abs(nextItem.Position.m_offset - startOffsetA_) * BYTE_TO_FLOAT_SCALE;
                        nextItem.ComparisonValue +=
                            nextOffsetDistance * nextSegment.m_averageLength / (nextSpeed * maxLength_);
                        nextItem.Duration += nextOffsetDistance * nextSegment.m_averageLength / nextSpeed;
                    }

                    if (nextLaneId == startLaneB_) {
                        if (((nextItem.Direction & NetInfo.Direction.Forward) == NetInfo.Direction.None ||
                             nextItem.Position.m_offset < startOffsetB_) &&
                            ((nextItem.Direction & NetInfo.Direction.Backward) == NetInfo.Direction.None ||
                             nextItem.Position.m_offset > startOffsetB_)) {
                            return;
                        }

                        var nextSpeed = CalculateLaneSpeed(
                            nextMaxSpeed,
                            startOffsetB_,
                            nextItem.Position.m_offset,
                            ref nextSegment,
                            nextLaneInfo); // NON-STOCK CODE
                        var nextOffsetDistance =
                            Mathf.Abs(nextItem.Position.m_offset - startOffsetB_) * BYTE_TO_FLOAT_SCALE;
                        nextItem.ComparisonValue +=
                            nextOffsetDistance * nextSegment.m_averageLength / (nextSpeed * maxLength_);
                        nextItem.Duration += nextOffsetDistance * nextSegment.m_averageLength / nextSpeed;
                    }

                    nextItem.LaneId = nextLaneId;
                    nextItem.LanesUsed = item.LanesUsed | nextLaneInfo.m_laneType;
                    nextItem.VehiclesUsed = item.VehiclesUsed | nextLaneInfo.m_vehicleType;
                    nextItem.TrafficRand = 0;
#if DEBUGNEWPF
                    if (debug) {
                        m_debugPositions[item.Position.m_segment].Add(nextItem.Position.m_segment);
                    }
#endif
                    AddBufferItem(nextItem, item.Position);

                    return;
                }

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                ++laneIndex;
            }
        }

        private bool ProcessItemCosts(bool logLogic,
                                      BufferItem item,
                                      ushort nextNodeId,
                                      ushort nextSegmentId,
                                      ref NetSegment prevSegment, /*SegmentRoutingData prevSegmentRouting,*/
                                      ref NetSegment nextSegment,
                                      ref int laneIndexFromInner,
                                      byte connectOffset,
                                      bool enableVehicle,
                                      bool enablePedestrian,
                                      bool isMiddle) {
            var foundForced = false;
            return ProcessItemCosts(
                logLogic,
                true,
                LaneChangingCostCalculationMode.None,
                item,
                nextNodeId,
                nextSegmentId,
                ref prevSegment, /*prevSegmentRouting,*/
                ref nextSegment, /*routingManager.segmentRoutings[nextSegmentId],*/
                ref laneIndexFromInner,
                connectOffset,
                enableVehicle,
                enablePedestrian,
                null,
                null,
                null,
                null,
                null,
                isMiddle,
                out foundForced);
        }

        // 3
        private bool ProcessItemCosts(bool logLogic,
                                      bool obeyStockLaneArrows,
                                      LaneChangingCostCalculationMode laneChangingCostCalculationMode,
                                      BufferItem item,
                                      ushort nextNodeId,
                                      ushort nextSegmentId,
                                      ref NetSegment prevSegment, /* SegmentRoutingData prevSegmentRouting,*/
                                      ref NetSegment nextSegment, /*SegmentRoutingData nextSegmentRouting,*/
                                      ref int laneIndexFromInner,
                                      byte connectOffset,
                                      bool enableVehicle,
                                      bool enablePedestrian,
                                      int? forcedLaneIndex,
                                      uint? forcedLaneId,
                                      byte? forcedLaneDist,
                                      float? segmentSelectionCost,
                                      float? laneSelectionCost,
                                      bool isMiddle,
                                      out bool foundForced) {
#if DEBUGNEWPF && DEBUG
            logLogic = logLogic && DebugSwitch.RoutingBasicLog.Get();
#else
			logLogic = false;
            var oldPrevCost = 0f;
            var oldCustomDeltaCost = 0f;
#endif

            List<string> logBuf = null;
#if DEBUGNEWPF && DEBUG
            if (logLogic) {
                logBuf = new List<string>();
            }
#endif

            foundForced = false;
            var blocked = false;

            if ((nextSegment.m_flags & disableMask_) != NetSegment.Flags.None) {
                var nextSegFlags = nextSegment.m_flags;
                MemoryLog_FlushCost_If(
                    logLogic,
                    logBuf,
                    () => $"Segment is PathFailed or flooded: {nextSegFlags}\n" +
                    "-- method returns --");
                return false;
            }

            var netManager = Singleton<NetManager>.instance;

            var nextSegmentInfo = nextSegment.Info;
            var prevSegmentInfo = prevSegment.Info;
            var nextNumLanes = nextSegmentInfo.m_lanes.Length;
            var nextDir = nextNodeId != nextSegment.m_startNode
                              ? NetInfo.Direction.Forward
                              : NetInfo.Direction.Backward;
            var nextFinalDir = (nextSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None
                                   ? nextDir
                                   : NetInfo.InvertDirection(nextDir);
            var turningAngle = 1f;

            MemoryLog_NoFlush_If(
                logLogic,
                logBuf,
                () => $"isStockLaneChangerUsed={Options.isStockLaneChangerUsed()}, " +
                $"_extVehicleType={queueItem_.vehicleType}, " +
                $"nonBus={(queueItem_.vehicleType & ExtVehicleType.RoadVehicle & ~ExtVehicleType.Bus) != ExtVehicleType.None}, " +
                $"_stablePath={stablePath_}, enablePedestrian={enablePedestrian}, " +
                $"enableVehicle={enableVehicle}");

            var prevMaxSpeed = 1f;
            var prevLaneSpeed = 1f;
            var prevLaneType = NetInfo.LaneType.None;
            var prevVehicleType = VehicleInfo.VehicleType.None;

            // NON-STOCK CODE START //
            var nextIsStartNodeOfPrevSegment = prevSegment.m_startNode == nextNodeId;
            var sourceNodeId = nextIsStartNodeOfPrevSegment ? prevSegment.m_endNode : prevSegment.m_startNode;
            var prevIsJunction =
                (netManager.m_nodes.m_buffer[sourceNodeId].m_flags &
                 (NetNode.Flags.Junction | NetNode.Flags.Transition)) == NetNode.Flags.Junction;
#if COUNTSEGMENTSTONEXTJUNCTION
            bool prevIsRealJunction = prevIsJunction
            && (netManager.m_nodes.m_buffer[sourceNodeId].m_flags
            & (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)) !=
            (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut);
#endif
            // bool nextIsRealJunction =
            //    (netManager.m_nodes.m_buffer[nextNodeId].m_flags &
            //     (NetNode.Flags.Junction | NetNode.Flags.Transition)) == NetNode.Flags.Junction &&
            //    (netManager.m_nodes.m_buffer[nextNodeId].m_flags &
            //     (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)) !=
            //    (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut);

            var prevOuterSimilarLaneIndex = -1;

            // NON-STOCK CODE END //
            if (item.Position.m_lane < prevSegmentInfo.m_lanes.Length) {
                var prevLaneInfo = prevSegmentInfo.m_lanes[item.Position.m_lane];
                prevLaneType = prevLaneInfo.m_laneType;
                prevVehicleType = prevLaneInfo.m_vehicleType;

                prevMaxSpeed = GetLaneSpeedLimit(
                    item.Position.m_segment,
                    item.Position.m_lane,
                    item.LaneId,
                    prevLaneInfo); // NON-STOCK CODE

                prevLaneSpeed = CalculateLaneSpeed(
                    prevMaxSpeed,
                    connectOffset,
                    item.Position.m_offset,
                    ref prevSegment,
                    prevLaneInfo); // NON-STOCK CODE

                // NON-STOCK CODE START
                if ((byte)(prevLaneInfo.m_direction & NetInfo.Direction.Forward) != 0) {
                    prevOuterSimilarLaneIndex = prevLaneInfo.m_similarLaneCount - prevLaneInfo.m_similarLaneIndex - 1;
                } else {
                    prevOuterSimilarLaneIndex = prevLaneInfo.m_similarLaneIndex;
                }

                // NON-STOCK CODE END
            }

            if (prevLaneType == NetInfo.LaneType.Vehicle &&
                (prevVehicleType & VehicleInfo.VehicleType.Car) == VehicleInfo.VehicleType.None) {

                // check turning angle
                turningAngle = 0.01f - Mathf.Min(nextSegmentInfo.m_maxTurnAngleCos, prevSegmentInfo.m_maxTurnAngleCos);
                if (turningAngle < 1f) {
                    var prevDirection = nextNodeId == prevSegment.m_startNode
                                            ? prevSegment.m_startDirection
                                            : prevSegment.m_endDirection;

                    var nextDirection =
                        (nextDir & NetInfo.Direction.Forward) != NetInfo.Direction.None
                            ? nextSegment.m_endDirection
                            : nextSegment.m_startDirection;

                    var dirDotProd = (prevDirection.x * nextDirection.x) +
                                     (prevDirection.z * nextDirection.z);
                    if (dirDotProd >= turningAngle) {
                        MemoryLog_FlushCost_If(
                            logLogic,
                            logBuf,
                            () => $"turningAngle < 1f! dirDotProd={dirDotProd} >= " +
                            $"turningAngle{turningAngle}!\n" +
                            "-- method returns --");
                        return blocked;
                    }
                }
            }

            var prevDist = prevLaneType == NetInfo.LaneType.PublicTransport
                               ? netManager.m_lanes.m_buffer[item.LaneId].m_length
                               : Mathf.Max(SEGMENT_MIN_AVERAGE_LENGTH, prevSegment.m_averageLength);

            var prevCost = prevDist;

#if DEBUGNEWPF
            var oldPrevCost = prevCost;
#endif

            // NON-STOCK CODE START
            if (segmentSelectionCost != null) {
                prevCost *= (float)segmentSelectionCost;
            }

            if (laneSelectionCost != null) {
                prevCost *= (float)laneSelectionCost;
            }

            // NON-STOCK CODE END
            MemoryLog_NoFlush_If(
                logLogic,
                logBuf,
                () => $"item: seg. {item.Position.m_segment}, lane {item.Position.m_lane}, " +
                $"node {nextNodeId}:\n\tapplied traffic cost factors:\n\toldPrevCost={oldPrevCost}\n" +
                $"\t=> prevCost={prevCost}\n");

            // stock code check for vehicle ban policies removed
            // stock code for transport lane usage control removed

            // calculate ticket costs
            var ticketCosts = 0f;
            if (!ignoreCost_) {
                int ticketCost = netManager.m_lanes.m_buffer[item.LaneId].m_ticketCost;
                if (ticketCost != 0) {
                    ticketCosts += ticketCost * pathRandomizer_.Int32(2000u) * BYTE_TO_FLOAT_SCALE * 0.0001f;
                }
            }

            if ((prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                NetInfo.LaneType.None) {
                prevLaneType = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
            }

            var prevOffsetCost = Mathf.Abs(connectOffset - item.Position.m_offset) *
                                 BYTE_TO_FLOAT_SCALE * prevCost;
            var prevMethodDist = item.MethodDistance +
                                 (Mathf.Abs(connectOffset - item.Position.m_offset) *
                                  BYTE_TO_FLOAT_SCALE * prevDist);
            var prevDuration = item.Duration + (prevOffsetCost / prevMaxSpeed);

            // NON-STOCK: vehicle restriction are applied to previous segment length in MainPathFind (not here, and not to prevOffsetCost)
            var prevComparisonPlusOffsetCostOverSpeed =
                item.ComparisonValue + (prevOffsetCost / (prevLaneSpeed * maxLength_));

            if (!stablePath_) {
                // CO randomization. Only randomizes over segments, not over lanes.
                if (segmentSelectionCost == null) {
                    // NON-STOCK CODE
                    var randomizer = new Randomizer(pathFindIndex_ << 16 | item.Position.m_segment);
                    prevOffsetCost *=
                        ((randomizer.Int32(900, 1000 + (prevSegment.m_trafficDensity * 10)) +
                          pathRandomizer_.Int32(20u)) * 0.001f);
                }
            }

            var prevLaneConnectPos = netManager.m_lanes.m_buffer[item.LaneId]
                                               .CalculatePosition(connectOffset * BYTE_TO_FLOAT_SCALE);
            var newLaneIndexFromInner = laneIndexFromInner;
            var transitionNode = (netManager.m_nodes.m_buffer[nextNodeId].m_flags & NetNode.Flags.Transition) !=
                                 NetNode.Flags.None;
            var allowedLaneTypes = laneTypes_;
            var allowedVehicleTypes = vehicleTypes_;

            if (!enableVehicle) {
                allowedVehicleTypes &= VehicleInfo.VehicleType.Bicycle;
                if (allowedVehicleTypes == VehicleInfo.VehicleType.None) {
                    allowedLaneTypes &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
                }
            }

            if (!enablePedestrian) {
                allowedLaneTypes &= ~NetInfo.LaneType.Pedestrian;
            }

            MemoryLog_NoFlush_If(
                logLogic,
                logBuf,
                () => $"allowedVehicleTypes={allowedVehicleTypes} allowedLaneTypes={allowedLaneTypes}");

            // NON-STOCK CODE START //
            var laneChangeBaseCosts = 1f;
            var junctionBaseCosts = 1f;
            if (laneChangingCostCalculationMode != LaneChangingCostCalculationMode.None) {
                var rand = pathRandomizer_.Int32(101u) / 100f;
                laneChangeBaseCosts = globalConf_.AdvancedVehicleAI.LaneChangingBaseMinCost + (rand *
                                      (globalConf_.AdvancedVehicleAI.LaneChangingBaseMaxCost -
                                       globalConf_.AdvancedVehicleAI.LaneChangingBaseMinCost));
                if (prevIsJunction) {
                    junctionBaseCosts = globalConf_.AdvancedVehicleAI.LaneChangingJunctionBaseCost;
                }
            }

            // NON-STOCK CODE END
            var laneIndex =
                forcedLaneIndex != null
                    ? (uint)forcedLaneIndex
                    : 0u; // NON-STOCK CODE, forcedLaneIndex is not null if the next node is a (real) junction
            // NON-STOCK CODE, forceLaneId is not null if the next node is a (real) junction
            var curLaneId = forcedLaneId ?? nextSegment.m_lanes;

            while (laneIndex < nextNumLanes && curLaneId != 0u) {
                // NON-STOCK CODE START //
                if (forcedLaneIndex != null && laneIndex != forcedLaneIndex) {
                    MemoryLog_NoFlush_If(
                        logLogic,
                        logBuf,
                        () => $"forceLaneIndex break! laneIndex={laneIndex}");
                    break;
                }

                // NON-STOCK CODE END //
                var nextLaneInfo = nextSegmentInfo.m_lanes[laneIndex];

                if ((byte)(nextLaneInfo.m_finalDirection & nextFinalDir) != 0) {
                    // lane direction is compatible
                    MemoryLog_NoFlush_If(
                        logLogic,
                        logBuf,
                        () => $"Lane direction check passed: {nextLaneInfo.m_finalDirection}");

                    var nextLaneCheckType = nextLaneInfo.CheckType(allowedLaneTypes, allowedVehicleTypes);
                    var next1 = nextSegmentId != item.Position.m_segment
                                || laneIndex != (int)item.Position.m_lane;
                    if (nextLaneCheckType && next1) {
                        // vehicle types match and no u-turn to the previous lane
                        MemoryLog_NoFlush_If(
                            logLogic,
                            logBuf,
                            () => $"vehicle type check passed: {nextLaneCheckType} && {next1}");

                        // NON-STOCK CODE START
                        var nextMaxSpeed = GetLaneSpeedLimit(nextSegmentId, (byte)laneIndex, curLaneId, nextLaneInfo);
                        var customDeltaCost = 0f;

                        // NON-STOCK CODE END
                        var nextLaneEndPointPos = (nextDir & NetInfo.Direction.Forward) != NetInfo.Direction.None
                                                      ? netManager.m_lanes.m_buffer[curLaneId].m_bezier.d
                                                      : netManager.m_lanes.m_buffer[curLaneId].m_bezier.a;

                        var transitionCost = Vector3.Distance(
                            nextLaneEndPointPos,
                            prevLaneConnectPos); // This gives the distance of the previous to next lane endpoints.

                        MemoryLog_NoFlush_If(
                            logLogic,
                            logBuf,
                            () => $"costs from {nextSegmentId} (off " +
                            $"{(byte)((nextDir & NetInfo.Direction.Forward) == 0 ? 0 : 255)}) to " +
                            $"{item.Position.m_segment} (off {item.Position.m_offset}), " +
                            $"connectOffset={connectOffset}: transitionCost={transitionCost}");

                        if (transitionNode) {
                            transitionCost *= 2f;
                        }

                        BufferItem nextItem;
#if COUNTSEGMENTSTONEXTJUNCTION
                        // NON-STOCK CODE START
                        if (prevIsRealJunction) {
                            nextItem.m_numSegmentsToNextJunction = 0;
                        } else {
                            nextItem.m_numSegmentsToNextJunction = item.m_numSegmentsToNextJunction + 1;
                        }
                        // NON-STOCK CODE END
#endif
                        nextItem.ComparisonValue = ticketCosts;
                        nextItem.Position.m_segment = nextSegmentId;
                        nextItem.Position.m_lane = (byte)laneIndex;
                        nextItem.Position.m_offset = (byte)((nextDir & NetInfo.Direction.Forward) == 0 ? 0 : 255);
                        if ((nextLaneInfo.m_laneType & prevLaneType) == NetInfo.LaneType.None) {
                            nextItem.MethodDistance = 0f;

                            // NON-STOCK CODE START
                            if (Options.realisticPublicTransport && isMiddle &&
                                nextLaneInfo.m_laneType == NetInfo.LaneType.PublicTransport &&
                                (item.LanesUsed & NetInfo.LaneType.PublicTransport) != NetInfo.LaneType.None) {
                                // apply penalty when switching public transport vehicles
                                var transportTransitionPenalty =
                                    (globalConf_.PathFinding.PublicTransportTransitionMinPenalty +
                                     (netManager.m_nodes.m_buffer[nextNodeId].m_maxWaitTime *
                                      BYTE_TO_FLOAT_SCALE *
                                      (globalConf_.PathFinding.PublicTransportTransitionMaxPenalty -
                                       globalConf_.PathFinding.PublicTransportTransitionMinPenalty))) /
                                    (0.25f * maxLength_);
                                MemoryLog_NoFlush_If(
                                    logLogic,
                                    logBuf,
                                    () => "applying public transport transition penalty: " +
                                    $"{transportTransitionPenalty}");
                                nextItem.ComparisonValue += transportTransitionPenalty;
                            }

                            // NON-STOCK CODE END
                        } else {
                            nextItem.MethodDistance = prevMethodDist + transitionCost;
                        }

                        var nextLaneNotPed = nextLaneInfo.m_laneType != NetInfo.LaneType.Pedestrian;
                        var closeToWalk = nextItem.MethodDistance < globalConf_.PathFinding.MaxWalkingDistance;
                        MemoryLog_NoFlush_If(
                            logLogic,
                            logBuf,
                            () => "checking if methodDistance is in range: " +
                            $"{nextLaneNotPed} || {closeToWalk} ({nextItem.MethodDistance})");

                        if (nextLaneNotPed || closeToWalk || stablePath_) {
                            // NON-STOCK CODE START //
                            if (laneChangingCostCalculationMode == LaneChangingCostCalculationMode.None) {
                                var transitionCostOverMeanMaxSpeed =
                                    transitionCost / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * maxLength_);
                                nextItem.ComparisonValue +=
                                    prevComparisonPlusOffsetCostOverSpeed +
                                    transitionCostOverMeanMaxSpeed; // stock code
                            } else {
                                nextItem.ComparisonValue += item.ComparisonValue;

                                // customDeltaCost now holds the costs for driving on the segment + costs
                                // for changing the segment
                                customDeltaCost = transitionCost + prevOffsetCost;
                                MemoryLog_NoFlush_If(
                                    logLogic,
                                    logBuf,
                                    () => $"Path from {nextSegmentId} (idx {laneIndex}, id {curLaneId}) " +
                                    $"to {item.Position.m_segment} (lane {prevOuterSimilarLaneIndex} " +
                                    $"from outer, idx {item.Position.m_lane}): " +
                                    $"laneChangingCostCalculationMode={laneChangingCostCalculationMode}, " +
                                    $"transitionCost={transitionCost}");
                            }

                            nextItem.Duration =
                                prevDuration + (transitionCost / ((prevMaxSpeed + nextMaxSpeed) * 0.5f));

                            // account for public tranport transition costs on non-PT paths
                            if (
#if DEBUG
                                !DebugSwitch.RealisticPublicTransport.Get() &&
#endif
                                Options.realisticPublicTransport &&
                                (curLaneId == startLaneA_ || curLaneId == startLaneB_) &&
                                (item.LanesUsed & (NetInfo.LaneType.Pedestrian | NetInfo.LaneType.PublicTransport)) ==
                                NetInfo.LaneType.Pedestrian) {
                                var transportTransitionPenalty =
                                    (2f * globalConf_.PathFinding.PublicTransportTransitionMaxPenalty) /
                                    (0.25f * maxLength_);
                                MemoryLog_NoFlush_If(
                                    logLogic,
                                    logBuf,
                                    () => "applying public transport transition penalty on non-PT path: " +
                                    $"{transportTransitionPenalty}");
                                nextItem.ComparisonValue += transportTransitionPenalty;
                            }

                            // NON-STOCK CODE END
                            nextItem.Direction = nextDir;
                            if (curLaneId == startLaneA_) {
                                if (((byte)(nextItem.Direction & NetInfo.Direction.Forward) == 0 ||
                                     nextItem.Position.m_offset < startOffsetA_) &&
                                    ((byte)(nextItem.Direction & NetInfo.Direction.Backward) == 0 ||
                                     nextItem.Position.m_offset > startOffsetA_)) {
                                    MemoryLog_NoFlush_If(
                                        logLogic,
                                        logBuf,
                                        () => "Current lane is start lane A. goto next lane");
                                    goto CONTINUE_LANE_LOOP;
                                }

                                var nextLaneSpeed = CalculateLaneSpeed(
                                    nextMaxSpeed,
                                    startOffsetA_,
                                    nextItem.Position.m_offset,
                                    ref nextSegment,
                                    nextLaneInfo); // NON-STOCK CODE

                                var nextOffset = Mathf.Abs(nextItem.Position.m_offset - startOffsetA_) *
                                                 BYTE_TO_FLOAT_SCALE;
                                var nextSegLength = Mathf.Max(SEGMENT_MIN_AVERAGE_LENGTH,
                                                              nextSegment.m_averageLength);
                                nextItem.ComparisonValue +=
                                    nextOffset * nextSegLength / (nextLaneSpeed * maxLength_);
                                nextItem.Duration += nextOffset * nextSegLength / nextLaneSpeed;
                            }

                            if (curLaneId == startLaneB_) {
                                if (((byte)(nextItem.Direction & NetInfo.Direction.Forward) == 0 ||
                                     nextItem.Position.m_offset < startOffsetB_) &&
                                    ((byte)(nextItem.Direction & NetInfo.Direction.Backward) == 0 ||
                                     nextItem.Position.m_offset > startOffsetB_)) {
                                    MemoryLog_NoFlush_If(
                                        logLogic,
                                        logBuf,
                                        () => "Current lane is start lane B. goto next lane");
                                    goto CONTINUE_LANE_LOOP;
                                }

                                var nextLaneSpeed = CalculateLaneSpeed(
                                    nextMaxSpeed,
                                    startOffsetB_,
                                    nextItem.Position.m_offset,
                                    ref nextSegment,
                                    nextLaneInfo); // NON-STOCK CODE
                                var nextOffset = Mathf.Abs(nextItem.Position.m_offset - startOffsetB_) *
                                                 BYTE_TO_FLOAT_SCALE;
                                var nextSegLength = Mathf.Max(SEGMENT_MIN_AVERAGE_LENGTH, nextSegment.m_averageLength);
                                nextItem.ComparisonValue +=
                                    nextOffset * nextSegLength / (nextLaneSpeed * maxLength_);
                                nextItem.Duration += nextOffset * nextSegLength / nextLaneSpeed;
                            }

                            if (!ignoreBlocked_ &&
                                (nextSegment.m_flags & NetSegment.Flags.Blocked) != NetSegment.Flags.None &&
                                (byte)(nextLaneInfo.m_laneType &
                                       (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
                                // NON-STOCK CODE START //
                                if (laneChangingCostCalculationMode != LaneChangingCostCalculationMode.None) {
#if DEBUGNEWPF
                                    var oldCustomDeltaCost = customDeltaCost;
#endif

                                    // apply vanilla game restriction policies
                                    customDeltaCost *= 10f;
                                    MemoryLog_NoFlush_If(
                                        logLogic,
                                        logBuf,
                                        () => $"item: seg. {item.Position.m_segment}, " +
                                        $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                        "\tapplied blocked road cost factor on activated AI:\n" +
                                        $"\toldCustomDeltaCost={oldCustomDeltaCost}\n" +
                                        $"\t=> customDeltaCost={customDeltaCost}\n");
                                } else {
                                    // NON-STOCK CODE END //
                                    MemoryLog_NoFlush_If(
                                        logLogic,
                                        logBuf,
                                        () => "Applying blocked road cost factor on disabled advanced AI");
                                    nextItem.ComparisonValue += 0.1f;
                                }

                                blocked = true;
                            }

                            if ((byte)(nextLaneInfo.m_laneType & prevLaneType) != 0 &&
                                nextLaneInfo.m_vehicleType == prevVehicleType) {
                                MemoryLog_NoFlush_If(
                                    logLogic,
                                    logBuf,
                                    () => "Applying stock lane changing costs. " +
                                    $"obeyStockLaneArrows={obeyStockLaneArrows}");

                                if (obeyStockLaneArrows) {
                                    // this is CO's way of matching lanes between segments
                                    int firstTarget = netManager.m_lanes.m_buffer[curLaneId].m_firstTarget;
                                    int lastTarget = netManager.m_lanes.m_buffer[curLaneId].m_lastTarget;
                                    if (laneIndexFromInner < firstTarget || laneIndexFromInner >= lastTarget) {
                                        nextItem.ComparisonValue +=
                                            Mathf.Max(1f, (transitionCost * 3f) - 3f) /
                                            ((prevMaxSpeed + nextMaxSpeed) * 0.5f * maxLength_);
                                    }
                                } // NON-STOCK CODE

                                // stock code that prohibits cars to be on public transport lanes removed
                                /*if (!this._transportVehicle && nextLaneInfo.m_laneType == NetInfo.LaneType.TransportVehicle) {
                                        nextItem.m_comparisonValue += 20f / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength);
                                }*/
                            }

                            // NON-STOCK CODE START //
                            var addItem = true; // should we add the next item to the buffer?
                            if (laneChangingCostCalculationMode != LaneChangingCostCalculationMode.None) {
                                // Advanced AI cost calculation
                                MemoryLog_NoFlush_If(
                                    logLogic,
                                    logBuf,
                                    () => "Calculating advanced AI lane changing costs");

                                int laneDist; // absolute lane distance
                                if (laneChangingCostCalculationMode ==
                                    LaneChangingCostCalculationMode.ByGivenDistance && forcedLaneDist != null) {
                                    laneDist = (byte)forcedLaneDist;
                                } else {
                                    int nextOuterSimilarLaneIndex;
                                    if ((byte)(nextLaneInfo.m_direction & NetInfo.Direction.Forward) != 0) {
                                        nextOuterSimilarLaneIndex =
                                            nextLaneInfo.m_similarLaneCount - nextLaneInfo.m_similarLaneIndex - 1;
                                    } else {
                                        nextOuterSimilarLaneIndex = nextLaneInfo.m_similarLaneIndex;
                                    }

                                    var relLaneDist =
                                        nextOuterSimilarLaneIndex -
                                        prevOuterSimilarLaneIndex; // relative lane distance (positive: change to more outer lane, negative: change to more inner lane)
                                    laneDist = Math.Abs(relLaneDist);
                                }

                                // apply lane changing costs
                                var laneMetric = 1f;
                                var relaxedLaneChanging =
                                    queueItem_.vehicleId == 0 &&
                                    (curLaneId == startLaneA_ || curLaneId == startLaneB_);
                                if (laneDist > 0 && !relaxedLaneChanging) {
                                    laneMetric = 1f + (laneDist *
                                                 junctionBaseCosts *
                                                 laneChangeBaseCosts * // road type based lane changing cost factor
                                                 (laneDist > 1
                                                      ? globalConf_.AdvancedVehicleAI.MoreThanOneLaneChangingCostFactor
                                                      : 1f)); // additional costs for changing multiple lanes at once
                                }

                                // on highways: avoid lane changing before junctions: multiply with inverted distance to next junction
                                /*float junctionMetric = 1f;
                                if (prevSegmentRouting.highway && nextSegmentRouting.highway && // we are on a highway road
                                        !nextIsRealJunction && // next is not a junction
                                        laneDist > 0) {
                                        uint dist = _pathRandomizer.UInt32(_conf.MinHighwayInterchangeSegments, (_conf.MaxHighwayInterchangeSegments >= _conf.MinHighwayInterchangeSegments ? _conf.MaxHighwayInterchangeSegments : _conf.MinHighwayInterchangeSegments) + 1u);
                                        if (nextItem.m_numSegmentsToNextJunction < dist) {
                                                junctionMetric = _conf.HighwayInterchangeLaneChangingBaseCost * (float)(dist - nextItem.m_numSegmentsToNextJunction);
                                        }
                                }*/

                                // total metric value
                                var metric = laneMetric /* * junctionMetric*/;

                                // float oldTransitionDistanceOverMaxSpeed = transitionCostOverMeanMaxSpeed;
                                var finalDeltaCost = (metric * customDeltaCost) /
                                                     ((prevMaxSpeed + nextMaxSpeed) * 0.5f * maxLength_);
//
//                                if (finalDeltaCost < 0f) {
//                                    // should never happen
//#if DEBUG
//                                    Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId}, PF {this._pathFindIndex}: distanceOverMeanMaxSpeed < 0! seg. {nextSegmentId}, lane {laneIndex}, off {nextItem.m_position.m_offset} -> {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}. distanceOverMeanMaxSpeed={finalDeltaCost}, prevSpeed={prevUsage}"/* + ", prevSpeed={prevSpeed}"*/);
//#endif
//                                    finalDeltaCost = 0f;
//                                } else if (Single.IsNaN(finalDeltaCost) || Single.IsInfinity(finalDeltaCost)) {
//                                    // Fallback if we mess something up. Should never happen.
//#if DEBUG
//                                    //if (costDebug)
//                                    Log.Error($"Pathfinder ({this._pathFindIndex}): distanceOverMeanMaxSpeed is NaN or Infinity: seg. {nextSegmentId}, lane {laneIndex}, off {nextItem.m_position.m_offset} -> {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}. {finalDeltaCost} // nextMaxSpeed={nextMaxSpeed} prevMaxSpeed={prevMaxSpeed} nextMaxSpeed={nextMaxSpeed} laneDist={laneDist} laneMetric={laneMetric} metric={metric}");
//#endif
//#if DEBUGNEWPF
//                                    Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId}, PF {this._pathFindIndex}: deltaCostOverMeanMaxSpeed is NaN! deltaCostOverMeanMaxSpeed={finalDeltaCost}");
//#endif
//                                    finalDeltaCost = oldTransitionDistanceOverMaxSpeed;
//                                }

                                nextItem.ComparisonValue += finalDeltaCost;

                                if (nextItem.ComparisonValue > 1f) {
                                    // comparison value got too big. Do not add the lane to the buffer
                                    addItem = false;
                                }

                                MemoryLog_NoFlush_If(
                                    logLogic,
                                    logBuf,
                                    () => $"item: seg. {item.Position.m_segment}, " +
                                    $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                    $"-> TRANSIT to seg. {nextSegmentId}, lane {laneIndex}\n" +
                                    $"\tprevMaxSpeed={prevMaxSpeed}\n" +
                                    $"\tnextMaxSpeed={nextMaxSpeed}\n\n" +
                                    $"\tlaneChangingCostCalculationMode={laneChangingCostCalculationMode}\n\n" +
                                    $"\tlaneDist={laneDist}\n\n" +
                                    $"\t_extVehicleType={queueItem_.vehicleType}\n" +
                                    $"\tlaneChangeRoadBaseCost={laneChangeBaseCosts}\n" +
                                    $"\tmoreThanOneLaneCost={(laneDist > 1 ? globalConf_.AdvancedVehicleAI.MoreThanOneLaneChangingCostFactor : 1f)}\n" +
                                    $"\t=> laneMetric={laneMetric}\n" +
                                    $"\t=> metric={metric}\n" +
                                    $"\tdeltaCostOverMeanMaxSpeed={finalDeltaCost}\n" +
                                    $"\tnextItem.m_comparisonValue={nextItem.ComparisonValue}\n\n" +
                                    $"\t=> addItem={addItem}\n");
                            }

                            if (forcedLaneIndex != null && laneIndex == forcedLaneIndex && addItem) {
                                foundForced = true;
                            }

                            if (addItem) {
                                // NON-STOCK CODE END
                                nextItem.LanesUsed = item.LanesUsed | nextLaneInfo.m_laneType;
                                nextItem.VehiclesUsed = item.VehiclesUsed | nextLaneInfo.m_vehicleType;
                                nextItem.LaneId = curLaneId;
                                nextItem.TrafficRand = item.TrafficRand;
#if DEBUGNEWPF
                                if (logLogic) {
                                    logBuf.Add(
                                        $"adding item: seg {nextItem.Position.m_segment}, " +
                                        $"lane {nextItem.Position.m_lane} (idx {nextItem.LaneId}), " +
                                        $"off {nextItem.Position.m_offset} -> seg {item.Position.m_segment}, " +
                                        $"lane {item.Position.m_lane} (idx {item.LaneId}), " +
                                        $"off {item.Position.m_offset}, cost {nextItem.ComparisonValue}, " +
                                        $"previous cost {item.ComparisonValue}, " +
                                        $"methodDist {nextItem.MethodDistance}");
                                    m_debugPositions[item.Position.m_segment].Add(nextItem.Position.m_segment);
                                }
#endif

                                AddBufferItem(nextItem, item.Position);

                                // NON-STOCK CODE START
                            } else {
#if DEBUGNEWPF
                                if (logLogic) {
                                    logBuf.Add(
                                        $"item: seg. {item.Position.m_segment}, " +
                                        $"lane {item.Position.m_lane}, node {nextNodeId}:\n" +
                                        $"-> item seg. {nextSegmentId}, lane {laneIndex} NOT ADDED\n");
                                }
#endif
                            }

                            // NON-STOCK CODE END //
                        }
                    }

                    goto CONTINUE_LANE_LOOP;
                }

                if ((byte)(nextLaneInfo.m_laneType & prevLaneType) != 0 &&
                    (nextLaneInfo.m_vehicleType & prevVehicleType) != VehicleInfo.VehicleType.None) {
                    ++newLaneIndexFromInner;
                }

                CONTINUE_LANE_LOOP:

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                ++laneIndex;
                continue;
            } // foreach lane

            laneIndexFromInner = newLaneIndexFromInner;

            MemoryLog_FlushCost_If(logLogic, logBuf, () => "-- method returns --");
            return blocked;
        }

#if DEBUGNEWPF
        private void FlushCostLog(List<string> logBuf) {
            if (logBuf == null) {
                return;
            }

            foreach (var toLog in logBuf) {
                Log._Debug($"Pathfinder ({pathFindIndex_}) for unit {Calculating} *COSTS*: " + toLog);
            }

            logBuf.Clear();
        }

        [UsedImplicitly]
        private void FlushMainLog(List<string> logBuf, uint unitId) {
            if (logBuf == null) {
                return;
            }

            foreach (var toLog in logBuf) {
                Log._Debug($"Pathfinder ({pathFindIndex_}) for unit {Calculating} *MAIN*: " + toLog);
            }

            logBuf.Clear();
        }
#endif

        // 4
        private void ProcessItemPedBicycle(bool debug,
                                           BufferItem item,
                                           ushort nextNodeId,
                                           ushort nextSegmentId,
                                           ref NetSegment prevSegment,
                                           ref NetSegment nextSegment,
                                           byte connectOffset,
                                           byte laneSwitchOffset,
                                           int nextLaneIndex,
                                           uint nextLaneId) {
#if DEBUGNEWPF && DEBUG
            List<string> logBuf = null;
            if (debug) {
                logBuf = new List<string>();
                logBuf.Add(
                    $"*PED* item: seg. {item.Position.m_segment}, lane {item.Position.m_lane}, " +
                    $"node {nextNodeId}: exploring\n\tnextSegmentId={nextSegmentId}\n" +
                    $"\tconnectOffset={connectOffset}\n\tlaneSwitchOffset={laneSwitchOffset}\n" +
                    $"\tnextLaneIndex={nextLaneIndex}\n\tnextLaneId={nextLaneId}\n");
            }
#endif

            if ((nextSegment.m_flags & disableMask_) != NetSegment.Flags.None) {
#if DEBUGNEWPF
                if (debug) {
                    logBuf.Add(
                        $"*PED* item: seg. {item.Position.m_segment}, lane {item.Position.m_lane}, " +
                        $"node {nextNodeId}: -NOT ADDING- next segment disabled mask is incompatible!\n" +
                        $"\tnextSegment.m_flags={nextSegment.m_flags}\n\t_disableMask={disableMask_}\n");
                    FlushCostLog(logBuf);
                }
#endif
                return;
            }

            var netManager = Singleton<NetManager>.instance;

            // NON-STOCK CODE START
            var nextIsRegularNode = nextNodeId == nextSegment.m_startNode
                                    || nextNodeId == nextSegment.m_endNode;
#if COUNTSEGMENTSTONEXTJUNCTION
            bool prevIsRealJunction = false;
#endif
            if (nextIsRegularNode) {
                var nextIsStartNode = nextNodeId == nextSegment.m_startNode;
                var prevNodeId = nextNodeId == prevSegment.m_startNode
                                     ? prevSegment.m_endNode
                                     : prevSegment.m_startNode;
#if COUNTSEGMENTSTONEXTJUNCTION
                prevIsRealJunction =
 (netManager.m_nodes.m_buffer[prevNodeId].m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) != NetNode.Flags.Junction &&
                                     (netManager.m_nodes.m_buffer[prevNodeId].m_flags & (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)) != (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut);
#endif

                // check if pedestrians are not allowed to cross here
                if (!JunctionManager.IsPedestrianCrossingAllowed(nextSegmentId, nextIsStartNode)) {
#if DEBUGNEWPF
                    if (debug) {
                        logBuf.Add(
                            $"*PED* item: seg. {item.Position.m_segment}, lane {item.Position.m_lane}, " +
                            $"node {nextNodeId}: -NOT ADDING- pedestrian crossing not allowed!\n");
                        FlushCostLog(logBuf);
                    }
#endif
                    return;
                }

                // check if pedestrian light won't change to green
                var lights = CustomTrafficLightsManager.GetSegmentLights(nextSegmentId, nextIsStartNode, false);
                if (lights != null) {
                    if (lights.InvalidPedestrianLight) {
#if DEBUGNEWPF
                        if (debug) {
                            logBuf.Add(
                                $"*PED* item: seg. {item.Position.m_segment}, " +
                                $"lane {item.Position.m_lane}, node {nextNodeId}: " +
                                "-NOT ADDING- invalid pedestrian lights!\n");
                            FlushCostLog(logBuf);
                        }
#endif
                        return;
                    }
                }
            }

            // NON-STOCK CODE END
            // NON-STOCK CODE START
            /*if (!_allowEscapeTransport) {
                    ushort transportLineId = netManager.m_nodes.m_buffer[targetNodeId].m_transportLine;
                    if (transportLineId != 0 && Singleton<TransportManager>.instance.m_lines.m_buffer[transportLineId].Info.m_transportType == TransportInfo.TransportType.EvacuationBus)
                            return;
            }*/
            // NON-STOCK CODE END

            var nextSegmentInfo = nextSegment.Info;
            var prevSegmentInfo = prevSegment.Info;
            var numLanes = nextSegmentInfo.m_lanes.Length;
            float distance;
            byte offset;
            var b = netManager.m_lanes.m_buffer[item.LaneId]
                              .CalculatePosition(laneSwitchOffset * BYTE_TO_FLOAT_SCALE);
            if (nextSegmentId == item.Position.m_segment) {
                // next segment is previous segment
                var a = netManager.m_lanes.m_buffer[nextLaneId].CalculatePosition(connectOffset * BYTE_TO_FLOAT_SCALE);
                distance = Vector3.Distance(a, b);
                offset = connectOffset;
            } else {
                // next segment differs from previous segment
                var direction = nextNodeId != nextSegment.m_startNode
                                    ? NetInfo.Direction.Forward
                                    : NetInfo.Direction.Backward;
                Vector3 a;
                if ((byte)(direction & NetInfo.Direction.Forward) != 0) {
                    a = netManager.m_lanes.m_buffer[nextLaneId].m_bezier.d;
                } else {
                    a = netManager.m_lanes.m_buffer[nextLaneId].m_bezier.a;
                }

                distance = Vector3.Distance(a, b);
                offset = (byte)((direction & NetInfo.Direction.Forward) == 0 ? 0 : 255);
            }

            var prevMaxSpeed = 1f;
            var prevSpeed = 1f;
            var laneType = NetInfo.LaneType.None;
            var vehicleType = VehicleInfo.VehicleType.None; // NON-STOCK CODE
            if (item.Position.m_lane < prevSegmentInfo.m_lanes.Length) {
                var prevLaneInfo = prevSegmentInfo.m_lanes[item.Position.m_lane];
                prevMaxSpeed = GetLaneSpeedLimit(
                    item.Position.m_segment,
                    item.Position.m_lane,
                    item.LaneId,
                    prevLaneInfo); // SpeedLimitManager.GetLockFreeGameSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, ref lane2); // NON-STOCK CODE
                laneType = prevLaneInfo.m_laneType;
                vehicleType = prevLaneInfo.m_vehicleType; // NON-STOCK CODE
                if ((byte)(laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
                    laneType = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
                }

                prevSpeed = CalculateLaneSpeed(
                    prevMaxSpeed,
                    laneSwitchOffset,
                    item.Position.m_offset,
                    ref prevSegment,
                    prevLaneInfo); // NON-STOCK CODE
            }

            float segLength;
            if (laneType == NetInfo.LaneType.PublicTransport) {
                segLength = netManager.m_lanes.m_buffer[item.LaneId].m_length;
            } else {
                segLength = Mathf.Max(SEGMENT_MIN_AVERAGE_LENGTH, prevSegment.m_averageLength);
            }

            var offsetLength = Mathf.Abs(laneSwitchOffset - item.Position.m_offset) * BYTE_TO_FLOAT_SCALE * segLength;
            var methodDistance = item.MethodDistance + offsetLength;
            var comparisonValue = item.ComparisonValue + (offsetLength / (prevSpeed * maxLength_));
            var duration = (item.Duration + offsetLength / prevMaxSpeed);

            if (!ignoreCost_) {
                int ticketCost = netManager.m_lanes.m_buffer[item.LaneId].m_ticketCost;
                if (ticketCost != 0) {
                    comparisonValue += ticketCost * pathRandomizer_.Int32(2000u) * BYTE_TO_FLOAT_SCALE * 0.0001f;
                }
            }

            if (nextLaneIndex < numLanes) {
                var nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];
                BufferItem nextItem;
#if COUNTSEGMENTSTONEXTJUNCTION
                // NON-STOCK CODE START //
                if (prevIsRealJunction) {
                    nextItem.m_numSegmentsToNextJunction = 0;
                } else {
                    nextItem.m_numSegmentsToNextJunction = item.m_numSegmentsToNextJunction + 1;
                }
                // NON-STOCK CODE END //
#endif
                nextItem.Position.m_segment = nextSegmentId;
                nextItem.Position.m_lane = (byte)nextLaneIndex;
                nextItem.Position.m_offset = offset;
                if ((nextLaneInfo.m_laneType & laneType) == NetInfo.LaneType.None) {
                    nextItem.MethodDistance = 0f;
                } else {
                    // Use tolerance instead of comparing to 0f
                    if (Math.Abs(item.MethodDistance) < 0.001f) {
                        comparisonValue += 100f / (0.25f * maxLength_);
                    }

                    nextItem.MethodDistance = methodDistance + distance;
                }

                var nextMaxSpeed = GetLaneSpeedLimit(
                    nextSegmentId,
                    (byte)nextLaneIndex,
                    nextLaneId,
                    nextLaneInfo); // NON-STOCK CODE

                if (nextLaneInfo.m_laneType != NetInfo.LaneType.Pedestrian ||
                    nextItem.MethodDistance < globalConf_.PathFinding.MaxWalkingDistance || stablePath_) {
                    nextItem.ComparisonValue =
                        comparisonValue + (distance / ((prevMaxSpeed + nextMaxSpeed) * 0.25f * maxLength_));
                    nextItem.Duration = duration + (distance / ((prevMaxSpeed + nextMaxSpeed) * 0.5f));

                    nextItem.Direction = (nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None
                                               ? NetInfo.InvertDirection(nextLaneInfo.m_finalDirection)
                                               : nextLaneInfo.m_finalDirection;

                    if (nextLaneId == startLaneA_) {
                        if (((byte)(nextItem.Direction & NetInfo.Direction.Forward) == 0 ||
                             nextItem.Position.m_offset < startOffsetA_) &&
                            ((byte)(nextItem.Direction & NetInfo.Direction.Backward) == 0 ||
                             nextItem.Position.m_offset > startOffsetA_)) {
#if DEBUGNEWPF
                            if (debug) {
                                logBuf.Add(
                                    $"*PED* item: seg. {item.Position.m_segment}, " +
                                    $"lane {item.Position.m_lane}, node {nextNodeId}: " +
                                    "-NOT ADDING- start lane A reached in wrong direction!\n");
                                FlushCostLog(logBuf);
                            }
#endif
                            return;
                        }

                        var nextSpeed = CalculateLaneSpeed(
                            nextMaxSpeed,
                            startOffsetA_,
                            nextItem.Position.m_offset,
                            ref nextSegment,
                            nextLaneInfo); // NON-STOCK CODE
                        var nextOffset = Mathf.Abs(nextItem.Position.m_offset - startOffsetA_) * BYTE_TO_FLOAT_SCALE;
                        nextItem.ComparisonValue +=
                            nextOffset * nextSegment.m_averageLength / (nextSpeed * maxLength_);
                        nextItem.Duration += nextOffset * nextSegment.m_averageLength / nextSpeed;
                    }

                    if (nextLaneId == startLaneB_) {
                        if (((byte)(nextItem.Direction & NetInfo.Direction.Forward) == 0
                             || nextItem.Position.m_offset < startOffsetB_)
                            && ((byte)(nextItem.Direction & NetInfo.Direction.Backward) == 0
                                || nextItem.Position.m_offset > startOffsetB_)) {
#if DEBUGNEWPF
                            if (debug) {
                                logBuf.Add(
                                    $"*PED* item: seg. {item.Position.m_segment}, " +
                                    $"lane {item.Position.m_lane}, node {nextNodeId}: -NOT ADDING- " +
                                    "start lane B reached in wrong direction!\n");
                                FlushCostLog(logBuf);
                            }
#endif
                            return;
                        }

                        var nextSpeed = CalculateLaneSpeed(
                            nextMaxSpeed,
                            startOffsetB_,
                            nextItem.Position.m_offset,
                            ref nextSegment,
                            nextLaneInfo); // NON-STOCK CODE
                        var nextOffset = Mathf.Abs(nextItem.Position.m_offset - startOffsetB_) * BYTE_TO_FLOAT_SCALE;
                        nextItem.ComparisonValue +=
                            nextOffset * nextSegment.m_averageLength / (nextSpeed * maxLength_);
                        nextItem.Duration += nextOffset * nextSegment.m_averageLength / nextSpeed;
                    }

                    nextItem.LaneId = nextLaneId;
                    nextItem.LanesUsed = item.LanesUsed | nextLaneInfo.m_laneType;
                    nextItem.VehiclesUsed = item.VehiclesUsed | nextLaneInfo.m_vehicleType;
                    nextItem.TrafficRand = 0;
#if DEBUGNEWPF
                    if (debug) {
                        logBuf.Add(
                            $"*PED* item: seg. {item.Position.m_segment}, lane {item.Position.m_lane}, " +
                            $"node {nextNodeId}: *ADDING*\n\tnextItem.m_laneID={nextItem.LaneId}\n" +
                            $"\tnextItem.m_lanesUsed={nextItem.LanesUsed}\n" +
                            $"\tnextItem.m_vehiclesUsed={nextItem.VehiclesUsed}\n" +
                            $"\tnextItem.m_comparisonValue={nextItem.ComparisonValue}\n" +
                            $"\tnextItem.m_methodDistance={nextItem.MethodDistance}\n");
                        FlushCostLog(logBuf);

                        m_debugPositions[item.Position.m_segment].Add(nextItem.Position.m_segment);
                    }
#endif
                    AddBufferItem(nextItem, item.Position);
                } else {
#if DEBUGNEWPF
                    if (debug) {
                        logBuf.Add(
                            $"*PED* item: seg. {item.Position.m_segment}, lane {item.Position.m_lane}, " +
                            $"node {nextNodeId}: -NOT ADDING- lane incompatible or method " +
                            "distance too large!\n\tnextItem.m_methodDistance" +
                            $"={nextItem.MethodDistance}\n\tnextLaneInfo.m_laneType" +
                            $"={nextLaneInfo.m_laneType}\n");
                        FlushCostLog(logBuf);
                    }
#endif
                }
            } else {
#if DEBUGNEWPF
                if (debug) {
                    logBuf.Add(
                        $"*PED* item: seg. {item.Position.m_segment}, lane {item.Position.m_lane}, " +
                        $"node {nextNodeId}: -NOT ADDING- nextLaneIndex >= numLanes ({nextLaneIndex} " +
                        $">= {numLanes})!\n");
                    FlushCostLog(logBuf);
                }
#endif
            }
        }

        private float CalculateLaneSpeed(float speedLimit,
                                         byte startOffset,
                                         byte endOffset,
                                         ref NetSegment segment,
                                         NetInfo.Lane laneInfo) {
            /*if ((laneInfo.m_vehicleType & (VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram)) != VehicleInfo.VehicleType.None)
                    speedLimit = laneInfo.m_speedLimit;
            */
            var direction = (segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None
                                ? laneInfo.m_finalDirection
                                : NetInfo.InvertDirection(laneInfo.m_finalDirection);
            if ((byte)(direction & NetInfo.Direction.Avoid) == 0) {
                return speedLimit;
            }

            if (endOffset > startOffset && direction == NetInfo.Direction.AvoidForward) {
                return speedLimit * 0.1f;
            }

            if (endOffset < startOffset && direction == NetInfo.Direction.AvoidBackward) {
                return speedLimit * 0.1f;
            }

            return speedLimit * 0.2f;
        }

        private void AddBufferItem(BufferItem item, PathUnit.Position target) {
            var laneLocation = laneLocation_[item.LaneId];
            var locPathFindIndex = laneLocation >> 16; // upper 16 bit, expected (?) path find index
            var bufferIndex = (int)(laneLocation & 65535u); // lower 16 bit
            int comparisonBufferPos;
            if (locPathFindIndex == pathFindIndex_) {
                if (item.ComparisonValue >= buffer_[bufferIndex].ComparisonValue) {
                    return;
                }

                var bufferPosIndex = bufferIndex >> 6; // arithmetic shift (sign stays), upper 10 bit
                var bufferPos = bufferIndex & -64; // upper 10 bit (no shift)
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

                var newBufferIndex = bufferPosIndex << 6 | bufferMax_[bufferPosIndex]--;
                var bufferItem = buffer_[newBufferIndex];
                laneLocation_[bufferItem.LaneId] = laneLocation;
                buffer_[bufferIndex] = bufferItem;
            } else {
                comparisonBufferPos = Mathf.Max(Mathf.RoundToInt(item.ComparisonValue * 1024f), bufferMinPos_);
            }

            if (comparisonBufferPos >= 1024) {
                return;
            }

            if (comparisonBufferPos < 0) {
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

        private void GetLaneDirection(PathUnit.Position pathPos, out NetInfo.Direction direction, out NetInfo.LaneType laneType, out VehicleInfo.VehicleType vehicleType) {
            var instance = Singleton<NetManager>.instance;
            var info = instance.m_segments.m_buffer[pathPos.m_segment].Info;
            if (info.m_lanes.Length > pathPos.m_lane) {
                direction = info.m_lanes[pathPos.m_lane].m_finalDirection;
                laneType = info.m_lanes[pathPos.m_lane].m_laneType;
                vehicleType = info.m_lanes[pathPos.m_lane].m_vehicleType;
                if ((instance.m_segments.m_buffer[pathPos.m_segment].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
                    direction = NetInfo.InvertDirection(direction);
                }
            } else {
                direction = NetInfo.Direction.None;
                laneType = NetInfo.LaneType.None;
                vehicleType = VehicleInfo.VehicleType.None;
            }
        }

        private void PathFindThread() {
            while (true) {
                // Log.Message($"Pathfind Thread #{Thread.CurrentThread.ManagedThreadId} iteration!");
                try {
                    Monitor.Enter(QueueLock);

                    while (QueueFirst == 0u && !Terminated) {
                        Monitor.Wait(QueueLock);
                    }

                    if (Terminated) {
                        break;
                    }

                    Calculating = QueueFirst;
                    QueueFirst = CustomPathManager._instance.QueueItems[Calculating].nextPathUnitId;

                    // QueueFirst = PathUnits.m_buffer[Calculating].m_nextPathUnit;
                    if (QueueFirst == 0u) {
                        QueueLast = 0u;
                        m_queuedPathFindCount = 0;
                    } else {
                        --m_queuedPathFindCount;
                    }

                    CustomPathManager._instance.QueueItems[Calculating].nextPathUnitId = 0u;

                    // PathUnits.m_buffer[Calculating].m_nextPathUnit = 0u;
                    // check if path unit is created
                    /*if ((PathUnits.m_buffer[Calculating].m_pathFindFlags & PathUnit.FLAG_CREATED) == 0) {
                            Log.Warning($"CustomPathFind: Refusing to calculate path unit {Calculating} which is not created!");
                            continue;
                    }*/

                    PathUnits.m_buffer[Calculating].m_pathFindFlags = (byte)((PathUnits.m_buffer[Calculating].m_pathFindFlags & ~PathUnit.FLAG_CREATED) | PathUnit.FLAG_CALCULATING);
                    queueItem_ = CustomPathManager._instance.QueueItems[Calculating];
                } catch (Exception e) {
                    Log.Error($"(PF #{pathFindIndex_}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread Error for unit {Calculating}, flags={PathUnits.m_buffer[Calculating].m_pathFindFlags} (1): {e}");
                } finally {
                    Monitor.Exit(QueueLock);
                }

                // calculate path unit
                try {
                    PathFindImplementation(Calculating, ref PathUnits.m_buffer[Calculating]);
                } catch (Exception ex) {
                    Log.Error($"(PF #{pathFindIndex_}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread Error for unit {Calculating}, flags={PathUnits.m_buffer[Calculating].m_pathFindFlags} (2): {ex}");
                    // UIView.ForwardException(ex);
#if DEBUG
                    ++m_failedPathFinds;

#if DEBUGNEWPF
                    var debug = m_debug;
                    if (debug) {
                        Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {pathFindIndex_}: " +
                                   $"Could not find path for unit {Calculating} -- exception occurred " +
                                   "in PathFindImplementation");
                    }
#endif
#endif
                    // CustomPathManager._instance.ResetQueueItem(Calculating);
                    PathUnits.m_buffer[Calculating].m_pathFindFlags |= PathUnit.FLAG_FAILED;
                }

                // tCurrentState = 10;
#if DEBUGLOCKS
                lockIter = 0;
#endif

                try {
                    Monitor.Enter(QueueLock);

                    PathUnits.m_buffer[Calculating].m_pathFindFlags =
                        (byte)(PathUnits.m_buffer[Calculating].m_pathFindFlags & ~PathUnit.FLAG_CALCULATING);

                    // NON-STOCK CODE START
                    try {
                        Monitor.Enter(bufferLock_);
                        CustomPathManager._instance.QueueItems[Calculating].queued = false;
                        CustomPathManager._instance.ReleasePath(Calculating);
                    } finally {
                        Monitor.Exit(bufferLock_);
                    }

                    // NON-STOCK CODE END
                    Calculating = 0u;
                    Monitor.Pulse(QueueLock);
                } catch (Exception e) {
                    Log.Error($"(PF #{pathFindIndex_}, T#{Thread.CurrentThread.ManagedThreadId}, " +
                              $"Id #{pfId}) CustomPathFind.PathFindThread Error for unit {Calculating}, " +
                              $"flags={PathUnits.m_buffer[Calculating].m_pathFindFlags} (3): {e}");
                } finally {
                    Monitor.Exit(QueueLock);
                }
            }
        }

        /// <summary>
        /// Determines if a given lane may be used by the vehicle whose path is currently being calculated.
        /// </summary>
        /// <param name="debug"></param>
        /// <param name="segmentId"></param>
        /// <param name="laneIndex"></param>
        /// <param name="laneId"></param>
        /// <param name="laneInfo"></param>
        /// <returns></returns>
        protected virtual bool CanUseLane(bool debug,
                                          ushort segmentId,
                                          NetInfo segmentInfo,
                                          uint laneIndex,
                                          NetInfo.Lane laneInfo) {
            if (!Options.vehicleRestrictionsEnabled) {
                return true;
            }

            if (queueItem_.vehicleType == ExtVehicleType.None || queueItem_.vehicleType == ExtVehicleType.Tram) {
                return true;
            }

            /*if (laneInfo == null)
                    laneInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex];*/

            if ((laneInfo.m_vehicleType &
                 (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train)) ==
                VehicleInfo.VehicleType.None) {
                return true;
            }

            var allowedTypes = VehicleRestrictionsManager.GetAllowedVehicleTypes(
                segmentId,
                segmentInfo,
                laneIndex,
                laneInfo,
                VehicleRestrictionsMode.Configured);

            return (allowedTypes & queueItem_.vehicleType) != ExtVehicleType.None;
        }

        /// <summary>
        /// Determines the speed limit for the given lane.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="laneIndex"></param>
        /// <param name="laneId"></param>
        /// <param name="lane"></param>
        /// <returns></returns>
        protected virtual float GetLaneSpeedLimit(ushort segmentId,
                                                  byte laneIndex,
                                                  uint laneId,
                                                  NetInfo.Lane lane) {
            return Options.customSpeedLimitsEnabled
                       ? SpeedLimitManager.GetLockFreeGameSpeedLimit(
                           segmentId,
                           laneIndex,
                           laneId,
                           lane)
                       : lane.m_speedLimit;
        }
    }
}