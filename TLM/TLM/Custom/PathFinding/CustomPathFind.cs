#define DEBUGLOCKSx
#define COUNTSEGMENTSTONEXTJUNCTIONx

using System;
using System.Reflection;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using TrafficManager.Geometry;
using UnityEngine;
using System.Collections.Generic;
using TrafficManager.Custom.AI;
using TrafficManager.TrafficLight;
using TrafficManager.State;
using TrafficManager.Manager;
using TrafficManager.Traffic;
using CSUtil.Commons;
using TrafficManager.Manager.Impl;
using static TrafficManager.Custom.PathFinding.CustomPathManager;
using TrafficManager.Traffic.Data;

namespace TrafficManager.Custom.PathFinding {
	public class CustomPathFind : PathFind {
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

		private enum LaneChangingCostCalculationMode {
			None,
			ByLaneDistance,
			ByGivenDistance
		}

		private const float SEGMENT_MIN_AVERAGE_LENGTH = 30f;
		private const float LANE_DENSITY_DISCRETIZATION = 25f;
		private const float LANE_USAGE_DISCRETIZATION = 25f;
		private const float BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR = 0.003921569f;

		//Expose the private fields
		FieldInfo _fieldpathUnits;
		FieldInfo _fieldQueueFirst;
		FieldInfo _fieldQueueLast;
		FieldInfo _fieldQueueLock;
		FieldInfo _fieldCalculating;
		FieldInfo _fieldTerminated;
		FieldInfo _fieldPathFindThread;

		private Array32<PathUnit> PathUnits {
			get { return _fieldpathUnits.GetValue(this) as Array32<PathUnit>; }
			set { _fieldpathUnits.SetValue(this, value); }
		}

		private uint QueueFirst {
			get { return (uint)_fieldQueueFirst.GetValue(this); }
			set { _fieldQueueFirst.SetValue(this, value); }
		}

		private uint QueueLast {
			get { return (uint)_fieldQueueLast.GetValue(this); }
			set { _fieldQueueLast.SetValue(this, value); }
		}

		private uint Calculating {
			get { return (uint)_fieldCalculating.GetValue(this); }
			set { _fieldCalculating.SetValue(this, value); }
		}

		private object QueueLock {
			get { return _fieldQueueLock.GetValue(this); }
			set { _fieldQueueLock.SetValue(this, value); }
		}

		private object _bufferLock;
		internal Thread CustomPathFindThread {
			get { return (Thread)_fieldPathFindThread.GetValue(this); }
			set { _fieldPathFindThread.SetValue(this, value); }
		}

		private bool Terminated {
			get { return (bool)_fieldTerminated.GetValue(this); }
			set { _fieldTerminated.SetValue(this, value); }
		}
		private int _bufferMinPos;
		private int _bufferMaxPos;
		private uint[] _laneLocation;
		private PathUnit.Position[] _laneTarget;
		private BufferItem[] _buffer;
		private int[] _bufferMin;
		private int[] _bufferMax;
		private float _maxLength;
		private uint _startLaneA;
		private uint _startLaneB;
		private ushort _startSegmentA;
		private ushort _startSegmentB;
		private uint _endLaneA;
		private uint _endLaneB;
		private uint _vehicleLane;
		private byte _startOffsetA;
		private byte _startOffsetB;
		private byte _vehicleOffset;
		private NetSegment.Flags _carBanMask;
		private bool _isHeavyVehicle;
		private bool _ignoreBlocked;
		private bool _stablePath;
		private bool _randomParking;
		private bool _transportVehicle;
		private PathUnitQueueItem queueItem;
		private NetSegment.Flags _disableMask;
		/*private ExtVehicleType? _extVehicleType;
		private ushort? _vehicleId;
		private ExtCitizenInstance.ExtPathType? _extPathType;*/
		private bool _isRoadVehicle;
		private bool _isLaneArrowObeyingEntity;
		private bool _isLaneConnectionObeyingEntity;
		private bool _leftHandDrive;
		//private float _speedRand;
		//private bool _extPublicTransport;
		//private static ushort laneChangeRandCounter = 0;
#if DEBUG
		public uint _failedPathFinds = 0;
		public uint _succeededPathFinds = 0;
		private bool _debug = false;
		private IDictionary<ushort, IList<ushort>> _debugPositions = null;
#endif
		public int pfId = 0;
		private Randomizer _pathRandomizer;
		private uint _pathFindIndex;
		private NetInfo.LaneType _laneTypes;
		private VehicleInfo.VehicleType _vehicleTypes;

		private GlobalConfig _conf = null;

		private static readonly CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
		private static readonly JunctionRestrictionsManager junctionManager = JunctionRestrictionsManager.Instance;
		private static readonly VehicleRestrictionsManager vehicleRestrictionsManager = VehicleRestrictionsManager.Instance;
		private static readonly SpeedLimitManager speedLimitManager = SpeedLimitManager.Instance;
		private static readonly TrafficMeasurementManager trafficMeasurementManager = TrafficMeasurementManager.Instance;
		private static readonly RoutingManager routingManager = RoutingManager.Instance;

		public bool IsMasterPathFind = false;

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

