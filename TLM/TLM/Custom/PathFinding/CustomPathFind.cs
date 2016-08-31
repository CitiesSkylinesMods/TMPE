#define DEBUGPFx
#define DEBUGPF2x
#define DEBUGPF3x
#define DEBUGMERGEx
#define DEBUGLOCKSx
#define DEBUGCOSTSx
#define MARKCONGESTEDSEGMENTSx
#define EXTRAPFx
#define VEHICLERANDx
#define PREFEROUTERx

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

namespace TrafficManager.Custom.PathFinding {
	public class CustomPathFind : PathFind {
		private struct BufferItem {
			public PathUnit.Position m_position;
			public float m_comparisonValue;
			public float m_methodDistance;
			public uint m_laneID;
			public NetInfo.Direction m_direction;
			public NetInfo.LaneType m_lanesUsed;
			public uint m_numSegmentsToJunction;
		}

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
		private ExtVehicleType? _extVehicleType;
		private ushort? _vehicleId;
#if VEHICLERAND
		private float _vehicleRand;
#endif
		private bool _extPublicTransport;
		private static ushort laneChangeRandCounter = 0;
#if DEBUG
		public uint _failedPathFinds = 0;
		public uint _succeededPathFinds = 0;
#endif
		public int pfId = 0;
		private Randomizer _pathRandomizer;
		private uint _pathFindIndex;
		private NetInfo.LaneType _laneTypes;
		private VehicleInfo.VehicleType _vehicleTypes;

		// used in ProcessItemMain
		private byte[] laneIndexes = new byte[16]; // index of NetSegment.Info.m_lanes
		private uint[] laneIds = new uint[16]; // index of NetManager.m_lanes.m_buffer
		private byte[] nextOuterLaneSimilarIndexes = new byte[16];
		private byte[] nextInnerLaneSimilarIndexes = new byte[16];
		private byte[] laneIndexByOuterSimilarIndex = new byte[16];
		private bool[] hasOutgoingConnections = new bool[16];
		private bool[] nextIsConnectedWithPrevious = new bool[16];
		private ushort compatibleOuterSimilarIndexesMask = (ushort)0;
		private static readonly ushort[] POW2MASKS = new ushort[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768 };

		private static readonly CustomTrafficLightsManager customTrafficLightsManager = CustomTrafficLightsManager.Instance();
		private static readonly LaneConnectionManager laneConnManager = LaneConnectionManager.Instance();
		private static readonly JunctionRestrictionsManager junctionManager = JunctionRestrictionsManager.Instance();
		private static readonly VehicleRestrictionsManager vehicleRestrictionsManager = VehicleRestrictionsManager.Instance();
		private static readonly SpeedLimitManager speedLimitManager = SpeedLimitManager.Instance();

		public bool IsMasterPathFind = false;
#if EXTRAPF
		public bool IsExtraPathFind = false;
#endif

		private ExtVehicleType?[] pathUnitExtVehicleType = null;
		private ushort?[] pathUnitVehicleIds = null;

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

			if (pathUnitExtVehicleType == null) {
				pathUnitExtVehicleType = new ExtVehicleType?[PathUnits.m_size];
				pathUnitVehicleIds = new ushort?[PathUnits.m_size];
			}

			m_pathfindProfiler = new ThreadProfiler();
			CustomPathFindThread = new Thread(PathFindThread) { Name = "Pathfind" };
			CustomPathFindThread.Priority = SimulationManager.SIMULATION_PRIORITY;
			CustomPathFindThread.Start();
			if (!CustomPathFindThread.IsAlive) {
				//CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find thread failed to start!");
				Log.Error($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) Path find thread failed to start!");
			}

		}

#region stock code
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
			return ExtCalculatePath(null, null, unit, skipQueue);
		}

		public bool ExtCalculatePath(ExtVehicleType? vehicleType, ushort? vehicleId, uint unit, bool skipQueue) {
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
							this.PathUnits.m_buffer[unit].m_nextPathUnit = this.QueueFirst;
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
							this.PathUnits.m_buffer[this.QueueLast].m_nextPathUnit = unit;
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
					pathUnitExtVehicleType[unit] = vehicleType;
					pathUnitVehicleIds[unit] = vehicleId;
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
					Log.Error($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.CalculatePath({vehicleType}, {vehicleId}, {unit}, {skipQueue}): Error: {e.ToString()}");
				} finally {
					Monitor.Exit(this.QueueLock);
				}
				return true;
			}
			return false;
		}

		// PathFind
		protected void PathFindImplementation(uint unit, ref PathUnit data) {
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
			this._extVehicleType = pathUnitExtVehicleType[unit];
			this._vehicleId = pathUnitVehicleIds[unit];
#if VEHICLERAND
			this._vehicleRand = this._vehicleId == null ? 0f : Math.Min(1f, (float)(_vehicleId % 101) * 0.01f);
#endif
			this._extPublicTransport = _extVehicleType != null && (_extVehicleType & ExtVehicleType.PublicTransport) != ExtVehicleType.None;
#if DEBUGPF
			//Log._Debug($"CustomPathFind.PathFindImplementation: path unit {unit}, type {_extVehicleType}");
#endif
			if ((byte)(this._laneTypes & NetInfo.LaneType.Vehicle) != 0) {
				this._laneTypes |= NetInfo.LaneType.TransportVehicle;
			}
			int num = (int)(this.PathUnits.m_buffer[unit].m_positionCount & 15);
			int num2 = this.PathUnits.m_buffer[unit].m_positionCount >> 4;
			BufferItem bufferItemStartA;
			if (data.m_position00.m_segment != 0 && num >= 1) {
				this._startLaneA = PathManager.GetLaneID(data.m_position00);
				this._startOffsetA = data.m_position00.m_offset;
				bufferItemStartA.m_laneID = this._startLaneA;
				bufferItemStartA.m_position = data.m_position00;
				this.GetLaneDirection(data.m_position00, out bufferItemStartA.m_direction, out bufferItemStartA.m_lanesUsed);
				bufferItemStartA.m_comparisonValue = 0f;
				bufferItemStartA.m_numSegmentsToJunction = 0;
			} else {
				this._startLaneA = 0u;
				this._startOffsetA = 0;
				bufferItemStartA = default(BufferItem);
			}
			BufferItem bufferItemStartB;
			if (data.m_position02.m_segment != 0 && num >= 3) {
				this._startLaneB = PathManager.GetLaneID(data.m_position02);
				this._startOffsetB = data.m_position02.m_offset;
				bufferItemStartB.m_laneID = this._startLaneB;
				bufferItemStartB.m_position = data.m_position02;
				this.GetLaneDirection(data.m_position02, out bufferItemStartB.m_direction, out bufferItemStartB.m_lanesUsed);
				bufferItemStartB.m_comparisonValue = 0f;
				bufferItemStartB.m_numSegmentsToJunction = 0;
			} else {
				this._startLaneB = 0u;
				this._startOffsetB = 0;
				bufferItemStartB = default(BufferItem);
			}
			BufferItem bufferItemEndA;
			if (data.m_position01.m_segment != 0 && num >= 2) {
				this._endLaneA = PathManager.GetLaneID(data.m_position01);
				bufferItemEndA.m_laneID = this._endLaneA;
				bufferItemEndA.m_position = data.m_position01;
				this.GetLaneDirection(data.m_position01, out bufferItemEndA.m_direction, out bufferItemEndA.m_lanesUsed);
				bufferItemEndA.m_methodDistance = 0.01f;
				bufferItemEndA.m_comparisonValue = 0f;
				bufferItemEndA.m_numSegmentsToJunction = 0;
			} else {
				this._endLaneA = 0u;
				bufferItemEndA = default(BufferItem);
			}
			BufferItem bufferItemEndB;
			if (data.m_position03.m_segment != 0 && num >= 4) {
				this._endLaneB = PathManager.GetLaneID(data.m_position03);
				bufferItemEndB.m_laneID = this._endLaneB;
				bufferItemEndB.m_position = data.m_position03;
				this.GetLaneDirection(data.m_position03, out bufferItemEndB.m_direction, out bufferItemEndB.m_lanesUsed);
				bufferItemEndB.m_methodDistance = 0.01f;
				bufferItemEndB.m_comparisonValue = 0f;
				bufferItemEndB.m_numSegmentsToJunction = 0;
			} else {
				this._endLaneB = 0u;
				bufferItemEndB = default(BufferItem);
			}
			if (data.m_position11.m_segment != 0 && num2 >= 1) {
				this._vehicleLane = PathManager.GetLaneID(data.m_position11);
				this._vehicleOffset = data.m_position11.m_offset;
			} else {
				this._vehicleLane = 0u;
				this._vehicleOffset = 0;
			}
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
				/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
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
						this.ProcessItemMain(unit, candidateItem, startNode, ref instance.m_nodes.m_buffer[startNode], 0, false);
					}

					if ((byte)(candidateItem.m_direction & NetInfo.Direction.Backward) != 0) {
						ushort endNode = instance.m_segments.m_buffer[candidateItem.m_position.m_segment].m_endNode;
						this.ProcessItemMain(unit, candidateItem, endNode, ref instance.m_nodes.m_buffer[endNode], 255, false);
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
								this.ProcessItemMain(unit, candidateItem, specialNodeId, ref instance.m_nodes.m_buffer[specialNodeId], laneOffset, true);
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

			/*if (m_queuedPathFindCount > 100 && Options.disableSomething0)
				Log.Message($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: MAIN LOOP FINISHED! bufferMinPos: {this._bufferMinPos}, bufferMaxPos: {this._bufferMaxPos}, startA: {bufferItemStartA.m_position.m_segment}, startB: {bufferItemStartB.m_position.m_segment}, endA: {bufferItemEndA.m_position.m_segment}, endB: {bufferItemEndB.m_position.m_segment}");*/
			if (!canFindPath) {
				// we could not find a path
				PathUnits.m_buffer[(int)unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;
#if DEBUG
				++_failedPathFinds;
				//Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Cannot find path (pfCurrentState={pfCurrentState}) for unit {unit}");
#endif
				pathUnitExtVehicleType[unit] = null;
				pathUnitVehicleIds[unit] = null;

				return;
			}
			// we could calculate a valid path

			float totalPathLength = finalBufferItem.m_comparisonValue * this._maxLength;
			this.PathUnits.m_buffer[unit].m_length = totalPathLength;
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
#endif
					pathUnitExtVehicleType[unit] = null;
					pathUnitVehicleIds[unit] = null;

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
							//Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Cannot find path (pfCurrentState={pfCurrentState}) for unit {unit}");
#endif
							pathUnitExtVehicleType[unit] = null;
							pathUnitVehicleIds[unit] = null;
							return;
						}
						this.PathUnits.m_buffer[createdPathUnitId] = this.PathUnits.m_buffer[(int)currentPathUnitId];
						this.PathUnits.m_buffer[createdPathUnitId].m_referenceCount = 1;
						this.PathUnits.m_buffer[createdPathUnitId].m_pathFindFlags = PathUnit.FLAG_READY;
						this.PathUnits.m_buffer[currentPathUnitId].m_nextPathUnit = createdPathUnitId;
						this.PathUnits.m_buffer[currentPathUnitId].m_positionCount = (byte)currentItemPositionCount;
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
				if (!Options.isStockLaneChangerUsed()) {
					NetInfo.Lane laneInfo = Singleton<NetManager>.instance.m_segments.m_buffer[currentPosition.m_segment].Info.m_lanes[currentPosition.m_lane];
					if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None)
						CustomRoadAI.AddTraffic(currentPosition.m_segment, currentPosition.m_lane, (ushort)(this._isHeavyVehicle || _extVehicleType == ExtVehicleType.Bus ? 50 : 25), null);
				}
				// NON-STOCK CODE END
				currentPosition = this._laneTarget[laneID];
			}
			PathUnits.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;
#if DEBUG
			++_failedPathFinds;
#endif
			pathUnitExtVehicleType[unit] = null;
			pathUnitVehicleIds[unit] = null;
#if DEBUG
			//Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Cannot find path (pfCurrentState={pfCurrentState}) for unit {unit}");
#endif
		}
