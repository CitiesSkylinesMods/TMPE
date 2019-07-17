#define DEBUGLOCKSx
#define COUNTSEGMENTSTONEXTJUNCTIONx

namespace TrafficManager.Custom.PathFinding {
    using System;
    using System.Collections.Generic;
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
    using State.ConfigData;
    using Traffic.Data;
    using Traffic.Enums;
    using UnityEngine;

#if !PF2
	[TargetType(typeof(PathFind))]
#endif
    public class CustomPathFind : PathFind {
        private enum LaneChangingCostCalculationMode {
            None,
            [UsedImplicitly]
            ByLaneDistance,
            ByGivenDistance
        }

        private struct BufferItem {
            public PathUnit.Position m_position;
            public float m_comparisonValue;
            public float m_methodDistance;
            public float m_duration;
            public uint m_laneID;
            public NetInfo.Direction m_direction;
            public NetInfo.LaneType m_lanesUsed;
            public VehicleInfo.VehicleType m_vehiclesUsed;
            public float m_trafficRand;
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
        FieldInfo _fieldpathUnits;
        FieldInfo _fieldQueueFirst;
        FieldInfo _fieldQueueLast;
        FieldInfo _fieldQueueLock;
        FieldInfo _fieldCalculating;
        FieldInfo _fieldTerminated;
        FieldInfo _fieldPathFindThread;

        private Array32<PathUnit> PathUnits {
            get => _fieldpathUnits.GetValue(this) as Array32<PathUnit>;
            set => _fieldpathUnits.SetValue(this, value);
        }

        private uint QueueFirst {
            get => (uint)_fieldQueueFirst.GetValue(this);
            set => _fieldQueueFirst.SetValue(this, value);
        }

        private uint QueueLast {
            get => (uint)_fieldQueueLast.GetValue(this);
            set => _fieldQueueLast.SetValue(this, value);
        }

        private uint Calculating {
            get => (uint)_fieldCalculating.GetValue(this);
            set => _fieldCalculating.SetValue(this, value);
        }

        private object QueueLock {
            get => _fieldQueueLock.GetValue(this);
            set => _fieldQueueLock.SetValue(this, value);
        }

        private object _bufferLock;

        private Thread CustomPathFindThread {
            get => (Thread)_fieldPathFindThread.GetValue(this);
            set => _fieldPathFindThread.SetValue(this, value);
        }

        private bool Terminated {
            get => (bool)_fieldTerminated.GetValue(this);
            set => _fieldTerminated.SetValue(this, value);
        }

        private int m_bufferMinPos;
        private int m_bufferMaxPos;
        private uint[] m_laneLocation;
        private PathUnit.Position[] m_laneTarget;
        private BufferItem[] m_buffer;
        private int[] m_bufferMin;
        private int[] m_bufferMax;
        private float m_maxLength;
        private uint m_startLaneA;
        private uint m_startLaneB;
        private ushort m_startSegmentA;
        private ushort m_startSegmentB;
        private uint m_endLaneA;
        private uint m_endLaneB;
        private uint m_vehicleLane;
        private byte m_startOffsetA;
        private byte m_startOffsetB;
        private byte m_vehicleOffset;
        private NetSegment.Flags m_carBanMask;
        private bool m_isHeavyVehicle;
        private bool m_ignoreBlocked;
        private bool m_stablePath;
        private bool m_randomParking;
        private bool m_transportVehicle;
        private bool m_ignoreCost;
        private PathUnitQueueItem queueItem;
        private NetSegment.Flags m_disableMask;
        /*private ExtVehicleType? _extVehicleType;
        private ushort? _vehicleId;
        private ExtCitizenInstance.ExtPathType? _extPathType;*/
        private bool m_isRoadVehicle;
        private bool m_isLaneArrowObeyingEntity;
        private bool m_isLaneConnectionObeyingEntity;
        private bool m_leftHandDrive;
#if DEBUG
        public uint m_failedPathFinds;
        public uint m_succeededPathFinds;
        private bool m_debug;
        private IDictionary<ushort, IList<ushort>> m_debugPositions;
#endif
        public int pfId;
        private Randomizer m_pathRandomizer;
        private uint m_pathFindIndex;
        private NetInfo.LaneType m_laneTypes;
        private VehicleInfo.VehicleType m_vehicleTypes;

        private GlobalConfig m_conf;

        private static readonly CustomSegmentLightsManager customTrafficLightsManager =
            CustomSegmentLightsManager.Instance;

        private static readonly JunctionRestrictionsManager junctionManager = JunctionRestrictionsManager.Instance;

        private static readonly VehicleRestrictionsManager vehicleRestrictionsManager =
            VehicleRestrictionsManager.Instance;

        private static readonly SpeedLimitManager speedLimitManager = SpeedLimitManager.Instance;

        private static readonly TrafficMeasurementManager
            trafficMeasurementManager = TrafficMeasurementManager.Instance;

        private static readonly RoutingManager routingManager = RoutingManager.Instance;

        public bool IsMasterPathFind;