			_buffer = new BufferItem[65536]; // 2^16
			_bufferLock = PathManager.instance.m_bufferLock;
			PathUnits = PathManager.instance.m_pathUnits;
#if DEBUG
			if (QueueLock == null) {
				Log._Debug($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.Awake: QueueLock is null. Creating.");
				QueueLock = new object();
			} else {
				Log._Debug($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.Awake: QueueLock is NOT null.");
			}
#else
			QueueLock = new object();
#endif
			_laneLocation = new uint[262144]; // 2^18
			_laneTarget = new PathUnit.Position[262144]; // 2^18
			_bufferMin = new int[1024]; // 2^10
			_bufferMax = new int[1024]; // 2^10

			m_pathfindProfiler = new ThreadProfiler();
			CustomPathFindThread = new Thread(PathFindThread) { Name = "Pathfind" };
			CustomPathFindThread.Priority = SimulationManager.SIMULATION_PRIORITY;
			CustomPathFindThread.Start();
			if (!CustomPathFindThread.IsAlive) {
				//CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find thread failed to start!");
				Log.Error($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) Path find thread failed to start!");
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
				Log.Error("CustomPathFind.OnDestroy Error: " + e.ToString());
			} finally {
				Monitor.Exit(QueueLock);
			}
		}

		public new bool CalculatePath(uint unit, bool skipQueue) {
			return ExtCalculatePath(unit, skipQueue);
		}

		public bool ExtCalculatePath(uint unit, bool skipQueue) {
			if (CustomPathManager._instance.AddPathReference(unit)) {
				try {
					Monitor.Enter(QueueLock);

					if (skipQueue) {

						if (this.QueueLast == 0u) {
							this.QueueLast = unit;
						} else {
							CustomPathManager._instance.queueItems[unit].nextPathUnitId = QueueFirst;
							//this.PathUnits.m_buffer[unit].m_nextPathUnit = this.QueueFirst;
						}
						this.QueueFirst = unit;
					} else {
						if (this.QueueLast == 0u) {
							this.QueueFirst = unit;
						} else {
							CustomPathManager._instance.queueItems[QueueLast].nextPathUnitId = unit;
							//this.PathUnits.m_buffer[this.QueueLast].m_nextPathUnit = unit;
						}
						this.QueueLast = unit;
					}
					this.PathUnits.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_CREATED;
					++this.m_queuedPathFindCount;
					
					Monitor.Pulse(this.QueueLock);
				} catch (Exception e) {
					Log.Error($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.CalculatePath({unit}, {skipQueue}): Error: {e.ToString()}");
				} finally {
					Monitor.Exit(this.QueueLock);
				}
				return true;
			}
			return false;
		}

		// PathFind
		protected void PathFindImplementation(uint unit, ref PathUnit data) {
			_conf = GlobalConfig.Instance; // NON-STOCK CODE

			NetManager instance = Singleton<NetManager>.instance;
			this._laneTypes = (NetInfo.LaneType)this.PathUnits.m_buffer[unit].m_laneTypes;
			this._vehicleTypes = (VehicleInfo.VehicleType)this.PathUnits.m_buffer[unit].m_vehicleTypes;
			this._maxLength = this.PathUnits.m_buffer[unit].m_length;
			this._pathFindIndex = (this._pathFindIndex + 1u & 32767u);
			this._pathRandomizer = new Randomizer(unit);

			this._carBanMask = NetSegment.Flags.CarBan;
			this._isHeavyVehicle = ((this.PathUnits.m_buffer[unit].m_simulationFlags & 16) != 0);
			if (_isHeavyVehicle) {
				this._carBanMask |= NetSegment.Flags.HeavyBan;
			}
			if ((this.PathUnits.m_buffer[unit].m_simulationFlags & 4) != 0) {
				this._carBanMask |= NetSegment.Flags.WaitingPath;
			}
			this._ignoreBlocked = ((this.PathUnits.m_buffer[unit].m_simulationFlags & 32) != 0);
			this._stablePath = ((this.PathUnits.m_buffer[unit].m_simulationFlags & 64) != 0);
			this._randomParking = ((this.PathUnits.m_buffer[unit].m_simulationFlags & 128) != 0);
			this._transportVehicle = ((byte)(this._laneTypes & NetInfo.LaneType.TransportVehicle) != 0);
			this._disableMask = (NetSegment.Flags.Collapsed | NetSegment.Flags.PathFailed);
			if ((this.PathUnits.m_buffer[unit].m_simulationFlags & 2) == 0) {
				this._disableMask |= NetSegment.Flags.Flooded;
			}
			//this._speedRand = 0;
			this._leftHandDrive = Constants.ServiceFactory.SimulationService.LeftHandDrive;
			this._isRoadVehicle = (queueItem.vehicleType & ExtVehicleType.RoadVehicle) != ExtVehicleType.None;
			this._isLaneArrowObeyingEntity = (_vehicleTypes & LaneArrowManager.VEHICLE_TYPES) != VehicleInfo.VehicleType.None &&
										(queueItem.vehicleType & LaneArrowManager.EXT_VEHICLE_TYPES) != ExtVehicleType.None;
			this._isLaneConnectionObeyingEntity = (_vehicleTypes & LaneConnectionManager.VEHICLE_TYPES) != VehicleInfo.VehicleType.None &&
													(queueItem.vehicleType & LaneConnectionManager.EXT_VEHICLE_TYPES) != ExtVehicleType.None;
#if DEBUGNEWPF && DEBUG
			bool debug = this._debug = _conf.Debug.Switches[0] &&
				((_conf.Debug.ExtVehicleType == ExtVehicleType.None && queueItem.vehicleType == ExtVehicleType.None) || (queueItem.vehicleType & _conf.Debug.ExtVehicleType) != ExtVehicleType.None) &&
				(_conf.Debug.StartSegmentId <= 0 || data.m_position00.m_segment == _conf.Debug.StartSegmentId || data.m_position02.m_segment == _conf.Debug.StartSegmentId) &&
				(_conf.Debug.EndSegmentId <= 0 || data.m_position01.m_segment == _conf.Debug.EndSegmentId || data.m_position03.m_segment == _conf.Debug.EndSegmentId) &&
				(_conf.Debug.VehicleId <= 0 || queueItem.vehicleId == _conf.Debug.VehicleId)
				;
			if (debug) {
				Log._Debug($"CustomPathFind.PathFindImplementation: START calculating path unit {unit}, type {queueItem.vehicleType}");
				_debugPositions = new Dictionary<ushort, IList<ushort>>();
			}
#endif

			if ((byte)(this._laneTypes & NetInfo.LaneType.Vehicle) != 0) {
				this._laneTypes |= NetInfo.LaneType.TransportVehicle;
			}
			int posCount = (int)(this.PathUnits.m_buffer[unit].m_positionCount & 15);
			int vehiclePosIndicator = this.PathUnits.m_buffer[unit].m_positionCount >> 4;
			BufferItem bufferItemStartA;
			if (data.m_position00.m_segment != 0 && posCount >= 1) {
				this._startLaneA = PathManager.GetLaneID(data.m_position00);
				this._startSegmentA = data.m_position00.m_segment; // NON-STOCK CODE
				this._startOffsetA = data.m_position00.m_offset;
				bufferItemStartA.m_laneID = this._startLaneA;
				bufferItemStartA.m_position = data.m_position00;
				this.GetLaneDirection(data.m_position00, out bufferItemStartA.m_direction, out bufferItemStartA.m_lanesUsed, out bufferItemStartA.m_vehiclesUsed);
				bufferItemStartA.m_comparisonValue = 0f;
				bufferItemStartA.m_duration = 0f;
#if COUNTSEGMENTSTONEXTJUNCTION
				bufferItemStartA.m_numSegmentsToNextJunction = 0;
#endif
			} else {
				this._startLaneA = 0u;
				this._startSegmentA = 0; // NON-STOCK CODE
				this._startOffsetA = 0;
				bufferItemStartA = default(BufferItem);
			}
			BufferItem bufferItemStartB;
			if (data.m_position02.m_segment != 0 && posCount >= 3) {
				this._startLaneB = PathManager.GetLaneID(data.m_position02);
				this._startSegmentB = data.m_position02.m_segment; // NON-STOCK CODE
				this._startOffsetB = data.m_position02.m_offset;
				bufferItemStartB.m_laneID = this._startLaneB;
				bufferItemStartB.m_position = data.m_position02;
				this.GetLaneDirection(data.m_position02, out bufferItemStartB.m_direction, out bufferItemStartB.m_lanesUsed, out bufferItemStartB.m_vehiclesUsed);
				bufferItemStartB.m_comparisonValue = 0f;
				bufferItemStartB.m_duration = 0f;
#if COUNTSEGMENTSTONEXTJUNCTION
				bufferItemStartB.m_numSegmentsToNextJunction = 0;
#endif
			} else {
				this._startLaneB = 0u;
				this._startSegmentB = 0; // NON-STOCK CODE
				this._startOffsetB = 0;
				bufferItemStartB = default(BufferItem);
			}
			BufferItem bufferItemEndA;
			if (data.m_position01.m_segment != 0 && posCount >= 2) {
				this._endLaneA = PathManager.GetLaneID(data.m_position01);
				bufferItemEndA.m_laneID = this._endLaneA;
				bufferItemEndA.m_position = data.m_position01;
				this.GetLaneDirection(data.m_position01, out bufferItemEndA.m_direction, out bufferItemEndA.m_lanesUsed, out bufferItemEndA.m_vehiclesUsed);
				bufferItemEndA.m_methodDistance = 0.01f;
				bufferItemEndA.m_comparisonValue = 0f;
				bufferItemEndA.m_duration = 0f;
				bufferItemEndA.m_trafficRand = 0; // NON-STOCK CODE
#if COUNTSEGMENTSTONEXTJUNCTION
				bufferItemEndA.m_numSegmentsToNextJunction = 0;
#endif
			} else {
				this._endLaneA = 0u;
				bufferItemEndA = default(BufferItem);
			}
			BufferItem bufferItemEndB;
			if (data.m_position03.m_segment != 0 && posCount >= 4) {
				this._endLaneB = PathManager.GetLaneID(data.m_position03);
				bufferItemEndB.m_laneID = this._endLaneB;
				bufferItemEndB.m_position = data.m_position03;
				this.GetLaneDirection(data.m_position03, out bufferItemEndB.m_direction, out bufferItemEndB.m_lanesUsed, out bufferItemEndB.m_vehiclesUsed);
				bufferItemEndB.m_methodDistance = 0.01f;
				bufferItemEndB.m_comparisonValue = 0f;
				bufferItemEndB.m_duration = 0f;
				bufferItemEndB.m_trafficRand = 0; // NON-STOCK CODE
#if COUNTSEGMENTSTONEXTJUNCTION
				bufferItemEndB.m_numSegmentsToNextJunction = 0;
#endif
			} else {
				this._endLaneB = 0u;
				bufferItemEndB = default(BufferItem);
			}
			if (data.m_position11.m_segment != 0 && vehiclePosIndicator >= 1) {
				this._vehicleLane = PathManager.GetLaneID(data.m_position11);
				this._vehicleOffset = data.m_position11.m_offset;
			} else {
				this._vehicleLane = 0u;
				this._vehicleOffset = 0;
			}
#if DEBUGNEWPF && DEBUG
			if (debug) {
				Log._Debug($"CustomPathFind.PathFindImplementation: Preparing calculating path unit {unit}, type {queueItem.vehicleType}:\n" +
					$"\tbufferItemStartA: segment={bufferItemStartA.m_position.m_segment} lane={bufferItemStartA.m_position.m_lane} off={bufferItemStartA.m_position.m_offset} laneId={bufferItemStartA.m_laneID}\n" +
					$"\tbufferItemStartB: segment={bufferItemStartB.m_position.m_segment} lane={bufferItemStartB.m_position.m_lane} off={bufferItemStartB.m_position.m_offset} laneId={bufferItemStartB.m_laneID}\n" +
					$"\tbufferItemEndA: segment={bufferItemEndA.m_position.m_segment} lane={bufferItemEndA.m_position.m_lane} off={bufferItemEndA.m_position.m_offset} laneId={bufferItemEndA.m_laneID}\n" +
					$"\tbufferItemEndB: segment={bufferItemEndB.m_position.m_segment} lane={bufferItemEndB.m_position.m_lane} off={bufferItemEndB.m_position.m_offset} laneId={bufferItemEndB.m_laneID}\n"
					);
			}
#endif
			BufferItem finalBufferItem = default(BufferItem);
			byte startOffset = 0;
			this._bufferMinPos = 0;
			this._bufferMaxPos = -1;
			if (this._pathFindIndex == 0u) {
				uint maxUInt = 4294901760u;
				for (int i = 0; i < 262144; ++i) {
					this._laneLocation[i] = maxUInt;
				}
			}
			for (int j = 0; j < 1024; ++j) {
				this._bufferMin[j] = 0;
				this._bufferMax[j] = -1;
			}
			if (bufferItemEndA.m_position.m_segment != 0) {
				++this._bufferMax[0];
				this._buffer[++this._bufferMaxPos] = bufferItemEndA;
			}
			if (bufferItemEndB.m_position.m_segment != 0) {
				++this._bufferMax[0];
				this._buffer[++this._bufferMaxPos] = bufferItemEndB;
			}
			bool canFindPath = false;

			while (this._bufferMinPos <= this._bufferMaxPos) {
				int bufMin = this._bufferMin[this._bufferMinPos];
				int bufMax = this._bufferMax[this._bufferMinPos];
				if (bufMin > bufMax) {
					++this._bufferMinPos;
				} else {
					this._bufferMin[this._bufferMinPos] = bufMin + 1;
					BufferItem candidateItem = this._buffer[(this._bufferMinPos << 6) + bufMin];
					if (candidateItem.m_position.m_segment == bufferItemStartA.m_position.m_segment && candidateItem.m_position.m_lane == bufferItemStartA.m_position.m_lane) {
						// we reached startA
						if ((byte)(candidateItem.m_direction & NetInfo.Direction.Forward) != 0 && candidateItem.m_position.m_offset >= this._startOffsetA) {
							finalBufferItem = candidateItem;
							startOffset = this._startOffsetA;
							canFindPath = true;
							break;
						}
						if ((byte)(candidateItem.m_direction & NetInfo.Direction.Backward) != 0 && candidateItem.m_position.m_offset <= this._startOffsetA) {
							finalBufferItem = candidateItem;
							startOffset = this._startOffsetA;
							canFindPath = true;
							break;
						}
					}
					if (candidateItem.m_position.m_segment == bufferItemStartB.m_position.m_segment && candidateItem.m_position.m_lane == bufferItemStartB.m_position.m_lane) {
						// we reached startB
						if ((byte)(candidateItem.m_direction & NetInfo.Direction.Forward) != 0 && candidateItem.m_position.m_offset >= this._startOffsetB) {
							finalBufferItem = candidateItem;
							startOffset = this._startOffsetB;
							canFindPath = true;
							break;
						}
						if ((byte)(candidateItem.m_direction & NetInfo.Direction.Backward) != 0 && candidateItem.m_position.m_offset <= this._startOffsetB) {
							finalBufferItem = candidateItem;
							startOffset = this._startOffsetB;
							canFindPath = true;
							break;
						}
					}

					// explore the path
					if ((byte)(candidateItem.m_direction & NetInfo.Direction.Forward) != 0) {
						ushort startNode = instance.m_segments.m_buffer[candidateItem.m_position.m_segment].m_startNode;
						uint laneRoutingIndex = routingManager.GetLaneEndRoutingIndex(candidateItem.m_laneID, true);
						this.ProcessItemMain(unit, candidateItem, ref instance.m_segments.m_buffer[candidateItem.m_position.m_segment], routingManager.segmentRoutings[candidateItem.m_position.m_segment], routingManager.laneEndBackwardRoutings[laneRoutingIndex], startNode, true, ref instance.m_nodes.m_buffer[startNode], 0, false);
					}

					if ((byte)(candidateItem.m_direction & NetInfo.Direction.Backward) != 0) {
						ushort endNode = instance.m_segments.m_buffer[candidateItem.m_position.m_segment].m_endNode;
						uint laneRoutingIndex = routingManager.GetLaneEndRoutingIndex(candidateItem.m_laneID, false);
						this.ProcessItemMain(unit, candidateItem, ref instance.m_segments.m_buffer[candidateItem.m_position.m_segment], routingManager.segmentRoutings[candidateItem.m_position.m_segment], routingManager.laneEndBackwardRoutings[laneRoutingIndex], endNode, false, ref instance.m_nodes.m_buffer[endNode], 255, false);
					}

					// handle special nodes (e.g. bus stops)
					int num6 = 0;
					ushort specialNodeId = instance.m_lanes.m_buffer[candidateItem.m_laneID].m_nodes;
					if (specialNodeId != 0) {
						ushort startNode2 = instance.m_segments.m_buffer[candidateItem.m_position.m_segment].m_startNode;
						ushort endNode2 = instance.m_segments.m_buffer[candidateItem.m_position.m_segment].m_endNode;
						bool nodesDisabled = ((instance.m_nodes.m_buffer[startNode2].m_flags | instance.m_nodes.m_buffer[endNode2].m_flags) & NetNode.Flags.Disabled) != NetNode.Flags.None;
						while (specialNodeId != 0) {
							NetInfo.Direction direction = NetInfo.Direction.None;
							byte laneOffset = instance.m_nodes.m_buffer[specialNodeId].m_laneOffset;
							if (laneOffset <= candidateItem.m_position.m_offset) {
								direction |= NetInfo.Direction.Forward;
							}
							if (laneOffset >= candidateItem.m_position.m_offset) {
								direction |= NetInfo.Direction.Backward;
							}
							if ((byte)(candidateItem.m_direction & direction) != 0 && (!nodesDisabled || (instance.m_nodes.m_buffer[specialNodeId].m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None)) {
#if DEBUGNEWPF && DEBUG
								if (debug && (_conf.Debug.NodeId <= 0 || specialNodeId == _conf.Debug.NodeId)) {
									Log._Debug($"CustomPathFind.PathFindImplementation: Handling special node for path unit {unit}, type {queueItem.vehicleType}:\n" +
										$"\tcandidateItem.m_position.m_segment={candidateItem.m_position.m_segment}\n" +
										$"\tcandidateItem.m_position.m_lane={candidateItem.m_position.m_lane}\n" +
										$"\tcandidateItem.m_laneID={candidateItem.m_laneID}\n" +
										$"\tspecialNodeId={specialNodeId}\n" +
										$"\tstartNode2={startNode2}\n" +
										$"\tendNode2={endNode2}\n"
										);
								}
#endif
								this.ProcessItemMain(unit, candidateItem, ref instance.m_segments.m_buffer[candidateItem.m_position.m_segment], routingManager.segmentRoutings[candidateItem.m_position.m_segment], routingManager.laneEndBackwardRoutings[0], specialNodeId, false, ref instance.m_nodes.m_buffer[specialNodeId], laneOffset, true);
							}
							specialNodeId = instance.m_nodes.m_buffer[specialNodeId].m_nextLaneNode;
							if (++num6 == 32768) {
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
				++_failedPathFinds;

#if DEBUGNEWPF
				if (debug) {
					Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Could not find path for unit {unit} -- path-finding failed during process");
					string reachableBuf = "";
					string unreachableBuf = "";
					foreach (KeyValuePair<ushort, IList<ushort>> e in _debugPositions) {
						string buf = $"{e.Key} -> {e.Value.CollectionToString()}\n";
						if (e.Value.Count <= 0) {
							unreachableBuf += buf;
						} else {
							reachableBuf += buf;
						}
					}
					Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Reachability graph for unit {unit}:\n== REACHABLE ==\n" + reachableBuf + "\n== UNREACHABLE ==\n" + unreachableBuf);
				}
#endif
#endif
				//CustomPathManager._instance.ResetQueueItem(unit);

				return;
			}
			// we could calculate a valid path

			float duration = finalBufferItem.m_duration;
			this.PathUnits.m_buffer[unit].m_length = duration;
			this.PathUnits.m_buffer[unit].m_laneTypes = (byte)finalBufferItem.m_lanesUsed; // NON-STOCK CODE
			this.PathUnits.m_buffer[unit].m_vehicleTypes = (ushort)finalBufferItem.m_vehiclesUsed; // NON-STOCK CODE
#if DEBUG
			/*if (_conf.Debug.Switches[4])
				Log._Debug($"Lane/Vehicle types of path unit {unit}: {finalBufferItem.m_lanesUsed} / {finalBufferItem.m_vehiclesUsed}");*/
#endif
			uint currentPathUnitId = unit;
			int currentItemPositionCount = 0;
			int sumOfPositionCounts = 0;
			PathUnit.Position currentPosition = finalBufferItem.m_position;
			if ((currentPosition.m_segment != bufferItemEndA.m_position.m_segment || currentPosition.m_lane != bufferItemEndA.m_position.m_lane || currentPosition.m_offset != bufferItemEndA.m_position.m_offset) &&
				(currentPosition.m_segment != bufferItemEndB.m_position.m_segment || currentPosition.m_lane != bufferItemEndB.m_position.m_lane || currentPosition.m_offset != bufferItemEndB.m_position.m_offset)) {
				// the found starting position differs from the desired end position
				if (startOffset != currentPosition.m_offset) {
					// the offsets differ: copy the found starting position and modify the offset to fit the desired offset
					PathUnit.Position position2 = currentPosition;
					position2.m_offset = startOffset;
					this.PathUnits.m_buffer[currentPathUnitId].SetPosition(currentItemPositionCount++, position2);
					// now we have: [desired starting position]
				}
				// add the found starting position to the path unit
				this.PathUnits.m_buffer[currentPathUnitId].SetPosition(currentItemPositionCount++, currentPosition);
				currentPosition = this._laneTarget[finalBufferItem.m_laneID]; // go to the next path position

				// now we have either [desired starting position, found starting position] or [found starting position], depending on if the found starting position matched the desired
			}

			// beginning with the starting position, going to the target position: assemble the path units
			for (int k = 0; k < 262144; ++k) {
				//pfCurrentState = 6;
				this.PathUnits.m_buffer[currentPathUnitId].SetPosition(currentItemPositionCount++, currentPosition); // add the next path position to the current unit

				if ((currentPosition.m_segment == bufferItemEndA.m_position.m_segment && currentPosition.m_lane == bufferItemEndA.m_position.m_lane && currentPosition.m_offset == bufferItemEndA.m_position.m_offset) ||
					(currentPosition.m_segment == bufferItemEndB.m_position.m_segment && currentPosition.m_lane == bufferItemEndB.m_position.m_lane && currentPosition.m_offset == bufferItemEndB.m_position.m_offset)) {
					// we have reached the end position

					this.PathUnits.m_buffer[currentPathUnitId].m_positionCount = (byte)currentItemPositionCount;
					sumOfPositionCounts += currentItemPositionCount; // add position count of last unit to sum
					if (sumOfPositionCounts != 0) {
						// for each path unit from start to target: calculate length (distance) to target
						currentPathUnitId = this.PathUnits.m_buffer[unit].m_nextPathUnit; // (we do not need to calculate the length for the starting unit since this is done before; it's the total path length)
						currentItemPositionCount = (int)this.PathUnits.m_buffer[unit].m_positionCount;
						int totalIter = 0;
						while (currentPathUnitId != 0u) {
							this.PathUnits.m_buffer[currentPathUnitId].m_length = duration * (float)(sumOfPositionCounts - currentItemPositionCount) / (float)sumOfPositionCounts;
							currentItemPositionCount += (int)this.PathUnits.m_buffer[currentPathUnitId].m_positionCount;
							currentPathUnitId = this.PathUnits.m_buffer[currentPathUnitId].m_nextPathUnit;
							if (++totalIter >= 262144) {
#if DEBUG
								Log.Error("THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: PathFindImplementation: Invalid list detected.");
#endif
								CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
								break;
							}
						}
					}
#if DEBUG
					//Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Path found (pfCurrentState={pfCurrentState}) for unit {unit}");
#endif
					PathUnits.m_buffer[(int)unit].m_pathFindFlags |= PathUnit.FLAG_READY; // Path found
#if DEBUG
					++_succeededPathFinds;

#if DEBUGNEWPF
					if (debug)
						Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Path-find succeeded for unit {unit}");
#endif
#endif
					//CustomPathManager._instance.ResetQueueItem(unit);

					return;
				}

				// We have not reached the target position yet 
				if (currentItemPositionCount == 12) {
					// the current path unit is full, we need a new one
					uint createdPathUnitId;
					try {
						Monitor.Enter(_bufferLock);
						if (!this.PathUnits.CreateItem(out createdPathUnitId, ref this._pathRandomizer)) {
							// we failed to create a new path unit, thus the path-finding also failed
							PathUnits.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;
#if DEBUG
							++_failedPathFinds;

#if DEBUGNEWPF
							if (debug)
								Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Could not find path for unit {unit} -- Could not create path unit");
#endif
#endif
							//CustomPathManager._instance.ResetQueueItem(unit);
							return;
						}
						this.PathUnits.m_buffer[createdPathUnitId] = this.PathUnits.m_buffer[(int)currentPathUnitId];
						this.PathUnits.m_buffer[createdPathUnitId].m_referenceCount = 1;
						this.PathUnits.m_buffer[createdPathUnitId].m_pathFindFlags = PathUnit.FLAG_READY;
						this.PathUnits.m_buffer[currentPathUnitId].m_nextPathUnit = createdPathUnitId;
						this.PathUnits.m_buffer[currentPathUnitId].m_positionCount = (byte)currentItemPositionCount;
						this.PathUnits.m_buffer[currentPathUnitId].m_laneTypes = (byte)finalBufferItem.m_lanesUsed; // NON-STOCK CODE (this is not accurate!)
						this.PathUnits.m_buffer[currentPathUnitId].m_vehicleTypes = (ushort)finalBufferItem.m_vehiclesUsed; // NON-STOCK CODE (this is not accurate!)
						sumOfPositionCounts += currentItemPositionCount;
						Singleton<PathManager>.instance.m_pathUnitCount = (int)(this.PathUnits.ItemCount() - 1u);
					} catch (Exception e) {
						Log.Error($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindImplementation Error: {e.ToString()}");
						break;
					} finally {
						Monitor.Exit(this._bufferLock);
					}
					currentPathUnitId = createdPathUnitId;
					currentItemPositionCount = 0;
				}

				uint laneID = PathManager.GetLaneID(currentPosition);
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
				currentPosition = this._laneTarget[laneID];
			}
			PathUnits.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;
#if DEBUG
			++_failedPathFinds;

#if DEBUGNEWPF
			if (debug)
				Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Could not find path for unit {unit} -- internal error: for loop break");
#endif
#endif
			//CustomPathManager._instance.ResetQueueItem(unit);
#if DEBUG
			//Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Cannot find path (pfCurrentState={pfCurrentState}) for unit {unit}");
#endif
		}

		// be aware:
		//   (1) path-finding works from target to start. the "next" segment is always the previous and the "previous" segment is always the next segment on the path!
		//   (2) when I use the term "lane index from outer" this means outer right lane for right-hand traffic systems and outer-left lane for left-hand traffic systems.

		// 1
		private void ProcessItemMain(uint unitId, BufferItem item, ref NetSegment prevSegment, SegmentRoutingData prevSegmentRouting, LaneEndRoutingData prevLaneEndRouting, ushort nextNodeId, bool nextIsStartNode, ref NetNode nextNode, byte connectOffset, bool isMiddle) {
#if DEBUGNEWPF && DEBUG
			bool debug = this._debug && (_conf.Debug.NodeId <= 0 || nextNodeId == _conf.Debug.NodeId);
			bool debugPed = debug && _conf.Debug.Switches[12];
			if (debug) {
				if (! _debugPositions.ContainsKey(item.m_position.m_segment)) {
					_debugPositions[item.m_position.m_segment] = new List<ushort>();
				}
			}
#else
			bool debug = false;
			bool debugPed = false;
#endif
			//Log.Message($"THREAD #{Thread.CurrentThread.ManagedThreadId} Path finder: " + this._pathFindIndex + " vehicle types: " + this._vehicleTypes);
#if DEBUGNEWPF && DEBUG
			//bool debug = isTransportVehicle && isMiddle && item.m_position.m_segment == 13550;
			List<String> logBuf = null;
			if (debug)
				logBuf = new List<String>();
#endif

			NetManager netManager = Singleton<NetManager>.instance;
			bool prevIsPedestrianLane = false;
			//bool prevIsBusLane = false; // non-stock
			bool prevIsBicycleLane = false;
			bool prevIsCenterPlatform = false;
			bool prevIsElevated = false;
			bool prevIsCarLane = false;
			int prevRelSimilarLaneIndex = 0; // inner/outer similar index
			//int prevInnerSimilarLaneIndex = 0; // similar index, starting with 0 at leftmost lane in right hand traffic
			int prevOuterSimilarLaneIndex = 0; // similar index, starting with 0 at rightmost lane in right hand traffic
			NetInfo prevSegmentInfo = prevSegment.Info;
			NetInfo.Lane prevLaneInfo = null;
			byte prevSimilarLaneCount = 0;
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				prevLaneInfo = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				prevIsPedestrianLane = (prevLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian);
				prevIsBicycleLane = (prevLaneInfo.m_laneType == NetInfo.LaneType.Vehicle && (prevLaneInfo.m_vehicleType & this._vehicleTypes) == VehicleInfo.VehicleType.Bicycle);
				prevIsCarLane = (prevLaneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None && (prevLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None;
				//prevIsBusLane = (prevLane.m_laneType == NetInfo.LaneType.TransportVehicle && (prevLane.m_vehicleType & this._vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None);
				prevIsCenterPlatform = prevLaneInfo.m_centerPlatform;
				prevIsElevated = prevLaneInfo.m_elevated;
				prevSimilarLaneCount = (byte)prevLaneInfo.m_similarLaneCount;
				//prevInnerSimilarLaneIndex = RoutingManager.Instance.CalcInnerSimilarLaneIndex(prevLaneInfo);
				prevOuterSimilarLaneIndex = RoutingManager.Instance.CalcOuterSimilarLaneIndex(prevLaneInfo);
				if ((byte)(prevLaneInfo.m_finalDirection & NetInfo.Direction.Forward) != 0) {
					prevRelSimilarLaneIndex = prevLaneInfo.m_similarLaneIndex;
				} else {
					prevRelSimilarLaneIndex = prevLaneInfo.m_similarLaneCount - prevLaneInfo.m_similarLaneIndex - 1;
				}
			}
			int firstPrevSimilarLaneIndexFromInner = prevRelSimilarLaneIndex;
			ushort prevSegmentId = item.m_position.m_segment;
			if (isMiddle) {
				for (int i = 0; i < 8; ++i) {
					ushort nextSegmentId = nextNode.GetSegment(i);
					if (nextSegmentId <= 0)
						continue;

#if DEBUGNEWPF
					if (debug) {
						FlushMainLog(logBuf, unitId);
					}
#endif

					this.ProcessItemCosts(debug, item, nextNodeId, nextSegmentId, ref prevSegment, /*prevSegmentRouting,*/ ref netManager.m_segments.m_buffer[(int)nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, !prevIsPedestrianLane, prevIsPedestrianLane, isMiddle);
				}
			} else if (prevIsPedestrianLane) {
				bool allowPedSwitch = (this._laneTypes & NetInfo.LaneType.Pedestrian) != 0;
				if (!prevIsElevated) {
					// explore pedestrian lanes
					int prevLaneIndex = (int)item.m_position.m_lane;
					if (nextNode.Info.m_class.m_service != ItemClass.Service.Beautification) {
						if (allowPedSwitch) { // NON-STOCK CODE
							bool isEndBendOrJunction = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None;
							bool isOnCenterPlatform = prevIsCenterPlatform && (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Junction)) == NetNode.Flags.None;
							ushort nextLeftSegment = prevSegmentId;
							ushort nextRightSegment = prevSegmentId;
							int leftLaneIndex;
							int rightLaneIndex;
							uint leftLaneId;
							uint rightLaneId;
							prevSegment.GetLeftAndRightLanes(nextNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, prevLaneIndex, isOnCenterPlatform, out leftLaneIndex, out rightLaneIndex, out leftLaneId, out rightLaneId);
							if (leftLaneId == 0u || rightLaneId == 0u) {
								ushort leftSegment;
								ushort rightSegment;
								prevSegment.GetLeftAndRightSegments(nextNodeId, out leftSegment, out rightSegment);
								int numIter = 0;
								while (leftSegment != 0 && leftSegment != prevSegmentId && leftLaneId == 0u) {
									int someLeftLaneIndex;
									int someRightLaneIndex;
									uint someLeftLaneId;
									uint someRightLaneId;
									netManager.m_segments.m_buffer[(int)leftSegment].GetLeftAndRightLanes(nextNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, isOnCenterPlatform, out someLeftLaneIndex, out someRightLaneIndex, out someLeftLaneId, out someRightLaneId);
									if (someRightLaneId != 0u) {
										nextLeftSegment = leftSegment;
										leftLaneIndex = someRightLaneIndex;
										leftLaneId = someRightLaneId;
									} else {
										leftSegment = netManager.m_segments.m_buffer[(int)leftSegment].GetLeftSegment(nextNodeId);
									}
									if (++numIter == 8) {
										break;
									}
								}
								numIter = 0;
								while (rightSegment != 0 && rightSegment != prevSegmentId && rightLaneId == 0u) {
									int someLeftLaneIndex;
									int someRightLaneIndex;
									uint someLeftLaneId;
									uint someRightLaneId;
									netManager.m_segments.m_buffer[(int)rightSegment].GetLeftAndRightLanes(nextNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, isOnCenterPlatform, out someLeftLaneIndex, out someRightLaneIndex, out someLeftLaneId, out someRightLaneId);
									if (someLeftLaneId != 0u) {
										nextRightSegment = rightSegment;
										rightLaneIndex = someLeftLaneIndex;
										rightLaneId = someLeftLaneId;
									} else {
										rightSegment = netManager.m_segments.m_buffer[(int)rightSegment].GetRightSegment(nextNodeId);
									}
									if (++numIter == 8) {
										break;
									}
								}
							}
							if (leftLaneId != 0u && (nextLeftSegment != prevSegmentId || isEndBendOrJunction || isOnCenterPlatform)) {
#if DEBUGNEWPF
								if (debugPed) {
										logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId} ({nextIsStartNode}): Exploring left segment\n" +
											"\t" + $"_extPathType={queueItem.pathType}\n" +
											"\t" + $"_vehicleTypes={_vehicleTypes}, _laneTypes={_laneTypes}\n" +
											"\t" + $"_extVehicleType={queueItem.vehicleType}\n" +
											"\t" + $"_isRoadVehicle={_isRoadVehicle}\n" +
											"\t" + $"_isHeavyVehicle={_isHeavyVehicle}\n" +
											"\t" + $"_stablePath={_stablePath}\n" +
											"\t" + $"_isLaneConnectionObeyingEntity={_isLaneConnectionObeyingEntity}\n" +
											"\t" + $"_isLaneArrowObeyingEntity={_isLaneArrowObeyingEntity}\n\n" +
											"\t" + $"nextIsStartNode={nextIsStartNode}\n" +
											"\t" + $"nextLeftSegment={nextLeftSegment}\n" +
											"\t" + $"leftLaneId={leftLaneId}\n" +
											"\t" + $"isEndBendOrJunction={isEndBendOrJunction}\n" +
											"\t" + $"isOnCenterPlatform={isOnCenterPlatform}\n" +
											"\t" + $"nextIsStartNode={nextIsStartNode}\n" +
											"\t" + $"nextIsStartNode={nextIsStartNode}\n"
											);
									FlushMainLog(logBuf, unitId);
								}
#endif
								this.ProcessItemPedBicycle(debugPed, item, nextNodeId, nextLeftSegment, ref prevSegment, ref netManager.m_segments.m_buffer[(int)nextLeftSegment], connectOffset, connectOffset, leftLaneIndex, leftLaneId); // ped
							}
							if (rightLaneId != 0u && rightLaneId != leftLaneId && (nextRightSegment != prevSegmentId || isEndBendOrJunction || isOnCenterPlatform)) {
#if DEBUGNEWPF
								if (debugPed) {
									logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId} ({nextIsStartNode}): Exploring right segment\n" +
										"\t" + $"_extPathType={queueItem.pathType}\n" +
										"\t" + $"_vehicleTypes={_vehicleTypes}, _laneTypes={_laneTypes}\n" +
										"\t" + $"_extVehicleType={queueItem.vehicleType}\n" +
										"\t" + $"_isRoadVehicle={_isRoadVehicle}\n" +
										"\t" + $"_isHeavyVehicle={_isHeavyVehicle}\n" +
										"\t" + $"_stablePath={_stablePath}\n" +
										"\t" + $"_isLaneConnectionObeyingEntity={_isLaneConnectionObeyingEntity}\n" +
										"\t" + $"_isLaneArrowObeyingEntity={_isLaneArrowObeyingEntity}\n\n" +
										"\t" + $"nextIsStartNode={nextIsStartNode}\n" +
										"\t" + $"nextRightSegment={nextRightSegment}\n" +
										"\t" + $"rightLaneId={rightLaneId}\n" +
										"\t" + $"isEndBendOrJunction={isEndBendOrJunction}\n" +
										"\t" + $"isOnCenterPlatform={isOnCenterPlatform}\n" +
										"\t" + $"nextIsStartNode={nextIsStartNode}\n"
										);
									FlushMainLog(logBuf, unitId);
								}
#endif
								this.ProcessItemPedBicycle(debugPed, item, nextNodeId, nextRightSegment, ref prevSegment, ref netManager.m_segments.m_buffer[(int)nextRightSegment], connectOffset, connectOffset, rightLaneIndex, rightLaneId); // ped
							}
						}

						// switch from bicycle lane to pedestrian lane
						int nextLaneIndex;
						uint nextLaneId;
						if ((this._vehicleTypes & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None &&
							prevSegment.GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Bicycle, out nextLaneIndex, out nextLaneId)) {
#if DEBUGNEWPF
							if (debugPed) {
								logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId} ({nextIsStartNode}): Exploring bicycle switch\n" +
									"\t" + $"_extPathType={queueItem.pathType}\n" +
									"\t" + $"_vehicleTypes={_vehicleTypes}, _laneTypes={_laneTypes}\n" +
									"\t" + $"_extVehicleType={queueItem.vehicleType}\n" +
									"\t" + $"_isRoadVehicle={_isRoadVehicle}\n" +
									"\t" + $"_isHeavyVehicle={_isHeavyVehicle}\n" +
									"\t" + $"_stablePath={_stablePath}\n" +
									"\t" + $"_isLaneConnectionObeyingEntity={_isLaneConnectionObeyingEntity}\n" +
									"\t" + $"_isLaneArrowObeyingEntity={_isLaneArrowObeyingEntity}\n\n" +
									"\t" + $"nextIsStartNode={nextIsStartNode}\n" +
									"\t" + $"nextLaneIndex={nextLaneIndex}\n" +
									"\t" + $"nextLaneId={nextLaneId}\n" +
									"\t" + $"nextIsStartNode={nextIsStartNode}\n"
									);
								FlushMainLog(logBuf, unitId);
							}
#endif
							this.ProcessItemPedBicycle(debugPed, item, nextNodeId, prevSegmentId, ref prevSegment, ref prevSegment, connectOffset, connectOffset, nextLaneIndex, nextLaneId); // bicycle
						}
					} else {
						// we are going from pedestrian lane to a beautification node

						for (int j = 0; j < 8; ++j) {
							ushort nextSegmentId = nextNode.GetSegment(j);
							if (nextSegmentId != 0 && nextSegmentId != prevSegmentId) {
#if DEBUGNEWPF
								if (debug) {
									FlushMainLog(logBuf, unitId);
								}
#endif

								this.ProcessItemCosts(debug, item, nextNodeId, nextSegmentId, ref prevSegment, /*prevSegmentRouting,*/ ref netManager.m_segments.m_buffer[(int)nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, false, true, isMiddle);
							}
						}
					}

					// NON-STOCK CODE START
					// switch from vehicle to pedestrian lane (parking)
					bool parkingAllowed = true;
					if (Options.prohibitPocketCars) {
						if (queueItem.vehicleType == ExtVehicleType.PassengerCar) {
							if ((byte)(item.m_lanesUsed & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
								// if pocket cars are prohibited, a citizen may only park their car once per path
								parkingAllowed = false;
							} else if ((byte)(item.m_lanesUsed & NetInfo.LaneType.PublicTransport) == 0) {
								// if the citizen is walking to their target (= no public transport used), the passenger car must be parked in the very last moment
								parkingAllowed = item.m_laneID == _endLaneA || item.m_laneID == _endLaneB;
								/*if (_conf.Debug.Switches[4]) {
									Log._Debug($"Path unit {unitId}: public transport has not been used. ");
								}*/
							}
						}
					}

					if (parkingAllowed) {
						// NON-STOCK CODE END
						NetInfo.LaneType laneType = this._laneTypes & ~NetInfo.LaneType.Pedestrian;
						VehicleInfo.VehicleType vehicleType = this._vehicleTypes & ~VehicleInfo.VehicleType.Bicycle;
						if ((byte)(item.m_lanesUsed & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
							laneType &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
						}
						int nextLaneIndex2;
						uint nextlaneId2;
						if (laneType != NetInfo.LaneType.None &&
							vehicleType != VehicleInfo.VehicleType.None &&
							prevSegment.GetClosestLane(prevLaneIndex, laneType, vehicleType, out nextLaneIndex2, out nextlaneId2)) {
							NetInfo.Lane lane5 = prevSegmentInfo.m_lanes[nextLaneIndex2];
							byte connectOffset2;
							if ((prevSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None == ((byte)(lane5.m_finalDirection & NetInfo.Direction.Backward) != 0)) {
								connectOffset2 = 1;
							} else {
								connectOffset2 = 254;
							}

							CustomPathFind.BufferItem item2 = item;
							if (this._randomParking) {
								item2.m_comparisonValue += (float)this._pathRandomizer.Int32(300u) / this._maxLength;
							}
#if DEBUGNEWPF
							if (debugPed) {
								logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId} ({nextIsStartNode}): Exploring parking switch\n" +
									"\t" + $"_extPathType={queueItem.pathType}\n" +
									"\t" + $"_vehicleTypes={_vehicleTypes}, _laneTypes={_laneTypes}\n" +
									"\t" + $"_extVehicleType={queueItem.vehicleType}\n" +
									"\t" + $"_isRoadVehicle={_isRoadVehicle}\n" +
									"\t" + $"_isHeavyVehicle={_isHeavyVehicle}\n" +
									"\t" + $"_stablePath={_stablePath}\n" +
									"\t" + $"_isLaneConnectionObeyingEntity={_isLaneConnectionObeyingEntity}\n" +
									"\t" + $"_isLaneArrowObeyingEntity={_isLaneArrowObeyingEntity}\n\n" +
									"\t" + $"nextIsStartNode={nextIsStartNode}\n" +
									"\t" + $"nextLaneIndex2={nextLaneIndex2}\n" +
									"\t" + $"nextlaneId2={nextlaneId2}\n" +
									"\t" + $"nextIsStartNode={nextIsStartNode}\n"
									);
								FlushMainLog(logBuf, unitId);
							}
#endif
							this.ProcessItemPedBicycle(debugPed, item2, nextNodeId, prevSegmentId, ref prevSegment, ref prevSegment, connectOffset2, 128, nextLaneIndex2, nextlaneId2); // ped
						}
					}
				}
			} else {
				// we are going to a non-pedestrian lane

				bool allowPedestrian = (byte)(this._laneTypes & NetInfo.LaneType.Pedestrian) != 0; // allow pedestrian switching to vehicle?
				bool nextIsBeautificationNode = nextNode.Info.m_class.m_service == ItemClass.Service.Beautification;
				bool allowPedestrians = false; // is true if cim is using a bike
				byte parkingConnectOffset = 0;
				if (allowPedestrian) {
					if (prevIsBicycleLane) {
						parkingConnectOffset = connectOffset;
						allowPedestrians = nextIsBeautificationNode;
					} else if (this._vehicleLane != 0u) {
						// there is a parked vehicle position
						if (this._vehicleLane != item.m_laneID) {
							// we have not reached the parked vehicle yet
							allowPedestrian = false;
						} else {
							// pedestrian switches to parked vehicle
							parkingConnectOffset = this._vehicleOffset;
						}
					} else if (this._stablePath) {
						// enter a bus
						parkingConnectOffset = 128;
					} else {
						// pocket car spawning
						if (Options.prohibitPocketCars &&
								queueItem.vehicleType == ExtVehicleType.PassengerCar &&
								(queueItem.pathType == ExtCitizenInstance.ExtPathType.WalkingOnly || (queueItem.pathType == ExtCitizenInstance.ExtPathType.DrivingOnly && item.m_position.m_segment != _startSegmentA && item.m_position.m_segment != _startSegmentB))) {
							allowPedestrian = false;
						} else {
							parkingConnectOffset = (byte)this._pathRandomizer.UInt32(1u, 254u);
						}
					}
				}

				if ((this._vehicleTypes & (VehicleInfo.VehicleType.Ferry /* | VehicleInfo.VehicleType.Monorail*/)) != VehicleInfo.VehicleType.None) {
					// monorail / ferry

					for (int k = 0; k < 8; k++) {
						ushort nextSegmentId = nextNode.GetSegment(k);
						if (nextSegmentId == 0 || nextSegmentId == prevSegmentId) {
							continue;
						}

						this.ProcessItemCosts(debug, item, nextNodeId, nextSegmentId, ref prevSegment, /*prevSegmentRouting,*/ ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, true, allowPedestrians, isMiddle);
					}

					if ((nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None /*&&
						(this._vehicleTypes & VehicleInfo.VehicleType.Monorail) == VehicleInfo.VehicleType.None*/) {
						this.ProcessItemCosts(debug, item, nextNodeId, prevSegmentId, ref prevSegment, /*prevSegmentRouting,*/ ref prevSegment, ref prevRelSimilarLaneIndex, connectOffset, true, false, isMiddle);
					}
				} else {
					// road vehicles, trams, trains, metros, monorails, etc.


					// specifies if vehicles should follow lane arrows
					bool isStrictLaneChangePolicyEnabled = false;
					// specifies if the entity is allowed to u-turn (in general)
					bool isEntityAllowedToUturn = (this._vehicleTypes & (VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Monorail)) == VehicleInfo.VehicleType.None;
					// specifies if thes next node allows for u-turns
					bool isUturnAllowedHere = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
					/*
					 * specifies if u-turns are handled by custom code.
					 * If not (performCustomVehicleUturns == false) AND the vanilla u-turn condition (stockUturn) evaluates to true, then u-turns are handled by the vanilla code
					 */
					//bool performCustomVehicleUturns = false;
					bool prevIsRouted = prevLaneEndRouting.routed
#if DEBUG
						&& !_conf.Debug.Switches[11]
#endif
					;

					if (prevIsRouted) {
						bool prevIsOutgoingOneWay = nextIsStartNode ? prevSegmentRouting.startNodeOutgoingOneWay : prevSegmentRouting.endNodeOutgoingOneWay;
						bool nextIsUntouchable = (nextNode.m_flags & (NetNode.Flags.Untouchable)) != NetNode.Flags.None;
						bool nextIsTransitionOrJunction = (nextNode.m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) != NetNode.Flags.None;
						bool nextIsBend = (nextNode.m_flags & (NetNode.Flags.Bend)) != NetNode.Flags.None;

						// determine if the vehicle may u-turn at the target node according to customization
						isUturnAllowedHere =
							isUturnAllowedHere || // stock u-turn points
							(Options.junctionRestrictionsEnabled &&
							_isRoadVehicle && // only road vehicles may perform u-turns
							junctionManager.IsUturnAllowed(prevSegmentId, nextIsStartNode) && // only do u-turns if allowed
							!nextIsBeautificationNode && // no u-turns at beautification nodes // TODO refactor to JunctionManager
							prevIsCarLane && // u-turns for road vehicles only
							!_isHeavyVehicle && // only small vehicles may perform u-turns
							(nextIsTransitionOrJunction || nextIsBend) && // perform u-turns at transitions, junctions and bend nodes // TODO refactor to JunctionManager
							!prevIsOutgoingOneWay); // do not u-turn on one-ways // TODO refactor to JunctionManager

						isStrictLaneChangePolicyEnabled =
							!nextIsBeautificationNode && // do not obey lane arrows at beautification nodes
							!nextIsUntouchable &&
							_isLaneArrowObeyingEntity &&
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
						if (debug)
							logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId} ({nextIsStartNode}):\n" +
								"\t" + $"_extPathType={queueItem.pathType}\n" +
								"\t" + $"_vehicleTypes={_vehicleTypes}, _laneTypes={_laneTypes}\n" +
								"\t" + $"_extVehicleType={queueItem.vehicleType}\n" +
								"\t" + $"_isRoadVehicle={_isRoadVehicle}\n" +
								"\t" + $"_isHeavyVehicle={_isHeavyVehicle}\n" +
								"\t" + $"_stablePath={_stablePath}\n" +
								"\t" + $"_isLaneConnectionObeyingEntity={_isLaneConnectionObeyingEntity}\n" +
								"\t" + $"_isLaneArrowObeyingEntity={_isLaneArrowObeyingEntity}\n\n" +
								"\t" + $"prevIsOutgoingOneWay={prevIsOutgoingOneWay}\n" +
								"\t" + $"prevIsRouted={prevIsRouted}\n\n" +
								"\t" + $"nextIsStartNode={nextIsStartNode}\n" +
								"\t" + $"isNextBeautificationNode={nextIsBeautificationNode}\n" +
								//"\t" + $"nextIsRealJunction={nextIsRealJunction}\n" +
								"\t" + $"nextIsTransitionOrJunction={nextIsTransitionOrJunction}\n" +
								"\t" + $"nextIsBend={nextIsBend}\n" +
								"\t" + $"nextIsUntouchable={nextIsUntouchable}\n" +
								"\t" + $"allowPedestrians={allowPedestrians}\n" +
								"\t" + $"isCustomUturnAllowed={junctionManager.IsUturnAllowed(prevSegmentId, nextIsStartNode)}\n" +
								"\t" + $"isStrictLaneArrowPolicyEnabled={isStrictLaneChangePolicyEnabled}\n" +
								"\t" + $"isEntityAllowedToUturn={isEntityAllowedToUturn}\n" +
								"\t" + $"isUturnAllowedHere={isUturnAllowedHere}\n"
								//"\t" + $"performCustomVehicleUturns={performCustomVehicleUturns}\n"
								);
#endif
					} else {
#if DEBUGNEWPF
						if (debug)
							logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId} ({nextIsStartNode}):\n" +
								"\t" + $"_extPathType={queueItem.pathType}\n" +
								"\t" + $"_vehicleTypes={_vehicleTypes}, _laneTypes={_laneTypes}\n" +
								"\t" + $"_extVehicleType={queueItem.vehicleType}\n" +
								"\t" + $"_isRoadVehicle={_isRoadVehicle}\n" +
								"\t" + $"_isHeavyVehicle={_isHeavyVehicle}\n" +
								"\t" + $"_stablePath={_stablePath}\n" +
								"\t" + $"_isLaneConnectionObeyingEntity={_isLaneConnectionObeyingEntity}\n" +
								"\t" + $"_isLaneArrowObeyingEntity={_isLaneArrowObeyingEntity}\n\n" +
								"\t" + $"prevIsRouted={prevIsRouted}\n\n"
							);
#endif
					}

					if (allowPedestrians || !prevIsRouted) {
						/*
						 * pedestrian to bicycle switch or no routing information available:
						 *		if pedestrian lanes should be explored (allowPedestrians == true): do this here
						 *		if previous segment has custom routing (prevIsRouted == true): do NOT explore vehicle lanes here, else: vanilla exploration of vehicle lanes
						*/

#if DEBUGNEWPF
						if (debug) {
							logBuf.Add(
								$"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
								"\t" + $"-> using DEFAULT exploration mode\n"
								);
							FlushMainLog(logBuf, unitId);
						}
#endif

						/*if (performCustomVehicleUturns) {
							isUturnAllowedHere = true;
							isEntityAllowedToUturn = true;
						}*/

						ushort nextSegmentId = prevSegment.GetRightSegment(nextNodeId);
						for (int k = 0; k < 8; ++k) {
							if (nextSegmentId == 0 || nextSegmentId == prevSegmentId) {
								break;
							}

							if (ProcessItemCosts(debug, item, nextNodeId, nextSegmentId, ref prevSegment, /*prevSegmentRouting,*/ ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, !prevIsRouted, allowPedestrians, isMiddle)) {
								// exceptional u-turns
								isUturnAllowedHere = true;
							}

							nextSegmentId = netManager.m_segments.m_buffer[nextSegmentId].GetRightSegment(nextNodeId);
						}
					}

					if (prevIsRouted) {
						/* routed vehicle paths */

#if DEBUGNEWPF
						if (debug)
							logBuf.Add(
								$"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
								"\t" + $"-> using CUSTOM exploration mode\n"
								);
#endif
						bool canUseLane = CanUseLane(debug, item.m_position.m_segment, prevSegmentInfo, item.m_position.m_lane, prevLaneInfo);
						LaneTransitionData[] laneTransitions = prevLaneEndRouting.transitions;
						if (laneTransitions != null && (canUseLane || Options.vehicleRestrictionsAggression != VehicleRestrictionsAggression.Strict)) {

#if DEBUGNEWPF
							if (debug)
								logBuf.Add(
									$"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
									"\t" + $"CUSTOM exploration\n"
									);
#endif

							LaneChangingCostCalculationMode laneChangingCostCalculationMode = LaneChangingCostCalculationMode.None; // lane changing cost calculation mode to use
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
								if (debug)
									logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
										"\t" + $"applied vehicle restrictions for vehicle {queueItem.vehicleId}, type {queueItem.vehicleType}:\n" +
										"\t" + $"=> laneSelectionCost={laneSelectionCost}\n"
										);
#endif
							}

							if (_isRoadVehicle &&
								prevLaneInfo != null &&
								prevIsCarLane) {

								if (Options.advancedAI) {
									laneChangingCostCalculationMode = LaneChangingCostCalculationMode.ByGivenDistance;
#if DEBUGNEWPF
									if (debug)
										logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
											"\t" + $"AI is active, prev is car lane and we are a car\n"
											);
#endif
								}

								/*
								 * =======================================================================================================
								 * (2) Apply car ban district policies
								 * =======================================================================================================
								 */

								// Apply costs for traffic ban policies
								if ((prevLaneInfo.m_laneType & NetInfo.LaneType.Vehicle) == NetInfo.LaneType.Vehicle &&
										(prevLaneInfo.m_vehicleType & this._vehicleTypes) == VehicleInfo.VehicleType.Car &&
										(netManager.m_segments.m_buffer[item.m_position.m_segment].m_flags & this._carBanMask) != NetSegment.Flags.None) {
									// heavy vehicle ban / car ban ("Old Town" policy)
									if (laneSelectionCost == null) {
										laneSelectionCost = 1f;
									}
#if DEBUGNEWPF
									float? oldLaneSelectionCost = laneSelectionCost;
#endif
									laneSelectionCost *= 7.5f;

#if DEBUGNEWPF
									if (debug)
										logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
											"\t" + $"applied heavy vehicle ban / car ban ('Old Town' policy):\n" +
											"\t" + $"oldLaneSelectionCost={oldLaneSelectionCost}\n" +
											"\t" + $"=> laneSelectionCost={laneSelectionCost}\n"
											);
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
								if ((prevLaneInfo.m_laneType & NetInfo.LaneType.TransportVehicle) != NetInfo.LaneType.None) {
									// previous lane is a public transport lane
									if ((queueItem.vehicleType & ExtVehicleType.Bus) != ExtVehicleType.None) {
										if (laneSelectionCost == null) {
											laneSelectionCost = 1f;
										}
#if DEBUGNEWPF
										float? oldLaneSelectionCost = laneSelectionCost;
#endif
										laneSelectionCost *= _conf.PathFinding.PublicTransportLaneReward; // (1)
#if DEBUGNEWPF
										if (debug)
											logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
												"\t" + $"applied bus-on-transport lane reward:\n" +
												"\t" + $"oldLaneSelectionCost={oldLaneSelectionCost}\n" +
												"\t" + $"=> laneSelectionCost={laneSelectionCost}\n"
												);
#endif
									} else if ((queueItem.vehicleType & (ExtVehicleType.RoadPublicTransport | ExtVehicleType.Service | ExtVehicleType.Emergency)) == ExtVehicleType.None) {
										if (laneSelectionCost == null) {
											laneSelectionCost = 1f;
										}
#if DEBUGNEWPF
										float? oldLaneSelectionCost = laneSelectionCost;
#endif
										laneSelectionCost *= _conf.PathFinding.PublicTransportLanePenalty; // (2)
#if DEBUGNEWPF
										if (debug)
											logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
												"\t" + $"applied car-on-transport lane penalty:\n" +
												"\t" + $"oldLaneSelectionCost={oldLaneSelectionCost}\n" +
												"\t" + $"=> laneSelectionCost={laneSelectionCost}\n"
												);
#endif
									} else {
										// (3), do nothing
									}
								}

								/*
								 * =======================================================================================================
								 * (4) Apply costs for large vehicles using inner lanes on highways
								 * =======================================================================================================
								 */

								bool nextIsJunction = (netManager.m_nodes.m_buffer[nextNodeId].m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) == NetNode.Flags.Junction;
								bool nextIsRealJunction = nextIsJunction && (netManager.m_nodes.m_buffer[nextNodeId].m_flags & (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)) != (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut);
								ushort prevNodeId = (nextNodeId == prevSegment.m_startNode) ? prevSegment.m_endNode : prevSegment.m_startNode;
								//bool prevIsRealJunction = (netManager.m_nodes.m_buffer[prevNodeId].m_flags & NetNode.Flags.Junction) != NetNode.Flags.None && (netManager.m_nodes.m_buffer[prevNodeId].m_flags & (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)) != (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut);
								if (prevLaneInfo.m_similarLaneCount > 1) {
									if (_isHeavyVehicle &&
										Options.preferOuterLane &&
										prevSegmentRouting.highway &&
										_pathRandomizer.Int32(_conf.PathFinding.HeavyVehicleInnerLanePenaltySegmentSel) == 0
										/* && (netManager.m_nodes.m_buffer[prevNodeId].m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) != NetNode.Flags.None */
									) {
										// penalize large vehicles for using inner lanes
										if (laneSelectionCost == null) {
											laneSelectionCost = 1f;
										}
#if DEBUGNEWPF
										float? oldLaneSelectionCost = laneSelectionCost;
#endif
										float prevRelOuterLane = ((float)prevOuterSimilarLaneIndex / (float)(prevLaneInfo.m_similarLaneCount - 1));
										laneSelectionCost *= 1f + _conf.PathFinding.HeavyVehicleMaxInnerLanePenalty * prevRelOuterLane;
#if DEBUGNEWPF
										if (debug)
											logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
												"\t" + $"applied inner lane penalty:\n" +
												"\t" + $"oldLaneSelectionCost={oldLaneSelectionCost}\n" +
												"\t" + $"=> laneSelectionCost={laneSelectionCost}\n"
												);
#endif
									}

									/*
									 * =======================================================================================================
									 * (5) Apply costs for randomized lane selection in front of junctions and highway transitions
									 * =======================================================================================================
									 */
									if (Options.advancedAI &&
												!_stablePath &&
												!_isHeavyVehicle &&
												nextIsJunction &&
												_pathRandomizer.Int32(_conf.AdvancedVehicleAI.LaneRandomizationJunctionSel) == 0) {
										// randomized lane selection at junctions
										if (laneSelectionCost == null) {
											laneSelectionCost = 1f;
										}
#if DEBUGNEWPF
										float? oldLaneSelectionCost = laneSelectionCost;
#endif
										laneSelectionCost *= 1f + _pathRandomizer.Int32(2) * _conf.AdvancedVehicleAI.LaneRandomizationCostFactor;
#if DEBUGNEWPF
										if (debug)
											logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
												"\t" + $"applied lane randomizations at junctions:\n" +
												"\t" + $"oldLaneSelectionCost={oldLaneSelectionCost}\n" +
												"\t" + $"=> laneSelectionCost={laneSelectionCost}\n"
												);
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

									segmentSelectionCost *= 1f + _conf.AdvancedVehicleAI.JunctionBaseCost;
								}

								/*
								 * =======================================================================================================
								 * (7) Apply traffic measurement costs for segment selection
								 * =======================================================================================================
								 */
								if (Options.advancedAI && (queueItem.vehicleType & (ExtVehicleType.RoadVehicle & ~ExtVehicleType.Bus)) != ExtVehicleType.None && !_stablePath) {
									// segment selection based on segment traffic volume
									NetInfo.Direction prevFinalDir = nextIsStartNode ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
									prevFinalDir = ((prevSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? prevFinalDir : NetInfo.InvertDirection(prevFinalDir);
									TrafficMeasurementManager.SegmentDirTrafficData prevDirTrafficData = trafficMeasurementManager.segmentDirTrafficData[trafficMeasurementManager.GetDirIndex(item.m_position.m_segment, prevFinalDir)];

									float segmentTraffic = Mathf.Clamp(1f - (float)prevDirTrafficData.meanSpeed / (float)TrafficMeasurementManager.REF_REL_SPEED + item.m_trafficRand, 0, 1f);

									if (segmentSelectionCost == null) {
										segmentSelectionCost = 1f;
									}

									segmentSelectionCost *= 1f +
										_conf.AdvancedVehicleAI.TrafficCostFactor *
										segmentTraffic;

#if DEBUGNEWPF
									if (debug)
										logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
											"\t" + $"applied traffic measurement costs for segment selection:\n" +
											"\t" + $"segmentTraffic={segmentTraffic}\n" +
											"\t" + $"=> segmentSelectionCost={segmentSelectionCost}\n"
											);
#endif

									if (_conf.AdvancedVehicleAI.LaneDensityRandInterval > 0 && nextIsRealJunction) {
										item.m_trafficRand = 0.01f * ((float)_pathRandomizer.Int32((uint)_conf.AdvancedVehicleAI.LaneDensityRandInterval + 1u) - _conf.AdvancedVehicleAI.LaneDensityRandInterval / 2f);

#if DEBUGNEWPF
										if (debug)
											logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
												"\t" + $"updated item.m_trafficRand:\n" +
												"\t" + $"=> item.m_trafficRand={item.m_trafficRand}\n"
												);
#endif
									}
								}

#if DEBUGNEWPF
								if (debug)
									logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
										"\t" + $"calculated traffic stats:\n" +
										"\t" + $"_vehicleTypes={_vehicleTypes}, _laneTypes={_laneTypes}\n" +
										"\t" + $"_extVehicleType={queueItem.vehicleType}\n" +
										"\t" + $"_isRoadVehicle={_isRoadVehicle}\n" +
										"\t" + $"_isHeavyVehicle={_isHeavyVehicle}\n" +
										"\t" + $"_isLaneConnectionObeyingEntity={_isLaneConnectionObeyingEntity}\n" +
										"\t" + $"_isLaneArrowObeyingEntity={_isLaneArrowObeyingEntity}\n\n" +
										"\t" + $"laneSelectionCost={laneSelectionCost}\n" +
										"\t" + $"segmentSelectionCost={segmentSelectionCost}\n"
										);
#endif
							}