#endregion

		// be aware:
		//   (1) path-finding works from target to start. the "next" segment is always the previous and the "previous" segment is always the next segment on the path!
		//   (2) when I use the term "lane index from outer" this means outer right lane for right-hand traffic systems and outer-left lane for left-hand traffic systems.

		// 1
		private void ProcessItemMain(uint unitId, BufferItem item, ushort nextNodeId, ref NetNode nextNode, byte connectOffset, bool isMiddle) {
#if DEBUGPF
			//bool debug = Options.disableSomething1 && item.m_position.m_segment == 1459 && nextNodeId == 19630;
			//bool debug = Options.disableSomething1 && (item.m_position.m_segment == 3833 || item.m_position.m_segment == 9649);
			bool debug = Options.disableSomething1 && nextNodeId == 2790 && item.m_position.m_segment == 36426;
			//bool debug = Options.disableSomething1 && ((nextNodeId == 27237 && item.m_position.m_segment == 5699) || (nextNodeId == 16068 && item.m_position.m_segment == 24398) || (nextNodeId == 12825 && item.m_position.m_segment == 17008));
#endif
#if DEBUGPF
			/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
				Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: processItemMain RUNNING! item: {item.m_position.m_segment}, {item.m_position.m_lane} nextNodeId: {nextNodeId}");*/
#endif
			//Log.Message($"THREAD #{Thread.CurrentThread.ManagedThreadId} Path finder: " + this._pathFindIndex + " vehicle types: " + this._vehicleTypes);
#if DEBUGPF
			//bool debug = isTransportVehicle && isMiddle && item.m_position.m_segment == 13550;
			List<String> logBuf = null;
			if (debug)
				logBuf = new List<String>();
			//bool debug = nextNodeId == 12732;
#else
			bool debug = false;
#endif

#if DEBUGPF2
			bool debug2 = debug; //Options.disableSomething1 && _extVehicleType == ExtVehicleType.Bicycle;
			List<String> logBuf2 = null;
			if (debug2)
				logBuf2 = new List<String>();
#endif
			NetManager netManager = Singleton<NetManager>.instance;
			bool prevIsPedestrianLane = false;
			//bool prevIsBusLane = false; // non-stock
			bool prevIsBicycleLane = false;
			bool prevIsCenterPlatform = false;
			int similarLaneIndexFromInner = 0; // similar index, starting with 0 at leftmost lane
			NetInfo prevSegmentInfo = netManager.m_segments.m_buffer[(int)item.m_position.m_segment].Info;
			byte prevSimilarLaneCount = 0;
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane prevLane = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				prevIsPedestrianLane = (prevLane.m_laneType == NetInfo.LaneType.Pedestrian);
				prevIsBicycleLane = (prevLane.m_laneType == NetInfo.LaneType.Vehicle && (prevLane.m_vehicleType & this._vehicleTypes) == VehicleInfo.VehicleType.Bicycle);
				//prevIsBusLane = (prevLane.m_laneType == NetInfo.LaneType.TransportVehicle && (prevLane.m_vehicleType & this._vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None);
				prevIsCenterPlatform = prevLane.m_centerPlatform;
				prevSimilarLaneCount = (byte)prevLane.m_similarLaneCount;
				if ((byte)(prevLane.m_finalDirection & NetInfo.Direction.Forward) != 0) {
					similarLaneIndexFromInner = prevLane.m_similarLaneIndex;
				} else {
					similarLaneIndexFromInner = prevLane.m_similarLaneCount - prevLane.m_similarLaneIndex - 1;
				}
			}
			int firstSimilarLaneIndexFromInner = similarLaneIndexFromInner;
			ushort prevSegmentId = item.m_position.m_segment;
			if (isMiddle) {
				for (int i = 0; i < 8; ++i) {
					ushort nextSegmentId = nextNode.GetSegment(i);
					if (nextSegmentId <= 0)
						continue;

#if DEBUGPF
					if (debug) {
						FlushMainLog(logBuf, unitId);
					}
#endif

					this.ProcessItemCosts(false, false, debug, item, nextNodeId, nextSegmentId, ref netManager.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromInner, connectOffset, !prevIsPedestrianLane, prevIsPedestrianLane);
				}
			} else if (prevIsPedestrianLane) {
				int prevLaneIndex = (int)item.m_position.m_lane;
				if (nextNode.Info.m_class.m_service != ItemClass.Service.Beautification) {
					bool isEndBendOrJunction = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None;
					bool isOnCenterPlatform = prevIsCenterPlatform && (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Junction)) == NetNode.Flags.None;
					ushort nextLeftSegment = prevSegmentId;
					ushort nextRightSegment = prevSegmentId;
					int leftLaneIndex;
					int rightLaneIndex;
					uint leftLaneId;
					uint rightLaneId;
					netManager.m_segments.m_buffer[(int)prevSegmentId].GetLeftAndRightLanes(nextNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, prevLaneIndex, isOnCenterPlatform, out leftLaneIndex, out rightLaneIndex, out leftLaneId, out rightLaneId);
					if (leftLaneId == 0u || rightLaneId == 0u) {
						ushort leftSegment;
						ushort rightSegment;
						netManager.m_segments.m_buffer[(int)prevSegmentId].GetLeftAndRightSegments(nextNodeId, out leftSegment, out rightSegment);
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
						this.ProcessItemPedBicycle(item, nextNodeId, nextLeftSegment, ref netManager.m_segments.m_buffer[(int)nextLeftSegment], connectOffset, connectOffset, leftLaneIndex, leftLaneId); // ped
					}
					if (rightLaneId != 0u && rightLaneId != leftLaneId && (nextRightSegment != prevSegmentId || isEndBendOrJunction || isOnCenterPlatform)) {
#if DEBUGPF2
						if (debug2)
							logBuf2.Add($"Exploring path! Segment {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset} @ node {nextNodeId}: going ped2, seg. {nextRightSegment}, off {connectOffset}, lane idx {rightLaneIndex}, id {rightLaneId}");
#endif
						this.ProcessItemPedBicycle(item, nextNodeId, nextRightSegment, ref netManager.m_segments.m_buffer[(int)nextRightSegment], connectOffset, connectOffset, rightLaneIndex, rightLaneId); // ped
					}
					int nextLaneIndex;
					uint nextLaneId;
					if ((this._vehicleTypes & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None &&
						netManager.m_segments.m_buffer[(int)prevSegmentId].GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Bicycle, out nextLaneIndex, out nextLaneId)) {
#if DEBUGPF2
						if (debug2)
							logBuf2.Add($"Exploring path! Segment {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset} @ node {nextNodeId}: going bike, seg. {prevSegmentId}, off {connectOffset}, lane idx {nextLaneIndex}, id {nextLaneId}");
#endif
						this.ProcessItemPedBicycle(item, nextNodeId, prevSegmentId, ref netManager.m_segments.m_buffer[(int)prevSegmentId], connectOffset, connectOffset, nextLaneIndex, nextLaneId); // bicycle
					}
				} else {
					//mCurrentState = 9;
					for (int j = 0; j < 8; ++j) {
						ushort nextSegmentId = nextNode.GetSegment(j);
						if (nextSegmentId != 0 && nextSegmentId != prevSegmentId) {
#if DEBUGPF2
							if (debug2)
								logBuf2.Add($"Exploring path! Segment {item.m_position.m_segment} @ node {nextNodeId}: going beauty1, seg. {nextSegmentId}, off {connectOffset}");
#endif
#if DEBUGPF
							if (debug) {
								FlushMainLog(logBuf, unitId);
							}
#endif

							this.ProcessItemCosts(false, false, debug, item, nextNodeId, nextSegmentId, ref netManager.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromInner, connectOffset, false, true);
						}
					}
				}
				NetInfo.LaneType laneType = this._laneTypes & ~NetInfo.LaneType.Pedestrian;
				VehicleInfo.VehicleType vehicleType = this._vehicleTypes & ~VehicleInfo.VehicleType.Bicycle;
				if ((byte)(item.m_lanesUsed & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
					laneType &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
				int nextLaneIndex2;
				uint nextlaneId2;
				if (laneType != NetInfo.LaneType.None &&
					vehicleType != VehicleInfo.VehicleType.None &&
					netManager.m_segments.m_buffer[(int)prevSegmentId].GetClosestLane(prevLaneIndex, laneType, vehicleType, out nextLaneIndex2, out nextlaneId2)) {
					NetInfo.Lane lane5 = prevSegmentInfo.m_lanes[nextLaneIndex2];
					byte connectOffset2;
					if ((netManager.m_segments.m_buffer[(int)prevSegmentId].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None == ((byte)(lane5.m_finalDirection & NetInfo.Direction.Backward) != 0)) {
						connectOffset2 = 1;
					} else {
						connectOffset2 = 254;
					}
#if DEBUGPF2
					if (debug2)
						logBuf2.Add($"Exploring path! Segment {item.m_position.m_segment} @ node {nextNodeId}: going beauty2, seg. {prevSegmentId}, off {connectOffset}, lane idx {nextLaneIndex2}, id {nextlaneId2}");
#endif
					CustomPathFind.BufferItem item2 = item;
					if (this._randomParking) {
						item2.m_comparisonValue += (float)this._pathRandomizer.Int32(300u) / this._maxLength;
					}
					this.ProcessItemPedBicycle(item2, nextNodeId, prevSegmentId, ref netManager.m_segments.m_buffer[(int)prevSegmentId], connectOffset2, 128, nextLaneIndex2, nextlaneId2); // ped
				}
			} else {
				bool mayTurnAround = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
				bool allowPedSwitch = (byte)(this._laneTypes & NetInfo.LaneType.Pedestrian) != 0; // allow pedestrian switching to vehicle?
				bool nextIsBeautificationNode = false;
				byte nextConnectOffset = 0;
				if (allowPedSwitch) {
					if (prevIsBicycleLane) {
						nextConnectOffset = connectOffset;
						nextIsBeautificationNode = (nextNode.Info.m_class.m_service == ItemClass.Service.Beautification);
					} else if (this._vehicleLane != 0u) {
						// there is a parked vehicle position
						if (this._vehicleLane != item.m_laneID) {
							// we have not reached the parked vehicle yet
							allowPedSwitch = false;
						} else {
							// pedestrian switches to parked vehicle
							nextConnectOffset = this._vehicleOffset;
						}
					} else if (this._stablePath) {
						// enter a bus
						nextConnectOffset = 128;
					} else {
						// pocket car spawning
						if (Options.prohibitPocketCars) {
							allowPedSwitch = false;
						} else {
							nextConnectOffset = (byte)this._pathRandomizer.UInt32(1u, 254u);
						}
					}
				}

				// NON-STOCK CODE START //
#if DEBUGPF
				if (debug)
					logBuf.Add($"Exploring path! Segment {item.m_position.m_segment} @ node {nextNodeId}: Preparation started");
#endif

				CustomPathManager pathManager = Singleton<CustomPathManager>.instance;
				bool nextIsJunction = (nextNode.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
				bool nextIsTransition = (nextNode.m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;
				bool nextIsStartNodeOfPrevSegment = netManager.m_segments.m_buffer[(int)prevSegmentId].m_startNode == nextNodeId;
				//NetInfo.Direction normDirection = NetInfo.Direction.Backward;// TrafficPriority.IsLeftHandDrive() ? NetInfo.Direction.Forward : NetInfo.Direction.Backward; // direction to normalize indices to

				bool isStrictLaneArrowPolicyEnabled = 
					(_extVehicleType != ExtVehicleType.Emergency || Options.disableSomething4) &&
					(nextIsJunction || nextIsTransition) &&
					//!Options.allRelaxed &&
					!(Options.allRelaxed || (Options.relaxedBusses && _extVehicleType == ExtVehicleType.Bus)) &&
					(this._vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None;

				// get segment geometry
				//SegmentGeometry geometry = IsMasterPathFind ? SegmentGeometry.Get(prevSegmentId, nextNodeId) : SegmentGeometry.Get(prevSegmentId);
				SegmentGeometry prevGeometry = SegmentGeometry.Get(prevSegmentId);
				bool nextIsRealJunction = prevGeometry.CountOtherSegments(nextIsStartNodeOfPrevSegment) > 1;
				bool prevIsOutgoingOneWay = prevGeometry.IsOutgoingOneWay(nextIsStartNodeOfPrevSegment);
				bool prevIsHighway = prevGeometry.IsHighway();
				bool prevHasBusLane = prevGeometry.HasBusLane();
				bool nextAreOnlyOneWayHighways = prevGeometry.HasOnlyHighways(nextIsStartNodeOfPrevSegment);

				short prevOuterSimilarLaneIndex;
				short prevInnerSimilarLaneIndex;
				NetInfo.Lane prevLane = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				if ((byte)(prevLane.m_direction & NetInfo.Direction.Forward) != 0) {
					prevOuterSimilarLaneIndex = (short)(prevLane.m_similarLaneCount - prevLane.m_similarLaneIndex - 1);
					prevInnerSimilarLaneIndex = (short)prevLane.m_similarLaneIndex;
				} else {
					prevOuterSimilarLaneIndex = (short)prevLane.m_similarLaneIndex;
					prevInnerSimilarLaneIndex = (short)(prevLane.m_similarLaneCount - prevLane.m_similarLaneIndex - 1);
				}

				bool foundForced = false;
				int totalIncomingLanes = 0; // running number of next incoming lanes (number is updated at each segment iteration)
				int totalOutgoingLanes = 0; // running number of next outgoing lanes (number is updated at each segment iteration)

				ushort[] incomingStraightSegments = null; // ids of incoming straight segments
				ushort[] incomingRightSegments = null; // ids of incoming right segments
				ushort[] incomingLeftSegments = null; // ids of incoming left segments
				if (isStrictLaneArrowPolicyEnabled) {
					incomingStraightSegments = prevGeometry.GetIncomingStraightSegments(nextIsStartNodeOfPrevSegment);
					incomingLeftSegments = prevGeometry.GetIncomingLeftSegments(nextIsStartNodeOfPrevSegment);
					incomingRightSegments = prevGeometry.GetIncomingRightSegments(nextIsStartNodeOfPrevSegment);
				}

				// determine if we should explore the previous segment (for u-turns)
				bool explorePrevSegment = Flags.getUTurnAllowed(prevSegmentId, nextIsStartNodeOfPrevSegment) &&
					!Options.isStockLaneChangerUsed() &&
					nextIsJunction &&
					!prevIsHighway &&
					!prevIsOutgoingOneWay &&
					(_extVehicleType != null &&
					(_extVehicleType & ExtVehicleType.RoadVehicle) != ExtVehicleType.None);

				bool nextIsSimpleJunction = false;
				if (Options.highwayRules && (netManager.m_nodes.m_buffer[nextNodeId].m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) {
					// determine if junction is a simple junction (highway rules only apply to simple junctions)
					nextIsSimpleJunction = NodeGeometry.Get(nextNodeId).IsSimpleJunction;
				}

				bool applyHighwayRules = Options.highwayRules && nextIsSimpleJunction && nextAreOnlyOneWayHighways && prevIsOutgoingOneWay && prevIsHighway;
				bool applyHighwayRulesAtJunction = applyHighwayRules && nextIsRealJunction;

				ushort nextSegmentId;
				if (explorePrevSegment) {
					nextSegmentId = prevSegmentId;
				} else {
					if (TrafficPriorityManager.IsLeftHandDrive()) {
						nextSegmentId = netManager.m_segments.m_buffer[prevSegmentId].GetLeftSegment(nextNodeId);
					} else {
						nextSegmentId = netManager.m_segments.m_buffer[prevSegmentId].GetRightSegment(nextNodeId);
					}
				}
#if DEBUGPF
				if (debug)
					logBuf.Add($"Exploring path! Segment {item.m_position.m_segment} @ node {nextNodeId}: Preparation ended. nextIsSimpleJunction={nextIsSimpleJunction}");
#endif

#if DEBUGPF
				if (debug)
					logBuf.Add($"pathfind @ node {nextNodeId}: Path from {nextSegmentId} to {prevSegmentId}.");
#endif
#if DEBUGPF
				if (debug)
					logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer: Custom part started for vehicle type {_extVehicleType}");
#endif
				// NON-STOCK CODE END //
				for (int k = 0; k < 8; ++k) {
#if DEBUGPF
					if (debug)
						logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer: Segment Iteration {k}. nextSegmentId={nextSegmentId}");
#endif
					// NON-STOCK CODE START //
					int outgoingVehicleLanes = 0;
					int incomingVehicleLanes = 0;
#if DEBUGPF
					bool couldFindCustomPath = false;
#endif

					if (nextSegmentId == 0) {
						break;
					}

					bool uturn = nextSegmentId == prevSegmentId;
					if (!explorePrevSegment && uturn) {
						break;
					}

					NetInfo nextSegmentInfo = netManager.m_segments.m_buffer[nextSegmentId].Info;
					bool nextIsUntouchable = (netManager.m_segments.m_buffer[nextSegmentId].m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None;
					// a simple transition is (1) no junction, (2) no transition and (3) where lane count is equal
					bool nextIsSimpleTransition = !nextIsTransition && !nextIsRealJunction && nextSegmentInfo.m_lanes.Length == prevSegmentInfo.m_lanes.Length;

					if (!isStrictLaneArrowPolicyEnabled || nextIsUntouchable) {
						// don't obey lane arrows

#if DEBUGPF
						if (debug)
							logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer: strict lane arrow policy disabled. ({nextIsJunction} || {nextIsTransition}) && !({Options.allRelaxed} || ({Options.relaxedBusses} && {_extVehicleType == ExtVehicleType.Bus})) && {(this._vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None}");
#endif

#if DEBUGPF
						if (debug) {
							FlushMainLog(logBuf, unitId);
						}
#endif

						// NON-STOCK CODE END //
						if (ProcessItemCosts(nextIsSimpleTransition || uturn, true, debug, item, nextNodeId, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromInner, connectOffset, true, nextIsBeautificationNode)) {
							mayTurnAround = true;
						}
						// NON-STOCK CODE START //
#if DEBUGPF
						couldFindCustomPath = true; // not of interest
#endif
					} else if (!nextIsBeautificationNode) {
						if ((_vehicleTypes & ~VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
							// handle non-car paths
#if DEBUGPF
							if (debug)
								logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer: Handling everything that is not a car: {this._vehicleTypes}");
#endif

							_vehicleTypes &= ~VehicleInfo.VehicleType.Car;

#if DEBUGPF
							if (debug) {
								FlushMainLog(logBuf, unitId);
							}
#endif

							if (ProcessItemCosts(false, false, debug, item, nextNodeId, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromInner, connectOffset, true, nextIsBeautificationNode)) {
								mayTurnAround = true;
							}
							_vehicleTypes |= VehicleInfo.VehicleType.Car;
						}
#if DEBUGPF
						if (debug)
							logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer: !enablePedestrian");
#endif

						bool isIncomingRight = false;
						bool isIncomingStraight = false;
						bool isIncomingLeft = false;
						bool isIncomingTurn = false;
						bool isValid = true;

						SegmentGeometry nextGeometry = SegmentGeometry.Get(nextSegmentId);
						bool nextIsHighway = nextGeometry.IsHighway();
						bool nextIsStartNodeOfNextSegment = netManager.m_segments.m_buffer[nextSegmentId].m_startNode == nextNodeId;

						if (nextSegmentId != prevSegmentId) {
							for (int j = 0; j < incomingStraightSegments.Length; ++j) {
								if (incomingStraightSegments[j] == nextSegmentId)
									isIncomingStraight = true;
							}

							if (! isIncomingStraight) {
								for (int j = 0; j < incomingRightSegments.Length; ++j) {
									if (incomingRightSegments[j] == nextSegmentId)
										isIncomingRight = true;
								}

								if (! isIncomingRight) {
									for (int j = 0; j < incomingLeftSegments.Length; ++j) {
										if (incomingLeftSegments[j] == nextSegmentId)
											isIncomingLeft = true;
									}

									if (! isIncomingLeft)
										isValid = false;
								}
							}
						} else {
							isIncomingTurn = true;
						}

						// we need outgoing lanes too!
						if (!isValid) {
							// recalculate geometry if segment is unknown

							if (!applyHighwayRulesAtJunction) {
#if DEBUGPF
								couldFindCustomPath = true; // not of interest
#endif
								goto nextIter;
							} else {
								// we do not stop here because we need the number of outgoing lanes in highway mode
							}
						}

						NetInfo.LaneType drivingEnabledLaneTypes = this._laneTypes;
						drivingEnabledLaneTypes &= ~NetInfo.LaneType.Pedestrian;
						drivingEnabledLaneTypes &= ~NetInfo.LaneType.Parking;

						NetInfo.Direction nextDir = nextIsStartNodeOfNextSegment ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;
						NetInfo.Direction nextDir2 = ((netManager.m_segments.m_buffer[nextSegmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? nextDir : NetInfo.InvertDirection(nextDir);

						byte curLaneI = 0; // current array index
						uint curLaneId = netManager.m_segments.m_buffer[nextSegmentId].m_lanes;
						byte laneIndex = 0;
						compatibleOuterSimilarIndexesMask = 0; // holds a bitmask that indicates which elements in `laneIndexByOuterSimilarIndex` are valid for the current run
						bool hasLaneConnections = false; // true if any lanes are connected by the lane connection tool

						while (laneIndex < nextSegmentInfo.m_lanes.Length && curLaneId != 0u) {

							// determine valid lanes based on lane arrows
							NetInfo.Lane nextLane = nextSegmentInfo.m_lanes[laneIndex];

							bool nextHasOutgoingConnections = false;
							bool nextIsConnectedWithPrev = true;
							if (Options.laneConnectorEnabled) {
								nextHasOutgoingConnections = laneConnManager.HasConnections(curLaneId, nextIsStartNodeOfNextSegment);
								if (nextHasOutgoingConnections) {
									hasLaneConnections = true;
									nextIsConnectedWithPrev = laneConnManager.AreLanesConnected(curLaneId, item.m_laneID, nextIsStartNodeOfNextSegment);
								}
#if DEBUGPF
								if (debug) {
									logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment} (lane {item.m_laneID}), lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer: Lane Iteration {laneIndex}. nextSegmentId={nextSegmentId}, curLaneId={curLaneId}, nextHasOutgoingConnections={nextHasOutgoingConnections}, nextIsConnectedWithPrev={nextIsConnectedWithPrev}");
								}
#endif
							}
#if DEBUGPF
							if (debug)
								logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer: Lane Iteration {laneIndex}. nextSegmentId={nextSegmentId}, curLaneId={curLaneId} nextIsIncomingLane={(byte)(nextLane.m_finalDirection & nextDir2) != 0} nextIsCompatibleLane={nextLane.CheckType(drivingEnabledLaneTypes, _vehicleTypes)}");
#endif

							if (nextLane.CheckType(drivingEnabledLaneTypes, _vehicleTypes)) { // is compatible lane
								if ((byte)(nextLane.m_finalDirection & nextDir2) != 0) { // is incoming lane
									++incomingVehicleLanes;
#if DEBUGPF
									if (debug)
										logBuf.Add($"Segment {nextSegmentId}, lane {curLaneId}, {laneIndex} is compatible (prevSegment: {prevSegmentId}). laneTypes: {_laneTypes.ToString()}, vehicleTypes: {_vehicleTypes.ToString()}, incomingLanes={incomingVehicleLanes}, isIncomingRight? {isIncomingRight}, isIncomingLeft? {isIncomingLeft}, isIncomingStraight? {isIncomingStraight}");
#endif

									// calculate current similar lane index starting from outer lane
									byte nextOuterSimilarLaneIndex;
									byte nextInnerSimilarLaneIndex;
									if ((byte)(nextLane.m_direction & NetInfo.Direction.Forward) != 0) {
										nextOuterSimilarLaneIndex = (byte)(nextLane.m_similarLaneCount - nextLane.m_similarLaneIndex - 1);
										nextInnerSimilarLaneIndex = (byte)nextLane.m_similarLaneIndex;
									} else {
										nextOuterSimilarLaneIndex = (byte)nextLane.m_similarLaneIndex;
										nextInnerSimilarLaneIndex = (byte)(nextLane.m_similarLaneCount - nextLane.m_similarLaneIndex - 1);
									}

									/*bool nextIsBusLane = (nextLane.m_laneType == NetInfo.LaneType.TransportVehicle && (nextLane.m_vehicleType & this._vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None);
									if (nextIsBusLane && (Options.relaxedBusses && _extVehicleType == ExtVehicleType.Bus)) {
										// busses may ignore lane arrows at the next lane
										if (ProcessItemCosts(true, true, debug, item, nextNodeId, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromInner, connectOffset, true, false, laneIndex, curLaneId, out foundForced)) {
											mayTurnAround = true;
										}
									} else {*/
										bool hasLeftArrow = ((NetLane.Flags)netManager.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.Left) == NetLane.Flags.Left;
										bool hasRightArrow = ((NetLane.Flags)netManager.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.Right) == NetLane.Flags.Right;
										bool hasForwardArrow = ((NetLane.Flags)netManager.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.Forward) != NetLane.Flags.None || ((NetLane.Flags)netManager.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.LeftForwardRight) == NetLane.Flags.None;
#if DEBUGPF
										if (debug) {
											if (hasLeftArrow) {
												logBuf.Add($"Segment {nextSegmentId}, lane {curLaneId}, {laneIndex} has LEFT arrow. isIncomingRight? {isIncomingRight}, isIncomingLeft? {isIncomingLeft}, isIncomingStraight? {isIncomingStraight}");
											}

											if (hasRightArrow) {
												logBuf.Add($"Segment {nextSegmentId}, lane {curLaneId}, {laneIndex} has RIGHT arrow. isIncomingRight? {isIncomingRight}, isIncomingLeft? {isIncomingLeft}, isIncomingStraight? {isIncomingStraight}");
											}

											if (hasForwardArrow) {
												logBuf.Add($"Segment {nextSegmentId}, lane {curLaneId}, {laneIndex} has FORWARD arrow. isIncomingRight? {isIncomingRight}, isIncomingLeft? {isIncomingLeft}, isIncomingStraight? {isIncomingStraight}");
											}
										}
#endif

										/*bool isValidIncomingRight = isIncomingRight && hasLeftArrow;
										bool isValidIncomingLeft = isIncomingLeft && hasRightArrow;
										bool isValidIncomingStraight = isIncomingStraight && hasForwardArrow;
										bool isValidIncomingTurn = isIncomingTurn && ((TrafficPriority.IsLeftHandDrive() && hasRightArrow) || (!TrafficPriority.IsLeftHandDrive() && hasLeftArrow));*/

#if DEBUGPF
										/*if (debug)
											logBuf.Add($"Segment {nextSegmentId}, lane {curLaneId}, {laneIndex}. isValidIncomingRight? {isValidIncomingRight}, isValidIncomingLeft? {isValidIncomingLeft}, isValidIncomingStraight? {isValidIncomingStraight} isValidIncomingTurn? {isValidIncomingTurn}");*/
#endif

										// add valid next lanes
										if (
												(nextHasOutgoingConnections && nextIsConnectedWithPrev) || // lanes are connected
												applyHighwayRules || // highway rules enabled
												(isIncomingRight && hasLeftArrow) || // valid incoming right
												(isIncomingLeft && hasRightArrow) || // valid incoming left
												(isIncomingStraight && hasForwardArrow) || // valid incoming straight
												(isIncomingTurn && ((TrafficPriorityManager.IsLeftHandDrive() && hasRightArrow) || (!TrafficPriorityManager.IsLeftHandDrive() && hasLeftArrow))) // valid turning lane
											) { // valid incoming turn

											laneIndexes[curLaneI] = laneIndex;
											laneIds[curLaneI] = curLaneId;
											nextOuterLaneSimilarIndexes[curLaneI] = nextOuterSimilarLaneIndex;
											nextInnerLaneSimilarIndexes[curLaneI] = nextInnerSimilarLaneIndex;
											laneIndexByOuterSimilarIndex[nextOuterSimilarLaneIndex] = curLaneI;
											hasOutgoingConnections[curLaneI] = nextHasOutgoingConnections;
											nextIsConnectedWithPrevious[curLaneI] = nextIsConnectedWithPrev;
											compatibleOuterSimilarIndexesMask |= POW2MASKS[nextOuterSimilarLaneIndex];
#if DEBUGPF
											if (debug)
												logBuf.Add($"Adding lane #{curLaneI} (id {curLaneId}, idx {laneIndex}), outer sim. idx: {nextOuterSimilarLaneIndex}, inner sim. idx.: {nextInnerSimilarLaneIndex}");
#endif
											curLaneI++;
										}
									//}
								} else {
									++outgoingVehicleLanes;
								}
							}

							curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
							++laneIndex;
						} // foreach lane

						if (curLaneI > 0) {
							// we found compatible lanes
							byte nextLaneIndex = 0;
							uint nextLaneId = 0u;
							short nextLaneI = -1;
							byte nextCompatibleLaneCount = curLaneI;

							// enable highway rules only at junctions or at simple lane merging/splitting points
							short laneDiff = (short)((short)nextCompatibleLaneCount - (short)prevSimilarLaneCount);
							bool applyHighwayRulesAtSegment = applyHighwayRules && (nextIsRealJunction || Math.Abs(laneDiff) == 1);

#if DEBUGPF
							if (debug) {
								logBuf.Add($"Compatible lanes found.");
								logBuf.Add($"next segment: {nextSegmentId}, number of next lanes: {nextCompatibleLaneCount}, prev. segment: {prevSegmentId}, prev. lane ID: {item.m_laneID}, prev. lane idx: {item.m_position.m_lane}, prev. outer sim. idx: {prevOuterSimilarLaneIndex}, prev. inner sim. idx: {prevInnerSimilarLaneIndex}, laneTypes: {_laneTypes.ToString()}, vehicleTypes: {_vehicleTypes.ToString()}, incomingLanes={incomingVehicleLanes}, isIncomingRight? {isIncomingRight}, isIncomingLeft? {isIncomingLeft}, isIncomingStraight? {isIncomingStraight}");
							}
#endif

							// mix of incoming/outgoing lanes on the right side of prev. segment is not allowed in highway mode
							/*if (totalIncomingLanes > 0 && totalOutgoingLanes > 0) {
								// TODO should never happen since `nextIsSimpleJunction` == true
#if DEBUGPF
								if (debug)
									logBuf.Add($"{totalIncomingLanes} incoming lanes and {totalOutgoingLanes} outgoing lanes found. Disabling highway rules.");
#endif
								applyHighwayRulesAtSegment = false;
							}*/

							if (!hasLaneConnections && applyHighwayRulesAtSegment) {
								// apply highway rules
#if DEBUGPF
								if (debug)
									logBuf.Add($"Applying highway rules. lanes found: {totalIncomingLanes} incoming, {totalOutgoingLanes} outgoing.");
#endif

								if (applyHighwayRulesAtJunction) {
									// we reached a highway junction where more than two segments are connected to each other

									int numLanesSeen = Math.Max(totalIncomingLanes, totalOutgoingLanes); // number of lanes that were processed in earlier segment iterations (either all incoming or all outgoing)
									int nextInnerSimilarIndex;

									if (totalOutgoingLanes > 0) {
										// lane splitting at junction
										nextInnerSimilarIndex = prevInnerSimilarLaneIndex + numLanesSeen;
#if DEBUGPF
										if (debug)
											logBuf.Add($"Performing lane split. nextInnerSimilarIndex={nextInnerSimilarIndex} = prevInnerSimilarLaneIndex({prevInnerSimilarLaneIndex}) + numLanesSeen({numLanesSeen})");
#endif
									} else {
										// lane merging at junction
										nextInnerSimilarIndex = prevInnerSimilarLaneIndex - numLanesSeen;
									}
#if DEBUGPF
									if (debug)
										logBuf.Add($"Performing lane merge. nextInnerSimilarIndex={nextInnerSimilarIndex} = prevInnerSimilarLaneIndex({prevInnerSimilarLaneIndex}) - numLanesSeen({numLanesSeen})");
#endif

									if (nextInnerSimilarIndex >= 0 && nextInnerSimilarIndex < nextCompatibleLaneCount) {
										// enough lanes available
										nextLaneI = FindValue(ref nextInnerLaneSimilarIndexes, nextInnerSimilarIndex, nextCompatibleLaneCount);// Convert.ToInt32(indexByInnerSimilarLaneIndex[nextInnerSimilarIndex]) - 1;
#if DEBUGPF
									if (debug)
										logBuf.Add($"Next lane within bounds. nextLaneI={nextLaneI}");
#endif
									} else {
										// Highway lanes "failed". Too few lanes at prevSegment or nextSegment.
										if (nextInnerSimilarIndex < 0) {
											// lane merging failed (too many incoming lanes)
											if (totalIncomingLanes >= prevSimilarLaneCount) {
												// there have already been explored more incoming lanes than outgoing lanes on the previous segment. Allow the current segment to also join the big merging party. What a fun!
												nextLaneI = FindValue(ref nextOuterLaneSimilarIndexes, prevOuterSimilarLaneIndex, nextCompatibleLaneCount);
											}
										} else {
											if (totalOutgoingLanes >= nextCompatibleLaneCount) {
												// there have already been explored more outgoing lanes than incoming lanes on the previous segment. Also allow vehicles to go to the current segment.
												nextLaneI = FindValue(ref nextOuterLaneSimilarIndexes, 0, nextCompatibleLaneCount);
											}
										}

										// If nextLaneI is still -1 here, then highways rules really cannot handle this situation (that's ok).
#if DEBUGPF
									if (debug)
										logBuf.Add($"Next lane out of bounds. nextLaneI={nextLaneI}, isIncomingLeft={isIncomingLeft}, prevOuterSimilarLaneIndex={prevOuterSimilarLaneIndex}, prevInnerSimilarLaneIndex={prevInnerSimilarLaneIndex}");
#endif
									}

									if (nextLaneI < 0 || nextLaneI >= nextCompatibleLaneCount) {
#if DEBUGPF
										if (debug)
											Log.Error($"(PFERR) Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer, {prevInnerSimilarLaneIndex} from inner: Highway lane selector cannot find suitable lane! isIncomingLeft={isIncomingLeft} isIncomingRight={isIncomingRight} totalIncomingLanes={totalIncomingLanes}");
										couldFindCustomPath = true; // not of interest for us
#endif
										goto nextIter; // no path to this lane
									}
								} else {
#if DEBUGPF
									if (debug)
										logBuf.Add($"Simple lane splitting/merging point @ highway rules! laneDiff={laneDiff}");
#endif

									// simple lane splitting/merging point. the number of lanes is guaranteed to differ by 1
									short minNextOuterSimilarIndex = -1;
									short maxNextOuterSimilarIndex = -1;

									if (laneDiff == 1) {
										// simple lane merge
										if (prevOuterSimilarLaneIndex == 0) {
											// merge outer lane
											minNextOuterSimilarIndex = 0;
											maxNextOuterSimilarIndex = 1;

#if DEBUGPF
											if (debug)
												logBuf.Add($"Simple lane splitting/merging point @ highway rules! Lane merge: Merging outer lane: {minNextOuterSimilarIndex} - {maxNextOuterSimilarIndex}");
#endif
										} else {
											// other lanes stay + 1
											minNextOuterSimilarIndex = maxNextOuterSimilarIndex = (short)(prevOuterSimilarLaneIndex + 1);

#if DEBUGPF
											if (debug)
												logBuf.Add($"Simple lane splitting/merging point @ highway rules! Lane merge: Other lanes stay + 1: {minNextOuterSimilarIndex} - {maxNextOuterSimilarIndex}");
#endif
										}
									} else { // diff == -1
										// simple lane split
										if (prevOuterSimilarLaneIndex <= 1) {
											// split outer lane
											minNextOuterSimilarIndex = maxNextOuterSimilarIndex = 0;

#if DEBUGPF
											if (debug)
												logBuf.Add($"Simple lane splitting/merging point @ highway rules! Lane split: Outer lane splits: {minNextOuterSimilarIndex} - {maxNextOuterSimilarIndex}");
#endif
										} else {
											// other lanes stay - 1
											minNextOuterSimilarIndex = maxNextOuterSimilarIndex = (short)(prevOuterSimilarLaneIndex - 1);
#if DEBUGPF
											if (debug)
												logBuf.Add($"Simple lane splitting/merging point @ highway rules! Lane split: Other lanes stay - 1: {minNextOuterSimilarIndex} - {maxNextOuterSimilarIndex}");
#endif
										}
									}


									// explore lanes
									for (short nextOuterSimilarIndex = minNextOuterSimilarIndex; nextOuterSimilarIndex <= maxNextOuterSimilarIndex; ++nextOuterSimilarIndex) {
#if DEBUGPF
										if (debug)
											logBuf.Add($"current outer similar index = {nextOuterSimilarIndex}, min. {minNextOuterSimilarIndex} max. {maxNextOuterSimilarIndex}");
#endif
										nextLaneI = FindCompatibleLane(ref laneIndexByOuterSimilarIndex, compatibleOuterSimilarIndexesMask, nextOuterSimilarIndex);

#if DEBUGPF
										if (debug)
											logBuf.Add($"(*) nextLaneI = {nextLaneI}");
#endif
										if (nextLaneI < 0) {
											continue;
										}

										// go to matched lane
										nextLaneIndex = laneIndexes[nextLaneI];
										nextLaneId = laneIds[nextLaneI];

#if DEBUGPF
										if (debug)
											logBuf.Add($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane idx {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer. There are {curLaneI} candidate lanes. We choose lane {nextLaneI} (index {nextLaneIndex}, {nextOuterSimilarIndex} compatible from outer). lhd: {TrafficPriorityManager.IsLeftHandDrive()}, ped: {pedestrianAllowed}, magical flag4: {mayTurnAround}");
#endif

#if DEBUGPF
										if (debug) {
											FlushMainLog(logBuf, unitId);
										}
#endif

										if (ProcessItemCosts(nextIsSimpleTransition || uturn, false, debug, item, nextNodeId, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromInner, connectOffset, true, nextIsBeautificationNode, nextLaneIndex, nextLaneId, out foundForced)) {
											mayTurnAround = true;
										}
#if DEBUGPF
										couldFindCustomPath = true;
#endif
									}

									goto nextIter;
								}
							} else {
								// city rules, multiple lanes or single target lane: lane matching
#if DEBUGPF
								if (debug)
									logBuf.Add($"Target lanes ({nextCompatibleLaneCount}) found. prevSimilarLaneCount={prevSimilarLaneCount}");
#endif

								// min/max compatible outer similar lane indices
								short minNextOuterSimilarIndex = -1;
								short maxNextOuterSimilarIndex = -1;
								if (nextIsRealJunction) {
									// at junctions: try to match distinct lanes

									/*if (prevOuterSimilarLaneIndex >= nextCompatibleLaneCount-1) {
										// split inner lane
										minNextOuterSimilarIndex = maxNextOuterSimilarIndex = (short)((short)nextCompatibleLaneCount - 1);
#if DEBUGPF
										if (debug)
											logBuf.Add($"City rules: Splitting inner lane. minNextOuterSimilarLaneIndex={minNextOuterSimilarIndex} maxNextOuterSimilarLaneIndex={maxNextOuterSimilarIndex}");
#endif
									} else */if (nextCompatibleLaneCount > prevSimilarLaneCount && prevOuterSimilarLaneIndex == prevSimilarLaneCount-1) {
										// merge inner lanes
										minNextOuterSimilarIndex = prevOuterSimilarLaneIndex;
										maxNextOuterSimilarIndex = (short)((short)nextCompatibleLaneCount - 1);
#if DEBUGPF
										if (debug)
											logBuf.Add($"City rules: Merging inner lanes. minNextOuterSimilarLaneIndex={minNextOuterSimilarIndex} maxNextOuterSimilarLaneIndex={maxNextOuterSimilarIndex}");
#endif
									} else {
										// 1-to-n (lane splitting is done by FindCompatibleLane), 1-to-1 (direct lane matching)
										minNextOuterSimilarIndex = prevOuterSimilarLaneIndex;
										maxNextOuterSimilarIndex = prevOuterSimilarLaneIndex;
#if DEBUGPF
										if (debug)
											logBuf.Add($"City rules: 1-to-1/1-to-n. minNextOuterSimilarLaneIndex={minNextOuterSimilarIndex} maxNextOuterSimilarLaneIndex={maxNextOuterSimilarIndex}");
#endif
									}

									bool mayChangeLanes = isIncomingStraight && Flags.getStraightLaneChangingAllowed(nextSegmentId, nextIsStartNodeOfPrevSegment);
#if DEBUGPF
									if (debug)
										logBuf.Add($"Next is real junction. minNextOuterSimilarIndex={minNextOuterSimilarIndex}, maxNextOuterSimilarIndex={maxNextOuterSimilarIndex} prevHasBusLane={prevHasBusLane} mayChangeLanes={mayChangeLanes}");
#endif
									if (!mayChangeLanes) {
										bool nextHasBusLane = nextGeometry.HasBusLane();
										if (nextHasBusLane && !prevHasBusLane) {
											// allow vehicles on the bus lane AND on the next lane to merge on this lane
											maxNextOuterSimilarIndex = (short)Math.Min(nextCompatibleLaneCount - 1, maxNextOuterSimilarIndex + 1);
										} else if (!nextHasBusLane && prevHasBusLane) {
											// allow vehicles to enter the bus lane
											minNextOuterSimilarIndex = (short)Math.Max(0, minNextOuterSimilarIndex - 1);
										}
									} else {
										// vehicles may change lanes when going straight
										minNextOuterSimilarIndex = (short)Math.Max(0, minNextOuterSimilarIndex - 1);
										maxNextOuterSimilarIndex = (short)Math.Min(nextCompatibleLaneCount - 1, maxNextOuterSimilarIndex + 1);
#if DEBUGPF
										if (debug)
											logBuf.Add($"Next is incoming straight. Allowing lane changes! maxNextOuterSimilarIndex={minNextOuterSimilarIndex}, maxNextOuterSimilarIndex={maxNextOuterSimilarIndex}");
#endif
									}
#if DEBUGPF
									if (debug)
										logBuf.Add($"Next is junction with city rules. minNextOuterSimilarIndex={minNextOuterSimilarIndex}, maxNextOuterSimilarIndex={maxNextOuterSimilarIndex}");
#endif
								} else {
									// lane merging/splitting
									HandleLaneMergesAndSplits(ref item, nextSegmentId, prevOuterSimilarLaneIndex, nextCompatibleLaneCount, prevSimilarLaneCount, out minNextOuterSimilarIndex, out maxNextOuterSimilarIndex);
#if DEBUGPF
									if (debug)
										logBuf.Add($"Next is not a junction. nextOuterSimilarIndex=HandleLaneMergesAndSplits({prevOuterSimilarLaneIndex}, {nextCompatibleLaneCount}, {prevSimilarLaneCount})= min. {minNextOuterSimilarIndex} max. {maxNextOuterSimilarIndex}");
#endif
								}

								// find best matching lane(s)
								for (short nextOuterSimilarIndex = hasLaneConnections ? (short)0 : minNextOuterSimilarIndex; nextOuterSimilarIndex < (hasLaneConnections ? (short)nextCompatibleLaneCount : (short)(minNextOuterSimilarIndex+1)); ++nextOuterSimilarIndex) {
									nextLaneI = FindCompatibleLane(ref laneIndexByOuterSimilarIndex, compatibleOuterSimilarIndexesMask, nextOuterSimilarIndex);

#if DEBUGPF
									if (debug) {
										logBuf.Add($"BEST MATCHING LANE LOOP ITERATION {nextOuterSimilarIndex}. nextLaneI={nextLaneI} hasLaneConnections={hasLaneConnections} minNextOuterSimilarIndex={minNextOuterSimilarIndex} nextCompatibleLaneCount={nextCompatibleLaneCount} minNextOuterSimilarIndex={minNextOuterSimilarIndex}");
										logBuf.Add($"nextOuterSimilarIndex < minNextOuterSimilarIndex == {nextOuterSimilarIndex < minNextOuterSimilarIndex}, nextOuterSimilarIndex > maxNextOuterSimilarIndex == {nextOuterSimilarIndex > maxNextOuterSimilarIndex}, hasOutgoingConnections[nextLaneI]={hasOutgoingConnections[nextLaneI]}, nextIsConnectedWithPrevious[nextLaneI]={nextIsConnectedWithPrevious[nextLaneI]}");
                                    }
#endif

									if (nextLaneI < 0) {
										continue;
									}

									if (nextOuterSimilarIndex < minNextOuterSimilarIndex || nextOuterSimilarIndex > maxNextOuterSimilarIndex) {
										if (!hasOutgoingConnections[nextLaneI] || !nextIsConnectedWithPrevious[nextLaneI])
											continue; // disregard lane since it is not connected to previous lane
									} else {
										if (hasOutgoingConnections[nextLaneI] && !nextIsConnectedWithPrevious[nextLaneI])
											continue; // disregard lane since it is not connected to previous lane but has outgoing connections
									}

#if DEBUGPF
									if (debug)
										logBuf.Add($"current outer similar index = {nextOuterSimilarIndex}, min. {minNextOuterSimilarIndex} max. {maxNextOuterSimilarIndex}");
#endif

#if DEBUGPF
									if (debug)
										logBuf.Add($"(*) nextLaneI = {nextLaneI}");
#endif

									// go to matched lane
									nextLaneIndex = laneIndexes[nextLaneI];
									nextLaneId = laneIds[nextLaneI];

#if DEBUGPF
									if (debug)
										logBuf.Add($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane idx {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer. There are {curLaneI} candidate lanes. We choose lane {nextLaneI} (index {nextLaneIndex}, {nextOuterSimilarIndex} compatible from outer). lhd: {TrafficPriorityManager.IsLeftHandDrive()}, ped: {pedestrianAllowed}, magical flag4: {mayTurnAround}");
#endif
#if DEBUGPF
									if (debug) {
										FlushMainLog(logBuf, unitId);
									}
#endif

									if (ProcessItemCosts(nextIsSimpleTransition || uturn, false, debug, item, nextNodeId, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromInner, connectOffset, true, nextIsBeautificationNode, nextLaneIndex, nextLaneId, out foundForced)) {
										mayTurnAround = true;
									}
#if DEBUGPF
									couldFindCustomPath = true;
#endif
								}

								goto nextIter;
							}

							if (nextLaneI < 0) {
#if DEBUGPF
								if (debug)
									Log.Error($"(PFERR) Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer: nextLaneI < 0!");
#endif
								goto nextIter;
							}

							// go to matched lane
							nextLaneIndex = laneIndexes[nextLaneI];
							nextLaneId = laneIds[nextLaneI];

#if DEBUGPF
							if (debug)
								logBuf.Add($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer: nextLaneIndex={nextLaneIndex} nextLaneId={nextLaneId}");
#endif

							if (IsMasterPathFind) {
								if (hasOutgoingConnections[nextLaneI])
									Flags.removeHighwayLaneArrowFlags(nextLaneId);
								else if (applyHighwayRulesAtSegment) {
									// update highway mode lane arrows

#if DEBUGPF
									/*if (Options.disableSomething1)
										Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Setting highway arrows @ lane {nextLaneId}: START");*/
#endif
									Flags.LaneArrows? prevHighwayArrows = Flags.getHighwayLaneArrowFlags(nextLaneId);
									Flags.LaneArrows newHighwayArrows = Flags.LaneArrows.None;
									if (prevHighwayArrows != null)
										newHighwayArrows = (Flags.LaneArrows)prevHighwayArrows;
									if (isIncomingRight)
										newHighwayArrows |= Flags.LaneArrows.Left;
									else if (isIncomingLeft)
										newHighwayArrows |= Flags.LaneArrows.Right;
									else if (isIncomingStraight)
										newHighwayArrows |= Flags.LaneArrows.Forward;

									if (newHighwayArrows != prevHighwayArrows && newHighwayArrows != Flags.LaneArrows.None)
										Flags.setHighwayLaneArrowFlags(nextLaneId, newHighwayArrows, false);
#if DEBUGPF
									/*if (Options.disableSomething1)
										Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Setting highway arrows @ lane {nextLaneId} to {newHighwayArrows.ToString()}: END");*/
#endif
								}
							}

#if DEBUGPF
							if (debug) {
								FlushMainLog(logBuf, unitId);
							}
#endif

							if (ProcessItemCosts(nextIsSimpleTransition || uturn, true, debug, item, nextNodeId, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromInner, connectOffset, true, nextIsBeautificationNode, nextLaneIndex, nextLaneId, out foundForced)) {
								mayTurnAround = true;
							}

#if DEBUGPF
							if (foundForced) {
								if (debug)
									logBuf.Add($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer: FORCED LANE FOUND!");
								couldFindCustomPath = true;
							}
#endif
						} else {
							// no compatible lanes found
#if DEBUGPF
							if (debug)
								Log.Error($"(PFERR) Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer: No lane arrows defined");
#endif
#if DEBUGPF
							couldFindCustomPath = true; // the player did not set lane arrows. this is ok...
														/*if (ProcessItem(debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromInner, connectOffset, true, enablePedestrian)) {
															blocked = true;
														}*/
#endif
						}
						// NON-STOCK CODE END
					} else {
						// we were coming from a beautfication node; visiting a park building or a pedestrian/bicycle pathway

#if DEBUGPF
						if (debug) {
							FlushMainLog(logBuf, unitId);
						}
#endif

						// stock code:
						if (this.ProcessItemCosts(false, false, debug, item, nextNodeId, nextSegmentId, ref netManager.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromInner, connectOffset, true, nextIsBeautificationNode)) {
							mayTurnAround = true;
						}
#if DEBUGPF
						couldFindCustomPath = true; // not of interest for us
#endif
					}

#if DEBUGPF
					if (!couldFindCustomPath) {
						if (debug)
							logBuf.Add($"(PFERR) Could not find custom path from segment {nextSegmentId} to segment {prevSegmentId}, lane {item.m_position.m_lane}, off {item.m_position.m_offset} at node {nextNodeId}!");
					}
#endif
							// stock code:
							/*if (this.ProcessItem(debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromInner, connectOffset, true, enablePedestrian)) {
								blocked = true;
							}*/
							//}

					nextIter:
					if (nextSegmentId == prevSegmentId)
						similarLaneIndexFromInner = firstSimilarLaneIndexFromInner; // u-turning does not "consume" a lane

					if (TrafficPriorityManager.IsLeftHandDrive()) {
						nextSegmentId = netManager.m_segments.m_buffer[nextSegmentId].GetLeftSegment(nextNodeId);
					} else {
						nextSegmentId = netManager.m_segments.m_buffer[nextSegmentId].GetRightSegment(nextNodeId);
					}

					if (nextSegmentId != prevSegmentId) {
						totalIncomingLanes += incomingVehicleLanes;
						totalOutgoingLanes += outgoingVehicleLanes;
					}

					if (explorePrevSegment && nextSegmentId == prevSegmentId)
						break;
				} // foreach segment
#if DEBUGPF
				if (debug)
					logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer: Custom part finished");
#endif
				if (!explorePrevSegment && mayTurnAround && (this._vehicleTypes & VehicleInfo.VehicleType.Tram) == VehicleInfo.VehicleType.None) {
					// turn-around for vehicles (if street is blocked)
#if DEBUGPF
					if (debug)
						logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer: Road may be blocked");
#endif
					// vehicles may turn around if the street is blocked
					nextSegmentId = item.m_position.m_segment;

#if DEBUGPF
					if (debug) {
						FlushMainLog(logBuf, unitId);
					}
#endif

					this.ProcessItemCosts(false, false, debug, item, nextNodeId, nextSegmentId, ref netManager.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromInner, connectOffset, true, false);
				}

				if (allowPedSwitch) {
					// switch from walking to driving a car, bus, etc.

					nextSegmentId = item.m_position.m_segment;
					int nextLaneIndex;
					uint nextLaneId;
					if (netManager.m_segments.m_buffer[(int)nextSegmentId].GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Pedestrian, this._vehicleTypes, out nextLaneIndex, out nextLaneId)) {
#if DEBUGPF
						if (debug)
							logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevOuterSimilarLaneIndex} from outer: Ped allowed u-turn");
#endif
#if DEBUGPF2
						if (debug2)
							logBuf2.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}: Ped allowed u-turn. nextConnectOffset={nextConnectOffset} nextLaneIndex={nextLaneIndex} nextLaneId={nextLaneId}");
#endif
						this.ProcessItemPedBicycle(item, nextNodeId, nextSegmentId, ref netManager.m_segments.m_buffer[(int)nextSegmentId], nextConnectOffset, nextConnectOffset, nextLaneIndex, nextLaneId); // ped
					}
				} // allowPedSwitch
			} // !prevIsPedestrianLane

			if (nextNode.m_lane != 0u) {
				// transport lines, cargo lines, etc.

				bool targetDisabled = (nextNode.m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None;
				ushort nextSegment = netManager.m_lanes.m_buffer[nextNode.m_lane].m_segment;
				if (nextSegment != 0 && nextSegment != item.m_position.m_segment) {
#if DEBUGPF
					if (debug)
						logBuf.Add($"Exploring path from {nextSegment} to {item.m_position.m_segment}, lane idx {item.m_position.m_lane}: handling special lanes");
#endif
#if DEBUGPF2
					if (debug2)
						logBuf2.Add($"Exploring path from {nextSegment} to {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}: handling special lanes");
#endif
					this.ProcessItemPublicTransport(item, nextNodeId, targetDisabled, nextSegment, ref netManager.m_segments.m_buffer[(int)nextSegment], nextNode.m_lane, nextNode.m_laneOffset, connectOffset);
				}
			}

#if DEBUGPF
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

		/// <summary>
		/// Finds a value `value` in an array `values` that contains `length` valid elements.
		/// </summary>
		/// <param name="values">array to be queried</param>
		/// <param name="value">value to be found</param>
		/// <param name="length">valid length of array (array may be indeed bigger than this)</param>
		/// <returns></returns>
		private static short FindValue(ref byte[] values, int value, short length) {
			for (short i = 0; i < length; ++i) {
				if (values[i] == value)
					return i;
			}
			return -1;
		}

		/// <summary>
		/// xxx Finds the value in `values` having the (n+1)th lowest index, or, if (n+1) > number of valid elements in `values` finds the value in `values` with the highest index.
		/// </summary>
		/// <param name="values">array to be queried</param>
		/// <param name="validMask">a bitmask holding all valid indices of `values`</param>
		/// <param name="n">query</param>
		/// <returns></returns>
		private static short FindCompatibleLane(ref byte[] values, ushort validMask, short n) {
			short nextLaneI = -1;
			for (byte i = 0; i < POW2MASKS.Length; ++i) {
				if ((validMask & POW2MASKS[i]) == 0)
					continue;

				nextLaneI = values[i];
				if (n <= 0)
					break; // found exact index
				--n;
			}
			return nextLaneI;
		}

		/// <summary>
		/// Calculates minimum and maximum outer similar lane indices for lane merging/splitting.
		/// </summary>
		/// <param name="prevSimilarLaneIndex">previous similar lane index</param>
		/// <param name="nextCompatibleLaneCount">number of found compatible lanes at the next segment</param>
		/// <param name="prevSimilarLaneCount">number of similar lanes at the previous segment</param>
		/// <param name="minNextSimilarLaneIndex">output: minimum outer similar lane index</param>
		/// <param name="maxNextSimilarLaneIndex">ouput: maximum outer similar lane index</param>
		private void HandleLaneMergesAndSplits(ref BufferItem item, ushort nextSegmentId, short prevSimilarLaneIndex, short nextCompatibleLaneCount, short prevSimilarLaneCount, out short minNextSimilarLaneIndex, out short maxNextSimilarLaneIndex) {
#if DEBUGMERGE
			uint cp = 0;
#endif

			bool sym1 = (prevSimilarLaneCount & 1) == 0; // mod 2 == 0
			bool sym2 = (nextCompatibleLaneCount & 1) == 0; // mod 2 == 0
			if (prevSimilarLaneCount < nextCompatibleLaneCount) {
#if DEBUGMERGE
				cp |= 1u;
#endif
				// lane merging
				if (sym1 == sym2) {
#if DEBUGMERGE
					cp |= 32u;
#endif
					// merge outer lanes
					short a = (short)((byte)(nextCompatibleLaneCount - prevSimilarLaneCount) >> 1); // nextCompatibleLaneCount - prevSimilarLaneCount is always > 0
					if (prevSimilarLaneCount == 1) {
#if DEBUGMERGE
						cp |= 512u;
#endif
						minNextSimilarLaneIndex = 0;
						maxNextSimilarLaneIndex = (short)(nextCompatibleLaneCount - 1); // always >=0
					} else if (prevSimilarLaneIndex == 0) {
#if DEBUGMERGE
						cp |= 1024u;
#endif
						minNextSimilarLaneIndex = 0;
						maxNextSimilarLaneIndex = a;
					} else if (prevSimilarLaneIndex == prevSimilarLaneCount - 1) {
#if DEBUGMERGE
						cp |= 2048u;
#endif
						minNextSimilarLaneIndex = (short)(prevSimilarLaneIndex + a);
						maxNextSimilarLaneIndex = (short)(nextCompatibleLaneCount - 1); // always >=0
					} else {
#if DEBUGMERGE
						cp |= 4096u;
#endif
						minNextSimilarLaneIndex = maxNextSimilarLaneIndex = (short)(prevSimilarLaneIndex + a);
					}
				} else {
#if DEBUGMERGE
					cp |= 64u;
#endif
					// criss-cross merge
					short a = (short)((byte)(nextCompatibleLaneCount - prevSimilarLaneCount - 1) >> 1); // nextCompatibleLaneCount - prevSimilarLaneCount - 1 is always >= 0
					short b = (short)((byte)(nextCompatibleLaneCount - prevSimilarLaneCount + 1) >> 1); // nextCompatibleLaneCount - prevSimilarLaneCount + 1 is always >= 2
					if (prevSimilarLaneCount == 1) {
#if DEBUGMERGE
						cp |= 8192u;
#endif
						minNextSimilarLaneIndex = 0;
						maxNextSimilarLaneIndex = (short)(nextCompatibleLaneCount - 1); // always >=0
					} else if (prevSimilarLaneIndex == 0) {
#if DEBUGMERGE
						cp |= 16384u;
#endif
						minNextSimilarLaneIndex = 0;
						maxNextSimilarLaneIndex = b;
					} else if (prevSimilarLaneIndex == prevSimilarLaneCount - 1) {
#if DEBUGMERGE
						cp |= 32768u;
#endif
						minNextSimilarLaneIndex = (short)(prevSimilarLaneIndex + a);
						maxNextSimilarLaneIndex = (short)(nextCompatibleLaneCount - 1); // always >=0
					} else if (_pathRandomizer.Int32(0, 1) == 0) {
#if DEBUGMERGE
						cp |= 65536u;
#endif
						minNextSimilarLaneIndex = maxNextSimilarLaneIndex = (short)(prevSimilarLaneIndex + a);
					} else {
#if DEBUGMERGE
						cp |= 131072u;
#endif
						minNextSimilarLaneIndex = maxNextSimilarLaneIndex = (short)(prevSimilarLaneIndex + b);
					}
				}
			} else if (prevSimilarLaneCount == nextCompatibleLaneCount) {
#if DEBUGMERGE
				cp |= 2u;
#endif
				minNextSimilarLaneIndex = maxNextSimilarLaneIndex = prevSimilarLaneIndex;
			} else {
#if DEBUGMERGE
				cp |= 4u;
#endif
				// at lane splits: distribute traffic evenly (1-to-n, n-to-n)										
				// prevOuterSimilarIndex is always > nextCompatibleLaneCount
				if (sym1 == sym2) {
#if DEBUGMERGE
					cp |= 128u;
#endif
					// split outer lanes
					short a = (short)((byte)(prevSimilarLaneCount - nextCompatibleLaneCount) >> 1); // prevSimilarLaneCount - nextCompatibleLaneCount is always > 0
					minNextSimilarLaneIndex = maxNextSimilarLaneIndex = (short)(prevSimilarLaneIndex - a); // a is always <= prevSimilarLaneCount
				} else {
#if DEBUGMERGE
					cp |= 256u;
#endif
					// split outer lanes, criss-cross inner lanes 
					short a = (short)((byte)(prevSimilarLaneCount - nextCompatibleLaneCount - 1) >> 1); // prevSimilarLaneCount - nextCompatibleLaneCount - 1 is always >= 0

					minNextSimilarLaneIndex = (a - 1 >= prevSimilarLaneIndex) ? (short)0 : (short)(prevSimilarLaneIndex - a - 1);
					maxNextSimilarLaneIndex = (a >= prevSimilarLaneIndex) ? (short)0 : (short)(prevSimilarLaneIndex - a);

#if DEBUGPF
					if (minNextSimilarLaneIndex > maxNextSimilarLaneIndex) {
						Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: HandleLaneMergesAndSplits: item: lane {item.m_position.m_lane} @ seg. {item.m_position.m_segment}, nextSegmentId={nextSegmentId} prevSimilarLaneCount={prevSimilarLaneCount} nextCompatibleLaneCount={nextCompatibleLaneCount} a={a} prevSimilarLaneIndex={prevSimilarLaneIndex} minNextSimilarLaneIndex={minNextSimilarLaneIndex} maxNextSimilarLaneIndex={maxNextSimilarLaneIndex}");

					}
#endif
				}
			}
			if (minNextSimilarLaneIndex > nextCompatibleLaneCount - 1) {
#if DEBUGMERGE
				cp |= 8u;
#endif
				minNextSimilarLaneIndex = (short)(nextCompatibleLaneCount - 1);
			}
			if (maxNextSimilarLaneIndex > nextCompatibleLaneCount - 1) {
#if DEBUGMERGE
				cp |= 16u;
#endif
				maxNextSimilarLaneIndex = (short)(nextCompatibleLaneCount - 1);
			}

			if (minNextSimilarLaneIndex > maxNextSimilarLaneIndex) {
#if DEBUGPF && DEBUGMERGE
				Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Erroneous calculation in HandleMergeAndSplits detected! cp={cp}");
#endif
				minNextSimilarLaneIndex = maxNextSimilarLaneIndex;
			}
		}

#region stock code
		// 2
		private void ProcessItemPublicTransport(BufferItem item, ushort targetNodeId, bool targetDisabled, ushort nextSegmentId, ref NetSegment nextSegment, uint nextLane, byte offset, byte connectOffset) {
			if ((nextSegment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None) {
				return;
			}
			NetManager instance = Singleton<NetManager>.instance;
			if (targetDisabled && ((instance.m_nodes.m_buffer[(int)nextSegment.m_startNode].m_flags | instance.m_nodes.m_buffer[(int)nextSegment.m_endNode].m_flags) & NetNode.Flags.Disabled) == NetNode.Flags.None) {
				return;
			}
			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = instance.m_segments.m_buffer[(int)item.m_position.m_segment].Info;
			int nextNumLanes = nextSegmentInfo.m_lanes.Length;
			uint curLaneId = nextSegment.m_lanes;
			float prevMaxSpeed = 1f;
			float prevSpeed = 1f;
			NetInfo.LaneType laneType = NetInfo.LaneType.None;
			// NON-STOCK CODE START //
			bool nextIsRealJunction = instance.m_nodes.m_buffer[targetNodeId].CountSegments() > 2;
			ushort sourceNodeId = (targetNodeId == instance.m_segments.m_buffer[item.m_position.m_segment].m_startNode) ? instance.m_segments.m_buffer[item.m_position.m_segment].m_endNode : instance.m_segments.m_buffer[item.m_position.m_segment].m_startNode; // no lane changing directly in front of a junction
			bool prevIsRealJunction = instance.m_nodes.m_buffer[sourceNodeId].CountSegments() > 2;
			// NON-STOCK CODE END //
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane lane2 = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				prevMaxSpeed = GetLaneSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, lane2); // SpeedLimitManager.GetLockFreeGameSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, ref lane2); // NON-STOCK CODE
				laneType = lane2.m_laneType;
				if ((byte)(laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
					laneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
				prevSpeed = this.CalculateLaneSpeed(prevMaxSpeed, connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[(int)item.m_position.m_segment], lane2); // NON-STOCK CODE
			}
			float averageLength = instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_averageLength;
			float offsetLength = (float)Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * 0.003921569f * averageLength;
			float methodDistance = item.m_methodDistance + offsetLength;
			float comparisonValue = item.m_comparisonValue + offsetLength / (prevSpeed * this._maxLength);
			Vector3 b = instance.m_lanes.m_buffer[item.m_laneID].CalculatePosition((float)connectOffset * 0.003921569f);
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
						Vector3 a = instance.m_lanes.m_buffer[nextLane].CalculatePosition((float)offset * 0.003921569f);
						float distance = Vector3.Distance(a, b);
						BufferItem nextItem;
						// NON-STOCK CODE START //
						if (prevIsRealJunction)
							nextItem.m_numSegmentsToJunction = 0;
						else
							nextItem.m_numSegmentsToJunction = item.m_numSegmentsToJunction + 1;
						// NON-STOCK CODE END //
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
								float nextOffsetDistance = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetA)) * 0.003921569f;
								nextItem.m_comparisonValue += nextOffsetDistance * nextSegment.m_averageLength / (nextSpeed * this._maxLength);
							}
							if (nextLane == this._startLaneB) {
								if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetB) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetB)) {
									return;
								}
								float nextSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo); // NON-STOCK CODE
								float nextOffsetDistance = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetB)) * 0.003921569f;
								nextItem.m_comparisonValue += nextOffsetDistance * nextSegment.m_averageLength / (nextSpeed * this._maxLength);
							}
							nextItem.m_laneID = nextLane;
							nextItem.m_lanesUsed = (item.m_lanesUsed | nextLaneInfo.m_laneType);
							this.AddBufferItem(nextItem, item.m_position);
						}
					}
					return;
				}
				curLaneId = instance.m_lanes.m_buffer[curLaneId].m_nextLane;
				++laneIndex;
			}
		}
#endregion

		private bool ProcessItemCosts(bool allowCustomLaneChanging, bool ignoreLaneArrows, bool debug, BufferItem item, ushort targetNode, ushort segmentID, ref NetSegment segment, ref int laneIndexFromInner, byte connectOffset, bool enableVehicle, bool enablePedestrian) {
			bool foundForced = false;
			return ProcessItemCosts(allowCustomLaneChanging, ignoreLaneArrows, debug, item, targetNode, segmentID, ref segment, ref laneIndexFromInner, connectOffset, enableVehicle, enablePedestrian, null, null, out foundForced);
		}

		// 3
		private bool ProcessItemCosts(bool allowCustomLaneChanging, bool ignoreLaneArrows, bool debug, BufferItem item, ushort targetNodeId, ushort nextSegmentId, ref NetSegment nextSegment, ref int laneIndexFromInner, byte connectOffset, bool enableVehicle, bool enablePedestrian, int? forceLaneIndex, uint? forceLaneId, out bool foundForced) {
#if DEBUGPF
			/*if (Options.disableSomething1)
				Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: ProcessItemSub item {item.m_position.m_segment} {item.m_position.m_lane}, targetNodeId {targetNodeId}");*/
#endif

#if DEBUGPF
			List<String> logBuf = null;
			if (debug) {
				logBuf = new List<String>();
				logBuf.Add($"ProcessItemCosts called. Exploring path from {nextSegmentId} to {item.m_position.m_segment} lane id {item.m_position.m_lane} @ {targetNodeId}. forceLaneIndex={forceLaneIndex}, forceLaneId={forceLaneId}");
			}
#endif

			//bool emergencyLaneSelection = (Options.disableSomething3 && _extVehicleType == ExtVehicleType.Emergency);

			foundForced = false;
			bool blocked = false;
			if ((nextSegment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None) {
#if DEBUGPF
				if (debug)
					logBuf.Add($"ProcessItemCosts: Segment is PathFailed or Flooded: {nextSegment.m_flags}");
#endif
#if DEBUGPF
				if (debug)
					FlushCostLog(logBuf);
#endif
				return blocked;
			}
			NetManager netManager = Singleton<NetManager>.instance;
			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = netManager.m_segments.m_buffer[(int)item.m_position.m_segment].Info;
			int nextNumLanes = nextSegmentInfo.m_lanes.Length;
			NetInfo.Direction nextDir = (targetNodeId != nextSegment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
			NetInfo.Direction nextFinalDir = ((nextSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? nextDir : NetInfo.InvertDirection(nextDir);
			float turningAngle = 0.01f - Mathf.Min(nextSegmentInfo.m_maxTurnAngleCos, prevSegmentInfo.m_maxTurnAngleCos);
			if (turningAngle < 1f) {
				Vector3 prevDirection;
				if (targetNodeId == netManager.m_segments.m_buffer[(int)item.m_position.m_segment].m_startNode) {
					prevDirection = netManager.m_segments.m_buffer[(int)item.m_position.m_segment].m_startDirection;
				} else {
					prevDirection = netManager.m_segments.m_buffer[(int)item.m_position.m_segment].m_endDirection;
				}
				Vector3 nextDirection;
				if ((byte)(nextDir & NetInfo.Direction.Forward) != 0) {
					nextDirection = nextSegment.m_endDirection;
				} else {
					nextDirection = nextSegment.m_startDirection;
				}
				float dirDotProd = prevDirection.x * nextDirection.x + prevDirection.z * nextDirection.z;
				if (dirDotProd >= turningAngle) {
#if DEBUGPF
					if (debug)
						logBuf.Add($"ProcessItemCosts: turningAngle < 1f! sqrLen={sqrLen} >= turningAngle{turningAngle}!");
#endif
#if DEBUGPF
					if (debug)
						FlushCostLog(logBuf);
#endif
					return blocked;
				}
			}

			// determine if Advanced AI shouuld be used here
			bool useAdvancedAI = !Options.isStockLaneChangerUsed() &&
				_extVehicleType != null &&
				allowCustomLaneChanging &&
				((ExtVehicleType)_extVehicleType & (ExtVehicleType.RoadVehicle & ~ExtVehicleType.Bus)) != ExtVehicleType.None &&
				!_stablePath &&
				enableVehicle;

#if DEBUGPF
			if (debug)
				logBuf.Add($"ProcessItemCosts: useAdvancedAI={useAdvancedAI}, isStockLaneChangerUsed={Options.isStockLaneChangerUsed()}, _extVehicleType={_extVehicleType}, allowCustomLaneChanging={allowCustomLaneChanging}, nonBus={((ExtVehicleType)_extVehicleType & (ExtVehicleType.RoadVehicle & ~ExtVehicleType.Bus)) != ExtVehicleType.None}, _stablePath={_stablePath}, enableVehicle={enableVehicle}");
#endif

			float prevMaxSpeed = 1f;
			float prevLaneSpeed = 1f;
			NetInfo.LaneType prevLaneType = NetInfo.LaneType.None;
			VehicleInfo.VehicleType prevVehicleType = VehicleInfo.VehicleType.None;
			// NON-STOCK CODE START //
			bool prevIsHighway = false;
			if (prevSegmentInfo.m_netAI is RoadBaseAI)
				prevIsHighway = ((RoadBaseAI)prevSegmentInfo.m_netAI).m_highwayRules;
			int prevOuterSimilarLaneIndex = -1;
			int prevNumLanes = 1;
			float prevSpeed = 1f;
			float prevDensity = 0.1f;
			bool isMiddle = connectOffset != 0 && connectOffset != 255;
			NetInfo.Lane prevLaneInfo = null;
			// NON-STOCK CODE END //
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				prevLaneInfo = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				prevLaneType = prevLaneInfo.m_laneType;
				prevVehicleType = prevLaneInfo.m_vehicleType;
				prevMaxSpeed = /*emergencyLaneSelection ? 5f :*/ GetLaneSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, prevLaneInfo); // SpeedLimitManager.GetLockFreeGameSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, ref lane); // NON-STOCK CODE
				prevLaneSpeed = this.CalculateLaneSpeed(prevMaxSpeed, connectOffset, item.m_position.m_offset, ref netManager.m_segments.m_buffer[(int)item.m_position.m_segment], prevLaneInfo); // NON-STOCK CODE
																																																  // NON-STOCK CODE START //
				prevNumLanes = prevLaneInfo.m_similarLaneCount;
				if ((byte)(prevLaneInfo.m_direction & NetInfo.Direction.Forward) != 0) {
					prevOuterSimilarLaneIndex = prevLaneInfo.m_similarLaneCount - prevLaneInfo.m_similarLaneIndex - 1;
				} else {
					prevOuterSimilarLaneIndex = prevLaneInfo.m_similarLaneIndex;
				}
				if (useAdvancedAI) {
					prevSpeed = CustomRoadAI.laneMeanSpeeds[item.m_position.m_segment] != null && item.m_position.m_lane < CustomRoadAI.laneMeanSpeeds[item.m_position.m_segment].Length ? CustomRoadAI.laneMeanSpeeds[item.m_position.m_segment][item.m_position.m_lane] : 1f;
					prevDensity = CustomRoadAI.laneMeanAbsDensities[item.m_position.m_segment] != null && item.m_position.m_lane < CustomRoadAI.laneMeanAbsDensities[item.m_position.m_segment].Length ? CustomRoadAI.laneMeanAbsDensities[item.m_position.m_segment][item.m_position.m_lane] : 0.1f;
					//prevDensity = CustomRoadAI.laneMeanRelDensities[item.m_position.m_segment] != null && item.m_position.m_lane < CustomRoadAI.laneMeanRelDensities[item.m_position.m_segment].Length ? CustomRoadAI.laneMeanRelDensities[item.m_position.m_segment][item.m_position.m_lane] : 0.1f;

					prevSpeed = (float)Math.Max(0.1f, Math.Round(prevSpeed * 0.1f) / 10f); // 0.01, 0.1, 0.2, ... , 1
					prevDensity = (float)Math.Max(0.1f, Math.Round(prevDensity * 0.1f) / 10f); // 0.1, 0.2, ..., 1
				}
				// NON-STOCK CODE END //
			}

			// determine if Advanced AI should be used here
			if (useAdvancedAI) {
				useAdvancedAI = ((prevVehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None/* || (prevVehicleType == VehicleInfo.VehicleType.None && emergencyLaneSelection)*/) &&
					(prevLaneType & (NetInfo.LaneType.Pedestrian | NetInfo.LaneType.Parking)) == NetInfo.LaneType.None; // NON-STOCK CODE
			}

#if DEBUGPF
			if (debug)
				logBuf.Add($"ProcessItemCosts: useAdvancedAI(2)={useAdvancedAI}, prevCar={(prevVehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None}, prevPedParking={(prevLaneType & (NetInfo.LaneType.Pedestrian | NetInfo.LaneType.Parking)) == NetInfo.LaneType.None}");
#endif

			float prevCost = netManager.m_segments.m_buffer[(int)item.m_position.m_segment].m_averageLength;
			if (!useAdvancedAI && !this._stablePath) { // NON-STOCK CODE
													   // CO randomization. Only randomizes over segments, not over lanes.

				Randomizer randomizer = new Randomizer(this._pathFindIndex << 16 | (uint)item.m_position.m_segment);
				prevCost *= (float)(randomizer.Int32(900, 1000 + (int)(netManager.m_segments.m_buffer[(int)item.m_position.m_segment].m_trafficDensity * 10)) + this._pathRandomizer.Int32(20u)) * 0.001f;
			}

#if MARKCONGESTEDSEGMENTS
			bool prevIsCongested = false;
#endif
			if (!useAdvancedAI) {
				// stock code check for vehicle ban policies

				if (this._isHeavyVehicle && (netManager.m_segments.m_buffer[(int)item.m_position.m_segment].m_flags & NetSegment.Flags.HeavyBan) != NetSegment.Flags.None) {
					// heavy vehicle ban
#if DEBUGPF
					if (debug)
						logBuf.Add($"ProcessItemCosts: applying heavy vehicle ban on deactivated advanced AI");
#endif
					prevCost *= 10f;
				} else if (prevLaneType == NetInfo.LaneType.Vehicle && (prevVehicleType & _vehicleTypes) == VehicleInfo.VehicleType.Car && (netManager.m_segments.m_buffer[(int)item.m_position.m_segment].m_flags & NetSegment.Flags.CarBan) != NetSegment.Flags.None) {
					// car ban: used by "Old Town" policy
#if DEBUGPF
					if (debug)
						logBuf.Add($"ProcessItemCosts: applying old town policy on deactivated advanced AI");
#endif
					prevCost *= 5f;
				}

				if (this._transportVehicle && prevLaneType == NetInfo.LaneType.TransportVehicle) {
					// public transport/emergency vehicles should stay on their designated lanes, if possible
#if DEBUGPF
					if (debug)
						logBuf.Add($"ProcessItemCosts: applying public transport cost modifier on deactivated advaned AI");
#endif
					prevCost *= 0.5f; // non-stock value
				}
			}
#if MARKCONGESTEDSEGMENTS
			else {
				prevIsCongested = CustomRoadAI.segmentCongestion[item.m_position.m_segment];
			}
#endif

			bool avoidLane = false; // if true, avoidance policies are in effect (e.g. "Old Town" policy of "Ban Heavy Vehicles" policy)
			bool strictlyAvoidLane = false; // if true, the player has setup a vehicle restriction

			// check vehicle ban policies
			if ((this._isHeavyVehicle && (nextSegment.m_flags & NetSegment.Flags.HeavyBan) != NetSegment.Flags.None) ||
				(prevLaneType == NetInfo.LaneType.Vehicle && (prevVehicleType & _vehicleTypes) == VehicleInfo.VehicleType.Car &&
				(netManager.m_segments.m_buffer[(int)item.m_position.m_segment].m_flags & NetSegment.Flags.CarBan) != NetSegment.Flags.None)) {
#if DEBUGPF
				if (debug) {
					logBuf.Add($"ProcessItemCosts: Vehicle {_extVehicleType} should not use lane {item.m_position.m_lane} @ seg. {item.m_position.m_segment}, null? {prevLaneInfo == null}");
				}
#endif
				avoidLane = true;
			}

			// check for vehicle restrictions
			if (!CanUseLane(debug, item.m_position.m_segment, prevSegmentInfo, item.m_position.m_lane, prevLaneInfo)) {
#if DEBUGPF
				if (debug) {
					logBuf.Add($"ProcessItemCosts: Vehicle {_extVehicleType} must not use lane {item.m_position.m_lane} @ seg. {item.m_position.m_segment}, null? {prevLaneInfo == null}");
				}
#endif
				strictlyAvoidLane = true;
			}

			if (!useAdvancedAI) {
				// apply vehicle restrictions when not using Advanced AI
				if (strictlyAvoidLane) {
#if DEBUGPF
					if (debug)
						logBuf.Add($"ProcessItemCosts: applying strict lane avoidance on deactivated advaned AI");
#endif
					prevCost *= 100f;
				}

				// add costs for u-turns
				if (!isMiddle && nextSegmentId == item.m_position.m_segment && (prevLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
#if DEBUGPF
					if (debug)
						logBuf.Add($"ProcessItemCosts: applying u-turn cost factor on deactivated advaned AI");
#endif
					prevCost *= Options.someValue4;
				}
			}

			if ((byte)(prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
				prevLaneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
			}
			float prevOffsetCost = (float)Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * 0.003921569f * prevCost;
			float prevMethodDist = item.m_methodDistance + prevOffsetCost;
			float prevComparisonPlusOffsetCostOverSpeed = item.m_comparisonValue + prevOffsetCost / (prevLaneSpeed * this._maxLength);
			Vector3 prevLanePosition = netManager.m_lanes.m_buffer[item.m_laneID].CalculatePosition((float)connectOffset * 0.003921569f);
			int newLaneIndexFromInner = laneIndexFromInner;
			bool transitionNode = (netManager.m_nodes.m_buffer[targetNodeId].m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;
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

			// NON-STOCK CODE START //


			bool nextIsStartNodeOfPrevSegment = netManager.m_segments.m_buffer[item.m_position.m_segment].m_startNode == targetNodeId;
			bool nextIsStartNodeOfNextSegment = netManager.m_segments.m_buffer[nextSegmentId].m_startNode == targetNodeId;
			ushort sourceNodeId = nextIsStartNodeOfPrevSegment ? netManager.m_segments.m_buffer[item.m_position.m_segment].m_endNode : netManager.m_segments.m_buffer[item.m_position.m_segment].m_startNode; // no lane changing directly in front of a junction
			bool prevIsJunction = (netManager.m_nodes.m_buffer[sourceNodeId].m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
			bool nextIsHighway = SegmentGeometry.Get(nextSegmentId).IsHighway();
			bool nextIsRealJunction = SegmentGeometry.Get(item.m_position.m_segment).CountOtherSegments(nextIsStartNodeOfPrevSegment) > 1;
			bool uturn = !isMiddle && nextSegmentId == item.m_position.m_segment;

			// determines if a vehicles wants to change lanes here (randomized). If true, costs for changing to an adjacent lane are reduced
			bool wantToChangeLane = false;
			if (useAdvancedAI && !uturn) {
				laneChangeRandCounter = (ushort)((laneChangeRandCounter + 1) % (int)Options.someValue16);
				wantToChangeLane = (laneChangeRandCounter == 0);
			}
			// NON-STOCK CODE END //

			uint laneIndex = forceLaneIndex != null ? (uint)forceLaneIndex : 0u; // NON-STOCK CODE, forcedLaneIndex is not null if the next node is a (real) junction
			uint curLaneId = (uint)(forceLaneId != null ? forceLaneId : nextSegment.m_lanes); // NON-STOCK CODE, forceLaneId is not null if the next node is a (real) junction
			while (laneIndex < nextNumLanes && curLaneId != 0u) {
				// NON-STOCK CODE START //
				if (forceLaneIndex != null && laneIndex != forceLaneIndex) {
#if DEBUGPF
					if (debug)
						logBuf.Add($"ProcessItemCosts: forceLaneIndex break! laneIndex={laneIndex}");
#endif
					break;
				}
				// NON-STOCK CODE END //
				NetInfo.Lane nextLane = nextSegmentInfo.m_lanes[laneIndex];

#if DEBUGCOSTS
				bool costDebug = debug;
				//bool costDebug = Options.disableSomething1 && (nextSegmentId == 9649 || nextSegmentId == 1043);
				if (costDebug) {
					logBuf.Add($"Path from {nextSegmentId} (idx {laneIndex}, id {curLaneId}) to {item.m_position.m_segment} (lane {prevOuterSimilarLaneIndex} from outer, idx {item.m_position.m_lane}): costDebug=TRUE, explore? {nextLane.CheckType(allowedLaneTypes, allowedVehicleTypes)} && {(nextSegmentId != item.m_position.m_segment || laneIndex != (int)item.m_position.m_lane)} && {(byte)(nextLane.m_finalDirection & nextFinalDir) != 0}");
				}
#endif

				if (forceLaneId == null && Options.laneConnectorEnabled) {
#if DEBUGPF
					if (debug)
						logBuf.Add($"ProcessItemCosts: forceLaneId == null. Checking if next lane idx {laneIndex}, id {curLaneId} has outgoing connections (startNode={nextIsStartNodeOfNextSegment})");
#endif

					if (laneConnManager.HasConnections(curLaneId, nextIsStartNodeOfNextSegment) && !laneConnManager.AreLanesConnected(curLaneId, item.m_laneID, nextIsStartNodeOfNextSegment)) {
#if DEBUGPF
						if (debug) {
							logBuf.Add($"Source lane {curLaneId} is NOT connected with target lane {item.m_laneID}, but source lane has outgoing connections! Skipping lane");
						}
#endif
						goto CONTINUE_LANE_LOOP;
					}

#if DEBUGPF
					if (debug)
						logBuf.Add($"ProcessItemCosts: Check for outgoing connections passed!");
#endif
				}

				if ((byte)(nextLane.m_finalDirection & nextFinalDir) != 0) {
					// lane direction is compatible
#if DEBUGPF
					if (debug)
						logBuf.Add($"ProcessItemCosts: Lane direction check passed: {nextLane.m_finalDirection}");
#endif
					if ((nextLane.CheckType(allowedLaneTypes, allowedVehicleTypes)/* || (emergencyLaneSelection && nextLane.m_vehicleType == VehicleInfo.VehicleType.None)*/) &&
							(nextSegmentId != item.m_position.m_segment || laneIndex != (int)item.m_position.m_lane)) {
						// vehicle types match and no u-turn to the previous lane

#if DEBUGPF
						if (debug)
							logBuf.Add($"ProcessItemCosts: vehicle type check passed: {nextLane.CheckType(allowedLaneTypes, allowedVehicleTypes)} && {(nextSegmentId != item.m_position.m_segment || laneIndex != (int)item.m_position.m_lane)}");
#endif

						// NON-STOCK CODE START //
						float nextMaxSpeed = /*emergencyLaneSelection ? 5f : */GetLaneSpeedLimit(nextSegmentId, laneIndex, curLaneId, nextLane);
						bool addCustomTrafficCosts = useAdvancedAI &&
							curLaneId != this._startLaneA &&
							curLaneId != this._startLaneB &&
							((nextLane.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None/* || emergencyLaneSelection*/) &&
							(nextLane.m_laneType & (NetInfo.LaneType.Pedestrian | NetInfo.LaneType.Parking)) == NetInfo.LaneType.None;

						/*if (nextIsRealJunction && addCustomTrafficCosts) {
#if DEBUGPF
							if (debug)
								logBuf.Add($"ProcessItemCosts: reducing maxSpeed in front of junctions");
#endif

							// max. speed is reduced in front of junctions
							nextMaxSpeed *= 0.25f;
						}*/

						float customDeltaCost = 0f;
						float nextSpeed = 1f;
						float nextDensity = 0.1f;
						if (addCustomTrafficCosts && !strictlyAvoidLane) {
							// calculate current lane density & speed
							nextSpeed = CustomRoadAI.laneMeanSpeeds[nextSegmentId] != null && laneIndex < CustomRoadAI.laneMeanSpeeds[nextSegmentId].Length ? CustomRoadAI.laneMeanSpeeds[nextSegmentId][laneIndex] : 1f;
							nextDensity = CustomRoadAI.laneMeanAbsDensities[nextSegmentId] != null && laneIndex < CustomRoadAI.laneMeanAbsDensities[nextSegmentId].Length ? CustomRoadAI.laneMeanAbsDensities[nextSegmentId][laneIndex] : 0.1f;
							//nextDensity = CustomRoadAI.laneMeanRelDensities[nextSegmentId] != null && laneIndex < CustomRoadAI.laneMeanRelDensities[nextSegmentId].Length ? CustomRoadAI.laneMeanRelDensities[nextSegmentId][laneIndex] : 0.1f;
							
							nextSpeed = (float)Math.Max(0.1f, Math.Round(nextSpeed * 0.1f) / 10f); // 0.1, 0.2, ..., 1
							nextDensity = (float)Math.Max(0.1f, Math.Round(nextDensity * 0.1f) / 10f); // 0.1, 0.2, ..., 1
						}
						// NON-STOCK CODE END //

						Vector3 a;
						if ((byte)(nextDir & NetInfo.Direction.Forward) != 0) {
							a = netManager.m_lanes.m_buffer[curLaneId].m_bezier.d;
						} else {
							a = netManager.m_lanes.m_buffer[curLaneId].m_bezier.a;
						}
						float transitionCost = Vector3.Distance(a, prevLanePosition); // This gives the distance of the previous to next lane endpoints.

#if DEBUGCOSTS
						if (costDebug)
							logBuf.Add($"ProcessItemCosts: costs from {nextSegmentId} (off {(byte)(((nextDir & NetInfo.Direction.Forward) == 0) ? 0 : 255)}) to {item.m_position.m_segment} (off {item.m_position.m_offset}), connectOffset={connectOffset}: transitionCost={transitionCost}");
#endif

						if (transitionNode) {
							transitionCost *= 2f;
						}

						float transitionCostOverMeanMaxSpeed = transitionCost / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength);
						BufferItem nextItem;
						// NON-STOCK CODE START //
						if (prevIsJunction)
							nextItem.m_numSegmentsToJunction = 0;
						else
							nextItem.m_numSegmentsToJunction = item.m_numSegmentsToJunction + 1;
						// NON-STOCK CODE END //
						nextItem.m_position.m_segment = nextSegmentId;
						nextItem.m_position.m_lane = (byte)laneIndex;
						nextItem.m_position.m_offset = (byte)(((nextDir & NetInfo.Direction.Forward) == 0) ? 0 : 255);
						if ((byte)(nextLane.m_laneType & prevLaneType) == 0) {
							nextItem.m_methodDistance = 0f;
						} else {
							nextItem.m_methodDistance = prevMethodDist + transitionCost;
						}

#if DEBUGPF
						if (debug)
							logBuf.Add($"ProcessItemCosts: checking if methodDistance is in range: {nextLane.m_laneType != NetInfo.LaneType.Pedestrian} || {nextItem.m_methodDistance < 1000f} ({nextItem.m_methodDistance})");
#endif

						if (nextLane.m_laneType != NetInfo.LaneType.Pedestrian || nextItem.m_methodDistance < 1000f) {
							// NON-STOCK CODE START //

							if (!addCustomTrafficCosts) {
								// stock code
								nextItem.m_comparisonValue = prevComparisonPlusOffsetCostOverSpeed + transitionCostOverMeanMaxSpeed;
							} else {
								nextItem.m_comparisonValue = item.m_comparisonValue;
								customDeltaCost = transitionCost + prevOffsetCost; // distanceOnBezier now holds the costs for driving on the segment + costs for changing the segment

								if (strictlyAvoidLane) {
#if DEBUGPF
									if (debug)
										logBuf.Add($"ProcessItemCosts: applying strict lane avoidance on ACTIVATED advanced AI");
#endif

									// apply vehicle restrictions
									customDeltaCost *= 100f;
								} else if (avoidLane && (_extVehicleType == null || (_extVehicleType & (ExtVehicleType.CargoTruck | ExtVehicleType.PassengerCar)) != ExtVehicleType.None)) {
#if DEBUGPF
									if (debug)
										logBuf.Add($"ProcessItemCosts: applying lane avoidance on ACTIVATED advanced AI");
#endif

									// apply vanilla game restriction policies
									customDeltaCost *= 3f;
								}

#if DEBUGCOSTS
								if (costDebug) {
									logBuf.Add($"ProcessItemCosts: Path from {nextSegmentId} (idx {laneIndex}, id {curLaneId}) to {item.m_position.m_segment} (lane {prevOuterSimilarLaneIndex} from outer, idx {item.m_position.m_lane}): useAdvancedAI={useAdvancedAI}, addCustomTrafficCosts={addCustomTrafficCosts}, transitionCost={transitionCost} avoidLane={avoidLane} strictlyAvoidLane={strictlyAvoidLane}");
								}
#endif
							}
							// NON-STOCK CODE END //

							nextItem.m_direction = nextDir;
							if (curLaneId == this._startLaneA) {
								if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetA) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetA)) {
#if DEBUGPF
									if (debug)
										logBuf.Add($"ProcessItemCosts: Current lane is start lane A. goto next lane");
#endif
									goto CONTINUE_LANE_LOOP;
								}
								float nextLaneSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetA, nextItem.m_position.m_offset, ref nextSegment, nextLane); // NON-STOCK CODE
								float nextOffset = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetA)) * 0.003921569f;
								nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextLaneSpeed * this._maxLength);
							}

							if (curLaneId == this._startLaneB) {
								if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetB) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetB)) {
#if DEBUGPF
									if (debug)
										logBuf.Add($"ProcessItemCosts: Current lane is start lane B. goto next lane");
#endif
									goto CONTINUE_LANE_LOOP;
								}
								float nextLaneSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLane); // NON-STOCK CODE
								float nextOffset = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetB)) * 0.003921569f;
								nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextLaneSpeed * this._maxLength);
							}

							if (!this._ignoreBlocked && (nextSegment.m_flags & NetSegment.Flags.Blocked) != NetSegment.Flags.None && (byte)(nextLane.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
								// NON-STOCK CODE START //
								if (addCustomTrafficCosts) {
#if DEBUGPF
									if (debug)
										logBuf.Add($"ProcessItemCosts: Applying blocked road cost factor on ACTIVATED advanced AI");
#endif
									customDeltaCost *= 10f;
								} else {
									// NON-STOCK CODE END //
#if DEBUGPF
									if (debug)
										logBuf.Add($"ProcessItemCosts: Applying blocked road cost factor on disabled advanced AI");
#endif

									nextItem.m_comparisonValue += 0.1f;
								}
								blocked = true;
							}



							if ((byte)(nextLane.m_laneType & prevLaneType) != 0 && nextLane.m_vehicleType == prevVehicleType) {
#if DEBUGPF
								if (debug)
									logBuf.Add($"ProcessItemCosts: Applying transport vehicle costs");
#endif

								// NON-STOCK CODE START //
								if (Options.isStockLaneChangerUsed()) {
									// NON-STOCK CODE END //

									//if (! ignoreLaneArrows) {
										// this is CO's way of matching lanes between segments
										int firstTarget = (int)netManager.m_lanes.m_buffer[curLaneId].m_firstTarget;
										int lastTarget = (int)netManager.m_lanes.m_buffer[curLaneId].m_lastTarget;
										if (laneIndexFromInner < firstTarget || laneIndexFromInner >= lastTarget) {
											nextItem.m_comparisonValue += Mathf.Max(1f, transitionCost * 3f - 3f) / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength);
										}
									//}
											
									// cars should not be on public transport lanes
									if (!this._transportVehicle && nextLane.m_laneType == NetInfo.LaneType.TransportVehicle) {
										nextItem.m_comparisonValue += 20f / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength);
									}

									// NON-STOCK CODE START //
								} else {

									if (addCustomTrafficCosts && (_extVehicleType != ExtVehicleType.Emergency || Options.disableSomething4)) {
										bool isBus = _extVehicleType != null && ((_extVehicleType & ExtVehicleType.Bus) != ExtVehicleType.None);
										bool isPublicTransport = _extVehicleType != null && ((_extVehicleType & ExtVehicleType.RoadPublicTransport) != ExtVehicleType.None);
										bool nextIsTransportLane = nextLane.m_laneType == NetInfo.LaneType.TransportVehicle;

										// public transport should prefer transport lanes
										if (isPublicTransport && nextIsTransportLane) {
											customDeltaCost *= Options.someValue7;
										}

										// busses should not leave public transport lanes
										if (isBus && !nextIsTransportLane) {
											customDeltaCost *= Options.someValue6;
										}
									}
								}
								// NON-STOCK CODE END //
							}

							// NON-STOCK CODE START //
							bool addItem = true; // should we add the next item to the buffer?
							if (addCustomTrafficCosts) {
								// Advanced AI cost calculation

#if DEBUGPF
								if (debug)
									logBuf.Add($"ProcessItemCosts: Calculating advanced AI costs");
#endif

								int nextOuterSimilarLaneIndex;
								if ((byte)(nextLane.m_direction & NetInfo.Direction.Forward) != 0) {
									nextOuterSimilarLaneIndex = nextLane.m_similarLaneCount - nextLane.m_similarLaneIndex - 1;
								} else {
									nextOuterSimilarLaneIndex = nextLane.m_similarLaneIndex;
								}

								float relLaneDist = nextOuterSimilarLaneIndex - prevOuterSimilarLaneIndex; // relative lane distance
#if PREFEROUTER
								bool isPreferredLaneChangingDir = relLaneDist == 1;
#endif

								float laneDist = uturn ? Options.someValue4 : (float)Math.Abs(relLaneDist); // absolute lane distance. U-turns are treated as changing lanes, too.

								if (forceLaneIndex == null && prevIsHighway && nextIsHighway && laneDist > 1) {
									// disable lane changing by more than one on highways
									goto CONTINUE_LANE_LOOP;
								}

								if (strictlyAvoidLane) {
									// we assume maximum traffic density and minimum speed on strictly-to-be-avoided lanes
									nextSpeed = 0;
									prevDensity = 1;

#if DEBUGPF
									if (debug)
										logBuf.Add($"ProcessItemCosts: Strict lane avoidance: nextSpeed = 0, prevDensity = 1");
#endif
								}

								float metric = 1f; // resulting cost multiplicator
								float multMetric = 1f; // the multiplication part
								float divMetric = nextMaxSpeed; // the division part
#if DEBUG
								float metricBeforeLanes = 1f; // metric before multiplying lane changing costs
#endif

								/* Vehicles should
									- choose lanes with low traffic volume,
									- choose lanes with high speeds,
									- not change to lanes that with high distance to current lane,
									- not change lanes too often and
									- should not change lanes near junctions
								*/

								// calculate speed metric
								divMetric = nextSpeed * nextMaxSpeed; // 0 .. nextMaxSpeed

								// calculate density metric
								if (prevSpeed <= Options.someValue13)
									prevDensity = 1f;
								multMetric = Options.someValue12 + Options.pathCostMultiplicator2 * prevDensity;

								// calculate density/speed metric
								metric = Math.Max(0.01f, multMetric) / Math.Max(0.1f, divMetric);
#if DEBUG
								metricBeforeLanes = metric;
#endif

								float laneMetric = 1f;
								
								if (laneDist > 0 && // lane would be changed
									(_extVehicleType != ExtVehicleType.Emergency || Options.disableSomething4) && // emergency vehicles may do everything
									!nextIsRealJunction && // no lane changing at junctions
									(!wantToChangeLane || laneDist > 1)) { // randomized lane changing
										
									// multiply with lane distance if distance > 1 or if vehicle does not like to change lanes
									float laneChangeCostBase = 1f + // base
										(nextIsHighway ? Options.someValue : Options.someValue5) * // changing lanes on highways is more expensive than on city streets
										(_isHeavyVehicle ? Options.someValue2 : 1f) * // changing lanes is more expensive for heavy vehicles
										(laneDist > 1 ? Options.someValue11 : 1f) // changing more than one lane at a time is expensive
#if VEHICLERAND
										* (2f - _vehicleRand)
#endif
										; // fast vehicles (= high _vehicleRand) tend to change lanes more often

#if PREFEROUTER
									if (isPreferredLaneChangingDir && prevIsHighway && nextIsHighway) {
										float outerLaneCostFactor = (1f + Options.pathCostMultiplicator * (_vehicleRand - 0.5f)); // [1 - 0.5 * pathCostMultiplicator; 1 + 0.5 * pathCostMultiplicator], depending on _vehicleRand
										laneChangeCostBase = Math.Max(1f, laneChangeCostBase * outerLaneCostFactor); // slower vehicles tend to stay on the outer lane; faster vehicles on inner lanes
									}
#endif

									// we use the power operator here to express that lane changing one-by-one is preferred over changing multiple lanes at once
									laneMetric = (float)Math.Pow(laneChangeCostBase, laneDist);
									metric *= laneMetric;
								}

								// avoid lane changing before junctions: multiply with inverted distance to next junction
								if ((!prevIsHighway || !nextIsHighway) &&
									!nextIsRealJunction &&
									(_extVehicleType != ExtVehicleType.Emergency || Options.disableSomething4) &&
									laneDist > 0 &&
									nextItem.m_numSegmentsToJunction < 3) {
									float junctionMetric = (float)Math.Pow(Options.someValue3, Math.Max(0f, 3f - nextItem.m_numSegmentsToJunction));
									metric *= junctionMetric;
								}

								// avoid leaving main highway
								if (nextIsRealJunction && nextIsHighway && (!prevIsHighway || nextNumLanes > prevNumLanes)) {
									metric *= Options.someValue14;
								}

#if DEBUGCOSTS
								if (costDebug) {
									logBuf.Add($"Path from {nextSegmentId} (lane {nextOuterSimilarLaneIndex} from outer, idx {laneIndex}, id {curLaneId}) to {item.m_position.m_segment} (lane {prevOuterSimilarLaneIndex} from outer, idx {item.m_position.m_lane}): nextMaxSpeed={nextMaxSpeed} prevMaxSpeed={prevMaxSpeed} nextMaxSpeed={nextMaxSpeed} divMetric={divMetric} nextDensity={nextDensity} multMetric={multMetric} laneDist={laneDist} laneMetric={laneMetric} metric={metric} metricBeforeLanes={metricBeforeLanes} isMiddle={isMiddle}");
								}
#endif

								float oldTransitionDistanceOverMaxSpeed = transitionCostOverMeanMaxSpeed;
								float deltaCostOverMeanMaxSpeed = (metric * customDeltaCost) / this._maxLength;

#if DEBUG
								/*if ((segmentID == 25320 || segmentID == 31177) && Options.disableSomething1)
									Log._Debug($"Costs for lane {curLaneId} @ {segmentID}: prevSpeed={prevSpeed} nextSpeed={nextSpeed} prevDensity={prevDensity} nextDensity={nextDensity} divMetric={divMetric}, multMetric={multMetric} laneDist={laneDist} metric={metric} distanceOnBezier={distanceOnBezier} prevCost={item2.m_comparisonValue} newCost={distanceOnBezier+item2.m_comparisonValue}");*/
#endif

								if (deltaCostOverMeanMaxSpeed < 0f) {
									// should never happen
#if DEBUG
									Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId}, PF {this._pathFindIndex}: distanceOverMeanMaxSpeed < 0! seg. {nextSegmentId}, lane {laneIndex}, off {nextItem.m_position.m_offset} -> {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}. distanceOverMeanMaxSpeed={deltaCostOverMeanMaxSpeed}, nextSpeed={nextSpeed}"/* + ", prevSpeed={prevSpeed}"*/);
#endif
									deltaCostOverMeanMaxSpeed = 0f;
								} else if (Single.IsNaN(deltaCostOverMeanMaxSpeed) || Single.IsInfinity(deltaCostOverMeanMaxSpeed)) {
									// Fallback if we mess something up. Should never happen.
#if DEBUG
									//if (costDebug)
									Log.Error($"Pathfinder ({this._pathFindIndex}): distanceOverMeanMaxSpeed is NaN or Infinity: seg. {nextSegmentId}, lane {laneIndex}, off {nextItem.m_position.m_offset} -> {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}. {deltaCostOverMeanMaxSpeed} // nextMaxSpeed={nextMaxSpeed} prevMaxSpeed={prevMaxSpeed} nextMaxSpeed={nextMaxSpeed} divMetric={divMetric} nextDensity={nextDensity} multMetric={multMetric} laneDist={laneDist} laneMetric={laneMetric} metric={metric} metricBeforeLanes={metricBeforeLanes}");
#endif
#if DEBUGPF
									//Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId}, PF {this._pathFindIndex}: distanceOverMeanMaxSpeed is NaN! distanceOverMeanMaxSpeed={distanceOverMeanMaxSpeed}, nextSpeed={nextSpeed}, prevSpeed={prevSpeed}");
#endif
									deltaCostOverMeanMaxSpeed = oldTransitionDistanceOverMaxSpeed;
								}

