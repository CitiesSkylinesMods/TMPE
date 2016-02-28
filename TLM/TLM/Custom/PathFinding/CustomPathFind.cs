#define DEBUGPFx
#define DEBUGPF2x
#define DEBUGLOCKSx
#define DEBUGCOSTSx

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using TrafficManager.Traffic;
using UnityEngine;
using System.Collections.Generic;
using TrafficManager.Custom.AI;
using TrafficManager.TrafficLight;
using TrafficManager.State;

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

		public readonly static int SYNC_TIMEOUT = 10;

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
		private bool _transportVehicle;
		private ExtVehicleType? _extVehicleType;
#if DEBUG
		public uint _failedPathFinds = 0;
		public uint _succeededPathFinds = 0;
#endif
		private Randomizer _pathRandomizer;
		private uint _pathFindIndex;
		private NetInfo.LaneType _laneTypes;
		private VehicleInfo.VehicleType _vehicleTypes;

		/*public int //pfCurrentState = 0;
		public int //tCurrentState = 0;
		public int //mCurrentState = 0;
		public int //sCurrentState = 0;
		public int //bCurrentState = 0;*/

		public bool IsMasterPathFind = false;

		internal ExtVehicleType?[] pathUnitExtVehicleType = null;

		protected virtual void Awake() {
			var stockPathFindType = typeof(PathFind);
			const BindingFlags fieldFlags = BindingFlags.NonPublic | BindingFlags.Instance;

			_fieldpathUnits = stockPathFindType.GetField("m_pathUnits", fieldFlags);
			_fieldQueueFirst = stockPathFindType.GetField("m_queueFirst", fieldFlags);
			_fieldQueueLast = stockPathFindType.GetField("m_queueLast", fieldFlags);
			_fieldQueueLock = stockPathFindType.GetField("m_queueLock", fieldFlags);
			_fieldTerminated = stockPathFindType.GetField("m_terminated", fieldFlags);
			_fieldCalculating = stockPathFindType.GetField("m_calculating", fieldFlags);
			_fieldPathFindThread = stockPathFindType.GetField("m_pathFindThread", fieldFlags);

			_buffer = new BufferItem[65536];
			_bufferLock = PathManager.instance.m_bufferLock;
			PathUnits = PathManager.instance.m_pathUnits;
			QueueLock = new object();
			_laneLocation = new uint[262144];
			_laneTarget = new PathUnit.Position[262144];
			_bufferMin = new int[1024];
			_bufferMax = new int[1024];

			if (pathUnitExtVehicleType == null)
				pathUnitExtVehicleType = new ExtVehicleType?[PathUnits.m_size];

			m_pathfindProfiler = new ThreadProfiler();
			CustomPathFindThread = new Thread(PathFindThread) { Name = "Pathfind" };
			CustomPathFindThread.Priority = SimulationManager.SIMULATION_PRIORITY;
			CustomPathFindThread.Start();
			if (!CustomPathFindThread.IsAlive) {
				//CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find thread failed to start!");
				Log.Error("Path find thread failed to start!");
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

		public bool CalculatePath(ExtVehicleType vehicleType, uint unit, bool skipQueue) {
			if (Singleton<PathManager>.instance.AddPathReference(unit)) {
				try {
					Monitor.Enter(QueueLock);
					if (skipQueue) {
						if (this.QueueLast == 0u) {
							this.QueueLast = unit;
						} else {
							this.PathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit = this.QueueFirst;
						}
						this.QueueFirst = unit;
					} else {
						if (this.QueueLast == 0u) {
							this.QueueFirst = unit;
						} else {
							this.PathUnits.m_buffer[(int)((UIntPtr)this.QueueLast)].m_nextPathUnit = unit;
						}
						this.QueueLast = unit;
					}
					this.PathUnits.m_buffer[unit].m_pathFindFlags |= 1;
					this.m_queuedPathFindCount++;
					pathUnitExtVehicleType[unit] = vehicleType;
					Monitor.Pulse(this.QueueLock);
				} finally {
					Monitor.Exit(this.QueueLock);
				}
				return true;
			}
			return false;
		}

		// PathFind
		protected void PathFindImplementation(uint unit, ref PathUnit data) {
			//pfCurrentState = 0;

			NetManager instance = Singleton<NetManager>.instance;
			this._laneTypes = (NetInfo.LaneType)this.PathUnits.m_buffer[(int)((UIntPtr)unit)].m_laneTypes;
			this._vehicleTypes = (VehicleInfo.VehicleType)this.PathUnits.m_buffer[(int)((UIntPtr)unit)].m_vehicleTypes;
			this._maxLength = this.PathUnits.m_buffer[(int)((UIntPtr)unit)].m_length;
			this._pathFindIndex = (this._pathFindIndex + 1u & 32767u);
			this._pathRandomizer = new Randomizer(unit);
			this._isHeavyVehicle = ((this.PathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags & 16) != 0);
			this._ignoreBlocked = ((this.PathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags & 32) != 0);
			this._stablePath = ((this.PathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags & 64) != 0);
			this._transportVehicle = ((byte)(this._laneTypes & NetInfo.LaneType.TransportVehicle) != 0);
			this._extVehicleType = pathUnitExtVehicleType[unit];
			if ((byte)(this._laneTypes & NetInfo.LaneType.Vehicle) != 0) {
				this._laneTypes |= NetInfo.LaneType.TransportVehicle;
			}
			int num = (int)(this.PathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount & 15);
			int num2 = this.PathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount >> 4;
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
				bufferItemEndA.m_methodDistance = 0f;
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
				bufferItemEndB.m_methodDistance = 0f;
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
				uint num3 = 4294901760u;
				for (int i = 0; i < 262144; i++) {
					this._laneLocation[i] = num3;
				}
			}
			for (int j = 0; j < 1024; j++) {
				this._bufferMin[j] = 0;
				this._bufferMax[j] = -1;
			}
			if (bufferItemEndA.m_position.m_segment != 0) {
				this._bufferMax[0]++;
				this._buffer[++this._bufferMaxPos] = bufferItemEndA;
			}
			if (bufferItemEndB.m_position.m_segment != 0) {
				this._bufferMax[0]++;
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
				int num4 = this._bufferMin[this._bufferMinPos];
				int num5 = this._bufferMax[this._bufferMinPos];
				if (num4 > num5) {
					this._bufferMinPos++;
				} else {
					this._bufferMin[this._bufferMinPos] = num4 + 1;
					BufferItem candidateItem = this._buffer[(this._bufferMinPos << 6) + num4];
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
						ushort startNode = instance.m_segments.m_buffer[(int)candidateItem.m_position.m_segment].m_startNode;
						this.ProcessItemMain(unit, candidateItem, startNode, ref instance.m_nodes.m_buffer[(int)startNode], 0, false);
					}

					if ((byte)(candidateItem.m_direction & NetInfo.Direction.Backward) != 0) {
						ushort endNode = instance.m_segments.m_buffer[(int)candidateItem.m_position.m_segment].m_endNode;
						this.ProcessItemMain(unit, candidateItem, endNode, ref instance.m_nodes.m_buffer[(int)endNode], 255, false);
					}

					// handle special nodes (e.g. bus stops)
					int num6 = 0;
					ushort specialNodeId = instance.m_lanes.m_buffer[(int)((UIntPtr)candidateItem.m_laneID)].m_nodes;
					if (specialNodeId != 0) {
						ushort startNode2 = instance.m_segments.m_buffer[(int)candidateItem.m_position.m_segment].m_startNode;
						ushort endNode2 = instance.m_segments.m_buffer[(int)candidateItem.m_position.m_segment].m_endNode;
						bool flag2 = ((instance.m_nodes.m_buffer[(int)startNode2].m_flags | instance.m_nodes.m_buffer[(int)endNode2].m_flags) & NetNode.Flags.Disabled) != NetNode.Flags.None;
						while (specialNodeId != 0) {
							NetInfo.Direction direction = NetInfo.Direction.None;
							byte laneOffset = instance.m_nodes.m_buffer[(int)specialNodeId].m_laneOffset;
							if (laneOffset <= candidateItem.m_position.m_offset) {
								direction |= NetInfo.Direction.Forward;
							}
							if (laneOffset >= candidateItem.m_position.m_offset) {
								direction |= NetInfo.Direction.Backward;
							}
							if ((byte)(candidateItem.m_direction & direction) != 0 && (!flag2 || (instance.m_nodes.m_buffer[(int)specialNodeId].m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None)) {
								this.ProcessItemMain(unit, candidateItem, specialNodeId, ref instance.m_nodes.m_buffer[(int)specialNodeId], laneOffset, true);
							}
							specialNodeId = instance.m_nodes.m_buffer[(int)specialNodeId].m_nextLaneNode;
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
			//pfCurrentState = 2;
			/*if (m_queuedPathFindCount > 100 && Options.disableSomething0)
				Log.Message($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: MAIN LOOP FINISHED! bufferMinPos: {this._bufferMinPos}, bufferMaxPos: {this._bufferMaxPos}, startA: {bufferItemStartA.m_position.m_segment}, startB: {bufferItemStartB.m_position.m_segment}, endA: {bufferItemEndA.m_position.m_segment}, endB: {bufferItemEndB.m_position.m_segment}");*/
			if (!canFindPath) {
				// we could not find a path
				//pfCurrentState = 3;
				PathUnits.m_buffer[(int)unit].m_pathFindFlags = (byte)(PathUnits.m_buffer[(int)((UIntPtr)unit)].m_pathFindFlags | 8);
#if DEBUG
				++_failedPathFinds;
				//Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Cannot find path (pfCurrentState={pfCurrentState}) for unit {unit}");
#endif
				pathUnitExtVehicleType[unit] = null;

				/*PathUnit[] expr_909_cp_0 = this._pathUnits.m_buffer;
				UIntPtr expr_909_cp_1 = (UIntPtr)unit;
				expr_909_cp_0[(int)expr_909_cp_1].m_pathFindFlags = (byte)(expr_909_cp_0[(int)expr_909_cp_1].m_pathFindFlags | 8);*/
				return;
			}
			// we could calculate a valid path

			//pfCurrentState = 4;
			float totalPathLength = finalBufferItem.m_comparisonValue * this._maxLength;
			this.PathUnits.m_buffer[(int)((UIntPtr)unit)].m_length = totalPathLength;
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
					this.PathUnits.m_buffer[(int)((UIntPtr)currentPathUnitId)].SetPosition(currentItemPositionCount++, position2);
					// now we have: [desired starting position]
				}
				// add the found starting position to the path unit
				this.PathUnits.m_buffer[(int)((UIntPtr)currentPathUnitId)].SetPosition(currentItemPositionCount++, currentPosition);
				currentPosition = this._laneTarget[(int)((UIntPtr)finalBufferItem.m_laneID)]; // go to the next path position

				// now we have either [desired starting position, found starting position] or [found starting position], depending on if the found starting position matched the desired
			}
			//pfCurrentState = 5;

			// beginning with the starting position, going to the target position: assemble the path units
			for (int k = 0; k < 262144; k++) {
				//pfCurrentState = 6;
				this.PathUnits.m_buffer[(int)((UIntPtr)currentPathUnitId)].SetPosition(currentItemPositionCount++, currentPosition); // add the next path position to the current unit

				if ((currentPosition.m_segment == bufferItemEndA.m_position.m_segment && currentPosition.m_lane == bufferItemEndA.m_position.m_lane && currentPosition.m_offset == bufferItemEndA.m_position.m_offset) ||
					(currentPosition.m_segment == bufferItemEndB.m_position.m_segment && currentPosition.m_lane == bufferItemEndB.m_position.m_lane && currentPosition.m_offset == bufferItemEndB.m_position.m_offset)) {
					// we have reached the end position

					this.PathUnits.m_buffer[(int)((UIntPtr)currentPathUnitId)].m_positionCount = (byte)currentItemPositionCount;
					sumOfPositionCounts += currentItemPositionCount; // add position count of last unit to sum
					if (sumOfPositionCounts != 0) {
						// for each path unit from start to target: calculate length (distance) to target
						currentPathUnitId = this.PathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit; // (we do not need to calculate the length for the starting unit since this is done before; it's the total path length)
						currentItemPositionCount = (int)this.PathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount;
						int totalIter = 0;
						while (currentPathUnitId != 0u) {
							//pfCurrentState = 7;
							this.PathUnits.m_buffer[(int)((UIntPtr)currentPathUnitId)].m_length = totalPathLength * (float)(sumOfPositionCounts - currentItemPositionCount) / (float)sumOfPositionCounts;
							currentItemPositionCount += (int)this.PathUnits.m_buffer[(int)((UIntPtr)currentPathUnitId)].m_positionCount;
							currentPathUnitId = this.PathUnits.m_buffer[(int)((UIntPtr)currentPathUnitId)].m_nextPathUnit;
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
					PathUnits.m_buffer[(int)unit].m_pathFindFlags = (byte)(PathUnits.m_buffer[(int)((UIntPtr)unit)].m_pathFindFlags | 4); // Path found
#if DEBUG
					++_succeededPathFinds;
					pathUnitExtVehicleType[unit] = null;
#endif
					return;
				}

				// We have not reached the target position yet 
				if (currentItemPositionCount == 12) {
					// the current path unit is full, we need a new one
					//pfCurrentState = 8;
					uint createdPathUnitId;
					try {
						Monitor.Enter(this._bufferLock);
						//pfCurrentState = 10;
						if (!this.PathUnits.CreateItem(out createdPathUnitId, ref this._pathRandomizer)) {
							// we failed to create a new path unit, thus the path-finding also failed
							PathUnits.m_buffer[(int)((UIntPtr)unit)].m_pathFindFlags = (byte)(PathUnits.m_buffer[(int)unit].m_pathFindFlags | 8);
#if DEBUG
							++_failedPathFinds;
							//Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Cannot find path (pfCurrentState={pfCurrentState}) for unit {unit}");
#endif
							pathUnitExtVehicleType[unit] = null;
							return;
						}
						this.PathUnits.m_buffer[(int)((UIntPtr)createdPathUnitId)] = this.PathUnits.m_buffer[(int)currentPathUnitId];
						this.PathUnits.m_buffer[(int)((UIntPtr)createdPathUnitId)].m_referenceCount = 1;
						this.PathUnits.m_buffer[(int)((UIntPtr)createdPathUnitId)].m_pathFindFlags = 4;
						this.PathUnits.m_buffer[(int)((UIntPtr)currentPathUnitId)].m_nextPathUnit = createdPathUnitId;
						this.PathUnits.m_buffer[(int)((UIntPtr)currentPathUnitId)].m_positionCount = (byte)currentItemPositionCount;
						sumOfPositionCounts += currentItemPositionCount;
						Singleton<PathManager>.instance.m_pathUnitCount = (int)(this.PathUnits.ItemCount() - 1u);
					} catch (Exception e) {
						Log.Error("CustomPathFind.PathFindImplementation Error: " + e.ToString());
						break;
					} finally {
						Monitor.Exit(this._bufferLock);
					}
					currentPathUnitId = createdPathUnitId;
					currentItemPositionCount = 0;
				}
				uint laneID = PathManager.GetLaneID(currentPosition);
				// NON-STOCK CODE START
				CustomRoadAI.AddTraffic(laneID, (ushort)(this._isHeavyVehicle || _extVehicleType == ExtVehicleType.Bus ? 50 : 25), (ushort)GetLaneSpeedLimit(currentPosition.m_segment, currentPosition.m_lane, laneID, Singleton<NetManager>.instance.m_segments.m_buffer[currentPosition.m_segment].Info.m_lanes[currentPosition.m_lane]), false); //SpeedLimitManager.GetLockFreeGameSpeedLimit(currentPosition.m_segment, currentPosition.m_lane, laneID, ref Singleton<NetManager>.instance.m_segments.m_buffer[currentPosition.m_segment].Info.m_lanes[currentPosition.m_lane])
				// NON-STOCK CODE END
				currentPosition = this._laneTarget[(int)((UIntPtr)laneID)];
			}
			PathUnits.m_buffer[(int)((UIntPtr)unit)].m_pathFindFlags = (byte)(PathUnits.m_buffer[(int)unit].m_pathFindFlags | 8);
#if DEBUG
			++_failedPathFinds;
#endif
			pathUnitExtVehicleType[unit] = null;
#if DEBUG
			//Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Cannot find path (pfCurrentState={pfCurrentState}) for unit {unit}");
#endif
		}
#endregion

		// be aware:
		//   (1) path-finding works from target to start. the "next" segment is always the previous and the "previous" segment is always the next segment on the path!
		//   (2) when I use the term "lane index from right" this holds for right-hand traffic systems. On maps where you activate left-hand traffic, the "lane index from right" values represent lane indices starting from the left side.

		// 1
		private void ProcessItemMain(uint unitId, BufferItem item, ushort nextNodeId, ref NetNode nextNode, byte connectOffset, bool isMiddle) {
			//mCurrentState = 0;
#if DEBUGPF
			//bool debug = Options.disableSomething1 && item.m_position.m_segment == 1459 && nextNodeId == 19630;
			//bool debug = Options.disableSomething1 && (item.m_position.m_segment == 3833 || item.m_position.m_segment == 9649);
			bool debug = false;// Options.disableSomething1;
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
			bool debug2 = Options.disableSomething1 && _extVehicleType == ExtVehicleType.Bicycle;
			List<String> logBuf2 = null;
			if (debug2)
				logBuf2 = new List<String>();
#endif
			//mCurrentState = 1;
			NetManager instance = Singleton<NetManager>.instance;
			bool isPedestrianLane = false;
			bool isBicycleLane = false;
			bool isCenterPlatform = false;
			int similarLaneIndexFromLeft = 0; // similar index, starting with 0 at leftmost lane
			NetInfo prevSegmentInfo = instance.m_segments.m_buffer[(int)item.m_position.m_segment].Info;
			int prevSimiliarLaneCount = 0;
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane prevLane = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				isPedestrianLane = (prevLane.m_laneType == NetInfo.LaneType.Pedestrian);
				isBicycleLane = (prevLane.m_laneType == NetInfo.LaneType.Vehicle && (prevLane.m_vehicleType & this._vehicleTypes) == VehicleInfo.VehicleType.Bicycle);
				isCenterPlatform = prevLane.m_centerPlatform;
				prevSimiliarLaneCount = prevLane.m_similarLaneCount;
				if ((byte)(prevLane.m_finalDirection & NetInfo.Direction.Forward) != 0) {
					similarLaneIndexFromLeft = prevLane.m_similarLaneIndex;
				} else {
					similarLaneIndexFromLeft = prevLane.m_similarLaneCount - prevLane.m_similarLaneIndex - 1;
				}
			}
			int firstSimilarLaneIndexFromLeft = similarLaneIndexFromLeft;
			ushort prevSegmentId = item.m_position.m_segment;
			if (isMiddle) {
				for (int i = 0; i < 8; i++) {
					ushort nextSegmentId = nextNode.GetSegment(i);
					if (nextSegmentId <= 0)
						continue;
					this.ProcessItemCosts(false, debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, !isPedestrianLane, isPedestrianLane);
				}
			} else if (isPedestrianLane) {
				int prevLaneIndex = (int)item.m_position.m_lane;
				if (nextNode.Info.m_class.m_service != ItemClass.Service.Beautification) {
					bool isEndBendOrJunction = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None;
					bool isOnCenterPlatform = isCenterPlatform && (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Junction)) == NetNode.Flags.None;
					ushort nextLeftSegment = prevSegmentId;
					ushort nextRightSegment = prevSegmentId;
					int leftLaneIndex;
					int rightLaneIndex;
					uint leftLaneId;
					uint rightLaneId;
					instance.m_segments.m_buffer[(int)prevSegmentId].GetLeftAndRightLanes(nextNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, prevLaneIndex, isOnCenterPlatform, out leftLaneIndex, out rightLaneIndex, out leftLaneId, out rightLaneId);
					if (leftLaneId == 0u || rightLaneId == 0u) {
						ushort leftSegment;
						ushort rightSegment;
						instance.m_segments.m_buffer[(int)prevSegmentId].GetLeftAndRightSegments(nextNodeId, out leftSegment, out rightSegment);
						int numIter = 0;
						while (leftSegment != 0 && leftSegment != prevSegmentId && leftLaneId == 0u) {
							int someLeftLaneIndex;
							int someRightLaneIndex;
							uint someLeftLaneId;
							uint someRightLaneId;
							instance.m_segments.m_buffer[(int)leftSegment].GetLeftAndRightLanes(nextNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, isOnCenterPlatform, out someLeftLaneIndex, out someRightLaneIndex, out someLeftLaneId, out someRightLaneId);
							if (someRightLaneId != 0u) {
								nextLeftSegment = leftSegment;
								leftLaneIndex = someRightLaneIndex;
								leftLaneId = someRightLaneId;
							} else {
								leftSegment = instance.m_segments.m_buffer[(int)leftSegment].GetLeftSegment(nextNodeId);
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
							instance.m_segments.m_buffer[(int)rightSegment].GetLeftAndRightLanes(nextNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, isOnCenterPlatform, out someLeftLaneIndex, out someRightLaneIndex, out someLeftLaneId, out someRightLaneId);
							if (someLeftLaneId != 0u) {
								nextRightSegment = rightSegment;
								rightLaneIndex = someLeftLaneIndex;
								rightLaneId = someLeftLaneId;
							} else {
								rightSegment = instance.m_segments.m_buffer[(int)rightSegment].GetRightSegment(nextNodeId);
							}
							if (++numIter == 8) {
								break;
							}
						}
						//mCurrentState = 8;
					}
					if (leftLaneId != 0u && (nextLeftSegment != prevSegmentId || isEndBendOrJunction || isOnCenterPlatform)) {
#if DEBUGPF2
						if (debug2)
							logBuf2.Add($"Exploring path! Segment {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset} @ node {nextNodeId}: going ped1, seg. {nextLeftSegment}, off {connectOffset}, lane idx {leftLaneIndex}, id {leftLaneId}");
#endif
						this.ProcessItemPedBicycle(item, nextNodeId, nextLeftSegment, ref instance.m_segments.m_buffer[(int)nextLeftSegment], connectOffset, leftLaneIndex, leftLaneId); // ped
					}
					if (rightLaneId != 0u && rightLaneId != leftLaneId && (nextRightSegment != prevSegmentId || isEndBendOrJunction || isOnCenterPlatform)) {
#if DEBUGPF2
						if (debug2)
							logBuf2.Add($"Exploring path! Segment {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset} @ node {nextNodeId}: going ped2, seg. {nextRightSegment}, off {connectOffset}, lane idx {rightLaneIndex}, id {rightLaneId}");
#endif
						this.ProcessItemPedBicycle(item, nextNodeId, nextRightSegment, ref instance.m_segments.m_buffer[(int)nextRightSegment], connectOffset, rightLaneIndex, rightLaneId); // ped
					}
					int nextLaneIndex;
					uint nextLaneId;
					if ((this._vehicleTypes & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None &&
						instance.m_segments.m_buffer[(int)prevSegmentId].GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Bicycle, out nextLaneIndex, out nextLaneId)) {
#if DEBUGPF2
						if (debug2)
							logBuf2.Add($"Exploring path! Segment {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset} @ node {nextNodeId}: going bike, seg. {prevSegmentId}, off {connectOffset}, lane idx {nextLaneIndex}, id {nextLaneId}");
#endif
						this.ProcessItemPedBicycle(item, nextNodeId, prevSegmentId, ref instance.m_segments.m_buffer[(int)prevSegmentId], connectOffset, nextLaneIndex, nextLaneId); // bicycle
					}
				} else {
					//mCurrentState = 9;
					for (int j = 0; j < 8; j++) {
						ushort nextSegmentId = nextNode.GetSegment(j);
						if (nextSegmentId != 0 && nextSegmentId != prevSegmentId) {
#if DEBUGPF2
							if (debug2)
								logBuf2.Add($"Exploring path! Segment {item.m_position.m_segment} @ node {nextNodeId}: going beauty1, seg. {nextSegmentId}, off {connectOffset}");
#endif
							this.ProcessItemCosts(false, debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, false, true);
						}
					}
					//mCurrentState = 10;
				}
				//mCurrentState = 11;
				NetInfo.LaneType laneType = this._laneTypes & ~NetInfo.LaneType.Pedestrian;
				VehicleInfo.VehicleType vehicleType = this._vehicleTypes & ~VehicleInfo.VehicleType.Bicycle;
				if ((byte)(item.m_lanesUsed & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
					laneType &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
				int nextLaneIndex2;
				uint nextlaneId2;
				if (laneType != NetInfo.LaneType.None &&
					vehicleType != VehicleInfo.VehicleType.None &&
					instance.m_segments.m_buffer[(int)prevSegmentId].GetClosestLane(prevLaneIndex, laneType, vehicleType, out nextLaneIndex2, out nextlaneId2)) {
					NetInfo.Lane lane5 = prevSegmentInfo.m_lanes[nextLaneIndex2];
					byte connectOffset2;
					if ((instance.m_segments.m_buffer[(int)prevSegmentId].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None == ((byte)(lane5.m_finalDirection & NetInfo.Direction.Backward) != 0)) {
						connectOffset2 = 1;
					} else {
						connectOffset2 = 254;
					}
					//mCurrentState = 12;
#if DEBUGPF2
					if (debug2)
						logBuf2.Add($"Exploring path! Segment {item.m_position.m_segment} @ node {nextNodeId}: going beauty2, seg. {prevSegmentId}, off {connectOffset}, lane idx {nextLaneIndex2}, id {nextlaneId2}");
#endif
					this.ProcessItemPedBicycle(item, nextNodeId, prevSegmentId, ref instance.m_segments.m_buffer[(int)prevSegmentId], connectOffset2, nextLaneIndex2, nextlaneId2); // ped
					//mCurrentState = 13;
				}
			} else {
				//mCurrentState = 14;
				bool mayTurnAround = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
				bool pedestrianAllowed = (byte)(this._laneTypes & NetInfo.LaneType.Pedestrian) != 0;
				bool enablePedestrian = false;
				byte nextConnectOffset = 0;
				if (pedestrianAllowed) {
					if (isBicycleLane) {
						nextConnectOffset = connectOffset;
						enablePedestrian = (nextNode.Info.m_class.m_service == ItemClass.Service.Beautification);
					} else if (this._vehicleLane != 0u) {
						if (this._vehicleLane != item.m_laneID) {
							pedestrianAllowed = false;
						} else {
							nextConnectOffset = this._vehicleOffset;
						}
					} else if (this._stablePath) {
						nextConnectOffset = 128;
					} else {
						nextConnectOffset = (byte)this._pathRandomizer.UInt32(1u, 254u);
					}
				}
				//mCurrentState = 15;

				// NON-STOCK CODE START //
#if DEBUGPF
				if (debug)
					logBuf.Add($"Exploring path! Segment {item.m_position.m_segment} @ node {nextNodeId}: Preparation started");
#endif

				CustomPathManager pathManager = Singleton<CustomPathManager>.instance;
				bool nextIsJunction = (nextNode.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
				bool nextIsRealJunction = nextNode.CountSegments() > 2;
				bool nextIsTransition = (nextNode.m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;
				bool prevIsHighway = false;
				if (prevSegmentInfo.m_netAI is RoadBaseAI)
					prevIsHighway = ((RoadBaseAI)prevSegmentInfo.m_netAI).m_highwayRules;
				//mCurrentState = 16;
				NetInfo.Direction normDirection = TrafficPriority.IsLeftHandDrive() ? NetInfo.Direction.Forward : NetInfo.Direction.Backward; // direction to normalize indices to
				int prevRightSimilarLaneIndex;
				int prevLeftSimilarLaneIndex;

				NetInfo.Lane lane = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				if ((byte)(lane.m_direction & normDirection) != 0) {
					prevRightSimilarLaneIndex = lane.m_similarLaneIndex;
					prevLeftSimilarLaneIndex = lane.m_similarLaneCount - lane.m_similarLaneIndex - 1;
				} else {
					prevRightSimilarLaneIndex = lane.m_similarLaneCount - lane.m_similarLaneIndex - 1;
					prevLeftSimilarLaneIndex = lane.m_similarLaneIndex;
				}
				bool foundForced = false;
				int totalIncomingLanes = 0;
				int totalOutgoingLanes = 0;
				bool isStrictLaneArrowPolicyEnabled = IsLaneArrowChangerEnabled() && _extVehicleType != ExtVehicleType.Emergency && (nextIsJunction || nextIsTransition) && !(Options.allRelaxed || (Options.relaxedBusses && _transportVehicle)) && (this._vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None;
				//mCurrentState = 17;
				// geometries are validated here
#if DEBUGPF
				/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
					Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Getting segment geometry of {prevSegmentId} @ {nextNodeId} START");*/
#endif
				SegmentGeometry geometry = IsMasterPathFind ? CustomRoadAI.GetSegmentGeometry(prevSegmentId, nextNodeId) : CustomRoadAI.GetSegmentGeometry(prevSegmentId);
#if DEBUGPF
				/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
					Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Getting segment geometry of {prevSegmentId} @ {nextNodeId} END");*/
#endif
				//mCurrentState = 18;
				bool prevIsOutgoingOneWay = geometry.IsOutgoingOneWay(nextNodeId);

				//mCurrentState = 19;
#if DEBUGPF
				/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
					Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Verifying segment geometry of {prevSegmentId} @ {nextNodeId} START");*/
#endif
				bool nextAreOnlyOneWayHighways = true;
				for (int k = 0; k < 8; k++) {
					ushort nextSegId = instance.m_nodes.m_buffer[nextNodeId].GetSegment(k);
					if (nextSegId == 0 || nextSegId == prevSegmentId) {
						continue;
					}

					if (IsMasterPathFind) {
						geometry.VerifyConnectedSegment(nextSegId);
					}

					if (instance.m_segments.m_buffer[nextSegId].Info.m_netAI is RoadBaseAI) {
						if (!CustomRoadAI.GetSegmentGeometry(nextSegId).IsOneWay() || !((RoadBaseAI)instance.m_segments.m_buffer[nextSegId].Info.m_netAI).m_highwayRules) {
							nextAreOnlyOneWayHighways = false;
							break;
						}
					} else {
						nextAreOnlyOneWayHighways = false;
						break;
					}
				}
				//mCurrentState = 20;
				
#if DEBUGPF
				/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
					Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Verifying segment geometry of {prevSegmentId} @ {nextNodeId} END");*/
#endif

				ushort[] incomingStraightSegmentsArray = null;
				ushort[] incomingRightSegmentsArray = null;
				ushort[] incomingLeftSegmentsArray = null;
				bool startNode = instance.m_segments.m_buffer[(int)prevSegmentId].m_startNode == nextNodeId;
				if (isStrictLaneArrowPolicyEnabled) {
					if (startNode) {
						incomingStraightSegmentsArray = geometry.StartNodeIncomingStraightSegmentsArray;
						incomingLeftSegmentsArray = geometry.StartNodeIncomingLeftSegmentsArray;
						incomingRightSegmentsArray = geometry.StartNodeIncomingRightSegmentsArray;
					} else {
						incomingStraightSegmentsArray = geometry.EndNodeIncomingStraightSegmentsArray;
						incomingLeftSegmentsArray = geometry.EndNodeIncomingLeftSegmentsArray;
						incomingRightSegmentsArray = geometry.EndNodeIncomingRightSegmentsArray;
					}
				}

				//mCurrentState = 21
				bool explorePrevSegment = Flags.getUTurnAllowed(prevSegmentId, startNode) && !Options.isStockLaneChangerUsed() && nextIsJunction && !prevIsHighway && !prevIsOutgoingOneWay && (_extVehicleType != null && (_extVehicleType & ExtVehicleType.RoadVehicle) != ExtVehicleType.None);
				ushort nextSegmentId = explorePrevSegment ? prevSegmentId : instance.m_segments.m_buffer[prevSegmentId].GetRightSegment(nextNodeId);
#if DEBUGPF
					if (debug)
						logBuf.Add($"Exploring path! Segment {item.m_position.m_segment} @ node {nextNodeId}: Preparation ended");
#endif

#if DEBUGPF
					if (debug)
						logBuf.Add($"pathfind @ node {nextNodeId}: Path from {nextSegmentId} to {prevSegmentId}.");
#endif
#if DEBUGPF
					if (debug)
						logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: Custom part started");
#endif
				// NON-STOCK CODE END //
				//mCurrentState = 22;
				for (int k = 0; k < 8; k++) {
#if DEBUGPF
					if (debug)
						logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: Segment Iteration {k}. nextSegmentId={nextSegmentId}");
#endif
					// NON-STOCK CODE START //
					int outgoingVehicleLanes = 0;
					int incomingVehicleLanes = 0;
					bool couldFindCustomPath = false;

					if (nextSegmentId == 0) {
						break;
					}
					if (!explorePrevSegment && nextSegmentId == prevSegmentId) {
						break;
					}

					//mCurrentState = 23;
					bool nextIsHighway = false;
					if (instance.m_segments.m_buffer[nextSegmentId].Info.m_netAI is RoadBaseAI)
						nextIsHighway = ((RoadBaseAI)instance.m_segments.m_buffer[nextSegmentId].Info.m_netAI).m_highwayRules;
					bool applyHighwayRules = Options.highwayRules && nextAreOnlyOneWayHighways && prevIsOutgoingOneWay && prevIsHighway && nextIsRealJunction;
					bool applyHighwayRulesAtSegment = applyHighwayRules;
					bool isUntouchable = (instance.m_segments.m_buffer[nextSegmentId].m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None;
					if (!isStrictLaneArrowPolicyEnabled || isUntouchable) {
#if DEBUGPF
						if (debug)
							logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: strict lane arrow policy disabled. ({nextIsJunction} || {nextIsTransition}) && !({Options.allRelaxed} || ({Options.relaxedBusses} && {_transportVehicle})) && {(this._vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None}");
#endif

							// NON-STOCK CODE END //
							//mCurrentState = 24;
						if (ProcessItemCosts(true, debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian)) {
							mayTurnAround = true;
						}
						//mCurrentState = 25;
						// NON-STOCK CODE START //
						couldFindCustomPath = true; // not of interest
					} else if (!enablePedestrian) {
						//mCurrentState = 26;

						if ((_vehicleTypes & ~VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
							// handle non-car paths

#if DEBUGPF
							if (debug)
								logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: Handling everything that is not a car: {this._vehicleTypes}");
#endif

							_vehicleTypes &= ~VehicleInfo.VehicleType.Car;
							if (ProcessItemCosts(false, debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian)) {
								mayTurnAround = true;
							}
							_vehicleTypes |= VehicleInfo.VehicleType.Car;
						}
#if DEBUGPF
						if (debug)
							logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: !enablePedestrian");
#endif

						//try {
						var nextSegmentInfo = instance.m_segments.m_buffer[nextSegmentId].Info;
						bool isIncomingRight = false;
						bool isIncomingStraight = false;
						bool isIncomingLeft = false;
						bool isIncomingTurn = false;

						if (nextSegmentId != prevSegmentId) {
							for (int j = 0; j < 7; ++j) {
								if (incomingRightSegmentsArray[j] == nextSegmentId)
									isIncomingRight = true;
								if (incomingLeftSegmentsArray[j] == nextSegmentId)
									isIncomingLeft = true;
								if (incomingStraightSegmentsArray[j] == nextSegmentId)
									isIncomingStraight = true;
							}
						} else {
							isIncomingTurn = true;
						}

						// we need outgoing lanes too!
						if (!isIncomingTurn && !isIncomingLeft && !isIncomingRight && !isIncomingStraight) {
#if DEBUGPF
							if (debug)
								logBuf.Add($"(PFWARN) Segment {nextSegmentId} is neither incoming left, right or straight segment @ {nextNodeId}, going to segment {prevSegmentId}");
#endif
							// recalculate geometry if segment is unknown
							geometry.VerifyConnectedSegment(nextSegmentId);

							if (!applyHighwayRulesAtSegment) {
								couldFindCustomPath = true; // not of interest
								goto nextIter;
							} else {
								// we do not stop here because we need the number of outgoing lanes in highway mode
							}
						}
						//mCurrentState = 27;
						VehicleInfo.VehicleType vehicleType2 = this._vehicleTypes;
						NetInfo.LaneType drivingEnabledLaneTypes = this._laneTypes;
						drivingEnabledLaneTypes &= ~NetInfo.LaneType.Pedestrian;
						drivingEnabledLaneTypes &= ~NetInfo.LaneType.Parking;

						//if (debug) {
						//Log.Message($"Path finding ({this._pathFindIndex}): Segment {nextSegmentId}");
						//}

						NetInfo.Direction nextDir = instance.m_segments.m_buffer[nextSegmentId].m_startNode != nextNodeId ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
						NetInfo.Direction nextDir2 = ((instance.m_segments.m_buffer[nextSegmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? nextDir : NetInfo.InvertDirection(nextDir);

						// valid next lanes:
						int[] laneIndexes = new int[16]; // index of NetNode.Info.m_lanes
						uint[] laneIds = new uint[16]; // index of NetManager.m_lanes.m_buffer
						uint[] indexByRightSimilarLaneIndex = new uint[16];
						uint[] indexByLeftSimilarLaneIndex = new uint[16];

						uint curLaneI = 0;
						uint curLaneId = instance.m_segments.m_buffer[nextSegmentId].m_lanes;
						int laneIndex = 0;
#if DEBUG
						uint wIter = 0;
#endif
						//mCurrentState = 28;
						while (laneIndex < nextSegmentInfo.m_lanes.Length && curLaneId != 0u) {
							//mCurrentState = 29;
#if DEBUGPF
							if (debug)
								logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: Lane Iteration {laneIndex}. nextSegmentId={nextSegmentId}, curLaneId={curLaneId}");
#endif

#if DEBUG
							++wIter;
							if (wIter >= 20) {
								Log.Error("Too many iterations in ProcessItemMain!");
								break;
							}
#endif

							// determine valid lanes based on lane arrows
							NetInfo.Lane nextLane = nextSegmentInfo.m_lanes[laneIndex];
							bool incomingLane = (byte)(nextLane.m_finalDirection & nextDir2) != 0;
							bool compatibleLane = nextLane.CheckType(drivingEnabledLaneTypes, _vehicleTypes);

							if (incomingLane && compatibleLane) {
								++incomingVehicleLanes;
#if DEBUGPF
								if (debug)
									logBuf.Add($"Segment {nextSegmentId}, lane {curLaneId}, {laneIndex} is compatible (prevSegment: {prevSegmentId}). laneTypes: {_laneTypes.ToString()}, vehicleTypes: {_vehicleTypes.ToString()}, incomingLanes={incomingVehicleLanes}, isIncomingRight? {isIncomingRight}, isIncomingLeft? {isIncomingLeft}, isIncomingStraight? {isIncomingStraight}");
#endif

								// calculate current similar lane index starting from right line
								int nextRightSimilarLaneIndex;
								int nextLeftSimilarLaneIndex;
								if ((byte)(nextLane.m_direction & normDirection) != 0) {
									nextRightSimilarLaneIndex = nextLane.m_similarLaneIndex;
									nextLeftSimilarLaneIndex = nextLane.m_similarLaneCount - nextLane.m_similarLaneIndex - 1;
								} else {
									nextRightSimilarLaneIndex = nextLane.m_similarLaneCount - nextLane.m_similarLaneIndex - 1;
									nextLeftSimilarLaneIndex = nextLane.m_similarLaneIndex;
								}

								bool hasLeftArrow = ((NetLane.Flags)instance.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.Left) == NetLane.Flags.Left;
								bool hasRightArrow = ((NetLane.Flags)instance.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.Right) == NetLane.Flags.Right;
								bool hasForwardArrow = ((NetLane.Flags)instance.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.Forward) != NetLane.Flags.None || ((NetLane.Flags)instance.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.LeftForwardRight) == NetLane.Flags.None;
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

								bool isValidIncomingRight = isIncomingRight && hasLeftArrow;
								bool isValidIncomingLeft = isIncomingLeft && hasRightArrow;
								bool isValidIncomingStraight = isIncomingStraight && hasForwardArrow;
								bool isValidIncomingTurn = isIncomingTurn && ((TrafficPriority.IsLeftHandDrive() && hasRightArrow) || (!TrafficPriority.IsLeftHandDrive() && hasLeftArrow));

#if DEBUGPF
								if (debug)
									logBuf.Add($"Segment {nextSegmentId}, lane {curLaneId}, {laneIndex}. isValidIncomingRight? {isValidIncomingRight}, isValidIncomingLeft? {isValidIncomingLeft}, isValidIncomingStraight? {isValidIncomingStraight} isValidIncomingTurn? {isValidIncomingTurn}");
#endif

									// add valid next lanes
								if (applyHighwayRulesAtSegment || isValidIncomingRight || isValidIncomingLeft || isValidIncomingStraight || isValidIncomingTurn) {
									laneIndexes[curLaneI] = laneIndex;
									laneIds[curLaneI] = curLaneId;
									indexByRightSimilarLaneIndex[nextRightSimilarLaneIndex] = curLaneI + 1;
									indexByLeftSimilarLaneIndex[nextLeftSimilarLaneIndex] = curLaneI + 1;
#if DEBUGPF
									if (debug)
										logBuf.Add($"Adding lane #{curLaneI} (id {curLaneId}, idx {laneIndex}), right sim. idx: {nextRightSimilarLaneIndex}, left sim. idx.: {nextLeftSimilarLaneIndex}");
#endif
									curLaneI++;
								}
							}

							if (!incomingLane && compatibleLane) {
								// outgoing lane
								++outgoingVehicleLanes;
							}

							curLaneId = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_nextLane;
							laneIndex++;
						} // foreach lane
						//mCurrentState = 30;

						if (curLaneI > 0) {
							//mCurrentState = 31;
							// we found compatible lanes
							var nextLaneIndex = 0;
							var nextLaneId = 0u;
							int nextLaneI = -1;
							int nextCompatibleLaneCount = Convert.ToInt32(curLaneI);

#if DEBUGPF
							if (debug) {
								logBuf.Add($"Compatible lanes found.");
								logBuf.Add($"next segment: {nextSegmentId}, number of next lanes: {nextCompatibleLaneCount}, prev. segment: {prevSegmentId}, prev. lane ID: {item.m_laneID}, prev. lane idx: {item.m_position.m_lane}, prev. right sim. idx: {prevRightSimilarLaneIndex}, prev. left sim. idx: {prevLeftSimilarLaneIndex}, laneTypes: {_laneTypes.ToString()}, vehicleTypes: {_vehicleTypes.ToString()}, incomingLanes={incomingVehicleLanes}, isIncomingRight? {isIncomingRight}, isIncomingLeft? {isIncomingLeft}, isIncomingStraight? {isIncomingStraight}");
							}
#endif

							// mix of incoming/outgoing lanes on the right sight of prev segment is not allowed in highway mode
							if (totalIncomingLanes > 0 && totalOutgoingLanes > 0) {
#if DEBUGPF
								if (debug)
									logBuf.Add($"{totalIncomingLanes} incoming lanes and {totalOutgoingLanes} outgoing lanes found. Disabling highway rules.");
#endif
								applyHighwayRulesAtSegment = false;
							}

							
							if (applyHighwayRulesAtSegment) {
								//mCurrentState = 32;
								int numRightLanes = Math.Max(totalIncomingLanes, totalOutgoingLanes);
#if DEBUGPF
								if (debug)
									logBuf.Add($"Applying highway rules. {numRightLanes} right lanes found ({totalIncomingLanes} incoming, {totalOutgoingLanes} outgoing).");
#endif
								int nextLeftSimilarIndex;
								if (totalOutgoingLanes > 0) {
									nextLeftSimilarIndex = prevLeftSimilarLaneIndex + numRightLanes; // lane splitting
#if DEBUGPF
									if (debug)
										logBuf.Add($"Performing lane split. nextLeftSimilarIndex={nextLeftSimilarIndex} = prevLeftSimilarIndex({prevLeftSimilarLaneIndex}) + numRightLanes({numRightLanes})");
#endif
								} else {
									nextLeftSimilarIndex = prevLeftSimilarLaneIndex - numRightLanes; // lane merging
#if DEBUGPF
									if (debug)
										logBuf.Add($"Performing lane merge. nextLeftSimilarIndex={nextLeftSimilarIndex} = prevLeftSimilarIndex({prevLeftSimilarLaneIndex}) - numRightLanes({numRightLanes})");
#endif
								}

								if (nextLeftSimilarIndex >= 0 && nextLeftSimilarIndex < nextCompatibleLaneCount) {
									// enough lanes available
									nextLaneI = Convert.ToInt32(indexByLeftSimilarLaneIndex[nextLeftSimilarIndex]) - 1;
#if DEBUGPF
									if (debug)
										logBuf.Add($"Next lane within bounds. nextLaneI={nextLaneI}");
#endif
								} else {
									if (nextLeftSimilarIndex < 0) {
										// too few lanes at prevSegment or nextSegment: sort right
										if (totalIncomingLanes >= prevSimiliarLaneCount)
											nextLaneI = Convert.ToInt32(indexByRightSimilarLaneIndex[prevRightSimilarLaneIndex]) - 1;
									} else {
										if (totalOutgoingLanes >= nextCompatibleLaneCount)
											nextLaneI = Convert.ToInt32(indexByRightSimilarLaneIndex[0]) - 1;
									}
#if DEBUGPF
									if (debug)
										logBuf.Add($"Next lane out of bounds. nextLaneI={nextLaneI}, isIncomingLeft={isIncomingLeft}, prevRightSimilarIndex={prevRightSimilarLaneIndex}, prevLeftSimilarIndex={prevLeftSimilarLaneIndex}");
#endif
								}

								if (nextLaneI < 0 || nextLaneI >= nextCompatibleLaneCount) {
#if DEBUGPF
									if (debug)
										Log.Error($"(PFERR) Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right, {prevLeftSimilarLaneIndex} from left: Highway lane selector cannot find suitable lane! isIncomingLeft={isIncomingLeft} isIncomingRight={isIncomingRight} totalIncomingLanes={totalIncomingLanes}");
#endif
									couldFindCustomPath = true; // not of interest for us
									//mCurrentState = 33;
									goto nextIter; // no path to this lane
								}
							} else if (nextCompatibleLaneCount == 1) {
								//mCurrentState = 34;
								nextLaneI = 0;
#if DEBUGPF
								if (debug)
									logBuf.Add($"Single target lane found. nextLaneI={nextLaneI}");
#endif
							} else {
								//mCurrentState = 35;
								// lane matching
								int prevSimilarLaneCount = lane.m_similarLaneCount;

#if DEBUGPF
								if (debug)
									logBuf.Add($"Multiple target lanes found. prevSimilarLaneCount={prevSimilarLaneCount}");
#endif

								int minNextRightSimilarIndex = -1;
								int maxNextRightSimilarIndex = -1;
								if (nextIsRealJunction) {
									// at junctions: try to match distinct lanes (1-to-1, n-to-1)
									minNextRightSimilarIndex = prevRightSimilarLaneIndex;
									maxNextRightSimilarIndex = prevRightSimilarLaneIndex;

									// vehicles may change lanes at straight segments?w
									if (isIncomingStraight && Flags.getStraightLaneChangingAllowed(nextSegmentId, Singleton<NetManager>.instance.m_segments.m_buffer[nextSegmentId].m_startNode == nextNodeId)) {
										minNextRightSimilarIndex = Math.Max(0, minNextRightSimilarIndex - 1);
										maxNextRightSimilarIndex = Math.Min(nextCompatibleLaneCount - 1, maxNextRightSimilarIndex + 1);
#if DEBUGPF
										if (debug)
											logBuf.Add($"Next is incoming straight. Allowing lane changes! minNextRightSimilarIndex={minNextRightSimilarIndex}, maxNextRightSimilarIndex={maxNextRightSimilarIndex}");
#endif
									}
#if DEBUGPF
									if (debug)
										logBuf.Add($"Next is junction. minNextRightSimilarIndex={minNextRightSimilarIndex}, maxNextRightSimilarIndex={maxNextRightSimilarIndex}");
#endif
								} else {
									// lane merging/splitting
									//mCurrentState = 36;
									HandleLaneMergesAndSplits(prevRightSimilarLaneIndex, nextCompatibleLaneCount, prevSimilarLaneCount, out minNextRightSimilarIndex, out maxNextRightSimilarIndex);
									//mCurrentState = 37;
#if DEBUGPF
									if (debug)
										logBuf.Add($"Next is not a junction. nextRightSimilarLaneIndex=HandleLaneMergesAndSplits({prevRightSimilarLaneIndex}, {nextCompatibleLaneCount}, {prevSimilarLaneCount})= min. {minNextRightSimilarIndex} max. {maxNextRightSimilarIndex}");
#endif
								}

								// find best matching lane(s)
								for (int nextRightSimilarIndex = minNextRightSimilarIndex; nextRightSimilarIndex <= maxNextRightSimilarIndex; ++nextRightSimilarIndex) {
#if DEBUGPF
									if (debug)
										logBuf.Add($"current right similar index = {nextRightSimilarIndex}, min. {minNextRightSimilarIndex} max. {maxNextRightSimilarIndex}");
#endif
									//mCurrentState = 38;
									nextLaneI = FindNthCompatibleLane(ref indexByRightSimilarLaneIndex, nextRightSimilarIndex);
									//mCurrentState = 39;

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
										logBuf.Add($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane idx {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right. There are {curLaneI} candidate lanes. We choose lane {nextLaneI} (index {nextLaneIndex}, {nextRightSimilarIndex} compatible from right). lhd: {TrafficPriority.IsLeftHandDrive()}, ped: {pedestrianAllowed}, magical flag4: {mayTurnAround}");
#endif

									//mCurrentState = 40;
									if (ProcessItemCosts(true, debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian, nextLaneIndex, nextLaneId, out foundForced)) {
										mayTurnAround = true;
									}
									//mCurrentState = 41;
									couldFindCustomPath = true;
								}

								goto nextIter;
							}

							//mCurrentState = 42;
							if (nextLaneI < 0) {
#if DEBUGPF
								if (debug)
									Log.Error($"(PFERR) Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: nextLaneI < 0!");
#endif
								//mCurrentState = 43;
								goto nextIter;
							}

							//mCurrentState = 44;
							// go to matched lane
							nextLaneIndex = laneIndexes[nextLaneI];
							nextLaneId = laneIds[nextLaneI];

#if DEBUGPF
							if (debug)
								logBuf.Add($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: nextLaneIndex={nextLaneIndex} nextLaneId={nextLaneId}");
#endif

							//mCurrentState = 45;
							if (IsMasterPathFind && applyHighwayRulesAtSegment) {
								// udpate highway mode arrows
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
									Flags.setHighwayLaneArrowFlags(nextLaneId, newHighwayArrows);
#if DEBUGPF
								/*if (Options.disableSomething1)
									Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Setting highway arrows @ lane {nextLaneId} to {newHighwayArrows.ToString()}: END");*/
#endif
							}
							//mCurrentState = 46;

							if (ProcessItemCosts(true, debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian, nextLaneIndex, nextLaneId, out foundForced)) {
								mayTurnAround = true;
							}
							//mCurrentState = 47;

							if (foundForced) {
#if DEBUGPF
								if (debug)
									logBuf.Add($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: FORCED LANE FOUND!");
#endif
								couldFindCustomPath = true;
							}
						} else {
							// no compatible lanes found
							//mCurrentState = 48;
#if DEBUGPF
							if (debug)
								Log.Error($"(PFERR) Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: No lane arrows defined");
#endif
							couldFindCustomPath = true; // the player did not set lane arrows. this is ok...
														/*if (ProcessItem(debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian)) {
															blocked = true;
														}*/
						}
						/*} catch (Exception e) {
							Log.Error($"(PFERR) Error occurred in custom path-finding (main): {e.ToString()}");

							couldFindCustomPath = true; // not of interest for us
														// stock code fallback
							if (this.ProcessItemSub(debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian)) {
								blocked = true;
							}
						}*/
						// NON-STOCK CODE END
					} else {
						//mCurrentState = 49;
						// pedestrians

						// stock code:
						if (this.ProcessItemCosts(false, debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian)) {
							mayTurnAround = true;
						}
						couldFindCustomPath = true; // not of interest for us
						//mCurrentState = 50;
					}

					nextIter:
					//mCurrentState = 51;
					if (!couldFindCustomPath) {
#if DEBUGPF
						if (debug)
							logBuf.Add($"(PFERR) Could not find custom path from segment {nextSegmentId} to segment {prevSegmentId}, lane {item.m_position.m_lane}, off {item.m_position.m_offset} at node {nextNodeId}!");
#endif
						// stock code:
						/*if (this.ProcessItem(debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian)) {
							blocked = true;
						}*/
					}

					if (nextSegmentId == prevSegmentId)
						similarLaneIndexFromLeft = firstSimilarLaneIndexFromLeft; // u-turning does not "consume" a lane

					nextSegmentId = instance.m_segments.m_buffer[(int)nextSegmentId].GetRightSegment(nextNodeId);
					if (nextSegmentId != prevSegmentId) {
						totalIncomingLanes += incomingVehicleLanes;
						totalOutgoingLanes += outgoingVehicleLanes;
					}

					if (explorePrevSegment && nextSegmentId == prevSegmentId)
						break;
					//mCurrentState = 52;
				} // foreach segment
				//mCurrentState = 53;
#if DEBUGPF
				if (debug)
					logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: Custom part finished");
#endif
				if (mayTurnAround && (this._vehicleTypes & VehicleInfo.VehicleType.Tram) == VehicleInfo.VehicleType.None) {
					// turn-around for vehicles (if street is blocked)
#if DEBUGPF
					if (debug)
						logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: Road may be blocked");
#endif
					// vehicles may turn around if the street is blocked
					nextSegmentId = item.m_position.m_segment;
					//mCurrentState = 54;
					this.ProcessItemCosts(false, debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, false);
					//mCurrentState = 55;
				}
				//mCurrentState = 56;
				// NON-STOCK CODE START
				/*if (foundForced)
					return;*/
				// NON-STOCK CODE END
				if (pedestrianAllowed) {
					// turn-around for pedestrians
					nextSegmentId = item.m_position.m_segment;
					int nextLaneIndex;
					uint nextLaneId;
					if (instance.m_segments.m_buffer[(int)nextSegmentId].GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Pedestrian, this._vehicleTypes, out nextLaneIndex, out nextLaneId)) {
#if DEBUGPF
						if (debug)
							logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: Ped allowed u-turn");
#endif
#if DEBUGPF2
						if (debug2)
							logBuf2.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}: Ped allowed u-turn. nextConnectOffset={nextConnectOffset} nextLaneIndex={nextLaneIndex} nextLaneId={nextLaneId}");
#endif
						this.ProcessItemPedBicycle(item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], nextConnectOffset, nextLaneIndex, nextLaneId); // ped
					}
				}
			}
			if (nextNode.m_lane != 0u) {
				bool targetDisabled = (nextNode.m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None;
				ushort nextSegment = instance.m_lanes.m_buffer[(int)((UIntPtr)nextNode.m_lane)].m_segment;
				if (nextSegment != 0 && nextSegment != item.m_position.m_segment) {
#if DEBUGPF
						if (debug)
							logBuf.Add($"Exploring path from {nextSegment} to {item.m_position.m_segment}, lane idx {item.m_position.m_lane}: handling special lanes");
#endif
#if DEBUGPF2
						if (debug2)
							logBuf2.Add($"Exploring path from {nextSegment} to {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}: handling special lanes");
#endif
					//mCurrentState = 59;
					this.ProcessItem2(item, nextNodeId, targetDisabled, nextSegment, ref instance.m_segments.m_buffer[(int)nextSegment], nextNode.m_lane, nextNode.m_laneOffset, connectOffset);
					//mCurrentState = 60;
				}
			}
			//mCurrentState = 61;
#if DEBUGPF
			if (debug) {
				foreach (String toLog in logBuf) {
					Log._Debug($"Pathfinder ({this._pathFindIndex}) for unit {unitId}: " + toLog);
				}
			}
#endif
#if DEBUGPF2
			if (debug2) {
				foreach (String toLog in logBuf2) {
					Log._Debug($"Pathfinder ({this._pathFindIndex}) for unit {unitId}: " + toLog);
				}
			}
#endif
			//mCurrentState = 62;
		}

		private static int FindNthCompatibleLane(ref uint[] indexBySimilarLaneIndex, int prevRightSimilarLaneIndex) {
			int nextLaneI = -1;
			for (int j = 0; j < 16; ++j) {
				if (indexBySimilarLaneIndex[j] <= 0)
					continue;
				nextLaneI = Convert.ToInt32(indexBySimilarLaneIndex[j]) - 1;
				if (prevRightSimilarLaneIndex <= 0) { // matching lane found
					if (prevRightSimilarLaneIndex < 0) {
#if DEBUG
						Log.Error($"FindNthCompatibleLane: prevRightSimilarIndex < 0!");
#endif
					}
					break;
				}
				--prevRightSimilarLaneIndex;
			}
			return nextLaneI;
		}

		private void HandleLaneMergesAndSplits(int prevRightSimilarLaneIndex, int nextCompatibleLaneCount, int prevSimilarLaneCount, out int minNextRightSimilarLaneIndex, out int maxNextRightSimilarLaneIndex) {
			bool sym1 = (prevSimilarLaneCount & 1) == 0; // mod 2 == 0
			bool sym2 = (nextCompatibleLaneCount & 1) == 0; // mod 2 == 0
			if (prevSimilarLaneCount < nextCompatibleLaneCount) {
				// lane merging
				if (sym1 == sym2) {
					// merge outer lanes
					int a = (nextCompatibleLaneCount - prevSimilarLaneCount) / 2;
					if (prevSimilarLaneCount == 1) {
						minNextRightSimilarLaneIndex = 0;
						maxNextRightSimilarLaneIndex = nextCompatibleLaneCount - 1;
					} else if (prevRightSimilarLaneIndex == 0) {
						minNextRightSimilarLaneIndex = 0;
						maxNextRightSimilarLaneIndex = a;
					} else if (prevRightSimilarLaneIndex == prevSimilarLaneCount - 1) {
						minNextRightSimilarLaneIndex = prevRightSimilarLaneIndex + a;
						maxNextRightSimilarLaneIndex = nextCompatibleLaneCount - 1;
					} else {
						minNextRightSimilarLaneIndex = maxNextRightSimilarLaneIndex = prevRightSimilarLaneIndex + a;
					}
				} else {
					// criss-cross merge
					int a = (nextCompatibleLaneCount - prevSimilarLaneCount - 1) / 2;
					int b = (nextCompatibleLaneCount - prevSimilarLaneCount + 1) / 2;
					if (prevSimilarLaneCount == 1) {
						minNextRightSimilarLaneIndex = 0;
						maxNextRightSimilarLaneIndex = nextCompatibleLaneCount - 1;
					} else if (prevRightSimilarLaneIndex == 0) {
						minNextRightSimilarLaneIndex = 0;
						maxNextRightSimilarLaneIndex = b;
					} else if (prevRightSimilarLaneIndex == prevSimilarLaneCount - 1) {
						minNextRightSimilarLaneIndex = prevRightSimilarLaneIndex + a;
						maxNextRightSimilarLaneIndex = nextCompatibleLaneCount - 1;
					} else if (_pathRandomizer.Int32(0, 1) == 0) {
						minNextRightSimilarLaneIndex = maxNextRightSimilarLaneIndex = prevRightSimilarLaneIndex + a;
					} else {
						minNextRightSimilarLaneIndex = maxNextRightSimilarLaneIndex = prevRightSimilarLaneIndex + b;
					}
				}
			} else {
				// at lane splits: distribute traffic evenly (1-to-n, n-to-n)										
				if (sym1 == sym2) {
					// split outer lanes
					int a = (prevSimilarLaneCount - nextCompatibleLaneCount) / 2;
					minNextRightSimilarLaneIndex = maxNextRightSimilarLaneIndex = prevRightSimilarLaneIndex - a;
				} else {
					// split outer lanes, criss-cross inner lanes 
					int a = (prevSimilarLaneCount - nextCompatibleLaneCount - 1) / 2;
					minNextRightSimilarLaneIndex = prevRightSimilarLaneIndex - a - 1;
					maxNextRightSimilarLaneIndex = prevRightSimilarLaneIndex - a;

				}
			}
			minNextRightSimilarLaneIndex = COMath.Clamp(minNextRightSimilarLaneIndex, 0, nextCompatibleLaneCount - 1);
			maxNextRightSimilarLaneIndex = COMath.Clamp(maxNextRightSimilarLaneIndex, 0, nextCompatibleLaneCount - 1);

			if (minNextRightSimilarLaneIndex > maxNextRightSimilarLaneIndex) {
#if DEBUG
				Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Erroneous calculation in HandleMergeAndSplits detected!");
#endif
				minNextRightSimilarLaneIndex = maxNextRightSimilarLaneIndex;
			}
		}

#region stock code
		// 2
		private void ProcessItem2(BufferItem item, ushort targetNodeId, bool targetDisabled, ushort nextSegmentId, ref NetSegment nextSegment, uint lane, byte offset, byte connectOffset) {
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
			float num4 = 1f;
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
				num4 = this.CalculateLaneSpeed(prevMaxSpeed, connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[(int)item.m_position.m_segment], lane2); // NON-STOCK CODE
			}
			float averageLength = instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_averageLength;
			float offsetLength = (float)Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * 0.003921569f * averageLength;
			float methodDistance = item.m_methodDistance + offsetLength;
			float comparisonValue = item.m_comparisonValue + offsetLength / (num4 * this._maxLength);
			Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)item.m_laneID)].CalculatePosition((float)connectOffset * 0.003921569f);
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

				if (lane == curLaneId) {
					NetInfo.Lane lane3 = nextSegmentInfo.m_lanes[laneIndex];
					if (lane3.CheckType(this._laneTypes, this._vehicleTypes)) {
						Vector3 a = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].CalculatePosition((float)offset * 0.003921569f);
						float num9 = Vector3.Distance(a, b);
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
						if ((byte)(lane3.m_laneType & laneType) == 0) {
							nextItem.m_methodDistance = 0f;
						} else {
							nextItem.m_methodDistance = methodDistance + num9;
						}
						float nextMaxSpeed = GetLaneSpeedLimit(nextSegmentId, laneIndex, curLaneId, lane3); // SpeedLimitManager.GetLockFreeGameSpeedLimit(nextSegmentId, laneIndex, curLaneId, ref lane3); // NON-STOCK CODE
						if (lane3.m_laneType != NetInfo.LaneType.Pedestrian || nextItem.m_methodDistance < 1000f) {
							nextItem.m_comparisonValue = comparisonValue + num9 / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength); // NON-STOCK CODE
							if ((nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
								nextItem.m_direction = NetInfo.InvertDirection(lane3.m_finalDirection);
							} else {
								nextItem.m_direction = lane3.m_finalDirection;
							}
							if (lane == this._startLaneA) {
								if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetA) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetA)) {
									return;
								}
								float num10 = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetA, nextItem.m_position.m_offset, ref nextSegment, lane3); // NON-STOCK CODE
								float num11 = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetA)) * 0.003921569f;
								nextItem.m_comparisonValue += num11 * nextSegment.m_averageLength / (num10 * this._maxLength);
							}
							if (lane == this._startLaneB) {
								if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetB) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetB)) {
									return;
								}
								float num12 = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetB, nextItem.m_position.m_offset, ref nextSegment, lane3); // NON-STOCK CODE
								float num13 = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetB)) * 0.003921569f;
								nextItem.m_comparisonValue += num13 * nextSegment.m_averageLength / (num12 * this._maxLength);
							}
							nextItem.m_laneID = lane;
							nextItem.m_lanesUsed = (item.m_lanesUsed | lane3.m_laneType);
							this.AddBufferItem(nextItem, item.m_position);
						}
					}
					return;
				}
				curLaneId = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_nextLane;
				laneIndex++;
			}
		}