							for (int k = 0; k < laneTransitions.Length; ++k) {
								ushort nextSegmentId = laneTransitions[k].segmentId;

								if (nextSegmentId == 0) {
#if DEBUGNEWPF
									if (debug) {
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

								bool uturn = nextSegmentId == prevSegmentId;
								if (uturn) {
									// prevent double/forbidden exploration of previous segment by vanilla code during this method execution
									if (! isEntityAllowedToUturn || ! isUturnAllowedHere) {
#if DEBUGNEWPF
										if (debug) {
											logBuf.Add(
												$"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
												"\t" + $"CUSTOM exploration\n" +
												"\t" + $"transition iteration {k}:\n" +
												"\t" + $"{laneTransitions[k].ToString()}\n" +
												"\t" + $"*** SKIPPING *** (u-turns prohibited)\n"
												);
											FlushMainLog(logBuf, unitId);
										}
#endif
										continue;
									}
								}

								if (laneTransitions[k].type == LaneEndTransitionType.Invalid) {
#if DEBUGNEWPF
									if (debug) {
										logBuf.Add(
											$"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
											"\t" + $"CUSTOM exploration\n" +
											"\t" + $"transition iteration {k}:\n" +
											"\t" + $"{laneTransitions[k].ToString()}\n" +
											"\t" + $"relaxedLaneChanging={relaxedLaneChanging}\n" +
											"\t" + $"isStrictLaneChangePolicyEnabled={relaxedLaneChanging}\n" +
											"\t" + $"*** SKIPPING *** (invalid transition)\n"
											);
										FlushMainLog(logBuf, unitId);
									}
#endif
									continue;
								}

								// allow vehicles to ignore strict lane routing when moving off
								bool relaxedLaneChanging =
									_isRoadVehicle &&
									(queueItem.vehicleType & (ExtVehicleType.Service | ExtVehicleType.PublicTransport | ExtVehicleType.Emergency)) != ExtVehicleType.None &&
									queueItem.vehicleId == 0 &&
									(laneTransitions[k].laneId == _startLaneA || laneTransitions[k].laneId == _startLaneB);

								if (! relaxedLaneChanging &&
									(isStrictLaneChangePolicyEnabled && laneTransitions[k].type == LaneEndTransitionType.Relaxed)) {
#if DEBUGNEWPF
									if (debug) {
										logBuf.Add(
											$"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
											"\t" + $"CUSTOM exploration\n" +
											"\t" + $"transition iteration {k}:\n" +
											"\t" + $"{laneTransitions[k].ToString()}\n" +
											"\t" + $"relaxedLaneChanging={relaxedLaneChanging}\n" +
											"\t" + $"isStrictLaneChangePolicyEnabled={relaxedLaneChanging}\n" +
											"\t" + $"*** SKIPPING *** (incompatible lane)\n"
											);
										FlushMainLog(logBuf, unitId);
									}
#endif
									continue;
								}

#if DEBUGNEWPF
								if (debug) {
									logBuf.Add(
										$"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
										"\t" + $"CUSTOM exploration\n" +
										"\t" + $"transition iteration {k}:\n" +
										"\t" + $"{laneTransitions[k].ToString()}\n" +
										"\t" + $"> PERFORMING EXPLORATION NOW <\n"
										);
									FlushMainLog(logBuf, unitId);
								}
#endif

								bool foundForced = false;
								int prevLaneIndexFromInner = prevRelSimilarLaneIndex;
								if (ProcessItemCosts(debug, false, laneChangingCostCalculationMode, item, nextNodeId, nextSegmentId, ref prevSegment, /*prevSegmentRouting,*/ ref netManager.m_segments.m_buffer[nextSegmentId], /*routingManager.segmentRoutings[nextSegmentId],*/ ref prevLaneIndexFromInner, connectOffset, true, false, laneTransitions[k].laneIndex, laneTransitions[k].laneId, laneTransitions[k].distance, segmentSelectionCost, laneSelectionCost, isMiddle, out foundForced)) {
										// process exceptional u-turning in vanilla code
										isUturnAllowedHere = true;
								}
							}
						}
					}