#if DEBUGCOSTS
								if (costDebug) {
									logBuf.Add($"Path from {nextSegmentId} (lane {nextOuterSimilarLaneIndex} from outer, idx {laneIndex}) to {item.m_position.m_segment} (lane {prevOuterSimilarLaneIndex} from outer, idx {item.m_position.m_lane}.");
									//logBuf.Add($"distanceOverMeanMaxSpeed = {distanceOverMeanMaxSpeed} oldDistanceOverMaxSpeed = {oldDistanceOverMaxSpeed}, prevMaxSpeed={prevMaxSpeed}, nextMaxSpeed={nextMaxSpeed}, prevSpeed={prevSpeed}, nextSpeed={nextSpeed}");
									logBuf.Add($"deltaCostOverMeanMaxSpeed = {deltaCostOverMeanMaxSpeed} oldTransitionDistanceOverMaxSpeed = {oldTransitionDistanceOverMaxSpeed}, prevMaxSpeed={prevMaxSpeed}, nextMaxSpeed={nextMaxSpeed}, nextSpeed={nextSpeed} nextDensity={nextDensity}");
								}
#endif

								nextItem.m_comparisonValue += deltaCostOverMeanMaxSpeed;
#if DEBUGCOSTS
								if (costDebug) {
									logBuf.Add($"Total cost = {deltaCostOverMeanMaxSpeed}, comparison value = {nextItem.m_comparisonValue}");
								}