#endregion

		private bool ProcessItemCosts(bool allowCustomLaneChanging, bool debug, BufferItem item, ushort targetNode, ushort segmentID, ref NetSegment segment, ref int laneIndexFromLeft, byte connectOffset, bool enableVehicle, bool enablePedestrian) {
			bool foundForced = false;
			return ProcessItemCosts(allowCustomLaneChanging, debug, item, targetNode, segmentID, ref segment, ref laneIndexFromLeft, connectOffset, enableVehicle, enablePedestrian, null, null, out foundForced);
		}

		// 3
		private bool ProcessItemCosts(bool allowCustomLaneChanging, bool debug, BufferItem item, ushort targetNodeId, ushort nextSegmentId, ref NetSegment nextSegment, ref int laneIndexFromLeft, byte connectOffset, bool enableVehicle, bool enablePedestrian, int? forceLaneIndex, uint? forceLaneId, out bool foundForced) {
			//sCurrentState = 0;
#if DEBUGPF
			/*if (Options.disableSomething1)
				Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: ProcessItemSub item {item.m_position.m_segment} {item.m_position.m_lane}, targetNodeId {targetNodeId}");*/
#endif

			foundForced = false;
			bool result = false;
			if ((nextSegment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None) {
				return result;
			}
			NetManager instance = Singleton<NetManager>.instance;
			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = instance.m_segments.m_buffer[(int)item.m_position.m_segment].Info;
			int nextNumLanes = nextSegmentInfo.m_lanes.Length;
			NetInfo.Direction nextDir = (targetNodeId != nextSegment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
			NetInfo.Direction nextDir2 = ((nextSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? nextDir : NetInfo.InvertDirection(nextDir);
			float num3 = 0.01f - Mathf.Min(nextSegmentInfo.m_maxTurnAngleCos, prevSegmentInfo.m_maxTurnAngleCos);
			//sCurrentState = 1;
			if (num3 < 1f) {
				Vector3 vector;
				if (targetNodeId == instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_startNode) {
					vector = instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_startDirection;
				} else {
					vector = instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_endDirection;
				}
				Vector3 vector2;
				if ((byte)(nextDir & NetInfo.Direction.Forward) != 0) {
					vector2 = nextSegment.m_endDirection;
				} else {
					vector2 = nextSegment.m_startDirection;
				}
				float num4 = vector.x * vector2.x + vector.z * vector2.z;
				if (num4 >= num3) {
					return result;
				}
			}
			//sCurrentState = 2;
			float prevMaxSpeed = 1f;
			float prevLaneSpeed = 1f;
			NetInfo.LaneType laneType = NetInfo.LaneType.None;
			VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None;
			// NON-STOCK CODE START //
			bool prevIsHighway = false;
			if (prevSegmentInfo.m_netAI is RoadBaseAI)
				prevIsHighway = ((RoadBaseAI)prevSegmentInfo.m_netAI).m_highwayRules;
			float nextSpeed = 1f;
			float nextDensity = 0f;
			int prevRightSimilarLaneIndex = -1;
			NetInfo.Direction normDirection = TrafficPriority.IsLeftHandDrive() ? NetInfo.Direction.Forward : NetInfo.Direction.Backward; // direction to normalize indices to
			int prevNumLanes = 1;
			float prevSpeed = 0f;
			bool isMiddle = connectOffset != 0 && connectOffset != 255;
			NetInfo.Lane lane = null;
			//sCurrentState = 3;
			// NON-STOCK CODE END //
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				lane = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				laneType = lane.m_laneType;
				vehicleType = lane.m_vehicleType;
				prevMaxSpeed = GetLaneSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, lane); // SpeedLimitManager.GetLockFreeGameSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, ref lane); // NON-STOCK CODE
				prevLaneSpeed = this.CalculateLaneSpeed(prevMaxSpeed, connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[(int)item.m_position.m_segment], lane); // NON-STOCK CODE
				// NON-STOCK CODE START //
				prevNumLanes = lane.m_similarLaneCount;
				if ((byte)(lane.m_direction & normDirection) != 0) {
					prevRightSimilarLaneIndex = lane.m_similarLaneIndex;
				} else {
					prevRightSimilarLaneIndex = lane.m_similarLaneCount - lane.m_similarLaneIndex - 1;
				}
				prevSpeed = CustomRoadAI.laneMeanSpeeds[item.m_laneID];
				prevSpeed = (float)Math.Max(0.1f, Math.Round(prevSpeed * 0.1f) / 10f); // 0.01, 0.1, 0.2, ... , 1
				// NON-STOCK CODE END //
			}
			//sCurrentState = 4;
			bool useAdvancedAI = !Options.isStockLaneChangerUsed() &&
				(_extVehicleType != null &&
				(_extVehicleType & (ExtVehicleType.RoadVehicle & ~ExtVehicleType.RoadPublicTransport)) != ExtVehicleType.None) &&
				allowCustomLaneChanging &&
				!_transportVehicle &&
				!_stablePath &&
				enableVehicle &&
				(vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None &&
				(laneType & (NetInfo.LaneType.PublicTransport | NetInfo.LaneType.Pedestrian | NetInfo.LaneType.Parking)) == NetInfo.LaneType.None; // NON-STOCK CODE
			float prevCost = instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_averageLength;
			if (!useAdvancedAI && !this._stablePath) { // NON-STOCK CODE
				Randomizer randomizer = new Randomizer(this._pathFindIndex << 16 | (uint)item.m_position.m_segment);
				prevCost *= (float)(randomizer.Int32(900, 1000 + (int)(instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_trafficDensity * 10)) + this._pathRandomizer.Int32(20u)) * 0.001f;
			}
			if (!useAdvancedAI) {
				if (this._isHeavyVehicle && (instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_flags & NetSegment.Flags.HeavyBan) != NetSegment.Flags.None) {
					prevCost *= 10f;
				} else if (laneType == NetInfo.LaneType.Vehicle && (vehicleType & _vehicleTypes) == VehicleInfo.VehicleType.Car && (instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_flags & NetSegment.Flags.CarBan) != NetSegment.Flags.None) {
					prevCost *= 5f;
				}
			}
			if (this._transportVehicle && laneType == NetInfo.LaneType.TransportVehicle) {
				prevCost *= 0.5f;
			}
			bool avoidLane = false;
			bool strictlyAvoidLane = false;
			if (
					(this._isHeavyVehicle && (nextSegment.m_flags & NetSegment.Flags.HeavyBan) != NetSegment.Flags.None) ||
					(laneType == NetInfo.LaneType.Vehicle && (vehicleType & _vehicleTypes) == VehicleInfo.VehicleType.Car && (instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_flags & NetSegment.Flags.CarBan) != NetSegment.Flags.None)
					) {
				if (Options.disableSomething1 && debug) {
					Log._Debug($"Vehicle {_extVehicleType} should not use lane {item.m_position.m_lane} @ seg. {item.m_position.m_segment}, null? {lane == null}");
				}
				avoidLane = true;
			}
			if (!CanUseLane(debug, item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, lane)) {
				if (Options.disableSomething1 && debug) {
					Log._Debug($"Vehicle {_extVehicleType} must not use lane {item.m_position.m_lane} @ seg. {item.m_position.m_segment}, null? {lane== null}");
				}
				strictlyAvoidLane = true;
			}
			if (!useAdvancedAI) {
				if (strictlyAvoidLane)
					prevCost *= 50f;
			}
			if ((byte)(laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
				laneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
			}
			float prevOffsetCost = (float)Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * 0.003921569f * prevCost;
			float prevMethodDist = item.m_methodDistance + prevOffsetCost;
			float prevComparisonPlusOffsetCostOverSpeed = item.m_comparisonValue + prevOffsetCost / (prevLaneSpeed * this._maxLength);
			Vector3 prevLanePosition = instance.m_lanes.m_buffer[(int)((UIntPtr)item.m_laneID)].CalculatePosition((float)connectOffset * 0.003921569f);
			int newLaneIndexFromLeft = laneIndexFromLeft;
			bool transitionNode = (instance.m_nodes.m_buffer[(int)targetNodeId].m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;
			NetInfo.LaneType laneType2 = this._laneTypes;
			VehicleInfo.VehicleType vehicleType2 = this._vehicleTypes;
			if (!enableVehicle) {
				vehicleType2 &= VehicleInfo.VehicleType.Bicycle;
				if (vehicleType2 == VehicleInfo.VehicleType.None) {
					laneType2 &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
			}
			if (!enablePedestrian) {
				laneType2 &= ~NetInfo.LaneType.Pedestrian;
			}
			// NON-STOCK CODE START //
			//NetNode targetNode = instance.m_nodes.m_buffer[targetNodeId];
			//float segmentDensity = (float)(instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_trafficDensity + nextSegment.m_trafficDensity) / 2f;
			bool nextIsRealJunction = instance.m_nodes.m_buffer[targetNodeId].CountSegments() > 2;
			ushort sourceNodeId = (targetNodeId == instance.m_segments.m_buffer[item.m_position.m_segment].m_startNode) ? instance.m_segments.m_buffer[item.m_position.m_segment].m_endNode : instance.m_segments.m_buffer[item.m_position.m_segment].m_startNode; // no lane changing directly in front of a junction
			bool prevIsRealJunction = instance.m_nodes.m_buffer[sourceNodeId].CountSegments() > 2;
			//sCurrentState = 9;
			bool nextIsHighway = false;
			if (nextSegmentInfo.m_netAI is RoadBaseAI)
				nextIsHighway = ((RoadBaseAI)nextSegmentInfo.m_netAI).m_highwayRules;
			bool wantToChangeLane = false;
			if (useAdvancedAI)
				wantToChangeLane = Options.laneChangingRandomization != 5 ? _pathRandomizer.Int32(1, Options.getLaneChangingRandomizationTargetValue()) == 1 : false;

			float nextDensitySum = 0f;
			if (useAdvancedAI) {
				// measure speed variance
				uint lIndex = 0;
				uint currentLaneId = nextSegment.m_lanes;
				uint nextCompatibleLanes = 0;
				while (lIndex < nextNumLanes && currentLaneId != 0u) {
					NetInfo.Lane nextLane = nextSegmentInfo.m_lanes[lIndex];
					if (nextLane.CheckType(laneType2, vehicleType2) && (nextSegmentId != item.m_position.m_segment || lIndex != (int)item.m_position.m_lane) && (byte)(nextLane.m_finalDirection & nextDir2) != 0 && CanLanesConnect(currentLaneId, item.m_laneID)) {
						++nextCompatibleLanes;
						nextDensitySum += CustomRoadAI.currentLaneDensities[currentLaneId];
					}

					lIndex++;
					currentLaneId = instance.m_lanes.m_buffer[currentLaneId].m_nextLane;
				}
			}
			// NON-STOCK CODE END //

			uint laneIndex = forceLaneIndex != null ? (uint)forceLaneIndex : 0u;
			uint curLaneId = (uint)(forceLaneId != null ? forceLaneId : nextSegment.m_lanes); // NON-STOCK CODE
																							  //sCurrentState = 10;
			while (laneIndex < nextNumLanes && curLaneId != 0u) {
				//sCurrentState = 11;

				//Log.Message($"PF {this._pathFindIndex} -- CustomPathfind (3): Iter #{laneIndex}, curLaneId={curLaneId}, prevSegment={item.m_position.m_segment}, targetNodeId={targetNodeId}");

				// NON-STOCK CODE START //
				if (forceLaneIndex != null && laneIndex != forceLaneIndex)
					break;
				// NON-STOCK CODE END //
				NetInfo.Lane nextLane = nextSegmentInfo.m_lanes[laneIndex];

#if DEBUGCOSTS
				bool costDebug = debug;
				//bool costDebug = Options.disableSomething1 && (nextSegmentId == 9649 || nextSegmentId == 1043);
				List<String> logBuf = null;
				if (costDebug) {
					logBuf = new List<String>();
					logBuf.Add($"Path from {nextSegmentId} (idx {laneIndex}, id {curLaneId}) to {item.m_position.m_segment} (lane {prevRightSimilarLaneIndex} from right, idx {item.m_position.m_lane}): costDebug=TRUE, explore? {nextLane.CheckType(laneType2, vehicleType2)} && {(nextSegmentId != item.m_position.m_segment || laneIndex != (int)item.m_position.m_lane)} && {(byte)(nextLane.m_finalDirection & nextDir2) != 0 && CanLanesConnect(curLaneId, item.m_laneID)}");
				}
#endif

				if ((byte)(nextLane.m_finalDirection & nextDir2) != 0 && CanLanesConnect(curLaneId, item.m_laneID)) {
					//sCurrentState = 12;
					if (nextLane.CheckType(laneType2, vehicleType2) && (nextSegmentId != item.m_position.m_segment || laneIndex != (int)item.m_position.m_lane) && (byte)(nextLane.m_finalDirection & nextDir2) != 0) {
						// NON-STOCK CODE START //
						float nextMaxSpeed = GetLaneSpeedLimit(nextSegmentId, laneIndex, curLaneId, nextLane);// SpeedLimitManager.GetLockFreeGameSpeedLimit(segmentID, laneIndex, curLaneId, ref nextLane);
						bool addCustomTrafficCosts = useAdvancedAI && curLaneId != this._startLaneA && curLaneId != this._startLaneB && curLaneId != this._endLaneA && curLaneId != this._endLaneB && (byte)(nextLane.m_laneType & laneType) != 0 && (nextLane.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None && (nextLane.m_laneType & NetInfo.LaneType.PublicTransport) == NetInfo.LaneType.None;

						if (addCustomTrafficCosts) {
							nextSpeed = CustomRoadAI.laneMeanSpeeds[curLaneId];
							nextSpeed = (float)Math.Max(0.1f, Math.Round(nextSpeed * 0.1f) / 10f); // 0.01, 0.1, 0.2, ... , 1
							if (nextDensitySum <= 0f)
								nextDensity = 0.01f;
							else {
								nextDensity = Math.Min(1f, (float)CustomRoadAI.currentLaneDensities[curLaneId] / nextDensitySum);
								nextDensity = (float)Math.Max(0.01f, Math.Round(nextDensity * 10f) / 10f); // 0.01, 0.25, 0.5, 0.75, 1
							}

							/*if (Options.disableSomething4) {
								Log._Debug($"nextSegment={nextSegmentId} laneIndex={laneIndex} nextDensitySum={nextDensitySum} nextDensity={(float)CustomRoadAI.currentLaneDensities[curLaneId]} / {nextDensitySum} = {nextDensity}");
							}*/
						}
						// NON-STOCK CODE END //

						//sCurrentState = 13;
						float distanceOnBezier = 0f;

						Vector3 a;
						Vector3 b;

						// NON-STOCK CODE START //
						/*if (customLaneChanging) {
							a = instance.m_nodes.m_buffer[targetNodeId].m_position;
							b = meanPrevLanePosition;
							distanceOnBezier = Vector3.Distance(a, b);
						} else {*/
							// NON-STOCK CODE END //
							if ((byte)(nextDir & NetInfo.Direction.Forward) != 0) {
								a = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_bezier.d;
							} else {
								a = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_bezier.a;
							}
							b = prevLanePosition;
							distanceOnBezier = Vector3.Distance(a, b);

						// NON-STOCK CODE START //
						//}
						// NON-STOCK CODE END //

						//sCurrentState = 14;
						//sCurrentState = 15;
						/*if (targetNodeId == 17415) {*/
						//Log.Message($">> PF {this._pathFindIndex} -- Calculated distance: " + distanceOnBezier + " Original distance: " + Vector3.Distance(a1, b1) + " New distance: " + Vector3.Distance(a2, b2) + " next segment: " + segmentID + " from segment: " + item.m_position.m_segment + " a1: " + a1.ToString() + " b1: " + b1.ToString() + " a2: " + a2.ToString() + " b2: " + b2.ToString());
						/*}*/

#if DEBUGCOSTS
						if (costDebug)
							logBuf.Add($"ProcessItemCosts: costs from {nextSegmentId} (off {(byte)(((nextDir & NetInfo.Direction.Forward) == 0) ? 0 : 255)}) to {item.m_position.m_segment} (off {item.m_position.m_offset}), connectOffset={connectOffset}: distanceOnBezier={distanceOnBezier}");
#endif

						if (transitionNode) {
							distanceOnBezier *= 2f;
						}

						//sCurrentState = 16;
						float distanceOverMeanMaxSpeed = distanceOnBezier / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength);
						BufferItem nextItem;
						// NON-STOCK CODE START //
						if (prevIsRealJunction)
							nextItem.m_numSegmentsToJunction = 0;
						else
							nextItem.m_numSegmentsToJunction = item.m_numSegmentsToJunction + 1;
						// NON-STOCK CODE END //
						nextItem.m_position.m_segment = nextSegmentId;
						nextItem.m_position.m_lane = (byte)laneIndex;
						nextItem.m_position.m_offset = (byte)(((nextDir & NetInfo.Direction.Forward) == 0) ? 0 : 255);
						if ((byte)(nextLane.m_laneType & laneType) == 0) {
							nextItem.m_methodDistance = 0f;
						} else {
							nextItem.m_methodDistance = prevMethodDist + distanceOnBezier;
						}

						if (nextLane.m_laneType != NetInfo.LaneType.Pedestrian || nextItem.m_methodDistance < 1000f) {
							//sCurrentState = 17;
							// NON-STOCK CODE START //
							// apply vehicle restrictions
							if (!addCustomTrafficCosts) {
								nextItem.m_comparisonValue = prevComparisonPlusOffsetCostOverSpeed + distanceOverMeanMaxSpeed;
							} else {
								nextItem.m_comparisonValue = item.m_comparisonValue;
								distanceOnBezier += prevOffsetCost;

								if (strictlyAvoidLane) {
									distanceOnBezier *= 75f;
								} else if (avoidLane && (_extVehicleType == null || (_extVehicleType & (ExtVehicleType.CargoTruck | ExtVehicleType.PassengerCar)) != ExtVehicleType.None)) {
									distanceOnBezier *= 3f;
								}

#if DEBUGCOSTS
								if (costDebug) {
									logBuf.Add($"Path from {nextSegmentId} (idx {laneIndex}, id {curLaneId}) to {item.m_position.m_segment} (lane {prevRightSimilarLaneIndex} from right, idx {item.m_position.m_lane}): useAdvancedAI={useAdvancedAI}, addCustomTrafficCosts={addCustomTrafficCosts}, distanceOnBezier={distanceOnBezier} avoidLane={avoidLane} strictlyAvoidLane={strictlyAvoidLane}");
								}
#endif
							}
							// NON-STOCK CODE END //
							nextItem.m_direction = nextDir;
							if (curLaneId == this._startLaneA) {
								if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetA) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetA)) {
									curLaneId = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_nextLane;
									//sCurrentState = 18;
									goto IL_90F;
								}
								float num15 = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetA, nextItem.m_position.m_offset, ref nextSegment, nextLane); // NON-STOCK CODE
								float num16 = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetA)) * 0.003921569f;
								nextItem.m_comparisonValue += num16 * nextSegment.m_averageLength / (num15 * this._maxLength);
							}
							if (curLaneId == this._startLaneB) {
								if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetB) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetB)) {
									curLaneId = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_nextLane;
									//sCurrentState = 19;
									goto IL_90F;
								}
								float num17 = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLane); // NON-STOCK CODE
								float num18 = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetB)) * 0.003921569f;
								nextItem.m_comparisonValue += num18 * nextSegment.m_averageLength / (num17 * this._maxLength);
							}
							if (!this._ignoreBlocked && (nextSegment.m_flags & NetSegment.Flags.Blocked) != NetSegment.Flags.None && (byte)(nextLane.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
								if (addCustomTrafficCosts)
									distanceOnBezier *= 100f;
								else
									nextItem.m_comparisonValue += 0.1f;
								result = true;
							}
							//sCurrentState = 20;
							nextItem.m_lanesUsed = (item.m_lanesUsed | nextLane.m_laneType);
							nextItem.m_laneID = curLaneId;
							if ((byte)(nextLane.m_laneType & laneType) != 0 && nextLane.m_vehicleType == vehicleType) {
								// NON-STOCK CODE START //
								if (!addCustomTrafficCosts) {
									// NON-STOCK CODE END //
									int firstTarget = (int)instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_firstTarget;
									int lastTarget = (int)instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_lastTarget;
									if (laneIndexFromLeft < firstTarget || laneIndexFromLeft >= lastTarget) {
										nextItem.m_comparisonValue += Mathf.Max(1f, distanceOnBezier * 3f - 3f) / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength);
									}
									if (!this._transportVehicle && nextLane.m_laneType == NetInfo.LaneType.TransportVehicle) {
										nextItem.m_comparisonValue += 20f / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * this._maxLength);
									}
									// NON-STOCK CODE START //
								}
								// NON-STOCK CODE END //
							}
							//sCurrentState = 21;

							// NON-STOCK CODE START //
							bool addItem = true;
							if (addCustomTrafficCosts) {
								//sCurrentState = 22;
								int nextRightSimilarLaneIndex;
								if ((byte)(nextLane.m_direction & normDirection) != 0) {
									nextRightSimilarLaneIndex = nextLane.m_similarLaneIndex;
								} else {
									nextRightSimilarLaneIndex = nextLane.m_similarLaneCount - nextLane.m_similarLaneIndex - 1;
								}

								float relLaneDist = nextRightSimilarLaneIndex - prevRightSimilarLaneIndex;
								bool isPreferredLaneChangingDir = relLaneDist > 0 ^ TrafficPriority.IsLeftHandDrive(); // RH traffic: prefer changes to the right
								float laneDist = !isMiddle && nextSegmentId == item.m_position.m_segment ? Options.someValue4 : (float)Math.Abs(relLaneDist);

								if (forceLaneIndex == null && prevIsHighway && nextIsHighway && laneDist > 1) {
									goto IL_8F5;
								}

								if (strictlyAvoidLane) {
									nextSpeed = 0;
									nextDensity = 1;
								}

								float metric = 1f;
								float multMetric = 1f;
								float divMetric = nextMaxSpeed;
								float metricBeforeLanes = 1f;

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
								multMetric = Options.pathCostMultiplicator * nextDensity; // 1 .. pathCostMultiplicator

								// calculate density/speed metric
								metric = Math.Max(0.01f, multMetric) / Math.Max(0.1f, divMetric);
								metricBeforeLanes = metric;

								// multiply with lane distance if distance > 1 or if vehicle does not like to change lanes
								float laneMetric = 1f;
								if ((!isMiddle && nextSegmentId == item.m_position.m_segment) ||
									(_extVehicleType != ExtVehicleType.Emergency && 
									forceLaneIndex == null &&
									(!wantToChangeLane || laneDist > 1))) {
									laneMetric = (float)Math.Pow(isPreferredLaneChangingDir ? 2f : 3f, laneDist);
									metric *= laneMetric;
								}

								// multiply with inverted distance to next junction
								if (forceLaneIndex == null && _extVehicleType != ExtVehicleType.Emergency && laneDist > 0 && nextItem.m_numSegmentsToJunction < 3) {
									float junctionMetric = (float)Math.Pow(Options.someValue3, Math.Max(0f, 3f - nextItem.m_numSegmentsToJunction));
									metric *= junctionMetric;
								}

#if DEBUGCOSTS
								if (costDebug) {
									logBuf.Add($"Path from {nextSegmentId} (lane {nextRightSimilarLaneIndex} from right, idx {laneIndex}, id {curLaneId}) to {item.m_position.m_segment} (lane {prevRightSimilarLaneIndex} from right, idx {item.m_position.m_lane}): nextMaxSpeed={nextMaxSpeed} prevMaxSpeed={prevMaxSpeed} nextMaxSpeed={nextMaxSpeed} divMetric={divMetric} nextDensity={nextDensity} multMetric={multMetric} laneDist={laneDist} laneMetric={laneMetric} metric={metric} metricBeforeLanes={metricBeforeLanes} isMiddle={isMiddle}");
								}
#endif

								//sCurrentState = 24;

								float oldDistanceOverMaxSpeed = distanceOverMeanMaxSpeed;
								distanceOverMeanMaxSpeed = (metric * distanceOnBezier) / this._maxLength;

#if DEBUG
								/*if ((segmentID == 25320 || segmentID == 31177) && Options.disableSomething1)
									Log._Debug($"Costs for lane {curLaneId} @ {segmentID}: prevSpeed={prevSpeed} nextSpeed={nextSpeed} prevDensity={prevDensity} nextDensity={nextDensity} divMetric={divMetric}, multMetric={multMetric} laneDist={laneDist} metric={metric} distanceOnBezier={distanceOnBezier} prevCost={item2.m_comparisonValue} newCost={distanceOnBezier+item2.m_comparisonValue}");*/
#endif

								if (distanceOverMeanMaxSpeed < 0f) {
#if DEBUG
									Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId}, PF {this._pathFindIndex}: distanceOverMeanMaxSpeed < 0! seg. {nextSegmentId}, lane {laneIndex}, off {nextItem.m_position.m_offset} -> {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}. distanceOverMeanMaxSpeed={distanceOverMeanMaxSpeed}, nextSpeed={nextSpeed}, prevSpeed={prevSpeed}");
#endif
									distanceOverMeanMaxSpeed = 0f;
								} else if (Single.IsNaN(distanceOverMeanMaxSpeed) || Single.IsInfinity(distanceOverMeanMaxSpeed)) {
#if DEBUG
									//if (costDebug)
									Log.Error($"Pathfinder ({this._pathFindIndex}): distanceOverMeanMaxSpeed is NaN or Infinity: seg. {nextSegmentId}, lane {laneIndex}, off {nextItem.m_position.m_offset} -> {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}. {distanceOverMeanMaxSpeed} // nextMaxSpeed={nextMaxSpeed} prevMaxSpeed={prevMaxSpeed} nextMaxSpeed={nextMaxSpeed} divMetric={divMetric} nextDensity={nextDensity} multMetric={multMetric} laneDist={laneDist} laneMetric={laneMetric} metric={metric} metricBeforeLanes={metricBeforeLanes}");
#endif
#if DEBUGPF
									//Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId}, PF {this._pathFindIndex}: distanceOverMeanMaxSpeed is NaN! distanceOverMeanMaxSpeed={distanceOverMeanMaxSpeed}, nextSpeed={nextSpeed}, prevSpeed={prevSpeed}");
#endif
									distanceOverMeanMaxSpeed = oldDistanceOverMaxSpeed;
								}
								//sCurrentState = 25;