					if (!prevIsRouted && isEntityAllowedToUturn && isUturnAllowedHere) {
#if DEBUGNEWPF
						if (debug) {
							logBuf.Add($"path unit {unitId}\n" +
								$"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
								"\t" + $"-> exploring DEFAULT u-turn\n"
							);
							FlushMainLog(logBuf, unitId);
						}
#endif

						this.ProcessItemCosts(debug, item, nextNodeId, prevSegmentId, ref prevSegment, /*prevSegmentRouting,*/ ref prevSegment, ref prevRelSimilarLaneIndex, connectOffset, true, false, isMiddle);
					}
				}

				if (allowPedestrian) {
					// switch from walking to driving a car, bus, etc.
					int nextLaneIndex;
					uint nextLaneId;
					if (prevSegment.GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Pedestrian, this._vehicleTypes, out nextLaneIndex, out nextLaneId)) {
#if DEBUGNEWPF
						if (debugPed) {
							logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId} ({nextIsStartNode}): Exploring vehicle switch\n" +
								"\t" + $"_extPathType={queueItem.pathType}\n" +
								"\t" + $"_vehicleTypes={_vehicleTypes}, _laneTypes={_laneTypes}\n" +
								"\t" + $"_extVehicleType={queueItem.vehicleType}\n" +
								"\t" + $"_isRoadVehicle={_isRoadVehicle}\n" +
								"\t" + $"_isHeavyVehicle={_isHeavyVehicle}\n" +
								"\t" + $"_stablePath={_stablePath}\n" +
								"\t" + $"_isLaneConnectionObeyingEntity={_isLaneConnectionObeyingEntity}\n" +
								"\t" + $"_isLaneArrowObeyingEntity={_isLaneArrowObeyingEntity}\n\n" +
								"\t" + $"nextIsStartNode={nextIsStartNode}\n" +
								"\t" + $"nextLaneIndex={nextLaneIndex}\n" +
								"\t" + $"nextLaneId={nextLaneId}\n" +
								"\t" + $"nextIsStartNode={nextIsStartNode}\n"
								);
							FlushMainLog(logBuf, unitId);
						}