#endif

								if (nextItem.m_comparisonValue > 1f) {
									// comparison value got too big. Do not add the lane to the buffer
#if DEBUGPF
									if (debug)
										logBuf.Add($"Pathfinder ({this._pathFindIndex}): comparisonValue is >1, NaN or Infinity: {nextItem.m_comparisonValue}. seg. {nextSegmentId}, lane {laneIndex}, off {nextItem.m_position.m_offset} -> {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}.");
#endif
#if DEBUG
									//Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId}, PF {this._pathFindIndex}: Comparison value > 1, NaN or infinity! distanceOverMeanMaxSpeed={distanceOverMeanMaxSpeed}, nextSpeed={nextSpeed}, prevSpeed={prevSpeed}");
#endif
									addItem = false;
								}
#if DEBUGPF
								if (debug) {
									//logBuf.Add($">> PF {this._pathFindIndex} -- seg {item2.m_position.m_segment}, lane {item2.m_position.m_lane} (idx {item2.m_laneID}), off {item2.m_position.m_offset}, cost {item2.m_comparisonValue}, totalCost {totalCost} = traffic={trafficCost}, junction={junctionCost}, lane={laneChangeCost}");
								}
#endif

#if DEBUGPF
								if (debug) {
									logBuf.Add($"addItem={addItem}");

									FlushCostLog(logBuf);
								}