#if DEBUGCOSTS
								if (costDebug) {
									logBuf.Add($"Path from {nextSegmentId} (lane {nextRightSimilarLaneIndex} from right, idx {laneIndex}) to {item.m_position.m_segment} (lane {prevRightSimilarLaneIndex} from right, idx {item.m_position.m_lane}.");
									//logBuf.Add($"distanceOverMeanMaxSpeed = {distanceOverMeanMaxSpeed} oldDistanceOverMaxSpeed = {oldDistanceOverMaxSpeed}, prevMaxSpeed={prevMaxSpeed}, nextMaxSpeed={nextMaxSpeed}, prevSpeed={prevSpeed}, nextSpeed={nextSpeed}");
									logBuf.Add($"distanceOverMeanMaxSpeed = {distanceOverMeanMaxSpeed} oldDistanceOverMaxSpeed = {oldDistanceOverMaxSpeed}, prevMaxSpeed={prevMaxSpeed}, nextMaxSpeed={nextMaxSpeed}, nextSpeed={nextSpeed} nextDensity={nextDensity}");
								}
#endif

								nextItem.m_comparisonValue += distanceOverMeanMaxSpeed;
#if DEBUGCOSTS
								if (costDebug) {
									logBuf.Add($"Total cost = {distanceOverMeanMaxSpeed}, comparison value = {nextItem.m_comparisonValue}");
								}
