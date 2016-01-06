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

namespace TrafficManager.Custom.Misc
{
    public class CustomPathFind : PathFind
    {
        private struct BufferItem
        {
            public PathUnit.Position m_position;
            public float m_comparisonValue;
            public float m_methodDistance;
            public uint m_laneID;
            public NetInfo.Direction m_direction;
            public NetInfo.LaneType m_lanesUsed;
			public uint m_numSegmentsToJunction;
        }

        public CustomPathFind()
        {

        }

        //Expose the private fields
        FieldInfo _fieldpathUnits;
        FieldInfo _fieldQueueFirst;
        FieldInfo _fieldQueueLast;
        FieldInfo _fieldQueueLock;
        FieldInfo _fieldCalculating;
        FieldInfo _fieldTerminated;
        FieldInfo _fieldPathFindThread;

        private Array32<PathUnit> _pathUnits
        {
            get { return _fieldpathUnits.GetValue(this) as Array32<PathUnit>; }
            set { _fieldpathUnits.SetValue(this, value); }
        }

        private uint QueueFirst
        {
            get { return (uint)_fieldQueueFirst.GetValue(this); }
            set { _fieldQueueFirst.SetValue(this, value); }
        }

        private uint QueueLast
        {
            get { return (uint)_fieldQueueLast.GetValue(this); }
            set { _fieldQueueLast.SetValue(this, value); }
        }

        private uint Calculating
        {
            get { return (uint)_fieldCalculating.GetValue(this); }
            set { _fieldCalculating.SetValue(this, value); }
        }

        private object QueueLock
        {
            get { return _fieldQueueLock.GetValue(this); }
            set { _fieldQueueLock.SetValue(this, value); }
        }

        private object _bufferLock;
        private Thread CustomPathFindThread
        {
            get { return (Thread)_fieldPathFindThread.GetValue(this); }
            set { _fieldPathFindThread.SetValue(this, value); }
        }

