#define DEBUGPFx
#define DEBUGPF2x
#define DEBUGPF3x
#define DEBUGMERGEx
#define DEBUGLOCKSx
#define MARKCONGESTEDSEGMENTS
#define EXTRAPFx

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
using static TrafficManager.Manager.RoutingManager;
using CSUtil.Commons;
using static TrafficManager.Manager.TrafficMeasurementManager;
using static TrafficManager.Manager.VehicleRestrictionsManager;

namespace TrafficManager.Custom.PathFinding {
	public class CustomPathFind : PathFind {
		private struct BufferItem {
			public PathUnit.Position m_position;
			public float m_comparisonValue;
			public float m_methodDistance;
			public uint m_laneID;
			public NetInfo.Direction m_direction;
			public NetInfo.LaneType m_lanesUsed;
			public VehicleInfo.VehicleType m_vehiclesUsed;
			public float m_speedRand;
			public float m_trafficRand;
		}

		private enum LaneChangingCostCalculationMode {
			None,
			ByLaneDistance,
			ByGivenDistance
		}

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
		private bool _isHeavyVehicle;
		private bool _ignoreBlocked;
		private bool _stablePath;
		private bool _randomParking;
		private bool _transportVehicle;
		private NetSegment.Flags _disableMask;
		private ExtVehicleType? _extVehicleType;
		private ushort? _vehicleId;
		private ExtCitizenInstance.ExtPathType? _extPathType;
		private bool _isRoadVehicle;
		private bool _isLaneArrowObeyingEntity;
		private bool _isLaneConnectionObeyingEntity;
		private bool _leftHandDrive;
		//private float _speedRand;
		//private bool _extPublicTransport;
		private float _vehicleCosts;
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

		private static readonly ushort[] POW2MASKS = new ushort[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768 };

		private static readonly CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
		private static readonly LaneConnectionManager laneConnManager = LaneConnectionManager.Instance;
		private static readonly JunctionRestrictionsManager junctionManager = JunctionRestrictionsManager.Instance;
		private static readonly VehicleRestrictionsManager vehicleRestrictionsManager = VehicleRestrictionsManager.Instance;
		private static readonly SpeedLimitManager speedLimitManager = SpeedLimitManager.Instance;
		private static readonly TrafficMeasurementManager trafficMeasurementManager = TrafficMeasurementManager.Instance;
		private static readonly RoutingManager routingManager = RoutingManager.Instance;