#endif
							}

							if (forceLaneIndex != null && laneIndex == forceLaneIndex && addItem) {
								foundForced = true;
							}

							if (addItem) {
								// NON-STOCK CODE END //

								nextItem.m_lanesUsed = (item.m_lanesUsed | nextLane.m_laneType);
								nextItem.m_laneID = curLaneId;
#if DEBUGPF
								if (debug)
									logBuf.Add($">> PF {this._pathFindIndex} -- Adding item: seg {nextItem.m_position.m_segment}, lane {nextItem.m_position.m_lane} (idx {nextItem.m_laneID}), off {nextItem.m_position.m_offset} -> seg {item.m_position.m_segment}, lane {item.m_position.m_lane} (idx {item.m_laneID}), off {item.m_position.m_offset}, cost {nextItem.m_comparisonValue}, previous cost {item.m_comparisonValue}, methodDist {nextItem.m_methodDistance}");
#endif

								this.AddBufferItem(nextItem, item.m_position);
								// NON-STOCK CODE START //
							} else {
#if DEBUGPF
								if (debug)
									logBuf.Add($">> PF {this._pathFindIndex} -- NOT adding item");
#endif
							}
							// NON-STOCK CODE END //
						}
					}
					goto CONTINUE_LANE_LOOP;
				}

				if ((byte)(nextLane.m_laneType & prevLaneType) != 0 && (nextLane.m_vehicleType & prevVehicleType) != VehicleInfo.VehicleType.None) {
					++newLaneIndexFromInner;
				}

				CONTINUE_LANE_LOOP:

				curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
				++laneIndex;
				continue;
			} // foreach lane
			laneIndexFromInner = newLaneIndexFromInner;