#endif
						this.ProcessItemPedBicycle(debugPed, item, nextNodeId, prevSegmentId, ref prevSegment, ref prevSegment, parkingConnectOffset, parkingConnectOffset, nextLaneIndex, nextLaneId); // ped
					}
				} // allowPedSwitch
			} // !prevIsPedestrianLane

			if (nextNode.m_lane != 0u &&
				(!Options.prohibitPocketCars || queueItem.vehicleType != ExtVehicleType.PassengerCar || (item.m_lanesUsed & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == NetInfo.LaneType.None)) {
				// transport lines, cargo lines, etc.

				bool targetDisabled = (nextNode.m_flags & (NetNode.Flags.Disabled | NetNode.Flags.DisableOnlyMiddle)) == NetNode.Flags.Disabled;
				ushort nextSegmentId = netManager.m_lanes.m_buffer[nextNode.m_lane].m_segment;
				if (nextSegmentId != 0 && nextSegmentId != item.m_position.m_segment) {
#if DEBUGNEWPF
					if (debug) {
						logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId} ({nextIsStartNode}): Exploring transport segment\n" +
							"\t" + $"_extPathType={queueItem.pathType}\n" +
							"\t" + $"_vehicleTypes={_vehicleTypes}, _laneTypes={_laneTypes}\n" +
							"\t" + $"_extVehicleType={queueItem.vehicleType}\n" +
							"\t" + $"_isRoadVehicle={_isRoadVehicle}\n" +
							"\t" + $"_isHeavyVehicle={_isHeavyVehicle}\n" +
							"\t" + $"_stablePath={_stablePath}\n" +
							"\t" + $"_isLaneConnectionObeyingEntity={_isLaneConnectionObeyingEntity}\n" +
							"\t" + $"_isLaneArrowObeyingEntity={_isLaneArrowObeyingEntity}\n\n" +
							"\t" + $"nextNode.m_lane={nextNode.m_lane}\n" +
							"\t" + $"nextSegmentId={nextSegmentId}\n" +
							"\t" + $"nextIsStartNode={nextIsStartNode}\n"
							);
						FlushMainLog(logBuf, unitId);
					}
#endif
					this.ProcessItemPublicTransport(debug, item, nextNodeId, targetDisabled, nextSegmentId, ref prevSegment, ref netManager.m_segments.m_buffer[nextSegmentId], nextNode.m_lane, nextNode.m_laneOffset, connectOffset);
				}
			}

#if DEBUGNEWPF
			if (debug) {
				FlushMainLog(logBuf, unitId);
			}