        protected virtual void Awake() {
#if DEBUG
            Log._Debug($"CustomPathFind.Awake called.");
#endif

            var stockPathFindType = typeof(PathFind);
            const BindingFlags fieldFlags = BindingFlags.NonPublic | BindingFlags.Instance;

            _fieldpathUnits = stockPathFindType.GetField("m_pathUnits", fieldFlags);
            _fieldQueueFirst = stockPathFindType.GetField("m_queueFirst", fieldFlags);
            _fieldQueueLast = stockPathFindType.GetField("m_queueLast", fieldFlags);
            _fieldQueueLock = stockPathFindType.GetField("m_queueLock", fieldFlags);
            _fieldTerminated = stockPathFindType.GetField("m_terminated", fieldFlags);
            _fieldCalculating = stockPathFindType.GetField("m_calculating", fieldFlags);
            _fieldPathFindThread = stockPathFindType.GetField("m_pathFindThread", fieldFlags);

            m_buffer = new BufferItem[65536]; // 2^16
            _bufferLock = PathManager.instance.m_bufferLock;
            PathUnits = PathManager.instance.m_pathUnits;
#if DEBUG
            if (QueueLock == null) {
                Log._Debug($"(PF #{m_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, " +
                           $"Id #{pfId}) CustomPathFind.Awake: QueueLock is null. Creating.");
                QueueLock = new object();
            } else {
                Log._Debug($"(PF #{m_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, " +
                           $"Id #{pfId}) CustomPathFind.Awake: QueueLock is NOT null.");
            }
#else
			QueueLock = new object();
#endif
            m_laneLocation = new uint[262144]; // 2^18
            m_laneTarget = new PathUnit.Position[262144]; // 2^18
            m_bufferMin = new int[1024]; // 2^10
            m_bufferMax = new int[1024]; // 2^10

            m_pathfindProfiler = new ThreadProfiler();
            CustomPathFindThread = new Thread(PathFindThread) {
                Name = "Pathfind", Priority = SimulationManager.SIMULATION_PRIORITY
            };
            CustomPathFindThread.Start();
            if (!CustomPathFindThread.IsAlive) {
                // CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find thread failed to start!");
                Log.Error($"(PF #{m_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, " +
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
                Log.Error($"(PF #{m_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, " +
                          $"Id #{pfId}) CustomPathFind.CalculatePath({unit}, {skipQueue}): Error: {e}");
            } finally {
                Monitor.Exit(QueueLock);
            }

            return true;
        }

        // PathFind
        private void PathFindImplementation(uint unit, ref PathUnit data) {
            m_conf = GlobalConfig.Instance; // NON-STOCK CODE

            var netManager = Singleton<NetManager>.instance;
            m_laneTypes = (NetInfo.LaneType)PathUnits.m_buffer[unit].m_laneTypes;
            m_vehicleTypes = (VehicleInfo.VehicleType)PathUnits.m_buffer[unit].m_vehicleTypes;
            m_maxLength = PathUnits.m_buffer[unit].m_length;
            m_pathFindIndex = m_pathFindIndex + 1u & 32767u;
            m_pathRandomizer = new Randomizer(unit);

            m_carBanMask = NetSegment.Flags.CarBan;
            m_isHeavyVehicle = (PathUnits.m_buffer[unit].m_simulationFlags & 16) != 0;
            if (m_isHeavyVehicle) {
                m_carBanMask |= NetSegment.Flags.HeavyBan;
            }

            if ((PathUnits.m_buffer[unit].m_simulationFlags & 4) != 0) {
                m_carBanMask |= NetSegment.Flags.WaitingPath;
            }

            m_ignoreBlocked = (PathUnits.m_buffer[unit].m_simulationFlags & 32) != 0;
            m_stablePath = (PathUnits.m_buffer[unit].m_simulationFlags & 64) != 0;
            m_randomParking = (PathUnits.m_buffer[unit].m_simulationFlags & 128) != 0;
            m_transportVehicle = (byte)(m_laneTypes & NetInfo.LaneType.TransportVehicle) != 0;
            m_ignoreCost = m_stablePath || (PathUnits.m_buffer[unit].m_simulationFlags & 8) != 0;
            m_disableMask = NetSegment.Flags.Collapsed | NetSegment.Flags.PathFailed;
            if ((PathUnits.m_buffer[unit].m_simulationFlags & 2) == 0) {
                m_disableMask |= NetSegment.Flags.Flooded;
            }

            // this._speedRand = 0;
            m_leftHandDrive = Constants.ServiceFactory.SimulationService.LeftHandDrive;
            m_isRoadVehicle = (queueItem.vehicleType & ExtVehicleType.RoadVehicle) != ExtVehicleType.None;
            m_isLaneArrowObeyingEntity =
                (m_vehicleTypes & LaneArrowManager.VEHICLE_TYPES) != VehicleInfo.VehicleType.None
                && (queueItem.vehicleType & LaneArrowManager.EXT_VEHICLE_TYPES) != ExtVehicleType.None;
            m_isLaneConnectionObeyingEntity =
                (m_vehicleTypes & LaneConnectionManager.VEHICLE_TYPES) != VehicleInfo.VehicleType.None
                && (queueItem.vehicleType & LaneConnectionManager.EXT_VEHICLE_TYPES) != ExtVehicleType.None;
#if DEBUGNEWPF && DEBUG
            var logLogic =
                m_debug =
                    DebugSwitch.PathFindingLog.Get() &&
                    ((GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.None
                      && queueItem.vehicleType == ExtVehicleType.None)
                     || (queueItem.vehicleType & GlobalConfig.Instance.Debug.ApiExtVehicleType) != ExtVehicleType.None)
                    && (DebugSettings.StartSegmentId == 0
                        || data.m_position00.m_segment == DebugSettings.StartSegmentId
                        || data.m_position02.m_segment == DebugSettings.StartSegmentId)
                    && (DebugSettings.EndSegmentId == 0
                        || data.m_position01.m_segment == DebugSettings.EndSegmentId
                        || data.m_position03.m_segment == DebugSettings.EndSegmentId)
                    && (DebugSettings.VehicleId == 0
                        || queueItem.vehicleId == DebugSettings.VehicleId);

            if (logLogic) {
                Log._Debug($"CustomPathFind.PathFindImplementation: START calculating " +
                           $"path unit {unit}, type {queueItem.vehicleType}");
                m_debugPositions = new Dictionary<ushort, IList<ushort>>();
            }
#endif

            if ((byte)(m_laneTypes & NetInfo.LaneType.Vehicle) != 0) {
                m_laneTypes |= NetInfo.LaneType.TransportVehicle;
            }

            var posCount = PathUnits.m_buffer[unit].m_positionCount & 15;
            var vehiclePosIndicator = PathUnits.m_buffer[unit].m_positionCount >> 4;
            BufferItem bufferItemStartA;
            if (data.m_position00.m_segment != 0 && posCount >= 1) {
                m_startLaneA = PathManager.GetLaneID(data.m_position00);
                m_startSegmentA = data.m_position00.m_segment; // NON-STOCK CODE
                m_startOffsetA = data.m_position00.m_offset;
                bufferItemStartA.m_laneID = m_startLaneA;
                bufferItemStartA.m_position = data.m_position00;
                GetLaneDirection(data.m_position00, out bufferItemStartA.m_direction, out bufferItemStartA.m_lanesUsed, out bufferItemStartA.m_vehiclesUsed);
                bufferItemStartA.m_comparisonValue = 0f;
                bufferItemStartA.m_duration = 0f;
#if COUNTSEGMENTSTONEXTJUNCTION
				bufferItemStartA.m_numSegmentsToNextJunction = 0;
#endif
            } else {
                m_startLaneA = 0u;
                m_startSegmentA = 0; // NON-STOCK CODE
                m_startOffsetA = 0;
                bufferItemStartA = default;
            }

            BufferItem bufferItemStartB;
            if (data.m_position02.m_segment != 0 && posCount >= 3) {
                m_startLaneB = PathManager.GetLaneID(data.m_position02);
                m_startSegmentB = data.m_position02.m_segment; // NON-STOCK CODE
                m_startOffsetB = data.m_position02.m_offset;
                bufferItemStartB.m_laneID = m_startLaneB;
                bufferItemStartB.m_position = data.m_position02;

                GetLaneDirection(
                    data.m_position02,
                    out bufferItemStartB.m_direction,
                    out bufferItemStartB.m_lanesUsed,
                    out bufferItemStartB.m_vehiclesUsed);

                bufferItemStartB.m_comparisonValue = 0f;
                bufferItemStartB.m_duration = 0f;
#if COUNTSEGMENTSTONEXTJUNCTION
				bufferItemStartB.m_numSegmentsToNextJunction = 0;
#endif
            } else {
                m_startLaneB = 0u;
                m_startSegmentB = 0; // NON-STOCK CODE
                m_startOffsetB = 0;
                bufferItemStartB = default;
            }

            BufferItem bufferItemEndA;
            if (data.m_position01.m_segment != 0 && posCount >= 2) {
                m_endLaneA = PathManager.GetLaneID(data.m_position01);
                bufferItemEndA.m_laneID = m_endLaneA;
                bufferItemEndA.m_position = data.m_position01;

                GetLaneDirection(
                    data.m_position01,
                    out bufferItemEndA.m_direction,
                    out bufferItemEndA.m_lanesUsed,
                    out bufferItemEndA.m_vehiclesUsed);

                bufferItemEndA.m_methodDistance = 0.01f;
                bufferItemEndA.m_comparisonValue = 0f;
                bufferItemEndA.m_duration = 0f;
                bufferItemEndA.m_trafficRand = 0; // NON-STOCK CODE
#if COUNTSEGMENTSTONEXTJUNCTION
				bufferItemEndA.m_numSegmentsToNextJunction = 0;
#endif
            } else {
                m_endLaneA = 0u;
                bufferItemEndA = default;
            }

            BufferItem bufferItemEndB;
            if (data.m_position03.m_segment != 0 && posCount >= 4) {
                m_endLaneB = PathManager.GetLaneID(data.m_position03);
                bufferItemEndB.m_laneID = m_endLaneB;
                bufferItemEndB.m_position = data.m_position03;

                GetLaneDirection(
                    data.m_position03,
                    out bufferItemEndB.m_direction,
                    out bufferItemEndB.m_lanesUsed,
                    out bufferItemEndB.m_vehiclesUsed);

                bufferItemEndB.m_methodDistance = 0.01f;
                bufferItemEndB.m_comparisonValue = 0f;
                bufferItemEndB.m_duration = 0f;
                bufferItemEndB.m_trafficRand = 0; // NON-STOCK CODE
#if COUNTSEGMENTSTONEXTJUNCTION
				bufferItemEndB.m_numSegmentsToNextJunction = 0;
#endif
            } else {
                m_endLaneB = 0u;
                bufferItemEndB = default;
            }
            if (data.m_position11.m_segment != 0 && vehiclePosIndicator >= 1) {
                m_vehicleLane = PathManager.GetLaneID(data.m_position11);
                m_vehicleOffset = data.m_position11.m_offset;
            } else {
                m_vehicleLane = 0u;
                m_vehicleOffset = 0;
            }
#if DEBUGNEWPF && DEBUG
            if (logLogic) {
                Log._Debug($"CustomPathFind.PathFindImplementation: Preparing calculating " +
                           $"path unit {unit}, type {queueItem.vehicleType}:\n" +
                           $"\tbufferItemStartA: segment={bufferItemStartA.m_position.m_segment} " +
                           $"lane={bufferItemStartA.m_position.m_lane} " +
                           $"off={bufferItemStartA.m_position.m_offset} " +
                           $"laneId={bufferItemStartA.m_laneID}\n" +
                           $"\tbufferItemStartB: segment={bufferItemStartB.m_position.m_segment} " +
                           $"lane={bufferItemStartB.m_position.m_lane} " +
                           $"off={bufferItemStartB.m_position.m_offset} laneId={bufferItemStartB.m_laneID}\n" +
                           $"\tbufferItemEndA: segment={bufferItemEndA.m_position.m_segment} " +
                           $"lane={bufferItemEndA.m_position.m_lane} off={bufferItemEndA.m_position.m_offset} " +
                           $"laneId={bufferItemEndA.m_laneID}\n\tbufferItemEndB: " +
                           $"segment={bufferItemEndB.m_position.m_segment} " +
                           $"lane={bufferItemEndB.m_position.m_lane} off={bufferItemEndB.m_position.m_offset} " +
                           $"laneId={bufferItemEndB.m_laneID}\n\tvehicleItem: " +
                           $"segment={data.m_position11.m_segment} lane={data.m_position11.m_lane} " +
                           $"off={data.m_position11.m_offset} laneId={m_vehicleLane} " +
                           $"vehiclePosIndicator={vehiclePosIndicator}\n"
                    );
            }
#endif
            var finalBufferItem = default(BufferItem);
            byte startOffset = 0;
            m_bufferMinPos = 0;
            m_bufferMaxPos = -1;
            if (m_pathFindIndex == 0u) {
                var maxUInt = 4294901760u;
                for (var i = 0; i < 262144; ++i) {
                    m_laneLocation[i] = maxUInt;
                }
            }

            for (var j = 0; j < 1024; ++j) {
                m_bufferMin[j] = 0;
                m_bufferMax[j] = -1;
            }

            if (bufferItemEndA.m_position.m_segment != 0) {
                ++m_bufferMax[0];
                m_buffer[++m_bufferMaxPos] = bufferItemEndA;
            }

            if (bufferItemEndB.m_position.m_segment != 0) {
                ++m_bufferMax[0];
                m_buffer[++m_bufferMaxPos] = bufferItemEndB;
            }

            var canFindPath = false;

            while (m_bufferMinPos <= m_bufferMaxPos) {
                var bufMin = m_bufferMin[m_bufferMinPos];
                var bufMax = m_bufferMax[m_bufferMinPos];
                if (bufMin > bufMax) {
                    ++m_bufferMinPos;
                } else {
                    m_bufferMin[m_bufferMinPos] = bufMin + 1;
                    var candidateItem = m_buffer[(m_bufferMinPos << 6) + bufMin];
                    if (candidateItem.m_position.m_segment == bufferItemStartA.m_position.m_segment
                        && candidateItem.m_position.m_lane == bufferItemStartA.m_position.m_lane) {
                        // we reached startA
                        if ((byte)(candidateItem.m_direction & NetInfo.Direction.Forward) != 0
                            && candidateItem.m_position.m_offset >= m_startOffsetA)
                        {
                            finalBufferItem = candidateItem;
                            startOffset = m_startOffsetA;
                            canFindPath = true;
                            break;
                        }

                        if ((byte)(candidateItem.m_direction & NetInfo.Direction.Backward) != 0
                            && candidateItem.m_position.m_offset <= m_startOffsetA)
                        {
                            finalBufferItem = candidateItem;
                            startOffset = m_startOffsetA;
                            canFindPath = true;
                            break;
                        }
                    }

                    if (candidateItem.m_position.m_segment == bufferItemStartB.m_position.m_segment
                        && candidateItem.m_position.m_lane == bufferItemStartB.m_position.m_lane) {
                        // we reached startB
                        if ((byte)(candidateItem.m_direction & NetInfo.Direction.Forward) != 0
                            && candidateItem.m_position.m_offset >= m_startOffsetB)
                        {
                            finalBufferItem = candidateItem;
                            startOffset = m_startOffsetB;
                            canFindPath = true;
                            break;
                        }

                        if ((byte)(candidateItem.m_direction & NetInfo.Direction.Backward) != 0
                            && candidateItem.m_position.m_offset <= m_startOffsetB)
                        {
                            finalBufferItem = candidateItem;
                            startOffset = m_startOffsetB;
                            canFindPath = true;
                            break;
                        }
                    }

                    // explore the path
                    if ((byte)(candidateItem.m_direction & NetInfo.Direction.Forward) != 0) {
                        var startNode = netManager.m_segments.m_buffer[candidateItem.m_position.m_segment].m_startNode;
                        var laneRoutingIndex = routingManager.GetLaneEndRoutingIndex(candidateItem.m_laneID, true);
                        ProcessItemMain(unit, candidateItem, ref netManager.m_segments.m_buffer[candidateItem.m_position.m_segment], routingManager.SegmentRoutings[candidateItem.m_position.m_segment], routingManager.LaneEndBackwardRoutings[laneRoutingIndex], startNode, true, ref netManager.m_nodes.m_buffer[startNode], 0, false);
                    }

                    if ((byte)(candidateItem.m_direction & NetInfo.Direction.Backward) != 0) {
                        var endNode = netManager.m_segments.m_buffer[candidateItem.m_position.m_segment].m_endNode;
                        var laneRoutingIndex = routingManager.GetLaneEndRoutingIndex(candidateItem.m_laneID, false);
                        ProcessItemMain(unit, candidateItem, ref netManager.m_segments.m_buffer[candidateItem.m_position.m_segment], routingManager.SegmentRoutings[candidateItem.m_position.m_segment], routingManager.LaneEndBackwardRoutings[laneRoutingIndex], endNode, false, ref netManager.m_nodes.m_buffer[endNode], 255, false);
                    }

                    // handle special nodes (e.g. bus stops)
                    var num6 = 0;
                    var specialNodeId = netManager.m_lanes.m_buffer[candidateItem.m_laneID].m_nodes;
                    if (specialNodeId != 0) {
                        var startNode2 = netManager.m_segments.m_buffer[candidateItem.m_position.m_segment].m_startNode;
                        var endNode2 = netManager.m_segments.m_buffer[candidateItem.m_position.m_segment].m_endNode;
                        var nodesDisabled =
                            ((netManager.m_nodes.m_buffer[startNode2].m_flags |
                              netManager.m_nodes.m_buffer[endNode2].m_flags) & NetNode.Flags.Disabled) !=
                            NetNode.Flags.None;

                        while (specialNodeId != 0) {
                            var direction = NetInfo.Direction.None;
                            var laneOffset = netManager.m_nodes.m_buffer[specialNodeId].m_laneOffset;
                            if (laneOffset <= candidateItem.m_position.m_offset) {
                                direction |= NetInfo.Direction.Forward;
                            }

                            if (laneOffset >= candidateItem.m_position.m_offset) {
                                direction |= NetInfo.Direction.Backward;
                            }

                            if ((byte)(candidateItem.m_direction & direction) != 0
                                && (!nodesDisabled
                                    || (netManager.m_nodes.m_buffer[specialNodeId].m_flags
                                        & NetNode.Flags.Disabled) != NetNode.Flags.None)) {
#if DEBUGNEWPF && DEBUG
                                if (logLogic && (DebugSettings.NodeId <= 0
                                                 || specialNodeId == DebugSettings.NodeId))
                                {
                                    Log._Debug($"CustomPathFind.PathFindImplementation: Handling " +
                                               $"special node for path unit {unit}, type {queueItem.vehicleType}:\n" +
                                               $"\tcandidateItem.m_position.m_segment" +
                                               $"={candidateItem.m_position.m_segment}\n" +
                                               $"\tcandidateItem.m_position.m_lane" +
                                               $"={candidateItem.m_position.m_lane}\n" +
                                               $"\tcandidateItem.m_laneID={candidateItem.m_laneID}\n" +
                                               $"\tspecialNodeId={specialNodeId}\n" +
                                               $"\tstartNode2={startNode2}\n" +
                                               $"\tendNode2={endNode2}\n");
                                }
#endif
                                ProcessItemMain(
                                    unit,
                                    candidateItem,
                                    ref netManager.m_segments.m_buffer[candidateItem.m_position.m_segment],
                                    routingManager.SegmentRoutings[candidateItem.m_position.m_segment],
                                    routingManager.LaneEndBackwardRoutings[0],
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
            }

            if (!canFindPath) {
                // we could not find a path
                PathUnits.m_buffer[(int)unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;
#if DEBUG
                ++m_failedPathFinds;

#if DEBUGNEWPF
                if (logLogic) {
                    Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} " +
                               $"PF {m_pathFindIndex}: Could not find path for unit {unit} " +
                               $"-- path-finding failed during process");
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
                               $"PF {m_pathFindIndex}: Reachability graph for unit {unit}:\n" +
                               $"== REACHABLE ==\n" + reachableBuf + "\n" +
                               "== UNREACHABLE ==\n" + unreachableBuf);
                }
#endif
#endif
                // CustomPathManager._instance.ResetQueueItem(unit);
                return;
            }

            // we could calculate a valid path
            var duration = m_laneTypes != NetInfo.LaneType.Pedestrian
                               ? finalBufferItem.m_duration
                               : finalBufferItem.m_methodDistance;
            PathUnits.m_buffer[unit].m_length = duration;
            PathUnits.m_buffer[unit].m_laneTypes = (byte)finalBufferItem.m_lanesUsed; // NON-STOCK CODE
            PathUnits.m_buffer[unit].m_vehicleTypes = (ushort)finalBufferItem.m_vehiclesUsed; // NON-STOCK CODE
#if DEBUG
            /*if (_conf.Debug.Switches[4])
                    Log._Debug($"Lane/Vehicle types of path unit {unit}: {finalBufferItem.m_lanesUsed} / {finalBufferItem.m_vehiclesUsed}");*/
#endif
            var currentPathUnitId = unit;
            var currentItemPositionCount = 0;
            var sumOfPositionCounts = 0;
            var currentPosition = finalBufferItem.m_position;
            if ((currentPosition.m_segment != bufferItemEndA.m_position.m_segment || currentPosition.m_lane != bufferItemEndA.m_position.m_lane || currentPosition.m_offset != bufferItemEndA.m_position.m_offset) &&
                (currentPosition.m_segment != bufferItemEndB.m_position.m_segment || currentPosition.m_lane != bufferItemEndB.m_position.m_lane || currentPosition.m_offset != bufferItemEndB.m_position.m_offset)) {
                // the found starting position differs from the desired end position
                if (startOffset != currentPosition.m_offset) {
                    // the offsets differ: copy the found starting position and modify the offset to fit the desired offset
                    var position2 = currentPosition;
                    position2.m_offset = startOffset;
                    PathUnits.m_buffer[currentPathUnitId].SetPosition(currentItemPositionCount++, position2);

                    // now we have: [desired starting position]
                }

                // add the found starting position to the path unit
                PathUnits.m_buffer[currentPathUnitId].SetPosition(currentItemPositionCount++, currentPosition);
                currentPosition = m_laneTarget[finalBufferItem.m_laneID]; // go to the next path position

                // now we have either [desired starting position, found starting position] or
                // [found starting position], depending on if the found starting position
                // matched the desired
            }

            // beginning with the starting position, going to the target position:
            // assemble the path units
            for (var k = 0; k < 262144; ++k) {
                // pfCurrentState = 6;
                // add the next path position to the current unit
                PathUnits.m_buffer[currentPathUnitId].SetPosition(currentItemPositionCount++, currentPosition);

                if ((currentPosition.m_segment == bufferItemEndA.m_position.m_segment &&
                     currentPosition.m_lane == bufferItemEndA.m_position.m_lane &&
                     currentPosition.m_offset == bufferItemEndA.m_position.m_offset)
                    || (currentPosition.m_segment == bufferItemEndB.m_position.m_segment &&
                        currentPosition.m_lane == bufferItemEndB.m_position.m_lane &&
                        currentPosition.m_offset == bufferItemEndB.m_position.m_offset)) {
                    // we have reached the end position
                    PathUnits.m_buffer[currentPathUnitId].m_positionCount = (byte)currentItemPositionCount;
                    sumOfPositionCounts += currentItemPositionCount; // add position count of last unit to sum

                    if (sumOfPositionCounts != 0) {
                        // for each path unit from start to target: calculate length (distance) to target
                        currentPathUnitId = PathUnits.m_buffer[unit].m_nextPathUnit; // (we do not need to calculate the length for the starting unit since this is done before; it's the total path length)
                        currentItemPositionCount = PathUnits.m_buffer[unit].m_positionCount;
                        var totalIter = 0;
                        while (currentPathUnitId != 0u) {
                            PathUnits.m_buffer[currentPathUnitId].m_length =
                                duration * (sumOfPositionCounts - currentItemPositionCount) / sumOfPositionCounts;
                            currentItemPositionCount += PathUnits.m_buffer[currentPathUnitId].m_positionCount;
                            currentPathUnitId = PathUnits.m_buffer[currentPathUnitId].m_nextPathUnit;

                            if (++totalIter >= 262144) {
#if DEBUG
                                Log.Error("THREAD #{Thread.CurrentThread.ManagedThreadId} PF " +
                                          "{this._pathFindIndex}: PathFindImplementation: Invalid list detected.");
#endif
                                CODebugBase<LogChannel>.Error(
                                    LogChannel.Core,
                                    $"Invalid list detected!\n{Environment.StackTrace}");
                                break;
                            }
                        }
                    }
#if DEBUG
                    // Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Path found (pfCurrentState={pfCurrentState}) for unit {unit}");
#endif
                    PathUnits.m_buffer[(int)unit].m_pathFindFlags |= PathUnit.FLAG_READY; // Path found
#if DEBUG
                    ++m_succeededPathFinds;

#if DEBUGNEWPF
                    if (logLogic) {
                        Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {m_pathFindIndex}: " +
                                   $"Path-find succeeded for unit {unit}");
                    }
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
                        Monitor.Enter(_bufferLock);
                        if (!PathUnits.CreateItem(out createdPathUnitId, ref m_pathRandomizer)) {
                            // we failed to create a new path unit, thus the path-finding also failed
                            PathUnits.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;
#if DEBUG
                            ++m_failedPathFinds;

#if DEBUGNEWPF
                            if (logLogic) {
                                Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF " +
                                           $"{m_pathFindIndex}: Could not find path for unit {unit} " +
                                           $"-- Could not create path unit");
                            }
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
                        PathUnits.m_buffer[currentPathUnitId].m_laneTypes = (byte)finalBufferItem.m_lanesUsed;
                        // NON-STOCK CODE (this is not accurate!)

                        PathUnits.m_buffer[currentPathUnitId].m_vehicleTypes = (ushort)finalBufferItem.m_vehiclesUsed;
                        // NON-STOCK CODE (this is not accurate!)
                        sumOfPositionCounts += currentItemPositionCount;
                        Singleton<PathManager>.instance.m_pathUnitCount = (int)(PathUnits.ItemCount() - 1u);
                    } catch (Exception e) {
                        Log.Error($"(PF #{m_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, " +
                                  $"Id #{pfId}) CustomPathFind.PathFindImplementation Error: {e}");
                        break;
                    } finally {
                        Monitor.Exit(_bufferLock);
                    }
                    currentPathUnitId = createdPathUnitId;
                    currentItemPositionCount = 0;
                }

                var laneID = PathManager.GetLaneID(currentPosition);
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
                currentPosition = m_laneTarget[laneID];
            }

            PathUnits.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;
#if DEBUG
            ++m_failedPathFinds;

#if DEBUGNEWPF
            if (logLogic) {
                Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {m_pathFindIndex}: " +
                           $"Could not find path for unit {unit} -- internal error: for loop break");
            }
#endif
#endif
            // CustomPathManager._instance.ResetQueueItem(unit);
#if DEBUG
            // Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}:
            // Cannot find path (pfCurrentState={pfCurrentState}) for unit {unit}");
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
                if (!m_debugPositions.ContainsKey(item.m_position.m_segment)) {
                    m_debugPositions[item.m_position.m_segment] = new List<ushort>();
                }
            }
#else
            bool debug = false;
            bool debugPed = false;
#endif
#if DEBUGNEWPF && DEBUG
            List<string> logBuf = null;
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

            if (item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
                prevLaneInfo = prevSegmentInfo.m_lanes[item.m_position.m_lane];
                prevIsPedestrianLane = prevLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian;
                prevIsBicycleLane = prevLaneInfo.m_laneType == NetInfo.LaneType.Vehicle
                                    && (prevLaneInfo.m_vehicleType & m_vehicleTypes)
                                    == VehicleInfo.VehicleType.Bicycle;
                prevIsCarLane =
                    (prevLaneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                    NetInfo.LaneType.None && (prevLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) !=
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
                    prevRelSimilarLaneIndex = prevLaneInfo.m_similarLaneCount - prevLaneInfo.m_similarLaneIndex - 1;
                }
            }

            var firstPrevSimilarLaneIndexFromInner = prevRelSimilarLaneIndex;
            var prevSegmentId = item.m_position.m_segment;
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
                var allowPedSwitch = (m_laneTypes & NetInfo.LaneType.Pedestrian) != 0;
                if (!prevIsElevated) {
                    // explore pedestrian lanes
                    int prevLaneIndex = item.m_position.m_lane;
                    if (nextNode.Info.m_class.m_service != ItemClass.Service.Beautification) {
                        if (allowPedSwitch) { // NON-STOCK CODE
                            var canCrossStreet = (nextNode.m_flags & (NetNode.Flags.End
                                                                      | NetNode.Flags.Bend
                                                                      | NetNode.Flags.Junction)) != NetNode.Flags.None;
                            var isOnCenterPlatform = prevIsCenterPlatform
                                                     && (nextNode.m_flags & (NetNode.Flags.End
                                                                             | NetNode.Flags.Junction))
                                                     == NetNode.Flags.None;
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
                                    netManager.m_segments.m_buffer[leftSegment].GetLeftAndRightLanes(
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
                                                      .m_segments.m_buffer[leftSegment].GetLeftSegment(nextNodeId);
                                    }

                                    if (++numIter == 8) {
                                        break;
                                    }
                                }

                                numIter = 0;
                                while (rightSegment != 0 && rightSegment != prevSegmentId && rightLaneId == 0u) {
                                    netManager.m_segments.m_buffer[rightSegment].GetLeftAndRightLanes(
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
                                        rightSegment = netManager.m_segments.m_buffer[rightSegment].GetRightSegment(nextNodeId);
                                    }

                                    if (++numIter == 8) {
                                        break;
                                    }
                                }
                            }

                            if (leftLaneId != 0u && (nextLeftSegment != prevSegmentId
                                                     || canCrossStreet
                                                     || isOnCenterPlatform)) {
#if DEBUGNEWPF
                                if (debugPed) {
                                    logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId} ({nextIsStartNode}): Exploring left segment\n" +
                                               $"\t_extPathType={queueItem.pathType}\n" +
                                               $"\t_vehicleTypes={m_vehicleTypes}, _laneTypes={m_laneTypes}\n" +
                                               $"\t_extVehicleType={queueItem.vehicleType}\n" +
                                               $"\t_isRoadVehicle={m_isRoadVehicle}\n" +
                                               $"\t_isHeavyVehicle={m_isHeavyVehicle}\n" +
                                               $"\t_stablePath={m_stablePath}\n" +
                                               $"\t_isLaneConnectionObeyingEntity" +
                                               $"={m_isLaneConnectionObeyingEntity}\n" +
                                               $"\t_isLaneArrowObeyingEntity={m_isLaneArrowObeyingEntity}\n\n" +
                                               $"\tnextIsStartNode={nextIsStartNode}\n" +
                                               $"\tnextLeftSegment={nextLeftSegment}\n" +
                                               $"\tleftLaneId={leftLaneId}\n" +
                                               $"\tmayCrossStreet={canCrossStreet}\n" +
                                               $"\tisOnCenterPlatform={isOnCenterPlatform}\n" +
                                               $"\tnextIsStartNode={nextIsStartNode}\n" +
                                               $"\tnextIsStartNode={nextIsStartNode}\n");
                                    FlushMainLog(logBuf, unitId);
                                }
#endif
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
#if DEBUGNEWPF
                                if (debugPed) {
                                    logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId} ({nextIsStartNode}): Exploring right segment\n" +
                                               $"\t_extPathType={queueItem.pathType}\n" +
                                               $"\t_vehicleTypes={m_vehicleTypes}, _laneTypes={m_laneTypes}\n" +
                                               $"\t_extVehicleType={queueItem.vehicleType}\n" +
                                               $"\t_isRoadVehicle={m_isRoadVehicle}\n" +
                                               $"\t_isHeavyVehicle={m_isHeavyVehicle}\n" +
                                               $"\t_stablePath={m_stablePath}\n" +
                                               $"\t_isLaneConnectionObeyingEntity" +
                                               $"={m_isLaneConnectionObeyingEntity}\n" +
                                               $"\t_isLaneArrowObeyingEntity={m_isLaneArrowObeyingEntity}\n\n" +
                                               $"\tnextIsStartNode={nextIsStartNode}\n" +
                                               $"\tnextRightSegment={nextRightSegment}\n" +
                                               $"\trightLaneId={rightLaneId}\n" +
                                               $"\tmayCrossStreet={canCrossStreet}\n" +
                                               $"\tisOnCenterPlatform={isOnCenterPlatform}\n" +
                                               $"\tnextIsStartNode={nextIsStartNode}\n");
                                    FlushMainLog(logBuf, unitId);
                                }
#endif
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
                        if ((m_vehicleTypes & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None
                            && prevSegment.GetClosestLane(
                                item.m_position.m_lane,
                                NetInfo.LaneType.Vehicle,
                                VehicleInfo.VehicleType.Bicycle,
                                out var nextLaneIndex,
                                out var nextLaneId)) {
#if DEBUGNEWPF
                            if (debugPed) {
                                logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId} ({nextIsStartNode}): Exploring bicycle switch\n" +
                                           $"\t_extPathType={queueItem.pathType}\n" +
                                           $"\t_vehicleTypes={m_vehicleTypes}, _laneTypes={m_laneTypes}\n" +
                                           $"\t_extVehicleType={queueItem.vehicleType}\n" +
                                           $"\t_isRoadVehicle={m_isRoadVehicle}\n" +
                                           $"\t_isHeavyVehicle={m_isHeavyVehicle}\n" +
                                           $"\t_stablePath={m_stablePath}\n" +
                                           $"\t_isLaneConnectionObeyingEntity={m_isLaneConnectionObeyingEntity}\n" +
                                           $"\t_isLaneArrowObeyingEntity={m_isLaneArrowObeyingEntity}\n\n" +
                                           $"\tnextIsStartNode={nextIsStartNode}\n" +
                                           $"\tnextLaneIndex={nextLaneIndex}\n" +
                                           $"\tnextLaneId={nextLaneId}\n" +
                                           $"\tnextIsStartNode={nextIsStartNode}\n");
                                FlushMainLog(logBuf, unitId);
                            }
#endif
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
                        if (queueItem.vehicleType == ExtVehicleType.PassengerCar) {
                            if ((item.m_lanesUsed & (NetInfo.LaneType.Vehicle
                                                     | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None) {
                                // if pocket cars are prohibited, a citizen may only park their car once per path
                                parkingAllowed = false;
                            } else if ((item.m_lanesUsed & NetInfo.LaneType.PublicTransport)
                                       == NetInfo.LaneType.None) {
                                // if the citizen is walking to their target (= no public transport used), the passenger car must be parked in the very last moment
                                parkingAllowed = item.m_laneID == m_endLaneA
                                                 || item.m_laneID == m_endLaneB;
                                /*if (_conf.Debug.Switches[4]) {
                                        Log._Debug($"Path unit {unitId}: public transport has not been used. ");
                                }*/
                            }
                        }
                    }

                    if (parkingAllowed) {
                        // NON-STOCK CODE END
                        var laneType = m_laneTypes & ~NetInfo.LaneType.Pedestrian;
                        var vehicleType = m_vehicleTypes & ~VehicleInfo.VehicleType.Bicycle;
                        if ((byte)(item.m_lanesUsed & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
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
                            if (m_randomParking) {
                                item2.m_comparisonValue += m_pathRandomizer.Int32(300u) / m_maxLength;
                            }
#if DEBUGNEWPF
                            if (debugPed) {
                                logBuf.Add(
                                    $"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId} ({nextIsStartNode}): Exploring parking switch\n" +
                                    $"\t_extPathType={queueItem.pathType}\n" +
                                    $"\t_vehicleTypes={m_vehicleTypes}, _laneTypes={m_laneTypes}\n" +
                                    $"\t_extVehicleType={queueItem.vehicleType}\n" +
                                    $"\t_isRoadVehicle={m_isRoadVehicle}\n" +
                                    $"\t_isHeavyVehicle={m_isHeavyVehicle}\n" +
                                    $"\t_stablePath={m_stablePath}\n" +
                                    $"\t_isLaneConnectionObeyingEntity={m_isLaneConnectionObeyingEntity}\n" +
                                    $"\t_isLaneArrowObeyingEntity={m_isLaneArrowObeyingEntity}\n\n" +
                                    $"\tnextIsStartNode={nextIsStartNode}\n" +
                                    $"\tnextLaneIndex2={nextLaneIndex2}\n" +
                                    $"\tnextlaneId2={nextlaneId2}\n" +
                                    $"\tnextIsStartNode={nextIsStartNode}\n");
                                FlushMainLog(logBuf, unitId);
                            }
#endif
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
                var allowPedestrian = (byte)(m_laneTypes & NetInfo.LaneType.Pedestrian) != 0; // allow pedestrian switching to vehicle?
                var nextIsBeautificationNode = nextNode.Info.m_class.m_service == ItemClass.Service.Beautification;
                var allowBicycle = false; // is true if cim may switch from a pedestrian lane to a bike lane
                byte parkingConnectOffset = 0;
                if (allowPedestrian) {
                    if (prevIsBicycleLane) {
                        // we are going to a bicycle lane
                        parkingConnectOffset = connectOffset;
                        allowBicycle = nextIsBeautificationNode;
                    } else if (m_vehicleLane != 0u) {
                        // there is a parked vehicle position
                        if (m_vehicleLane != item.m_laneID) {
                            // we have not reached the parked vehicle yet
                            allowPedestrian = false;
                        } else {
                            // pedestrian switches to parked vehicle
                            parkingConnectOffset = m_vehicleOffset;
                        }
                    } else if (m_stablePath) {
                        // enter a bus
                        parkingConnectOffset = 128;
                    } else {
                        // pocket car spawning
                        if (Options.parkingAI &&
                            queueItem.vehicleType == ExtVehicleType.PassengerCar
                            && (queueItem.pathType == ExtPathType.WalkingOnly
                             || (queueItem.pathType == ExtPathType.DrivingOnly
                                 && item.m_position.m_segment != m_startSegmentA
                                 && item.m_position.m_segment != m_startSegmentB))) {
                            allowPedestrian = false;
                        } else {
                            parkingConnectOffset = (byte)m_pathRandomizer.UInt32(1u, 254u);
                        }
                    }
                }

                if ((m_vehicleTypes & VehicleInfo.VehicleType.Ferry) != VehicleInfo.VehicleType.None) {
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
                        (m_vehicleTypes & (VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Monorail)) ==
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
                             m_isRoadVehicle && // only road vehicles may perform u-turns
                             junctionManager.IsUturnAllowed(prevSegmentId, nextIsStartNode) && // only do u-turns if allowed
                             !nextIsBeautificationNode && // no u-turns at beautification nodes // TODO refactor to JunctionManager
                             prevIsCarLane && // u-turns for road vehicles only
                             !m_isHeavyVehicle && // only small vehicles may perform u-turns
                             (nextIsTransitionOrJunction || nextIsBend) && // perform u-turns at transitions, junctions and bend nodes // TODO refactor to JunctionManager
                             !prevIsOutgoingOneWay); // do not u-turn on one-ways // TODO refactor to JunctionManager

                        isStrictLaneChangePolicyEnabled =
                            !nextIsBeautificationNode && // do not obey lane arrows at beautification nodes
                            !nextIsUntouchable &&
                            m_isLaneArrowObeyingEntity &&
                            //nextIsTransitionOrJunction && // follow lane arrows only at transitions and junctions
                            !(
#if DEBUG
                                 Options.allRelaxed || // debug option: all vehicle may ignore lane arrows
#endif
                                 (Options.relaxedBusses && queueItem.vehicleType == ExtVehicleType.Bus)); // option: busses may ignore lane arrows

                        /*if (! performCustomVehicleUturns) {
                                isUturnAllowedHere = false;
                        }*/
                        //isEntityAllowedToUturn = isEntityAllowedToUturn && !performCustomVehicleUturns;


#if DEBUGNEWPF
                        if (logLogic) {
                            logBuf.Add(
                                    $"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane} (id {item.m_laneID}), node {nextNodeId} ({nextIsStartNode}):\n" +
                                    $"\t_extPathType={queueItem.pathType}\n" +
                                    $"\t_vehicleTypes={m_vehicleTypes}, _laneTypes={m_laneTypes}\n" +
                                    $"\t_extVehicleType={queueItem.vehicleType}\n" +
                                    $"\t_isRoadVehicle={m_isRoadVehicle}\n" +
                                    $"\t_isHeavyVehicle={m_isHeavyVehicle}\n" +
                                    $"\t_vehicleLane={m_vehicleLane}\n" +
                                    $"\t_stablePath={m_stablePath}\n" +
                                    $"\t_isLaneConnectionObeyingEntity={m_isLaneConnectionObeyingEntity}\n" +
                                    $"\t_isLaneArrowObeyingEntity={m_isLaneArrowObeyingEntity}\n\n" +
                                    $"\tprevIsOutgoingOneWay={prevIsOutgoingOneWay}\n" +
                                    $"\tprevIsRouted={prevIsRouted}\n\n" +
                                    $"\tnextIsStartNode={nextIsStartNode}\n" +
                                    $"\tisNextBeautificationNode={nextIsBeautificationNode}\n" +
                                    $"\tnextIsTransitionOrJunction={nextIsTransitionOrJunction}\n" +
                                    $"\tnextIsBend={nextIsBend}\n" +
                                    $"\tnextIsUntouchable={nextIsUntouchable}\n" +
                                    $"\tallowBicycle={allowBicycle}\n" +
                                    $"\tisCustomUturnAllowed" +
                                    $"={junctionManager.IsUturnAllowed(prevSegmentId, nextIsStartNode)}\n" +
                                    $"\tisStrictLaneArrowPolicyEnabled={isStrictLaneChangePolicyEnabled}\n" +
                                    $"\tisEntityAllowedToUturn={isEntityAllowedToUturn}\n" +
                                    $"\tisUturnAllowedHere={isUturnAllowedHere}\n"
                                    //"\t" + $"performCustomVehicleUturns={performCustomVehicleUturns}\n"
                                );
                        }
#endif
                    } else {
#if DEBUGNEWPF
                        if (logLogic) {
                            logBuf.Add(
                                $"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, " +
                                $"node {nextNodeId} ({nextIsStartNode}):\n" +
                                $"\t_extPathType={queueItem.pathType}\n" +
                                $"\t_vehicleTypes={m_vehicleTypes}, _laneTypes={m_laneTypes}\n" +
                                $"\t_extVehicleType={queueItem.vehicleType}\n" +
                                $"\t_isRoadVehicle={m_isRoadVehicle}\n" +
                                $"\t_isHeavyVehicle={m_isHeavyVehicle}\n" +
                                $"\t_stablePath={m_stablePath}\n" +
                                $"\t_isLaneConnectionObeyingEntity={m_isLaneConnectionObeyingEntity}\n" +
                                $"\t_isLaneArrowObeyingEntity={m_isLaneArrowObeyingEntity}\n\n" +
                                $"\tprevIsRouted={prevIsRouted}\n\n");
                        }
#endif
                    }

                    if (allowBicycle || !prevIsRouted) {
                        /*
                         * pedestrian to bicycle lane switch or no routing information available:
                         * if pedestrian lanes should be explored (allowBicycle == true): do this here
                         * if previous segment has custom routing (prevIsRouted == true): do NOT explore
                         * vehicle lanes here, else: vanilla exploration of vehicle lanes
                        */
#if DEBUGNEWPF
                        if (logLogic) {
                            logBuf.Add(
                                $"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, " +
                                $"node {nextNodeId}:\n\t-> using DEFAULT exploration mode\n");
                            FlushMainLog(logBuf, unitId);
                        }
#endif

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
#if DEBUGNEWPF
                        if (logLogic) {
                            logBuf.Add(
                                $"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, " +
                                $"node {nextNodeId}:\n\t-> using CUSTOM exploration mode\n");
                        }
#endif
                        var canUseLane = CanUseLane(
                            logLogic,
                            item.m_position.m_segment,
                            prevSegmentInfo,
                            item.m_position.m_lane,
                            prevLaneInfo);

                        var laneTransitions = prevLaneEndRouting.transitions;
                        if (laneTransitions != null &&
                            (canUseLane || Options.vehicleRestrictionsAggression !=
                             VehicleRestrictionsAggression.Strict)) {

#if DEBUGNEWPF
                            if (logLogic) {
                                logBuf.Add(
                                    $"item: seg. {item.m_position.m_segment}, " +
                                    $"lane {item.m_position.m_lane}, node {nextNodeId}:\n\tCUSTOM exploration\n");
                            }
#endif
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

#if DEBUGNEWPF
                                if (logLogic) {
                                    logBuf.Add(
                                        $"item: seg. {item.m_position.m_segment}, " +
                                        $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                        $"\tapplied vehicle restrictions for vehicle {queueItem.vehicleId}, " +
                                        $"type {queueItem.vehicleType}:\n" +
                                        $"\t=> laneSelectionCost={laneSelectionCost}\n");
                                }
#endif
                            }

                            if (m_isRoadVehicle &&
                                prevLaneInfo != null &&
                                prevIsCarLane) {

                                if (Options.advancedAI) {
                                    laneChangingCostCalculationMode = LaneChangingCostCalculationMode.ByGivenDistance;
#if DEBUGNEWPF
                                    if (logLogic) {
                                        logBuf.Add(
                                            $"item: seg. {item.m_position.m_segment}, " +
                                            $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                            $"\tAI is active, prev is car lane and we are a car\n");
                                    }
#endif
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
                                    (prevLaneInfo.m_vehicleType & m_vehicleTypes) == VehicleInfo.VehicleType.Car
                                    && (netManager.m_segments.m_buffer[item.m_position.m_segment].m_flags &
                                     m_carBanMask) != NetSegment.Flags.None)
                                {
                                    // heavy vehicle ban / car ban ("Old Town" policy)
                                    if (laneSelectionCost == null) {
                                        laneSelectionCost = 1f;
                                    }
#if DEBUGNEWPF
                                    var oldLaneSelectionCost = laneSelectionCost;
#endif
                                    laneSelectionCost *= 7.5f;

#if DEBUGNEWPF
                                    if (logLogic) {
                                        logBuf.Add(
                                            $"item: seg. {item.m_position.m_segment}, " +
                                            $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                            $"\tapplied heavy vehicle ban / car ban ('Old Town' policy):\n" +
                                            $"\toldLaneSelectionCost={oldLaneSelectionCost}\n" +
                                            $"\t=> laneSelectionCost={laneSelectionCost}\n");
                                    }
#endif
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
                                    if ((queueItem.vehicleType & ExtVehicleType.Bus) != ExtVehicleType.None) {
                                        if (laneSelectionCost == null) {
                                            laneSelectionCost = 1f;
                                        }
#if DEBUGNEWPF
                                        var oldLaneSelectionCost = laneSelectionCost;
#endif
                                        laneSelectionCost *= m_conf.PathFinding.PublicTransportLaneReward; // (1)
#if DEBUGNEWPF
                                        if (logLogic) {
                                            logBuf.Add(
                                                $"item: seg. {item.m_position.m_segment}, " +
                                                $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                                $"\tapplied bus-on-transport lane reward:\n" +
                                                $"\toldLaneSelectionCost={oldLaneSelectionCost}\n" +
                                                $"\t=> laneSelectionCost={laneSelectionCost}\n");
                                        }
#endif
                                    } else if ((queueItem.vehicleType &
                                                (ExtVehicleType.RoadPublicTransport | ExtVehicleType.Service |
                                                 ExtVehicleType.Emergency)) == ExtVehicleType.None) {
                                        if (laneSelectionCost == null) {
                                            laneSelectionCost = 1f;
                                        }
#if DEBUGNEWPF
                                        var oldLaneSelectionCost = laneSelectionCost;
#endif
                                        laneSelectionCost *= m_conf.PathFinding.PublicTransportLanePenalty; // (2)
#if DEBUGNEWPF
                                        if (logLogic) {
                                            logBuf.Add(
                                                $"item: seg. {item.m_position.m_segment}, " +
                                                $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                                $"\tapplied car-on-transport lane penalty:\n" +
                                                $"\toldLaneSelectionCost={oldLaneSelectionCost}\n" +
                                                $"\t=> laneSelectionCost={laneSelectionCost}\n");
                                        }
#endif
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
                                    if (m_isHeavyVehicle &&
                                        Options.preferOuterLane &&
                                        prevSegmentRouting.highway &&
                                        m_pathRandomizer.Int32(m_conf.PathFinding.HeavyVehicleInnerLanePenaltySegmentSel) == 0
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
                                            1f + (m_conf.PathFinding.HeavyVehicleMaxInnerLanePenalty
                                            * prevRelOuterLane);
#if DEBUGNEWPF
                                        if (logLogic) {
                                            logBuf.Add(
                                                $"item: seg. {item.m_position.m_segment}, " +
                                                $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                                $"\tapplied inner lane penalty:\n" +
                                                $"\toldLaneSelectionCost={oldLaneSelectionCost}\n" +
                                                $"\t=> laneSelectionCost={laneSelectionCost}\n");
                                        }
#endif
                                    }

                                    /*
                                     * =======================================================================================================
                                     * (5) Apply costs for randomized lane selection in front of
                                     * junctions and highway transitions
                                     * =======================================================================================================
                                     */
                                    if (Options.advancedAI &&
                                        !m_stablePath &&
                                        !m_isHeavyVehicle &&
                                        nextIsJunction &&
                                        m_pathRandomizer.Int32(
                                            m_conf.AdvancedVehicleAI.LaneRandomizationJunctionSel) == 0)
                                    {
                                        // randomized lane selection at junctions
                                        if (laneSelectionCost == null) {
                                            laneSelectionCost = 1f;
                                        }
#if DEBUGNEWPF
                                        var oldLaneSelectionCost = laneSelectionCost;
#endif
                                        laneSelectionCost *=
                                            1f + (m_pathRandomizer.Int32(2) *
                                            m_conf.AdvancedVehicleAI.LaneRandomizationCostFactor);
#if DEBUGNEWPF
                                        if (logLogic) {
                                            logBuf.Add(
                                                $"item: seg. {item.m_position.m_segment}, " +
                                                $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                                $"\tapplied lane randomizations at junctions:\n" +
                                                $"\toldLaneSelectionCost={oldLaneSelectionCost}\n" +
                                                $"\t=> laneSelectionCost={laneSelectionCost}\n");
                                        }
#endif
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

                                    segmentSelectionCost *= 1f + m_conf.AdvancedVehicleAI.JunctionBaseCost;
                                }

                                /*
                                 * =======================================================================================================
                                 * (7) Apply traffic measurement costs for segment selection
                                 * =======================================================================================================
                                 */
                                if (Options.advancedAI &&
                                    (queueItem.vehicleType & ExtVehicleType.RoadVehicle & ~ExtVehicleType.Bus) !=
                                    ExtVehicleType.None && !m_stablePath)
                                {
                                    // segment selection based on segment traffic volume
                                    var prevFinalDir =
                                        nextIsStartNode ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;

                                    prevFinalDir =
                                        (prevSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None
                                            ? prevFinalDir
                                            : NetInfo.InvertDirection(prevFinalDir);

                                    var prevDirTrafficData =
                                        trafficMeasurementManager.SegmentDirTrafficData[
                                            trafficMeasurementManager.GetDirIndex(
                                                item.m_position.m_segment,
                                                prevFinalDir)];

                                    var segmentTraffic = Mathf.Clamp(
                                        1f - (prevDirTrafficData.meanSpeed /
                                        (float)TrafficMeasurementManager.REF_REL_SPEED) + item.m_trafficRand,
                                        0,
                                        1f);

                                    if (segmentSelectionCost == null) {
                                        segmentSelectionCost = 1f;
                                    }

                                    segmentSelectionCost *= 1f +
                                                            (m_conf.AdvancedVehicleAI.TrafficCostFactor *
                                                            segmentTraffic);

#if DEBUGNEWPF
                                    if (logLogic) {
                                        logBuf.Add(
                                            $"item: seg. {item.m_position.m_segment}, " +
                                            $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                            $"\tapplied traffic measurement costs for segment selection:\n" +
                                            $"\tsegmentTraffic={segmentTraffic}\n" +
                                            $"\t=> segmentSelectionCost={segmentSelectionCost}\n");
                                    }
#endif

                                    if (m_conf.AdvancedVehicleAI.LaneDensityRandInterval > 0 && nextIsRealJunction) {
                                        item.m_trafficRand =
                                            0.01f * (m_pathRandomizer.Int32(
                                                         (uint)m_conf.AdvancedVehicleAI.LaneDensityRandInterval
                                                         + 1u) - (m_conf.AdvancedVehicleAI.LaneDensityRandInterval
                                                                  / 2f));

#if DEBUGNEWPF
                                        if (logLogic) {
                                            logBuf.Add(
                                                $"item: seg. {item.m_position.m_segment}, " +
                                                $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                                $"\tupdated item.m_trafficRand:\n" +
                                                $"\t=> item.m_trafficRand={item.m_trafficRand}\n");
                                        }
#endif
                                    }
                                }

#if DEBUGNEWPF
                                if (logLogic) {
                                    logBuf.Add(
                                        $"item: seg. {item.m_position.m_segment}, " +
                                        $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                        $"\tcalculated traffic stats:\n" +
                                        $"\t_vehicleTypes={m_vehicleTypes}, _laneTypes={m_laneTypes}\n" +
                                        $"\t_extVehicleType={queueItem.vehicleType}\n" +
                                        $"\t_isRoadVehicle={m_isRoadVehicle}\n" +
                                        $"\t_isHeavyVehicle={m_isHeavyVehicle}\n" +
                                        $"\t_isLaneConnectionObeyingEntity={m_isLaneConnectionObeyingEntity}\n" +
                                        $"\t_isLaneArrowObeyingEntity={m_isLaneArrowObeyingEntity}\n\n" +
                                        $"\tlaneSelectionCost={laneSelectionCost}\n" +
                                        $"\tsegmentSelectionCost={segmentSelectionCost}\n");
                                }
#endif
                            }

                            for (var k = 0; k < laneTransitions.Length; ++k) {
                                var nextSegmentId = laneTransitions[k].segmentId;

                                if (nextSegmentId == 0) {
#if DEBUGNEWPF
                                    if (logLogic) {
                                        logBuf.Add(
                                                $"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                                "\t" + $"CUSTOM exploration\n" +
                                                "\t" + $"transition iteration {k}:\n" +
                                                "\t" + $"{laneTransitions[k].ToString()}\n" +
                                                "\t" + $"*** SKIPPING *** (nextSegmentId=0)\n"
                                            );
                                        FlushMainLog(logBuf, unitId);
                                    }
#endif
                                    continue;
                                }

                                var uturn = nextSegmentId == prevSegmentId;
                                if (uturn) {
                                    // prevent double/forbidden exploration of previous segment by vanilla code during this method execution
                                    if (! isEntityAllowedToUturn || ! isUturnAllowedHere) {
#if DEBUGNEWPF
                                        if (logLogic) {
                                            logBuf.Add(
                                                $"item: seg. {item.m_position.m_segment}, " +
                                                $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                                $"\tCUSTOM exploration\n" +
                                                $"\ttransition iteration {k}:\n" +
                                                $"\t{laneTransitions[k].ToString()}\n" +
                                                $"\t*** SKIPPING *** (u-turns prohibited)\n");
                                            FlushMainLog(logBuf, unitId);
                                        }
#endif
                                        continue;
                                    }
                                }

                                if (laneTransitions[k].type == LaneEndTransitionType.Invalid) {
#if DEBUGNEWPF
                                    if (logLogic) {
                                        logBuf.Add(
                                            $"item: seg. {item.m_position.m_segment}, " +
                                            $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                            $"\tCUSTOM exploration\n" +
                                            $"\ttransition iteration {k}:\n" +
                                            $"\t{laneTransitions[k].ToString()}\n" +
                                            $"\t*** SKIPPING *** (invalid transition)\n");
                                        FlushMainLog(logBuf, unitId);
                                    }
#endif
                                    continue;
                                }

                                // allow vehicles to ignore strict lane routing when moving off
                                var relaxedLaneChanging =
                                    m_isRoadVehicle &&
                                    (queueItem.vehicleType &
                                     (ExtVehicleType.Service | ExtVehicleType.PublicTransport |
                                      ExtVehicleType.Emergency)) != ExtVehicleType.None &&
                                    queueItem.vehicleId == 0 &&
                                    (laneTransitions[k].laneId == m_startLaneA ||
                                     laneTransitions[k].laneId == m_startLaneB);

                                if (!relaxedLaneChanging && isStrictLaneChangePolicyEnabled &&
                                    laneTransitions[k].type == LaneEndTransitionType.Relaxed) {
#if DEBUGNEWPF
                                    if (logLogic) {
                                        logBuf.Add(
                                            $"item: seg. {item.m_position.m_segment}, " +
                                            $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                            $"\tCUSTOM exploration\n" +
                                            $"\ttransition iteration {k}:\n" +
                                            $"\t{laneTransitions[k].ToString()}\n" +
                                            $"\trelaxedLaneChanging={relaxedLaneChanging}\n" +
                                            $"\tisStrictLaneChangePolicyEnabled={relaxedLaneChanging}\n" +
                                            $"\t*** SKIPPING *** (incompatible lane)\n");
                                        FlushMainLog(logBuf, unitId);
                                    }
#endif
                                    continue;
                                }

#if DEBUGNEWPF
                                if (logLogic) {
                                    logBuf.Add(
                                        $"item: seg. {item.m_position.m_segment}, " +
                                        $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                        $"\tCUSTOM exploration\n" +
                                        $"\ttransition iteration {k}:\n" +
                                        $"\t{laneTransitions[k].ToString()}\n" +
                                        $"\t> PERFORMING EXPLORATION NOW <\n");
                                    FlushMainLog(logBuf, unitId);
                                }
#endif

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
#if DEBUGNEWPF
                        if (logLogic) {
                            logBuf.Add(
                                $"path unit {unitId}\nitem: seg. {item.m_position.m_segment}, " +
                                $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                $"\t-> exploring DEFAULT u-turn\n");
                            FlushMainLog(logBuf, unitId);
                        }
#endif

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
                        item.m_position.m_lane,
                        NetInfo.LaneType.Pedestrian,
                        m_vehicleTypes,
                        out var nextLaneIndex,
                        out var nextLaneId)) {
#if DEBUGNEWPF
                        if (debugPed) {
                            logBuf.Add(
                                $"*PED* item: seg. {item.m_position.m_segment}, " +
                                $"lane {item.m_position.m_lane}, node {nextNodeId} ({nextIsStartNode}): " +
                                $"Exploring vehicle switch\n" +
                                $"\t_extPathType={queueItem.pathType}\n" +
                                $"\t_vehicleTypes={m_vehicleTypes}, _laneTypes={m_laneTypes}\n" +
                                $"\t_extVehicleType={queueItem.vehicleType}\n" +
                                $"\t_isRoadVehicle={m_isRoadVehicle}\n" +
                                $"\t_isHeavyVehicle={m_isHeavyVehicle}\n" +
                                $"\t_stablePath={m_stablePath}\n" +
                                $"\t_isLaneConnectionObeyingEntity={m_isLaneConnectionObeyingEntity}\n" +
                                $"\t_isLaneArrowObeyingEntity={m_isLaneArrowObeyingEntity}\n\n" +
                                $"\tnextIsStartNode={nextIsStartNode}\n" +
                                $"\tnextLaneIndex={nextLaneIndex}\n" +
                                $"\tnextLaneId={nextLaneId}\n" +
                                $"\tnextIsStartNode={nextIsStartNode}\n");
                            FlushMainLog(logBuf, unitId);
                        }
#endif
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

                if (nextSegmentId != 0 && nextSegmentId != item.m_position.m_segment) {
#if DEBUGNEWPF
                    if (logLogic) {
                        logBuf.Add(
                            $"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, " +
                            $"node {nextNodeId} ({nextIsStartNode}): " +
                            $"Exploring transport segment\n" +
                            $"\t_extPathType={queueItem.pathType}\n" +
                            $"\t_vehicleTypes={m_vehicleTypes}, _laneTypes={m_laneTypes}\n" +
                            $"\t_extVehicleType={queueItem.vehicleType}\n" +
                            $"\t_isRoadVehicle={m_isRoadVehicle}\n" +
                            $"\t_isHeavyVehicle={m_isHeavyVehicle}\n" +
                            $"\t_stablePath={m_stablePath}\n" +
                            $"\t_isLaneConnectionObeyingEntity={m_isLaneConnectionObeyingEntity}\n" +
                            $"\t_isLaneArrowObeyingEntity={m_isLaneArrowObeyingEntity}\n\n" +
                            $"\tnextNode.m_lane={nextNode.m_lane}\n" +
                            $"\tnextSegmentId={nextSegmentId}\n" +
                            $"\tnextIsStartNode={nextIsStartNode}\n");
                        FlushMainLog(logBuf, unitId);
                    }
#endif
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
            if ((nextSegment.m_flags & m_disableMask) != NetSegment.Flags.None) {
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
            if (item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
                var prevLaneInfo = prevSegmentInfo.m_lanes[item.m_position.m_lane];
                prevMaxSpeed = GetLaneSpeedLimit(
                    item.m_position.m_segment,
                    item.m_position.m_lane,
                    item.m_laneID,
                    prevLaneInfo); // NON-STOCK CODE
                prevLaneType = prevLaneInfo.m_laneType;
                if ((prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                    NetInfo.LaneType.None) {
                    prevLaneType = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
                }

                prevSpeed = CalculateLaneSpeed(
                    prevMaxSpeed,
                    connectOffset,
                    item.m_position.m_offset,
                    ref prevSegment,
                    prevLaneInfo); // NON-STOCK CODE
            }

            var segLength = prevLaneType == NetInfo.LaneType.PublicTransport
                                  ? netManager.m_lanes.m_buffer[item.m_laneID].m_length
                                  : Mathf.Max(SEGMENT_MIN_AVERAGE_LENGTH, prevSegment.m_averageLength);

            var offsetLength = Mathf.Abs(connectOffset - item.m_position.m_offset) * BYTE_TO_FLOAT_SCALE * segLength;
            var methodDistance = item.m_methodDistance + offsetLength;
            var comparisonValue = item.m_comparisonValue + (offsetLength / (prevSpeed * m_maxLength));
            var duration = (item.m_duration + offsetLength / prevMaxSpeed);
            var b = netManager.m_lanes.m_buffer[item.m_laneID]
                              .CalculatePosition(connectOffset * BYTE_TO_FLOAT_SCALE);

            if (! m_ignoreCost) {
                int ticketCost = netManager.m_lanes.m_buffer[item.m_laneID].m_ticketCost;
                if (ticketCost != 0) {
                    comparisonValue += ticketCost * m_pathRandomizer.Int32(2000u)
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
                    if (nextLaneInfo.CheckType(m_laneTypes, m_vehicleTypes)) {
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
                        nextItem.m_position.m_segment = nextSegmentId;
                        nextItem.m_position.m_lane = (byte)laneIndex;
                        nextItem.m_position.m_offset = offset;
                        if ((nextLaneInfo.m_laneType & prevLaneType) == NetInfo.LaneType.None) {
                            nextItem.m_methodDistance = 0f;
                        } else {
                            nextItem.m_methodDistance = methodDistance + distance;
                        }

                        var nextMaxSpeed = GetLaneSpeedLimit(
                            nextSegmentId,
                            (byte)laneIndex,
                            curLaneId,
                            nextLaneInfo); // NON-STOCK CODE

                        if (nextLaneInfo.m_laneType != NetInfo.LaneType.Pedestrian
                            || nextItem.m_methodDistance < m_conf.PathFinding.MaxWalkingDistance
                            || m_stablePath)
                        {
                            nextItem.m_comparisonValue =
                                comparisonValue + (distance / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * m_maxLength));
                            nextItem.m_duration = duration + (distance / ((prevMaxSpeed + nextMaxSpeed) * 0.5f));

                            nextItem.m_direction =
                                (nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None
                                    ? NetInfo.InvertDirection(nextLaneInfo.m_finalDirection)
                                    : nextLaneInfo.m_finalDirection;

                            if (nextLaneId == m_startLaneA) {
                                if (((nextItem.m_direction & NetInfo.Direction.Forward) == NetInfo.Direction.None ||
                                     nextItem.m_position.m_offset < m_startOffsetA) &&
                                    ((nextItem.m_direction & NetInfo.Direction.Backward) == NetInfo.Direction.None ||
                                     nextItem.m_position.m_offset > m_startOffsetA)) {
                                    return;
                                }

                                var nextSpeed = CalculateLaneSpeed(
                                    nextMaxSpeed,
                                    m_startOffsetA,
                                    nextItem.m_position.m_offset,
                                    ref nextSegment,
                                    nextLaneInfo); // NON-STOCK CODE
                                var nextOffsetDistance =
                                    Mathf.Abs(nextItem.m_position.m_offset - m_startOffsetA) * BYTE_TO_FLOAT_SCALE;
                                nextItem.m_comparisonValue +=
                                    nextOffsetDistance * nextSegment.m_averageLength / (nextSpeed * m_maxLength);
                                nextItem.m_duration += nextOffsetDistance * nextSegment.m_averageLength / nextSpeed;
                            }

                            if (nextLaneId == m_startLaneB) {
                                if (((nextItem.m_direction & NetInfo.Direction.Forward) == NetInfo.Direction.None ||
                                     nextItem.m_position.m_offset < m_startOffsetB) &&
                                    ((nextItem.m_direction & NetInfo.Direction.Backward) == NetInfo.Direction.None ||
                                     nextItem.m_position.m_offset > m_startOffsetB)) {
                                    return;
                                }

                                var nextSpeed = CalculateLaneSpeed(
                                    nextMaxSpeed,
                                    m_startOffsetB,
                                    nextItem.m_position.m_offset,
                                    ref nextSegment,
                                    nextLaneInfo); // NON-STOCK CODE
                                var nextOffsetDistance =
                                    Mathf.Abs(nextItem.m_position.m_offset - m_startOffsetB) * BYTE_TO_FLOAT_SCALE;
                                nextItem.m_comparisonValue +=
                                    nextOffsetDistance * nextSegment.m_averageLength / (nextSpeed * m_maxLength);
                                nextItem.m_duration += nextOffsetDistance * nextSegment.m_averageLength / nextSpeed;
                            }

                            nextItem.m_laneID = nextLaneId;
                            nextItem.m_lanesUsed = item.m_lanesUsed | nextLaneInfo.m_laneType;
                            nextItem.m_vehiclesUsed = item.m_vehiclesUsed | nextLaneInfo.m_vehicleType;
                            nextItem.m_trafficRand = 0;
#if DEBUGNEWPF
                            if (debug) {
                                m_debugPositions[item.m_position.m_segment].Add(nextItem.m_position.m_segment);
                            }
#endif
                            AddBufferItem(nextItem, item.m_position);
                        }
                    }

                    return;
                }

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                ++laneIndex;
            }
        }

        private bool ProcessItemCosts(bool debug,
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
                debug,
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
        private bool ProcessItemCosts(bool debug,
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
            debug = debug && DebugSwitch.RoutingBasicLog.Get();
#else
			debug = false;
#endif

#if DEBUGNEWPF && DEBUG
            List<string> logBuf = null;
            if (debug)
                logBuf = new List<string>();
#endif

            foundForced = false;
            var blocked = false;
            if ((nextSegment.m_flags & m_disableMask) != NetSegment.Flags.None) {
#if DEBUGNEWPF
                if (debug) {
                    logBuf.Add($"Segment is PathFailed or flooded: {nextSegment.m_flags}");
                    logBuf.Add("-- method returns --");
                    FlushCostLog(logBuf);
                }
#endif
                return blocked;
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

#if DEBUGNEWPF
            if (debug)
                logBuf.Add(
                    $"isStockLaneChangerUsed={Options.isStockLaneChangerUsed()}, " +
                    $"_extVehicleType={queueItem.vehicleType}, " +
                    $"nonBus={(queueItem.vehicleType & ExtVehicleType.RoadVehicle & ~ExtVehicleType.Bus) != ExtVehicleType.None}, " +
                    $"_stablePath={m_stablePath}, enablePedestrian={enablePedestrian}, " +
                    $"enableVehicle={enableVehicle}");
#endif

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
            bool prevIsRealJunction = prevIsJunction &&
                                      (netManager.m_nodes.m_buffer[sourceNodeId].m_flags & (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)) != (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut);
#endif
            /*bool nextIsRealJunction = (netManager.m_nodes.m_buffer[nextNodeId].m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) == NetNode.Flags.Junction &&
                                                              (netManager.m_nodes.m_buffer[nextNodeId].m_flags & (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)) != (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut);*/
            var prevOuterSimilarLaneIndex = -1;
            // NON-STOCK CODE END //
            if (item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
                var prevLaneInfo = prevSegmentInfo.m_lanes[item.m_position.m_lane];
                prevLaneType = prevLaneInfo.m_laneType;
                prevVehicleType = prevLaneInfo.m_vehicleType;

                prevMaxSpeed = GetLaneSpeedLimit(
                    item.m_position.m_segment,
                    item.m_position.m_lane,
                    item.m_laneID,
                    prevLaneInfo); // NON-STOCK CODE

                prevLaneSpeed = CalculateLaneSpeed(
                    prevMaxSpeed,
                    connectOffset,
                    item.m_position.m_offset,
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
                    Vector3 prevDirection;
                    if (nextNodeId == prevSegment.m_startNode) {
                        prevDirection = prevSegment.m_startDirection;
                    } else {
                        prevDirection = prevSegment.m_endDirection;
                    }

                    Vector3 nextDirection;
                    if ((nextDir & NetInfo.Direction.Forward) != NetInfo.Direction.None) {
                        nextDirection = nextSegment.m_endDirection;
                    } else {
                        nextDirection = nextSegment.m_startDirection;
                    }

                    var dirDotProd = (prevDirection.x * nextDirection.x) + (prevDirection.z * nextDirection.z);
                    if (dirDotProd >= turningAngle) {
#if DEBUGNEWPF
                        if (debug) {
                            logBuf.Add($"turningAngle < 1f! dirDotProd={dirDotProd} >= " +
                                       $"turningAngle{turningAngle}!");
                            logBuf.Add("-- method returns --");
                            FlushCostLog(logBuf);
                        }
#endif
                        return blocked;
                    }
                }
            }

            var prevDist = prevLaneType == NetInfo.LaneType.PublicTransport
                               ? netManager.m_lanes.m_buffer[item.m_laneID].m_length
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
#if DEBUGNEWPF
            if (debug) {
                logBuf.Add(
                    $"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, " +
                    $"node {nextNodeId}:\n\tapplied traffic cost factors:\n\toldPrevCost={oldPrevCost}\n" +
                    $"\t=> prevCost={prevCost}\n");
            }
#endif

            // stock code check for vehicle ban policies removed
            // stock code for transport lane usage control removed

            // calculate ticket costs
            var ticketCosts = 0f;
            if (!m_ignoreCost) {
                int ticketCost = netManager.m_lanes.m_buffer[item.m_laneID].m_ticketCost;
                if (ticketCost != 0) {
                    ticketCosts += ticketCost * m_pathRandomizer.Int32(2000u) * BYTE_TO_FLOAT_SCALE * 0.0001f;
                }
            }

            if ((prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                NetInfo.LaneType.None) {
                prevLaneType = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
            }

            var prevOffsetCost = Mathf.Abs(connectOffset - item.m_position.m_offset) * BYTE_TO_FLOAT_SCALE * prevCost;
            var prevMethodDist = item.m_methodDistance +
                                 (Mathf.Abs(connectOffset - item.m_position.m_offset) * BYTE_TO_FLOAT_SCALE
                                                                                      * prevDist);
            var prevDuration = item.m_duration + (prevOffsetCost / prevMaxSpeed);

            // NON-STOCK: vehicle restriction are applied to previous segment length in MainPathFind (not here, and not to prevOffsetCost)
            var prevComparisonPlusOffsetCostOverSpeed =
                item.m_comparisonValue + (prevOffsetCost / (prevLaneSpeed * m_maxLength));

            if (!m_stablePath) {
                // CO randomization. Only randomizes over segments, not over lanes.
                if (segmentSelectionCost == null) {
                    // NON-STOCK CODE
                    var randomizer = new Randomizer(m_pathFindIndex << 16 | item.m_position.m_segment);
                    prevOffsetCost *= ((randomizer.Int32(900, 1000 + (prevSegment.m_trafficDensity * 10))
                                        + m_pathRandomizer.Int32(20u)) * 0.001f);
                }
            }

            var prevLaneConnectPos = netManager.m_lanes.m_buffer[item.m_laneID]
                                               .CalculatePosition(connectOffset * BYTE_TO_FLOAT_SCALE);
            var newLaneIndexFromInner = laneIndexFromInner;
            var transitionNode = (netManager.m_nodes.m_buffer[nextNodeId].m_flags & NetNode.Flags.Transition) !=
                                 NetNode.Flags.None;
            var allowedLaneTypes = m_laneTypes;
            var allowedVehicleTypes = m_vehicleTypes;

            if (!enableVehicle) {
                allowedVehicleTypes &= VehicleInfo.VehicleType.Bicycle;
                if (allowedVehicleTypes == VehicleInfo.VehicleType.None) {
                    allowedLaneTypes &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
                }
            }

            if (!enablePedestrian) {
                allowedLaneTypes &= ~NetInfo.LaneType.Pedestrian;
            }

#if DEBUGNEWPF
            if (debug) {
                logBuf.Add($"allowedVehicleTypes={allowedVehicleTypes} allowedLaneTypes={allowedLaneTypes}");
            }
#endif

            // NON-STOCK CODE START //
            var laneChangeBaseCosts = 1f;
            var junctionBaseCosts = 1f;
            if (laneChangingCostCalculationMode != LaneChangingCostCalculationMode.None) {
                var rand = m_pathRandomizer.Int32(101u) / 100f;
                laneChangeBaseCosts = m_conf.AdvancedVehicleAI.LaneChangingBaseMinCost + (rand *
                                      (m_conf.AdvancedVehicleAI.LaneChangingBaseMaxCost -
                                       m_conf.AdvancedVehicleAI.LaneChangingBaseMinCost));
                if (prevIsJunction) {
                    junctionBaseCosts = m_conf.AdvancedVehicleAI.LaneChangingJunctionBaseCost;
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
#if DEBUGNEWPF
                    if (debug) {
                        logBuf.Add($"forceLaneIndex break! laneIndex={laneIndex}");
                    }
#endif
                    break;
                }

                // NON-STOCK CODE END //
                var nextLaneInfo = nextSegmentInfo.m_lanes[laneIndex];

                if ((byte)(nextLaneInfo.m_finalDirection & nextFinalDir) != 0) {
                    // lane direction is compatible
#if DEBUGNEWPF
                    if (debug) {
                        logBuf.Add($"Lane direction check passed: {nextLaneInfo.m_finalDirection}");
                    }
#endif
                    var nextLaneCheckType = nextLaneInfo.CheckType(allowedLaneTypes, allowedVehicleTypes);
                    var next1 = nextSegmentId != item.m_position.m_segment
                                || laneIndex != (int)item.m_position.m_lane;
                    if (nextLaneCheckType && next1) {
                        // vehicle types match and no u-turn to the previous lane
#if DEBUGNEWPF
                        if (debug) {
                            logBuf.Add(
                                $"vehicle type check passed: {nextLaneCheckType} && {next1}");
                        }
#endif

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

#if DEBUGNEWPF
                        if (debug) {
                            logBuf.Add(
                                $"costs from {nextSegmentId} (off " +
                                $"{(byte)((nextDir & NetInfo.Direction.Forward) == 0 ? 0 : 255)}) to " +
                                $"{item.m_position.m_segment} (off {item.m_position.m_offset}), " +
                                $"connectOffset={connectOffset}: transitionCost={transitionCost}");
                        }
#endif

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
                        nextItem.m_comparisonValue = ticketCosts;
                        nextItem.m_position.m_segment = nextSegmentId;
                        nextItem.m_position.m_lane = (byte)laneIndex;
                        nextItem.m_position.m_offset = (byte)((nextDir & NetInfo.Direction.Forward) == 0 ? 0 : 255);
                        if ((nextLaneInfo.m_laneType & prevLaneType) == NetInfo.LaneType.None) {
                            nextItem.m_methodDistance = 0f;

                            // NON-STOCK CODE START
                            if (Options.realisticPublicTransport && isMiddle &&
                                nextLaneInfo.m_laneType == NetInfo.LaneType.PublicTransport &&
                                (item.m_lanesUsed & NetInfo.LaneType.PublicTransport) != NetInfo.LaneType.None) {

                                // apply penalty when switching public transport vehicles
                                var transportTransitionPenalty =
                                    (m_conf.PathFinding.PublicTransportTransitionMinPenalty +
                                     (netManager.m_nodes.m_buffer[nextNodeId].m_maxWaitTime * BYTE_TO_FLOAT_SCALE *
                                     (m_conf.PathFinding.PublicTransportTransitionMaxPenalty -
                                      m_conf.PathFinding.PublicTransportTransitionMinPenalty))) / (0.25f * m_maxLength);
#if DEBUGNEWPF
                                if (debug) {
                                    logBuf.Add(
                                        $"applying public transport transition penalty: " +
                                        $"{transportTransitionPenalty}");
                                }
#endif
                                nextItem.m_comparisonValue += transportTransitionPenalty;
                            }

                            // NON-STOCK CODE END
                        } else {
                            nextItem.m_methodDistance = prevMethodDist + transitionCost;
                        }

                        var nextLaneNotPed = nextLaneInfo.m_laneType != NetInfo.LaneType.Pedestrian;
                        var closeToWalk = nextItem.m_methodDistance < m_conf.PathFinding.MaxWalkingDistance;
#if DEBUGNEWPF
                        if (debug) {
                            logBuf.Add(
                                $"checking if methodDistance is in range: " +
                                $"{nextLaneNotPed} || {closeToWalk} ({nextItem.m_methodDistance})");
                        }
#endif
                        if (nextLaneNotPed || closeToWalk || m_stablePath) {
                            // NON-STOCK CODE START //
                            if (laneChangingCostCalculationMode == LaneChangingCostCalculationMode.None) {
                                var transitionCostOverMeanMaxSpeed =
                                    transitionCost / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * m_maxLength);
                                nextItem.m_comparisonValue +=
                                    prevComparisonPlusOffsetCostOverSpeed +
                                    transitionCostOverMeanMaxSpeed; // stock code
                            } else {
                                nextItem.m_comparisonValue += item.m_comparisonValue;

                                // customDeltaCost now holds the costs for driving on the segment + costs
                                // for changing the segment
                                customDeltaCost = transitionCost + prevOffsetCost;
#if DEBUGNEWPF
                                if (debug) {
                                    logBuf.Add(
                                        $"Path from {nextSegmentId} (idx {laneIndex}, id {curLaneId}) " +
                                        $"to {item.m_position.m_segment} (lane {prevOuterSimilarLaneIndex} " +
                                        $"from outer, idx {item.m_position.m_lane}): " +
                                        $"laneChangingCostCalculationMode={laneChangingCostCalculationMode}, " +
                                        $"transitionCost={transitionCost}");
                                }
#endif
                            }

                            nextItem.m_duration =
                                prevDuration + (transitionCost / ((prevMaxSpeed + nextMaxSpeed) * 0.5f));

                            // account for public tranport transition costs on non-PT paths
                            if (
#if DEBUG
                                !DebugSwitch.RealisticPublicTransport.Get() &&
#endif
                                Options.realisticPublicTransport &&
                                (curLaneId == m_startLaneA || curLaneId == m_startLaneB) &&
                                (item.m_lanesUsed & (NetInfo.LaneType.Pedestrian | NetInfo.LaneType.PublicTransport)) ==
                                NetInfo.LaneType.Pedestrian) {
                                var transportTransitionPenalty =
                                    (2f * m_conf.PathFinding.PublicTransportTransitionMaxPenalty) /
                                    (0.25f * m_maxLength);
#if DEBUGNEWPF
                                if (debug) {
                                    logBuf.Add(
                                        $"applying public transport transition penalty on non-PT path: " +
                                        $"{transportTransitionPenalty}");
                                }
#endif
                                nextItem.m_comparisonValue += transportTransitionPenalty;
                            }

                            // NON-STOCK CODE END
                            nextItem.m_direction = nextDir;
                            if (curLaneId == m_startLaneA) {
                                if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 ||
                                     nextItem.m_position.m_offset < m_startOffsetA) &&
                                    ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 ||
                                     nextItem.m_position.m_offset > m_startOffsetA)) {
#if DEBUGNEWPF
                                    if (debug) {
                                        logBuf.Add($"Current lane is start lane A. goto next lane");
                                    }
#endif
                                    goto CONTINUE_LANE_LOOP;
                                }

                                var nextLaneSpeed = CalculateLaneSpeed(
                                    nextMaxSpeed,
                                    m_startOffsetA,
                                    nextItem.m_position.m_offset,
                                    ref nextSegment,
                                    nextLaneInfo); // NON-STOCK CODE

                                var nextOffset = Mathf.Abs(nextItem.m_position.m_offset - m_startOffsetA) *
                                                 BYTE_TO_FLOAT_SCALE;
                                var nextSegLength = Mathf.Max(SEGMENT_MIN_AVERAGE_LENGTH,
                                                              nextSegment.m_averageLength);
                                nextItem.m_comparisonValue +=
                                    nextOffset * nextSegLength / (nextLaneSpeed * m_maxLength);
                                nextItem.m_duration += nextOffset * nextSegLength / nextLaneSpeed;
                            }

                            if (curLaneId == m_startLaneB) {
                                if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 ||
                                     nextItem.m_position.m_offset < m_startOffsetB) &&
                                    ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 ||
                                     nextItem.m_position.m_offset > m_startOffsetB)) {
#if DEBUGNEWPF
                                    if (debug)
                                        logBuf.Add($"Current lane is start lane B. goto next lane");
#endif
                                    goto CONTINUE_LANE_LOOP;
                                }

                                var nextLaneSpeed = CalculateLaneSpeed(
                                    nextMaxSpeed,
                                    m_startOffsetB,
                                    nextItem.m_position.m_offset,
                                    ref nextSegment,
                                    nextLaneInfo); // NON-STOCK CODE
                                var nextOffset = Mathf.Abs(nextItem.m_position.m_offset - m_startOffsetB) *
                                                 BYTE_TO_FLOAT_SCALE;
                                var nextSegLength = Mathf.Max(SEGMENT_MIN_AVERAGE_LENGTH, nextSegment.m_averageLength);
                                nextItem.m_comparisonValue +=
                                    nextOffset * nextSegLength / (nextLaneSpeed * m_maxLength);
                                nextItem.m_duration += nextOffset * nextSegLength / nextLaneSpeed;
                            }

                            if (!m_ignoreBlocked &&
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
#if DEBUGNEWPF
                                    if (debug) {
                                        logBuf.Add(
                                            $"item: seg. {item.m_position.m_segment}, " +
                                            $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                            $"\tapplied blocked road cost factor on activated AI:\n" +
                                            $"\toldCustomDeltaCost={oldCustomDeltaCost}\n" +
                                            $"\t=> customDeltaCost={customDeltaCost}\n");
                                    }
#endif
                                } else {
                                    // NON-STOCK CODE END //
#if DEBUGNEWPF
                                    if (debug) {
                                        logBuf.Add($"Applying blocked road cost factor on disabled advanced AI");
                                    }
#endif
                                    nextItem.m_comparisonValue += 0.1f;
                                }

                                blocked = true;
                            }

                            if ((byte)(nextLaneInfo.m_laneType & prevLaneType) != 0 &&
                                nextLaneInfo.m_vehicleType == prevVehicleType) {
#if DEBUGNEWPF
                                if (debug) {
                                    logBuf.Add(
                                        $"Applying stock lane changing costs. " +
                                        $"obeyStockLaneArrows={obeyStockLaneArrows}");
                                }
#endif

                                if (obeyStockLaneArrows) {
                                    // this is CO's way of matching lanes between segments
                                    int firstTarget = netManager.m_lanes.m_buffer[curLaneId].m_firstTarget;
                                    int lastTarget = netManager.m_lanes.m_buffer[curLaneId].m_lastTarget;
                                    if (laneIndexFromInner < firstTarget || laneIndexFromInner >= lastTarget) {
                                        nextItem.m_comparisonValue +=
                                            Mathf.Max(1f, (transitionCost * 3f) - 3f) /
                                            ((prevMaxSpeed + nextMaxSpeed) * 0.5f * m_maxLength);
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
#if DEBUGNEWPF
                                if (debug) {
                                    logBuf.Add($"Calculating advanced AI lane changing costs");
                                }
#endif

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
                                    queueItem.vehicleId == 0 &&
                                    (curLaneId == m_startLaneA || curLaneId == m_startLaneB);
                                if (laneDist > 0 && !relaxedLaneChanging) {
                                    laneMetric = 1f + (laneDist *
                                                 junctionBaseCosts *
                                                 laneChangeBaseCosts * // road type based lane changing cost factor
                                                 (laneDist > 1
                                                      ? m_conf.AdvancedVehicleAI.MoreThanOneLaneChangingCostFactor
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
                                                     ((prevMaxSpeed + nextMaxSpeed) * 0.5f * m_maxLength);
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

                                nextItem.m_comparisonValue += finalDeltaCost;

                                if (nextItem.m_comparisonValue > 1f) {
                                    // comparison value got too big. Do not add the lane to the buffer
                                    addItem = false;
                                }
#if DEBUGNEWPF
                                if (debug) {
                                    logBuf.Add(
                                        $"item: seg. {item.m_position.m_segment}, " +
                                        $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
                                        $"-> TRANSIT to seg. {nextSegmentId}, lane {laneIndex}\n" +
                                        $"\tprevMaxSpeed={prevMaxSpeed}\n" +
                                        $"\tnextMaxSpeed={nextMaxSpeed}\n\n" +
                                        $"\tlaneChangingCostCalculationMode={laneChangingCostCalculationMode}\n\n" +
                                        $"\tlaneDist={laneDist}\n\n" +
                                        $"\t_extVehicleType={queueItem.vehicleType}\n" +
                                        $"\tlaneChangeRoadBaseCost={laneChangeBaseCosts}\n" +
                                        $"\tmoreThanOneLaneCost={(laneDist > 1 ? m_conf.AdvancedVehicleAI.MoreThanOneLaneChangingCostFactor : 1f)}\n" +
                                        $"\t=> laneMetric={laneMetric}\n" +
                                        $"\t=> metric={metric}\n" +
                                        $"\tdeltaCostOverMeanMaxSpeed={finalDeltaCost}\n" +
                                        $"\tnextItem.m_comparisonValue={nextItem.m_comparisonValue}\n\n" +
                                        $"\t=> addItem={addItem}\n");
                                }
#endif
                            }

                            if (forcedLaneIndex != null && laneIndex == forcedLaneIndex && addItem) {
                                foundForced = true;
                            }

                            if (addItem) {
                                // NON-STOCK CODE END
                                nextItem.m_lanesUsed = item.m_lanesUsed | nextLaneInfo.m_laneType;
                                nextItem.m_vehiclesUsed = item.m_vehiclesUsed | nextLaneInfo.m_vehicleType;
                                nextItem.m_laneID = curLaneId;
                                nextItem.m_trafficRand = item.m_trafficRand;
#if DEBUGNEWPF
                                if (debug) {
                                    logBuf.Add(
                                        $"adding item: seg {nextItem.m_position.m_segment}, " +
                                        $"lane {nextItem.m_position.m_lane} (idx {nextItem.m_laneID}), " +
                                        $"off {nextItem.m_position.m_offset} -> seg {item.m_position.m_segment}, " +
                                        $"lane {item.m_position.m_lane} (idx {item.m_laneID}), " +
                                        $"off {item.m_position.m_offset}, cost {nextItem.m_comparisonValue}, " +
                                        $"previous cost {item.m_comparisonValue}, " +
                                        $"methodDist {nextItem.m_methodDistance}");
                                    m_debugPositions[item.m_position.m_segment].Add(nextItem.m_position.m_segment);
                                }
#endif

                                AddBufferItem(nextItem, item.m_position);

                                // NON-STOCK CODE START
                            } else {
#if DEBUGNEWPF
                                if (debug) {
                                    logBuf.Add(
                                        $"item: seg. {item.m_position.m_segment}, " +
                                        $"lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
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

#if DEBUGNEWPF
            if (debug) {
                logBuf.Add("-- method returns --");
                FlushCostLog(logBuf);
            }
#endif
            return blocked;
        }

#if DEBUGNEWPF
        private void FlushCostLog(List<string> logBuf) {
            if (logBuf == null) {
                return;
            }

            foreach (var toLog in logBuf) {
                Log._Debug($"Pathfinder ({m_pathFindIndex}) for unit {Calculating} *COSTS*: " + toLog);
            }

            logBuf.Clear();
        }

        [UsedImplicitly]
        private void FlushMainLog(List<string> logBuf, uint unitId) {
            if (logBuf == null) {
                return;
            }

            foreach (var toLog in logBuf) {
                Log._Debug($"Pathfinder ({m_pathFindIndex}) for unit {Calculating} *MAIN*: " + toLog);
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
                    $"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, " +
                    $"node {nextNodeId}: exploring\n\tnextSegmentId={nextSegmentId}\n" +
                    $"\tconnectOffset={connectOffset}\n\tlaneSwitchOffset={laneSwitchOffset}\n" +
                    $"\tnextLaneIndex={nextLaneIndex}\n\tnextLaneId={nextLaneId}\n");
            }
#endif

            if ((nextSegment.m_flags & m_disableMask) != NetSegment.Flags.None) {
#if DEBUGNEWPF
                if (debug) {
                    logBuf.Add(
                        $"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, " +
                        $"node {nextNodeId}: -NOT ADDING- next segment disabled mask is incompatible!\n" +
                        $"\tnextSegment.m_flags={nextSegment.m_flags}\n\t_disableMask={m_disableMask}\n");
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
                if (!junctionManager.IsPedestrianCrossingAllowed(nextSegmentId, nextIsStartNode)) {
#if DEBUGNEWPF
                    if (debug) {
                        logBuf.Add(
                            $"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, " +
                            $"node {nextNodeId}: -NOT ADDING- pedestrian crossing not allowed!\n");
                        FlushCostLog(logBuf);
                    }
#endif
                    return;
                }

                // check if pedestrian light won't change to green
                var lights = customTrafficLightsManager.GetSegmentLights(nextSegmentId, nextIsStartNode, false);
                if (lights != null) {
                    if (lights.InvalidPedestrianLight) {
#if DEBUGNEWPF
                        if (debug) {
                            logBuf.Add(
                                $"*PED* item: seg. {item.m_position.m_segment}, " +
                                $"lane {item.m_position.m_lane}, node {nextNodeId}: " +
                                $"-NOT ADDING- invalid pedestrian lights!\n");
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
            var b = netManager.m_lanes.m_buffer[item.m_laneID]
                              .CalculatePosition(laneSwitchOffset * BYTE_TO_FLOAT_SCALE);
            if (nextSegmentId == item.m_position.m_segment) {
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
            if (item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
                var prevLaneInfo = prevSegmentInfo.m_lanes[item.m_position.m_lane];
                prevMaxSpeed = GetLaneSpeedLimit(
                    item.m_position.m_segment,
                    item.m_position.m_lane,
                    item.m_laneID,
                    prevLaneInfo); // SpeedLimitManager.GetLockFreeGameSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, ref lane2); // NON-STOCK CODE
                laneType = prevLaneInfo.m_laneType;
                vehicleType = prevLaneInfo.m_vehicleType; // NON-STOCK CODE
                if ((byte)(laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
                    laneType = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
                }

                prevSpeed = CalculateLaneSpeed(
                    prevMaxSpeed,
                    laneSwitchOffset,
                    item.m_position.m_offset,
                    ref prevSegment,
                    prevLaneInfo); // NON-STOCK CODE
            }

            float segLength;
            if (laneType == NetInfo.LaneType.PublicTransport) {
                segLength = netManager.m_lanes.m_buffer[item.m_laneID].m_length;
            } else {
                segLength = Mathf.Max(SEGMENT_MIN_AVERAGE_LENGTH, prevSegment.m_averageLength);
            }

            var offsetLength = Mathf.Abs(laneSwitchOffset - item.m_position.m_offset) * BYTE_TO_FLOAT_SCALE * segLength;
            var methodDistance = item.m_methodDistance + offsetLength;
            var comparisonValue = item.m_comparisonValue + (offsetLength / (prevSpeed * m_maxLength));
            var duration = (item.m_duration + offsetLength / prevMaxSpeed);

            if (!m_ignoreCost) {
                int ticketCost = netManager.m_lanes.m_buffer[item.m_laneID].m_ticketCost;
                if (ticketCost != 0) {
                    comparisonValue += ticketCost * m_pathRandomizer.Int32(2000u) * BYTE_TO_FLOAT_SCALE * 0.0001f;
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
                nextItem.m_position.m_segment = nextSegmentId;
                nextItem.m_position.m_lane = (byte)nextLaneIndex;
                nextItem.m_position.m_offset = offset;
                if ((nextLaneInfo.m_laneType & laneType) == NetInfo.LaneType.None) {
                    nextItem.m_methodDistance = 0f;
                } else {
                    // Use tolerance instead of comparing to 0f
                    if (Math.Abs(item.m_methodDistance) < 0.001f) {
                        comparisonValue += 100f / (0.25f * m_maxLength);
                    }

                    nextItem.m_methodDistance = methodDistance + distance;
                }

                var nextMaxSpeed = GetLaneSpeedLimit(
                    nextSegmentId,
                    (byte)nextLaneIndex,
                    nextLaneId,
                    nextLaneInfo); // NON-STOCK CODE

                if (nextLaneInfo.m_laneType != NetInfo.LaneType.Pedestrian ||
                    nextItem.m_methodDistance < m_conf.PathFinding.MaxWalkingDistance || m_stablePath) {
                    nextItem.m_comparisonValue =
                        comparisonValue + (distance / ((prevMaxSpeed + nextMaxSpeed) * 0.25f * m_maxLength));
                    nextItem.m_duration = duration + (distance / ((prevMaxSpeed + nextMaxSpeed) * 0.5f));

                    nextItem.m_direction = (nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None
                                               ? NetInfo.InvertDirection(nextLaneInfo.m_finalDirection)
                                               : nextLaneInfo.m_finalDirection;

                    if (nextLaneId == m_startLaneA) {
                        if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 ||
                             nextItem.m_position.m_offset < m_startOffsetA) &&
                            ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 ||
                             nextItem.m_position.m_offset > m_startOffsetA)) {
#if DEBUGNEWPF
                            if (debug) {
                                logBuf.Add(
                                    $"*PED* item: seg. {item.m_position.m_segment}, " +
                                    $"lane {item.m_position.m_lane}, node {nextNodeId}: " +
                                    $"-NOT ADDING- start lane A reached in wrong direction!\n");
                                FlushCostLog(logBuf);
                            }
#endif
                            return;
                        }

                        var nextSpeed = CalculateLaneSpeed(
                            nextMaxSpeed,
                            m_startOffsetA,
                            nextItem.m_position.m_offset,
                            ref nextSegment,
                            nextLaneInfo); // NON-STOCK CODE
                        var nextOffset = Mathf.Abs(nextItem.m_position.m_offset - m_startOffsetA) * BYTE_TO_FLOAT_SCALE;
                        nextItem.m_comparisonValue +=
                            nextOffset * nextSegment.m_averageLength / (nextSpeed * m_maxLength);
                        nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextSpeed;
                    }

                    if (nextLaneId == m_startLaneB) {
                        if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0
                             || nextItem.m_position.m_offset < m_startOffsetB)
                            && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0
                                || nextItem.m_position.m_offset > m_startOffsetB)) {
#if DEBUGNEWPF
                            if (debug) {
                                logBuf.Add(
                                    $"*PED* item: seg. {item.m_position.m_segment}, " +
                                    $"lane {item.m_position.m_lane}, node {nextNodeId}: -NOT ADDING- " +
                                    $"start lane B reached in wrong direction!\n");
                                FlushCostLog(logBuf);
                            }
#endif
                            return;
                        }

                        var nextSpeed = CalculateLaneSpeed(
                            nextMaxSpeed,
                            m_startOffsetB,
                            nextItem.m_position.m_offset,
                            ref nextSegment,
                            nextLaneInfo); // NON-STOCK CODE
                        var nextOffset = Mathf.Abs(nextItem.m_position.m_offset - m_startOffsetB) * BYTE_TO_FLOAT_SCALE;
                        nextItem.m_comparisonValue +=
                            nextOffset * nextSegment.m_averageLength / (nextSpeed * m_maxLength);
                        nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextSpeed;
                    }

                    nextItem.m_laneID = nextLaneId;
                    nextItem.m_lanesUsed = item.m_lanesUsed | nextLaneInfo.m_laneType;
                    nextItem.m_vehiclesUsed = item.m_vehiclesUsed | nextLaneInfo.m_vehicleType;
                    nextItem.m_trafficRand = 0;
#if DEBUGNEWPF
                    if (debug) {
                        logBuf.Add(
                            $"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, " +
                            $"node {nextNodeId}: *ADDING*\n\tnextItem.m_laneID={nextItem.m_laneID}\n" +
                            $"\tnextItem.m_lanesUsed={nextItem.m_lanesUsed}\n" +
                            $"\tnextItem.m_vehiclesUsed={nextItem.m_vehiclesUsed}\n" +
                            $"\tnextItem.m_comparisonValue={nextItem.m_comparisonValue}\n" +
                            $"\tnextItem.m_methodDistance={nextItem.m_methodDistance}\n");
                        FlushCostLog(logBuf);

                        m_debugPositions[item.m_position.m_segment].Add(nextItem.m_position.m_segment);
                    }
#endif
                    AddBufferItem(nextItem, item.m_position);
                } else {
#if DEBUGNEWPF
                    if (debug) {
                        logBuf.Add(
                            $"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, " +
                            $"node {nextNodeId}: -NOT ADDING- lane incompatible or method " +
                            $"distance too large!\n\tnextItem.m_methodDistance" +
                            $"={nextItem.m_methodDistance}\n\tnextLaneInfo.m_laneType" +
                            $"={nextLaneInfo.m_laneType}\n");
                        FlushCostLog(logBuf);
                    }
#endif
                }
            } else {
#if DEBUGNEWPF
                if (debug) {
                    logBuf.Add(
                        $"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, " +
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
            var laneLocation = m_laneLocation[item.m_laneID];
            var locPathFindIndex = laneLocation >> 16; // upper 16 bit, expected (?) path find index
            var bufferIndex = (int)(laneLocation & 65535u); // lower 16 bit
            int comparisonBufferPos;
            if (locPathFindIndex == m_pathFindIndex) {
                if (item.m_comparisonValue >= m_buffer[bufferIndex].m_comparisonValue) {
                    return;
                }

                var bufferPosIndex = bufferIndex >> 6; // arithmetic shift (sign stays), upper 10 bit
                var bufferPos = bufferIndex & -64; // upper 10 bit (no shift)
                if (bufferPosIndex < m_bufferMinPos ||
                    (bufferPosIndex == m_bufferMinPos && bufferPos < m_bufferMin[bufferPosIndex])) {
                    return;
                }

                comparisonBufferPos = Mathf.Max(Mathf.RoundToInt(item.m_comparisonValue * 1024f), m_bufferMinPos);
                if (comparisonBufferPos == bufferPosIndex) {
                    m_buffer[bufferIndex] = item;
                    m_laneTarget[item.m_laneID] = target;
                    return;
                }

                var newBufferIndex = bufferPosIndex << 6 | m_bufferMax[bufferPosIndex]--;
                var bufferItem = m_buffer[newBufferIndex];
                m_laneLocation[bufferItem.m_laneID] = laneLocation;
                m_buffer[bufferIndex] = bufferItem;
            } else {
                comparisonBufferPos = Mathf.Max(Mathf.RoundToInt(item.m_comparisonValue * 1024f), m_bufferMinPos);
            }

            if (comparisonBufferPos >= 1024) {
                return;
            }

            if (comparisonBufferPos < 0) {
                return;
            }

            while (m_bufferMax[comparisonBufferPos] == 63) {
                ++comparisonBufferPos;
                if (comparisonBufferPos == 1024) {
                    return;
                }
            }

            if (comparisonBufferPos > m_bufferMaxPos) {
                m_bufferMaxPos = comparisonBufferPos;
            }

            bufferIndex = comparisonBufferPos << 6 | ++m_bufferMax[comparisonBufferPos];
            m_buffer[bufferIndex] = item;
            m_laneLocation[item.m_laneID] = m_pathFindIndex << 16 | (uint)bufferIndex;
            m_laneTarget[item.m_laneID] = target;
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
                    queueItem = CustomPathManager._instance.QueueItems[Calculating];
                } catch (Exception e) {
                    Log.Error($"(PF #{m_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread Error for unit {Calculating}, flags={PathUnits.m_buffer[Calculating].m_pathFindFlags} (1): {e}");
                } finally {
                    Monitor.Exit(QueueLock);
                }

                // calculate path unit
                try {
                    PathFindImplementation(Calculating, ref PathUnits.m_buffer[Calculating]);
                } catch (Exception ex) {
                    Log.Error($"(PF #{m_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread Error for unit {Calculating}, flags={PathUnits.m_buffer[Calculating].m_pathFindFlags} (2): {ex}");
                    // UIView.ForwardException(ex);
#if DEBUG
                    ++m_failedPathFinds;

#if DEBUGNEWPF
                    var debug = m_debug;
                    if (debug) {
                        Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {m_pathFindIndex}: " +
                                   $"Could not find path for unit {Calculating} -- exception occurred " +
                                   $"in PathFindImplementation");
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
                        Monitor.Enter(_bufferLock);
                        CustomPathManager._instance.QueueItems[Calculating].queued = false;
                        CustomPathManager._instance.ReleasePath(Calculating);
                    } finally {
                        Monitor.Exit(_bufferLock);
                    }

                    // NON-STOCK CODE END
                    Calculating = 0u;
                    Monitor.Pulse(QueueLock);
                } catch (Exception e) {
                    Log.Error($"(PF #{m_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, " +
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
            if (!Options.vehicleRestrictionsEnabled)
                return true;

            if (queueItem.vehicleType == ExtVehicleType.None || queueItem.vehicleType == ExtVehicleType.Tram)
                return true;

            /*if (laneInfo == null)
                    laneInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex];*/

            if ((laneInfo.m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train)) ==
                VehicleInfo.VehicleType.None)
                return true;

            var allowedTypes = vehicleRestrictionsManager.GetAllowedVehicleTypes(
                segmentId,
                segmentInfo,
                laneIndex,
                laneInfo,
                VehicleRestrictionsMode.Configured);

            return (allowedTypes & queueItem.vehicleType) != ExtVehicleType.None;
        }

        /// <summary>
        /// Determines the speed limit for the given lane.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="laneIndex"></param>
        /// <param name="laneId"></param>
        /// <param name="lane"></param>
        /// <returns></returns>
        protected virtual float GetLaneSpeedLimit(ushort segmentId, byte laneIndex, uint laneId, NetInfo.Lane lane) {
            return Options.customSpeedLimitsEnabled
                       ? speedLimitManager.GetLockFreeGameSpeedLimit(segmentId, laneIndex, laneId, lane)
                       : lane.m_speedLimit;
        }
    }
}