		public bool IsMasterPathFind = false;
#if EXTRAPF
		public bool IsExtraPathFind = false;
#endif

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
			if (Singleton<PathManager>.instance.AddPathReference(unit)) {
				try {
					Monitor.Enter(QueueLock);
#if DEBUGPF3
					uint oldQueueLast = this.QueueLast;
					uint oldQueueFirst = this.QueueFirst;
					uint ppath = 0;
#endif

					if (skipQueue) {
#if DEBUGPF3
						ppath |= 1;
#endif
						if (this.QueueLast == 0u) {
							this.QueueLast = unit;
#if DEBUGPF3
							ppath |= 2;
#endif
						} else {
							try {
								Monitor.Enter(CustomPathManager._instance.QueueItemLock);
								CustomPathManager._instance.queueItems[unit].nextPathUnitId = QueueFirst;
							} finally {
								Monitor.Exit(CustomPathManager._instance.QueueItemLock);
							}
							//this.PathUnits.m_buffer[unit].m_nextPathUnit = this.QueueFirst;
#if DEBUGPF3
							ppath |= 4;
#endif
						}
						this.QueueFirst = unit;
#if DEBUGPF3
						Log._Debug($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.CalculatePath({vehicleType}, {vehicleId}, {unit}, {skipQueue}) skipQueue! ppath={ppath} QueueFirst={QueueFirst} QueueLast={QueueLast} oldQueueFirst={oldQueueFirst} oldQueueLast={oldQueueLast} unit.nextPathUnit={this.PathUnits.m_buffer[unit].m_nextPathUnit}");
#endif
					} else {
#if DEBUGPF3
						ppath |= 8;
#endif
						if (this.QueueLast == 0u) {
							this.QueueFirst = unit;
#if DEBUGPF3
							ppath |= 16;
#endif
						} else {
							try {
								Monitor.Enter(CustomPathManager._instance.QueueItemLock);
								CustomPathManager._instance.queueItems[QueueLast].nextPathUnitId = unit;
							} finally {
								Monitor.Exit(CustomPathManager._instance.QueueItemLock);
							}
							//this.PathUnits.m_buffer[this.QueueLast].m_nextPathUnit = unit;
#if DEBUGPF3
							ppath |= 32;
#endif
						}
						this.QueueLast = unit;
#if DEBUGPF3
						Log._Debug($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.CalculatePath({vehicleType}, {vehicleId}, {unit}, {skipQueue}) NOT skipQueue! ppath={ppath} QueueFirst={QueueFirst} QueueLast={QueueLast} oldQueueFirst={oldQueueFirst} oldQueueLast={oldQueueLast} queueLast.nextPathUnit={this.PathUnits.m_buffer[QueueLast].m_nextPathUnit}");
#endif
					}
					this.PathUnits.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_CREATED;
					++this.m_queuedPathFindCount;
					
#if DEBUGPF3
					Log._Debug($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.CalculatePath({vehicleType}, {vehicleId}, {unit}, {skipQueue}) finished. QueueFirst={QueueFirst} QueueLast={QueueLast} Calculating={Calculating}");
					List<uint> allUnits = new List<uint>();
					uint currentUnit = this.QueueFirst;
					int i = 0;
					while (currentUnit != 0 && currentUnit != QueueLast) {
						allUnits.Add(currentUnit);
						currentUnit = this.PathUnits.m_buffer[currentUnit].m_nextPathUnit;

						++i;
						if (i > 10000) {
							Log.Error($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}): !!! CYCLE ???");
							break;
						}
					}
					allUnits.Add(QueueLast);
					Log._Debug($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.CalculatePath({vehicleType}, {vehicleId}, {unit}, {skipQueue}): allUnits={string.Join(", ", allUnits.Select(x => x.ToString()).ToArray())}");
#endif
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
			this._isHeavyVehicle = ((this.PathUnits.m_buffer[unit].m_simulationFlags & 16) != 0);
			this._ignoreBlocked = ((this.PathUnits.m_buffer[unit].m_simulationFlags & 32) != 0);
			this._stablePath = ((this.PathUnits.m_buffer[unit].m_simulationFlags & 64) != 0);
			this._randomParking = ((this.PathUnits.m_buffer[unit].m_simulationFlags & 128) != 0);
			this._transportVehicle = ((byte)(this._laneTypes & NetInfo.LaneType.TransportVehicle) != 0);
			this._disableMask = (NetSegment.Flags.Collapsed | NetSegment.Flags.PathFailed);
			if ((this.PathUnits.m_buffer[unit].m_simulationFlags & 32) == 0) {
				this._disableMask |= NetSegment.Flags.Flooded;
			}
			//this._speedRand = 0;
			this._extVehicleType = CustomPathManager._instance.pathUnitExtVehicleType[unit];
			this._vehicleId = CustomPathManager._instance.pathUnitVehicleIds[unit];
			this._extPathType = CustomPathManager._instance.pathUnitPathTypes[unit];
			this._leftHandDrive = Constants.ServiceFactory.SimulationService.LeftHandDrive;
			this._isRoadVehicle = _extVehicleType != null && (_extVehicleType & ExtVehicleType.RoadVehicle) != ExtVehicleType.None;
			this._isLaneArrowObeyingEntity = (_vehicleTypes & LaneArrowManager.VEHICLE_TYPES) != VehicleInfo.VehicleType.None &&
										_extVehicleType != null &&
										(_extVehicleType & LaneArrowManager.EXT_VEHICLE_TYPES) != ExtVehicleType.None;
			this._isLaneConnectionObeyingEntity = (_vehicleTypes & LaneConnectionManager.VEHICLE_TYPES) != VehicleInfo.VehicleType.None &&
													_extVehicleType != null && (_extVehicleType & LaneConnectionManager.EXT_VEHICLE_TYPES) != ExtVehicleType.None;
#if DEBUGNEWPF && DEBUG
			bool debug = this._debug = _conf.DebugSwitches[0] &&
				((_conf.PathFindDebugExtVehicleType == ExtVehicleType.None && (_extVehicleType == null || _extVehicleType == ExtVehicleType.None)) || (_extVehicleType != null && (_extVehicleType & _conf.PathFindDebugExtVehicleType) != ExtVehicleType.None)) &&
				(_conf.PathFindDebugStartSegmentId <= 0 || data.m_position00.m_segment == _conf.PathFindDebugStartSegmentId || data.m_position02.m_segment == _conf.PathFindDebugStartSegmentId) &&
				(_conf.PathFindDebugEndSegmentId <= 0 || data.m_position01.m_segment == _conf.PathFindDebugEndSegmentId || data.m_position03.m_segment == _conf.PathFindDebugEndSegmentId) &&
				(_conf.PathFindDebugVehicleId <= 0 || _vehicleId == _conf.PathFindDebugVehicleId)
				;
			if (debug) {
				Log._Debug($"CustomPathFind.PathFindImplementation: START calculating path unit {unit}, type {_extVehicleType}");
				_debugPositions = new Dictionary<ushort, IList<ushort>>();
			}
#endif

			if (! Options.isStockLaneChangerUsed()) {
				_vehicleCosts = _isHeavyVehicle ? _conf.HeavyVehicleLaneChangingCostFactor : 1f;
			} else {
				_vehicleCosts = 1f;
			}

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
				bufferItemEndA.m_speedRand = 0;
				bufferItemEndA.m_trafficRand = 0;
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
				bufferItemEndB.m_speedRand = 0f;
				bufferItemEndB.m_trafficRand = 0;
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
				Log._Debug($"CustomPathFind.PathFindImplementation: Preparing calculating path unit {unit}, type {_extVehicleType}:\n" +
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
#if DEBUGPF
			uint wIter = 0;
#endif
			//Log.Message($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: STARTING MAIN LOOP! bufferMinPos: {this._bufferMinPos}, bufferMaxPos: {this._bufferMaxPos}, startA: {bufferItemStartA.m_position.m_segment}, startB: {bufferItemStartB.m_position.m_segment}, endA: {bufferItemEndA.m_position.m_segment}, endB: {bufferItemEndB.m_position.m_segment}");
			while (this._bufferMinPos <= this._bufferMaxPos) {
				//pfCurrentState = 1;
#if DEBUGPF
				/*if (m_queuedPathFindCount > 100 && _conf.DebugSwitches[0])
					Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: MAIN LOOP RUNNING! bufferMinPos: {this._bufferMinPos}, bufferMaxPos: {this._bufferMaxPos}, startA: {bufferItemStartA.m_position.m_segment}, startB: {bufferItemStartB.m_position.m_segment}, endA: {bufferItemEndA.m_position.m_segment}, endB: {bufferItemEndB.m_position.m_segment}");*/
#endif
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
						this.ProcessItemMain(unit, candidateItem, ref instance.m_segments.m_buffer[candidateItem.m_position.m_segment], ref routingManager.segmentRoutings[candidateItem.m_position.m_segment], ref routingManager.laneEndRoutings[laneRoutingIndex], startNode, true, ref instance.m_nodes.m_buffer[startNode], 0, false);
					}

					if ((byte)(candidateItem.m_direction & NetInfo.Direction.Backward) != 0) {
						ushort endNode = instance.m_segments.m_buffer[candidateItem.m_position.m_segment].m_endNode;
						uint laneRoutingIndex = routingManager.GetLaneEndRoutingIndex(candidateItem.m_laneID, false);
						this.ProcessItemMain(unit, candidateItem, ref instance.m_segments.m_buffer[candidateItem.m_position.m_segment], ref routingManager.segmentRoutings[candidateItem.m_position.m_segment], ref routingManager.laneEndRoutings[laneRoutingIndex], endNode, false, ref instance.m_nodes.m_buffer[endNode], 255, false);
					}

					// handle special nodes (e.g. bus stops)
					int num6 = 0;
					ushort specialNodeId = instance.m_lanes.m_buffer[candidateItem.m_laneID].m_nodes;
					if (specialNodeId != 0) {
						ushort startNode2 = instance.m_segments.m_buffer[candidateItem.m_position.m_segment].m_startNode;
						ushort endNode2 = instance.m_segments.m_buffer[candidateItem.m_position.m_segment].m_endNode;
						bool flag2 = ((instance.m_nodes.m_buffer[startNode2].m_flags | instance.m_nodes.m_buffer[endNode2].m_flags) & NetNode.Flags.Disabled) != NetNode.Flags.None;
						while (specialNodeId != 0) {
							NetInfo.Direction direction = NetInfo.Direction.None;
							byte laneOffset = instance.m_nodes.m_buffer[specialNodeId].m_laneOffset;
							if (laneOffset <= candidateItem.m_position.m_offset) {
								direction |= NetInfo.Direction.Forward;
							}
							if (laneOffset >= candidateItem.m_position.m_offset) {
								direction |= NetInfo.Direction.Backward;
							}
							if ((byte)(candidateItem.m_direction & direction) != 0 && (!flag2 || (instance.m_nodes.m_buffer[specialNodeId].m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None)) {
#if DEBUGNEWPF && DEBUG
								if (debug) {
									Log._Debug($"CustomPathFind.PathFindImplementation: Handling special node for path unit {unit}, type {_extVehicleType}:\n" +
										$"\tcandidateItem.m_position.m_segment={candidateItem.m_position.m_segment}\n" +
										$"\tcandidateItem.m_position.m_lane={candidateItem.m_position.m_lane}\n" +
										$"\tcandidateItem.m_laneID={candidateItem.m_laneID}\n" +
										$"\tspecialNodeId={specialNodeId}\n" +
										$"\tstartNode2={startNode2}\n" +
										$"\tendNode2={endNode2}\n"
										);
								}
#endif
								this.ProcessItemMain(unit, candidateItem, ref instance.m_segments.m_buffer[candidateItem.m_position.m_segment], ref routingManager.segmentRoutings[candidateItem.m_position.m_segment], ref routingManager.laneEndRoutings[0], specialNodeId, false, ref instance.m_nodes.m_buffer[specialNodeId], laneOffset, true);
							}
							specialNodeId = instance.m_nodes.m_buffer[specialNodeId].m_nextLaneNode;
							if (++num6 == 32768) {
								Log.Warning("Special loop: Too many iterations");
								break;
							}
						}
					}
				}
#if DEBUGPF
				++wIter;
				if (wIter > 1000000) {
					Log.Error("Too many iterations in PathFindImpl.");
					break;
				}
#endif
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
				CustomPathManager._instance.ResetPathUnit(unit);

				return;
			}
			// we could calculate a valid path

			float totalPathLength = finalBufferItem.m_comparisonValue * this._maxLength;
			this.PathUnits.m_buffer[unit].m_length = totalPathLength;
			this.PathUnits.m_buffer[unit].m_laneTypes = (byte)finalBufferItem.m_lanesUsed; // NON-STOCK CODE
			this.PathUnits.m_buffer[unit].m_vehicleTypes = (ushort)finalBufferItem.m_vehiclesUsed; // NON-STOCK CODE
#if DEBUG
			/*if (_conf.DebugSwitches[4])
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
							this.PathUnits.m_buffer[currentPathUnitId].m_length = totalPathLength * (float)(sumOfPositionCounts - currentItemPositionCount) / (float)sumOfPositionCounts;
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
					CustomPathManager._instance.ResetPathUnit(unit);

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
							CustomPathManager._instance.ResetPathUnit(unit);
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
			CustomPathManager._instance.ResetPathUnit(unit);
#if DEBUG
			//Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Cannot find path (pfCurrentState={pfCurrentState}) for unit {unit}");
#endif
		}

		// be aware:
		//   (1) path-finding works from target to start. the "next" segment is always the previous and the "previous" segment is always the next segment on the path!
		//   (2) when I use the term "lane index from outer" this means outer right lane for right-hand traffic systems and outer-left lane for left-hand traffic systems.

		// 1
		private void ProcessItemMain(uint unitId, BufferItem item, ref NetSegment prevSegment, ref SegmentRoutingData prevSegmentRouting, ref LaneEndRoutingData prevLaneEndRouting, ushort nextNodeId, bool nextIsStartNode, ref NetNode nextNode, byte connectOffset, bool isMiddle) {
#if DEBUGNEWPF && DEBUG
			bool debug = this._debug && (_conf.PathFindDebugNodeId <= 0 || nextNodeId == _conf.PathFindDebugNodeId);
			if (debug) {
				if (! _debugPositions.ContainsKey(item.m_position.m_segment)) {
					_debugPositions[item.m_position.m_segment] = new List<ushort>();
				}
			}
#else
			bool debug = false;
#endif
			//Log.Message($"THREAD #{Thread.CurrentThread.ManagedThreadId} Path finder: " + this._pathFindIndex + " vehicle types: " + this._vehicleTypes);
#if DEBUGNEWPF && DEBUG
			//bool debug = isTransportVehicle && isMiddle && item.m_position.m_segment == 13550;
			List<String> logBuf = null;
			if (debug)
				logBuf = new List<String>();
#endif

#if DEBUGPF2
			bool debug2 = debug; //_conf.DebugSwitches[0] && _extVehicleType == ExtVehicleType.Bicycle;
			List<String> logBuf2 = null;
			if (debug2)
				logBuf2 = new List<String>();
#endif
			NetManager netManager = Singleton<NetManager>.instance;
			bool prevIsPedestrianLane = false;
			//bool prevIsBusLane = false; // non-stock
			bool prevIsBicycleLane = false;
			bool prevIsCenterPlatform = false;
			bool prevIsElevated = false;
			bool prevIsCarLane = false;
			int prevSimilarLaneIndexFromInner = 0; // similar index, starting with 0 at leftmost lane
			NetInfo prevSegmentInfo = prevSegment.Info;
			byte prevSimilarLaneCount = 0;
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane prevLane = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				prevIsPedestrianLane = (prevLane.m_laneType == NetInfo.LaneType.Pedestrian);
				prevIsBicycleLane = (prevLane.m_laneType == NetInfo.LaneType.Vehicle && (prevLane.m_vehicleType & this._vehicleTypes) == VehicleInfo.VehicleType.Bicycle);
				prevIsCarLane = (prevLane.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None && (prevLane.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None;
				//prevIsBusLane = (prevLane.m_laneType == NetInfo.LaneType.TransportVehicle && (prevLane.m_vehicleType & this._vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None);
				prevIsCenterPlatform = prevLane.m_centerPlatform;
				prevIsElevated = prevLane.m_elevated;
				prevSimilarLaneCount = (byte)prevLane.m_similarLaneCount;
				if ((byte)(prevLane.m_finalDirection & NetInfo.Direction.Forward) != 0) {
					prevSimilarLaneIndexFromInner = prevLane.m_similarLaneIndex;
				} else {
					prevSimilarLaneIndexFromInner = prevLane.m_similarLaneCount - prevLane.m_similarLaneIndex - 1;
				}
			}
			int firstPrevSimilarLaneIndexFromInner = prevSimilarLaneIndexFromInner;
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

					this.ProcessItemCosts(debug, item, nextNodeId, nextSegmentId, ref prevSegment, ref prevSegmentRouting, ref netManager.m_segments.m_buffer[(int)nextSegmentId], ref prevSimilarLaneIndexFromInner, connectOffset, !prevIsPedestrianLane, prevIsPedestrianLane);
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
#if DEBUGPF2
								if (debug2)
									logBuf2.Add($"Exploring path! Segment {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset} @ node {nextNodeId}: going ped1, seg. {nextLeftSegment}, off {connectOffset}, lane idx {leftLaneIndex}, id {leftLaneId}");
#endif
								this.ProcessItemPedBicycle(debug, item, nextNodeId, nextLeftSegment, ref prevSegment, ref netManager.m_segments.m_buffer[(int)nextLeftSegment], connectOffset, connectOffset, leftLaneIndex, leftLaneId); // ped
							}
							if (rightLaneId != 0u && rightLaneId != leftLaneId && (nextRightSegment != prevSegmentId || isEndBendOrJunction || isOnCenterPlatform)) {
#if DEBUGPF2
								if (debug2)
									logBuf2.Add($"Exploring path! Segment {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset} @ node {nextNodeId}: going ped2, seg. {nextRightSegment}, off {connectOffset}, lane idx {rightLaneIndex}, id {rightLaneId}");
#endif
								this.ProcessItemPedBicycle(debug, item, nextNodeId, nextRightSegment, ref prevSegment, ref netManager.m_segments.m_buffer[(int)nextRightSegment], connectOffset, connectOffset, rightLaneIndex, rightLaneId); // ped
							}
						}

						// switch from bicycle lane to pedestrian lane
						int nextLaneIndex;
						uint nextLaneId;
						if ((this._vehicleTypes & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None &&
							prevSegment.GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Bicycle, out nextLaneIndex, out nextLaneId)) {
#if DEBUGPF2
							if (debug2)
								logBuf2.Add($"Exploring path! Segment {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset} @ node {nextNodeId}: going bike, seg. {prevSegmentId}, off {connectOffset}, lane idx {nextLaneIndex}, id {nextLaneId}");
#endif
							this.ProcessItemPedBicycle(debug, item, nextNodeId, prevSegmentId, ref prevSegment, ref prevSegment, connectOffset, connectOffset, nextLaneIndex, nextLaneId); // bicycle
						}
					} else {
						// we are going from pedestrian lane to a beautification node

						for (int j = 0; j < 8; ++j) {
							ushort nextSegmentId = nextNode.GetSegment(j);
							if (nextSegmentId != 0 && nextSegmentId != prevSegmentId) {
#if DEBUGPF2
								if (debug2)
									logBuf2.Add($"Exploring path! Segment {item.m_position.m_segment} @ node {nextNodeId}: going beauty1, seg. {nextSegmentId}, off {connectOffset}");
#endif
#if DEBUGNEWPF
								if (debug) {
									FlushMainLog(logBuf, unitId);
								}
#endif

								this.ProcessItemCosts(debug, item, nextNodeId, nextSegmentId, ref prevSegment, ref prevSegmentRouting, ref netManager.m_segments.m_buffer[(int)nextSegmentId], ref prevSimilarLaneIndexFromInner, connectOffset, false, true);
							}
						}
					}

					// NON-STOCK CODE START
					// switch from vehicle to pedestrian lane (parking)
					bool parkingAllowed = true;
					if (Options.prohibitPocketCars) {
						if (_extVehicleType == ExtVehicleType.PassengerCar) {
							if ((byte)(item.m_lanesUsed & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
								// if pocket cars are prohibited, a citizen may only park their car once per path
								parkingAllowed = false;
							} else if ((byte)(item.m_lanesUsed & NetInfo.LaneType.PublicTransport) == 0) {
								// if the citizen is walking to their target (= no public transport used), the passenger car must be parked in the very last moment
								parkingAllowed = item.m_laneID == _endLaneA || item.m_laneID == _endLaneB;
								/*if (_conf.DebugSwitches[4]) {
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
#if DEBUGPF2
							if (debug2)
								logBuf2.Add($"Exploring path! Segment {item.m_position.m_segment} @ node {nextNodeId}: going parking, seg. {prevSegmentId}, off {connectOffset}, lane idx {nextLaneIndex2}, id {nextlaneId2}");
#endif
							CustomPathFind.BufferItem item2 = item;
							if (this._randomParking) {
								item2.m_comparisonValue += (float)this._pathRandomizer.Int32(300u) / this._maxLength;
							}
							this.ProcessItemPedBicycle(debug, item2, nextNodeId, prevSegmentId, ref prevSegment, ref prevSegment, connectOffset2, 128, nextLaneIndex2, nextlaneId2); // ped
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
								_extVehicleType == ExtVehicleType.PassengerCar &&
								(_extPathType == ExtCitizenInstance.ExtPathType.WalkingOnly || (_extPathType == ExtCitizenInstance.ExtPathType.DrivingOnly && item.m_position.m_segment != _startSegmentA && item.m_position.m_segment != _startSegmentB))) {
							allowPedestrian = false;
						} else {
							parkingConnectOffset = (byte)this._pathRandomizer.UInt32(1u, 254u);
						}
					}
				}

				if ((this._vehicleTypes & (VehicleInfo.VehicleType.Ferry | VehicleInfo.VehicleType.Monorail)) != VehicleInfo.VehicleType.None) {
					// monorail / ferry

					for (int k = 0; k < 8; k++) {
						ushort nextSegmentId = nextNode.GetSegment(k);
						if (nextSegmentId == 0 || nextSegmentId == prevSegmentId) {
							continue;
						}

						this.ProcessItemCosts(debug, item, nextNodeId, nextSegmentId, ref prevSegment, ref prevSegmentRouting, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevSimilarLaneIndexFromInner, connectOffset, true, allowPedestrians);
					}

					if ((nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None &&
						(this._vehicleTypes & VehicleInfo.VehicleType.Monorail) == VehicleInfo.VehicleType.None) {
						this.ProcessItemCosts(debug, item, nextNodeId, prevSegmentId, ref prevSegment, ref prevSegmentRouting, ref prevSegment, ref prevSimilarLaneIndexFromInner, connectOffset, true, false);
					}
				} else {
					// road vehicles, trams, trains, metros, etc.


					bool explorePrevSegment = false;
					bool isStrictLaneArrowPolicyEnabled = false;
					bool handleStockUturn = (this._vehicleTypes & VehicleInfo.VehicleType.Tram) == VehicleInfo.VehicleType.None;
					bool stockUturn = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;

					if (prevLaneEndRouting.routed) {
						bool prevIsOutgoingOneWay = nextIsStartNode ? prevSegmentRouting.startNodeOutgoingOneWay : prevSegmentRouting.endNodeOutgoingOneWay;
						bool nextIsUntouchable = (nextNode.m_flags & (NetNode.Flags.Untouchable)) != NetNode.Flags.None;
						bool nextIsTransitionOrJunction = (nextNode.m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) != NetNode.Flags.None;
						bool nextIsBend = (nextNode.m_flags & (NetNode.Flags.Bend)) != NetNode.Flags.None;
						bool nextIsEndOrOneWayOut = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
						bool isCustomUturnAllowed = Flags.getUTurnAllowed(prevSegmentId, nextIsStartNode);

						// determine if the vehicle may u-turn at the target node
						explorePrevSegment =
							(this._vehicleTypes & VehicleInfo.VehicleType.Tram) == VehicleInfo.VehicleType.None &&
							(nextIsEndOrOneWayOut || // stock u-turn points
							(Options.junctionRestrictionsEnabled &&
							_isRoadVehicle && // only road vehicles may perform u-turns
							isCustomUturnAllowed && // only do u-turns if allowed
							!nextIsBeautificationNode && // no u-turns at beautification nodes
							prevIsCarLane && // u-turns for road vehicles only
							!_isHeavyVehicle && // only small vehicles may perform u-turns
							(nextIsTransitionOrJunction || nextIsBend) && // perform u-turns at transitions, junctions and bend nodes
							!prevIsOutgoingOneWay)); // do not u-turn on one-ways

						isStrictLaneArrowPolicyEnabled =
							!nextIsBeautificationNode && // do not obey lane arrows at beautification nodes
							!nextIsUntouchable &&
							_isLaneArrowObeyingEntity &&
							nextIsTransitionOrJunction && // follow lane arrows only at transitions and junctions
							!(
#if DEBUG
							Options.allRelaxed || // debug option: all vehicle may ignore lane arrows
#endif
							(Options.relaxedBusses && _extVehicleType == ExtVehicleType.Bus)); // option: busses may ignore lane arrows

						handleStockUturn = !explorePrevSegment;

#if DEBUGNEWPF
						if (debug)
							logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId} ({nextIsStartNode}):\n" +
								"\t" + $"_extPathType={_extPathType}\n" +
								"\t" + $"_vehicleTypes={_vehicleTypes}, _laneTypes={_laneTypes}\n" +
								"\t" + $"_extVehicleType={_extVehicleType}\n" +
								"\t" + $"_isRoadVehicle={_isRoadVehicle}\n" +
								"\t" + $"_isHeavyVehicle={_isHeavyVehicle}\n" +
								"\t" + $"_stablePath={_stablePath}\n" +
								"\t" + $"_isLaneConnectionObeyingEntity={_isLaneConnectionObeyingEntity}\n" +
								"\t" + $"_isLaneArrowObeyingEntity={_isLaneArrowObeyingEntity}\n\n" +
								"\t" + $"prevIsOutgoingOneWay={prevIsOutgoingOneWay}\n" +
								"\t" + $"prevLaneHasRouting={prevLaneEndRouting.routed}\n\n" +
								"\t" + $"nextIsStartNode={nextIsStartNode}\n" +
								"\t" + $"isNextBeautificationNode={nextIsBeautificationNode}\n" +
								//"\t" + $"nextIsRealJunction={nextIsRealJunction}\n" +
								"\t" + $"nextIsBend={nextIsBend}\n" +
								"\t" + $"nextIsUntouchable={nextIsUntouchable}\n" +
								"\t" + $"nextIsEndOrOneWayOut={nextIsEndOrOneWayOut}\n\n" +
								"\t" + $"allowPedestrians={allowPedestrians}\n" +
								"\t" + $"isCustomUturnAllowed={isCustomUturnAllowed}\n" +
								"\t" + $"explorePrevSegment={explorePrevSegment}\n" +
								"\t" + $"isStrictLaneArrowPolicyEnabled={isStrictLaneArrowPolicyEnabled}\n" +
								"\t" + $"handleStockUturn={handleStockUturn}\n"
								);
#endif
					} else {
#if DEBUGNEWPF
						if (debug)
							logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId} ({nextIsStartNode}):\n" +
								"\t" + $"_extPathType={_extPathType}\n" +
								"\t" + $"_vehicleTypes={_vehicleTypes}, _laneTypes={_laneTypes}\n" +
								"\t" + $"_extVehicleType={_extVehicleType}\n" +
								"\t" + $"_isRoadVehicle={_isRoadVehicle}\n" +
								"\t" + $"_isHeavyVehicle={_isHeavyVehicle}\n" +
								"\t" + $"_stablePath={_stablePath}\n" +
								"\t" + $"_isLaneConnectionObeyingEntity={_isLaneConnectionObeyingEntity}\n" +
								"\t" + $"_isLaneArrowObeyingEntity={_isLaneArrowObeyingEntity}\n\n" +
								"\t" + $"prevLaneHasRouting={prevLaneEndRouting.routed}\n\n"
							);
#endif
					}

					if (allowPedestrians || !prevLaneEndRouting.routed) {
						/* pedestrian to bicycle switch or no routing information available */

#if DEBUGNEWPF
						if (debug) {
							logBuf.Add(
								$"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
								"\t" + $"-> using DEFAULT exploration mode\n"
								);
							FlushMainLog(logBuf, unitId);
						}
#endif

						if (explorePrevSegment) {
							stockUturn = true;
							handleStockUturn = true;
						}

						ushort nextSegmentId = prevSegment.GetRightSegment(nextNodeId);
						for (int k = 0; k < 8; ++k) {
							if (nextSegmentId == 0 || nextSegmentId == prevSegmentId) {
								break;
							}

							if (ProcessItemCosts(debug, item, nextNodeId, nextSegmentId, ref prevSegment, ref prevSegmentRouting, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevSimilarLaneIndexFromInner, connectOffset, !prevLaneEndRouting.routed, allowPedestrians)) {
								stockUturn = true;
							}

							nextSegmentId = netManager.m_segments.m_buffer[nextSegmentId].GetRightSegment(nextNodeId);
						}
					}

					if (prevLaneEndRouting.routed) {
						/* routed vehicle paths */

#if DEBUGNEWPF
						if (debug)
							logBuf.Add(
								$"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
								"\t" + $"-> using CUSTOM exploration mode\n"
								);
#endif

						for (int i = 0; i < 8; ++i) {
							LaneTransitionData[] laneTransitions = prevLaneEndRouting.GetTransitions(i);

							if (laneTransitions == null) {
								continue;
							}

#if DEBUGNEWPF
							if (debug)
								logBuf.Add(
									$"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
									"\t" + $"CUSTOM exploration iteration {i}\n"
									);
#endif


							for (int k = 0; k < laneTransitions.Length; ++k) {
								ushort nextSegmentId = laneTransitions[k].segmentId;

								if (nextSegmentId == 0) {
									continue;
								}

								if (nextSegmentId == prevSegmentId) {
									if (explorePrevSegment) {
										// prevent double exploration of previous segment during this method execution
										handleStockUturn = false;
									} else {
										continue;
									}
								}

								if (laneTransitions[k].type == LaneEndTransitionType.Invalid ||
									(isStrictLaneArrowPolicyEnabled && laneTransitions[k].type == LaneEndTransitionType.Relaxed)) {
									continue;
								}

#if DEBUGNEWPF
								if (debug) {
									logBuf.Add(
										$"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
										"\t" + $"CUSTOM exploration iteration {i}:\n" +
										"\t" + $"transition iteration {k}:\n" +
										"\t" + $"{laneTransitions[k].ToString()}\n"
										);
									FlushMainLog(logBuf, unitId);
								}
#endif

								bool foundForced = false;
								int prevLaneIndexFromInner = prevSimilarLaneIndexFromInner;
								if (ProcessItemCosts(debug, Options.advancedAI, false, LaneChangingCostCalculationMode.ByGivenDistance, item, nextNodeId, nextSegmentId, ref prevSegment, ref prevSegmentRouting, ref netManager.m_segments.m_buffer[nextSegmentId], ref routingManager.segmentRoutings[nextSegmentId], ref prevLaneIndexFromInner, connectOffset, true, false, laneTransitions[k].laneIndex, laneTransitions[k].laneId, laneTransitions[k].distance, out foundForced)) {
									stockUturn = true;
								}
							}
						}
					}

					if (handleStockUturn && stockUturn) {
#if DEBUGNEWPF
						if (debug) {
							logBuf.Add($"path unit {unitId}\n" +
								$"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
								"\t" + $"-> exploring DEFAULT u-turn\n"
							);
							FlushMainLog(logBuf, unitId);
						}
#endif

						this.ProcessItemCosts(debug, item, nextNodeId, prevSegmentId, ref prevSegment, ref prevSegmentRouting, ref prevSegment, ref prevSimilarLaneIndexFromInner, connectOffset, true, false);
					}
				}

				if (allowPedestrian) {
					// switch from walking to driving a car, bus, etc.
					int nextLaneIndex;
					uint nextLaneId;
					if (prevSegment.GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Pedestrian, this._vehicleTypes, out nextLaneIndex, out nextLaneId)) {
#if DEBUGPF
						/*if (debug)
							logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer: Ped allowed u-turn");*/
#endif
#if DEBUGPF2
						if (debug2)
							logBuf2.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}: Ped allowed u-turn. nextConnectOffset={nextConnectOffset} nextLaneIndex={nextLaneIndex} nextLaneId={nextLaneId}");
#endif
						this.ProcessItemPedBicycle(debug, item, nextNodeId, prevSegmentId, ref prevSegment, ref prevSegment, parkingConnectOffset, parkingConnectOffset, nextLaneIndex, nextLaneId); // ped
					}
				} // allowPedSwitch
			} // !prevIsPedestrianLane