#endif
		}

		// 2
		private void ProcessItemPublicTransport(bool debug, BufferItem item, ushort nextNodeId, bool targetDisabled, ushort nextSegmentId, ref NetSegment prevSegment, ref NetSegment nextSegment, uint nextLane, byte offset, byte connectOffset) {
			if ((nextSegment.m_flags & _disableMask) != NetSegment.Flags.None) {
				return;
			}
			NetManager netManager = Singleton<NetManager>.instance;
			if (targetDisabled && ((netManager.m_nodes.m_buffer[(int)nextSegment.m_startNode].m_flags | netManager.m_nodes.m_buffer[(int)nextSegment.m_endNode].m_flags) & NetNode.Flags.Disabled) == NetNode.Flags.None) {
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

			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = prevSegment.Info;
			int nextNumLanes = nextSegmentInfo.m_lanes.Length;
			uint curLaneId = nextSegment.m_lanes;
			float prevMaxSpeed = 1f;
			float prevSpeed = 1f;
			NetInfo.LaneType prevLaneType = NetInfo.LaneType.None;
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				prevMaxSpeed = GetLaneSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, prevLaneInfo); // SpeedLimitManager.GetLockFreeGameSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, ref lane2); // NON-STOCK CODE
				prevLaneType = prevLaneInfo.m_laneType;
				if ((byte)(prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
					prevLaneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
				prevSpeed = this.CalculateLaneSpeed(prevMaxSpeed, connectOffset, item.m_position.m_offset, ref prevSegment, prevLaneInfo); // NON-STOCK CODE
			}
			float segLength;
			if (prevLaneType == NetInfo.LaneType.PublicTransport) {
				segLength = netManager.m_lanes.m_buffer[item.m_laneID].m_length;
			} else {
				segLength = Mathf.Max(SEGMENT_MIN_AVERAGE_LENGTH, prevSegment.m_averageLength);
			}
			float offsetLength = (float)Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * segLength;
			float methodDistance = item.m_methodDistance + offsetLength;
			float comparisonValue = item.m_comparisonValue + offsetLength / (prevSpeed * this._maxLength);
			float duration = item.m_duration + offsetLength / prevMaxSpeed; 
			Vector3 b = netManager.m_lanes.m_buffer[item.m_laneID].CalculatePosition((float)connectOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
			uint laneIndex = 0;
#if DEBUG
			int wIter = 0;
#endif
			while (laneIndex < nextNumLanes && curLaneId != 0u) {
#if DEBUG
				++wIter;
				if (wIter >= 20) {
					Log.Error("Too many iterations in ProcessItem2!");
					break;
				}
#endif

				if (nextLane == curLaneId) {
					NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[laneIndex];
					if (nextLaneInfo.CheckType(this._laneTypes, this._vehicleTypes)) {
						Vector3 a = netManager.m_lanes.m_buffer[nextLane].CalculatePosition((float)offset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
						float distance = Vector3.Distance(a, b);
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
						if ((byte)(nextLaneInfo.m_laneType & prevLaneType) == 0) {
							nextItem.m_methodDistance = 0f;
						} else {
							nextItem.m_methodDistance = methodDistance + distance;
						}
						float nextMaxSpeed = GetLaneSpeedLimit(nextSegmentId, laneIndex, curLaneId, nextLaneInfo); // SpeedLimitManager.GetLockFreeGameSpeedLimit(nextSegmentId, laneIndex, curLaneId, ref lane3); // NON-STOCK CODE
						if (nextLaneInfo.m_laneType != NetInfo.LaneType.Pedestrian || nextItem.m_methodDistance < _conf.PathFinding.MaxWalkingDistance) {
							nextItem.m_comparisonValue = comparisonValue + distance / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength); // NON-STOCK CODE
							nextItem.m_duration = duration + distance / ((prevMaxSpeed + nextMaxSpeed) * 0.5f);
							if ((nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
								nextItem.m_direction = NetInfo.InvertDirection(nextLaneInfo.m_finalDirection);
							} else {
								nextItem.m_direction = nextLaneInfo.m_finalDirection;
							}
							if (nextLane == this._startLaneA) {
								if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetA) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetA)) {
									return;
								}
								float nextSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetA, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo); // NON-STOCK CODE
								float nextOffsetDistance = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetA)) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;
								nextItem.m_comparisonValue += nextOffsetDistance * nextSegment.m_averageLength / (nextSpeed * this._maxLength);
								nextItem.m_duration += nextOffsetDistance * nextSegment.m_averageLength / nextSpeed;
							}
							if (nextLane == this._startLaneB) {
								if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetB) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetB)) {
									return;
								}
								float nextSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo); // NON-STOCK CODE
								float nextOffsetDistance = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetB)) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;
								nextItem.m_comparisonValue += nextOffsetDistance * nextSegment.m_averageLength / (nextSpeed * this._maxLength);
								nextItem.m_duration += nextOffsetDistance * nextSegment.m_averageLength / nextSpeed;
							}
							nextItem.m_laneID = nextLane;
							nextItem.m_lanesUsed = (item.m_lanesUsed | nextLaneInfo.m_laneType);
							nextItem.m_vehiclesUsed = (item.m_vehiclesUsed | nextLaneInfo.m_vehicleType);
							nextItem.m_trafficRand = 0;
#if DEBUGNEWPF
							if (debug) {
								_debugPositions[item.m_position.m_segment].Add(nextItem.m_position.m_segment);
							}
#endif
							this.AddBufferItem(nextItem, item.m_position);
						}
					}
					return;
				}
				curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
				++laneIndex;
			}
		}

		private bool ProcessItemCosts(bool debug, BufferItem item, ushort nextNodeId, ushort nextSegmentId, ref NetSegment prevSegment, /*SegmentRoutingData prevSegmentRouting,*/ ref NetSegment nextSegment, ref int laneIndexFromInner, byte connectOffset, bool enableVehicle, bool enablePedestrian, bool isMiddle) {
			bool foundForced = false;
			return ProcessItemCosts(debug, true, LaneChangingCostCalculationMode.None, item, nextNodeId, nextSegmentId, ref prevSegment, /*prevSegmentRouting,*/ ref nextSegment, /*routingManager.segmentRoutings[nextSegmentId],*/ ref laneIndexFromInner, connectOffset, enableVehicle, enablePedestrian, null, null, null, null, null, isMiddle, out foundForced);
		}

		// 3
		private bool ProcessItemCosts(bool debug, bool obeyStockLaneArrows, LaneChangingCostCalculationMode laneChangingCostCalculationMode, BufferItem item, ushort nextNodeId, ushort nextSegmentId, ref NetSegment prevSegment, /* SegmentRoutingData prevSegmentRouting,*/ ref NetSegment nextSegment, /*SegmentRoutingData nextSegmentRouting,*/ ref int laneIndexFromInner, byte connectOffset, bool enableVehicle, bool enablePedestrian, int? forcedLaneIndex, uint? forcedLaneId, byte? forcedLaneDist, float? segmentSelectionCost, float? laneSelectionCost, bool isMiddle, out bool foundForced) {
#if DEBUGNEWPF && DEBUG
			debug = debug && _conf.Debug.Switches[1];
#else
			debug = false;
#endif

#if DEBUGNEWPF && DEBUG
			List<String> logBuf = null;
			if (debug)
				logBuf = new List<String>();
#endif

			foundForced = false;
			bool blocked = false;
			if ((nextSegment.m_flags & _disableMask) != NetSegment.Flags.None) {
#if DEBUGNEWPF
				if (debug) {
					logBuf.Add($"Segment is PathFailed or flooded: {nextSegment.m_flags}");
					logBuf.Add("-- method returns --");
					FlushCostLog(logBuf);
				}
#endif
				return blocked;
			}
			NetManager netManager = Singleton<NetManager>.instance;

			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = prevSegment.Info;
			int nextNumLanes = nextSegmentInfo.m_lanes.Length;
			NetInfo.Direction nextDir = (nextNodeId != nextSegment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
			NetInfo.Direction nextFinalDir = ((nextSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? nextDir : NetInfo.InvertDirection(nextDir);
			float turningAngle = 1f;

#if DEBUGNEWPF
			if (debug)
				logBuf.Add($"isStockLaneChangerUsed={Options.isStockLaneChangerUsed()}, _extVehicleType={queueItem.vehicleType}, nonBus={(queueItem.vehicleType & (ExtVehicleType.RoadVehicle & ~ExtVehicleType.Bus)) != ExtVehicleType.None}, _stablePath={_stablePath}, enableVehicle={enableVehicle}");
#endif

			float prevMaxSpeed = 1f;
			float prevLaneSpeed = 1f;
			NetInfo.LaneType prevLaneType = NetInfo.LaneType.None;
			VehicleInfo.VehicleType prevVehicleType = VehicleInfo.VehicleType.None;
			// NON-STOCK CODE START //
			bool nextIsStartNodeOfPrevSegment = prevSegment.m_startNode == nextNodeId;
			ushort sourceNodeId = nextIsStartNodeOfPrevSegment ? prevSegment.m_endNode : prevSegment.m_startNode;
			bool prevIsJunction = (netManager.m_nodes.m_buffer[sourceNodeId].m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) == NetNode.Flags.Junction;
#if COUNTSEGMENTSTONEXTJUNCTION
			bool prevIsRealJunction = prevIsJunction &&
									  (netManager.m_nodes.m_buffer[sourceNodeId].m_flags & (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)) != (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut);
#endif
			/*bool nextIsRealJunction = (netManager.m_nodes.m_buffer[nextNodeId].m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) == NetNode.Flags.Junction &&
									  (netManager.m_nodes.m_buffer[nextNodeId].m_flags & (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)) != (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut);*/
			int prevOuterSimilarLaneIndex = -1;
			NetInfo.Lane prevLaneInfo = null;
			// NON-STOCK CODE END //
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				prevLaneInfo = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				prevLaneType = prevLaneInfo.m_laneType;
				prevVehicleType = prevLaneInfo.m_vehicleType;
				prevMaxSpeed = /*emergencyLaneSelection ? 5f :*/ GetLaneSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, prevLaneInfo); // SpeedLimitManager.GetLockFreeGameSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, ref lane); // NON-STOCK CODE
				prevLaneSpeed = this.CalculateLaneSpeed(prevMaxSpeed, connectOffset, item.m_position.m_offset, ref prevSegment, prevLaneInfo); // NON-STOCK CODE
				
				// NON-STOCK CODE START
				if ((byte)(prevLaneInfo.m_direction & NetInfo.Direction.Forward) != 0) {
					prevOuterSimilarLaneIndex = prevLaneInfo.m_similarLaneCount - prevLaneInfo.m_similarLaneIndex - 1;
				} else {
					prevOuterSimilarLaneIndex = prevLaneInfo.m_similarLaneIndex;
				}
				// NON-STOCK CODE END
			}

			if (prevLaneType == NetInfo.LaneType.Vehicle && (prevVehicleType & VehicleInfo.VehicleType.Car) == VehicleInfo.VehicleType.None) {
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
					if ((byte)(nextDir & NetInfo.Direction.Forward) != 0) {
						nextDirection = nextSegment.m_endDirection;
					} else {
						nextDirection = nextSegment.m_startDirection;
					}
					float dirDotProd = prevDirection.x * nextDirection.x + prevDirection.z * nextDirection.z;
					if (dirDotProd >= turningAngle) {
#if DEBUGNEWPF
						if (debug) {
							logBuf.Add($"turningAngle < 1f! dirDotProd={dirDotProd} >= turningAngle{turningAngle}!");
							logBuf.Add("-- method returns --");
							FlushCostLog(logBuf);
						}
#endif
						return blocked;
					}
				}
			}

			float prevCost;
			if (prevLaneType == NetInfo.LaneType.PublicTransport) {
				prevCost = netManager.m_lanes.m_buffer[item.m_laneID].m_length;
			} else {
				prevCost = Mathf.Max(SEGMENT_MIN_AVERAGE_LENGTH, prevSegment.m_averageLength);
			}
			

#if DEBUGNEWPF
			float oldPrevCost = prevCost;
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
			if (debug)
				logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
				"\t" + $"applied traffic cost factors:\n" +
				"\t" + $"oldPrevCost={oldPrevCost}\n" +
				"\t" + $"=> prevCost={prevCost}\n"
				);