#if DEBUGPF
			if (debug)
				logBuf.Add($"ProcessItemCosts: method finished at end.");

			if (debug)
				FlushCostLog(logBuf);
#endif
			return blocked;
		}

#if DEBUGPF
		private void FlushCostLog(List<String> logBuf) {
			if (logBuf == null)
				return;

			foreach (String toLog in logBuf) {
				Log._Debug($"Pathfinder ({this._pathFindIndex}) *COSTS*: " + toLog);
			}
			logBuf.Clear();
		}

		private void FlushMainLog(List<String> logBuf, uint unitId) {
			if (logBuf == null)
				return;

			foreach (String toLog in logBuf) {
				Log._Debug($"Pathfinder ({this._pathFindIndex}) for unit {unitId} *MAIN*: " + toLog);
			}
			logBuf.Clear();
		}
#endif

		// 4
		private void ProcessItemPedBicycle(BufferItem item, ushort targetNodeId, ushort nextSegmentId, ref NetSegment nextSegment, byte connectOffset, byte laneSwitchOffset, int laneIndex, uint lane) {
			if ((nextSegment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None) {
				return;
			}
			// NON-STOCK CODE START
			// check if pedestrians are not allowed to cross here
			if (!junctionManager.IsPedestrianCrossingAllowed(nextSegmentId, targetNodeId == nextSegment.m_startNode))
				return;

			// check if pedestrian light won't change to green
			CustomSegmentLights lights = customTrafficLightsManager.GetSegmentLights(targetNodeId, nextSegmentId);
			if (lights != null) {
				if (lights.InvalidPedestrianLight) {
					return;
				}
			}
			// NON-STOCK CODE END
			NetManager netManager = Singleton<NetManager>.instance;
			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = netManager.m_segments.m_buffer[(int)item.m_position.m_segment].Info;
			int num = nextSegmentInfo.m_lanes.Length;
			float distance;
			byte offset;
			Vector3 b = netManager.m_lanes.m_buffer[item.m_laneID].CalculatePosition((float)laneSwitchOffset * 0.003921569f);
			if (nextSegmentId == item.m_position.m_segment) {
				// next segment is previous segment
				Vector3 a = netManager.m_lanes.m_buffer[lane].CalculatePosition((float)connectOffset * 0.003921569f);
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
				prevSpeed = this.CalculateLaneSpeed(prevMaxSpeed, laneSwitchOffset, item.m_position.m_offset, ref netManager.m_segments.m_buffer[(int)item.m_position.m_segment], prevLane); // NON-STOCK CODE
			}
			float prevCost = netManager.m_segments.m_buffer[(int)item.m_position.m_segment].m_averageLength;
			// NON-STOCK CODE START
			//if (_extVehicleType == ExtVehicleType.Bicycle) {
				/*if ((vehicleType & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None) {
					prevCost *= 0.95f;
				} else if ((netManager.m_segments.m_buffer[(int)item.m_position.m_segment].m_flags & NetSegment.Flags.BikeBan) != NetSegment.Flags.None) {
					prevCost *= 5f;
				}*/
			//}
			ushort sourceNodeId = (targetNodeId == netManager.m_segments.m_buffer[item.m_position.m_segment].m_startNode) ? netManager.m_segments.m_buffer[item.m_position.m_segment].m_endNode : netManager.m_segments.m_buffer[item.m_position.m_segment].m_startNode; // no lane changing directly in front of a junction
			bool prevIsJunction = (netManager.m_nodes.m_buffer[sourceNodeId].m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
			// NON-STOCK CODE END
			float offsetLength = (float)Mathf.Abs((int)(laneSwitchOffset - item.m_position.m_offset)) * 0.003921569f * prevCost;
			float methodDistance = item.m_methodDistance + offsetLength;
			float comparisonValue = item.m_comparisonValue + offsetLength / (prevSpeed * this._maxLength);
			if (laneIndex < num) {
				NetInfo.Lane nextLane = nextSegmentInfo.m_lanes[laneIndex];
				BufferItem nextItem;
				// NON-STOCK CODE START //
				if (prevIsJunction)
					nextItem.m_numSegmentsToJunction = 0;
				else
					nextItem.m_numSegmentsToJunction = item.m_numSegmentsToJunction + 1;
				// NON-STOCK CODE END //
				nextItem.m_position.m_segment = nextSegmentId;
				nextItem.m_position.m_lane = (byte)laneIndex;
				nextItem.m_position.m_offset = offset;
				if ((byte)(nextLane.m_laneType & laneType) == 0) {
					nextItem.m_methodDistance = 0f;
				} else {
					if (item.m_methodDistance == 0f) {
						comparisonValue += 100f / (0.25f * this._maxLength);
					}
					nextItem.m_methodDistance = methodDistance + distance;
				}
				float nextMaxSpeed = GetLaneSpeedLimit(nextSegmentId, (uint)laneIndex, lane, nextLane); // NON-STOCK CODE
				if (nextLane.m_laneType != NetInfo.LaneType.Pedestrian || nextItem.m_methodDistance < 1000f) {
					nextItem.m_comparisonValue = comparisonValue + distance / ((prevMaxSpeed + nextMaxSpeed) * 0.25f * this._maxLength);
					if ((nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
						nextItem.m_direction = NetInfo.InvertDirection(nextLane.m_finalDirection);
					} else {
						nextItem.m_direction = nextLane.m_finalDirection;
					}
					if (lane == this._startLaneA) {
						if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetA) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetA)) {
							return;
						}
						float nextSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetA, nextItem.m_position.m_offset, ref nextSegment, nextLane); // NON-STOCK CODE
						float nextOffset = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetA)) * 0.003921569f;
						nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * this._maxLength);
					}
					if (lane == this._startLaneB) {
						if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetB) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetB)) {
							return;
						}
						float nextSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLane); // NON-STOCK CODE
						float nextOffset = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetB)) * 0.003921569f;
						nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * this._maxLength);
					}
					nextItem.m_laneID = lane;
					nextItem.m_lanesUsed = (item.m_lanesUsed | nextLane.m_laneType);
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

		private void GetLaneDirection(PathUnit.Position pathPos, out NetInfo.Direction direction, out NetInfo.LaneType type) {
			NetManager instance = Singleton<NetManager>.instance;
			NetInfo info = instance.m_segments.m_buffer[pathPos.m_segment].Info;
			if (info.m_lanes.Length > pathPos.m_lane) {
				direction = info.m_lanes[pathPos.m_lane].m_finalDirection;
				type = info.m_lanes[pathPos.m_lane].m_laneType;
				if ((instance.m_segments.m_buffer[pathPos.m_segment].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
					direction = NetInfo.InvertDirection(direction);
				}
			} else {
				direction = NetInfo.Direction.None;
				type = NetInfo.LaneType.None;
			}
		}

		private void PathFindThread() {
			while (true) {
				//Log.Message($"Pathfind Thread #{Thread.CurrentThread.ManagedThreadId} iteration!");
				try {
					Monitor.Enter(QueueLock);

					while (QueueFirst == 0u && !Terminated) {
#if DEBUGPF
						/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
							Log._Debug($"Pathfind Thread #{Thread.CurrentThread.ManagedThreadId} waiting now for queue lock {QueueLock.GetHashCode()}!");*/
#endif
						Monitor.Wait(QueueLock);
						//if (!Monitor.Wait(QueueLock, SYNC_TIMEOUT)) {
#if DEBUGPF
							/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
								Log.Warning($"Pathfind Thread #{Thread.CurrentThread.ManagedThreadId} *WAIT TIMEOUT* waiting for queue lock {QueueLock.GetHashCode()}!");*/
#endif
						//}
					}

#if DEBUGPF3
					Log._Debug($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread iteration START. QueueFirst={QueueFirst} QueueLast={QueueLast}");
#endif
#if DEBUGPF
					/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
						Log._Debug($"Pathfind Thread #{Thread.CurrentThread.ManagedThreadId} is continuing now!");*/
#endif
					if (Terminated) {
						break;
					}
					Calculating = QueueFirst;
#if DEBUGPF3
					Log._Debug($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread iteration. Setting Calculating=QueueFirst=Calculating. Setting QueueFirst={PathUnits.m_buffer[Calculating].m_nextPathUnit}");
#endif
					QueueFirst = PathUnits.m_buffer[Calculating].m_nextPathUnit;
					if (QueueFirst == 0u) {
						QueueLast = 0u;
						m_queuedPathFindCount = 0;
					} else {
						--m_queuedPathFindCount;
					}
#if DEBUGPF
					/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
						Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PathFindThread: Starting pathfinder. Remaining queued pathfinders: {m_queuedPathFindCount}"); */
#endif
					PathUnits.m_buffer[Calculating].m_nextPathUnit = 0u;
					PathUnits.m_buffer[Calculating].m_pathFindFlags = (byte)((PathUnits.m_buffer[Calculating].m_pathFindFlags & ~PathUnit.FLAG_CREATED) | PathUnit.FLAG_CALCULATING);

#if DEBUGPF3
					Log._Debug($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread iteration END. QueueFirst={QueueFirst} QueueLast={QueueLast} Calculating={Calculating} flags={PathUnits.m_buffer[Calculating].m_pathFindFlags}");
#endif
				} catch (Exception e) {
					Log.Error($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread Error for unit {Calculating}, flags={PathUnits.m_buffer[Calculating].m_pathFindFlags} (1): {e.ToString()}");
				} finally {
					Monitor.Exit(QueueLock);
				}
				//tCurrentState = 7;
				try {
#if DEBUGPF
					m_pathfindProfiler.BeginStep();
#endif
#if DEBUGPF
					/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
						Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Calling PathFindImplementation now. Calculating={Calculating}");*/
#endif
					PathFindImplementation(Calculating, ref PathUnits.m_buffer[Calculating]);
				} catch (Exception ex) {
					Log.Error($"(PF #{_pathFindIndex}, T#{Thread.CurrentThread.ManagedThreadId}, Id #{pfId}) CustomPathFind.PathFindThread Error for unit {Calculating}, flags={PathUnits.m_buffer[Calculating].m_pathFindFlags} (2): {ex.ToString()}");
					//UIView.ForwardException(ex);

#if DEBUG
					++_failedPathFinds;
#endif
					pathUnitExtVehicleType[Calculating] = null;
					pathUnitVehicleIds[Calculating] = null;

					PathUnits.m_buffer[Calculating].m_pathFindFlags |= PathUnit.FLAG_FAILED;
				} finally {
#if DEBUGPF
					m_pathfindProfiler.EndStep();
#endif
#if DEBUGPF
					/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
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

			if (_extVehicleType == null || _extVehicleType == ExtVehicleType.None)
				return true;

			if (laneInfo == null)
				laneInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex];

			if ((laneInfo.m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train)) == VehicleInfo.VehicleType.None)
				return true;

			ExtVehicleType allowedTypes = vehicleRestrictionsManager.GetAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo);
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