        private bool Terminated
        {
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


        protected virtual void Awake()
        {
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

            CustomPathFindThread = new Thread(PathFindThread) {Name = "Pathfind"};
            CustomPathFindThread.Start();
            if (!CustomPathFindThread.IsAlive)
            {
                //CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find thread failed to start!");
                Log.Error("Path find thread failed to start!");
            }

        }

#region stock code
		protected virtual void OnDestroy()
        {
            while (!Monitor.TryEnter(QueueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
            {
            }
            try
            {
                Terminated = true;
                Monitor.PulseAll(QueueLock);
            }
            finally
            {
                Monitor.Exit(QueueLock);
            }
        }

        
        public new bool CalculatePath(uint unit, bool skipQueue)
        {
            if (Singleton<PathManager>.instance.AddPathReference(unit))
            {
                while (!Monitor.TryEnter(QueueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    if (skipQueue)
                    {
                        if (QueueLast == 0u)
                        {
                            QueueLast = unit;
                        }
                        else
                        {
                            _pathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit = QueueFirst;
                        }
                        QueueFirst = unit;
                    }
                    else
                    {
                        if (QueueLast == 0u)
                        {
                            QueueFirst = unit;
                        }
                        else
                        {
                            _pathUnits.m_buffer[(int)((UIntPtr)QueueLast)].m_nextPathUnit = unit;
                        }
                        QueueLast = unit;
                    }
                    var exprBdCp0 = _pathUnits.m_buffer;
                    var exprBdCp1 = (UIntPtr)unit;
                    exprBdCp0[(int)exprBdCp1].m_pathFindFlags = (byte)(exprBdCp0[(int)exprBdCp1].m_pathFindFlags | 1);
                    m_queuedPathFindCount++;
                    Monitor.Pulse(QueueLock);
                }
                finally
                {
                    Monitor.Exit(QueueLock);
                }
                return true;
            }
            return false;
        }

        
        public new void WaitForAllPaths()
        {
            while (!Monitor.TryEnter(QueueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
            {
            }
            try
            {
                while ((QueueFirst != 0u || Calculating != 0u) && !Terminated)
                {
                    Monitor.Wait(QueueLock);
                }
            }
            finally
            {
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
			BufferItem bufferItem = default(BufferItem);
			byte b = 0;
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
			bool flag = false;
			while (this._bufferMinPos <= this._bufferMaxPos) {
				int num4 = this._bufferMin[this._bufferMinPos];
				int num5 = this._bufferMax[this._bufferMinPos];
				if (num4 > num5) {
					this._bufferMinPos++;
				} else {
					this._bufferMin[this._bufferMinPos] = num4 + 1;
					BufferItem bufferItem6 = this._buffer[(this._bufferMinPos << 6) + num4];
					if (bufferItem6.m_position.m_segment == bufferItemStartA.m_position.m_segment && bufferItem6.m_position.m_lane == bufferItemStartA.m_position.m_lane) {
						if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Forward) != 0 && bufferItem6.m_position.m_offset >= this._startOffsetA) {
							bufferItem = bufferItem6;
							b = this._startOffsetA;
							flag = true;
							break;
						}
						if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Backward) != 0 && bufferItem6.m_position.m_offset <= this._startOffsetA) {
							bufferItem = bufferItem6;
							b = this._startOffsetA;
							flag = true;
							break;
						}
					}
					if (bufferItem6.m_position.m_segment == bufferItemStartB.m_position.m_segment && bufferItem6.m_position.m_lane == bufferItemStartB.m_position.m_lane) {
						if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Forward) != 0 && bufferItem6.m_position.m_offset >= this._startOffsetB) {
							bufferItem = bufferItem6;
							b = this._startOffsetB;
							flag = true;
							break;
						}
						if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Backward) != 0 && bufferItem6.m_position.m_offset <= this._startOffsetB) {
							bufferItem = bufferItem6;
							b = this._startOffsetB;
							flag = true;
							break;
						}
					}
					if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Forward) != 0) {
						ushort startNode = instance.m_segments.m_buffer[(int)bufferItem6.m_position.m_segment].m_startNode;
						this.ProcessItemMain(bufferItem6, startNode, ref instance.m_nodes.m_buffer[(int)startNode], 0, false);
					}
					if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Backward) != 0) {
						ushort endNode = instance.m_segments.m_buffer[(int)bufferItem6.m_position.m_segment].m_endNode;
						this.ProcessItemMain(bufferItem6, endNode, ref instance.m_nodes.m_buffer[(int)endNode], 255, false);
					}
					int num6 = 0;
					ushort num7 = instance.m_lanes.m_buffer[(int)((UIntPtr)bufferItem6.m_laneID)].m_nodes;
					if (num7 != 0) {
						ushort startNode2 = instance.m_segments.m_buffer[(int)bufferItem6.m_position.m_segment].m_startNode;
						ushort endNode2 = instance.m_segments.m_buffer[(int)bufferItem6.m_position.m_segment].m_endNode;
						bool flag2 = ((instance.m_nodes.m_buffer[(int)startNode2].m_flags | instance.m_nodes.m_buffer[(int)endNode2].m_flags) & NetNode.Flags.Disabled) != NetNode.Flags.None;
						while (num7 != 0) {
							NetInfo.Direction direction = NetInfo.Direction.None;
							byte laneOffset = instance.m_nodes.m_buffer[(int)num7].m_laneOffset;
							if (laneOffset <= bufferItem6.m_position.m_offset) {
								direction |= NetInfo.Direction.Forward;
							}
							if (laneOffset >= bufferItem6.m_position.m_offset) {
								direction |= NetInfo.Direction.Backward;
							}
							if ((byte)(bufferItem6.m_direction & direction) != 0 && (!flag2 || (instance.m_nodes.m_buffer[(int)num7].m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None)) {
								this.ProcessItemMain(bufferItem6, num7, ref instance.m_nodes.m_buffer[(int)num7], laneOffset, true);
							}
							num7 = instance.m_nodes.m_buffer[(int)num7].m_nextLaneNode;
							if (++num6 == 32768) {
								break;
							}
						}
					}
				}
			}
			if (!flag) {
				PathUnit[] expr_909_cp_0 = this._pathUnits.m_buffer;
				UIntPtr expr_909_cp_1 = (UIntPtr)unit;
				expr_909_cp_0[(int)expr_909_cp_1].m_pathFindFlags = (byte) (expr_909_cp_0[(int)expr_909_cp_1].m_pathFindFlags | 8);
				return;
			}
			float num8 = bufferItem.m_comparisonValue * this._maxLength;
			this._pathUnits.m_buffer[(int)((UIntPtr)unit)].m_length = num8;
			uint num9 = unit;
			int num10 = 0;
			int num11 = 0;
			PathUnit.Position position = bufferItem.m_position;
			if ((position.m_segment != bufferItemEndA.m_position.m_segment || position.m_lane != bufferItemEndA.m_position.m_lane || position.m_offset != bufferItemEndA.m_position.m_offset) && (position.m_segment != bufferItemEndB.m_position.m_segment || position.m_lane != bufferItemEndB.m_position.m_lane || position.m_offset != bufferItemEndB.m_position.m_offset)) {
				if (b != position.m_offset) {
					PathUnit.Position position2 = position;
					position2.m_offset = b;
					this._pathUnits.m_buffer[(int)((UIntPtr)num9)].SetPosition(num10++, position2);
				}
				this._pathUnits.m_buffer[(int)((UIntPtr)num9)].SetPosition(num10++, position);
				position = this._laneTarget[(int)((UIntPtr)bufferItem.m_laneID)];
			}
			for (int k = 0; k < 262144; k++) {
				this._pathUnits.m_buffer[(int)((UIntPtr)num9)].SetPosition(num10++, position);
				if ((position.m_segment == bufferItemEndA.m_position.m_segment && position.m_lane == bufferItemEndA.m_position.m_lane && position.m_offset == bufferItemEndA.m_position.m_offset) || (position.m_segment == bufferItemEndB.m_position.m_segment && position.m_lane == bufferItemEndB.m_position.m_lane && position.m_offset == bufferItemEndB.m_position.m_offset)) {
					this._pathUnits.m_buffer[(int)((UIntPtr)num9)].m_positionCount = (byte)num10;
					num11 += num10;
					if (num11 != 0) {
						num9 = this._pathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit;
						num10 = (int)this._pathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount;
						int num12 = 0;
						while (num9 != 0u) {
							this._pathUnits.m_buffer[(int)((UIntPtr)num9)].m_length = num8 * (float)(num11 - num10) / (float)num11;
							num10 += (int)this._pathUnits.m_buffer[(int)((UIntPtr)num9)].m_positionCount;
							num9 = this._pathUnits.m_buffer[(int)((UIntPtr)num9)].m_nextPathUnit;
							if (++num12 >= 262144) {
								CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
								break;
							}
						}
					}
					PathUnit[] expr_C16_cp_0 = this._pathUnits.m_buffer;
					UIntPtr expr_C16_cp_1 = (UIntPtr)unit;
					expr_C16_cp_0[(int)expr_C16_cp_1].m_pathFindFlags = (byte) (expr_C16_cp_0[(int)expr_C16_cp_1].m_pathFindFlags | 4);
					return;
				}
				if (num10 == 12) {
					while (!Monitor.TryEnter(this._bufferLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) {
					}
					uint num13;
					try {
						if (!this._pathUnits.CreateItem(out num13, ref this._pathRandomizer)) {
							PathUnit[] expr_D15_cp_0 = this._pathUnits.m_buffer;
							UIntPtr expr_D15_cp_1 = (UIntPtr)unit;
							expr_D15_cp_0[(int)expr_D15_cp_1].m_pathFindFlags = (byte) (expr_D15_cp_0[(int)expr_D15_cp_1].m_pathFindFlags | 8);
							return;
						}
						this._pathUnits.m_buffer[(int)((UIntPtr)num13)] = this._pathUnits.m_buffer[(int)((UIntPtr)num9)];
						this._pathUnits.m_buffer[(int)((UIntPtr)num13)].m_referenceCount = 1;
						this._pathUnits.m_buffer[(int)((UIntPtr)num13)].m_pathFindFlags = 4;
						this._pathUnits.m_buffer[(int)((UIntPtr)num9)].m_nextPathUnit = num13;
						this._pathUnits.m_buffer[(int)((UIntPtr)num9)].m_positionCount = (byte)num10;
						num11 += num10;
						Singleton<PathManager>.instance.m_pathUnitCount = (int)(this._pathUnits.ItemCount() - 1u);
					} finally {
						Monitor.Exit(this._bufferLock);
					}
					num9 = num13;
					num10 = 0;
				}
				uint laneID = PathManager.GetLaneID(position);
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
		private void ProcessItemMain(BufferItem item, ushort targetNodeId, ref NetNode targetNode, byte connectOffset, bool isMiddle) {
			try {
#if DEBUG
				//bool debug = nodeID == 28311u && item.m_position.m_segment == 33016;
				//bool debug = nodeID == 26128u && item.m_position.m_segment == 4139 && nextSegmentId == 27106;
				//bool debug = nodeID == 13630u && item.m_position.m_segment == 35546u;
				//bool debug = nodeID == 13630 && item.m_position.m_segment == 31428u && item.m_position.m_lane == 5;
				bool debug = false;
#endif
				NetManager instance = Singleton<NetManager>.instance;
				bool isPedestrianLane = false;
				bool isBicycleLane = false;
				int similarLaneIndexFromLeft = 0; // similar index, starting with 0 at leftmost lane
				NetInfo prevSegmentInfo = instance.m_segments.m_buffer[(int)item.m_position.m_segment].Info;
				if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
					NetInfo.Lane lane = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
					isPedestrianLane = (lane.m_laneType == NetInfo.LaneType.Pedestrian);
					isBicycleLane = (lane.m_laneType == NetInfo.LaneType.Vehicle && lane.m_vehicleType == VehicleInfo.VehicleType.Bicycle);
					if ((byte)(lane.m_finalDirection & NetInfo.Direction.Forward) != 0) {
						similarLaneIndexFromLeft = lane.m_similarLaneIndex;
					} else {
						similarLaneIndexFromLeft = lane.m_similarLaneCount - lane.m_similarLaneIndex - 1;
					}
				}
				if (isMiddle) {
#if DEBUG
					if (debug) {
						Log.Message("Path finding: segment is middle");
					}
#endif

					for (int i = 0; i < 8; i++) {
						ushort segment = targetNode.GetSegment(i);
						if (segment <= 0)
							continue;
						this.ProcessItem(item, targetNodeId, segment, ref instance.m_segments.m_buffer[(int)segment], ref similarLaneIndexFromLeft, connectOffset, !isPedestrianLane, isPedestrianLane);
					}
				} else if (isPedestrianLane) {
					ushort segment2 = item.m_position.m_segment;
					int lane2 = (int)item.m_position.m_lane;
					if (targetNode.Info.m_class.m_service != ItemClass.Service.Beautification) {
						bool flag3 = (targetNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None;
						int laneIndex;
						int laneIndex2;
						uint num2;
						uint num3;
						instance.m_segments.m_buffer[(int)segment2].GetLeftAndRightLanes(targetNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, lane2, out laneIndex, out laneIndex2, out num2, out num3);
						ushort num4 = segment2;
						ushort num5 = segment2;
						if (num2 == 0u || num3 == 0u) {
							ushort leftSegment;
							ushort rightSegment;
							instance.m_segments.m_buffer[(int)segment2].GetLeftAndRightSegments(targetNodeId, out leftSegment, out rightSegment);
							int num6 = 0;
							while (leftSegment != 0 && leftSegment != segment2 && num2 == 0u) {
								int num7;
								int num8;
								uint num9;
								uint num10;
								instance.m_segments.m_buffer[(int)leftSegment].GetLeftAndRightLanes(targetNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, out num7, out num8, out num9, out num10);
								if (num10 != 0u) {
									num4 = leftSegment;
									laneIndex = num8;
									num2 = num10;
								} else {
									leftSegment = instance.m_segments.m_buffer[(int)leftSegment].GetLeftSegment(targetNodeId);
								}
								if (++num6 == 8) {
									break;
								}
							}
							num6 = 0;
							while (rightSegment != 0 && rightSegment != segment2 && num3 == 0u) {
								int num11;
								int num12;
								uint num13;
								uint num14;
								instance.m_segments.m_buffer[(int)rightSegment].GetLeftAndRightLanes(targetNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, out num11, out num12, out num13, out num14);
								if (num13 != 0u) {
									num5 = rightSegment;
									laneIndex2 = num11;
									num3 = num13;
								} else {
									rightSegment = instance.m_segments.m_buffer[(int)rightSegment].GetRightSegment(targetNodeId);
								}
								if (++num6 == 8) {
									break;
								}
							}
						}
						if (num2 != 0u && (num4 != segment2 || flag3)) {
							this.ProcessItemPedBicycle(item, targetNodeId, num4, ref instance.m_segments.m_buffer[(int)num4], connectOffset, laneIndex, num2); // ped
						}
						if (num3 != 0u && num3 != num2 && (num5 != segment2 || flag3)) {
							this.ProcessItemPedBicycle(item, targetNodeId, num5, ref instance.m_segments.m_buffer[(int)num5], connectOffset, laneIndex2, num3); // ped
						}
						int laneIndex3;
						uint lane3;
						if ((this._vehicleTypes & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None && instance.m_segments.m_buffer[(int)segment2].GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Bicycle, out laneIndex3, out lane3)) {
							this.ProcessItemPedBicycle(item, targetNodeId, segment2, ref instance.m_segments.m_buffer[(int)segment2], connectOffset, laneIndex3, lane3); // bicycle
						}
					} else {
						for (int j = 0; j < 8; j++) {
							ushort segment3 = targetNode.GetSegment(j);
							if (segment3 != 0 && segment3 != segment2) {
								this.ProcessItem(item, targetNodeId, segment3, ref instance.m_segments.m_buffer[(int)segment3], ref similarLaneIndexFromLeft, connectOffset, false, true);
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
					if (laneType != NetInfo.LaneType.None && vehicleType != VehicleInfo.VehicleType.None && instance.m_segments.m_buffer[(int)segment2].GetClosestLane(lane2, laneType, vehicleType, out num15, out lane4)) {
						NetInfo.Lane lane5 = prevSegmentInfo.m_lanes[num15];
						byte connectOffset2;
						if ((instance.m_segments.m_buffer[(int)segment2].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None == ((byte)(lane5.m_finalDirection & NetInfo.Direction.Backward) != 0)) {
							connectOffset2 = 1;
						} else {
							connectOffset2 = 254;
						}
						this.ProcessItemPedBicycle(item, targetNodeId, segment2, ref instance.m_segments.m_buffer[(int)segment2], connectOffset2, num15, lane4); // ped
					}
				} else {
					bool blocked = (targetNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
					bool pedestrianAllowed = (byte)(this._laneTypes & NetInfo.LaneType.Pedestrian) != 0;
					bool enablePedestrian = false;
					byte connectOffset3 = 0;
					if (pedestrianAllowed) {
						if (isBicycleLane) {
							connectOffset3 = connectOffset;
							enablePedestrian = (targetNode.Info.m_class.m_service == ItemClass.Service.Beautification);
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

					ushort nextSegmentId = instance.m_segments.m_buffer[(int)item.m_position.m_segment].GetRightSegment(targetNodeId);

					// NON-STOCK CODE START //
					bool isJunction = targetNode.CountSegments() > 2;
					bool prevIsHighway = false;
					if (prevSegmentInfo.m_netAI is RoadBaseAI)
						prevIsHighway = ((RoadBaseAI)prevSegmentInfo.m_netAI).m_highwayRules;
					NetInfo.Lane lane = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];

					NetInfo.Direction normDirection = TrafficPriority.LeftHandDrive ? NetInfo.Direction.Forward : NetInfo.Direction.Backward; // direction to normalize indices to
					int prevRightSimilarLaneIndex;
					int prevLeftSimilarLaneIndex;
					if ((byte)(lane.m_direction & normDirection) != 0) {
						prevRightSimilarLaneIndex = lane.m_similarLaneIndex;
						prevLeftSimilarLaneIndex = lane.m_similarLaneCount - lane.m_similarLaneIndex - 1;
					} else {
						prevRightSimilarLaneIndex = lane.m_similarLaneCount - lane.m_similarLaneIndex - 1;
						prevLeftSimilarLaneIndex = lane.m_similarLaneIndex;
					}
					bool foundForced = false;

					HashSet<ushort> leftSegments = new HashSet<ushort>();
					HashSet<ushort> rightSegments = new HashSet<ushort>();
					//HashSet<ushort> straightSegments = new HashSet<ushort>();

					if (!enablePedestrian) {
						// calculate segment geometry	
						for (var s = 0; s < 8; s++) {
							var _nextSegmentId = targetNode.GetSegment(s);

							if (_nextSegmentId == 0 || _nextSegmentId == item.m_position.m_segment)
								continue;

							if (TrafficPriority.IsLeftSegment(item.m_position.m_segment, _nextSegmentId, targetNodeId))
								leftSegments.Add(_nextSegmentId);
							else if (TrafficPriority.IsRightSegment(item.m_position.m_segment, _nextSegmentId, targetNodeId))
								rightSegments.Add(_nextSegmentId);
							/*else
								straightSegments.Add(_nextSegmentId);*/
						}
					}
					// NON-STOCK CODE END //

					for (int k = 0; k < 8; k++) {
						if (nextSegmentId == 0 || nextSegmentId == item.m_position.m_segment) {
							break;
						}

						if (!enablePedestrian) {
							// NON-STOCK CODE START //
							var nextSegment = instance.m_segments.m_buffer[nextSegmentId];
							var nextSegmentInfo = nextSegment.Info;

							NetInfo.Direction nextDir = nextSegment.m_startNode != targetNodeId ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
							NetInfo.Direction nextDir2 = ((nextSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? nextDir : NetInfo.InvertDirection(nextDir);

							// valid next lanes:
							int[] laneIndexes = new int[16]; // index of NetNode.Info.m_lanes
							uint[] laneIds = new uint[16]; // index of NetManager.m_lanes.m_buffer
							uint[] indexByRightSimilarLaneIndex = new uint[16];
							uint[] indexByLeftSimilarLaneIndex = new uint[16];

							bool laneArrowsDefined = false;
							uint curLaneI = 0;
							uint curLaneId = nextSegment.m_lanes;
							int i = 0;
							while (i < nextSegmentInfo.m_lanes.Length && curLaneId != 0u) {
								// determine valid lanes based on lane arrows
								NetInfo.Lane nextLane = nextSegmentInfo.m_lanes[i];

								if ((byte)(nextLane.m_finalDirection & nextDir2) != 0 && nextLane.CheckType(_laneTypes, _vehicleTypes)) {
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

									if (((NetLane.Flags)instance.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.LeftForwardRight) != NetLane.Flags.None) {
										laneArrowsDefined = true;
									}

									if (rightSegments.Contains(nextSegmentId)) {// TrafficPriority.IsLeftSegment(nextSegmentId, item.m_position.m_segment, targetNodeId)) {
										if (((NetLane.Flags)instance.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.Left) ==
											NetLane.Flags.Left) {
											laneIndexes[curLaneI] = i;
											laneIds[curLaneI] = curLaneId;
											indexByRightSimilarLaneIndex[nextRightSimilarLaneIndex] = curLaneI + 1;
											indexByLeftSimilarLaneIndex[nextLeftSimilarLaneIndex] = curLaneI + 1;
											curLaneI++;
										}
									} else if (leftSegments.Contains(nextSegmentId)) { // if (TrafficPriority.IsRightSegment(nextSegmentId, item.m_position.m_segment, targetNodeId)) {
										if (((NetLane.Flags)instance.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.Right) ==
											NetLane.Flags.Right) {
											laneIndexes[curLaneI] = i;
											laneIds[curLaneI] = curLaneId;
											indexByRightSimilarLaneIndex[nextRightSimilarLaneIndex] = curLaneI + 1;
											indexByLeftSimilarLaneIndex[nextLeftSimilarLaneIndex] = curLaneI + 1;
											curLaneI++;
										}
									} else {
										if (((NetLane.Flags)instance.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.Forward) != NetLane.Flags.None || ((NetLane.Flags)instance.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.LeftForwardRight) == NetLane.Flags.None) { // valid if straight segment and no arrows given or forward arrow is set
											laneIndexes[curLaneI] = i;
											laneIds[curLaneI] = curLaneId;
											indexByRightSimilarLaneIndex[nextRightSimilarLaneIndex] = curLaneI + 1;
											indexByLeftSimilarLaneIndex[nextLeftSimilarLaneIndex] = curLaneI + 1;
											curLaneI++;
										}
									}
								}

								curLaneId = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_nextLane;
								i++;
							} // foreach lane

							if (laneArrowsDefined) {
								var newLaneIndex = 0;
								var newLaneId = 0u;
								int nextLaneI = -1;
								int nextCompatibleLaneCount = Convert.ToInt32(curLaneI);

								if (prevIsHighway && isJunction) {
									if (leftSegments.Contains(nextSegmentId)) {
										// right segment joins with highway: find directly matching lanes from right
										nextLaneI = Convert.ToInt32(indexByRightSimilarLaneIndex[prevRightSimilarLaneIndex]) - 1;
									} else if (rightSegments.Contains(nextSegmentId)) {
										// left segment joins with highway: find directly matching lanes from left
										nextLaneI = Convert.ToInt32(indexByLeftSimilarLaneIndex[prevLeftSimilarLaneIndex]) - 1;
									} else {
										if (leftSegments.Count() > 0) {
											// sort right
											nextLaneI = Convert.ToInt32(indexByLeftSimilarLaneIndex[prevLeftSimilarLaneIndex]) - 1;
										} else {
											// sort left
											nextLaneI = Convert.ToInt32(indexByRightSimilarLaneIndex[prevRightSimilarLaneIndex]) - 1;
										}
									}
									if (nextLaneI < 0 || nextLaneI >= nextCompatibleLaneCount)
										goto nextIter; // no path to this lane
								} else if (curLaneI > 0) {
									if (curLaneI == 1) {
										nextLaneI = 0;
									} else {
										// lane matching
										int prevSimilarLaneCount = lane.m_similarLaneCount;										

										int nextRightSimilarLaneIndex = -1;
										int x = 0;
										if (isJunction) {
											// at junctions: try to match distinct lanes (1-to-1, n-to-1)
											x = prevRightSimilarLaneIndex;
										} else {
											bool sym1 = (prevSimilarLaneCount & 1) == 0; // mod 2 == 0
											bool sym2 = (nextCompatibleLaneCount & 1) == 0; // mod 2 == 0
											if (prevSimilarLaneCount < nextCompatibleLaneCount) {
												// lane merging
												if (sym1 == sym2) {
													// merge outer lanes
													int a = (nextCompatibleLaneCount - prevSimilarLaneCount) / 2;
													if (prevSimilarLaneCount == 1)
														nextRightSimilarLaneIndex = _pathRandomizer.Int32(0, nextCompatibleLaneCount - 1);
													else if (prevRightSimilarLaneIndex == 0)
														nextRightSimilarLaneIndex = _pathRandomizer.Int32(0, a);
													else if (prevRightSimilarLaneIndex == prevSimilarLaneCount - 1)
														nextRightSimilarLaneIndex = _pathRandomizer.Int32(prevRightSimilarLaneIndex + a, nextCompatibleLaneCount - 1);
													else
														nextRightSimilarLaneIndex = prevRightSimilarLaneIndex + a;
												} else {
													// criss-cross merge
													int a = (nextCompatibleLaneCount - prevSimilarLaneCount - 1) / 2;
													int b = (nextCompatibleLaneCount - prevSimilarLaneCount + 1) / 2;
													if (prevSimilarLaneCount == 1)
														nextRightSimilarLaneIndex = _pathRandomizer.Int32(0, nextCompatibleLaneCount - 1);
													else if (prevRightSimilarLaneIndex == 0)
														nextRightSimilarLaneIndex = _pathRandomizer.Int32(0, b);
													else if (prevRightSimilarLaneIndex == prevSimilarLaneCount - 1)
														nextRightSimilarLaneIndex = _pathRandomizer.Int32(prevRightSimilarLaneIndex + a, nextCompatibleLaneCount - 1);
													else if (_pathRandomizer.Int32(0, 1) == 0)
														nextRightSimilarLaneIndex = prevRightSimilarLaneIndex + a;
													else
														nextRightSimilarLaneIndex = prevRightSimilarLaneIndex + b;
												}
											} else {
												// at lane splits: distribute traffic evenly (1-to-n, n-to-n)										
												if (sym1 == sym2) {
													// split outer lanes
													int a = (prevSimilarLaneCount - nextCompatibleLaneCount) / 2;
													nextRightSimilarLaneIndex = prevRightSimilarLaneIndex - a;
												} else {
													// split outer lanes, criss-cross inner lanes 
													int a = (prevSimilarLaneCount - nextCompatibleLaneCount - 1) / 2;
													if (_pathRandomizer.Int32(0, 1) == 0)
														nextRightSimilarLaneIndex = prevRightSimilarLaneIndex - a;
													else
														nextRightSimilarLaneIndex = prevRightSimilarLaneIndex - a - 1;
												}
											}
											x = COMath.Clamp(nextRightSimilarLaneIndex, 0, nextCompatibleLaneCount - 1);
										}

										// find best matching lane
										for (int j = 0; j < 16; ++j) {
											if (indexByRightSimilarLaneIndex[j] == 0)
												continue;
											nextLaneI = Convert.ToInt32(indexByRightSimilarLaneIndex[j]) - 1;
											nextRightSimilarLaneIndex = j;
											if (x == 0) { // matching lane found
												break;
											}
											--x;
										}
#if DEBUG
										if (debug) {
											Log.Message($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right. There are {curLaneI} candidate lanes. We choose lane {nextLaneI} (index {newLaneIndex}, id {newLaneId}, {nextRightSimilarLaneIndex} from right). lhd: {TrafficPriority.LeftHandDrive}, ped: {pedestrianAllowed}, magical flag4: {blocked}");
										}
#endif
									}
								} else {
#if DEBUG
									if (debug) {
										Log.Message($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: No compatible lanes found");
									}
#endif
									goto nextIter;
								}

								// go to matched lane
								newLaneIndex = laneIndexes[nextLaneI];
								newLaneId = laneIds[nextLaneI];

								if (ProcessItem(item, targetNodeId, nextSegmentId, ref instance.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian, newLaneIndex, newLaneId, out foundForced))
									blocked = true;

								if (foundForced) {
#if DEBUG
									if (debug) {
										Log.Message($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: FORCED LANE FOUND!");
									}
#endif
								}
							} else {
#if DEBUG
								if (debug) {
									Log.Message($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: No lane arrows defined");
								}
#endif

								if (ProcessItem(item, targetNodeId, nextSegmentId, ref instance.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian)) {
									blocked = true;
								}
							}
							// NON-STOCK CODE END
						} else {
							// stock code:
							if (this.ProcessItem(item, targetNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian)) {
								blocked = true;
							}
						}

						nextIter:
						nextSegmentId = instance.m_segments.m_buffer[(int)nextSegmentId].GetRightSegment(targetNodeId);
					} // foreach segment
					if (blocked) {
#if DEBUG
						if (debug) {
							Log.Message($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: Road may be blocked");
						}
#endif
						// vehicles may turn around if the street is blocked
						nextSegmentId = item.m_position.m_segment;
						this.ProcessItem(item, targetNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, false);
					}
					// NON-STOCK CODE START
					if (foundForced)
						return;
					// NON-STOCK CODE END
					if (pedestrianAllowed) {
#if DEBUG
						if (debug) {
							Log.Message($"Exploring path from {nextSegmentId} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: Ped allowed");
						}
#endif
						nextSegmentId = item.m_position.m_segment;
						int laneIndex4;
						uint lane6;
						if (instance.m_segments.m_buffer[(int)nextSegmentId].GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Pedestrian, this._vehicleTypes, out laneIndex4, out lane6)) {
							this.ProcessItemPedBicycle(item, targetNodeId, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], connectOffset3, laneIndex4, lane6); // ped
						}
					}
				}
				if (targetNode.m_lane != 0u) {
					bool targetDisabled = (targetNode.m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None;
					ushort segment4 = instance.m_lanes.m_buffer[(int)((UIntPtr)targetNode.m_lane)].m_segment;
					if (segment4 != 0 && segment4 != item.m_position.m_segment) {
#if DEBUG
						if (debug) {
							Log.Message($"Exploring path from {segment4} to {item.m_position.m_segment}, lane id {item.m_position.m_lane}: dont know what this does");
						}
#endif
						this.ProcessItem(item, targetNodeId, targetDisabled, segment4, ref instance.m_segments.m_buffer[(int)segment4], targetNode.m_lane, targetNode.m_laneOffset, connectOffset);
					}
				}
			} catch (Exception e) {
				Log.Error("Path find failure! " + e.ToString() + " " + e.StackTrace.ToString() + " " + e.Source.ToString());
			}
		}

#region stock code
		// 2
		private void ProcessItem(BufferItem item, ushort targetNodeId, bool targetDisabled, ushort nextSegmentId, ref NetSegment nextSegment, uint lane, byte offset, byte connectOffset) {
			if ((nextSegment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None) {
				return;
			}
			NetManager instance = Singleton<NetManager>.instance;
			if (targetDisabled && ((instance.m_nodes.m_buffer[(int)nextSegment.m_startNode].m_flags | instance.m_nodes.m_buffer[(int)nextSegment.m_endNode].m_flags) & NetNode.Flags.Disabled) == NetNode.Flags.None) {
				return;
			}
			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = instance.m_segments.m_buffer[(int)item.m_position.m_segment].Info;
			int num = nextSegmentInfo.m_lanes.Length;
			uint num2 = nextSegment.m_lanes;
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
			int num8 = 0;
			while (num8 < num && num2 != 0u) {
				if (lane == num2) {
					NetInfo.Lane lane3 = nextSegmentInfo.m_lanes[num8];
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
						item2.m_position.m_lane = (byte)num8;
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
				num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
				num8++;
			}
		}

		private bool ProcessItem(BufferItem item, ushort targetNode, ushort segmentID, ref NetSegment segment, ref int currentTargetIndex, byte connectOffset, bool enableVehicle, bool enablePedestrian) {
			bool foundForced = false;
			return ProcessItem(item, targetNode, segmentID, ref segment, ref currentTargetIndex, connectOffset, enableVehicle, enablePedestrian, null, null, out foundForced);
		}

		// 3
		private bool ProcessItem(BufferItem item, ushort targetNodeId, ushort segmentID, ref NetSegment nextSegment, ref int currentTargetIndex, byte connectOffset, bool enableVehicle, bool enablePedestrian, int? forceLaneIndex, uint? forceLaneId, out bool foundForced) {
			foundForced = false;
			bool result = false;
			if ((nextSegment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None) {
				return result;
			}
			NetManager instance = Singleton<NetManager>.instance;
			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = instance.m_segments.m_buffer[(int)item.m_position.m_segment].Info;
			int nextNumLanes = nextSegmentInfo.m_lanes.Length;
			uint curLaneId = (uint)(forceLaneId != null ? forceLaneId : nextSegment.m_lanes); // NON-STOCK CODE
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
			float maxSpeed = 1f;
			float laneSpeed = 1f;
			NetInfo.LaneType laneType = NetInfo.LaneType.None;
			VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None;
			// NON-STOCK CODE START //
			bool customLaneChanging = !Options.isStockLaneChangerUsed();
			bool prevIsHighway = false;
			if (prevSegmentInfo.m_netAI is RoadBaseAI)
				prevIsHighway = ((RoadBaseAI)prevSegmentInfo.m_netAI).m_highwayRules;
			float density = 0f;
			int prevRightSimilarLaneIndex = -1;
			NetInfo.Direction normDirection = TrafficPriority.LeftHandDrive ? NetInfo.Direction.Forward : NetInfo.Direction.Backward; // direction to normalize indices to
			// NON-STOCK CODE END //
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane lane = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				// NON-STOCK CODE START //
				if (customLaneChanging) {
					density = (float)instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_trafficDensity * 0.01f;
				}
				// NON-STOCK CODE END //
				laneType = lane.m_laneType;
				vehicleType = lane.m_vehicleType;
				maxSpeed = lane.m_speedLimit;
				laneSpeed = this.CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[(int)item.m_position.m_segment], lane);
				// NON-STOCK CODE START //
				if ((byte)(lane.m_direction & normDirection) != 0) {
					prevRightSimilarLaneIndex = lane.m_similarLaneIndex;
				} else {
					prevRightSimilarLaneIndex = lane.m_similarLaneCount - lane.m_similarLaneIndex - 1;
				}
				// NON-STOCK CODE END //
			}
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
			float num8 = (float)Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * 0.003921569f * cost;
			float num9 = item.m_methodDistance + num8;
			float num10 = item.m_comparisonValue + num8 / (laneSpeed * this._maxLength);
			Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)item.m_laneID)].CalculatePosition((float)connectOffset * 0.003921569f);
			int num11 = currentTargetIndex;
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
			bool nextIsJunction = instance.m_nodes.m_buffer[targetNodeId].CountSegments() > 2;
			ushort sourceNodeId = (targetNodeId == instance.m_segments.m_buffer[item.m_position.m_segment].m_startNode) ? instance.m_segments.m_buffer[item.m_position.m_segment].m_endNode : instance.m_segments.m_buffer[item.m_position.m_segment].m_startNode; // no lane changing directly in front of a junction
			//NetNode sourceNode = instance.m_nodes.m_buffer[sourceNodeId];
			bool prevIsJunction = instance.m_nodes.m_buffer[sourceNodeId].CountSegments() > 2;
			bool changeLane = !prevIsHighway && !prevIsJunction && !this._stablePath && density <= 0.5f && forceLaneIndex == null && customLaneChanging && _pathRandomizer.Int32(1, Options.getLaneChangingRandomizationTargetValue()) == 1; // lane randomization
			int laneIndex = (int)(forceLaneIndex != null ? forceLaneIndex : 0);
			bool nextIsHighway = false;
			if (nextSegmentInfo.m_netAI is RoadBaseAI)
				nextIsHighway = ((RoadBaseAI)nextSegmentInfo.m_netAI).m_highwayRules;
			// NON-STOCK CODE END //
			while (laneIndex < nextNumLanes && curLaneId != 0u) {
				// NON-STOCK CODE START //
				if (forceLaneIndex != null && laneIndex != forceLaneIndex)
					break;
				// NON-STOCK CODE END //

				NetInfo.Lane nextLane = nextSegmentInfo.m_lanes[laneIndex];
				if ((byte)(nextLane.m_finalDirection & nextDir2) != 0) {
					if (nextLane.CheckType(laneType2, vehicleType2) && (segmentID != item.m_position.m_segment || laneIndex != (int)item.m_position.m_lane) && (byte)(nextLane.m_finalDirection & nextDir2) != 0) {
						Vector3 a;
						if ((byte)(nextDir & NetInfo.Direction.Forward) != 0) {
							a = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_bezier.d;
						} else {
							a = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_bezier.a;
						}
						float distanceOnBezier = customLaneChanging ? 1f : Vector3.Distance(a, b); // NON-STOCK CODE
						if (transitionNode) {
							distanceOnBezier *= 2f;
						}
						float num14 = distanceOnBezier / ((maxSpeed + nextLane.m_speedLimit) * 0.5f * this._maxLength);
						BufferItem item2;
						// NON-STOCK CODE START //
						if (nextIsJunction)
							item2.m_numSegmentsToJunction = 0;
						else if (prevIsJunction)
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
							item2.m_methodDistance = num9 + distanceOnBezier;
						}
						if (nextLane.m_laneType != NetInfo.LaneType.Pedestrian || item2.m_methodDistance < 1000f) {
							item2.m_comparisonValue = num10 + num14;
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
								int firstTarget = (int)instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_firstTarget;
								int lastTarget = (int)instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_lastTarget;
								if (currentTargetIndex < firstTarget || currentTargetIndex >= lastTarget) {
									item2.m_comparisonValue += Mathf.Max(1f, distanceOnBezier * 3f - 3f) / ((maxSpeed + nextLane.m_speedLimit) * 0.5f * this._maxLength);
								}
								if (!this._transportVehicle && nextLane.m_laneType == NetInfo.LaneType.TransportVehicle) {
									item2.m_comparisonValue += 20f / ((maxSpeed + nextLane.m_speedLimit) * 0.5f * this._maxLength);
								}
							}

							// NON-STOCK CODE START //
							if (customLaneChanging && forceLaneIndex == null && nextLane.m_similarLaneCount > 1) {
								int nextRightSimilarLaneIndex;
								if ((byte)(nextLane.m_direction & normDirection) != 0) {
									nextRightSimilarLaneIndex = nextLane.m_similarLaneIndex;
								} else {
									nextRightSimilarLaneIndex = nextLane.m_similarLaneCount - nextLane.m_similarLaneIndex - 1;
								}

								if (changeLane) {
									// calculate current similar lane index starting from right lane

									if (Math.Abs(nextRightSimilarLaneIndex - prevRightSimilarLaneIndex) == 1) {
										// reward for changing to adjacent (+-1) lane
										item2.m_comparisonValue -= 0.5f;
									}/* else if (nextRightSimilarLaneIndex == prevRightSimilarLaneIndex) {
										// punishment for staying on lane
										item2.m_comparisonValue += 1f;
									}*/
								} else {
									if (nextRightSimilarLaneIndex == prevRightSimilarLaneIndex) {
										// reward for staying on lane
										if (nextIsHighway) {
											// on highway: high reward for staying on lane
											item2.m_comparisonValue -= 1f;
										} else {
											// on road: reward depends on traffic density (higher density => more cars stay on lane)
											item2.m_comparisonValue -= density;
										}
									} else {
										// punishment for changing lane
										float junctionCost = 0f;
										if (nextIsHighway) {
											// on highways: vehicles should switch lanes 2-3 segments before an exit
											if (item2.m_numSegmentsToJunction <= _pathRandomizer.Int32(1, 3))
												junctionCost = 0.5f;
											else if (item2.m_numSegmentsToJunction > _pathRandomizer.Int32(2, 4))
												junctionCost = 0.1f * (float)Math.Max(0, 10 - item2.m_numSegmentsToJunction);
										} else {
											// on roads: vehicles should not switch lane before junctions
											junctionCost = 0.5f * (float)Math.Max(0, 10 - item2.m_numSegmentsToJunction);
										}
										float laneDist = density * Math.Abs(nextRightSimilarLaneIndex - prevRightSimilarLaneIndex);
										item2.m_comparisonValue += laneDist + junctionCost;
									}
								}
								item2.m_comparisonValue = Math.Max(0f, item2.m_comparisonValue);
							}
							if (forceLaneIndex != null && laneIndex == forceLaneIndex)
								foundForced = true;
							// NON-STOCK CODE END //

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
			}
			currentTargetIndex = num11;
			return result;
		}

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

		private void AddBufferItem(BufferItem item, PathUnit.Position target)
        {
            uint num = _laneLocation[(int)((UIntPtr)item.m_laneID)];
            uint num2 = num >> 16;
            int num3 = (int)(num & 65535u);
            int num6;
            if (num2 == _pathFindIndex)
            {
                if (item.m_comparisonValue >= _buffer[num3].m_comparisonValue)
                {
                    return;
                }
                int num4 = num3 >> 6;
                int num5 = num3 & -64;
                if (num4 < _bufferMinPos || (num4 == _bufferMinPos && num5 < _bufferMin[num4]))
                {
                    return;
                }
                num6 = Mathf.Max(Mathf.RoundToInt(item.m_comparisonValue * 1024f), _bufferMinPos);
                if (num6 == num4)
                {
                    _buffer[num3] = item;
                    _laneTarget[(int)((UIntPtr)item.m_laneID)] = target;
                    return;
                }
                int num7 = num4 << 6 | _bufferMax[num4]--;
                BufferItem bufferItem = _buffer[num7];
                _laneLocation[(int)((UIntPtr)bufferItem.m_laneID)] = num;
                _buffer[num3] = bufferItem;
            }
            else
            {
                num6 = Mathf.Max(Mathf.RoundToInt(item.m_comparisonValue * 1024f), _bufferMinPos);
            }
            if (num6 >= 1024)
            {
                return;
            }
            while (_bufferMax[num6] == 63)
            {
                num6++;
                if (num6 == 1024)
                {
                    return;
                }
            }
            if (num6 > _bufferMaxPos)
            {
                _bufferMaxPos = num6;
            }
            num3 = (num6 << 6 | ++_bufferMax[num6]);
            _buffer[num3] = item;
            _laneLocation[(int)((UIntPtr)item.m_laneID)] = (_pathFindIndex << 16 | (uint)num3);
            _laneTarget[(int)((UIntPtr)item.m_laneID)] = target;
        }
        private void GetLaneDirection(PathUnit.Position pathPos, out NetInfo.Direction direction, out NetInfo.LaneType type)
        {
            NetManager instance = Singleton<NetManager>.instance;
            NetInfo info = instance.m_segments.m_buffer[pathPos.m_segment].Info;
            if (info.m_lanes.Length > pathPos.m_lane)
            {
                direction = info.m_lanes[pathPos.m_lane].m_finalDirection;
                type = info.m_lanes[pathPos.m_lane].m_laneType;
                if ((instance.m_segments.m_buffer[pathPos.m_segment].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None)
                {
                    direction = NetInfo.InvertDirection(direction);
                }
            }
            else
            {
                direction = NetInfo.Direction.None;
                type = NetInfo.LaneType.None;
            }
        }

        private void PathFindThread()
        {
            while (true)
            {
                while (!Monitor.TryEnter(QueueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    while (QueueFirst == 0u && !Terminated)
                    {
                        Monitor.Wait(QueueLock);
                    }
                    if (Terminated)
                    {
                        break;
                    }
                    Calculating = QueueFirst;
                    QueueFirst = _pathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_nextPathUnit;
                    if (QueueFirst == 0u)
                    {
                        QueueLast = 0u;
                        m_queuedPathFindCount = 0;
                    }
                    else
                    {
                        m_queuedPathFindCount--;
                    }
                    _pathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_nextPathUnit = 0u;
                    _pathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags = (byte)((_pathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags & -2) | 2);
                }
                finally
                {
                    Monitor.Exit(QueueLock);
                }
                try
                {
                    m_pathfindProfiler.BeginStep();
                    try
                    {
                        PathFindImplementation(Calculating, ref _pathUnits.m_buffer[(int)((UIntPtr)Calculating)]);
                    }
                    finally
                    {
                        m_pathfindProfiler.EndStep();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("path thread error: " + ex.Message);
                    UIView.ForwardException(ex);
                    Log.Error("Path find error: " + ex.Message + "\n" + ex.StackTrace);
                    var expr_1A0Cp0 = _pathUnits.m_buffer;
                    var expr_1A0Cp1 = (UIntPtr)Calculating;
                    expr_1A0Cp0[(int)expr_1A0Cp1].m_pathFindFlags = (byte)(expr_1A0Cp0[(int)expr_1A0Cp1].m_pathFindFlags | 8);
                }
                while (!Monitor.TryEnter(QueueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    _pathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags = (byte)(_pathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags & -3);
                    Singleton<PathManager>.instance.ReleasePath(Calculating);
                    Calculating = 0u;
                    Monitor.Pulse(QueueLock);
                }
                finally
                {
                    Monitor.Exit(QueueLock);
                }
            }
        }
	}
}