#endif

			// stock code check for vehicle ban policies removed
			// stock code for transport lane usage control removed

			if ((byte)(prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
				prevLaneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
			}
			float prevOffsetCost = (float)Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * prevCost;
			float prevMethodDist = item.m_methodDistance + prevOffsetCost;
			float prevDuration = item.m_duration + prevOffsetCost / prevMaxSpeed;
			// NON-STOCK: vehicle restriction are applied to previous segment length in MainPathFind (not here, and not to prevOffsetCost)
			float prevComparisonPlusOffsetCostOverSpeed = item.m_comparisonValue + prevOffsetCost / (prevLaneSpeed * this._maxLength);

			if (!_stablePath) {
				// CO randomization. Only randomizes over segments, not over lanes.
				if (segmentSelectionCost == null) { // NON-STOCK CODE
					Randomizer randomizer = new Randomizer(this._pathFindIndex << 16 | (uint)item.m_position.m_segment);
					prevOffsetCost *= (float)(randomizer.Int32(900, 1000 + (int)(prevSegment.m_trafficDensity * 10)) + this._pathRandomizer.Int32(20u)) * 0.001f;
				}
			}

			Vector3 prevLaneConnectPos = netManager.m_lanes.m_buffer[item.m_laneID].CalculatePosition((float)connectOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
			int newLaneIndexFromInner = laneIndexFromInner;
			bool transitionNode = (netManager.m_nodes.m_buffer[nextNodeId].m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;
			NetInfo.LaneType allowedLaneTypes = this._laneTypes;
			VehicleInfo.VehicleType allowedVehicleTypes = this._vehicleTypes;

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
			if (debug)
				logBuf.Add($"allowedVehicleTypes={allowedVehicleTypes} allowedLaneTypes={allowedLaneTypes}");
#endif

			// NON-STOCK CODE START //
			float laneChangeBaseCosts = 1f;
			float junctionBaseCosts = 1f;
			if (laneChangingCostCalculationMode != LaneChangingCostCalculationMode.None) {
				float rand = (float)this._pathRandomizer.Int32(101u) / 100f;
				laneChangeBaseCosts = _conf.AdvancedVehicleAI.LaneChangingBaseMinCost + rand * (_conf.AdvancedVehicleAI.LaneChangingBaseMaxCost - _conf.AdvancedVehicleAI.LaneChangingBaseMinCost);
				if (prevIsJunction) {
					junctionBaseCosts = _conf.AdvancedVehicleAI.LaneChangingJunctionBaseCost;
				}
			}

			// NON-STOCK CODE END //

			uint laneIndex = forcedLaneIndex != null ? (uint)forcedLaneIndex : 0u; // NON-STOCK CODE, forcedLaneIndex is not null if the next node is a (real) junction
			uint curLaneId = (uint)(forcedLaneId != null ? forcedLaneId : nextSegment.m_lanes); // NON-STOCK CODE, forceLaneId is not null if the next node is a (real) junction
			while (laneIndex < nextNumLanes && curLaneId != 0u) {
				// NON-STOCK CODE START //
				if (forcedLaneIndex != null && laneIndex != forcedLaneIndex) {
#if DEBUGNEWPF
					if (debug)
						logBuf.Add($"forceLaneIndex break! laneIndex={laneIndex}");
#endif
					break;
				}
				// NON-STOCK CODE END //
				NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[laneIndex];

				if ((byte)(nextLaneInfo.m_finalDirection & nextFinalDir) != 0) {
					// lane direction is compatible
#if DEBUGNEWPF
					if (debug)
						logBuf.Add($"Lane direction check passed: {nextLaneInfo.m_finalDirection}");
#endif
					if ((nextLaneInfo.CheckType(allowedLaneTypes, allowedVehicleTypes)/* || (emergencyLaneSelection && nextLane.m_vehicleType == VehicleInfo.VehicleType.None)*/) &&
							(nextSegmentId != item.m_position.m_segment || laneIndex != (int)item.m_position.m_lane)) {
						// vehicle types match and no u-turn to the previous lane

#if DEBUGNEWPF
						if (debug)
							logBuf.Add($"vehicle type check passed: {nextLaneInfo.CheckType(allowedLaneTypes, allowedVehicleTypes)} && {(nextSegmentId != item.m_position.m_segment || laneIndex != (int)item.m_position.m_lane)}");
#endif

						// NON-STOCK CODE START //
						float nextMaxSpeed = GetLaneSpeedLimit(nextSegmentId, laneIndex, curLaneId, nextLaneInfo);
						float customDeltaCost = 0f;
						// NON-STOCK CODE END //

						Vector3 nextLaneEndPointPos;
						if ((byte)(nextDir & NetInfo.Direction.Forward) != 0) {
							nextLaneEndPointPos = netManager.m_lanes.m_buffer[curLaneId].m_bezier.d;
						} else {
							nextLaneEndPointPos = netManager.m_lanes.m_buffer[curLaneId].m_bezier.a;
						}
						float transitionCost = Vector3.Distance(nextLaneEndPointPos, prevLaneConnectPos); // This gives the distance of the previous to next lane endpoints.

#if DEBUGNEWPF
						if (debug)
							logBuf.Add($"costs from {nextSegmentId} (off {(byte)(((nextDir & NetInfo.Direction.Forward) == 0) ? 0 : 255)}) to {item.m_position.m_segment} (off {item.m_position.m_offset}), connectOffset={connectOffset}: transitionCost={transitionCost}");
#endif

						if (transitionNode) {
							transitionCost *= 2f;
						}

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
						nextItem.m_comparisonValue = 0;
						nextItem.m_position.m_segment = nextSegmentId;
						nextItem.m_position.m_lane = (byte)laneIndex;
						nextItem.m_position.m_offset = (byte)(((nextDir & NetInfo.Direction.Forward) == 0) ? 0 : 255);
						if ((byte)(nextLaneInfo.m_laneType & prevLaneType) == 0) {
							nextItem.m_methodDistance = 0f;
							// NON-STOCK CODE START
							if (Options.realisticPublicTransport && isMiddle && nextLaneInfo.m_laneType == NetInfo.LaneType.PublicTransport && (item.m_lanesUsed & NetInfo.LaneType.PublicTransport) != NetInfo.LaneType.None) {
								// apply penalty when switching public transport vehicles
								float transportTransitionPenalty = (_conf.PathFinding.PublicTransportTransitionMinPenalty + ((float)netManager.m_nodes.m_buffer[nextNodeId].m_maxWaitTime * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR) * (_conf.PathFinding.PublicTransportTransitionMaxPenalty - _conf.PathFinding.PublicTransportTransitionMinPenalty)) / (0.25f * this._maxLength);
#if DEBUGNEWPF
								if (debug)
									logBuf.Add($"applying public transport transition penalty: {transportTransitionPenalty}");
#endif
								nextItem.m_comparisonValue += transportTransitionPenalty;
							}
							// NON-STOCK CODE END
						} else {
							nextItem.m_methodDistance = prevMethodDist + transitionCost;
						}

#if DEBUGNEWPF
						if (debug)
							logBuf.Add($"checking if methodDistance is in range: {nextLaneInfo.m_laneType != NetInfo.LaneType.Pedestrian} || {nextItem.m_methodDistance < _conf.PathFinding.MaxWalkingDistance} ({nextItem.m_methodDistance})");
#endif

						if (nextLaneInfo.m_laneType != NetInfo.LaneType.Pedestrian || nextItem.m_methodDistance < _conf.PathFinding.MaxWalkingDistance) {

							// NON-STOCK CODE START //
							if (laneChangingCostCalculationMode == LaneChangingCostCalculationMode.None) {
								float transitionCostOverMeanMaxSpeed = transitionCost / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength);
								nextItem.m_comparisonValue += prevComparisonPlusOffsetCostOverSpeed + transitionCostOverMeanMaxSpeed; // stock code
							} else {
								nextItem.m_comparisonValue += item.m_comparisonValue;
								customDeltaCost = transitionCost + prevOffsetCost; // customDeltaCost now holds the costs for driving on the segment + costs for changing the segment
#if DEBUGNEWPF
								if (debug) {
									logBuf.Add($"Path from {nextSegmentId} (idx {laneIndex}, id {curLaneId}) to {item.m_position.m_segment} (lane {prevOuterSimilarLaneIndex} from outer, idx {item.m_position.m_lane}): laneChangingCostCalculationMode={laneChangingCostCalculationMode}, transitionCost={transitionCost}");
								}
#endif
							}
							nextItem.m_duration = prevDuration + transitionCost / ((prevMaxSpeed + nextMaxSpeed) * 0.5f);

							// account for public tranport transition costs on non-PT paths
							if (
#if DEBUG
								!_conf.Debug.Switches[20] &&
#endif
								Options.realisticPublicTransport &&
								(curLaneId == this._startLaneA || curLaneId == this._startLaneB) &&
								(item.m_lanesUsed & (NetInfo.LaneType.Pedestrian | NetInfo.LaneType.PublicTransport)) == NetInfo.LaneType.Pedestrian) {
								float transportTransitionPenalty = (2f * _conf.PathFinding.PublicTransportTransitionMaxPenalty) / (0.25f * this._maxLength);
#if DEBUGNEWPF
								if (debug)
									logBuf.Add($"applying public transport transition penalty on non-PT path: {transportTransitionPenalty}");
#endif
								nextItem.m_comparisonValue += transportTransitionPenalty;
							}
							// NON-STOCK CODE END //

							nextItem.m_direction = nextDir;
							if (curLaneId == this._startLaneA) {
								if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetA) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetA)) {
#if DEBUGNEWPF
									if (debug)
										logBuf.Add($"Current lane is start lane A. goto next lane");
#endif
									goto CONTINUE_LANE_LOOP;
								}
								float nextLaneSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetA, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo); // NON-STOCK CODE
								float nextOffset = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetA)) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;
								float nextSegLength = Mathf.Max(SEGMENT_MIN_AVERAGE_LENGTH, nextSegment.m_averageLength);
								nextItem.m_comparisonValue += nextOffset * nextSegLength / (nextLaneSpeed * this._maxLength);
								nextItem.m_duration += nextOffset * nextSegLength / nextLaneSpeed;
							}

							if (curLaneId == this._startLaneB) {
								if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetB) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetB)) {
#if DEBUGNEWPF
									if (debug)
										logBuf.Add($"Current lane is start lane B. goto next lane");
#endif
									goto CONTINUE_LANE_LOOP;
								}
								float nextLaneSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo); // NON-STOCK CODE
								float nextOffset = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetB)) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;
								float nextSegLength = Mathf.Max(SEGMENT_MIN_AVERAGE_LENGTH, nextSegment.m_averageLength);
								nextItem.m_comparisonValue += nextOffset * nextSegLength / (nextLaneSpeed * this._maxLength);
								nextItem.m_duration += nextOffset * nextSegLength / nextLaneSpeed;
							}

							if (!this._ignoreBlocked && (nextSegment.m_flags & NetSegment.Flags.Blocked) != NetSegment.Flags.None &&
								(byte)(nextLaneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
								// NON-STOCK CODE START //
								if (laneChangingCostCalculationMode != LaneChangingCostCalculationMode.None) {
#if DEBUGNEWPF
									float oldCustomDeltaCost = customDeltaCost;
#endif
									// apply vanilla game restriction policies
									customDeltaCost *= 10f;
#if DEBUGNEWPF
									if (debug)
										logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
											"\t" + $"applied blocked road cost factor on activated AI:\n" +
											"\t" + $"oldCustomDeltaCost={oldCustomDeltaCost}\n" +
											"\t" + $"=> customDeltaCost={customDeltaCost}\n"
											);
#endif
								} else {
									// NON-STOCK CODE END //
#if DEBUGNEWPF
									if (debug)
										logBuf.Add($"Applying blocked road cost factor on disabled advanced AI");
#endif

									nextItem.m_comparisonValue += 0.1f;
								}
								blocked = true;
							}

							if ((byte)(nextLaneInfo.m_laneType & prevLaneType) != 0 && nextLaneInfo.m_vehicleType == prevVehicleType) {
#if DEBUGNEWPF
								if (debug)
									logBuf.Add($"Applying stock lane changing costs. obeyStockLaneArrows={obeyStockLaneArrows}");
#endif


								if (obeyStockLaneArrows) {
									// this is CO's way of matching lanes between segments
									int firstTarget = (int)netManager.m_lanes.m_buffer[curLaneId].m_firstTarget;
									int lastTarget = (int)netManager.m_lanes.m_buffer[curLaneId].m_lastTarget;
									if (laneIndexFromInner < firstTarget || laneIndexFromInner >= lastTarget) {
										nextItem.m_comparisonValue += Mathf.Max(1f, transitionCost * 3f - 3f) / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength);
									}
								} // NON-STOCK CODE

								// stock code that prohibits cars to be on public transport lanes removed
								/*if (!this._transportVehicle && nextLaneInfo.m_laneType == NetInfo.LaneType.TransportVehicle) {
									nextItem.m_comparisonValue += 20f / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength);
								}*/
								
							}

							// NON-STOCK CODE START //
							bool addItem = true; // should we add the next item to the buffer?
							if (laneChangingCostCalculationMode != LaneChangingCostCalculationMode.None) {
								// Advanced AI cost calculation

#if DEBUGNEWPF
								if (debug)
									logBuf.Add($"Calculating advanced AI lane changing costs");
#endif

								int laneDist; // absolute lane distance
								if (laneChangingCostCalculationMode == LaneChangingCostCalculationMode.ByGivenDistance && forcedLaneDist != null) {
									laneDist = (byte)forcedLaneDist;
								} else {
									int nextOuterSimilarLaneIndex;
									if ((byte)(nextLaneInfo.m_direction & NetInfo.Direction.Forward) != 0) {
										nextOuterSimilarLaneIndex = nextLaneInfo.m_similarLaneCount - nextLaneInfo.m_similarLaneIndex - 1;
									} else {
										nextOuterSimilarLaneIndex = nextLaneInfo.m_similarLaneIndex;
									}

									int relLaneDist = nextOuterSimilarLaneIndex - prevOuterSimilarLaneIndex; // relative lane distance (positive: change to more outer lane, negative: change to more inner lane)
									laneDist = Math.Abs(relLaneDist);
								}

								// apply lane changing costs
								float laneMetric = 1f;
								bool relaxedLaneChanging = queueItem.vehicleId == 0 && (curLaneId == _startLaneA || curLaneId == _startLaneB);
								if (laneDist > 0 && !relaxedLaneChanging) {
									laneMetric = 1f + laneDist *
										junctionBaseCosts *
										laneChangeBaseCosts * // road type based lane changing cost factor
										(laneDist > 1 ? _conf.AdvancedVehicleAI.MoreThanOneLaneChangingCostFactor : 1f); // additional costs for changing multiple lanes at once
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
								float metric = laneMetric/* * junctionMetric*/;

								//float oldTransitionDistanceOverMaxSpeed = transitionCostOverMeanMaxSpeed;
								float finalDeltaCost = (metric * customDeltaCost) / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength);

//								if (finalDeltaCost < 0f) {
//									// should never happen
//#if DEBUG
//									Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId}, PF {this._pathFindIndex}: distanceOverMeanMaxSpeed < 0! seg. {nextSegmentId}, lane {laneIndex}, off {nextItem.m_position.m_offset} -> {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}. distanceOverMeanMaxSpeed={finalDeltaCost}, prevSpeed={prevUsage}"/* + ", prevSpeed={prevSpeed}"*/);
//#endif
//									finalDeltaCost = 0f;
//								} else if (Single.IsNaN(finalDeltaCost) || Single.IsInfinity(finalDeltaCost)) {
//									// Fallback if we mess something up. Should never happen.
//#if DEBUG
//									//if (costDebug)
//									Log.Error($"Pathfinder ({this._pathFindIndex}): distanceOverMeanMaxSpeed is NaN or Infinity: seg. {nextSegmentId}, lane {laneIndex}, off {nextItem.m_position.m_offset} -> {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}. {finalDeltaCost} // nextMaxSpeed={nextMaxSpeed} prevMaxSpeed={prevMaxSpeed} nextMaxSpeed={nextMaxSpeed} laneDist={laneDist} laneMetric={laneMetric} metric={metric}");
//#endif
//#if DEBUGNEWPF
//									Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId}, PF {this._pathFindIndex}: deltaCostOverMeanMaxSpeed is NaN! deltaCostOverMeanMaxSpeed={finalDeltaCost}");
//#endif
//									finalDeltaCost = oldTransitionDistanceOverMaxSpeed;
//								}

								nextItem.m_comparisonValue += finalDeltaCost;

								if (nextItem.m_comparisonValue > 1f) {
									// comparison value got too big. Do not add the lane to the buffer
									addItem = false;
								}
#if DEBUGNEWPF
								if (debug)
									logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
										$"-> TRANSIT to seg. {nextSegmentId}, lane {laneIndex}\n" +
										"\t" + $"prevMaxSpeed={prevMaxSpeed}\n" +
										"\t" + $"nextMaxSpeed={nextMaxSpeed}\n\n" +
										"\t" + $"laneChangingCostCalculationMode={laneChangingCostCalculationMode}\n\n" +
										"\t" + $"laneDist={laneDist}\n\n" +
										"\t" + $"_extVehicleType={queueItem.vehicleType}\n" +
										"\t" + $"laneChangeRoadBaseCost={laneChangeBaseCosts}\n" +
										"\t" + $"moreThanOneLaneCost={(laneDist > 1 ? _conf.AdvancedVehicleAI.MoreThanOneLaneChangingCostFactor : 1f)}\n" +
										"\t" + $"=> laneMetric={laneMetric}\n" +
										//"\t" + $"=> junctionMetric={junctionMetric}\n\n" +
										"\t" + $"=> metric={metric}\n" +
										"\t" + $"deltaCostOverMeanMaxSpeed={finalDeltaCost}\n" +
										"\t" + $"nextItem.m_comparisonValue={nextItem.m_comparisonValue}\n\n" +
										"\t" + $"=> addItem={addItem}\n"
										);
#endif
							}

							if (forcedLaneIndex != null && laneIndex == forcedLaneIndex && addItem) {
								foundForced = true;
							}

							if (addItem) {
								// NON-STOCK CODE END //

								nextItem.m_lanesUsed = (item.m_lanesUsed | nextLaneInfo.m_laneType);
								nextItem.m_vehiclesUsed = (item.m_vehiclesUsed | nextLaneInfo.m_vehicleType);
								nextItem.m_laneID = curLaneId;
								nextItem.m_trafficRand = item.m_trafficRand;
#if DEBUGNEWPF
								if (debug) {
									logBuf.Add($"adding item: seg {nextItem.m_position.m_segment}, lane {nextItem.m_position.m_lane} (idx {nextItem.m_laneID}), off {nextItem.m_position.m_offset} -> seg {item.m_position.m_segment}, lane {item.m_position.m_lane} (idx {item.m_laneID}), off {item.m_position.m_offset}, cost {nextItem.m_comparisonValue}, previous cost {item.m_comparisonValue}, methodDist {nextItem.m_methodDistance}");
									_debugPositions[item.m_position.m_segment].Add(nextItem.m_position.m_segment);
								}
#endif

								this.AddBufferItem(nextItem, item.m_position);
								// NON-STOCK CODE START //
							} else {
#if DEBUGNEWPF
								if (debug)
									logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
										$"-> item seg. {nextSegmentId}, lane {laneIndex} NOT ADDED\n"
										);
#endif
							}
							// NON-STOCK CODE END //
						}
					}
					goto CONTINUE_LANE_LOOP;
				}

				if ((byte)(nextLaneInfo.m_laneType & prevLaneType) != 0 && (nextLaneInfo.m_vehicleType & prevVehicleType) != VehicleInfo.VehicleType.None) {
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
		private void FlushCostLog(List<String> logBuf) {
			if (logBuf == null)
				return;

			foreach (String toLog in logBuf) {
				Log._Debug($"Pathfinder ({this._pathFindIndex}) for unit {Calculating} *COSTS*: " + toLog);
			}
			logBuf.Clear();
		}

		private void FlushMainLog(List<String> logBuf, uint unitId) {
			if (logBuf == null)
				return;

			foreach (String toLog in logBuf) {
				Log._Debug($"Pathfinder ({this._pathFindIndex}) for unit {Calculating} *MAIN*: " + toLog);
			}
			logBuf.Clear();
		}
#endif

		// 4
		private void ProcessItemPedBicycle(bool debug, BufferItem item, ushort nextNodeId, ushort nextSegmentId, ref NetSegment prevSegment, ref NetSegment nextSegment, byte connectOffset, byte laneSwitchOffset, int nextLaneIndex, uint nextLaneId) {
#if DEBUGNEWPF && DEBUG
			List<String> logBuf = null;
			if (debug) {
				logBuf = new List<String>();
				logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}: exploring\n" +
						"\t" + $"nextSegmentId={nextSegmentId}\n" +
						"\t" + $"connectOffset={connectOffset}\n" +
						"\t" + $"laneSwitchOffset={laneSwitchOffset}\n" +
						"\t" + $"nextLaneIndex={nextLaneIndex}\n" +
						"\t" + $"nextLaneId={nextLaneId}\n");
			}
#endif

			if ((nextSegment.m_flags & _disableMask) != NetSegment.Flags.None) {
#if DEBUGNEWPF
				if (debug) {
					logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}: -NOT ADDING- next segment disabled mask is incompatible!\n" +
						"\t" + $"nextSegment.m_flags={nextSegment.m_flags}\n" +
						"\t" + $"_disableMask={_disableMask}\n");
					FlushCostLog(logBuf);
				}