#endif
#if DEBUGCOSTS
								if (costDebug) {
									foreach (String toLog in logBuf) {
										Log._Debug($"Pathfinder ({this._pathFindIndex}): " + toLog);
									}
									logBuf.Clear();
								}
#endif

								//sCurrentState = 26;
								if (nextItem.m_comparisonValue < 0f) {
#if DEBUG
									Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId}, PF {this._pathFindIndex}: Comparison value < 0! seg. {nextSegmentId}, lane {laneIndex}, off {nextItem.m_position.m_offset} -> {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}. distanceOverMeanMaxSpeed={distanceOverMeanMaxSpeed}, nextSpeed={nextSpeed}");
#endif
									nextItem.m_comparisonValue = 0f;
								} else if (nextItem.m_comparisonValue > 1f || Single.IsNaN(nextItem.m_comparisonValue) || Single.IsInfinity(nextItem.m_comparisonValue)) {
#if DEBUGPF
									if (debug)
										Log._Debug($"Pathfinder ({this._pathFindIndex}): comparisonValue is >1, NaN or Infinity: {nextItem.m_comparisonValue}. seg. {nextSegmentId}, lane {laneIndex}, off {nextItem.m_position.m_offset} -> {item.m_position.m_segment}, lane {item.m_position.m_lane}, off {item.m_position.m_offset}.");
#endif
#if DEBUG
									//Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId}, PF {this._pathFindIndex}: Comparison value > 1, NaN or infinity! distanceOverMeanMaxSpeed={distanceOverMeanMaxSpeed}, nextSpeed={nextSpeed}, prevSpeed={prevSpeed}");
#endif
									addItem = false;
								}
								//sCurrentState = 27;
