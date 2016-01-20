#define DEBUGPFx
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
using TrafficManager.Custom.Manager;
using TrafficManager.Custom.AI;
using TrafficManager.TrafficLight;
using TrafficManager.State;

namespace TrafficManager.Custom.Misc {
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

		public readonly static int SYNC_TIMEOUT = 50;

		//Expose the private fields
		FieldInfo _fieldpathUnits;
		FieldInfo _fieldQueueFirst;
		FieldInfo _fieldQueueLast;
		FieldInfo _fieldQueueLock;
		FieldInfo _fieldCalculating;
		FieldInfo _fieldTerminated;
		FieldInfo _fieldPathFindThread;

		private Array32<PathUnit> _pathUnits {
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
		private Thread CustomPathFindThread {
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
		/*
				private TrafficRoadRestrictions.VehicleTypes _vehicleType;
		*/
		private Randomizer _pathRandomizer;
		private uint _pathFindIndex;
		private NetInfo.LaneType _laneTypes;
		private VehicleInfo.VehicleType _vehicleTypes;


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
			_pathUnits = PathManager.instance.m_pathUnits;
			QueueLock = new object();
			_laneLocation = new uint[262144];
			_laneTarget = new PathUnit.Position[262144];
			_bufferMin = new int[1024];
			_bufferMax = new int[1024];

			m_pathfindProfiler = new ThreadProfiler();

			CustomPathFindThread = new Thread(PathFindThread) { Name = "Pathfind" };
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
				while (!Monitor.TryEnter(QueueLock, SYNC_TIMEOUT)) {
#if DEBUGLOCKS
					++lockIter;
					if (lockIter % 100 == 0)
						Log.Message("CustomPathFind.OnDestroy lockIter: " + lockIter);
#endif
				}

				Terminated = true;
				Monitor.PulseAll(QueueLock);
			} catch (Exception e) {
				Log.Error("CustomPathFind.OnDestroy Error: " + e.ToString());
			} finally {
				Monitor.Exit(QueueLock);
			}
		}

		public new bool CalculatePath(uint unit, bool skipQueue) {
			if (Singleton<PathManager>.instance.AddPathReference(unit)) {
#if DEBUGLOCKS
				uint lockIter = 0;
#endif
				try {
					while (!Monitor.TryEnter(QueueLock, SYNC_TIMEOUT)) {
#if DEBUGLOCKS
						++lockIter;
						if (lockIter % 100 == 0)
							Log.Message("CustomPathFind.CalculatePath lockIter: " + lockIter);
#endif
					}

					if (skipQueue) {
						if (QueueLast == 0u) {
							QueueLast = unit;
						} else {
							_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit = QueueFirst;
						}
						QueueFirst = unit;
					} else {
						if (QueueLast == 0u) {
							QueueFirst = unit;
						} else {
							_pathUnits.m_buffer[(int)((UIntPtr)QueueLast)].m_nextPathUnit = unit;
						}
						QueueLast = unit;
					}
					var exprBdCp0 = _pathUnits.m_buffer;
					var exprBdCp1 = (UIntPtr)unit;
					exprBdCp0[(int)exprBdCp1].m_pathFindFlags = (byte)(exprBdCp0[(int)exprBdCp1].m_pathFindFlags | 1);
					m_queuedPathFindCount++;
					if (m_queuedPathFindCount > 1000 && m_queuedPathFindCount % 100 == 0) {
						Log.Message($"CustomPathFind.CalculatePath, Thread #{Thread.CurrentThread.ManagedThreadId}: queued path finds: {m_queuedPathFindCount}");
					}
					Monitor.Pulse(QueueLock);
				} catch (Exception e) {
					Log.Error("CustomPathFind.CalculatePath Error: " + e.ToString());
				} finally {
					Monitor.Exit(QueueLock);
				}
				return true;
			}
			return false;
		}


		public new void WaitForAllPaths() {
#if DEBUGLOCKS
			uint lockIter = 0;
#endif
			try {
				while (!Monitor.TryEnter(QueueLock, SYNC_TIMEOUT)) {
#if DEBUGLOCKS
					++lockIter;
					if (lockIter % 100 == 0)
						Log.Message("CustomPathFind.OnDestroy lockIter: " + lockIter);
#endif
				}
#if DEBUGLOCKS
				uint waitIter = 0;
#endif
				while ((QueueFirst != 0u || Calculating != 0u) && !Terminated) {
					while (!Monitor.Wait(QueueLock, SYNC_TIMEOUT)) {
#if DEBUGLOCKS
						++waitIter;
						if (waitIter % 100 == 0)
							Log.Message("CustomPathFind.WaitForAllPaths waitIter: " + waitIter);
#endif
					}
				}
			} catch (Exception e) {
				Log.Error("CustomPathFind.WaitForAllPaths Error: " + e.ToString());
			} finally {
				Monitor.Exit(QueueLock);
			}
		}