			if (nextNode.m_lane != 0u &&
				(!Options.prohibitPocketCars || _extVehicleType != ExtVehicleType.PassengerCar || (item.m_lanesUsed & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == NetInfo.LaneType.None)) {
				// transport lines, cargo lines, etc.

				bool targetDisabled = (nextNode.m_flags & (NetNode.Flags.Disabled | NetNode.Flags.DisableOnlyMiddle)) == NetNode.Flags.Disabled;
				ushort nextSegmentId = netManager.m_lanes.m_buffer[nextNode.m_lane].m_segment;
				if (nextSegmentId != 0 && nextSegmentId != item.m_position.m_segment) {
#if DEBUGPF
					/*if (debug)
						logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane idx {item.m_position.m_lane}: handling special lanes");*/
#endif
#if DEBUGPF2
					if (debug2)
						logBuf2.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}: handling special lanes");
#endif
					this.ProcessItemPublicTransport(debug, item, nextNodeId, targetDisabled, nextSegmentId, ref prevSegment, ref netManager.m_segments.m_buffer[nextSegmentId], nextNode.m_lane, nextNode.m_laneOffset, connectOffset);
				}
			}

#if DEBUGNEWPF
			if (debug) {
				FlushMainLog(logBuf, unitId);
			}
#endif

#if DEBUGPF2
			if (debug2) {
				foreach (String toLog in logBuf2) {
					Log._Debug($"Pathfinder ({this._pathFindIndex}) for unit {unitId}: " + toLog);
				}
			}
#endif
		}