#if DEBUGPF
								if (debug) {
									//Log.Message($">> PF {this._pathFindIndex} -- seg {item2.m_position.m_segment}, lane {item2.m_position.m_lane} (idx {item2.m_laneID}), off {item2.m_position.m_offset}, cost {item2.m_comparisonValue}, totalCost {totalCost} = traffic={trafficCost}, junction={junctionCost}, lane={laneChangeCost}");
								}
#endif
							}
							//sCurrentState = 28;

							if (forceLaneIndex != null && laneIndex == forceLaneIndex && addItem) {
								foundForced = true;
							}

							/*if (addCustomCosts && ((item.m_position.m_segment == 2062 && item2.m_position.m_segment == 23640) || (item.m_position.m_segment == 23640 && item2.m_position.m_segment == 29505) || (item.m_position.m_segment == 29505 && item2.m_position.m_segment == 24975) || (item.m_position.m_segment == 24975 && item2.m_position.m_segment == 9535) || (item.m_position.m_segment == 9535 && item2.m_position.m_segment == 20914) || (item.m_position.m_segment == 20914 && item2.m_position.m_segment == 21545))) {
								Log.Message($">> PF {this._pathFindIndex} -- Adding item: seg {item2.m_position.m_segment}, lane {item2.m_position.m_lane} (idx {item2.m_laneID}), off {item2.m_position.m_offset} -> seg {item.m_position.m_segment}, lane {item.m_position.m_lane} (idx {item.m_laneID}), off {item.m_position.m_offset}, cost {item2.m_comparisonValue}, methodDist {item2.m_methodDistance}");
							}*/

							// NON-STOCK CODE END //

							if (addItem) {
#if DEBUGPF
								if (debug)
									Log._Debug($">> PF {this._pathFindIndex} -- Adding item: seg {nextItem.m_position.m_segment}, lane {nextItem.m_position.m_lane} (idx {nextItem.m_laneID}), off {nextItem.m_position.m_offset} -> seg {item.m_position.m_segment}, lane {item.m_position.m_lane} (idx {item.m_laneID}), off {item.m_position.m_offset}, cost {nextItem.m_comparisonValue}, previous cost {item.m_comparisonValue}, methodDist {nextItem.m_methodDistance}");
#endif
								//sCurrentState = 29;
								this.AddBufferItem(nextItem, item.m_position);
								//sCurrentState = 30;
							} else {
								//sCurrentState = 31;
#if DEBUGPF
								if (debug)
									Log._Debug($">> PF {this._pathFindIndex} -- NOT adding item");
#endif
							}
							//sCurrentState = 32;
						} else {
						}
						//sCurrentState = 33;
					}
					//sCurrentState = 34;
					goto IL_8F5;
				}
				//sCurrentState = 35;
				if ((byte)(nextLane.m_laneType & laneType) != 0 && (nextLane.m_vehicleType & vehicleType) != VehicleInfo.VehicleType.None) {
					newLaneIndexFromLeft++;
					goto IL_8F5;
				}
				//sCurrentState = 36;
				goto IL_8F5;
				IL_90F:
				//sCurrentState = 37;
				laneIndex++;
				continue;
				IL_8F5:
#if DEBUGCOSTS
				if (costDebug) {
					foreach (String toLog in logBuf) {
						Log._Debug($"Pathfinder ({this._pathFindIndex}): " + toLog);
					}
					logBuf.Clear();
				}
#endif
				//sCurrentState = 38;
				curLaneId = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_nextLane;
				goto IL_90F;
			} // foreach lane
			//sCurrentState = 39;
			laneIndexFromLeft = newLaneIndexFromLeft;
			return result;
		}

#region stock code
		// 4
		private void ProcessItemPedBicycle(BufferItem item, ushort targetNodeId, ushort segmentID, ref NetSegment segment, byte connectOffset, int laneIndex, uint lane) {
			if ((segment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None) {
				return;
			}
			NetManager instance = Singleton<NetManager>.instance;
			NetInfo info = segment.Info;
			NetInfo info2 = instance.m_segments.m_buffer[(int)item.m_position.m_segment].Info;
			int num = info.m_lanes.Length;
			float distance;
			byte offset;
			Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)item.m_laneID)].CalculatePosition((float)connectOffset * 0.003921569f);
			if (segmentID == item.m_position.m_segment) {
				// next segment is previous segment
				Vector3 a = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].CalculatePosition((float)connectOffset * 0.003921569f);
				distance = Vector3.Distance(a, b);
				offset = connectOffset;
			} else {
				// next segment differs from previous segment
				NetInfo.Direction direction = (targetNodeId != segment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
				Vector3 a;
				if ((byte)(direction & NetInfo.Direction.Forward) != 0) {
					a = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].m_bezier.d;
				} else {
					a = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].m_bezier.a;
				}
				distance = Vector3.Distance(a, b);
				offset = (byte)(((direction & NetInfo.Direction.Forward) == 0) ? 0 : 255);
			}
			float prevMaxSpeed = 1f;
			float prevSpeed = 1f;
			NetInfo.LaneType laneType = NetInfo.LaneType.None;
			VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None; // NON-STOCK CODE
			if ((int)item.m_position.m_lane < info2.m_lanes.Length) {
				NetInfo.Lane prevLane = info2.m_lanes[(int)item.m_position.m_lane];
				prevMaxSpeed = GetLaneSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, prevLane); // SpeedLimitManager.GetLockFreeGameSpeedLimit(item.m_position.m_segment, item.m_position.m_lane, item.m_laneID, ref lane2); // NON-STOCK CODE
				laneType = prevLane.m_laneType;
				vehicleType = prevLane.m_vehicleType; // NON-STOCK CODE
				if ((byte)(laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
					laneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
				prevSpeed = this.CalculateLaneSpeed(prevMaxSpeed, connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[(int)item.m_position.m_segment], prevLane); // NON-STOCK CODE
			}
			float prevCost = instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_averageLength;
			// NON-STOCK CODE START
			if (_extVehicleType == ExtVehicleType.Bicycle) {
				if ((vehicleType & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None) {
					prevCost *= 0.95f;
				} else if ((instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_flags & NetSegment.Flags.BikeBan) != NetSegment.Flags.None) {
					prevCost *= 5f;
				}
			}
			bool nextIsRealJunction = instance.m_nodes.m_buffer[targetNodeId].CountSegments() > 2;
			ushort sourceNodeId = (targetNodeId == instance.m_segments.m_buffer[item.m_position.m_segment].m_startNode) ? instance.m_segments.m_buffer[item.m_position.m_segment].m_endNode : instance.m_segments.m_buffer[item.m_position.m_segment].m_startNode; // no lane changing directly in front of a junction
			bool prevIsRealJunction = instance.m_nodes.m_buffer[sourceNodeId].CountSegments() > 2;
			// NON-STOCK CODE END
			float offsetLength = (float)Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * 0.003921569f * prevCost;
			float methodDistance = item.m_methodDistance + offsetLength;
			float comparisonValue = item.m_comparisonValue + offsetLength / (prevSpeed * this._maxLength);
			if (laneIndex < num) {
				NetInfo.Lane nextLane = info.m_lanes[laneIndex];
				BufferItem nextItem;
				// NON-STOCK CODE START //
				if (prevIsRealJunction)
					nextItem.m_numSegmentsToJunction = 0;
				else
					nextItem.m_numSegmentsToJunction = item.m_numSegmentsToJunction + 1;
				// NON-STOCK CODE END //
				nextItem.m_position.m_segment = segmentID;
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
				float nextMaxSpeed = GetLaneSpeedLimit(segmentID, (uint)laneIndex, lane, nextLane); // SpeedLimitManager.GetLockFreeGameSpeedLimit(segmentID, (uint)laneIndex, lane, ref lane3); // NON-STOCK CODE
				if (nextLane.m_laneType != NetInfo.LaneType.Pedestrian || nextItem.m_methodDistance < 1000f) {
					nextItem.m_comparisonValue = comparisonValue + distance / ((prevMaxSpeed + nextMaxSpeed) * 0.25f * this._maxLength); // NON-STOCK CODE
					if ((segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
						nextItem.m_direction = NetInfo.InvertDirection(nextLane.m_finalDirection);
					} else {
						nextItem.m_direction = nextLane.m_finalDirection;
					}
					if (lane == this._startLaneA) {
						if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetA) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetA)) {
							return;
						}
						float nextSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetA, nextItem.m_position.m_offset, ref segment, nextLane); // NON-STOCK CODE
						float nextOffset = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetA)) * 0.003921569f;
						nextItem.m_comparisonValue += nextOffset * segment.m_averageLength / (nextSpeed * this._maxLength);
					}
					if (lane == this._startLaneB) {
						if (((byte)(nextItem.m_direction & NetInfo.Direction.Forward) == 0 || nextItem.m_position.m_offset < this._startOffsetB) && ((byte)(nextItem.m_direction & NetInfo.Direction.Backward) == 0 || nextItem.m_position.m_offset > this._startOffsetB)) {
							return;
						}
						float nextSpeed = this.CalculateLaneSpeed(nextMaxSpeed, this._startOffsetB, nextItem.m_position.m_offset, ref segment, nextLane); // NON-STOCK CODE
						float nextOffset = (float)Mathf.Abs((int)(nextItem.m_position.m_offset - this._startOffsetB)) * 0.003921569f;
						nextItem.m_comparisonValue += nextOffset * segment.m_averageLength / (nextSpeed * this._maxLength);
					}
					nextItem.m_laneID = lane;
					nextItem.m_lanesUsed = (item.m_lanesUsed | nextLane.m_laneType);
					this.AddBufferItem(nextItem, item.m_position);
				}
			}
		}