		// PathFind
		private void PathFindImplementation(uint unit, ref PathUnit data) {
			NetManager instance = Singleton<NetManager>.instance;
			this._laneTypes = (NetInfo.LaneType)this._pathUnits.m_buffer[(int)((UIntPtr)unit)].m_laneTypes;
			this._vehicleTypes = (VehicleInfo.VehicleType)this._pathUnits.m_buffer[(int)((UIntPtr)unit)].m_vehicleTypes;
			this._maxLength = this._pathUnits.m_buffer[(int)((UIntPtr)unit)].m_length;
			this._pathFindIndex = (this._pathFindIndex + 1u & 32767u);
			this._pathRandomizer = new Randomizer(unit);
			this._isHeavyVehicle = ((this._pathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags & 16) != 0);
			this._ignoreBlocked = ((this._pathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags & 32) != 0);
			this._stablePath = ((this._pathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags & 64) != 0);
			this._transportVehicle = ((byte)(this._laneTypes & NetInfo.LaneType.TransportVehicle) != 0);
			if ((byte)(this._laneTypes & NetInfo.LaneType.Vehicle) != 0) {
				this._laneTypes |= NetInfo.LaneType.TransportVehicle;
			}
			int num = (int)(this._pathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount & 15);
			int num2 = this._pathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount >> 4;
			BufferItem bufferItemStartA;
			if (data.m_position00.m_segment != 0 && num >= 1) {
				this._startLaneA = PathManager.GetLaneID(data.m_position00);
				this._startOffsetA = data.m_position00.m_offset;
				bufferItemStartA.m_laneID = this._startLaneA;
				bufferItemStartA.m_position = data.m_position00;
				this.GetLaneDirection(data.m_position00, out bufferItemStartA.m_direction, out bufferItemStartA.m_lanesUsed);
				bufferItemStartA.m_comparisonValue = 0f;
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
			BufferItem curBufferItem = default(BufferItem);
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
#if DEBUG
			uint wIter = 0;
#endif
			//Log.Message($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: STARTING MAIN LOOP! bufferMinPos: {this._bufferMinPos}, bufferMaxPos: {this._bufferMaxPos}, startA: {bufferItemStartA.m_position.m_segment}, startB: {bufferItemStartB.m_position.m_segment}, endA: {bufferItemEndA.m_position.m_segment}, endB: {bufferItemEndB.m_position.m_segment}");
			while (this._bufferMinPos <= this._bufferMaxPos) {
				/*if (m_queuedPathFindCount > 1000)
					Log.Message($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: MAIN LOOP RUNNING! bufferMinPos: {this._bufferMinPos}, bufferMaxPos: {this._bufferMaxPos}, startA: {bufferItemStartA.m_position.m_segment}, startB: {bufferItemStartB.m_position.m_segment}, endA: {bufferItemEndA.m_position.m_segment}, endB: {bufferItemEndB.m_position.m_segment}");*/
				int num4 = this._bufferMin[this._bufferMinPos];
				int num5 = this._bufferMax[this._bufferMinPos];
				if (num4 > num5) {
					this._bufferMinPos++;
				} else {
					this._bufferMin[this._bufferMinPos] = num4 + 1;
					BufferItem prevItem = this._buffer[(this._bufferMinPos << 6) + num4];
					if (prevItem.m_position.m_segment == bufferItemStartA.m_position.m_segment && prevItem.m_position.m_lane == bufferItemStartA.m_position.m_lane) {
						if ((byte)(prevItem.m_direction & NetInfo.Direction.Forward) != 0 && prevItem.m_position.m_offset >= this._startOffsetA) {
							curBufferItem = prevItem;
							startOffset = this._startOffsetA;
							canFindPath = true;
							break;
						}
						if ((byte)(prevItem.m_direction & NetInfo.Direction.Backward) != 0 && prevItem.m_position.m_offset <= this._startOffsetA) {
							curBufferItem = prevItem;
							startOffset = this._startOffsetA;
							canFindPath = true;
							break;
						}
					}
					if (prevItem.m_position.m_segment == bufferItemStartB.m_position.m_segment && prevItem.m_position.m_lane == bufferItemStartB.m_position.m_lane) {
						if ((byte)(prevItem.m_direction & NetInfo.Direction.Forward) != 0 && prevItem.m_position.m_offset >= this._startOffsetB) {
							curBufferItem = prevItem;
							startOffset = this._startOffsetB;
							canFindPath = true;
							break;
						}
						if ((byte)(prevItem.m_direction & NetInfo.Direction.Backward) != 0 && prevItem.m_position.m_offset <= this._startOffsetB) {
							curBufferItem = prevItem;
							startOffset = this._startOffsetB;
							canFindPath = true;
							break;
						}
					}
					if ((byte)(prevItem.m_direction & NetInfo.Direction.Forward) != 0) {
						ushort startNode = instance.m_segments.m_buffer[(int)prevItem.m_position.m_segment].m_startNode;
						this.ProcessItemMain(prevItem, startNode, ref instance.m_nodes.m_buffer[(int)startNode], 0, false);
					}
					if ((byte)(prevItem.m_direction & NetInfo.Direction.Backward) != 0) {
						ushort endNode = instance.m_segments.m_buffer[(int)prevItem.m_position.m_segment].m_endNode;
						this.ProcessItemMain(prevItem, endNode, ref instance.m_nodes.m_buffer[(int)endNode], 255, false);
					}
					int num6 = 0;
					ushort specialNodeId = instance.m_lanes.m_buffer[(int)((UIntPtr)prevItem.m_laneID)].m_nodes;
					if (specialNodeId != 0) {
						ushort startNode2 = instance.m_segments.m_buffer[(int)prevItem.m_position.m_segment].m_startNode;
						ushort endNode2 = instance.m_segments.m_buffer[(int)prevItem.m_position.m_segment].m_endNode;
						bool flag2 = ((instance.m_nodes.m_buffer[(int)startNode2].m_flags | instance.m_nodes.m_buffer[(int)endNode2].m_flags) & NetNode.Flags.Disabled) != NetNode.Flags.None;
						while (specialNodeId != 0) {
							NetInfo.Direction direction = NetInfo.Direction.None;
							byte laneOffset = instance.m_nodes.m_buffer[(int)specialNodeId].m_laneOffset;
							if (laneOffset <= prevItem.m_position.m_offset) {
								direction |= NetInfo.Direction.Forward;
							}
							if (laneOffset >= prevItem.m_position.m_offset) {
								direction |= NetInfo.Direction.Backward;
							}
							if ((byte)(prevItem.m_direction & direction) != 0 && (!flag2 || (instance.m_nodes.m_buffer[(int)specialNodeId].m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None)) {
								this.ProcessItemMain(prevItem, specialNodeId, ref instance.m_nodes.m_buffer[(int)specialNodeId], laneOffset, true);
							}
							specialNodeId = instance.m_nodes.m_buffer[(int)specialNodeId].m_nextLaneNode;
							if (++num6 == 32768) {
								break;
							}
						}
					}
				}
#if DEBUG
				++wIter;
				if (wIter > 1000000) {
					Log.Error("Too many iterations in PathFindImpl.");
					break;
				}
#endif
			}
			/*if (m_queuedPathFindCount > 1000)
				Log.Message($"THREAD #{Thread.CurrentThread.ManagedThreadId} PF {this._pathFindIndex}: MAIN LOOP FINISHED! bufferMinPos: {this._bufferMinPos}, bufferMaxPos: {this._bufferMaxPos}, startA: {bufferItemStartA.m_position.m_segment}, startB: {bufferItemStartB.m_position.m_segment}, endA: {bufferItemEndA.m_position.m_segment}, endB: {bufferItemEndB.m_position.m_segment}");*/
			if (!canFindPath) {
				PathUnit[] expr_909_cp_0 = this._pathUnits.m_buffer;
				UIntPtr expr_909_cp_1 = (UIntPtr)unit;
				expr_909_cp_0[(int)expr_909_cp_1].m_pathFindFlags = (byte)(expr_909_cp_0[(int)expr_909_cp_1].m_pathFindFlags | 8);
				return;
			}
			float num8 = curBufferItem.m_comparisonValue * this._maxLength;
			this._pathUnits.m_buffer[(int)((UIntPtr)unit)].m_length = num8;
			uint pathUnitId = unit;
			int num10 = 0;
			int num11 = 0;
			PathUnit.Position position = curBufferItem.m_position;
			if ((position.m_segment != bufferItemEndA.m_position.m_segment || position.m_lane != bufferItemEndA.m_position.m_lane || position.m_offset != bufferItemEndA.m_position.m_offset) && (position.m_segment != bufferItemEndB.m_position.m_segment || position.m_lane != bufferItemEndB.m_position.m_lane || position.m_offset != bufferItemEndB.m_position.m_offset)) {
				if (startOffset != position.m_offset) {
					PathUnit.Position position2 = position;
					position2.m_offset = startOffset;
					this._pathUnits.m_buffer[(int)((UIntPtr)pathUnitId)].SetPosition(num10++, position2);
				}
				this._pathUnits.m_buffer[(int)((UIntPtr)pathUnitId)].SetPosition(num10++, position);
				position = this._laneTarget[(int)((UIntPtr)curBufferItem.m_laneID)];
			}
			for (int k = 0; k < 262144; k++) {
				this._pathUnits.m_buffer[(int)((UIntPtr)pathUnitId)].SetPosition(num10++, position);
				if ((position.m_segment == bufferItemEndA.m_position.m_segment && position.m_lane == bufferItemEndA.m_position.m_lane && position.m_offset == bufferItemEndA.m_position.m_offset) || (position.m_segment == bufferItemEndB.m_position.m_segment && position.m_lane == bufferItemEndB.m_position.m_lane && position.m_offset == bufferItemEndB.m_position.m_offset)) {
					this._pathUnits.m_buffer[(int)((UIntPtr)pathUnitId)].m_positionCount = (byte)num10;
					num11 += num10;
					if (num11 != 0) {
						pathUnitId = this._pathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit;
						num10 = (int)this._pathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount;
						int num12 = 0;
						while (pathUnitId != 0u) {
							this._pathUnits.m_buffer[(int)((UIntPtr)pathUnitId)].m_length = num8 * (float)(num11 - num10) / (float)num11;
							num10 += (int)this._pathUnits.m_buffer[(int)((UIntPtr)pathUnitId)].m_positionCount;
							pathUnitId = this._pathUnits.m_buffer[(int)((UIntPtr)pathUnitId)].m_nextPathUnit;
							if (++num12 >= 262144) {
								CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
								break;
							}
						}
					}
					PathUnit[] expr_C16_cp_0 = this._pathUnits.m_buffer;
					UIntPtr expr_C16_cp_1 = (UIntPtr)unit;
					expr_C16_cp_0[(int)expr_C16_cp_1].m_pathFindFlags = (byte)(expr_C16_cp_0[(int)expr_C16_cp_1].m_pathFindFlags | 4);
					return;
				}
				if (num10 == 12) {
#if DEBUGLOCKS
					uint lockIter = 0;
#endif
					uint num13;
					try {
						while (!Monitor.TryEnter(this._bufferLock, SYNC_TIMEOUT)) {
#if DEBUGLOCKS
							++lockIter;
							if (lockIter % 100 == 0)
								Log.Message("CustomPathFind.PathFindImplementation lockIter: " + lockIter);
#endif
						}

						if (!this._pathUnits.CreateItem(out num13, ref this._pathRandomizer)) {
							PathUnit[] expr_D15_cp_0 = this._pathUnits.m_buffer;
							UIntPtr expr_D15_cp_1 = (UIntPtr)unit;
							expr_D15_cp_0[(int)expr_D15_cp_1].m_pathFindFlags = (byte)(expr_D15_cp_0[(int)expr_D15_cp_1].m_pathFindFlags | 8);
							return;
						}
						this._pathUnits.m_buffer[(int)((UIntPtr)num13)] = this._pathUnits.m_buffer[(int)((UIntPtr)pathUnitId)];
						this._pathUnits.m_buffer[(int)((UIntPtr)num13)].m_referenceCount = 1;
						this._pathUnits.m_buffer[(int)((UIntPtr)num13)].m_pathFindFlags = 4;
						this._pathUnits.m_buffer[(int)((UIntPtr)pathUnitId)].m_nextPathUnit = num13;
						this._pathUnits.m_buffer[(int)((UIntPtr)pathUnitId)].m_positionCount = (byte)num10;
						num11 += num10;
						Singleton<PathManager>.instance.m_pathUnitCount = (int)(this._pathUnits.ItemCount() - 1u);
					} catch (Exception e) {
						Log.Error("CustomPathFind.PathFindImplementation Error: " + e.ToString());
						break;
					} finally {
						Monitor.Exit(this._bufferLock);
					}
					pathUnitId = num13;
					num10 = 0;
				}
				uint laneID = PathManager.GetLaneID(position);
				// NON-STOCK CODE START
				//CustomRoadAI.AddTraffic(laneID, (ushort)(this._isHeavyVehicle ? 8 : 4));
				// NON-STOCK CODE END
				position = this._laneTarget[(int)((UIntPtr)laneID)];
			}
			PathUnit[] expr_D99_cp_0 = this._pathUnits.m_buffer;
			UIntPtr expr_D99_cp_1 = (UIntPtr)unit;
			expr_D99_cp_0[(int)expr_D99_cp_1].m_pathFindFlags = (byte)(expr_D99_cp_0[(int)expr_D99_cp_1].m_pathFindFlags | 8);
		}
#endregion

		// be aware:
		//   (1) path-finding works from target to start. the "next" segment is always the previous and the "previous" segment is always the next segment on the path!
		//   (2) when I use the term "lane index from right" this holds for right-hand traffic systems. On maps where you activate left-hand traffic, the "lane index from right" values represent lane indices starting from the left side.

		// 1
		private void ProcessItemMain(BufferItem item, ushort nextNodeId, ref NetNode nextNode, byte connectOffset, bool isMiddle) {
			try {
#if DEBUGPF
				//bool debug = nodeID == 28311u && item.m_position.m_segment == 33016;
				//bool debug = nodeID == 26128u && item.m_position.m_segment == 4139 && nextSegmentId == 27106;
				//bool debug = nodeID == 13630u && item.m_position.m_segment == 35546u;
				bool debug = nextNodeId == 1093;
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
				NetManager instance = Singleton<NetManager>.instance;
				bool isPedestrianLane = false;
				bool isBicycleLane = false;
				int similarLaneIndexFromLeft = 0; // similar index, starting with 0 at leftmost lane
				NetInfo prevSegmentInfo = instance.m_segments.m_buffer[(int)item.m_position.m_segment].Info;
				if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
					NetInfo.Lane prevLane = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
					isPedestrianLane = (prevLane.m_laneType == NetInfo.LaneType.Pedestrian);
					isBicycleLane = (prevLane.m_laneType == NetInfo.LaneType.Vehicle && prevLane.m_vehicleType == VehicleInfo.VehicleType.Bicycle);
					if ((byte)(prevLane.m_finalDirection & NetInfo.Direction.Forward) != 0) {
						similarLaneIndexFromLeft = prevLane.m_similarLaneIndex;
					} else {
						similarLaneIndexFromLeft = prevLane.m_similarLaneCount - prevLane.m_similarLaneIndex - 1;
					}
				}

				ushort prevSegmentId = item.m_position.m_segment;
				if (isMiddle) {
					for (int i = 0; i < 8; i++) {
						ushort nextSegmentId = nextNode.GetSegment(i);
						if (nextSegmentId <= 0)
							continue;
						this.ProcessItemSub(debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, !isPedestrianLane, isPedestrianLane);
					}
				} else if (isPedestrianLane) {
					int prevLaneIndex = (int)item.m_position.m_lane;
					if (nextNode.Info.m_class.m_service != ItemClass.Service.Beautification) {
						bool flag3 = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None;
						int laneIndex;
						int laneIndex2;
						uint leftLaneId;
						uint rightLaneId;
						instance.m_segments.m_buffer[(int)prevSegmentId].GetLeftAndRightLanes(nextNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, prevLaneIndex, out laneIndex, out laneIndex2, out leftLaneId, out rightLaneId);
						ushort num4 = prevSegmentId;
						ushort num5 = prevSegmentId;
						if (leftLaneId == 0u || rightLaneId == 0u) {
							ushort leftSegment;
							ushort rightSegment;
							instance.m_segments.m_buffer[(int)prevSegmentId].GetLeftAndRightSegments(nextNodeId, out leftSegment, out rightSegment);
							int num6 = 0;
							while (leftSegment != 0 && leftSegment != prevSegmentId && leftLaneId == 0u) {
								int num7;
								int num8;
								uint num9;
								uint num10;
								instance.m_segments.m_buffer[(int)leftSegment].GetLeftAndRightLanes(nextNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, out num7, out num8, out num9, out num10);
								if (num10 != 0u) {
									num4 = leftSegment;
									laneIndex = num8;
									leftLaneId = num10;
								} else {
									leftSegment = instance.m_segments.m_buffer[(int)leftSegment].GetLeftSegment(nextNodeId);
								}
								if (++num6 == 8) {
									break;
								}
							}
							num6 = 0;
							while (rightSegment != 0 && rightSegment != prevSegmentId && rightLaneId == 0u) {
								int num11;
								int num12;
								uint num13;
								uint num14;
								instance.m_segments.m_buffer[(int)rightSegment].GetLeftAndRightLanes(nextNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, out num11, out num12, out num13, out num14);
								if (num13 != 0u) {
									num5 = rightSegment;
									laneIndex2 = num11;
									rightLaneId = num13;
								} else {
									rightSegment = instance.m_segments.m_buffer[(int)rightSegment].GetRightSegment(nextNodeId);
								}
								if (++num6 == 8) {
									break;
								}
							}
						}
						if (leftLaneId != 0u && (num4 != prevSegmentId || flag3)) {
							this.ProcessItemPedBicycle(item, nextNodeId, num4, ref instance.m_segments.m_buffer[(int)num4], connectOffset, laneIndex, leftLaneId); // ped
						}
						if (rightLaneId != 0u && rightLaneId != leftLaneId && (num5 != prevSegmentId || flag3)) {
							this.ProcessItemPedBicycle(item, nextNodeId, num5, ref instance.m_segments.m_buffer[(int)num5], connectOffset, laneIndex2, rightLaneId); // ped
						}
						int laneIndex3;
						uint lane3;
						if ((this._vehicleTypes & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None && instance.m_segments.m_buffer[(int)prevSegmentId].GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Bicycle, out laneIndex3, out lane3)) {
							this.ProcessItemPedBicycle(item, nextNodeId, prevSegmentId, ref instance.m_segments.m_buffer[(int)prevSegmentId], connectOffset, laneIndex3, lane3); // bicycle
						}
					} else {
						for (int j = 0; j < 8; j++) {
							ushort segment3 = nextNode.GetSegment(j);
							if (segment3 != 0 && segment3 != prevSegmentId) {
								this.ProcessItemSub(debug, item, nextNodeId, segment3, ref instance.m_segments.m_buffer[(int)segment3], ref similarLaneIndexFromLeft, connectOffset, false, true);
							}
						}
					}
					NetInfo.LaneType laneType = this._laneTypes & ~NetInfo.LaneType.Pedestrian;
					VehicleInfo.VehicleType vehicleType = this._vehicleTypes & ~VehicleInfo.VehicleType.Bicycle;
					if ((byte)(item.m_lanesUsed & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
						laneType &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
					}
					int num15;
					uint lane4;
					if (laneType != NetInfo.LaneType.None && vehicleType != VehicleInfo.VehicleType.None && instance.m_segments.m_buffer[(int)prevSegmentId].GetClosestLane(prevLaneIndex, laneType, vehicleType, out num15, out lane4)) {
						NetInfo.Lane lane5 = prevSegmentInfo.m_lanes[num15];
						byte connectOffset2;
						if ((instance.m_segments.m_buffer[(int)prevSegmentId].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None == ((byte)(lane5.m_finalDirection & NetInfo.Direction.Backward) != 0)) {
							connectOffset2 = 1;
						} else {
							connectOffset2 = 254;
						}
						this.ProcessItemPedBicycle(item, nextNodeId, prevSegmentId, ref instance.m_segments.m_buffer[(int)prevSegmentId], connectOffset2, num15, lane4); // ped
					}
				} else {
					bool blocked = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
					bool pedestrianAllowed = (byte)(this._laneTypes & NetInfo.LaneType.Pedestrian) != 0;
					bool enablePedestrian = false;
					byte connectOffset3 = 0;
					if (pedestrianAllowed) {
						if (isBicycleLane) {
							connectOffset3 = connectOffset;
							enablePedestrian = (nextNode.Info.m_class.m_service == ItemClass.Service.Beautification);
						} else if (this._vehicleLane != 0u) {
							if (this._vehicleLane != item.m_laneID) {
								pedestrianAllowed = false;
							} else {
								connectOffset3 = this._vehicleOffset;
							}
						} else if (this._stablePath) {
							connectOffset3 = 128;
						} else {
							connectOffset3 = (byte)this._pathRandomizer.UInt32(1u, 254u);
						}
					}

					// NON-STOCK CODE START //
#if DEBUGPF
					if (debug)
						logBuf.Add($"Exploring path! Segment {item.m_position.m_segment} @ node {nextNodeId}: Preparation started");
#endif

					CustomPathManager pathManager = Singleton<CustomPathManager>.instance;
					bool nextIsJunction = (nextNode.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
					bool nextIsRealJunction = nextNode.CountSegments() > 2;
					bool nextIsTransition = (nextNode.m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;
					bool prevIsOutgoingOneWay = TrafficLightsManual.SegmentIsOutgoingOneWay(prevSegmentId, nextNodeId);
					bool prevIsHighway = false;
					if (prevSegmentInfo.m_netAI is RoadBaseAI)
						prevIsHighway = ((RoadBaseAI)prevSegmentInfo.m_netAI).m_highwayRules;

					NetInfo.Direction normDirection = TrafficPriority.LeftHandDrive ? NetInfo.Direction.Forward : NetInfo.Direction.Backward; // direction to normalize indices to
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
					bool isStrictLaneArrowPolicyEnabled = (nextIsJunction || nextIsTransition) && !(Options.allRelaxed || (Options.relaxedBusses && _transportVehicle)) && (this._vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None;
					
					// TODO geometries must always be validated!!!
					SegmentGeometry geometry = CustomRoadAI.GetSegmentGeometry(prevSegmentId, nextNodeId);
					//if (this._pathFindIndex == 0) {
					// verify the segment geometry
					for (int k = 0; k < 8; k++) {
						ushort nextSegId = instance.m_nodes.m_buffer[nextNodeId].GetSegment(k);
						if (nextSegId == 0 || nextSegId == prevSegmentId) {
							continue;
						}

						if (geometry.VerifySegments(nextSegId))
							break;
					}

					HashSet<ushort> incomingStraightSegments = null;
					HashSet<ushort> incomingRightSegments = null;
					HashSet<ushort> incomingLeftSegments = null;
					if (isStrictLaneArrowPolicyEnabled) {
						if (instance.m_segments.m_buffer[(int)prevSegmentId].m_startNode == nextNodeId) {
							incomingStraightSegments = geometry.StartNodeIncomingStraightSegments;
							incomingLeftSegments = geometry.StartNodeIncomingLeftSegments;
							incomingRightSegments = geometry.StartNodeIncomingRightSegments;
						} else {
							incomingStraightSegments = geometry.EndNodeIncomingStraightSegments;
							incomingLeftSegments = geometry.EndNodeIncomingLeftSegments;
							incomingRightSegments = geometry.EndNodeIncomingRightSegments;
						}
					}

					ushort nextSegmentId = instance.m_segments.m_buffer[prevSegmentId].GetRightSegment(nextNodeId);
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

					for (int k = 0; k < 8; k++) {
						if (nextSegmentId == 0 || nextSegmentId == item.m_position.m_segment) {
							break;
						}

						/*if (Options.nodesOverlay) {
							bool isRealRight = TrafficPriority.IsRightSegment(prevSegment, nextSegmentId, nextNodeId);
							bool isRealLeft = TrafficPriority.IsLeftSegment(prevSegment, nextSegmentId, nextNodeId);
							bool isRealStraight = !isRealRight && !isRealLeft;
							if (rightRemaining > 0) {
								if (!isRealRight)
									Log.Warning($"k={k}: segment {nextSegmentId} ({prevSegment}) is not right. rightRemaining={rightRemaining}. hasStraight={hasStraight}, hasLeft={hasLeft}. realRSL: {isRealLeft},{isRealStraight},{isRealRight}");
							} else if (rightRemaining < 0) {
								if (!isRealLeft)
									Log.Warning($"k={k}: segment {nextSegmentId} ({prevSegment}) is not left. rightRemaining={rightRemaining}. hasStraight={hasStraight}, hasLeft={hasLeft}. realRSL: {isRealLeft},{isRealStraight},{isRealRight}");
							} else if (!hasLeft) {
								if (!isRealStraight)
									Log.Warning($"k={k}: segment {nextSegmentId} ({prevSegment}) is not straight. rightRemaining={rightRemaining}. hasStraight={hasStraight}, hasLeft={hasLeft}. realRSL: {isRealLeft},{isRealStraight},{isRealRight}");
							}
						}*/

						// NON-STOCK CODE START //
						bool nextIsHighway = false;
						if (instance.m_segments.m_buffer[nextSegmentId].Info.m_netAI is RoadBaseAI)
							nextIsHighway = ((RoadBaseAI)instance.m_segments.m_buffer[nextSegmentId].Info.m_netAI).m_highwayRules;
						bool applyHighwayRules = Options.highwayRules && prevIsOutgoingOneWay && prevIsHighway && nextIsHighway && nextIsRealJunction;
						bool applyHighwayRulesAtSegment = applyHighwayRules;
						int outgoingVehicleLanes = 0;
						int incomingVehicleLanes = 0;
						bool couldFindCustomPath = false;
						if (!isStrictLaneArrowPolicyEnabled) {
							// NON-STOCK CODE END //
							if (ProcessItemSub(debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian)) {
								blocked = true;
							}
							// NON-STOCK CODE START //
							couldFindCustomPath = true; // not of interest
						} else if (!enablePedestrian) {
							try {
								var nextSegment = instance.m_segments.m_buffer[nextSegmentId];
								var nextSegmentInfo = nextSegment.Info;
								bool isIncomingRight = incomingRightSegments.Contains(nextSegmentId);
								bool isIncomingStraight = incomingStraightSegments.Contains(nextSegmentId);
								bool isIncomingLeft = incomingLeftSegments.Contains(nextSegmentId);

								// we need outgoing lanes too!
								if (!isIncomingLeft && !isIncomingRight && !isIncomingStraight) {
#if DEBUGPF
									if (debug)
										logBuf.Add($"(PFWARN) Segment {nextSegmentId} is neither left, right or straight segment @ {nextNodeId}, going to segment {prevSegmentId}");
#endif
									if (!applyHighwayRulesAtSegment) {
										couldFindCustomPath = true; // not of interest
										goto nextIter;
									} else {
										// we do not stop here because we need the number of outgoing lanes in highway mode
									}
								}

								VehicleInfo.VehicleType vehicleType2 = this._vehicleTypes;
								NetInfo.LaneType drivingEnabledLaneTypes = this._laneTypes;
								drivingEnabledLaneTypes &= ~NetInfo.LaneType.Pedestrian;
								drivingEnabledLaneTypes &= ~NetInfo.LaneType.Parking;

								//if (debug) {
								//Log.Message($"Path finding ({this._pathFindIndex}): Segment {nextSegmentId}");
								//}

								NetInfo.Direction nextDir = nextSegment.m_startNode != nextNodeId ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
								NetInfo.Direction nextDir2 = ((nextSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? nextDir : NetInfo.InvertDirection(nextDir);

								// valid next lanes:
								int[] laneIndexes = new int[16]; // index of NetNode.Info.m_lanes
								uint[] laneIds = new uint[16]; // index of NetManager.m_lanes.m_buffer
								uint[] indexByRightSimilarLaneIndex = new uint[16];
								uint[] indexByLeftSimilarLaneIndex = new uint[16];

								uint curLaneI = 0;
								uint curLaneId = nextSegment.m_lanes;
								int i = 0;
#if DEBUG
								uint wIter = 0;
#endif
								while (i < nextSegmentInfo.m_lanes.Length && curLaneId != 0u) {
#if DEBUG
									++wIter;
									if (wIter >= 20) {
										Log.Error("Too many iterations in ProcessItemMain!");
										break;
									}
#endif

									// determine valid lanes based on lane arrows
									NetInfo.Lane nextLane = nextSegmentInfo.m_lanes[i];
									bool incomingLane = (byte)(nextLane.m_finalDirection & nextDir2) != 0;
									bool compatibleLane = nextLane.CheckType(drivingEnabledLaneTypes, _vehicleTypes);

									if (incomingLane && compatibleLane) {
										++incomingVehicleLanes;
#if DEBUGPF
										if (debug)
											logBuf.Add($"Segment {nextSegmentId}, lane {curLaneId}, {i} is compatible (prevSegment: {prevSegmentId}). laneTypes: {_laneTypes.ToString()}, vehicleTypes: {_vehicleTypes.ToString()}, incomingLanes={incomingVehicleLanes}, isIncomingRight? {isIncomingRight}, isIncomingLeft? {isIncomingLeft}, isIncomingStraight? {isIncomingStraight}");
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
												logBuf.Add($"Segment {nextSegmentId}, lane {curLaneId}, {i} has LEFT arrow. isIncomingRight? {isIncomingRight}, isIncomingLeft? {isIncomingLeft}, isIncomingStraight? {isIncomingStraight}");
											}

											if (hasRightArrow) {
												logBuf.Add($"Segment {nextSegmentId}, lane {curLaneId}, {i} has RIGHT arrow. isIncomingRight? {isIncomingRight}, isIncomingLeft? {isIncomingLeft}, isIncomingStraight? {isIncomingStraight}");
											}

											if (hasForwardArrow) {
												logBuf.Add($"Segment {nextSegmentId}, lane {curLaneId}, {i} has FORWARD arrow. isIncomingRight? {isIncomingRight}, isIncomingLeft? {isIncomingLeft}, isIncomingStraight? {isIncomingStraight}");
											}
										}
#endif

										bool isValidIncomingRight = isIncomingRight && hasLeftArrow;
										bool isValidIncomingLeft = isIncomingLeft && hasRightArrow;
										bool isValidIncomingStraight = isIncomingStraight && hasForwardArrow;

										// add valid next lanes
										if (applyHighwayRulesAtSegment || isValidIncomingRight || isValidIncomingLeft || isValidIncomingStraight) {
											laneIndexes[curLaneI] = i;
											laneIds[curLaneI] = curLaneId;
											indexByRightSimilarLaneIndex[nextRightSimilarLaneIndex] = curLaneI + 1;
											indexByLeftSimilarLaneIndex[nextLeftSimilarLaneIndex] = curLaneI + 1;
#if DEBUGPF
											if (debug)
												logBuf.Add($"Adding lane #{curLaneI} (id {curLaneId}, idx {i}), right sim. idx: {nextRightSimilarLaneIndex}, left sim. idx.: {nextLeftSimilarLaneIndex}");
#endif
											curLaneI++;
										}
									}

									if (!incomingLane && compatibleLane) {
										// outgoing lane
										++outgoingVehicleLanes;
									}

									curLaneId = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_nextLane;
									i++;
								} // foreach lane

								if (curLaneI > 0) {
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

									if (curLaneI > 0) {
										if (applyHighwayRulesAtSegment) {
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
												// too few lanes at prevSegment or nextSegment: sort left or right
												if (isIncomingLeft) {
													nextLaneI = Convert.ToInt32(indexByRightSimilarLaneIndex[prevRightSimilarLaneIndex]) - 1;
												} else if (totalIncomingLanes > 0) {
													// allow n-to-1 merging only if we need to
													nextLaneI = Convert.ToInt32(indexByLeftSimilarLaneIndex[prevLeftSimilarLaneIndex]) - 1;
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
												goto nextIter; // no path to this lane
											}
										} else if (curLaneI == 1) {
											nextLaneI = 0;
#if DEBUGPF
											if (debug)
												logBuf.Add($"Single target lane found. nextLaneI={nextLaneI}");
#endif
										} else {
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
#if DEBUGPF
												if (debug)
													logBuf.Add($"Next is junction. minNextRightSimilarIndex={minNextRightSimilarIndex} = maxNextRightSimilarIndex={maxNextRightSimilarIndex}");
#endif
											} else {
												// lane merging/splitting
												HandleLaneMergesAndSplits(prevRightSimilarLaneIndex, nextCompatibleLaneCount, prevSimilarLaneCount, out minNextRightSimilarIndex, out maxNextRightSimilarIndex);
#if DEBUGPF
												if (debug)
													logBuf.Add($"Next is not a junction. nextRightSimilarLaneIndex=HandleLaneMergesAndSplits({prevRightSimilarLaneIndex}, {nextCompatibleLaneCount}, {prevSimilarLaneCount})= min. {minNextRightSimilarIndex} max. {maxNextRightSimilarIndex}");
#endif
											}

											// find best matching lane(s)
											for (int nextRightSimilarIndex = minNextRightSimilarIndex; nextRightSimilarIndex <= maxNextRightSimilarIndex; ++nextRightSimilarIndex) {
												nextLaneI = FindNthCompatibleLane(ref indexByRightSimilarLaneIndex, nextRightSimilarIndex);
												if (nextLaneI < 0) {
													continue;
												}

												// go to matched lane
												nextLaneIndex = laneIndexes[nextLaneI];
												nextLaneId = laneIds[nextLaneI];

#if DEBUGPF
												if (debug)
													logBuf.Add($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane idx {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right. There are {curLaneI} candidate lanes. We choose lane {nextLaneI} (index {nextLaneIndex}, {nextRightSimilarIndex} compatible from right). lhd: {TrafficPriority.LeftHandDrive}, ped: {pedestrianAllowed}, magical flag4: {blocked}");
#endif

												if (ProcessItemSub(debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian, nextLaneIndex, nextLaneId, out foundForced))
													blocked = true;
												couldFindCustomPath = true;
											}
											goto nextIter;
										}
									} else {
#if DEBUGPF
										if (debug)
											Log.Error($"(PFERR) Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: No compatible lanes found");
#endif
										couldFindCustomPath = true; // not of interest for us
										goto nextIter;
									}

									if (nextLaneI < 0) {
#if DEBUGPF
										if (debug)
											Log.Error($"(PFERR) Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: nextLaneI < 0!");
#endif
										goto nextIter;
									}

									// go to matched lane
									nextLaneIndex = laneIndexes[nextLaneI];
									nextLaneId = laneIds[nextLaneI];

									if (/*this._pathFindIndex == 0 && */applyHighwayRulesAtSegment) {
										// udpate highway mode arrows
										Flags.LaneArrows? highwayArrows = Flags.getHighwayLaneArrowFlags(nextLaneId);
										if (highwayArrows == null)
											highwayArrows = Flags.LaneArrows.None;
										if (isIncomingRight)
											highwayArrows |= Flags.LaneArrows.Left;
										if (isIncomingLeft)
											highwayArrows |= Flags.LaneArrows.Right;
										if (isIncomingStraight)
											highwayArrows |= Flags.LaneArrows.Forward;
										Flags.setHighwayLaneArrowFlags(nextLaneId, (Flags.LaneArrows)highwayArrows);
									}

									if (ProcessItemSub(debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian, nextLaneIndex, nextLaneId, out foundForced))
										blocked = true;

									if (foundForced) {
#if DEBUGPF
										if (debug)
											logBuf.Add($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: FORCED LANE FOUND!");
#endif
										couldFindCustomPath = true;
									}
								} else {
									// no compatible lanes found
#if DEBUGPF
									if (debug)
										Log.Error($"(PFERR) Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: No lane arrows defined");
#endif
									couldFindCustomPath = true; // the player did not set lane arrows. this is ok...
																/*if (ProcessItem(debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian)) {
																	blocked = true;
																}*/
								}
							} catch (Exception e) {
								Log.Error($"(PFERR) Error occurred in custom path-finding (main): {e.ToString()}");

								couldFindCustomPath = true; // not of interest for us
															// stock code fallback
								if (this.ProcessItemSub(debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian)) {
									blocked = true;
								}
							}
							// NON-STOCK CODE END
						} else {
							// pedestrians

							// stock code:
							if (this.ProcessItemSub(debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian)) {
								blocked = true;
							}
							couldFindCustomPath = true; // not of interest for us
							goto nextIter;
						}

						nextIter:

						if (!couldFindCustomPath) {
#if DEBUGPF
							if (debug)
								Log.Warning($"(PFERR) Could not find custom path from segment {nextSegmentId} to segment {prevSegmentId} at node {nextNodeId}!");
#endif
							// stock code:
							/*if (this.ProcessItem(debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian)) {
								blocked = true;
							}*/
						}

						nextSegmentId = instance.m_segments.m_buffer[(int)nextSegmentId].GetRightSegment(nextNodeId);
						totalIncomingLanes += incomingVehicleLanes;
						totalOutgoingLanes += outgoingVehicleLanes;
					} // foreach segment
#if DEBUGPF
					if (debug)
						logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: Custom part finished");
#endif
					if (blocked) {
						// turn-around for vehicles
#if DEBUGPF
						if (debug)
							logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: Road may be blocked");
#endif
						// vehicles may turn around if the street is blocked
						nextSegmentId = item.m_position.m_segment;
						this.ProcessItemSub(debug, item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, false);
					}
					// NON-STOCK CODE START
					/*if (foundForced)
						return;*/
					// NON-STOCK CODE END
					if (pedestrianAllowed) {
						// turn-around for pedestrians
#if DEBUGPF
						if (debug)
							logBuf.Add($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: Ped allowed");
#endif
						nextSegmentId = item.m_position.m_segment;
						int laneIndex4;
						uint lane6;
						if (instance.m_segments.m_buffer[(int)nextSegmentId].GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Pedestrian, this._vehicleTypes, out laneIndex4, out lane6)) {
							this.ProcessItemPedBicycle(item, nextNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], connectOffset3, laneIndex4, lane6); // ped
						}
					}
				}
				if (nextNode.m_lane != 0u) {
					bool targetDisabled = (nextNode.m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None;
					ushort segment4 = instance.m_lanes.m_buffer[(int)((UIntPtr)nextNode.m_lane)].m_segment;
					if (segment4 != 0 && segment4 != item.m_position.m_segment) {
#if DEBUGPF
						if (debug)
							logBuf.Add($"Exploring path from {segment4} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}: handling special lanes");
#endif
						this.ProcessItem2(item, nextNodeId, targetDisabled, segment4, ref instance.m_segments.m_buffer[(int)segment4], nextNode.m_lane, nextNode.m_laneOffset, connectOffset);
					}
				}

#if DEBUGPF
			if (debug) {
				foreach (String toLog in logBuf) {
					Log.Message($"Pathfinder ({this._pathFindIndex}): " + toLog);
				}
			}
#endif
			} catch (Exception ex) {
				Log.Error($"Error occurred in path-finding (main): {ex.ToString()}");
				throw ex;
			}
		}

		private static int FindNthCompatibleLane(ref uint[] indexBySimilarLaneIndex, int prevRightSimilarLaneIndex) {
			int nextLaneI = -1;
			for (int j = 0; j < 16; ++j) {
				if (indexBySimilarLaneIndex[j] == 0)
					continue;
				nextLaneI = Convert.ToInt32(indexBySimilarLaneIndex[j]) - 1;
				if (prevRightSimilarLaneIndex <= 0) { // matching lane found
					if (prevRightSimilarLaneIndex < 0) {
						Log.Error($"FindNthCompatibleLane: prevRightSimilarIndex < 0!");
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
			float num3 = 1f;
			float num4 = 1f;
			NetInfo.LaneType laneType = NetInfo.LaneType.None;
			// NON-STOCK CODE START //
			NetNode targetNode = instance.m_nodes.m_buffer[targetNodeId];
			bool nextIsJunction = targetNode.CountSegments() > 2;
			ushort sourceNodeId = (targetNodeId == instance.m_segments.m_buffer[item.m_position.m_segment].m_startNode) ? instance.m_segments.m_buffer[item.m_position.m_segment].m_endNode : instance.m_segments.m_buffer[item.m_position.m_segment].m_startNode; // no lane changing directly in front of a junction
			NetNode sourceNode = instance.m_nodes.m_buffer[sourceNodeId];
			bool prevIsJunction = sourceNode.CountSegments() > 2;
			// NON-STOCK CODE END //
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane lane2 = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				num3 = lane2.m_speedLimit;
				laneType = lane2.m_laneType;
				if ((byte)(laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
					laneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
				num4 = this.CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[(int)item.m_position.m_segment], lane2);
			}
			float averageLength = instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_averageLength;
			float num5 = (float)Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * 0.003921569f * averageLength;
			float num6 = item.m_methodDistance + num5;
			float num7 = item.m_comparisonValue + num5 / (num4 * this._maxLength);
			Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)item.m_laneID)].CalculatePosition((float)connectOffset * 0.003921569f);
			int laneIndex = 0;
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
						BufferItem item2;
						// NON-STOCK CODE START //
						if (prevIsJunction)
							item2.m_numSegmentsToJunction = 1;
						else if (nextIsJunction)
							item2.m_numSegmentsToJunction = 0;
						else
							item2.m_numSegmentsToJunction = item.m_numSegmentsToJunction + 1;
						// NON-STOCK CODE END //
						item2.m_position.m_segment = nextSegmentId;
						item2.m_position.m_lane = (byte)laneIndex;
						item2.m_position.m_offset = offset;
						if ((byte)(lane3.m_laneType & laneType) == 0) {
							item2.m_methodDistance = 0f;
						} else {
							item2.m_methodDistance = num6 + num9;
						}
						if (lane3.m_laneType != NetInfo.LaneType.Pedestrian || item2.m_methodDistance < 1000f) {
							item2.m_comparisonValue = num7 + num9 / ((num3 + lane3.m_speedLimit) * 0.5f * this._maxLength);
							if ((nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
								item2.m_direction = NetInfo.InvertDirection(lane3.m_finalDirection);
							} else {
								item2.m_direction = lane3.m_finalDirection;
							}
							if (lane == this._startLaneA) {
								if (((byte)(item2.m_direction & NetInfo.Direction.Forward) == 0 || item2.m_position.m_offset < this._startOffsetA) && ((byte)(item2.m_direction & NetInfo.Direction.Backward) == 0 || item2.m_position.m_offset > this._startOffsetA)) {
									return;
								}
								float num10 = this.CalculateLaneSpeed(this._startOffsetA, item2.m_position.m_offset, ref nextSegment, lane3);
								float num11 = (float)Mathf.Abs((int)(item2.m_position.m_offset - this._startOffsetA)) * 0.003921569f;
								item2.m_comparisonValue += num11 * nextSegment.m_averageLength / (num10 * this._maxLength);
							}
							if (lane == this._startLaneB) {
								if (((byte)(item2.m_direction & NetInfo.Direction.Forward) == 0 || item2.m_position.m_offset < this._startOffsetB) && ((byte)(item2.m_direction & NetInfo.Direction.Backward) == 0 || item2.m_position.m_offset > this._startOffsetB)) {
									return;
								}
								float num12 = this.CalculateLaneSpeed(this._startOffsetB, item2.m_position.m_offset, ref nextSegment, lane3);
								float num13 = (float)Mathf.Abs((int)(item2.m_position.m_offset - this._startOffsetB)) * 0.003921569f;
								item2.m_comparisonValue += num13 * nextSegment.m_averageLength / (num12 * this._maxLength);
							}
							item2.m_laneID = lane;
							item2.m_lanesUsed = (item.m_lanesUsed | lane3.m_laneType);
							this.AddBufferItem(item2, item.m_position);
						}
					}
					return;
				}
				curLaneId = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_nextLane;
				laneIndex++;
			}
		}
#endregion

		private bool ProcessItemSub(bool debug, BufferItem item, ushort targetNode, ushort segmentID, ref NetSegment segment, ref int laneIndexFromLeft, byte connectOffset, bool enableVehicle, bool enablePedestrian) {
			bool foundForced = false;
			return ProcessItemSub(debug, item, targetNode, segmentID, ref segment, ref laneIndexFromLeft, connectOffset, enableVehicle, enablePedestrian, null, null, out foundForced);
		}

		/*private bool ProcessItemy(BufferItem item, ushort targetNode, ushort segmentID, ref NetSegment segment, ref int currentTargetIndex, byte connectOffset, bool enableVehicle, bool enablePedestrian) {
			bool result = false;
			if ((segment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None) {
				return result;
			}
			NetManager instance = Singleton<NetManager>.instance;
			NetInfo info = segment.Info;
			NetInfo info2 = instance.m_segments.m_buffer[(int)item.m_position.m_segment].Info;
			int num = info.m_lanes.Length;
			uint num2 = segment.m_lanes;
			NetInfo.Direction direction = (targetNode != segment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
			NetInfo.Direction direction2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? direction : NetInfo.InvertDirection(direction);
			float num3 = 0.01f - Mathf.Min(info.m_maxTurnAngleCos, info2.m_maxTurnAngleCos);
			if (num3 < 1f) {
				Vector3 vector;
				if (targetNode == instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_startNode) {
					vector = instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_startDirection;
				} else {
					vector = instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_endDirection;
				}
				Vector3 vector2;
				if ((byte)(direction & NetInfo.Direction.Forward) != 0) {
					vector2 = segment.m_endDirection;
				} else {
					vector2 = segment.m_startDirection;
				}
				float num4 = vector.x * vector2.x + vector.z * vector2.z;
				if (num4 >= num3) {
					return result;
				}
			}
			float num5 = 1f;
			float num6 = 1f;
			NetInfo.LaneType laneType = NetInfo.LaneType.None;
			VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None;
			if ((int)item.m_position.m_lane < info2.m_lanes.Length) {
				NetInfo.Lane lane = info2.m_lanes[(int)item.m_position.m_lane];
				laneType = lane.m_laneType;
				vehicleType = lane.m_vehicleType;
				num5 = lane.m_speedLimit;
				num6 = this.CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[(int)item.m_position.m_segment], lane);
			}
			float num7 = instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_averageLength;
			if (!this._stablePath) {
				Randomizer randomizer = new Randomizer(this._pathFindIndex << 16 | (uint)item.m_position.m_segment);
				num7 *= (float)(randomizer.Int32(900, 1000 + (int)(instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_trafficDensity * 10)) + this._pathRandomizer.Int32(20u)) * 0.001f;
			}
			if (this._isHeavyVehicle && (instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_flags & NetSegment.Flags.HeavyBan) != NetSegment.Flags.None) {
				num7 *= 10f;
			} else if (laneType == NetInfo.LaneType.Vehicle && vehicleType == VehicleInfo.VehicleType.Car && (instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_flags & NetSegment.Flags.CarBan) != NetSegment.Flags.None) {
				num7 *= 5f;
			}
			if (this._transportVehicle && laneType == NetInfo.LaneType.TransportVehicle) {
				num7 *= 0.95f;
			}
			if ((byte)(laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
				laneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
			}
			float num8 = (float)Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * 0.003921569f * num7;
			float num9 = item.m_methodDistance + num8;
			float num10 = item.m_comparisonValue + num8 / (num6 * this._maxLength);
			Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)item.m_laneID)].CalculatePosition((float)connectOffset * 0.003921569f);
			int num11 = currentTargetIndex;
			bool flag = (instance.m_nodes.m_buffer[(int)targetNode].m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;
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
			int num12 = 0;
			while (num12 < num && num2 != 0u) {
				NetInfo.Lane lane2 = info.m_lanes[num12];
				if ((byte)(lane2.m_finalDirection & direction2) != 0) {
					if (lane2.CheckType(laneType2, vehicleType2) && (segmentID != item.m_position.m_segment || num12 != (int)item.m_position.m_lane) && (byte)(lane2.m_finalDirection & direction2) != 0) {
						Vector3 a;
						if ((byte)(direction & NetInfo.Direction.Forward) != 0) {
							a = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_bezier.d;
						} else {
							a = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_bezier.a;
						}
						float num13 = Vector3.Distance(a, b);
						if (flag) {
							num13 *= 2f;
						}
						float num14 = num13 / ((num5 + lane2.m_speedLimit) * 0.5f * this._maxLength);
						BufferItem item2 = default(BufferItem);
						item2.m_position.m_segment = segmentID;
						item2.m_position.m_lane = (byte)num12;
						item2.m_position.m_offset = (byte)(((direction & NetInfo.Direction.Forward) == 0) ? 0 : 255);
						if ((byte)(lane2.m_laneType & laneType) == 0) {
							item2.m_methodDistance = 0f;
						} else {
							item2.m_methodDistance = num9 + num13;
						}
						if (lane2.m_laneType != NetInfo.LaneType.Pedestrian || item2.m_methodDistance < 1000f) {
							item2.m_comparisonValue = num10 + num14;
							item2.m_direction = direction;
							if (num2 == this._startLaneA) {
								if (((byte)(item2.m_direction & NetInfo.Direction.Forward) == 0 || item2.m_position.m_offset < this._startOffsetA) && ((byte)(item2.m_direction & NetInfo.Direction.Backward) == 0 || item2.m_position.m_offset > this._startOffsetA)) {
									num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
									goto IL_90F;
								}
								float num15 = this.CalculateLaneSpeed(this._startOffsetA, item2.m_position.m_offset, ref segment, lane2);
								float num16 = (float)Mathf.Abs((int)(item2.m_position.m_offset - this._startOffsetA)) * 0.003921569f;
								item2.m_comparisonValue += num16 * segment.m_averageLength / (num15 * this._maxLength);
							}
							if (num2 == this._startLaneB) {
								if (((byte)(item2.m_direction & NetInfo.Direction.Forward) == 0 || item2.m_position.m_offset < this._startOffsetB) && ((byte)(item2.m_direction & NetInfo.Direction.Backward) == 0 || item2.m_position.m_offset > this._startOffsetB)) {
									num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
									goto IL_90F;
								}
								float num17 = this.CalculateLaneSpeed(this._startOffsetB, item2.m_position.m_offset, ref segment, lane2);
								float num18 = (float)Mathf.Abs((int)(item2.m_position.m_offset - this._startOffsetB)) * 0.003921569f;
								item2.m_comparisonValue += num18 * segment.m_averageLength / (num17 * this._maxLength);
							}
							if (!this._ignoreBlocked && (segment.m_flags & NetSegment.Flags.Blocked) != NetSegment.Flags.None && (byte)(lane2.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
								item2.m_comparisonValue += 0.1f;
								result = true;
							}
							item2.m_lanesUsed = (item.m_lanesUsed | lane2.m_laneType);
							item2.m_laneID = num2;
							if ((byte)(lane2.m_laneType & laneType) != 0 && lane2.m_vehicleType == vehicleType) {
								int firstTarget = (int)instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_firstTarget;
								int lastTarget = (int)instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_lastTarget;
								if (currentTargetIndex < firstTarget || currentTargetIndex >= lastTarget) {
									item2.m_comparisonValue += Mathf.Max(1f, num13 * 3f - 3f) / ((num5 + lane2.m_speedLimit) * 0.5f * this._maxLength);
								}
								if (!this._transportVehicle && lane2.m_laneType == NetInfo.LaneType.TransportVehicle) {
									item2.m_comparisonValue += 20f / ((num5 + lane2.m_speedLimit) * 0.5f * this._maxLength);
								}
							}
							this.AddBufferItem(item2, item.m_position);
						}
					}
					goto IL_8F5;
				}
				if ((byte)(lane2.m_laneType & laneType) != 0 && lane2.m_vehicleType == vehicleType) {
					num11++;
					goto IL_8F5;
				}
				goto IL_8F5;
				IL_90F:
				num12++;
				continue;
				IL_8F5:
				num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
				goto IL_90F;
			}
			currentTargetIndex = num11;
			return result;
		}*/

		// 3
		private bool ProcessItemSub(bool debug, BufferItem item, ushort targetNodeId, ushort segmentID, ref NetSegment nextSegment, ref int laneIndexFromLeft, byte connectOffset, bool enableVehicle, bool enablePedestrian, int? forceLaneIndex, uint? forceLaneId, out bool foundForced) {
			try {
				//debug = targetNodeId == 6900;
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
				float prevMaxSpeed = 1f;
				float prevLaneSpeed = 1f;
				NetInfo.LaneType laneType = NetInfo.LaneType.None;
				VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None;
				// NON-STOCK CODE START //
				bool prevIsHighway = false;
				if (prevSegmentInfo.m_netAI is RoadBaseAI)
					prevIsHighway = ((RoadBaseAI)prevSegmentInfo.m_netAI).m_highwayRules;
				bool prevIsOutgoingOneWay = TrafficLightsManual.SegmentIsOutgoingOneWay(item.m_position.m_segment, targetNodeId);
				float nextSpeed = 0f;
				int prevRightSimilarLaneIndex = -1;
				NetInfo.Direction normDirection = TrafficPriority.LeftHandDrive ? NetInfo.Direction.Forward : NetInfo.Direction.Backward; // direction to normalize indices to
				int prevNumLanes = 1;
				float prevSpeed = 0f;
				// NON-STOCK CODE END //
				if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
					NetInfo.Lane lane = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
					laneType = lane.m_laneType;
					vehicleType = lane.m_vehicleType;
					prevMaxSpeed = lane.m_speedLimit;
					prevLaneSpeed = this.CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[(int)item.m_position.m_segment], lane);
					// NON-STOCK CODE START //
					prevNumLanes = lane.m_similarLaneCount;
					if ((byte)(lane.m_direction & normDirection) != 0) {
						prevRightSimilarLaneIndex = lane.m_similarLaneIndex;
					} else {
						prevRightSimilarLaneIndex = lane.m_similarLaneCount - lane.m_similarLaneIndex - 1;
					}
					prevSpeed = CustomRoadAI.laneMeanSpeeds[item.m_laneID];
					// NON-STOCK CODE END //
				}
				bool customLaneChanging = vehicleType == VehicleInfo.VehicleType.Car && !Options.isStockLaneChangerUsed() && !this._transportVehicle && !this._stablePath; // NON-STOCK CODE
				float cost = instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_averageLength;
				if (!customLaneChanging && !this._stablePath) { // NON-STOCK CODE
					Randomizer randomizer = new Randomizer(this._pathFindIndex << 16 | (uint)item.m_position.m_segment);
					cost *= (float)(randomizer.Int32(900, 1000 + (int)(instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_trafficDensity * 10)) + this._pathRandomizer.Int32(20u)) * 0.001f;
				}
				if (this._isHeavyVehicle && (instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_flags & NetSegment.Flags.HeavyBan) != NetSegment.Flags.None) {
					cost *= 10f;
				} else if (laneType == NetInfo.LaneType.Vehicle && vehicleType == VehicleInfo.VehicleType.Car && (instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_flags & NetSegment.Flags.CarBan) != NetSegment.Flags.None) {
					cost *= 5f;
				}
				if (this._transportVehicle && laneType == NetInfo.LaneType.TransportVehicle) {
					cost *= 0.95f;
				}
				if ((byte)(laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
					laneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
				float prevOffset = (float)Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * 0.003921569f * cost;
				float prevMethodDist = item.m_methodDistance + prevOffset;
				float prevComparisonPlusOffsetOverSpeed = item.m_comparisonValue + prevOffset / (prevLaneSpeed * this._maxLength);
				// NON-STOCK CODE START //
				Vector3 meanPrevLanePosition = new Vector3(0, 0, 0);
				if (customLaneChanging) {
					int lIndex = 0;
					uint laneId = instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_lanes;
					int validLanes = 0;
#if DEBUG
					uint wIter1 = 0;
#endif
					while (lIndex < prevSegmentInfo.m_lanes.Length && laneId != 0u) {
						//Log.Message($"PF {this._pathFindIndex} -- CustomPathfind (1): Iter #{lIndex}, laneId={laneId}, prevSegment={item.m_position.m_segment}, targetNodeId={targetNodeId}");
#if DEBUG
						++wIter1;
						if (wIter1 >= 20) {
							Log.Error("Too many iterations in ProcessItemSub (1)!");
							break;
						}
#endif

						NetInfo.Lane prevLane = prevSegmentInfo.m_lanes[lIndex];
						meanPrevLanePosition += instance.m_lanes.m_buffer[laneId].CalculatePosition((float)connectOffset * 0.003921569f);
						++validLanes;

						++lIndex;
						laneId = instance.m_lanes.m_buffer[laneId].m_nextLane;
					}
					if (validLanes > 0)
						meanPrevLanePosition /= Convert.ToSingle(validLanes);
				}
				// NON-STOCK CODE END //
				Vector3 prevLanePosition = instance.m_lanes.m_buffer[(int)((UIntPtr)item.m_laneID)].CalculatePosition((float)connectOffset * 0.003921569f);
				int num11 = laneIndexFromLeft;
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
																																																																	   //NetNode sourceNode = instance.m_nodes.m_buffer[sourceNodeId];
				bool prevIsRealJunction = instance.m_nodes.m_buffer[sourceNodeId].CountSegments() > 2;
				//int laneTargetValue = Options.getLaneChangingRandomizationTargetValue();
				/*if (this._isHeavyVehicle)
					laneTargetValue *= 2;
				bool changeLane =
					!prevIsJunction &&
					!this._stablePath &&
					forceLaneIndex == null &&
					customLaneChanging &&
					_pathRandomizer.Int32(1, Options.getLaneChangingRandomizationTargetValue()) == 1; // lane randomization*/
				bool nextIsHighway = false;
				if (nextSegmentInfo.m_netAI is RoadBaseAI)
					nextIsHighway = ((RoadBaseAI)nextSegmentInfo.m_netAI).m_highwayRules;
				int laneIndex = (int)(forceLaneIndex != null ? forceLaneIndex : 0);
				uint curLaneId = (uint)(forceLaneId != null ? forceLaneId : nextSegment.m_lanes); // NON-STOCK CODE

				// NON-STOCK CODE END //
#if DEBUG
				uint wIter3 = 0;
#endif
				while (laneIndex < nextNumLanes && curLaneId != 0u) {
#if DEBUG
					++wIter3;
					if (wIter3 >= 20) {
						Log.Error("Too many iterations in ProcessItemSub (3)!");
						break;
					}
#endif

					//Log.Message($"PF {this._pathFindIndex} -- CustomPathfind (3): Iter #{laneIndex}, curLaneId={curLaneId}, prevSegment={item.m_position.m_segment}, targetNodeId={targetNodeId}");

					// NON-STOCK CODE START //
					if (forceLaneIndex != null && laneIndex != forceLaneIndex)
						break;
					if (customLaneChanging) {
						nextSpeed = CustomRoadAI.laneMeanSpeeds[curLaneId];
					}
					// NON-STOCK CODE END //

					NetInfo.Lane nextLane = nextSegmentInfo.m_lanes[laneIndex];
					float nextMaxSpeed = nextLane.m_speedLimit; // NON-STOCK CODE
					if ((byte)(nextLane.m_finalDirection & nextDir2) != 0) {
						if (nextLane.CheckType(laneType2, vehicleType2) && (segmentID != item.m_position.m_segment || laneIndex != (int)item.m_position.m_lane) && (byte)(nextLane.m_finalDirection & nextDir2) != 0) {
							float distanceOnBezier = 0f;
							bool addCustomCosts = customLaneChanging && forceLaneIndex == null && curLaneId != this._startLaneA && curLaneId != this._startLaneB && curLaneId != this._endLaneA && curLaneId != this._endLaneB && (byte)(nextLane.m_laneType & laneType) != 0 && nextLane.m_vehicleType == vehicleType;
							
							Vector3 a;
							Vector3 b;

							// NON-STOCK CODE START //
							if (customLaneChanging) {
								a = instance.m_nodes.m_buffer[targetNodeId].m_position;
								b = meanPrevLanePosition;
							} else {
								// NON-STOCK CODE END //
								if ((byte)(nextDir & NetInfo.Direction.Forward) != 0) {
									a = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_bezier.d;
								} else {
									a = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_bezier.a;
								}
								b = prevLanePosition;
								// NON-STOCK CODE START //
							}
							// NON-STOCK CODE END //

							distanceOnBezier = Vector3.Distance(a, b);
							/*if (targetNodeId == 17415) {*/
							//Log.Message($">> PF {this._pathFindIndex} -- Calculated distance: " + distanceOnBezier + " Original distance: " + Vector3.Distance(a1, b1) + " New distance: " + Vector3.Distance(a2, b2) + " next segment: " + segmentID + " from segment: " + item.m_position.m_segment + " a1: " + a1.ToString() + " b1: " + b1.ToString() + " a2: " + a2.ToString() + " b2: " + b2.ToString());
							/*}*/

							if (transitionNode) {
								distanceOnBezier *= 2f;
							}

							if (customLaneChanging && distanceOnBezier <= 0f) {
								distanceOnBezier = 1f;
							}

						   /*if (customLaneChanging && nextIsRealJunction) {
							   distanceOnBezier *= 1.1f;
						   }*/

						   float distanceOverMeanMaxSpeed = distanceOnBezier / ((prevMaxSpeed + nextLane.m_speedLimit) * 0.5f * this._maxLength);
							BufferItem item2;
							// NON-STOCK CODE START //
							if (nextIsRealJunction)
								item2.m_numSegmentsToJunction = 0;
							else if (prevIsRealJunction)
								item2.m_numSegmentsToJunction = 1;
							else
								item2.m_numSegmentsToJunction = item.m_numSegmentsToJunction + 1;
							// NON-STOCK CODE END //
							item2.m_position.m_segment = segmentID;
							item2.m_position.m_lane = (byte)laneIndex;
							item2.m_position.m_offset = (byte)(((nextDir & NetInfo.Direction.Forward) == 0) ? 0 : 255);
							if ((byte)(nextLane.m_laneType & laneType) == 0) {
								item2.m_methodDistance = 0f;
							} else {
								item2.m_methodDistance = prevMethodDist + distanceOnBezier;
							}
							if (nextLane.m_laneType != NetInfo.LaneType.Pedestrian || item2.m_methodDistance < 1000f) {
								item2.m_comparisonValue = prevComparisonPlusOffsetOverSpeed;
								// NON-STOCK CODE START //
								if (!addCustomCosts)
									item2.m_comparisonValue += distanceOverMeanMaxSpeed;
								// NON-STOCK CODE END //
								item2.m_direction = nextDir;
								if (curLaneId == this._startLaneA) {
									if (((byte)(item2.m_direction & NetInfo.Direction.Forward) == 0 || item2.m_position.m_offset < this._startOffsetA) && ((byte)(item2.m_direction & NetInfo.Direction.Backward) == 0 || item2.m_position.m_offset > this._startOffsetA)) {
										curLaneId = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_nextLane;
										goto IL_90F;
									}
									float num15 = this.CalculateLaneSpeed(this._startOffsetA, item2.m_position.m_offset, ref nextSegment, nextLane);
									float num16 = (float)Mathf.Abs((int)(item2.m_position.m_offset - this._startOffsetA)) * 0.003921569f;
									item2.m_comparisonValue += num16 * nextSegment.m_averageLength / (num15 * this._maxLength);
								}
								if (curLaneId == this._startLaneB) {
									if (((byte)(item2.m_direction & NetInfo.Direction.Forward) == 0 || item2.m_position.m_offset < this._startOffsetB) && ((byte)(item2.m_direction & NetInfo.Direction.Backward) == 0 || item2.m_position.m_offset > this._startOffsetB)) {
										curLaneId = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_nextLane;
										goto IL_90F;
									}
									float num17 = this.CalculateLaneSpeed(this._startOffsetB, item2.m_position.m_offset, ref nextSegment, nextLane);
									float num18 = (float)Mathf.Abs((int)(item2.m_position.m_offset - this._startOffsetB)) * 0.003921569f;
									item2.m_comparisonValue += num18 * nextSegment.m_averageLength / (num17 * this._maxLength);
								}
								if (!this._ignoreBlocked && (nextSegment.m_flags & NetSegment.Flags.Blocked) != NetSegment.Flags.None && (byte)(nextLane.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
									item2.m_comparisonValue += 0.1f;
									result = true;
								}
								item2.m_lanesUsed = (item.m_lanesUsed | nextLane.m_laneType);
								item2.m_laneID = curLaneId;
								if ((byte)(nextLane.m_laneType & laneType) != 0 && nextLane.m_vehicleType == vehicleType) {
									// NON-STOCK CODE START //
									if (!customLaneChanging) {
										// NON-STOCK CODE END //
										int firstTarget = (int)instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_firstTarget;
										int lastTarget = (int)instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_lastTarget;
										if (laneIndexFromLeft < firstTarget || laneIndexFromLeft >= lastTarget) {
											item2.m_comparisonValue += Mathf.Max(1f, distanceOnBezier * 3f - 3f) / ((prevMaxSpeed + nextLane.m_speedLimit) * 0.5f * this._maxLength);
										}
										// NON-STOCK CODE START //
									}
									// NON-STOCK CODE END //

									if (!this._transportVehicle && nextLane.m_laneType == NetInfo.LaneType.TransportVehicle) {
										item2.m_comparisonValue += 20f / ((prevMaxSpeed + nextLane.m_speedLimit) * 0.5f * this._maxLength);
									}
								}

								// NON-STOCK CODE START //
								bool addItem = true;
								try {
									if (addCustomCosts) {
										int nextRightSimilarLaneIndex;
										if ((byte)(nextLane.m_direction & normDirection) != 0) {
											nextRightSimilarLaneIndex = nextLane.m_similarLaneIndex;
										} else {
											nextRightSimilarLaneIndex = nextLane.m_similarLaneCount - nextLane.m_similarLaneIndex - 1;
										}

										float laneDist = Convert.ToSingle(Math.Abs(nextRightSimilarLaneIndex - prevRightSimilarLaneIndex));
										if (laneDist <= 1) {

#if DEBUGCOSTS
											bool costDebug = segmentID == 27082 || segmentID == 13245;
											List<String> logBuf = null;
											if (costDebug)
												logBuf = new List<String>();
#endif
											float meanSpeed;

											if (laneDist > 1) {
												meanSpeed = 0.001f;
											} else {
												// vehicles should choose lanes with low traffic volume, but should neither change lanes too frequently nor change to distant lanes.
												float maxLaneDist = Convert.ToSingle(Math.Max(prevNumLanes - 1, nextLane.m_similarLaneCount - 1));
												float prevDens = prevSpeed < 0 ? prevMaxSpeed : Math.Min(1f, prevSpeed / prevMaxSpeed); // prev. lane density, discretized in 20 steps
												float nextDens = nextSpeed < 0 ? nextMaxSpeed : Math.Min(1f, nextSpeed / nextMaxSpeed); // prev. lane density, discretized in 20 steps
												meanSpeed = (prevDens + nextDens) * 0.5f;
												meanSpeed /= (laneDist * laneDist + 1f);

												if (meanSpeed < 0.01f)
													meanSpeed = 0.01f;
											}

											distanceOverMeanMaxSpeed = distanceOnBezier / (meanSpeed * this._maxLength);

#if DEBUGCOSTS
											if (costDebug) {
												logBuf.Add($"Path from {segmentID} (lane {nextRightSimilarLaneIndex} from right, idx {laneIndex}) to {item.m_position.m_segment} (lane {prevRightSimilarLaneIndex} from right, idx {item.m_position.m_lane}.");
												logBuf.Add($"prevDens = {prevDens}, nextDens = {nextDens}, distanceOverMeanMaxSpeed = {distanceOverMeanMaxSpeed}, meanDens = {meanDens}");
											}
#endif

											item2.m_comparisonValue += distanceOverMeanMaxSpeed;
#if DEBUGCOSTS
											if (costDebug) {
												logBuf.Add($"Total cost = {distanceOverMeanMaxSpeed}, comparison value = {item2.m_comparisonValue}");
											}
#endif
#if DEBUGCOSTS
											if (costDebug) {
												foreach (String toLog in logBuf) {
													Log.Message($"Pathfinder ({this._pathFindIndex}): " + toLog);
												}
											}
#endif

											if (item2.m_comparisonValue < 0f) {
#if DEBUG
												Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId}, PF {this._pathFindIndex}: Comparison value < 0! distanceOverMeanMaxSpeed={distanceOverMeanMaxSpeed}, nextSpeed={nextSpeed}, prevSpeed={prevSpeed}");
#endif
												item2.m_comparisonValue = 0f;
											} else if (item2.m_comparisonValue > 1f) {
#if DEBUG
												Log.Error($"THREAD #{Thread.CurrentThread.ManagedThreadId}, PF {this._pathFindIndex}: Comparison value > 1! distanceOverMeanMaxSpeed={distanceOverMeanMaxSpeed}, nextSpeed={nextSpeed}, prevSpeed={prevSpeed}");
#endif
												item2.m_comparisonValue = 1f;
												addItem = false;
											}
											if (debug) {
												//Log.Message($">> PF {this._pathFindIndex} -- seg {item2.m_position.m_segment}, lane {item2.m_position.m_lane} (idx {item2.m_laneID}), off {item2.m_position.m_offset}, cost {item2.m_comparisonValue}, totalCost {totalCost} = traffic={trafficCost}, junction={junctionCost}, lane={laneChangeCost}");
											}
										} else {
											addItem = false;
										}
									}

									if (forceLaneIndex != null && laneIndex == forceLaneIndex) {
										foundForced = true;
									}

									/*if (addCustomCosts && ((item.m_position.m_segment == 2062 && item2.m_position.m_segment == 23640) || (item.m_position.m_segment == 23640 && item2.m_position.m_segment == 29505) || (item.m_position.m_segment == 29505 && item2.m_position.m_segment == 24975) || (item.m_position.m_segment == 24975 && item2.m_position.m_segment == 9535) || (item.m_position.m_segment == 9535 && item2.m_position.m_segment == 20914) || (item.m_position.m_segment == 20914 && item2.m_position.m_segment == 21545))) {
										Log.Message($">> PF {this._pathFindIndex} -- Adding item: seg {item2.m_position.m_segment}, lane {item2.m_position.m_lane} (idx {item2.m_laneID}), off {item2.m_position.m_offset} -> seg {item.m_position.m_segment}, lane {item.m_position.m_lane} (idx {item.m_laneID}), off {item.m_position.m_offset}, cost {item2.m_comparisonValue}, methodDist {item2.m_methodDistance}");
									}*/
								} catch (Exception e) {
									Log.Error($"Error occurred in custom path-finding (sub): {e.ToString()}");
									throw e;
								}
								// NON-STOCK CODE END //

								if (addItem)
									this.AddBufferItem(item2, item.m_position);
							}
						}
						goto IL_8F5;
					}
					if ((byte)(nextLane.m_laneType & laneType) != 0 && nextLane.m_vehicleType == vehicleType) {
						num11++;
						goto IL_8F5;
					}
					goto IL_8F5;
					IL_90F:
					laneIndex++;
					continue;
					IL_8F5:
					curLaneId = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_nextLane;
					goto IL_90F;
				} // foreach lane
				laneIndexFromLeft = num11;
				return result;
			} catch (Exception ex) {
				Log.Error($"Error occurred in path-finding (sub): {ex.ToString()}");
				throw ex;
			}
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
			float num2;
			byte offset;
			if (segmentID == item.m_position.m_segment) {
				Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)item.m_laneID)].CalculatePosition((float)connectOffset * 0.003921569f);
				Vector3 a = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].CalculatePosition((float)connectOffset * 0.003921569f);
				num2 = Vector3.Distance(a, b);
				offset = connectOffset;
			} else {
				NetInfo.Direction direction = (targetNodeId != segment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
				Vector3 b2 = instance.m_lanes.m_buffer[(int)((UIntPtr)item.m_laneID)].CalculatePosition((float)connectOffset * 0.003921569f);
				Vector3 a2;
				if ((byte)(direction & NetInfo.Direction.Forward) != 0) {
					a2 = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].m_bezier.d;
				} else {
					a2 = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].m_bezier.a;
				}
				num2 = Vector3.Distance(a2, b2);
				offset = (byte)(((direction & NetInfo.Direction.Forward) == 0) ? 0 : 255);
			}
			float num3 = 1f;
			float num4 = 1f;
			NetInfo.LaneType laneType = NetInfo.LaneType.None;
			if ((int)item.m_position.m_lane < info2.m_lanes.Length) {
				NetInfo.Lane lane2 = info2.m_lanes[(int)item.m_position.m_lane];
				num3 = lane2.m_speedLimit;
				laneType = lane2.m_laneType;
				if ((byte)(laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
					laneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
				num4 = this.CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[(int)item.m_position.m_segment], lane2);
			}
			float averageLength = instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_averageLength;
			float num5 = (float)Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * 0.003921569f * averageLength;
			float num6 = item.m_methodDistance + num5;
			float num7 = item.m_comparisonValue + num5 / (num4 * this._maxLength);
			// NON-STOCK CODE START //
			NetNode targetNode = instance.m_nodes.m_buffer[targetNodeId];
			bool nextIsJunction = targetNode.CountSegments() > 2;
			ushort sourceNodeId = (targetNodeId == instance.m_segments.m_buffer[item.m_position.m_segment].m_startNode) ? instance.m_segments.m_buffer[item.m_position.m_segment].m_endNode : instance.m_segments.m_buffer[item.m_position.m_segment].m_startNode; // no lane changing directly in front of a junction
			NetNode sourceNode = instance.m_nodes.m_buffer[sourceNodeId];
			bool prevIsJunction = sourceNode.CountSegments() > 2;
			// NON-STOCK CODE END //
			if (laneIndex < num) {
				NetInfo.Lane lane3 = info.m_lanes[laneIndex];
				BufferItem item2;
				// NON-STOCK CODE START //
				if (prevIsJunction)
					item2.m_numSegmentsToJunction = 1;
				else if (nextIsJunction)
					item2.m_numSegmentsToJunction = 0;
				else
					item2.m_numSegmentsToJunction = item.m_numSegmentsToJunction + 1;
				// NON-STOCK CODE END //
				item2.m_position.m_segment = segmentID;
				item2.m_position.m_lane = (byte)laneIndex;
				item2.m_position.m_offset = offset;
				if ((byte)(lane3.m_laneType & laneType) == 0) {
					item2.m_methodDistance = 0f;
				} else {
					if (item.m_methodDistance == 0f) {
						num7 += 100f / (0.25f * this._maxLength);
					}
					item2.m_methodDistance = num6 + num2;
				}
				if (lane3.m_laneType != NetInfo.LaneType.Pedestrian || item2.m_methodDistance < 1000f) {
					item2.m_comparisonValue = num7 + num2 / ((num3 + lane3.m_speedLimit) * 0.25f * this._maxLength);
					if ((segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
						item2.m_direction = NetInfo.InvertDirection(lane3.m_finalDirection);
					} else {
						item2.m_direction = lane3.m_finalDirection;
					}
					if (lane == this._startLaneA) {
						if (((byte)(item2.m_direction & NetInfo.Direction.Forward) == 0 || item2.m_position.m_offset < this._startOffsetA) && ((byte)(item2.m_direction & NetInfo.Direction.Backward) == 0 || item2.m_position.m_offset > this._startOffsetA)) {
							return;
						}
						float num8 = this.CalculateLaneSpeed(this._startOffsetA, item2.m_position.m_offset, ref segment, lane3);
						float num9 = (float)Mathf.Abs((int)(item2.m_position.m_offset - this._startOffsetA)) * 0.003921569f;
						item2.m_comparisonValue += num9 * segment.m_averageLength / (num8 * this._maxLength);
					}
					if (lane == this._startLaneB) {
						if (((byte)(item2.m_direction & NetInfo.Direction.Forward) == 0 || item2.m_position.m_offset < this._startOffsetB) && ((byte)(item2.m_direction & NetInfo.Direction.Backward) == 0 || item2.m_position.m_offset > this._startOffsetB)) {
							return;
						}
						float num10 = this.CalculateLaneSpeed(this._startOffsetB, item2.m_position.m_offset, ref segment, lane3);
						float num11 = (float)Mathf.Abs((int)(item2.m_position.m_offset - this._startOffsetB)) * 0.003921569f;
						item2.m_comparisonValue += num11 * segment.m_averageLength / (num10 * this._maxLength);
					}
					item2.m_laneID = lane;
					item2.m_lanesUsed = (item.m_lanesUsed | lane3.m_laneType);
					this.AddBufferItem(item2, item.m_position);
				}
			}
		}
#endregion

		protected virtual NetLane.Flags GetLaneFlags(ushort segmentId, ushort nodeId) {
			NetManager instance = NetManager.instance;
			NetSegment seg = instance.m_segments.m_buffer[segmentId];
			NetLane.Flags flags = NetLane.Flags.None;
			NetInfo.Direction dir = NetInfo.Direction.Forward;
			if (seg.m_startNode == nodeId)
				dir = NetInfo.Direction.Backward;
			ulong currentLane = seg.m_lanes;
			for (int i = 0; i < seg.Info.m_lanes.Length; i++) {
				if (((seg.Info.m_lanes[i].m_direction & dir) == dir) && seg.Info.m_lanes[i].m_laneType == NetInfo.LaneType.Vehicle)
					flags |= (NetLane.Flags)instance.m_lanes.m_buffer[currentLane].m_flags;
				currentLane = instance.m_lanes.m_buffer[currentLane].m_nextLane;
			}
			return flags;
		}

		private float CalculateLaneSpeed(byte startOffset, byte endOffset, ref NetSegment segment, NetInfo.Lane laneInfo) {
			NetInfo.Direction direction = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? laneInfo.m_finalDirection : NetInfo.InvertDirection(laneInfo.m_finalDirection);
			if ((byte)(direction & NetInfo.Direction.Avoid) == 0) {
				return laneInfo.m_speedLimit;
			}
			if (endOffset > startOffset && direction == NetInfo.Direction.AvoidForward) {
				return laneInfo.m_speedLimit * 0.1f;
			}
			if (endOffset < startOffset && direction == NetInfo.Direction.AvoidBackward) {
				return laneInfo.m_speedLimit * 0.1f;
			}
			return laneInfo.m_speedLimit * 0.2f;
		}

		private void AddBufferItem(BufferItem item, PathUnit.Position target) {
			uint num = _laneLocation[(int)((UIntPtr)item.m_laneID)];
			uint num2 = num >> 16;
			int num3 = (int)(num & 65535u);
			int comparisonBufferPos;
			if (num2 == _pathFindIndex) {
				if (item.m_comparisonValue >= _buffer[num3].m_comparisonValue) {
					return;
				}
				int num4 = num3 >> 6;
				int num5 = num3 & -64;
				if (num4 < _bufferMinPos || (num4 == _bufferMinPos && num5 < _bufferMin[num4])) {
					return;
				}
				comparisonBufferPos = Mathf.Max(Mathf.RoundToInt(item.m_comparisonValue * 1024f), _bufferMinPos);
				if (comparisonBufferPos == num4) {
					_buffer[num3] = item;
					_laneTarget[(int)((UIntPtr)item.m_laneID)] = target;
					return;
				}
				int num7 = num4 << 6 | _bufferMax[num4]--;
				BufferItem bufferItem = _buffer[num7];
				_laneLocation[(int)((UIntPtr)bufferItem.m_laneID)] = num;
				_buffer[num3] = bufferItem;
			} else {
				comparisonBufferPos = Mathf.Max(Mathf.RoundToInt(item.m_comparisonValue * 1024f), _bufferMinPos);
			}
			if (comparisonBufferPos >= 1024) {
				return;
			}
			if (comparisonBufferPos < 0) {
				Log.Error("comparisonBufferPos < 1024!");
				return;
			}
			while (_bufferMax[comparisonBufferPos] == 63) {
				comparisonBufferPos++;
				if (comparisonBufferPos == 1024) {
					return;
				}
				// NON-STOCK CODE START //
				if (comparisonBufferPos > 1024) {
					Log.Error("num6 > 1024!");
					return;
				}
				// NON-STOCK CODE END //
			}
			if (comparisonBufferPos > _bufferMaxPos) {
				_bufferMaxPos = comparisonBufferPos;
			}
			num3 = (comparisonBufferPos << 6 | ++_bufferMax[comparisonBufferPos]);
			_buffer[num3] = item;
			_laneLocation[(int)((UIntPtr)item.m_laneID)] = (_pathFindIndex << 16 | (uint)num3);
			_laneTarget[(int)((UIntPtr)item.m_laneID)] = target;
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
#if DEBUGLOCKS
				uint lockIter = 0;
#endif
				try {
					while (!Monitor.TryEnter(QueueLock, SYNC_TIMEOUT)) {
#if DEBUGLOCKS
						++lockIter;
						if (lockIter % 100 == 0)
							Log.Message("CustomPathFind.PathFindThread (1) lockIter: " + lockIter);
#endif
					}
#if DEBUGLOCKS
					uint waitIter = 0;
#endif
					while (QueueFirst == 0u && !Terminated) {
						while (!Monitor.Wait(QueueLock, SYNC_TIMEOUT)) {
#if DEBUGLOCKS
							++waitIter;
							if (waitIter % 100 == 0)
								Log.Message("CustomPathFind.PathFindThread waitIter: " + waitIter);
#endif
						}
					}
					if (Terminated) {
						break;
					}
					Calculating = QueueFirst;
					QueueFirst = _pathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_nextPathUnit;
					if (QueueFirst == 0u) {
						QueueLast = 0u;
						m_queuedPathFindCount = 0;
					} else {
						m_queuedPathFindCount--;
					}
					if (m_queuedPathFindCount >= 1000 && m_queuedPathFindCount % 100 == 0)
						Log.Message($"THREAD #{Thread.CurrentThread.ManagedThreadId} PathFindThread: Starting pathfinder. Remaining queued pathfinders: {m_queuedPathFindCount}");
					_pathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_nextPathUnit = 0u;
					_pathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags = (byte)((_pathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags & -2) | 2);
				} catch (Exception e) {
					Log.Error("CustomPathFind.PathFindThread Error (1): " + e.ToString());
				} finally {
					Monitor.Exit(QueueLock);
				}
				try {
					m_pathfindProfiler.BeginStep();
					try {
						PathFindImplementation(Calculating, ref _pathUnits.m_buffer[(int)((UIntPtr)Calculating)]);
					} catch (Exception e) {
						Log.Error("CustomPathFind.PathFindThread Error (2): " + e.ToString());
					} finally {
						m_pathfindProfiler.EndStep();
//#if DEBUGPF
						//Log.Message($"THREAD #{Thread.CurrentThread.ManagedThreadId} last step duration: {m_pathfindProfiler.m_lastStepDuration} average step duration: {m_pathfindProfiler.m_averageStepDuration} peak step duration: {m_pathfindProfiler.m_peakStepDuration}");
//#endif
                    }
				} catch (Exception ex) {
					Log.Error("path thread error: " + ex.Message);
					UIView.ForwardException(ex);
					Log.Error("Path find error: " + ex.Message + "\n" + ex.StackTrace);
					var expr_1A0Cp0 = _pathUnits.m_buffer;
					var expr_1A0Cp1 = (UIntPtr)Calculating;
					expr_1A0Cp0[(int)expr_1A0Cp1].m_pathFindFlags = (byte)(expr_1A0Cp0[(int)expr_1A0Cp1].m_pathFindFlags | 8);
				}
#if DEBUGLOCKS
				lockIter = 0;
#endif

				try {
					while (!Monitor.TryEnter(QueueLock, SYNC_TIMEOUT)) {
#if DEBUGLOCKS
						++lockIter;
						if (lockIter % 100 == 0)
							Log.Message("CustomPathFind.PathFindThread (2) lockIter: " + lockIter);
#endif
					}

					_pathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags = (byte)(_pathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags & -3);
					Singleton<PathManager>.instance.ReleasePath(Calculating);
					Calculating = 0u;
					Monitor.Pulse(QueueLock);
				} catch (Exception e) {
					Log.Error("CustomPathFind.PathFindThread Error (3): " + e.ToString());
				} finally {
					Monitor.Exit(QueueLock);
				}
			}
		}
	}
}