		// 2
		private void ProcessItemPublicTransport(bool debug, BufferItem item, ushort targetNodeId, bool targetDisabled, ushort nextSegmentId, ref NetSegment prevSegment, ref NetSegment nextSegment, uint nextLane, byte offset, byte connectOffset) {
			if ((nextSegment.m_flags & _disableMask) != NetSegment.Flags.None) {
				return;
			}
			NetManager netManager = Singleton<NetManager>.instance;
			if (targetDisabled && ((netManager.m_nodes.m_buffer[(int)nextSegment.m_startNode].m_flags | netManager.m_nodes.m_buffer[(int)nextSegment.m_endNode].m_flags) & NetNode.Flags.Disabled) == NetNode.Flags.None) {
				return;
			}

			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = prevSegment.Info;
			int nextNumLanes = nextSegmentInfo.m_lanes.Length;
			uint curLaneId = nextSegment.m_lanes;
			float prevMaxSpeed = 1f;
			float prevSpeed = 1f;
			NetInfo.LaneType laneType = NetInfo.LaneType.None;
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane lane2 = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				prevMaxSpeed = GetLaneSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, lane2); // SpeedLimitManager.GetLockFreeGameSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, ref lane2); // NON-STOCK CODE
				laneType = lane2.m_laneType;
				if ((byte)(laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
					laneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
				prevSpeed = this.CalculateLaneSpeed(prevMaxSpeed, connectOffset, item.m_position.m_offset, ref prevSegment, lane2); // NON-STOCK CODE
			}
			float averageLength = prevSegment.m_averageLength;
			float offsetLength = (float)Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * averageLength;
			float methodDistance = item.m_methodDistance + offsetLength;
			float comparisonValue = item.m_comparisonValue + offsetLength / (prevSpeed * this._maxLength);
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
						nextItem.m_position.m_segment = nextSegmentId;
						nextItem.m_position.m_lane = (byte)laneIndex;
						nextItem.m_position.m_offset = offset;
						if ((byte)(nextLaneInfo.m_laneType & laneType) == 0) {
							nextItem.m_methodDistance = 0f;
						} else {
							nextItem.m_methodDistance = methodDistance + distance;
						}
						float nextMaxSpeed = GetLaneSpeedLimit(nextSegmentId, laneIndex, curLaneId, nextLaneInfo); // SpeedLimitManager.GetLockFreeGameSpeedLimit(nextSegmentId, laneIndex, curLaneId, ref lane3); // NON-STOCK CODE
						if (nextLaneInfo.m_laneType != NetInfo.LaneType.Pedestrian || nextItem.m_methodDistance < 1000f) {
							nextItem.m_comparisonValue = comparisonValue + distance / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength); // NON-STOCK CODE
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
							}
							if (nextLane == this._startLaneB) {
								if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetB) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetB)) {
									return;
								}
								float nextSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo); // NON-STOCK CODE
								float nextOffsetDistance = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetB)) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;
								nextItem.m_comparisonValue += nextOffsetDistance * nextSegment.m_averageLength / (nextSpeed * this._maxLength);
							}
							nextItem.m_laneID = nextLane;
							nextItem.m_lanesUsed = (item.m_lanesUsed | nextLaneInfo.m_laneType);
							nextItem.m_vehiclesUsed = (item.m_vehiclesUsed | nextLaneInfo.m_vehicleType);
							nextItem.m_speedRand = 0;
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

		private bool ProcessItemCosts(bool debug, BufferItem item, ushort nextNodeId, ushort nextSegmentId, ref NetSegment prevSegment, ref SegmentRoutingData prevSegmentRouting, ref NetSegment nextSegment, ref int laneIndexFromInner, byte connectOffset, bool enableVehicle, bool enablePedestrian) {
			bool foundForced = false;
			return ProcessItemCosts(debug, false, true, LaneChangingCostCalculationMode.None, item, nextNodeId, nextSegmentId, ref prevSegment, ref prevSegmentRouting, ref nextSegment, ref routingManager.segmentRoutings[nextSegmentId], ref laneIndexFromInner, connectOffset, enableVehicle, enablePedestrian, null, null, null, out foundForced);
		}

		// 3
		private bool ProcessItemCosts(bool debug, bool allowAdvancedAI, bool obeyStockLaneArrows, LaneChangingCostCalculationMode laneChangingCostCalculationMode, BufferItem item, ushort nextNodeId, ushort nextSegmentId, ref NetSegment prevSegment, ref SegmentRoutingData prevSegmentRouting, ref NetSegment nextSegment, ref SegmentRoutingData nextSegmentRouting, ref int laneIndexFromInner, byte connectOffset, bool enableVehicle, bool enablePedestrian, int? forcedLaneIndex, uint? forcedLaneId, byte? forcedLaneDist, out bool foundForced) {

#if DEBUGPF
			/*if (_conf.DebugSwitches[0])
				Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: ProcessItemSub item {item.m_position.m_segment} {item.m_position.m_lane}, targetNodeId {targetNodeId}");*/
#endif

#if DEBUGNEWPF && DEBUG
			debug = debug && _conf.DebugSwitches[1];
#else
			debug = false;
#endif
			//Log.Message($"THREAD #{Thread.CurrentThread.ManagedThreadId} Path finder: " + this._pathFindIndex + " vehicle types: " + this._vehicleTypes);
#if DEBUGNEWPF && DEBUG
			//bool debug = isTransportVehicle && isMiddle && item.m_position.m_segment == 13550;
			List<String> logBuf = null;
			if (debug)
				logBuf = new List<String>();
#endif

			//bool emergencyLaneSelection = (_conf.DebugSwitches[2] && _extVehicleType == ExtVehicleType.Emergency);

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

			// determine if Advanced AI should be used here
			bool useAdvancedAI =
				allowAdvancedAI && // traffic measurements may happen
				//allowLaneChangingCosts &&
				_extVehicleType != null && // we got a valid extended vehicle type
				((ExtVehicleType)_extVehicleType & (ExtVehicleType.RoadVehicle & ~ExtVehicleType.Bus)) != ExtVehicleType.None && // we are not a bus
				!_stablePath && // we do not need a stable path
				enableVehicle; // we may choose vehicle lanes
			bool calculateTrafficStats = useAdvancedAI;

#if DEBUGNEWPF
			if (debug)
				logBuf.Add($"useAdvancedAI={useAdvancedAI}, isStockLaneChangerUsed={Options.isStockLaneChangerUsed()}, _extVehicleType={_extVehicleType}, allowAdvancedAI={allowAdvancedAI}, nonBus={((ExtVehicleType)_extVehicleType & (ExtVehicleType.RoadVehicle & ~ExtVehicleType.Bus)) != ExtVehicleType.None}, _stablePath={_stablePath}, enableVehicle={enableVehicle}");
#endif

			float prevMaxSpeed = 1f;
			float prevLaneSpeed = 1f;
			NetInfo.LaneType prevLaneType = NetInfo.LaneType.None;
			VehicleInfo.VehicleType prevVehicleType = VehicleInfo.VehicleType.None;
			// NON-STOCK CODE START //
			/*bool prevIsHighway = false;
			if (prevSegmentInfo.m_netAI is RoadBaseAI)
				prevIsHighway = ((RoadBaseAI)prevSegmentInfo.m_netAI).m_highwayRules; */
			bool nextIsStartNodeOfPrevSegment = prevSegment.m_startNode == nextNodeId;
			ushort sourceNodeId = nextIsStartNodeOfPrevSegment ? prevSegment.m_endNode : prevSegment.m_startNode;
			bool prevIsJunction = (netManager.m_nodes.m_buffer[sourceNodeId].m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
			int prevOuterSimilarLaneIndex = -1;
			float prevUsage = 0f;
			float prevTraffic = 0f;
			//float prevDensity = 0.25f;
			bool isMiddle = connectOffset != 0 && connectOffset != 255;
			NetInfo.Lane prevLaneInfo = null;
			float usageRand = item.m_speedRand;
			float trafficRand = item.m_trafficRand;
#if MARKCONGESTEDSEGMENTS
			float congestionLaneChangingCosts = 1f;
			bool isCongested = false;
#endif
			// determines if a vehicles wants to change lanes here (pseudo-randomized). If true, costs for changing to an adjacent lane are not being calculated
			bool wantToChangeLane = false;
			bool avoidLane = false; // if true, avoidance policies are in effect (e.g. "Old Town" policy of "Ban Heavy Vehicles" policy)
			bool strictlyAvoidLane = false; // if true, the player has setup a vehicle restriction

			// NON-STOCK CODE END //
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				prevLaneInfo = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				prevLaneType = prevLaneInfo.m_laneType;
				prevVehicleType = prevLaneInfo.m_vehicleType;
				prevMaxSpeed = /*emergencyLaneSelection ? 5f :*/ GetLaneSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, prevLaneInfo); // SpeedLimitManager.GetLockFreeGameSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, ref lane); // NON-STOCK CODE
				prevLaneSpeed = this.CalculateLaneSpeed(prevMaxSpeed, connectOffset, item.m_position.m_offset, ref prevSegment, prevLaneInfo); // NON-STOCK CODE
				
				// NON-STOCK CODE START //
				int prevNumLanes = prevSegmentInfo.m_lanes.Length;
				if ((byte)(prevLaneInfo.m_finalDirection & NetInfo.Direction.Forward) != 0) {
					prevOuterSimilarLaneIndex = prevLaneInfo.m_similarLaneCount - prevLaneInfo.m_similarLaneIndex - 1;
				} else {
					prevOuterSimilarLaneIndex = prevLaneInfo.m_similarLaneIndex;
				}

				// check for vehicle restrictions
				if (!CanUseLane(debug, item.m_position.m_segment, prevSegmentInfo, item.m_position.m_lane, prevLaneInfo)) {
#if DEBUGNEWPF
					if (debug) {
						logBuf.Add($"Vehicle {_extVehicleType} must not use lane {item.m_position.m_lane} @ seg. {item.m_position.m_segment}, null? {prevLaneInfo == null}");
					}
#endif
					strictlyAvoidLane = true;
					calculateTrafficStats = false;
				} else if (calculateTrafficStats) {
					NetInfo.Direction prevFinalDir = nextIsStartNodeOfPrevSegment ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
					prevFinalDir = ((prevSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? prevFinalDir : NetInfo.InvertDirection(prevFinalDir);
					int prevDirIndex = trafficMeasurementManager.GetDirIndex(item.m_position.m_segment, prevFinalDir);
					int nextDirIndex = trafficMeasurementManager.GetDirIndex(nextSegmentId, nextFinalDir);

					bool fetchedPrevLaneTrafficData = false;
					if (prevLaneInfo.m_similarLaneCount > 1) {
						// determine path-finding lane utilization of the previous lane

						TrafficMeasurementManager.LaneTrafficData[] prevLanesTrafficData = null;
						if (trafficMeasurementManager.GetLaneTrafficData(item.m_position.m_segment, prevSegmentInfo, out prevLanesTrafficData)) {
							if (trafficMeasurementManager.segmentDirTrafficData[prevDirIndex].totalPathFindTrafficBuffer > 0) {
								prevUsage = Mathf.Clamp(((prevLanesTrafficData[item.m_position.m_lane].lastPathFindTrafficBuffer * 100u) / trafficMeasurementManager.segmentDirTrafficData[prevDirIndex].totalPathFindTrafficBuffer), 0, 100);
							}
						}
					}

					float prevCongestionRatio = trafficMeasurementManager.segmentDirTrafficData[prevDirIndex].numCongestionMeasurements > 0 ? ((uint)trafficMeasurementManager.segmentDirTrafficData[prevDirIndex].numCongested * 100u) / (uint)trafficMeasurementManager.segmentDirTrafficData[prevDirIndex].numCongestionMeasurements : 0; // now in %
					float nextCongestionRatio = trafficMeasurementManager.segmentDirTrafficData[nextDirIndex].numCongestionMeasurements > 0 ? ((uint)trafficMeasurementManager.segmentDirTrafficData[nextDirIndex].numCongested * 100u) / (uint)trafficMeasurementManager.segmentDirTrafficData[nextDirIndex].numCongestionMeasurements : 0; // now in %

					// get the min. speed for the previous segment
					prevTraffic = Mathf.Clamp(100 - (int)trafficMeasurementManager.segmentDirTrafficData[prevDirIndex].meanSpeed / 100, 0, 100);
						
					// get the direction-average minimum speed for the previous segment
#if MARKCONGESTEDSEGMENTS
					//int dirIndex = prevLaneInfo.m_finalDirection == NetInfo.Direction.Backward ? 1 : 0;

					isCongested = prevCongestionRatio >= _conf.CongestionFrequencyThreshold || nextCongestionRatio >= _conf.CongestionFrequencyThreshold;
					float maxCongestionRatio = Math.Max(prevCongestionRatio, nextCongestionRatio);
					congestionLaneChangingCosts = 1f + maxCongestionRatio * 0.01f * _conf.CongestionLaneChangingBaseCost;
					wantToChangeLane = _pathRandomizer.Int32((uint)maxCongestionRatio + _conf.RandomizedLaneChangingModulo) == 0;
#endif

					// prevUsage, prevTraffic is now in %
#if DEBUGNEWPF
					float oldUsageRand = usageRand;
					float oldTrafficRand = trafficRand;
#endif

					prevTraffic += trafficRand;
					if (!_isHeavyVehicle && !isCongested) {
						prevUsage += usageRand;
					}

					if ((prevNumLanes != nextNumLanes || (netManager.m_nodes.m_buffer[nextNodeId].m_flags & NetNode.Flags.Junction) != NetNode.Flags.None) && _conf.LaneSpeedRandInterval > 0) {
						usageRand = (float)_pathRandomizer.Int32((uint)_conf.LaneSpeedRandInterval) - (float)_conf.LaneSpeedRandInterval / 2f;
						trafficRand = (float)_pathRandomizer.Int32((uint)_conf.LaneDensityRandInterval) - (float)_conf.LaneDensityRandInterval / 2f;
					}

					prevUsage = (float)Mathf.Clamp01((float)Math.Round(prevUsage / 10f) / 10f); // 0, 0.1, 0.2, ..., 1
					prevTraffic = (float)Mathf.Clamp01((float)Math.Round(prevTraffic / 10f) / 10f); // 0, 0.1, 0.2, ..., 1

#if DEBUGNEWPF
					if (debug)
						logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
							"\t" + $"calculated traffic stats:\n" +
							"\t" + $"_vehicleTypes={_vehicleTypes}, _laneTypes={_laneTypes}\n" +
							"\t" + $"_extVehicleType={_extVehicleType}\n" +
							"\t" + $"_isRoadVehicle={_isRoadVehicle}\n" +
							"\t" + $"_isHeavyVehicle={_isHeavyVehicle}\n" +
							"\t" + $"_isLaneConnectionObeyingEntity={_isLaneConnectionObeyingEntity}\n" +
							"\t" + $"_isLaneArrowObeyingEntity={_isLaneArrowObeyingEntity}\n\n" +
							"\t" + $"prevCongestionRatio={prevCongestionRatio}\n" +
							"\t" + $"nextCongestionRatio={nextCongestionRatio}\n" +
							"\t" + $"isCongested={isCongested}\n" +
							"\t" + $"congestionLaneChangingCosts={congestionLaneChangingCosts}\n\n" +
							"\t" + $"prevUsage={prevUsage}\n\n" +
							"\t" + $"prevTraffic={prevTraffic}\n\n" +
							"\t" + $"oldUsageRand={oldUsageRand}\n" +
							"\t" + $"trafficRand={trafficRand}\n" +
							"\t" + $"oldTrafficRand={oldTrafficRand}\n" +
							"\t" + $"usageRand={usageRand}\n\n" +
							"\t" + $"prevNumLanes={prevNumLanes}\n" +
							"\t" + $"nextNumLanes={nextNumLanes}\n" +
							"\t" + $"nextSegmentId={nextSegmentId}\n\n" +
							"\t" + $"forcedLaneId={forcedLaneId}\n" +
							"\t" + $"forcedLaneIndex={forcedLaneIndex}\n" +
							"\t" + $"forcedLaneDist={forcedLaneDist}\n" +
							"\t" + $"laneChangingCostCalculationMode={laneChangingCostCalculationMode}\n"
							);
#endif
				}
				// NON-STOCK CODE END //
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

			// determine if Advanced AI should be used here
			if (useAdvancedAI) {
				useAdvancedAI = ((prevVehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None/* || (prevVehicleType == VehicleInfo.VehicleType.None && emergencyLaneSelection)*/) &&
					(prevLaneType & (NetInfo.LaneType.Pedestrian | NetInfo.LaneType.Parking)) == NetInfo.LaneType.None; // NON-STOCK CODE
																														// advanced AI may only be active if the previous lane was a lane for driving vehicles
			} else {
				laneChangingCostCalculationMode = LaneChangingCostCalculationMode.None;
			}

			float prevCost = Mathf.Max(_conf.SegmentMinAverageLength, prevSegment.m_averageLength);
			if (!this._stablePath) {
				// CO randomization. Only randomizes over segments, not over lanes.
				if (!useAdvancedAI) { // NON-STOCK CODE
					Randomizer randomizer = new Randomizer(this._pathFindIndex << 16 | (uint)item.m_position.m_segment);
					prevCost *= (float)(randomizer.Int32(900, 1000 + (int)(prevSegment.m_trafficDensity * 10)) + this._pathRandomizer.Int32(20u)) * 0.001f;
				} else {
					// NON-STOCK CODE

#if DEBUGNEWPF
					float oldPrevCost = prevCost;
#endif

					prevCost *= 1f + _conf.SpeedCostFactor * prevUsage;
					prevCost *= 1f + _conf.TrafficCostFactor * prevTraffic;

#if DEBUGNEWPF
					if (debug)
						logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
							"\t" + $"applied traffic cost factors:\n" +
							"\t" + $"oldPrevCost={oldPrevCost}\n" +
							"\t" + $"=> prevCost={prevCost}\n"
							);
#endif
				}
			}

			if (!useAdvancedAI) {
				// stock code check for vehicle ban policies

				if (this._isHeavyVehicle && (prevSegment.m_flags & NetSegment.Flags.HeavyBan) != NetSegment.Flags.None) {
					// heavy vehicle ban
#if DEBUGNEWPF
					float oldPrevCost = prevCost;
#endif
					prevCost *= 10f;

#if DEBUGNEWPF
					if (debug)
						logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
							"\t" + $"applied heavy traffic ban on deactivated AI:\n" +
							"\t" + $"oldPrevCost={oldPrevCost}\n" +
							"\t" + $"=> prevCost={prevCost}\n"
							);
#endif
				} else if (prevLaneType == NetInfo.LaneType.Vehicle && (prevVehicleType & _vehicleTypes) == VehicleInfo.VehicleType.Car && (prevSegment.m_flags & NetSegment.Flags.CarBan) != NetSegment.Flags.None) {
					// car ban: used by "Old Town" policy
#if DEBUGNEWPF
					float oldPrevCost = prevCost;
#endif
					prevCost *= 5f;

#if DEBUGNEWPF
					if (debug)
						logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
							"\t" + $"applied car ban on deactivated AI:\n" +
							"\t" + $"oldPrevCost={oldPrevCost}\n" +
							"\t" + $"=> prevCost={prevCost}\n"
							);
#endif
				}

				if (this._transportVehicle && prevLaneType == NetInfo.LaneType.TransportVehicle) {
					// public transport/emergency vehicles should stay on their designated lanes, if possible
#if DEBUGNEWPF
					float oldPrevCost = prevCost;
#endif
					prevCost *= 0.5f; // non-stock value

#if DEBUGNEWPF
					if (debug)
						logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
							"\t" + $"applied tranport lane reward on deactivated AI:\n" +
							"\t" + $"oldPrevCost={oldPrevCost}\n" +
							"\t" + $"=> prevCost={prevCost}\n"
							);
#endif
				}
			}

			// check vehicle ban policies
			if ((this._isHeavyVehicle && (nextSegment.m_flags & NetSegment.Flags.HeavyBan) != NetSegment.Flags.None) ||
				(prevLaneType == NetInfo.LaneType.Vehicle && (prevVehicleType & _vehicleTypes) == VehicleInfo.VehicleType.Car &&
				(prevSegment.m_flags & NetSegment.Flags.CarBan) != NetSegment.Flags.None)) {
#if DEBUGNEWPF
				if (debug) {
					logBuf.Add($"Vehicle {_extVehicleType} should not use lane {item.m_position.m_lane} @ seg. {item.m_position.m_segment}, null? {prevLaneInfo == null}");
				}
#endif
				avoidLane = true;
			}

			if (strictlyAvoidLane) {
#if DEBUGNEWPF
				float oldPrevCost = prevCost;
#endif
				prevCost *= _conf.VehicleRestrictionsPenalty;
#if DEBUGNEWPF
				if (debug)
					logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
						"\t" + $"applied strict lane avoidance:\n" +
						"\t" + $"oldPrevCost={oldPrevCost}\n" +
						"\t" + $"=> prevCost={prevCost}\n"
						);