#endregion

		private float CalculateLaneSpeed(float speedLimit, byte startOffset, byte endOffset, ref NetSegment segment, NetInfo.Lane laneInfo) {
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
			//bCurrentState = 0;
#if DEBUGPF
			/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
				Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: AddBufferItem item {item.m_position.m_segment} {item.m_position.m_lane}");*/
#endif

			uint num = _laneLocation[(int)((UIntPtr)item.m_laneID)];
			uint num2 = num >> 16;
			int num3 = (int)(num & 65535u);
			int comparisonBufferPos;
			if (num2 == _pathFindIndex) {
				//bCurrentState = 1;
				if (item.m_comparisonValue >= _buffer[num3].m_comparisonValue) {
					return;
				}
				//bCurrentState = 2;
				int num4 = num3 >> 6;
				int num5 = num3 & -64;
				if (num4 < _bufferMinPos || (num4 == _bufferMinPos && num5 < _bufferMin[num4])) {
					return;
				}
				//bCurrentState = 3;
				comparisonBufferPos = Mathf.Max(Mathf.RoundToInt(item.m_comparisonValue * 1024f), _bufferMinPos);
				if (comparisonBufferPos == num4) {
					_buffer[num3] = item;
					_laneTarget[(int)((UIntPtr)item.m_laneID)] = target;
					return;
				}
				//bCurrentState = 4;
				int num7 = num4 << 6 | _bufferMax[num4]--;
				BufferItem bufferItem = _buffer[num7];
				_laneLocation[(int)((UIntPtr)bufferItem.m_laneID)] = num;
				_buffer[num3] = bufferItem;
				//bCurrentState = 5;
			} else {
				//bCurrentState = 6;
				comparisonBufferPos = Mathf.Max(Mathf.RoundToInt(item.m_comparisonValue * 1024f), _bufferMinPos);
			}
			//bCurrentState = 7;
			if (comparisonBufferPos >= 1024) {
				return;
			}
			//bCurrentState = 8;
			if (comparisonBufferPos < 0) {
#if DEBUG
				Log.Error("comparisonBufferPos < 0!");
#endif
				return;
			}
			//bCurrentState = 9;
			while (_bufferMax[comparisonBufferPos] == 63) {
				//bCurrentState = 10;
				comparisonBufferPos++;
				if (comparisonBufferPos == 1024) {
					//bCurrentState = 11;
					return;
				}
			}
			//bCurrentState = 12;
			if (comparisonBufferPos > _bufferMaxPos) {
				_bufferMaxPos = comparisonBufferPos;
			}
			//bCurrentState = 13;
			num3 = (comparisonBufferPos << 6 | ++_bufferMax[comparisonBufferPos]);
			_buffer[num3] = item;
			_laneLocation[(int)((UIntPtr)item.m_laneID)] = (_pathFindIndex << 16 | (uint)num3);
			_laneTarget[(int)((UIntPtr)item.m_laneID)] = target;
			//bCurrentState = 14;
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
			//tCurrentState = 0;
			while (true) {
				//Log.Message($"Pathfind Thread #{Thread.CurrentThread.ManagedThreadId} iteration!");
				try {
					//tCurrentState = 1;
					Monitor.Enter(QueueLock);
					//tCurrentState = 3;

					while (QueueFirst == 0u && !Terminated) {
						//tCurrentState = 4;
#if DEBUGPF
						/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
							Log._Debug($"Pathfind Thread #{Thread.CurrentThread.ManagedThreadId} waiting now for queue lock {QueueLock.GetHashCode()}!");*/
#endif
						if (!Monitor.Wait(QueueLock, SYNC_TIMEOUT)) {
							//tCurrentState = 5;
#if DEBUGPF
							/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
								Log.Warning($"Pathfind Thread #{Thread.CurrentThread.ManagedThreadId} *WAIT TIMEOUT* waiting for queue lock {QueueLock.GetHashCode()}!");*/
#endif
						}
					}
					//tCurrentState = 6;
#if DEBUGPF
					/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
						Log._Debug($"Pathfind Thread #{Thread.CurrentThread.ManagedThreadId} is continuing now!");*/
#endif
					if (Terminated) {
						break;
					}
					Calculating = QueueFirst;
					QueueFirst = PathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_nextPathUnit;
					if (QueueFirst == 0u) {
						QueueLast = 0u;
						m_queuedPathFindCount = 0;
					} else {
						m_queuedPathFindCount--;
					}
#if DEBUGPF
					/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
						Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PathFindThread: Starting pathfinder. Remaining queued pathfinders: {m_queuedPathFindCount}"); */
#endif
					PathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_nextPathUnit = 0u;
					PathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags = (byte)((PathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags & -2) | 2);
				} catch (Exception e) {
					Log.Error("CustomPathFind.PathFindThread Error (1): " + e.ToString());
				} finally {
					Monitor.Exit(QueueLock);
				}
				//tCurrentState = 7;
				try {
					m_pathfindProfiler.BeginStep();
#if DEBUGPF
					/*if (m_queuedPathFindCount > 100 && Options.disableSomething1)
						Log._Debug($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: Calling PathFindImplementation now. Calculating={Calculating}");*/
#endif
					//tCurrentState = 8;
					PathFindImplementation(Calculating, ref PathUnits.m_buffer[(int)((UIntPtr)Calculating)]);
					//tCurrentState = 9;
				} catch (Exception ex) {
					Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId} Path find error: " + ex.ToString());
					//UIView.ForwardException(ex);
					var expr_1A0Cp0 = PathUnits.m_buffer;
					var expr_1A0Cp1 = (UIntPtr)Calculating;
					expr_1A0Cp0[(int)expr_1A0Cp1].m_pathFindFlags = (byte)(expr_1A0Cp0[(int)expr_1A0Cp1].m_pathFindFlags | 8);
				} finally {
					m_pathfindProfiler.EndStep();
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
					//tCurrentState = 12;
					PathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags = (byte)(PathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags & -3);
					Singleton<PathManager>.instance.ReleasePath(Calculating);
					Calculating = 0u;
					Monitor.Pulse(QueueLock);
					//tCurrentState = 13;
				} catch (Exception e) {
					Log.Error("CustomPathFind.PathFindThread Error (3): " + e.ToString());
				} finally {
					Monitor.Exit(QueueLock);
				}
				//tCurrentState = 14;
			}
		}

		protected virtual bool CanUseLane(bool debug, ushort segmentId, uint laneIndex, uint laneId, NetInfo.Lane laneInfo) {
			if (Options.disableSomething4)
				return true;

			if (_extVehicleType == null || _extVehicleType == ExtVehicleType.None)
				return true;

			if (laneInfo == null)
				laneInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex];

			if ((laneInfo.m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train)) == VehicleInfo.VehicleType.None)
				return true;

			ExtVehicleType allowedTypes = VehicleRestrictionsManager.GetAllowedVehicleTypes(segmentId, laneIndex, laneId, laneInfo);
#if DEBUGPF
			if (debug) {
				Log._Debug($"CanUseLane: segmentId={segmentId} laneIndex={laneIndex} laneId={laneId}, _extVehicleType={_extVehicleType} _vehicleTypes={_vehicleTypes} _laneTypes={_laneTypes} _transportVehicle={_transportVehicle} _isHeavyVehicle={_isHeavyVehicle} allowedTypes={allowedTypes} res={((allowedTypes & _extVehicleType) != ExtVehicleType.None)}");
            }
#endif
			
			return ((allowedTypes & _extVehicleType) != ExtVehicleType.None);
		}

		protected virtual float GetLaneSpeedLimit(ushort segmentId, uint laneIndex, uint laneId, NetInfo.Lane lane) {
			return SpeedLimitManager.GetLockFreeGameSpeedLimit(segmentId, (uint)laneIndex, laneId, lane);
		}

		protected virtual bool CanLanesConnect(uint laneId1, uint laneId2) {
			return true;
		}

		protected virtual bool IsLaneArrowChangerEnabled() {
			return true;
		}
	}
}
