using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using TrafficManager.Traffic;
using UnityEngine;

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
			BufferItem bufferItem;
			if (data.m_position00.m_segment != 0 && num >= 1) {
				this._startLaneA = PathManager.GetLaneID(data.m_position00);
				this._startOffsetA = data.m_position00.m_offset;
				bufferItem.m_laneID = this._startLaneA;
				bufferItem.m_position = data.m_position00;
				this.GetLaneDirection(data.m_position00, out bufferItem.m_direction, out bufferItem.m_lanesUsed);
				bufferItem.m_comparisonValue = 0f;
			} else {
				this._startLaneA = 0u;
				this._startOffsetA = 0;
				bufferItem = default(BufferItem);
			}
			BufferItem bufferItem2;
			if (data.m_position02.m_segment != 0 && num >= 3) {
				this._startLaneB = PathManager.GetLaneID(data.m_position02);
				this._startOffsetB = data.m_position02.m_offset;
				bufferItem2.m_laneID = this._startLaneB;
				bufferItem2.m_position = data.m_position02;
				this.GetLaneDirection(data.m_position02, out bufferItem2.m_direction, out bufferItem2.m_lanesUsed);
				bufferItem2.m_comparisonValue = 0f;
			} else {
				this._startLaneB = 0u;
				this._startOffsetB = 0;
				bufferItem2 = default(BufferItem);
			}
			BufferItem bufferItem3;
			if (data.m_position01.m_segment != 0 && num >= 2) {
				this._endLaneA = PathManager.GetLaneID(data.m_position01);
				bufferItem3.m_laneID = this._endLaneA;
				bufferItem3.m_position = data.m_position01;
				this.GetLaneDirection(data.m_position01, out bufferItem3.m_direction, out bufferItem3.m_lanesUsed);
				bufferItem3.m_methodDistance = 0f;
				bufferItem3.m_comparisonValue = 0f;
			} else {
				this._endLaneA = 0u;
				bufferItem3 = default(BufferItem);
			}
			BufferItem bufferItem4;
			if (data.m_position03.m_segment != 0 && num >= 4) {
				this._endLaneB = PathManager.GetLaneID(data.m_position03);
				bufferItem4.m_laneID = this._endLaneB;
				bufferItem4.m_position = data.m_position03;
				this.GetLaneDirection(data.m_position03, out bufferItem4.m_direction, out bufferItem4.m_lanesUsed);
				bufferItem4.m_methodDistance = 0f;
				bufferItem4.m_comparisonValue = 0f;
			} else {
				this._endLaneB = 0u;
				bufferItem4 = default(BufferItem);
			}
			if (data.m_position11.m_segment != 0 && num2 >= 1) {
				this._vehicleLane = PathManager.GetLaneID(data.m_position11);
				this._vehicleOffset = data.m_position11.m_offset;
			} else {
				this._vehicleLane = 0u;
				this._vehicleOffset = 0;
			}
			BufferItem bufferItem5 = default(BufferItem);
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
			if (bufferItem3.m_position.m_segment != 0) {
				this._bufferMax[0]++;
				this._buffer[++this._bufferMaxPos] = bufferItem3;
			}
			if (bufferItem4.m_position.m_segment != 0) {
				this._bufferMax[0]++;
				this._buffer[++this._bufferMaxPos] = bufferItem4;
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
					if (bufferItem6.m_position.m_segment == bufferItem.m_position.m_segment && bufferItem6.m_position.m_lane == bufferItem.m_position.m_lane) {
						if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Forward) != 0 && bufferItem6.m_position.m_offset >= this._startOffsetA) {
							bufferItem5 = bufferItem6;
							b = this._startOffsetA;
							flag = true;
							break;
						}
						if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Backward) != 0 && bufferItem6.m_position.m_offset <= this._startOffsetA) {
							bufferItem5 = bufferItem6;
							b = this._startOffsetA;
							flag = true;
							break;
						}
					}
					if (bufferItem6.m_position.m_segment == bufferItem2.m_position.m_segment && bufferItem6.m_position.m_lane == bufferItem2.m_position.m_lane) {
						if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Forward) != 0 && bufferItem6.m_position.m_offset >= this._startOffsetB) {
							bufferItem5 = bufferItem6;
							b = this._startOffsetB;
							flag = true;
							break;
						}
						if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Backward) != 0 && bufferItem6.m_position.m_offset <= this._startOffsetB) {
							bufferItem5 = bufferItem6;
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
			float num8 = bufferItem5.m_comparisonValue * this._maxLength;
			this._pathUnits.m_buffer[(int)((UIntPtr)unit)].m_length = num8;
			uint num9 = unit;
			int num10 = 0;
			int num11 = 0;
			PathUnit.Position position = bufferItem5.m_position;
			if ((position.m_segment != bufferItem3.m_position.m_segment || position.m_lane != bufferItem3.m_position.m_lane || position.m_offset != bufferItem3.m_position.m_offset) && (position.m_segment != bufferItem4.m_position.m_segment || position.m_lane != bufferItem4.m_position.m_lane || position.m_offset != bufferItem4.m_position.m_offset)) {
				if (b != position.m_offset) {
					PathUnit.Position position2 = position;
					position2.m_offset = b;
					this._pathUnits.m_buffer[(int)((UIntPtr)num9)].SetPosition(num10++, position2);
				}
				this._pathUnits.m_buffer[(int)((UIntPtr)num9)].SetPosition(num10++, position);
				position = this._laneTarget[(int)((UIntPtr)bufferItem5.m_laneID)];
			}
			for (int k = 0; k < 262144; k++) {
				this._pathUnits.m_buffer[(int)((UIntPtr)num9)].SetPosition(num10++, position);
				if ((position.m_segment == bufferItem3.m_position.m_segment && position.m_lane == bufferItem3.m_position.m_lane && position.m_offset == bufferItem3.m_position.m_offset) || (position.m_segment == bufferItem4.m_position.m_segment && position.m_lane == bufferItem4.m_position.m_lane && position.m_offset == bufferItem4.m_position.m_offset)) {
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

		// 1
		private void ProcessItemMain(BufferItem item, ushort nodeID, ref NetNode node, byte connectOffset, bool isMiddle) {
#if DEBUG
			//bool debug = nodeID == 28311u && item.m_position.m_segment == 33016;
			//bool debug = nodeID == 26128u && item.m_position.m_segment == 4139 && nextSegmentId == 27106;
			//bool debug = nodeID == 13630u && item.m_position.m_segment == 35546u;
			bool debug = false;
#endif


			NetManager instance = Singleton<NetManager>.instance;
			bool isPedestrianLane = false;
			bool isBicycleLane = false;
			int similarLaneIndexFromLeft = 0; // similar index, starting with 0 at leftmost lane
			NetInfo info = instance.m_segments.m_buffer[(int)item.m_position.m_segment].Info;
			if ((int)item.m_position.m_lane < info.m_lanes.Length) {
				NetInfo.Lane lane = info.m_lanes[(int)item.m_position.m_lane];
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
					ushort segment = node.GetSegment(i);
					if (segment <= 0)
						continue;
					this.ProcessItem(item, nodeID, segment, ref instance.m_segments.m_buffer[(int)segment], ref similarLaneIndexFromLeft, connectOffset, !isPedestrianLane, isPedestrianLane);
				}
			} else if (isPedestrianLane) {
				ushort segment2 = item.m_position.m_segment;
				int lane2 = (int)item.m_position.m_lane;
				if (node.Info.m_class.m_service != ItemClass.Service.Beautification) {
					bool flag3 = (node.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None;
					int laneIndex;
					int laneIndex2;
					uint num2;
					uint num3;
					instance.m_segments.m_buffer[(int)segment2].GetLeftAndRightLanes(nodeID, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, lane2, out laneIndex, out laneIndex2, out num2, out num3);
					ushort num4 = segment2;
					ushort num5 = segment2;
					if (num2 == 0u || num3 == 0u) {
						ushort leftSegment;
						ushort rightSegment;
						instance.m_segments.m_buffer[(int)segment2].GetLeftAndRightSegments(nodeID, out leftSegment, out rightSegment);
						int num6 = 0;
						while (leftSegment != 0 && leftSegment != segment2 && num2 == 0u) {
							int num7;
							int num8;
							uint num9;
							uint num10;
							instance.m_segments.m_buffer[(int)leftSegment].GetLeftAndRightLanes(nodeID, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, out num7, out num8, out num9, out num10);
							if (num10 != 0u) {
								num4 = leftSegment;
								laneIndex = num8;
								num2 = num10;
							} else {
								leftSegment = instance.m_segments.m_buffer[(int)leftSegment].GetLeftSegment(nodeID);
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
							instance.m_segments.m_buffer[(int)rightSegment].GetLeftAndRightLanes(nodeID, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, out num11, out num12, out num13, out num14);
							if (num13 != 0u) {
								num5 = rightSegment;
								laneIndex2 = num11;
								num3 = num13;
							} else {
								rightSegment = instance.m_segments.m_buffer[(int)rightSegment].GetRightSegment(nodeID);
							}
							if (++num6 == 8) {
								break;
							}
						}
					}
					if (num2 != 0u && (num4 != segment2 || flag3)) {
						this.ProcessItemPedBicycle(item, nodeID, num4, ref instance.m_segments.m_buffer[(int)num4], connectOffset, laneIndex, num2); // ped
					}
					if (num3 != 0u && num3 != num2 && (num5 != segment2 || flag3)) {
						this.ProcessItemPedBicycle(item, nodeID, num5, ref instance.m_segments.m_buffer[(int)num5], connectOffset, laneIndex2, num3); // ped
					}
					int laneIndex3;
					uint lane3;
					if ((this._vehicleTypes & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None && instance.m_segments.m_buffer[(int)segment2].GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Bicycle, out laneIndex3, out lane3)) {
						this.ProcessItemPedBicycle(item, nodeID, segment2, ref instance.m_segments.m_buffer[(int)segment2], connectOffset, laneIndex3, lane3); // bicycle
					}
				} else {
					for (int j = 0; j < 8; j++) {
						ushort segment3 = node.GetSegment(j);
						if (segment3 != 0 && segment3 != segment2) {
							this.ProcessItem(item, nodeID, segment3, ref instance.m_segments.m_buffer[(int)segment3], ref similarLaneIndexFromLeft, connectOffset, false, true);
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
					NetInfo.Lane lane5 = info.m_lanes[num15];
					byte connectOffset2;
					if ((instance.m_segments.m_buffer[(int)segment2].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None == ((byte)(lane5.m_finalDirection & NetInfo.Direction.Backward) != 0)) {
						connectOffset2 = 1;
					} else {
						connectOffset2 = 254;
					}
					this.ProcessItemPedBicycle(item, nodeID, segment2, ref instance.m_segments.m_buffer[(int)segment2], connectOffset2, num15, lane4); // ped
				}
			} else {
				bool blocked = (node.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
				bool pedestrianAllowed = (byte)(this._laneTypes & NetInfo.LaneType.Pedestrian) != 0;
				bool enablePedestrian = false;
				byte connectOffset3 = 0;
				if (pedestrianAllowed) {
					if (isBicycleLane) {
						connectOffset3 = connectOffset;
						enablePedestrian = (node.Info.m_class.m_service == ItemClass.Service.Beautification);
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

				ushort nextSegmentId = instance.m_segments.m_buffer[(int)item.m_position.m_segment].GetRightSegment(nodeID);

				// NON-STOCK CODE START //
				NetInfo.Lane lane = info.m_lanes[(int)item.m_position.m_lane];

				NetInfo.Direction normDirection = TrafficPriority.LeftHandDrive ? NetInfo.Direction.Forward : NetInfo.Direction.Backward; // direction to normalize indices to
				int prevRightSimilarLaneIndex;
				if ((byte)(lane.m_direction & normDirection) != 0) {
					prevRightSimilarLaneIndex = lane.m_similarLaneIndex;
				} else {
					prevRightSimilarLaneIndex = lane.m_similarLaneCount - lane.m_similarLaneIndex - 1;
				}
				// NON-STOCK CODE END //

				for (int k = 0; k < 8; k++) {
					if (nextSegmentId == 0 || nextSegmentId == item.m_position.m_segment) {
						break;
					}

#if DEBUG
					if (nextSegmentId == 7386u)
						debug = false;
#endif

					// NON-STOCK CODE START //
					// "nextSegment" is actually the previous segment because path-finding runs from target to start

					var nextSegment = instance.m_segments.m_buffer[nextSegmentId];
					var nextSegmentInfo = nextSegment.Info;

					NetInfo.Direction nextDir = nextSegment.m_startNode != nodeID ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
					NetInfo.Direction nextDir2 = ((nextSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? nextDir : NetInfo.InvertDirection(nextDir);

					// valid next lanes:
					int[] laneIndexes = new int[16]; // index of NetNode.Info.m_lanes
					uint[] laneIds = new uint[16]; // index of NetManager.m_lanes.m_buffer
					uint[] indexByRightSimilarLaneIndex = new uint[16];

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
							if ((byte)(nextLane.m_direction & normDirection) != 0) {
								nextRightSimilarLaneIndex = nextLane.m_similarLaneIndex;
							} else {
								nextRightSimilarLaneIndex = nextLane.m_similarLaneCount - nextLane.m_similarLaneIndex - 1;
							}

							if (((NetLane.Flags)instance.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.LeftForwardRight) != NetLane.Flags.None) {
								laneArrowsDefined = true;
							}
								
							if (TrafficPriority.IsLeftSegment(nextSegmentId, item.m_position.m_segment, nodeID)) {
								if (((NetLane.Flags)instance.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.Left) ==
									NetLane.Flags.Left) {
									laneIndexes[curLaneI] = i;
									laneIds[curLaneI] = curLaneId;
									indexByRightSimilarLaneIndex[nextRightSimilarLaneIndex] = curLaneI + 1;
									curLaneI++;
								}
							} else if (TrafficPriority.IsRightSegment(nextSegmentId, item.m_position.m_segment, nodeID)) {
								if (((NetLane.Flags)instance.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.Right) ==
									NetLane.Flags.Right) {
									laneIndexes[curLaneI] = i;
									laneIds[curLaneI] = curLaneId;
									indexByRightSimilarLaneIndex[nextRightSimilarLaneIndex] = curLaneI + 1;
									curLaneI++;
								}
							} else {
								if (((NetLane.Flags)instance.m_lanes.m_buffer[curLaneId].m_flags & NetLane.Flags.Forward) ==
									NetLane.Flags.Forward) {
									laneIndexes[curLaneI] = i;
									laneIds[curLaneI] = curLaneId;
									indexByRightSimilarLaneIndex[nextRightSimilarLaneIndex] = curLaneI + 1;
									curLaneI++;
								}
							}
						}

						curLaneId = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_nextLane;
						i++;
					}

					if (laneArrowsDefined) {
						var newLaneIndex = 0;
						var newLaneId = 0u;

						if (curLaneI > 0) {
							if (curLaneI == 1) {
								newLaneIndex = laneIndexes[0];
								newLaneId = laneIds[0];
							} else {
								// lane matching
								int x = prevRightSimilarLaneIndex;
								int nextLaneI = -1;
								int nextRightSimilarLaneIndex = -1;
								for (int j = 0; j < 16; ++j) {
									if (indexByRightSimilarLaneIndex[j] == 0)
										continue;
									nextLaneI = (int)indexByRightSimilarLaneIndex[j] - 1;
									nextRightSimilarLaneIndex = j;
									if (x == 0) { // matching lane found
										break;
									}
									--x;
								}

								newLaneIndex = laneIndexes[nextLaneI];
								newLaneId = laneIds[nextLaneI];

#if DEBUG
								if (debug) {
									Log.Message($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right. There are {curLaneI} candidate lanes. We choose lane {nextLaneI} (index {newLaneIndex}, id {newLaneId}, {nextRightSimilarLaneIndex} from right). lhd: {TrafficPriority.LeftHandDrive}, ped: {pedestrianAllowed}, magical flag4: {blocked}");
                                }
#endif
							}

							if (ProcessItem(item, nodeID, nextSegmentId, ref instance.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian, newLaneIndex, newLaneId))
								blocked = true;
						} else {
#if DEBUG
							if (debug) {
								Log.Message($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: No compatible lanes found");
							}
#endif
						}
					} else {
#if DEBUG
						if (debug) {
							Log.Message($"Exploring path from {nextSegmentId} ({nextDir}) to {item.m_position.m_segment}, lane id {item.m_position.m_lane}, {prevRightSimilarLaneIndex} from right: No lane arrows defined");
						}
#endif

						if (ProcessItem(item, nodeID, nextSegmentId, ref instance.m_segments.m_buffer[nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, enablePedestrian)) {
							blocked = true;
						}
					}
					// NON-STOCK CODE END

					// stock code:
					/*if (this.ProcessItem(item, nodeID, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref dirSimilarLaneIndex, connectOffset, true, enablePedestrian)) {
						flag4 = true;
					}*/

					nextSegmentId = instance.m_segments.m_buffer[(int)nextSegmentId].GetRightSegment(nodeID);
				}
				if (blocked) {
					// vehicles may turn around if the street is blocked
					nextSegmentId = item.m_position.m_segment;
					this.ProcessItem(item, nodeID, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], ref similarLaneIndexFromLeft, connectOffset, true, false);
				}
				if (pedestrianAllowed) {
					nextSegmentId = item.m_position.m_segment;
					int laneIndex4;
					uint lane6;
					if (instance.m_segments.m_buffer[(int)nextSegmentId].GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Pedestrian, this._vehicleTypes, out laneIndex4, out lane6)) {
						this.ProcessItemPedBicycle(item, nodeID, nextSegmentId, ref instance.m_segments.m_buffer[(int)nextSegmentId], connectOffset3, laneIndex4, lane6); // ped
					}
				}
			}
			if (node.m_lane != 0u) {
				bool targetDisabled = (node.m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None;
				ushort segment4 = instance.m_lanes.m_buffer[(int)((UIntPtr)node.m_lane)].m_segment;
				if (segment4 != 0 && segment4 != item.m_position.m_segment) {
					this.ProcessItem(item, nodeID, targetDisabled, segment4, ref instance.m_segments.m_buffer[(int)segment4], node.m_lane, node.m_laneOffset, connectOffset);
				}
			}
		}

#region stock code
		// 2
		private void ProcessItem(BufferItem item, ushort targetNode, bool targetDisabled, ushort segmentID, ref NetSegment segment, uint lane, byte offset, byte connectOffset) {
			if ((segment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None) {
				return;
			}
			NetManager instance = Singleton<NetManager>.instance;
			if (targetDisabled && ((instance.m_nodes.m_buffer[(int)segment.m_startNode].m_flags | instance.m_nodes.m_buffer[(int)segment.m_endNode].m_flags) & NetNode.Flags.Disabled) == NetNode.Flags.None) {
				return;
			}
			NetInfo info = segment.Info;
			NetInfo info2 = instance.m_segments.m_buffer[(int)item.m_position.m_segment].Info;
			int num = info.m_lanes.Length;
			uint num2 = segment.m_lanes;
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
			Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)item.m_laneID)].CalculatePosition((float)connectOffset * 0.003921569f);
			int num8 = 0;
			while (num8 < num && num2 != 0u) {
				if (lane == num2) {
					NetInfo.Lane lane3 = info.m_lanes[num8];
					if (lane3.CheckType(this._laneTypes, this._vehicleTypes)) {
						Vector3 a = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].CalculatePosition((float)offset * 0.003921569f);
						float num9 = Vector3.Distance(a, b);
						BufferItem item2;
						item2.m_position.m_segment = segmentID;
						item2.m_position.m_lane = (byte)num8;
						item2.m_position.m_offset = offset;
						if ((byte)(lane3.m_laneType & laneType) == 0) {
							item2.m_methodDistance = 0f;
						} else {
							item2.m_methodDistance = num6 + num9;
						}
						if (lane3.m_laneType != NetInfo.LaneType.Pedestrian || item2.m_methodDistance < 1000f) {
							item2.m_comparisonValue = num7 + num9 / ((num3 + lane3.m_speedLimit) * 0.5f * this._maxLength);
							if ((segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
								item2.m_direction = NetInfo.InvertDirection(lane3.m_finalDirection);
							} else {
								item2.m_direction = lane3.m_finalDirection;
							}
							if (lane == this._startLaneA) {
								if (((byte)(item2.m_direction & NetInfo.Direction.Forward) == 0 || item2.m_position.m_offset < this._startOffsetA) && ((byte)(item2.m_direction & NetInfo.Direction.Backward) == 0 || item2.m_position.m_offset > this._startOffsetA)) {
									return;
								}
								float num10 = this.CalculateLaneSpeed(this._startOffsetA, item2.m_position.m_offset, ref segment, lane3);
								float num11 = (float)Mathf.Abs((int)(item2.m_position.m_offset - this._startOffsetA)) * 0.003921569f;
								item2.m_comparisonValue += num11 * segment.m_averageLength / (num10 * this._maxLength);
							}
							if (lane == this._startLaneB) {
								if (((byte)(item2.m_direction & NetInfo.Direction.Forward) == 0 || item2.m_position.m_offset < this._startOffsetB) && ((byte)(item2.m_direction & NetInfo.Direction.Backward) == 0 || item2.m_position.m_offset > this._startOffsetB)) {
									return;
								}
								float num12 = this.CalculateLaneSpeed(this._startOffsetB, item2.m_position.m_offset, ref segment, lane3);
								float num13 = (float)Mathf.Abs((int)(item2.m_position.m_offset - this._startOffsetB)) * 0.003921569f;
								item2.m_comparisonValue += num13 * segment.m_averageLength / (num12 * this._maxLength);
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
			return ProcessItem(item, targetNode, segmentID, ref segment, ref currentTargetIndex, connectOffset, enableVehicle, enablePedestrian, null, null);
		}

		// 3
		private bool ProcessItem(BufferItem item, ushort targetNode, ushort segmentID, ref NetSegment nextSegment, ref int currentTargetIndex, byte connectOffset, bool enableVehicle, bool enablePedestrian, int? forceLaneIndex, uint? forceLaneId) {
			bool result = false;
			if ((nextSegment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None) {
				return result;
			}
			NetManager instance = Singleton<NetManager>.instance;
			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = instance.m_segments.m_buffer[(int)item.m_position.m_segment].Info;
			int nextNumLanes = nextSegmentInfo.m_lanes.Length;
			uint curLaneId = (uint)(forceLaneId != null ? forceLaneId : nextSegment.m_lanes); // NON-STOCK CODE
			NetInfo.Direction nextDir = (targetNode != nextSegment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
			NetInfo.Direction nextDir2 = ((nextSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? nextDir : NetInfo.InvertDirection(nextDir);
			float num3 = 0.01f - Mathf.Min(nextSegmentInfo.m_maxTurnAngleCos, prevSegmentInfo.m_maxTurnAngleCos);
			if (num3 < 1f) {
				Vector3 vector;
				if (targetNode == instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_startNode) {
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
			float num5 = 1f;
			float num6 = 1f;
			NetInfo.LaneType laneType = NetInfo.LaneType.None;
			VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None;
			// NON-STOCK CODE START //
			int prevRightSimilarLaneIndex = -1;
			NetInfo.Direction normDirection = TrafficPriority.LeftHandDrive ? NetInfo.Direction.Forward : NetInfo.Direction.Backward; // direction to normalize indices to
			// NON-STOCK CODE END //
			if ((int)item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane lane = prevSegmentInfo.m_lanes[(int)item.m_position.m_lane];
				laneType = lane.m_laneType;
				vehicleType = lane.m_vehicleType;
				num5 = lane.m_speedLimit;
				num6 = this.CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[(int)item.m_position.m_segment], lane);
				// NON-STOCK CODE START //
				if ((byte)(lane.m_direction & normDirection) != 0) {
					prevRightSimilarLaneIndex = lane.m_similarLaneIndex;
				} else {
					prevRightSimilarLaneIndex = lane.m_similarLaneCount - lane.m_similarLaneIndex - 1;
				}
				// NON-STOCK CODE END //
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
			// NON-STOCK CODE START //
			bool changeLane = !this._stablePath && forceLaneIndex == null && Options.laneChangingRandomization < 4 && _pathRandomizer.Int32(1, Options.getLaneChangingRandomizationTargetValue()) == 1; // lane randomization
			int laneIndex = (int)(forceLaneIndex != null ? forceLaneIndex : 0);
			// NON-STOCK CODE END //
			while (laneIndex < nextNumLanes && curLaneId != 0u) {
				// NON-STOCK CODE START //
				if (forceLaneIndex != null && laneIndex != forceLaneIndex)
					break;
				// NON-STOCK CODE END //

				NetInfo.Lane nextLane = nextSegmentInfo.m_lanes[laneIndex];
				if ((byte)(nextLane.m_finalDirection & nextDir2) != 0) {
					if (nextLane.CheckType(laneType2, vehicleType2) && (segmentID != item.m_position.m_segment || laneIndex != (int)item.m_position.m_lane) && (byte)(nextLane.m_finalDirection & nextDir2) != 0) {
						// NON-STOCK CODE START //
						if (changeLane && nextLane.m_similarLaneCount > 1) {
							// calculate current similar lane index starting from right lane
							int nextRightSimilarLaneIndex;
							if ((byte)(nextLane.m_direction & normDirection) != 0) {
								nextRightSimilarLaneIndex = nextLane.m_similarLaneIndex;
							} else {
								nextRightSimilarLaneIndex = nextLane.m_similarLaneCount - nextLane.m_similarLaneIndex - 1;
							}

							if (nextRightSimilarLaneIndex != prevRightSimilarLaneIndex + 1 && nextRightSimilarLaneIndex != prevRightSimilarLaneIndex - 1) {
								goto IL_8F5;
							}
						}
						// NON-STOCK CODE END //

						Vector3 a;
						if ((byte)(nextDir & NetInfo.Direction.Forward) != 0) {
							a = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_bezier.d;
						} else {
							a = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_bezier.a;
						}
						float num13 = Vector3.Distance(a, b);
						if (flag) {
							num13 *= 2f;
						}
						float num14 = num13 / ((num5 + nextLane.m_speedLimit) * 0.5f * this._maxLength);
						BufferItem item2;
						item2.m_position.m_segment = segmentID;
						item2.m_position.m_lane = (byte)laneIndex;
						item2.m_position.m_offset = (byte)(((nextDir & NetInfo.Direction.Forward) == 0) ? 0 : 255);
						if ((byte)(nextLane.m_laneType & laneType) == 0) {
							item2.m_methodDistance = 0f;
						} else {
							item2.m_methodDistance = num9 + num13;
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
									item2.m_comparisonValue += Mathf.Max(1f, num13 * 3f - 3f) / ((num5 + nextLane.m_speedLimit) * 0.5f * this._maxLength);
								}
								if (!this._transportVehicle && nextLane.m_laneType == NetInfo.LaneType.TransportVehicle) {
									item2.m_comparisonValue += 20f / ((num5 + nextLane.m_speedLimit) * 0.5f * this._maxLength);
								}
							}
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
		private void ProcessItemPedBicycle(BufferItem item, ushort targetNode, ushort segmentID, ref NetSegment segment, byte connectOffset, int laneIndex, uint lane) {
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
				NetInfo.Direction direction = (targetNode != segment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
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
			if (laneIndex < num) {
				NetInfo.Lane lane3 = info.m_lanes[laneIndex];
				BufferItem item2;
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