#endif
			}

			if (!useAdvancedAI) {
				// apply vehicle restrictions when not using Advanced AI
				

				// add costs for u-turns
				if (!isMiddle && nextSegmentId == item.m_position.m_segment && (prevLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
#if DEBUGNEWPF
					float oldPrevCost = prevCost;
#endif
					prevCost *= (float)_conf.UturnLaneDistance;
#if DEBUGNEWPF
					if (debug)
						logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
							"\t" + $"applied u-turn cost factor on deactivated AI:\n" +
							"\t" + $"oldPrevCost={oldPrevCost}\n" +
							"\t" + $"=> prevCost={prevCost}\n"
							);
#endif
				}
			}

			if ((byte)(prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
				prevLaneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
			}
			float prevOffsetCost = (float)Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * prevCost;
			float prevMethodDist = item.m_methodDistance + prevOffsetCost;
			float prevComparisonPlusOffsetCostOverSpeed = item.m_comparisonValue + prevOffsetCost / (prevLaneSpeed * this._maxLength);
			Vector3 prevLanePosition = netManager.m_lanes.m_buffer[item.m_laneID].CalculatePosition((float)connectOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
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

			bool nextIsStartNodeOfNextSegment = netManager.m_segments.m_buffer[nextSegmentId].m_startNode == nextNodeId;
			bool uturn = !isMiddle && nextSegmentId == item.m_position.m_segment;

			float laneChangeRoadBaseCost = 1f;
			if (laneChangingCostCalculationMode != LaneChangingCostCalculationMode.None) {
				laneChangeRoadBaseCost = nextSegmentRouting.highway ? _conf.HighwayLaneChangingBaseCost : _conf.CityRoadLaneChangingBaseCost;
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

				if (forcedLaneId == null && Options.laneConnectorEnabled) {
#if DEBUGNEWPF
					if (debug)
						logBuf.Add($"forceLaneId == null. Checking if next lane idx {laneIndex}, id {curLaneId} has outgoing connections (startNode={nextIsStartNodeOfNextSegment})");
#endif

					if (laneConnManager.HasConnections(curLaneId, nextIsStartNodeOfNextSegment) && !laneConnManager.AreLanesConnected(curLaneId, item.m_laneID, nextIsStartNodeOfNextSegment)) {
#if DEBUGNEWPF
						if (debug) {
							logBuf.Add($"Source lane {curLaneId} is NOT connected with target lane {item.m_laneID}, but source lane has outgoing connections! Skipping lane");
						}
#endif
						goto CONTINUE_LANE_LOOP;
					}

#if DEBUGNEWPF
					if (debug)
						logBuf.Add($"Check for outgoing connections passed!");
#endif
				}

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
						float nextMaxSpeed = /*emergencyLaneSelection ? 5f : */GetLaneSpeedLimit(nextSegmentId, laneIndex, curLaneId, nextLaneInfo);
						// determine if traffic measurements should be taken into account
						bool useAdvancedAIforNextLane = useAdvancedAI && // advanced AI is activated and allowed
							((nextLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) &&
							(nextLaneInfo.m_laneType & (NetInfo.LaneType.Pedestrian | NetInfo.LaneType.Parking)) == NetInfo.LaneType.None; // next lane is a lane for driving vehicles
						bool calculateTrafficStatsForNextLane = calculateTrafficStats && useAdvancedAIforNextLane;
						float customDeltaCost = 0f;
						// NON-STOCK CODE END //

						Vector3 nextLaneEndPointPos;
						if ((byte)(nextDir & NetInfo.Direction.Forward) != 0) {
							nextLaneEndPointPos = netManager.m_lanes.m_buffer[curLaneId].m_bezier.d;
						} else {
							nextLaneEndPointPos = netManager.m_lanes.m_buffer[curLaneId].m_bezier.a;
						}
						float transitionCost = Vector3.Distance(nextLaneEndPointPos, prevLanePosition); // This gives the distance of the previous to next lane endpoints.

#if DEBUGNEWPF
						if (debug)
							logBuf.Add($"costs from {nextSegmentId} (off {(byte)(((nextDir & NetInfo.Direction.Forward) == 0) ? 0 : 255)}) to {item.m_position.m_segment} (off {item.m_position.m_offset}), connectOffset={connectOffset}: transitionCost={transitionCost}");
#endif

						if (transitionNode) {
							transitionCost *= 2f;
						}

						float transitionCostOverMeanMaxSpeed = transitionCost / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength);
						BufferItem nextItem;
						nextItem.m_position.m_segment = nextSegmentId;
						nextItem.m_position.m_lane = (byte)laneIndex;
						nextItem.m_position.m_offset = (byte)(((nextDir & NetInfo.Direction.Forward) == 0) ? 0 : 255);
						if ((byte)(nextLaneInfo.m_laneType & prevLaneType) == 0) {
							nextItem.m_methodDistance = 0f;
						} else {
							nextItem.m_methodDistance = prevMethodDist + transitionCost;
						}

#if DEBUGNEWPF
						if (debug)
							logBuf.Add($"checking if methodDistance is in range: {nextLaneInfo.m_laneType != NetInfo.LaneType.Pedestrian} || {nextItem.m_methodDistance < 1000f} ({nextItem.m_methodDistance})");
#endif

						if (nextLaneInfo.m_laneType != NetInfo.LaneType.Pedestrian || nextItem.m_methodDistance < 1000f) {
							// NON-STOCK CODE START //

							if (!useAdvancedAIforNextLane) {
								// stock code
								nextItem.m_comparisonValue = prevComparisonPlusOffsetCostOverSpeed + transitionCostOverMeanMaxSpeed;
							} else {
								nextItem.m_comparisonValue = item.m_comparisonValue;
								customDeltaCost = transitionCost + prevOffsetCost; // customDeltaCost now holds the costs for driving on the segment + costs for changing the segment

								


								if (avoidLane && (_extVehicleType == null || (_extVehicleType & (ExtVehicleType.CargoTruck | ExtVehicleType.PassengerCar)) != ExtVehicleType.None)) {
#if DEBUGNEWPF
									float oldCustomDeltaCost = customDeltaCost;
#endif
									// apply vanilla game restriction policies
									customDeltaCost *= 3f;
#if DEBUGNEWPF
									if (debug)
										logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
											"\t" + $"applied lane avoidance on activated AI:\n" +
											"\t" + $"oldCustomDeltaCost={oldCustomDeltaCost}\n" +
											"\t" + $"=> customDeltaCost={customDeltaCost}\n"
											);
#endif
								}

#if DEBUGNEWPF
								if (debug) {
									logBuf.Add($"Path from {nextSegmentId} (idx {laneIndex}, id {curLaneId}) to {item.m_position.m_segment} (lane {prevOuterSimilarLaneIndex} from outer, idx {item.m_position.m_lane}): useAdvancedAI={useAdvancedAI}, transitionCost={transitionCost}, avoidLane={avoidLane}");
								}
#endif
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
								nextItem.m_comparisonValue += nextOffset * Mathf.Max(_conf.SegmentMinAverageLength, nextSegment.m_averageLength) / (nextLaneSpeed * this._maxLength);
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
								nextItem.m_comparisonValue += nextOffset * Mathf.Max(_conf.SegmentMinAverageLength, nextSegment.m_averageLength) / (nextLaneSpeed * this._maxLength);
							}

							if (!this._ignoreBlocked && (nextSegment.m_flags & NetSegment.Flags.Blocked) != NetSegment.Flags.None && (byte)(nextLaneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
								// NON-STOCK CODE START //
								if (useAdvancedAIforNextLane) {
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
									logBuf.Add($"Applying lane and transport vehicle costs");
#endif

								// NON-STOCK CODE START //
								if (Options.advancedAI) {
									/*
									 * (1) busses should prefer transport lanes
									 * (2) regular traffic should prefer regular lanes
									 * (3) taxis, service vehicles and emergency vehicles may choose freely between regular and transport lanes
									 */

									if (_extVehicleType != null && (_extVehicleType & ExtVehicleType.RoadVehicle) != ExtVehicleType.None) {
										if ((nextLaneInfo.m_laneType & NetInfo.LaneType.TransportVehicle) != NetInfo.LaneType.None) {
											// next lane is a public transport lane
											if ((_extVehicleType & ExtVehicleType.Bus) != ExtVehicleType.None) {
#if DEBUGNEWPF
												float oldCustomDeltaCost = customDeltaCost;
#endif
												customDeltaCost *= _conf.PublicTransportLaneReward; // (1)
#if DEBUGNEWPF
												if (debug)
													logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
														"\t" + $"applied bus-on-transport lane reward on activated AI:\n" +
														"\t" + $"oldCustomDeltaCost={oldCustomDeltaCost}\n" +
														"\t" + $"=> customDeltaCost={customDeltaCost}\n"
														);
#endif
											} else if ((_extVehicleType & (ExtVehicleType.RoadPublicTransport | ExtVehicleType.Emergency)) == ExtVehicleType.None) {
#if DEBUGNEWPF
												float oldCustomDeltaCost = customDeltaCost;
#endif
												customDeltaCost *= _conf.PublicTransportLanePenalty; // (2)
#if DEBUGNEWPF
												if (debug)
													logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
														"\t" + $"applied car-on-transport lane penalty on activated AI:\n" +
														"\t" + $"oldCustomDeltaCost={oldCustomDeltaCost}\n" +
														"\t" + $"=> customDeltaCost={customDeltaCost}\n"
														);
#endif
											} else {
												// (3), do nothing
											}
										}
									}
								} else {
									if (obeyStockLaneArrows) { // TODO check this
										// NON-STOCK CODE END //
															   // this is CO's way of matching lanes between segments
										int firstTarget = (int)netManager.m_lanes.m_buffer[curLaneId].m_firstTarget;
										int lastTarget = (int)netManager.m_lanes.m_buffer[curLaneId].m_lastTarget;
										if (laneIndexFromInner < firstTarget || laneIndexFromInner >= lastTarget) {
											nextItem.m_comparisonValue += Mathf.Max(1f, transitionCost * 3f - 3f) / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength);
										}
									} // NON-STOCK CODE

									// cars should not be on public transport lanes
									if (!this._transportVehicle && nextLaneInfo.m_laneType == NetInfo.LaneType.TransportVehicle) {
										nextItem.m_comparisonValue += 20f / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength);
									}
								} // NON-STOCK CODE
							}

							// NON-STOCK CODE START //
							bool addItem = true; // should we add the next item to the buffer?
							if (useAdvancedAIforNextLane) {
								// Advanced AI cost calculation

#if DEBUGNEWPF
								if (debug)
									logBuf.Add($"Calculating advanced AI costs");
#endif

								int nextOuterSimilarLaneIndex;
								if ((byte)(nextLaneInfo.m_finalDirection & NetInfo.Direction.Forward) != 0) {
									nextOuterSimilarLaneIndex = nextLaneInfo.m_similarLaneCount - nextLaneInfo.m_similarLaneIndex - 1;
								} else {
									nextOuterSimilarLaneIndex = nextLaneInfo.m_similarLaneIndex;
								}

								int laneDist; // absolute lane distance
								if (laneChangingCostCalculationMode == LaneChangingCostCalculationMode.ByGivenDistance && forcedLaneDist != null) {
									laneDist = (byte)forcedLaneDist;
								} else {
									int relLaneDist = nextOuterSimilarLaneIndex - prevOuterSimilarLaneIndex; // relative lane distance (positive: change to more outer lane, negative: change to more inner lane)
									laneDist = Math.Abs(relLaneDist);
								}
									

								if (forcedLaneIndex == null && prevSegmentRouting.highway && nextSegmentRouting.highway && laneDist > 1) {
									// disable lane changing by more than one on highways
									goto CONTINUE_LANE_LOOP;
								}

								float metric = 1f; // resulting cost multiplicator
#if DEBUGNEWPF
								float metricBeforeLanes = 1f; // metric before multiplying lane changing costs
#endif
								if (calculateTrafficStatsForNextLane) {

									/* Vehicles should
										- choose lanes with low traffic volume,
										- choose lanes with high speeds,
										- not change to lanes that with high distance to current lane,
										- not change lanes too often and
										- should not change lanes near junctions
									*/

									// calculate speed metric
									float divMetric = /*prevSpeed **/ (prevMaxSpeed + nextMaxSpeed) * 0.5f; // the division part; 0 .. (nextMaxSpeed + nextMaxSpeed)/2

									// calculate density metric
									/*if (prevSpeed <= Options.someValue13)
										prevDensity = 1f;*/
									float multMetric = 1f; // _conf.SpeedToDensityBalance + (1f - _conf.SpeedToDensityBalance) * prevDensity; // the multiplication part

									// calculate density/speed metric
									metric = /*Math.Max(0.01f, */multMetric/*)*/ / Math.Max(0.1f, divMetric);
#if DEBUGNEWPF
									metricBeforeLanes = metric;
#endif
								}

								float laneMetric = 1f;
#if DEBUGNEWPF
								bool calculatedLaneCosts = false;
								float laneChangeCostBase = 0f;
#endif
								if (
									laneChangingCostCalculationMode != LaneChangingCostCalculationMode.None && // applying lane changing costs is allowed
									//!nextIsRealJunction && // no lane changing at junctions
									laneDist > 0 && // lane would be changed
									_extVehicleType != ExtVehicleType.Emergency && // emergency vehicles may do everything
									(!wantToChangeLane || laneDist > 1 || isCongested)) { // randomized lane changing

#if MARKCONGESTEDSEGMENTS2
									int nextDirIndex = nextLaneInfo.m_finalDirection == NetInfo.Direction.Backward ? 1 : 0;
									float nextMinSpeed = (ushort)(CustomRoadAI.segmentDirMinSpeeds[nextSegmentId][nextDirIndex] / 100);
									nextMinSpeed = (float)Math.Min(1f, Math.Max(0.05f, Math.Round(prevDensity / 5f) / 20f)); // 0.05, 0.1, 0.15, ..., 1

									bool nextIsCongested = nextMinSpeed < _conf.CongestionSpeedThreshold;
									float nextCongestionFactor = 1f - nextMinSpeed;
#endif


									// multiply with lane distance if distance > 1 or if vehicle does not like to change lanes
#if !DEBUGNEWPF
									float
#endif
									laneChangeCostBase = 
										laneChangeRoadBaseCost * // changing lanes on highways is more expensive than on city streets
										_vehicleCosts * // changing lanes is more expensive for heavy vehicles
										(laneDist > 1 ? _conf.MoreThanOneLaneChangingCostFactor : 1f) * // changing more than one lane at a time is expensive
										congestionLaneChangingCosts // lane changing at congested segments is expensive
										;

									// we use the power operator here to express that lane changing one-by-one is preferred over changing multiple lanes at once
									laneMetric = (float)Math.Pow(laneChangeCostBase, laneDist);
									metric *= laneMetric;

#if DEBUGNEWPF
									calculatedLaneCosts = true;
#endif
								}

								// avoid lane changing before junctions: multiply with inverted distance to next junction
#if JUNCTIONLANECHANGEPENALTY
								if (//allowLaneChangingCosts && //(!prevIsHighway || !nextIsHighway) &&
									!nextIsRealJunction &&
									_extVehicleType != ExtVehicleType.Emergency &&
									laneDist > 0) {
									uint dist = _pathRandomizer.UInt32(_conf.MinNumSegmentsAheadJunctionLaneChangePenalty, _conf.MaxNumSegmentsAheadJunctionLaneChangePenalty+1u);
									if (nextItem.m_numSegmentsToJunction < dist) {
										float junctionMetric = _conf.JunctionLaneChangingBaseCost * (float)(dist - nextItem.m_numSegmentsToJunction);
										metric *= junctionMetric;
									}
								}
#endif

								float oldTransitionDistanceOverMaxSpeed = transitionCostOverMeanMaxSpeed;
								float deltaCostOverMeanMaxSpeed = (metric * customDeltaCost) / this._maxLength;

								if (deltaCostOverMeanMaxSpeed < 0f) {
									// should never happen
#if DEBUG
									Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId}, PF {this._pathFindIndex}: distanceOverMeanMaxSpeed < 0! seg. {nextSegmentId}, lane {laneIndex}, off {nextItem.m_position.m_offset} -> {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}. distanceOverMeanMaxSpeed={deltaCostOverMeanMaxSpeed}, prevSpeed={prevUsage}"/* + ", prevSpeed={prevSpeed}"*/);
#endif
									deltaCostOverMeanMaxSpeed = 0f;
								} else if (Single.IsNaN(deltaCostOverMeanMaxSpeed) || Single.IsInfinity(deltaCostOverMeanMaxSpeed)) {
									// Fallback if we mess something up. Should never happen.
#if DEBUG
									//if (costDebug)
									Log.Error($"Pathfinder ({this._pathFindIndex}): distanceOverMeanMaxSpeed is NaN or Infinity: seg. {nextSegmentId}, lane {laneIndex}, off {nextItem.m_position.m_offset} -> {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}. {deltaCostOverMeanMaxSpeed} // nextMaxSpeed={nextMaxSpeed} prevMaxSpeed={prevMaxSpeed} nextMaxSpeed={nextMaxSpeed} laneDist={laneDist} laneMetric={laneMetric} metric={metric}");
#endif
#if DEBUGNEWPF
									Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId}, PF {this._pathFindIndex}: deltaCostOverMeanMaxSpeed is NaN! deltaCostOverMeanMaxSpeed={deltaCostOverMeanMaxSpeed}");
#endif
									deltaCostOverMeanMaxSpeed = oldTransitionDistanceOverMaxSpeed;
								}

								nextItem.m_comparisonValue += deltaCostOverMeanMaxSpeed;

								if (nextItem.m_comparisonValue > 1f) {
									// comparison value got too big. Do not add the lane to the buffer
									addItem = false;
								}