#endif
				return;
			}

			NetManager netManager = Singleton<NetManager>.instance;

			// NON-STOCK CODE START
			bool nextIsRegularNode = nextNodeId == nextSegment.m_startNode || nextNodeId == nextSegment.m_endNode;
#if COUNTSEGMENTSTONEXTJUNCTION
			bool prevIsRealJunction = false;
#endif
			if (nextIsRegularNode) {
				bool nextIsStartNode = nextNodeId == nextSegment.m_startNode;
				ushort prevNodeId = (nextNodeId == prevSegment.m_startNode) ? prevSegment.m_endNode : prevSegment.m_startNode;
#if COUNTSEGMENTSTONEXTJUNCTION
				prevIsRealJunction = (netManager.m_nodes.m_buffer[prevNodeId].m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) != NetNode.Flags.Junction &&
									 (netManager.m_nodes.m_buffer[prevNodeId].m_flags & (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)) != (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut);
#endif

				// check if pedestrians are not allowed to cross here
				if (!junctionManager.IsPedestrianCrossingAllowed(nextSegmentId, nextIsStartNode)) {
#if DEBUGNEWPF
					if (debug) {
						logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}: -NOT ADDING- pedestrian crossing not allowed!\n");
						FlushCostLog(logBuf);
					}
#endif
					return;
				}

				// check if pedestrian light won't change to green
				ICustomSegmentLights lights = customTrafficLightsManager.GetSegmentLights(nextSegmentId, nextIsStartNode, false);
				if (lights != null) {
					if (lights.InvalidPedestrianLight) {
#if DEBUGNEWPF
						if (debug) {
							logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}: -NOT ADDING- invalid pedestrian lights!\n");
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
			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = prevSegment.Info;
			int numLanes = nextSegmentInfo.m_lanes.Length;
			float distance;
			byte offset;
			Vector3 b = netManager.m_lanes.m_buffer[item.m_laneID].CalculatePosition((float)laneSwitchOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
			if (nextSegmentId == item.m_position.m_segment) {
				// next segment is previous segment
				Vector3 a = netManager.m_lanes.m_buffer[nextLaneId].CalculatePosition((float)connectOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
				distance = Vector3.Distance(a, b);
				offset = connectOffset;
			} else {
				// next segment differs from previous segment
				NetInfo.Direction direction = (nextNodeId != nextSegment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
				Vector3 a;
				if ((byte)(direction & NetInfo.Direction.Forward) != 0) {
					a = netManager.m_lanes.m_buffer[nextLaneId].m_bezier.d;
				} else {
					a = netManager.m_lanes.m_buffer[nextLaneId].m_bezier.a;
				}
				distance = Vector3.Distance(a, b);
				offset = (byte)(((direction & NetInfo.Direction.Forward) == 0) ? 0 : 255);
			}
			float prevMaxSpeed = 1f;
			float prevSpeed = 1f;
			NetInfo.LaneType laneType = NetInfo.LaneType.None;
			VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None; // NON-STOCK CODE
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				prevMaxSpeed = GetLaneSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, prevLaneInfo); // SpeedLimitManager.GetLockFreeGameSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, ref lane2); // NON-STOCK CODE
				laneType = prevLaneInfo.m_laneType;
				vehicleType = prevLaneInfo.m_vehicleType; // NON-STOCK CODE
				if ((byte)(laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
					laneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
				prevSpeed = this.CalculateLaneSpeed(prevMaxSpeed, laneSwitchOffset, item.m_position.m_offset, ref prevSegment, prevLaneInfo); // NON-STOCK CODE
			}

			float segLength;
			if (laneType == NetInfo.LaneType.PublicTransport) {
				segLength = netManager.m_lanes.m_buffer[item.m_laneID].m_length;
			} else {
				segLength = Mathf.Max(SEGMENT_MIN_AVERAGE_LENGTH, prevSegment.m_averageLength);
			}
			float offsetLength = (float)Mathf.Abs((int)(laneSwitchOffset - item.m_position.m_offset)) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * segLength;
			float methodDistance = item.m_methodDistance + offsetLength;
			float comparisonValue = item.m_comparisonValue + offsetLength / (prevSpeed * this._maxLength);
			float duration = item.m_duration + offsetLength / prevMaxSpeed;
			if (nextLaneIndex < numLanes) {
				NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];
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
				if ((byte)(nextLaneInfo.m_laneType & laneType) == 0) {
					//comparisonValue += 100f / (0.25f * this._maxLength); // NON-STOCK CODE
					nextItem.m_methodDistance = 0f;
				} else {
					// stock code commented
					if (item.m_methodDistance == 0f) {
						comparisonValue += 100f / (0.25f * this._maxLength);
					}
					nextItem.m_methodDistance = methodDistance + distance;
				}
				float nextMaxSpeed = GetLaneSpeedLimit(nextSegmentId, (uint)nextLaneIndex, nextLaneId, nextLaneInfo); // NON-STOCK CODE
				if (nextLaneInfo.m_laneType != NetInfo.LaneType.Pedestrian || nextItem.m_methodDistance < _conf.PathFinding.MaxWalkingDistance) {
					nextItem.m_comparisonValue = comparisonValue + distance / ((prevMaxSpeed + nextMaxSpeed) * 0.25f * this._maxLength);
					nextItem.m_duration = duration + distance / ((prevMaxSpeed + nextMaxSpeed) * 0.5f);
					if ((nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
						nextItem.m_direction = NetInfo.InvertDirection(nextLaneInfo.m_finalDirection);
					} else {
						nextItem.m_direction = nextLaneInfo.m_finalDirection;
					}
					if (nextLaneId == this._startLaneA) {
						if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetA) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetA)) {
#if DEBUGNEWPF
							if (debug) {
								logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}: -NOT ADDING- start lane A reached in wrong direction!\n");
								FlushCostLog(logBuf);
							}
#endif
							return;
						}
						float nextSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetA, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo); // NON-STOCK CODE
						float nextOffset = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetA)) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;
						nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * this._maxLength);
						nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextSpeed;
					}
					if (nextLaneId == this._startLaneB) {
						if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetB) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetB)) {
#if DEBUGNEWPF
							if (debug) {
								logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}: -NOT ADDING- start lane B reached in wrong direction!\n");
								FlushCostLog(logBuf);
							}
#endif
							return;
						}
						float nextSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo); // NON-STOCK CODE
						float nextOffset = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetB)) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;
						nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * this._maxLength);
						nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextSpeed;
					}
					nextItem.m_laneID = nextLaneId;
					nextItem.m_lanesUsed = (item.m_lanesUsed | nextLaneInfo.m_laneType);
					nextItem.m_vehiclesUsed = (item.m_vehiclesUsed | nextLaneInfo.m_vehicleType);
					nextItem.m_trafficRand = 0;
#if DEBUGNEWPF
					if (debug) {
						logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}: *ADDING*\n" +
							"\t" + $"nextItem.m_laneID={nextItem.m_laneID}\n" +
							"\t" + $"nextItem.m_lanesUsed={nextItem.m_lanesUsed}\n" +
							"\t" + $"nextItem.m_vehiclesUsed={nextItem.m_vehiclesUsed }\n" +
							"\t" + $"nextItem.m_comparisonValue={nextItem.m_comparisonValue}\n" +
							"\t" + $"nextItem.m_methodDistance={nextItem.m_methodDistance}\n"
							);
						FlushCostLog(logBuf);

						_debugPositions[item.m_position.m_segment].Add(nextItem.m_position.m_segment);
					}
#endif
					this.AddBufferItem(nextItem, item.m_position);
				} else {
#if DEBUGNEWPF
					if (debug) {
						logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}: -NOT ADDING- lane incompatible or method distance too large!\n" +
							"\t" + $"nextItem.m_methodDistance={nextItem.m_methodDistance}\n" +
							"\t" + $"nextLaneInfo.m_laneType={nextLaneInfo.m_laneType}\n"
						);
						FlushCostLog(logBuf);
					}
#endif
				}
			} else {
#if DEBUGNEWPF
				if (debug) {
					logBuf.Add($"*PED* item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}: -NOT ADDING- nextLaneIndex >= numLanes ({nextLaneIndex} >= {numLanes})!\n");
					FlushCostLog(logBuf);
				}
#endif
			}
		}

		private float CalculateLaneSpeed(float speedLimit, byte startOffset, byte endOffset, ref NetSegment segment, NetInfo.Lane laneInfo) {
			if ((laneInfo.m_vehicleType & (VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram)) != VehicleInfo.VehicleType.None)
				speedLimit = laneInfo.m_speedLimit;

			NetInfo.Direction direction = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? laneInfo.m_finalDirection : NetInfo.InvertDirection(laneInfo.m_finalDirection);
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
			uint laneLocation = _laneLocation[item.m_laneID];
			uint locPathFindIndex = laneLocation >> 16; // upper 16 bit, expected (?) path find index
			int bufferIndex = (int)(laneLocation & 65535u); // lower 16 bit
			int comparisonBufferPos;
			if (locPathFindIndex == _pathFindIndex) {
				if (item.m_comparisonValue >= _buffer[bufferIndex].m_comparisonValue) {
					return;
				}

				int bufferPosIndex = bufferIndex >> 6; // arithmetic shift (sign stays), upper 10 bit
				int bufferPos = bufferIndex & -64; // upper 10 bit (no shift)
				if (bufferPosIndex < _bufferMinPos || (bufferPosIndex == _bufferMinPos && bufferPos < _bufferMin[bufferPosIndex])) {
					return;
				}

				comparisonBufferPos = Mathf.Max(Mathf.RoundToInt(item.m_comparisonValue * 1024f), _bufferMinPos);
				if (comparisonBufferPos == bufferPosIndex) {
					_buffer[bufferIndex] = item;
					_laneTarget[item.m_laneID] = target;
					return;
				}

				int newBufferIndex = bufferPosIndex << 6 | _bufferMax[bufferPosIndex]--;
				BufferItem bufferItem = _buffer[newBufferIndex];
				_laneLocation[bufferItem.m_laneID] = laneLocation;
				_buffer[bufferIndex] = bufferItem;
			} else {
				comparisonBufferPos = Mathf.Max(Mathf.RoundToInt(item.m_comparisonValue * 1024f), _bufferMinPos);
			}

			if (comparisonBufferPos >= 1024) {
				return;
			}

			if (comparisonBufferPos < 0) {
				return;
			}

			while (_bufferMax[comparisonBufferPos] == 63) {
				++comparisonBufferPos;
				if (comparisonBufferPos == 1024) {
					return;
				}
			}

			if (comparisonBufferPos > _bufferMaxPos) {
				_bufferMaxPos = comparisonBufferPos;
			}

			bufferIndex = (comparisonBufferPos << 6 | ++_bufferMax[comparisonBufferPos]);
			_buffer[bufferIndex] = item;
			_laneLocation[item.m_laneID] = (_pathFindIndex << 16 | (uint)bufferIndex);
			_laneTarget[item.m_laneID] = target;
		}

		private void GetLaneDirection(PathUnit.Position pathPos, out NetInfo.Direction direction, out NetInfo.LaneType laneType, out VehicleInfo.VehicleType vehicleType) {
			NetManager instance = Singleton<NetManager>.instance;
			NetInfo info = instance.m_segments.m_buffer[pathPos.m_segment].Info;
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
				//Log.Message($"Pathfind Thread #{Thread.CurrentThread.ManagedThreadId} iteration!");
				try {
					Monitor.Enter(QueueLock);

					while (QueueFirst == 0u && !Terminated) {
						Monitor.Wait(QueueLock);
					}

					if (Terminated) {
						break;
					}
					Calculating = QueueFirst;
					QueueFirst = CustomPathManager._instance.queueItems[Calculating].nextPathUnitId;
					//QueueFirst = PathUnits.m_buffer[Calculating].m_nextPathUnit;
					if (QueueFirst == 0u) {
						QueueLast = 0u;
						m_queuedPathFindCount = 0;
					} else {
						--m_queuedPathFindCount;
					}

					CustomPathManager._instance.queueItems[Calculating].nextPathUnitId = 0u;
					//PathUnits.m_buffer[Calculating].m_nextPathUnit = 0u;

					// check if path unit is created
					/*if ((PathUnits.m_buffer[Calculating].m_pathFindFlags & PathUnit.FLAG_CREATED) == 0) {
						Log.Warning($"CustomPathFind: Refusing to calculate path unit {Calculating} which is not created!");
						continue;
					}*/

					PathUnits.m_buffer[Calculating].m_pathFindFlags = (byte)((PathUnits.m_buffer[Calculating].m_pathFindFlags & ~PathUnit.FLAG_CREATED) | PathUnit.FLAG_CALCULATING);
					this.queueItem = CustomPathManager._instance.queueItems[Calculating];
				} catch (Exception e) {
					Log.Error($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread Error for unit {Calculating}, flags={PathUnits.m_buffer[Calculating].m_pathFindFlags} (1): {e.ToString()}");
				} finally {
					Monitor.Exit(QueueLock);
				}
				
				// calculate path unit
				try {
					PathFindImplementation(Calculating, ref PathUnits.m_buffer[Calculating]);
				} catch (Exception ex) {
					Log.Error($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread Error for unit {Calculating}, flags={PathUnits.m_buffer[Calculating].m_pathFindFlags} (2): {ex.ToString()}");
					//UIView.ForwardException(ex);

#if DEBUG
					++_failedPathFinds;

#if DEBUGNEWPF
					bool debug = this._debug;
					if (debug)
						Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Could not find path for unit {Calculating} -- exception occurred in PathFindImplementation");
#endif
#endif
					//CustomPathManager._instance.ResetQueueItem(Calculating);

					PathUnits.m_buffer[Calculating].m_pathFindFlags |= PathUnit.FLAG_FAILED;
				} finally {
				}
				//tCurrentState = 10;
#if DEBUGLOCKS
				lockIter = 0;
#endif

				try {
					Monitor.Enter(QueueLock);

					PathUnits.m_buffer[Calculating].m_pathFindFlags = (byte)(PathUnits.m_buffer[Calculating].m_pathFindFlags & ~PathUnit.FLAG_CALCULATING);

					// NON-STOCK CODE START
					try {
						Monitor.Enter(_bufferLock);
						CustomPathManager._instance.queueItems[Calculating].queued = false;
						CustomPathManager._instance.ReleasePath(Calculating);
					} finally {
						Monitor.Exit(this._bufferLock);
					}
					// NON-STOCK CODE END

					Calculating = 0u;
					Monitor.Pulse(QueueLock);
				} catch (Exception e) {
					Log.Error($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread Error for unit {Calculating}, flags={PathUnits.m_buffer[Calculating].m_pathFindFlags} (3): {e.ToString()}");
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
		protected virtual bool CanUseLane(bool debug, ushort segmentId, NetInfo segmentInfo, uint laneIndex, NetInfo.Lane laneInfo) {
			if (!Options.vehicleRestrictionsEnabled)
				return true;

			if (queueItem.vehicleType == ExtVehicleType.None || queueItem.vehicleType == ExtVehicleType.Tram)
				return true;

			/*if (laneInfo == null)
				laneInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex];*/

			if ((laneInfo.m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train)) == VehicleInfo.VehicleType.None)
				return true;

			ExtVehicleType allowedTypes = vehicleRestrictionsManager.GetAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo, VehicleRestrictionsMode.Configured);

			return ((allowedTypes & queueItem.vehicleType) != ExtVehicleType.None);
		}

		/// <summary>
		/// Determines the speed limit for the given lane.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="laneIndex"></param>
		/// <param name="laneId"></param>
		/// <param name="lane"></param>
		/// <returns></returns>
		protected virtual float GetLaneSpeedLimit(ushort segmentId, uint laneIndex, uint laneId, NetInfo.Lane lane) {
			return Options.customSpeedLimitsEnabled ? speedLimitManager.GetLockFreeGameSpeedLimit(segmentId, (uint)laneIndex, laneId, lane) : lane.m_speedLimit;
		}
	}
}