#if DEBUGNEWPF
								if (debug)
									logBuf.Add($"item: seg. {item.m_position.m_segment}, lane {item.m_position.m_lane}, node {nextNodeId}:\n" +
										$"-> TRANSIT to seg. {nextSegmentId}, lane {laneIndex}\n" +
										"\t" + $"prevUsage={prevUsage}\n" +
										"\t" + $"prevTraffic={prevTraffic}\n" +
										"\t" + $"prevMaxSpeed={prevMaxSpeed}\n" +
										"\t" + $"nextMaxSpeed={nextMaxSpeed}\n\n" +
										"\t" + $"=> metric={metric}\n" +
										"\t" + $"=> metricBeforeLanes={metricBeforeLanes}\n\n" +
										"\t" + $"laneChangingCostCalculationMode={laneChangingCostCalculationMode}\n\n" +
										"\t" + $"laneDist={laneDist}\n\n" +
										"\t" + $"_extVehicleType={_extVehicleType}\n" +
										"\t" + $"wantToChangeLane={wantToChangeLane}\n" +
										"\t" + $"isCongested={isCongested}\n" +
										"\t" + $"=> calculatedLaneCosts={calculatedLaneCosts}\n\n" +
										"\t" + $"laneChangeRoadBaseCost={laneChangeRoadBaseCost}\n" +
										"\t" + $"_vehicleCosts={_vehicleCosts}\n" +
										"\t" + $"moreThanOneLaneCost={(laneDist > 1 ? _conf.MoreThanOneLaneChangingCostFactor : 1f)}\n" +
										"\t" + $"congestionLaneChangingCosts={congestionLaneChangingCosts}\n" +
										"\t" + $"=> laneChangeCostBase={laneChangeCostBase}\n" +
										"\t" + $"=> laneMetric={laneMetric}\n\n" +
										"\t" + $"deltaCostOverMeanMaxSpeed={deltaCostOverMeanMaxSpeed}\n" +
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
								nextItem.m_speedRand = usageRand;
								nextItem.m_trafficRand = trafficRand;
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
		private void ProcessItemPedBicycle(bool debug, BufferItem item, ushort targetNodeId, ushort nextSegmentId, ref NetSegment prevSegment, ref NetSegment nextSegment, byte connectOffset, byte laneSwitchOffset, int laneIndex, uint lane) {
			if ((nextSegment.m_flags & _disableMask) != NetSegment.Flags.None) {
				return;
			}
			// NON-STOCK CODE START
			// check if pedestrians are not allowed to cross here
			bool nextIsStartNode = targetNodeId == nextSegment.m_startNode;
			if (!junctionManager.IsPedestrianCrossingAllowed(nextSegmentId, nextIsStartNode))
				return;

			// check if pedestrian light won't change to green
			CustomSegmentLights lights = customTrafficLightsManager.GetSegmentLights(nextSegmentId, nextIsStartNode, false);
			if (lights != null) {
				if (lights.InvalidPedestrianLight) {
					return;
				}
			}
			// NON-STOCK CODE END
			NetManager netManager = Singleton<NetManager>.instance;
			// NON-STOCK CODE START
			/*if (!_allowEscapeTransport) {
				ushort transportLineId = netManager.m_nodes.m_buffer[targetNodeId].m_transportLine;
				if (transportLineId != 0 && Singleton<TransportManager>.instance.m_lines.m_buffer[transportLineId].Info.m_transportType == TransportInfo.TransportType.EvacuationBus)
					return;
			}*/
			// NON-STOCK CODE END
			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = prevSegment.Info;
			int num = nextSegmentInfo.m_lanes.Length;
			float distance;
			byte offset;
			Vector3 b = netManager.m_lanes.m_buffer[item.m_laneID].CalculatePosition((float)laneSwitchOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
			if (nextSegmentId == item.m_position.m_segment) {
				// next segment is previous segment
				Vector3 a = netManager.m_lanes.m_buffer[lane].CalculatePosition((float)connectOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
				distance = Vector3.Distance(a, b);
				offset = connectOffset;
			} else {
				// next segment differs from previous segment
				NetInfo.Direction direction = (targetNodeId != nextSegment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
				Vector3 a;
				if ((byte)(direction & NetInfo.Direction.Forward) != 0) {
					a = netManager.m_lanes.m_buffer[lane].m_bezier.d;
				} else {
					a = netManager.m_lanes.m_buffer[lane].m_bezier.a;
				}
				distance = Vector3.Distance(a, b);
				offset = (byte)(((direction & NetInfo.Direction.Forward) == 0) ? 0 : 255);
			}
			float prevMaxSpeed = 1f;
			float prevSpeed = 1f;
			NetInfo.LaneType laneType = NetInfo.LaneType.None;
			VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None; // NON-STOCK CODE
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane prevLane = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				prevMaxSpeed = GetLaneSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, prevLane); // SpeedLimitManager.GetLockFreeGameSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, ref lane2); // NON-STOCK CODE
				laneType = prevLane.m_laneType;
				vehicleType = prevLane.m_vehicleType; // NON-STOCK CODE
				if ((byte)(laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
					laneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
				prevSpeed = this.CalculateLaneSpeed(prevMaxSpeed, laneSwitchOffset, item.m_position.m_offset, ref prevSegment, prevLane); // NON-STOCK CODE
			}
			float prevCost = prevSegment.m_averageLength;
			// NON-STOCK CODE START
			//if (_extVehicleType == ExtVehicleType.Bicycle) {
			/*if ((vehicleType & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None) {
				prevCost *= 0.95f;
			} else if ((prevSegment.m_flags & NetSegment.Flags.BikeBan) != NetSegment.Flags.None) {
				prevCost *= 5f;
			}*/
			//}
			ushort sourceNodeId = (targetNodeId == prevSegment.m_startNode) ? prevSegment.m_endNode : prevSegment.m_startNode; // no lane changing directly in front of a junction
			bool prevIsJunction = (netManager.m_nodes.m_buffer[sourceNodeId].m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
			// NON-STOCK CODE END
			float offsetLength = (float)Mathf.Abs((int)(laneSwitchOffset - item.m_position.m_offset)) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * prevCost;
			float methodDistance = item.m_methodDistance + offsetLength;
			float comparisonValue = item.m_comparisonValue + offsetLength / (prevSpeed * this._maxLength);
			if (laneIndex < num) {
				NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[laneIndex];
				BufferItem nextItem;
				nextItem.m_position.m_segment = nextSegmentId;
				nextItem.m_position.m_lane = (byte)laneIndex;
				nextItem.m_position.m_offset = offset;
				if ((byte)(nextLaneInfo.m_laneType & laneType) == 0) {
					nextItem.m_methodDistance = 0f;
				} else {
					if (item.m_methodDistance == 0f) {
						comparisonValue += 100f / (0.25f * this._maxLength);
					}
					nextItem.m_methodDistance = methodDistance + distance;
				}
				float nextMaxSpeed = GetLaneSpeedLimit(nextSegmentId, (uint)laneIndex, lane, nextLaneInfo); // NON-STOCK CODE
				if (nextLaneInfo.m_laneType != NetInfo.LaneType.Pedestrian || nextItem.m_methodDistance < 1000f) {
					nextItem.m_comparisonValue = comparisonValue + distance / ((prevMaxSpeed + nextMaxSpeed) * 0.25f * this._maxLength);
					if ((nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
						nextItem.m_direction = NetInfo.InvertDirection(nextLaneInfo.m_finalDirection);
					} else {
						nextItem.m_direction = nextLaneInfo.m_finalDirection;
					}
					if (lane == this._startLaneA) {
						if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetA) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetA)) {
							return;
						}
						float nextSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetA, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo); // NON-STOCK CODE
						float nextOffset = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetA)) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;
						nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * this._maxLength);
					}
					if (lane == this._startLaneB) {
						if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetB) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetB)) {
							return;
						}
						float nextSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo); // NON-STOCK CODE
						float nextOffset = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetB)) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;
						nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * this._maxLength);
					}
					nextItem.m_laneID = lane;
					nextItem.m_lanesUsed = (item.m_lanesUsed | nextLaneInfo.m_laneType);
					nextItem.m_vehiclesUsed = (item.m_vehiclesUsed | nextLaneInfo.m_vehicleType);
					nextItem.m_speedRand = 0;
					nextItem.m_trafficRand = 0;
#if DEBUGNEWPF
					if (debug) {
						_debugPositions[item.m_position.m_segment].Add(nextItem.m_position.m_segment);
					}
#endif
					this.AddBufferItem(nextItem, item.m_position);
				}
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
#if DEBUGPF
						/*if (m_queuedPathFindCount > 100 && _conf.DebugSwitches[0])
							Log._Debug($"Pathfind Thread #{Thread.CurrentThread.ManagedThreadId} waiting now for queue lock {QueueLock.GetHashCode()}!");*/
#endif
						Monitor.Wait(QueueLock);
						//if (!Monitor.Wait(QueueLock, SYNC_TIMEOUT)) {
#if DEBUGPF
							/*if (m_queuedPathFindCount > 100 && _conf.DebugSwitches[0])
								Log.Warning($"Pathfind Thread #{Thread.CurrentThread.ManagedThreadId} *WAIT TIMEOUT* waiting for queue lock {QueueLock.GetHashCode()}!");*/
#endif
						//}
					}

#if DEBUGPF3
					Log._Debug($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread iteration START. QueueFirst={QueueFirst} QueueLast={QueueLast}");
#endif
#if DEBUGPF
					/*if (m_queuedPathFindCount > 100 && _conf.DebugSwitches[0])
						Log._Debug($"Pathfind Thread #{Thread.CurrentThread.ManagedThreadId} is continuing now!");*/
#endif
					if (Terminated) {
						break;
					}
					Calculating = QueueFirst;
#if DEBUGPF3
					Log._Debug($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread iteration. Setting Calculating=QueueFirst=Calculating. Setting QueueFirst={PathUnits.m_buffer[Calculating].m_nextPathUnit}");
#endif
					QueueFirst = CustomPathManager._instance.queueItems[Calculating].nextPathUnitId;
					//QueueFirst = PathUnits.m_buffer[Calculating].m_nextPathUnit;
					if (QueueFirst == 0u) {
						QueueLast = 0u;
						m_queuedPathFindCount = 0;
					} else {
						--m_queuedPathFindCount;
					}
#if DEBUGPF
					/*if (m_queuedPathFindCount > 100 && _conf.DebugSwitches[0])
						Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PathFindThread: Starting pathfinder. Remaining queued pathfinders: {m_queuedPathFindCount}"); */
#endif
					try {
						Monitor.Enter(CustomPathManager._instance.QueueItemLock);
						CustomPathManager._instance.queueItems[Calculating].nextPathUnitId = 0u;
					} finally {
						Monitor.Exit(CustomPathManager._instance.QueueItemLock);
					}
					//PathUnits.m_buffer[Calculating].m_nextPathUnit = 0u;

					// check if path unit is created
					if ((PathUnits.m_buffer[Calculating].m_pathFindFlags & PathUnit.FLAG_CREATED) == 0) {
						Log.Warning($"CustomPathFind: Refusing to calculate path unit {Calculating} which is not created!");
						continue;
					}

					PathUnits.m_buffer[Calculating].m_pathFindFlags = (byte)((PathUnits.m_buffer[Calculating].m_pathFindFlags & ~PathUnit.FLAG_CREATED) | PathUnit.FLAG_CALCULATING);

#if DEBUGPF3
					Log._Debug($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread iteration END. QueueFirst={QueueFirst} QueueLast={QueueLast} Calculating={Calculating} flags={PathUnits.m_buffer[Calculating].m_pathFindFlags}");
#endif
				} catch (Exception e) {
					Log.Error($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread Error for unit {Calculating}, flags={PathUnits.m_buffer[Calculating].m_pathFindFlags} (1): {e.ToString()}");
				} finally {
					Monitor.Exit(QueueLock);
				}
				
				// calculate path unit
				try {
#if DEBUGPF
					m_pathfindProfiler.BeginStep();
#endif
#if DEBUGPF
					/*if (m_queuedPathFindCount > 100 && _conf.DebugSwitches[0])
						Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Calling PathFindImplementation now. Calculating={Calculating}");*/
#endif
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
					CustomPathManager._instance.ResetPathUnit(Calculating);

					PathUnits.m_buffer[Calculating].m_pathFindFlags |= PathUnit.FLAG_FAILED;
				} finally {
#if DEBUGPF
					m_pathfindProfiler.EndStep();
#endif
#if DEBUGPF
					/*if (m_queuedPathFindCount > 100 && _conf.DebugSwitches[0])
						Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} last step duration: {m_pathfindProfiler.m_lastStepDuration} average step duration: {m_pathfindProfiler.m_averageStepDuration} peak step duration: {m_pathfindProfiler.m_peakStepDuration}");*/
#endif
				}
				//tCurrentState = 10;
#if DEBUGLOCKS
				lockIter = 0;
#endif

				try {
					Monitor.Enter(QueueLock);

#if DEBUGPF3
					Log._Debug($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread calculation finished for unit {Calculating}. flags={PathUnits.m_buffer[Calculating].m_pathFindFlags}");

					List<uint> allUnits = new List<uint>();
					uint currentUnit = this.QueueFirst;
					int i = 0;
					while (currentUnit != 0 && currentUnit != QueueLast) {
						allUnits.Add(currentUnit);
						currentUnit = this.PathUnits.m_buffer[currentUnit].m_nextPathUnit;
						++i;
						if (i > 10000) {
							Log.Error($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}): !!! CYCLE ???");
							break;
						}
					}
					allUnits.Add(QueueLast);
					Log._Debug($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}): allUnits={string.Join(", ", allUnits.Select(x => x.ToString()).ToArray())}");
#endif

					PathUnits.m_buffer[Calculating].m_pathFindFlags = (byte)(PathUnits.m_buffer[Calculating].m_pathFindFlags & ~PathUnit.FLAG_CALCULATING);
					Singleton<PathManager>.instance.ReleasePath(Calculating);
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

			if (_extVehicleType == null || _extVehicleType == ExtVehicleType.None || _extVehicleType == ExtVehicleType.Tram)
				return true;

			/*if (laneInfo == null)
				laneInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex];*/

			if ((laneInfo.m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train)) == VehicleInfo.VehicleType.None)
				return true;

			ExtVehicleType allowedTypes = vehicleRestrictionsManager.GetAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo, RestrictionMode.Configured);
#if DEBUGPF
			if (debug) {
				Log._Debug($"CanUseLane: segmentId={segmentId} laneIndex={laneIndex} _extVehicleType={_extVehicleType} _vehicleTypes={_vehicleTypes} _laneTypes={_laneTypes} _transportVehicle={_transportVehicle} _isHeavyVehicle={_isHeavyVehicle} allowedTypes={allowedTypes} res={((allowedTypes & _extVehicleType) != ExtVehicleType.None)}");
			}
#endif

			return ((allowedTypes & _extVehicleType) != ExtVehicleType.None);
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
