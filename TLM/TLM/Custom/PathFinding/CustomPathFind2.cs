using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace TrafficManager.Custom.PathFinding {
	public class CustomPathFind2 : PathFind {
		private const float BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR = 0.003921569f;
		private const float TICKET_COST_CONVERSION_FACTOR = BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * 0.0001f;

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
		private NetSegment.Flags m_disableMask;
		private Randomizer m_pathRandomizer;
		private uint m_pathFindIndex;
		private NetInfo.LaneType m_laneTypes;
		private VehicleInfo.VehicleType m_vehicleTypes;

		public ThreadProfiler m_pathfindProfiler;
		public volatile int m_queuedPathFindCount;
		private uint m_queueFirst;
		private uint m_queueLast;
		private uint m_calculating;
		private object m_queueLock;
		private object m_bufferLock;
		private Array32<PathUnit> m_pathUnits;
		private Thread m_pathFindThread;
		private bool m_terminated;

		public bool IsAvailable {
			get {
				return this.m_pathFindThread.IsAlive;
			}
		}

		private void Awake() {
			this.m_pathfindProfiler = new ThreadProfiler();
			this.m_laneLocation = new uint[262144];
			this.m_laneTarget = new PathUnit.Position[262144];
			this.m_buffer = new BufferItem[65536];
			this.m_bufferMin = new int[1024];
			this.m_bufferMax = new int[1024];
			this.m_queueLock = new object();
			this.m_bufferLock = Singleton<PathManager>.instance.m_bufferLock;
			this.m_pathUnits = Singleton<PathManager>.instance.m_pathUnits;
			this.m_pathFindThread = new Thread(this.PathFindThread);
			this.m_pathFindThread.Name = "Pathfind";
			this.m_pathFindThread.Priority = SimulationManager.SIMULATION_PRIORITY;
			this.m_pathFindThread.Start();
			if (!this.m_pathFindThread.IsAlive) {
				CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find thread failed to start!");
			}
		}

		private void OnDestroy() {
			while (!Monitor.TryEnter(this.m_queueLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) {
			}
			try {
				this.m_terminated = true;
				Monitor.PulseAll(this.m_queueLock);
			} finally {
				Monitor.Exit(this.m_queueLock);
			}
		}

		public bool CalculatePath(uint unit, bool skipQueue) {
			if (Singleton<PathManager>.instance.AddPathReference(unit)) {
				while (!Monitor.TryEnter(this.m_queueLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) {
				}
				try {
					if (skipQueue) {
						if (this.m_queueLast == 0) {
							this.m_queueLast = unit;
						} else {
							this.m_pathUnits.m_buffer[unit].m_nextPathUnit = this.m_queueFirst;
						}
						this.m_queueFirst = unit;
					} else {
						if (this.m_queueLast == 0) {
							this.m_queueFirst = unit;
						} else {
							this.m_pathUnits.m_buffer[this.m_queueLast].m_nextPathUnit = unit;
						}
						this.m_queueLast = unit;
					}
					this.m_pathUnits.m_buffer[unit].m_pathFindFlags |= 1;
					this.m_queuedPathFindCount++;
					Monitor.Pulse(this.m_queueLock);
				} finally {
					Monitor.Exit(this.m_queueLock);
				}
				return true;
			}
			return false;
		}

		private void PathFindImplementation(uint unit, ref PathUnit data) {
			NetManager netManager = Singleton<NetManager>.instance;

			this.m_laneTypes = (NetInfo.LaneType)this.m_pathUnits.m_buffer[unit].m_laneTypes;
			this.m_vehicleTypes = (VehicleInfo.VehicleType)this.m_pathUnits.m_buffer[unit].m_vehicleTypes;
			this.m_maxLength = this.m_pathUnits.m_buffer[unit].m_length;
			this.m_pathFindIndex = (this.m_pathFindIndex + 1 & 0x7FFF);
			this.m_pathRandomizer = new Randomizer(unit);
			this.m_carBanMask = NetSegment.Flags.CarBan;

			if ((this.m_pathUnits.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_IS_HEAVY) != 0) {
				this.m_carBanMask |= NetSegment.Flags.HeavyBan;
			}

			if ((this.m_pathUnits.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_READY) != 0) {
				this.m_carBanMask |= NetSegment.Flags.WaitingPath;
			}

			this.m_ignoreBlocked = ((this.m_pathUnits.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_IGNORE_BLOCKED) != 0);
			this.m_stablePath = ((this.m_pathUnits.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_STABLE_PATH) != 0);
			this.m_randomParking = ((this.m_pathUnits.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_RANDOM_PARKING) != 0);
			this.m_transportVehicle = ((this.m_laneTypes & NetInfo.LaneType.TransportVehicle) != NetInfo.LaneType.None);
			this.m_ignoreCost = (this.m_stablePath || (this.m_pathUnits.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_IGNORE_COST) != 0);
			this.m_disableMask = (NetSegment.Flags.Collapsed | NetSegment.Flags.PathFailed);

			if ((this.m_pathUnits.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_IGNORE_FLOODED) == 0) {
				this.m_disableMask |= NetSegment.Flags.Flooded;
			}

			if ((this.m_laneTypes & NetInfo.LaneType.Vehicle) != NetInfo.LaneType.None) {
				this.m_laneTypes |= NetInfo.LaneType.TransportVehicle;
			}

			int posCount = this.m_pathUnits.m_buffer[unit].m_positionCount & 0xF;
			int vehiclePosIndicator = this.m_pathUnits.m_buffer[unit].m_positionCount >> 4;
			BufferItem bufferItemStartA = default(BufferItem);
			if (data.m_position00.m_segment != 0 && posCount >= 1) {
				this.m_startLaneA = PathManager.GetLaneID(data.m_position00);
				this.m_startOffsetA = data.m_position00.m_offset;
				bufferItemStartA.m_laneID = this.m_startLaneA;
				bufferItemStartA.m_position = data.m_position00;
				this.GetLaneDirection(data.m_position00, out bufferItemStartA.m_direction, out bufferItemStartA.m_lanesUsed);
				bufferItemStartA.m_comparisonValue = 0f;
				bufferItemStartA.m_duration = 0f;
			} else {
				this.m_startLaneA = 0u;
				this.m_startOffsetA = 0;
				bufferItemStartA = default(BufferItem);
			}

			BufferItem bufferItemStartB = default(BufferItem);
			if (data.m_position02.m_segment != 0 && posCount >= 3) {
				this.m_startLaneB = PathManager.GetLaneID(data.m_position02);
				this.m_startOffsetB = data.m_position02.m_offset;
				bufferItemStartB.m_laneID = this.m_startLaneB;
				bufferItemStartB.m_position = data.m_position02;
				this.GetLaneDirection(data.m_position02, out bufferItemStartB.m_direction, out bufferItemStartB.m_lanesUsed);
				bufferItemStartB.m_comparisonValue = 0f;
				bufferItemStartB.m_duration = 0f;
			} else {
				this.m_startLaneB = 0u;
				this.m_startOffsetB = 0;
				bufferItemStartB = default(BufferItem);
			}

			BufferItem bufferItemEndA = default(BufferItem);
			if (data.m_position01.m_segment != 0 && posCount >= 2) {
				this.m_endLaneA = PathManager.GetLaneID(data.m_position01);
				bufferItemEndA.m_laneID = this.m_endLaneA;
				bufferItemEndA.m_position = data.m_position01;
				this.GetLaneDirection(data.m_position01, out bufferItemEndA.m_direction, out bufferItemEndA.m_lanesUsed);
				bufferItemEndA.m_methodDistance = 0.01f;
				bufferItemEndA.m_comparisonValue = 0f;
				bufferItemEndA.m_duration = 0f;
			} else {
				this.m_endLaneA = 0u;
				bufferItemEndA = default(BufferItem);
			}

			BufferItem bufferItemEndB = default(BufferItem);
			if (data.m_position03.m_segment != 0 && posCount >= 4) {
				this.m_endLaneB = PathManager.GetLaneID(data.m_position03);
				bufferItemEndB.m_laneID = this.m_endLaneB;
				bufferItemEndB.m_position = data.m_position03;
				this.GetLaneDirection(data.m_position03, out bufferItemEndB.m_direction, out bufferItemEndB.m_lanesUsed);
				bufferItemEndB.m_methodDistance = 0.01f;
				bufferItemEndB.m_comparisonValue = 0f;
				bufferItemEndB.m_duration = 0f;
			} else {
				this.m_endLaneB = 0u;
				bufferItemEndB = default(BufferItem);
			}

			if (data.m_position11.m_segment != 0 && vehiclePosIndicator >= 1) {
				this.m_vehicleLane = PathManager.GetLaneID(data.m_position11);
				this.m_vehicleOffset = data.m_position11.m_offset;
			} else {
				this.m_vehicleLane = 0u;
				this.m_vehicleOffset = 0;
			}

			BufferItem finalBufferItem = default(BufferItem);
			byte startOffset = 0;
			this.m_bufferMinPos = 0;
			this.m_bufferMaxPos = -1;

			if (this.m_pathFindIndex == 0) {
				uint num3 = 4294901760u;
				for (int i = 0; i < 262144; i++) {
					this.m_laneLocation[i] = num3;
				}
			}

			for (int j = 0; j < 1024; j++) {
				this.m_bufferMin[j] = 0;
				this.m_bufferMax[j] = -1;
			}

			if (bufferItemEndA.m_position.m_segment != 0) {
				this.m_bufferMax[0]++;
				this.m_buffer[++this.m_bufferMaxPos] = bufferItemEndA;
			}

			if (bufferItemEndB.m_position.m_segment != 0) {
				this.m_bufferMax[0]++;
				this.m_buffer[++this.m_bufferMaxPos] = bufferItemEndB;
			}

			bool canFindPath = false;
			while (this.m_bufferMinPos <= this.m_bufferMaxPos) {
				int bufMin = this.m_bufferMin[this.m_bufferMinPos];
				int bufMax = this.m_bufferMax[this.m_bufferMinPos];

				if (bufMin > bufMax) {
					this.m_bufferMinPos++;
				} else {
					this.m_bufferMin[this.m_bufferMinPos] = bufMin + 1;
					BufferItem candidateItem = this.m_buffer[(this.m_bufferMinPos << 6) + bufMin];
					if (candidateItem.m_position.m_segment == bufferItemStartA.m_position.m_segment && candidateItem.m_position.m_lane == bufferItemStartA.m_position.m_lane) {
						if ((candidateItem.m_direction & NetInfo.Direction.Forward) != NetInfo.Direction.None && candidateItem.m_position.m_offset >= this.m_startOffsetA) {
							finalBufferItem = candidateItem;
							startOffset = this.m_startOffsetA;
							canFindPath = true;
							break;
						}

						if ((candidateItem.m_direction & NetInfo.Direction.Backward) != NetInfo.Direction.None && candidateItem.m_position.m_offset <= this.m_startOffsetA) {
							finalBufferItem = candidateItem;
							startOffset = this.m_startOffsetA;
							canFindPath = true;
							break;
						}
					}

					if (candidateItem.m_position.m_segment == bufferItemStartB.m_position.m_segment && candidateItem.m_position.m_lane == bufferItemStartB.m_position.m_lane) {
						if ((candidateItem.m_direction & NetInfo.Direction.Forward) != NetInfo.Direction.None && candidateItem.m_position.m_offset >= this.m_startOffsetB) {
							finalBufferItem = candidateItem;
							startOffset = this.m_startOffsetB;
							canFindPath = true;
							break;
						}

						if ((candidateItem.m_direction & NetInfo.Direction.Backward) != NetInfo.Direction.None && candidateItem.m_position.m_offset <= this.m_startOffsetB) {
							finalBufferItem = candidateItem;
							startOffset = this.m_startOffsetB;
							canFindPath = true;
							break;
						}
					}

					if ((candidateItem.m_direction & NetInfo.Direction.Forward) != NetInfo.Direction.None) {
						ushort startNodeId = netManager.m_segments.m_buffer[candidateItem.m_position.m_segment].m_startNode;
						this.ProcessItemMain(candidateItem, ref netManager.m_segments.m_buffer[candidateItem.m_position.m_segment], ref netManager.m_lanes.m_buffer[candidateItem.m_laneID], startNodeId, ref netManager.m_nodes.m_buffer[startNodeId], ref netManager.m_lanes.m_buffer[netManager.m_nodes.m_buffer[startNodeId].m_lane], 0, false);
					}

					if ((candidateItem.m_direction & NetInfo.Direction.Backward) != NetInfo.Direction.None) {
						ushort endNodeId = netManager.m_segments.m_buffer[candidateItem.m_position.m_segment].m_endNode;
						this.ProcessItemMain(candidateItem, ref netManager.m_segments.m_buffer[candidateItem.m_position.m_segment], ref netManager.m_lanes.m_buffer[candidateItem.m_laneID], endNodeId, ref netManager.m_nodes.m_buffer[endNodeId], ref netManager.m_lanes.m_buffer[netManager.m_nodes.m_buffer[endNodeId].m_lane], 255, false);
					}

					int numIter = 0;
					ushort specialNodeId = netManager.m_lanes.m_buffer[candidateItem.m_laneID].m_nodes;
					if (specialNodeId != 0) {
						ushort startNode2 = netManager.m_segments.m_buffer[candidateItem.m_position.m_segment].m_startNode;
						ushort endNode2 = netManager.m_segments.m_buffer[candidateItem.m_position.m_segment].m_endNode;
						bool nodesDisabled = ((netManager.m_nodes.m_buffer[startNode2].m_flags | netManager.m_nodes.m_buffer[endNode2].m_flags) & NetNode.Flags.Disabled) != NetNode.Flags.None;

						while (specialNodeId != 0) {
							NetInfo.Direction direction = NetInfo.Direction.None;
							byte laneOffset = netManager.m_nodes.m_buffer[specialNodeId].m_laneOffset;

							if (laneOffset <= candidateItem.m_position.m_offset) {
								direction |= NetInfo.Direction.Forward;
							}

							if (laneOffset >= candidateItem.m_position.m_offset) {
								direction |= NetInfo.Direction.Backward;
							}

							if ((candidateItem.m_direction & direction) != NetInfo.Direction.None && (!nodesDisabled || (netManager.m_nodes.m_buffer[specialNodeId].m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None)) {
								this.ProcessItemMain(candidateItem, ref netManager.m_segments.m_buffer[candidateItem.m_position.m_segment], ref netManager.m_lanes.m_buffer[candidateItem.m_laneID], specialNodeId, ref netManager.m_nodes.m_buffer[specialNodeId], ref netManager.m_lanes.m_buffer[netManager.m_nodes.m_buffer[specialNodeId].m_lane], laneOffset, true);
							}

							specialNodeId = netManager.m_nodes.m_buffer[specialNodeId].m_nextLaneNode;

							if (++numIter == 32768) {
								break;
							}
						}
					}
				}
			}

			if (!canFindPath) {
				this.m_pathUnits.m_buffer[unit].m_pathFindFlags |= 8;
			} else {
				float duration = (this.m_laneTypes != NetInfo.LaneType.Pedestrian) ? finalBufferItem.m_duration : finalBufferItem.m_methodDistance;
				this.m_pathUnits.m_buffer[unit].m_length = duration;

				uint currentPathUnitId = unit;
				int currentItemPositionCount = 0;
				int sumOfPositionCounts = 0;
				PathUnit.Position currentPosition = finalBufferItem.m_position;

				if ((currentPosition.m_segment != bufferItemEndA.m_position.m_segment || currentPosition.m_lane != bufferItemEndA.m_position.m_lane || currentPosition.m_offset != bufferItemEndA.m_position.m_offset) &&
					(currentPosition.m_segment != bufferItemEndB.m_position.m_segment || currentPosition.m_lane != bufferItemEndB.m_position.m_lane || currentPosition.m_offset != bufferItemEndB.m_position.m_offset)) {
					if (startOffset != currentPosition.m_offset) {
						PathUnit.Position position2 = currentPosition;
						position2.m_offset = startOffset;
						this.m_pathUnits.m_buffer[currentPathUnitId].SetPosition(currentItemPositionCount++, position2);
					}

					this.m_pathUnits.m_buffer[currentPathUnitId].SetPosition(currentItemPositionCount++, currentPosition);
					currentPosition = this.m_laneTarget[finalBufferItem.m_laneID];
				}

				for (int k = 0; k < 262144; k++) {
					this.m_pathUnits.m_buffer[currentPathUnitId].SetPosition(currentItemPositionCount++, currentPosition);

					if ((currentPosition.m_segment == bufferItemEndA.m_position.m_segment && currentPosition.m_lane == bufferItemEndA.m_position.m_lane && currentPosition.m_offset == bufferItemEndA.m_position.m_offset) ||
					(currentPosition.m_segment == bufferItemEndB.m_position.m_segment && currentPosition.m_lane == bufferItemEndB.m_position.m_lane && currentPosition.m_offset == bufferItemEndB.m_position.m_offset)) {
						this.m_pathUnits.m_buffer[currentPathUnitId].m_positionCount = (byte)currentItemPositionCount;
						sumOfPositionCounts += currentItemPositionCount;
						if (sumOfPositionCounts != 0) {
							currentPathUnitId = this.m_pathUnits.m_buffer[unit].m_nextPathUnit;
							currentItemPositionCount = this.m_pathUnits.m_buffer[unit].m_positionCount;
							int num16 = 0;
							while (currentPathUnitId != 0) {
								this.m_pathUnits.m_buffer[currentPathUnitId].m_length = duration * (float)(sumOfPositionCounts - currentItemPositionCount) / (float)sumOfPositionCounts;
								currentItemPositionCount += this.m_pathUnits.m_buffer[currentPathUnitId].m_positionCount;
								currentPathUnitId = this.m_pathUnits.m_buffer[currentPathUnitId].m_nextPathUnit;
								if (++num16 >= 262144) {
									CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
									break;
								}
							}
						}
						this.m_pathUnits.m_buffer[unit].m_pathFindFlags |= 4;
						return;
					}
					
					if (currentItemPositionCount == 12) {
						while (!Monitor.TryEnter(this.m_bufferLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) {
						}

						uint createdPathUnitId = default(uint);
						try {
							if (!this.m_pathUnits.CreateItem(out createdPathUnitId, ref this.m_pathRandomizer)) {
								this.m_pathUnits.m_buffer[unit].m_pathFindFlags |= 8;
								return;
							}

							this.m_pathUnits.m_buffer[createdPathUnitId] = this.m_pathUnits.m_buffer[currentPathUnitId];
							this.m_pathUnits.m_buffer[createdPathUnitId].m_referenceCount = 1;
							this.m_pathUnits.m_buffer[createdPathUnitId].m_pathFindFlags = 4;
							this.m_pathUnits.m_buffer[currentPathUnitId].m_nextPathUnit = createdPathUnitId;
							this.m_pathUnits.m_buffer[currentPathUnitId].m_positionCount = (byte)currentItemPositionCount;
							sumOfPositionCounts += currentItemPositionCount;
							Singleton<PathManager>.instance.m_pathUnitCount = (int)(this.m_pathUnits.ItemCount() - 1);
						} finally {
							Monitor.Exit(this.m_bufferLock);
						}

						currentPathUnitId = createdPathUnitId;
						currentItemPositionCount = 0;
					}

					uint laneID = PathManager.GetLaneID(currentPosition);
					currentPosition = this.m_laneTarget[laneID];
				}

				this.m_pathUnits.m_buffer[unit].m_pathFindFlags |= 8;
			}
		}

		// 1
		private void ProcessItemMain(BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, ushort nextNodeId, ref NetNode nextNode, ref NetLane nextNodeLane, byte connectOffset, bool isMiddle) {
			NetManager netManager = Singleton<NetManager>.instance;

			ushort prevSegmentId = item.m_position.m_segment;
			bool prevIsPedestrianLane = false;
			bool prevIsBicycleLane = false;
			bool prevIsCenterPlatform = false;
			bool prevIsElevated = false;
			int prevRelSimilarLaneIndex = 0;
			NetInfo prevSegmentInfo = prevSegment.Info;
			if (item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[item.m_position.m_lane];
				prevIsPedestrianLane = (prevLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian);
				prevIsBicycleLane = (prevLaneInfo.m_laneType == NetInfo.LaneType.Vehicle && (prevLaneInfo.m_vehicleType & this.m_vehicleTypes) == VehicleInfo.VehicleType.Bicycle);
				prevIsCenterPlatform = prevLaneInfo.m_centerPlatform;
				prevIsElevated = prevLaneInfo.m_elevated;
				prevRelSimilarLaneIndex = (((prevLaneInfo.m_finalDirection & NetInfo.Direction.Forward) == NetInfo.Direction.None) ? (prevLaneInfo.m_similarLaneCount - prevLaneInfo.m_similarLaneIndex - 1) : prevLaneInfo.m_similarLaneIndex);
			}

			if (isMiddle) {
				for (int i = 0; i < 8; i++) {
					ushort nextSegmentId = nextNode.GetSegment(i);
					if (nextSegmentId != 0) {
						this.ProcessItemCosts(item, nextSegmentId, ref prevSegment, ref prevLane, nextNodeId, ref nextNode, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, !prevIsPedestrianLane, prevIsPedestrianLane);
					}
				}
			} else if (prevIsPedestrianLane) {
				if (!prevIsElevated) {
					int prevLaneIndex = item.m_position.m_lane;
					if (nextNode.Info.m_class.m_service != ItemClass.Service.Beautification) {
						bool canCrossStreet = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None;
						bool isOnCenterPlatform = prevIsCenterPlatform && (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Junction)) == NetNode.Flags.None;
						ushort nextLeftSegmentId = prevSegmentId;
						ushort nextRightSegmentId = prevSegmentId;
						int leftLaneIndex = default(int);
						int rightLaneIndex = default(int);
						uint leftLaneId = default(uint);
						uint rightLaneId = default(uint);
						prevSegment.GetLeftAndRightLanes(nextNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, prevLaneIndex, isOnCenterPlatform, out leftLaneIndex, out rightLaneIndex, out leftLaneId, out rightLaneId);

						if (leftLaneId == 0 || rightLaneId == 0) {
							ushort leftSegmentId = default(ushort);
							ushort rightSegmentId = default(ushort);
							prevSegment.GetLeftAndRightSegments(nextNodeId, out leftSegmentId, out rightSegmentId);

							int numIter = 0;
							while (leftSegmentId != 0 && leftSegmentId != prevSegmentId && leftLaneId == 0) {
								int someLeftLaneIndex = default(int);
								int someRightLaneIndex = default(int);
								uint someLeftLaneId = default(uint);
								uint someRightLaneId = default(uint);
								netManager.m_segments.m_buffer[leftSegmentId].GetLeftAndRightLanes(nextNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, isOnCenterPlatform, out someLeftLaneIndex, out someRightLaneIndex, out someLeftLaneId, out someRightLaneId);

								if (someRightLaneId != 0) {
									nextLeftSegmentId = leftSegmentId;
									leftLaneIndex = someRightLaneIndex;
									leftLaneId = someRightLaneId;
								} else {
									leftSegmentId = netManager.m_segments.m_buffer[leftSegmentId].GetLeftSegment(nextNodeId);
								}

								if (++numIter == 8) {
									break;
								}
							}

							numIter = 0;
							while (rightSegmentId != 0 && rightSegmentId != prevSegmentId && rightLaneId == 0) {
								int someLeftLaneIndex = default(int);
								int someRightLaneIndex = default(int);
								uint someLeftLaneId = default(uint);
								uint someRightLaneId = default(uint);
								netManager.m_segments.m_buffer[rightSegmentId].GetLeftAndRightLanes(nextNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, isOnCenterPlatform, out someLeftLaneIndex, out someRightLaneIndex, out someLeftLaneId, out someRightLaneId);

								if (someLeftLaneId != 0) {
									nextRightSegmentId = rightSegmentId;
									rightLaneIndex = someLeftLaneIndex;
									rightLaneId = someLeftLaneId;
								} else {
									rightSegmentId = netManager.m_segments.m_buffer[rightSegmentId].GetRightSegment(nextNodeId);
								}

								if (++numIter == 8) {
									break;
								}
							}
						}

						if (leftLaneId != 0 && (nextLeftSegmentId != prevSegmentId || canCrossStreet || isOnCenterPlatform)) {
							this.ProcessItemPedBicycle(item, ref prevSegment, ref prevLane, nextLeftSegmentId, ref netManager.m_segments.m_buffer[nextLeftSegmentId], nextNodeId, ref nextNode, leftLaneIndex, leftLaneId, ref netManager.m_lanes.m_buffer[leftLaneId], connectOffset, connectOffset);
						}

						if (rightLaneId != 0 && rightLaneId != leftLaneId && (nextRightSegmentId != prevSegmentId || canCrossStreet || isOnCenterPlatform)) {
							this.ProcessItemPedBicycle(item, ref prevSegment, ref prevLane, nextRightSegmentId, ref netManager.m_segments.m_buffer[nextRightSegmentId], nextNodeId, ref nextNode, rightLaneIndex, rightLaneId, ref netManager.m_lanes.m_buffer[rightLaneId], connectOffset, connectOffset);
						}

						int nextLaneIndex = default(int);
						uint nextLaneId = default(uint);
						if ((this.m_vehicleTypes & VehicleInfo.VehicleType.Bicycle) != 0 && prevSegment.GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Bicycle, out nextLaneIndex, out nextLaneId)) {
							this.ProcessItemPedBicycle(item, ref prevSegment, ref prevLane, prevSegmentId, ref prevSegment, nextNodeId, ref nextNode, nextLaneIndex, nextLaneId, ref netManager.m_lanes.m_buffer[nextLaneId], connectOffset, connectOffset);
						}
					} else {
						for (int j = 0; j < 8; j++) {
							ushort nextSegmentId = nextNode.GetSegment(j);
							if (nextSegmentId != 0 && nextSegmentId != prevSegmentId) {
								this.ProcessItemCosts(item, nextSegmentId, ref prevSegment, ref prevLane, nextNodeId, ref nextNode, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, false, true);
							}
						}
					}

					NetInfo.LaneType laneType = this.m_laneTypes & ~NetInfo.LaneType.Pedestrian;
					VehicleInfo.VehicleType vehicleType = this.m_vehicleTypes & ~VehicleInfo.VehicleType.Bicycle;
					if ((item.m_lanesUsed & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None) {
						laneType &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
					}

					int sameSegLaneIndex = default(int);
					uint sameSegLaneId = default(uint);
					if (laneType != NetInfo.LaneType.None && vehicleType != 0 && prevSegment.GetClosestLane(prevLaneIndex, laneType, vehicleType, out sameSegLaneIndex, out sameSegLaneId)) {
						NetInfo.Lane sameSegLaneInfo = prevSegmentInfo.m_lanes[sameSegLaneIndex];
						byte sameSegConnectOffset = (byte)(((prevSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None == ((sameSegLaneInfo.m_finalDirection & NetInfo.Direction.Backward) != NetInfo.Direction.None)) ? 1 : 254);
						BufferItem nextItem = item;
						if (this.m_randomParking) {
							nextItem.m_comparisonValue += (float)this.m_pathRandomizer.Int32(300u) / this.m_maxLength;
						}
						this.ProcessItemPedBicycle(item, ref prevSegment, ref prevLane, prevSegmentId, ref prevSegment, nextNodeId, ref nextNode, sameSegLaneIndex, sameSegLaneId, ref netManager.m_lanes.m_buffer[sameSegLaneId], sameSegConnectOffset, 128);
					}
				}
			} else {
				bool allowPedestrian = (this.m_laneTypes & NetInfo.LaneType.Pedestrian) != NetInfo.LaneType.None;
				bool allowBicycle = false;
				byte parkingConnectOffset = 0;
				if (allowPedestrian) {
					if (prevIsBicycleLane) {
						parkingConnectOffset = connectOffset;
						allowBicycle = (nextNode.Info.m_class.m_service == ItemClass.Service.Beautification);
					} else if (this.m_vehicleLane != 0) {
						if (this.m_vehicleLane != item.m_laneID) {
							allowPedestrian = false;
						} else {
							parkingConnectOffset = this.m_vehicleOffset;
						}
					} else {
						parkingConnectOffset = (byte)((!this.m_stablePath) ? ((byte)this.m_pathRandomizer.UInt32(1u, 254u)) : 128);
					}
				}

				ushort nextSegmentId = 0;
				if ((this.m_vehicleTypes & (VehicleInfo.VehicleType.Ferry | VehicleInfo.VehicleType.Monorail)) != VehicleInfo.VehicleType.None) {
					bool isUturnAllowedHere = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None;
					for (int k = 0; k < 8; k++) {
						nextSegmentId = nextNode.GetSegment(k);
						if (nextSegmentId != 0 && nextSegmentId != prevSegmentId) {
							this.ProcessItemCosts(item, nextSegmentId, ref prevSegment, ref prevLane, nextNodeId, ref nextNode, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, true, allowBicycle);
						}
					}

					if (isUturnAllowedHere && (this.m_vehicleTypes & VehicleInfo.VehicleType.Monorail) == VehicleInfo.VehicleType.None) {
						nextSegmentId = prevSegmentId;
						this.ProcessItemCosts(item, nextSegmentId, ref prevSegment, ref prevLane, nextNodeId, ref nextNode, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, true, false);
					}
				} else {
					bool isUturnAllowedHere = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
					nextSegmentId = prevSegment.GetRightSegment(nextNodeId);
					for (int l = 0; l < 8; l++) {
						if (nextSegmentId == 0) {
							break;
						}

						if (nextSegmentId == prevSegmentId) {
							break;
						}

						if (this.ProcessItemCosts(item, nextSegmentId, ref prevSegment, ref prevLane, nextNodeId, ref nextNode, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, true, allowBicycle)) {
							isUturnAllowedHere = true;
						}

						nextSegmentId = netManager.m_segments.m_buffer[nextSegmentId].GetRightSegment(nextNodeId);
					}

					if (isUturnAllowedHere && (this.m_vehicleTypes & VehicleInfo.VehicleType.Tram) == VehicleInfo.VehicleType.None) {
						this.ProcessItemCosts(item, prevSegmentId, ref prevSegment, ref prevLane, nextNodeId, ref nextNode, ref prevSegment, ref prevRelSimilarLaneIndex, connectOffset, true, false);
					}
				}

				if (allowPedestrian) {
					int nextLaneIndex = default(int);
					uint nextLaneId = default(uint);
					if (prevSegment.GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Pedestrian, this.m_vehicleTypes, out nextLaneIndex, out nextLaneId)) {
						this.ProcessItemPedBicycle(item, ref prevSegment, ref prevLane, prevSegmentId, ref prevSegment, nextNodeId, ref nextNode, nextLaneIndex, nextLaneId, ref netManager.m_lanes.m_buffer[nextLaneId], parkingConnectOffset, parkingConnectOffset);
					}
				}
			}

			if (nextNode.m_lane != 0) {
				bool targetDisabled = (nextNode.m_flags & (NetNode.Flags.Disabled | NetNode.Flags.DisableOnlyMiddle)) == NetNode.Flags.Disabled;
				ushort nextSegmentId = nextNodeLane.m_segment;
				if (nextSegmentId != 0 && nextSegmentId != prevSegmentId) {
					this.ProcessItemPublicTransport(item, ref prevSegment, ref prevLane, nextNodeId, targetDisabled, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], nextNode.m_lane, nextNode.m_laneOffset, connectOffset);
				}
			}
		}

		// 2
		private void ProcessItemPublicTransport(BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, ushort nextNodeId, bool targetDisabled, ushort nextSegmentId, ref NetSegment nextSegment, uint nextLaneId, byte offset, byte connectOffset) {
			if ((nextSegment.m_flags & this.m_disableMask) != NetSegment.Flags.None) {
				return;
			}

			NetManager netManager = Singleton<NetManager>.instance;
			if (targetDisabled && ((netManager.m_nodes.m_buffer[nextSegment.m_startNode].m_flags | netManager.m_nodes.m_buffer[nextSegment.m_endNode].m_flags) & NetNode.Flags.Disabled) == NetNode.Flags.None) {
				return;
			}

			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = prevSegment.Info;
			int nextNumLanes = nextSegmentInfo.m_lanes.Length;
			uint curLaneId = nextSegment.m_lanes;
			float prevMaxSpeed = 1f;
			float prevSpeed = 1f;
			NetInfo.LaneType prevLaneType = NetInfo.LaneType.None;
			if (item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[item.m_position.m_lane];
				prevMaxSpeed = prevLaneInfo.m_speedLimit;
				prevLaneType = prevLaneInfo.m_laneType;
				if ((prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None) {
					prevLaneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
				prevSpeed = this.CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref prevSegment, prevLaneInfo);
			}

			float prevLength = (prevLaneType != NetInfo.LaneType.PublicTransport) ? prevSegment.m_averageLength : prevLane.m_length;
			float offsetLength = (float)Mathf.Abs(connectOffset - item.m_position.m_offset) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * prevLength;
			float methodDistance = item.m_methodDistance + offsetLength;
			float comparisonValue = item.m_comparisonValue + offsetLength / (prevSpeed * this.m_maxLength);
			float duration = item.m_duration + offsetLength / prevMaxSpeed;
			Vector3 b = prevLane.CalculatePosition((float)(int)connectOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);

			if (!this.m_ignoreCost) {
				int ticketCost = prevLane.m_ticketCost;
				if (ticketCost != 0) {
					comparisonValue += (float)(ticketCost * this.m_pathRandomizer.Int32(2000u)) * TICKET_COST_CONVERSION_FACTOR;
				}
			}

			int nextLaneIndex = 0;
			while (true) {
				if (nextLaneIndex < nextNumLanes && curLaneId != 0) {
					if (nextLaneId != curLaneId) {
						curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
						nextLaneIndex++;
						continue;
					}
					break;
				}
				return;
			}

			NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];
			if (nextLaneInfo.CheckType(this.m_laneTypes, this.m_vehicleTypes)) {
				Vector3 a = netManager.m_lanes.m_buffer[nextLaneId].CalculatePosition((float)(int)offset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
				float distance = Vector3.Distance(a, b);
				BufferItem nextItem = default(BufferItem);
				nextItem.m_position.m_segment = nextSegmentId;
				nextItem.m_position.m_lane = (byte)nextLaneIndex;
				nextItem.m_position.m_offset = offset;

				if ((nextLaneInfo.m_laneType & prevLaneType) == NetInfo.LaneType.None) {
					nextItem.m_methodDistance = 0f;
				} else {
					nextItem.m_methodDistance = methodDistance + distance;
				}

				if (nextLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian && !(nextItem.m_methodDistance < 1000f) && !this.m_stablePath) {
					return;
				}

				nextItem.m_comparisonValue = comparisonValue + distance / ((prevMaxSpeed + nextLaneInfo.m_speedLimit) * 0.5f * this.m_maxLength);
				nextItem.m_duration = duration + distance / ((prevMaxSpeed + nextLaneInfo.m_speedLimit) * 0.5f);

				if ((nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
					nextItem.m_direction = NetInfo.InvertDirection(nextLaneInfo.m_finalDirection);
				} else {
					nextItem.m_direction = nextLaneInfo.m_finalDirection;
				}

				if (nextLaneId == this.m_startLaneA) {
					if (((nextItem.m_direction & NetInfo.Direction.Forward) == NetInfo.Direction.None || nextItem.m_position.m_offset < this.m_startOffsetA) ||
						((nextItem.m_direction & NetInfo.Direction.Backward) == NetInfo.Direction.None || nextItem.m_position.m_offset > this.m_startOffsetA)) {
						return;
					}

					float nextSpeed = this.CalculateLaneSpeed(this.m_startOffsetA, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo);
					float nextOffset = (float)Mathf.Abs(nextItem.m_position.m_offset - this.m_startOffsetA) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

					nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * this.m_maxLength);
					nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextSpeed;
				}

				if (nextLaneId == this.m_startLaneB) {
					if (((nextItem.m_direction & NetInfo.Direction.Forward) == NetInfo.Direction.None || nextItem.m_position.m_offset < this.m_startOffsetB) ||
						((nextItem.m_direction & NetInfo.Direction.Backward) == NetInfo.Direction.None || nextItem.m_position.m_offset > this.m_startOffsetB)) {
						return;
					}

					float nextSpeed = this.CalculateLaneSpeed(this.m_startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo);
					float nextOffset = (float)Mathf.Abs(nextItem.m_position.m_offset - this.m_startOffsetB) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

					nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * this.m_maxLength);
					nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextSpeed;
				}

				nextItem.m_laneID = nextLaneId;
				nextItem.m_lanesUsed = (item.m_lanesUsed | nextLaneInfo.m_laneType);

				this.AddBufferItem(nextItem, item.m_position);
			}
		}

		// 3
		private bool ProcessItemCosts(BufferItem item, ushort nextSegmentId, ref NetSegment prevSegment, ref NetLane prevLane, ushort nextNodeId, ref NetNode nextNode, ref NetSegment nextSegment, ref int laneIndexFromInner, byte connectOffset, bool enableVehicle, bool enablePedestrian) {
			bool blocked = false;
			if ((nextSegment.m_flags & this.m_disableMask) != NetSegment.Flags.None) {
				return blocked;
			}

			NetManager netManager = Singleton<NetManager>.instance;
			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = prevSegment.Info;
			int nextNumLanes = nextSegmentInfo.m_lanes.Length;
			uint curLaneId = nextSegment.m_lanes;
			NetInfo.Direction nextDir = (nextNodeId != nextSegment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
			NetInfo.Direction nextFinalDir = ((nextSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? nextDir : NetInfo.InvertDirection(nextDir);
			float prevMaxSpeed = 1f;
			float prevLaneSpeed = 1f;
			NetInfo.LaneType prevLaneType = NetInfo.LaneType.None;
			VehicleInfo.VehicleType prevVehicleType = VehicleInfo.VehicleType.None;
			if (item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[item.m_position.m_lane];
				prevLaneType = prevLaneInfo.m_laneType;
				prevVehicleType = prevLaneInfo.m_vehicleType;
				prevMaxSpeed = prevLaneInfo.m_speedLimit;
				prevLaneSpeed = this.CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref prevSegment, prevLaneInfo);
			}

			bool acuteTurningAngle = false;
			if (prevLaneType == NetInfo.LaneType.Vehicle && (prevVehicleType & VehicleInfo.VehicleType.Car) == VehicleInfo.VehicleType.None) {
				float turningAngle = 0.01f - Mathf.Min(nextSegmentInfo.m_maxTurnAngleCos, prevSegmentInfo.m_maxTurnAngleCos);
				if (turningAngle < 1f) {
					Vector3 vector = (nextNodeId != prevSegment.m_startNode) ? prevSegment.m_endDirection : prevSegment.m_startDirection;
					Vector3 vector2 = ((nextDir & NetInfo.Direction.Forward) == NetInfo.Direction.None) ? nextSegment.m_startDirection : nextSegment.m_endDirection;
					float dirDotProd = vector.x * vector2.x + vector.z * vector2.z;
					if (dirDotProd >= turningAngle) {
						acuteTurningAngle = true;
					}
				}
			}

			float prevLength = (prevLaneType != NetInfo.LaneType.PublicTransport) ? prevSegment.m_averageLength : prevLane.m_length;
			float offsetLength = (float)Mathf.Abs(connectOffset - item.m_position.m_offset) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * prevLength;
			float methodDistance = item.m_methodDistance + offsetLength;
			float duration = item.m_duration + offsetLength / prevMaxSpeed;

			if (!this.m_stablePath) {
				offsetLength *= (float)(new Randomizer(this.m_pathFindIndex << 16 | item.m_position.m_segment).Int32(900, 1000 + prevSegment.m_trafficDensity * 10) + this.m_pathRandomizer.Int32(20u)) * 0.001f;
			}

			if ((prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None && (prevVehicleType & this.m_vehicleTypes) == VehicleInfo.VehicleType.Car && (prevSegment.m_flags & this.m_carBanMask) != NetSegment.Flags.None) {
				offsetLength *= 7.5f;
			}

			if (this.m_transportVehicle && prevLaneType == NetInfo.LaneType.TransportVehicle) {
				offsetLength *= 0.95f;
			}

			float comparisonValue = item.m_comparisonValue + offsetLength / (prevLaneSpeed * this.m_maxLength);
			if (!this.m_ignoreCost) {
				int ticketCost = prevLane.m_ticketCost;
				if (ticketCost != 0) {
					comparisonValue += (float)(ticketCost * this.m_pathRandomizer.Int32(2000u)) * TICKET_COST_CONVERSION_FACTOR;
				}
			}

			if ((prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None) {
				prevLaneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
			}

			Vector3 b = prevLane.CalculatePosition((float)connectOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
			int newLaneIndexFromInner = laneIndexFromInner;
			bool transitionNode = (nextNode.m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;

			NetInfo.LaneType allowedLaneTypes = this.m_laneTypes;
			VehicleInfo.VehicleType allowedVehicleTypes = this.m_vehicleTypes;
			if (!enableVehicle) {
				allowedVehicleTypes &= VehicleInfo.VehicleType.Bicycle;
				if (allowedVehicleTypes == VehicleInfo.VehicleType.None) {
					allowedLaneTypes &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
			}
			if (!enablePedestrian) {
				allowedLaneTypes &= ~NetInfo.LaneType.Pedestrian;
			}

			for (int i = 0; i < nextNumLanes && curLaneId != 0; i++) {
				NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[i];
				if ((nextLaneInfo.m_finalDirection & nextFinalDir) != NetInfo.Direction.None) {
					if (nextLaneInfo.CheckType(allowedLaneTypes, allowedVehicleTypes) && (nextSegmentId != item.m_position.m_segment || i != item.m_position.m_lane) && (nextLaneInfo.m_finalDirection & nextFinalDir) != NetInfo.Direction.None) {
						if (acuteTurningAngle && nextLaneInfo.m_laneType == NetInfo.LaneType.Vehicle && (nextLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) == VehicleInfo.VehicleType.None) {
							continue;
						}

						BufferItem nextItem = default(BufferItem);

						Vector3 a = ((nextDir & NetInfo.Direction.Forward) == NetInfo.Direction.None) ? netManager.m_lanes.m_buffer[curLaneId].m_bezier.a : netManager.m_lanes.m_buffer[curLaneId].m_bezier.d;
						float transitionCost = Vector3.Distance(a, b);
						if (transitionNode) {
							transitionCost *= 2f;
						}

						float transitionCostOverMeanMaxSpeed = transitionCost / ((prevMaxSpeed + nextLaneInfo.m_speedLimit) * 0.5f * this.m_maxLength);
						nextItem.m_position.m_segment = nextSegmentId;
						nextItem.m_position.m_lane = (byte)i;
						nextItem.m_position.m_offset = (byte)(((nextDir & NetInfo.Direction.Forward) != NetInfo.Direction.None) ? 255 : 0);
						if ((nextLaneInfo.m_laneType & prevLaneType) == NetInfo.LaneType.None) {
							nextItem.m_methodDistance = 0f;
						} else {
							nextItem.m_methodDistance = methodDistance + transitionCost;
						}

						if (nextLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian && !(nextItem.m_methodDistance < 1000f) && !this.m_stablePath) {
							curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
							continue;
						}

						nextItem.m_comparisonValue = comparisonValue + transitionCostOverMeanMaxSpeed;
						nextItem.m_duration = duration + transitionCost / ((prevMaxSpeed + nextLaneInfo.m_speedLimit) * 0.5f);
						nextItem.m_direction = nextDir;

						if (curLaneId == this.m_startLaneA) {
							if (((nextItem.m_direction & NetInfo.Direction.Forward) != NetInfo.Direction.None && nextItem.m_position.m_offset >= this.m_startOffsetA) ||
								((nextItem.m_direction & NetInfo.Direction.Backward) != NetInfo.Direction.None && nextItem.m_position.m_offset <= this.m_startOffsetA)) {
								float nextLaneSpeed = this.CalculateLaneSpeed(this.m_startOffsetA, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo);
								float nextOffset = (float)Mathf.Abs(nextItem.m_position.m_offset - this.m_startOffsetA) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

								nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextLaneSpeed * this.m_maxLength);
								nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextLaneSpeed;
							}
							curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
							continue;
						}

						if (curLaneId == this.m_startLaneB) {
							if (((nextItem.m_direction & NetInfo.Direction.Forward) != NetInfo.Direction.None && nextItem.m_position.m_offset >= this.m_startOffsetB) ||
								((nextItem.m_direction & NetInfo.Direction.Backward) != NetInfo.Direction.None && nextItem.m_position.m_offset <= this.m_startOffsetB)) {
								float nextLaneSpeed = this.CalculateLaneSpeed(this.m_startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo);
								float nextOffset = (float)Mathf.Abs(nextItem.m_position.m_offset - this.m_startOffsetB) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

								nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextLaneSpeed * this.m_maxLength);
								nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextLaneSpeed;
							}
							curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
							continue;
						}

						if (!this.m_ignoreBlocked && (nextSegment.m_flags & NetSegment.Flags.Blocked) != NetSegment.Flags.None && (nextLaneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None) {
							nextItem.m_comparisonValue += 0.1f;
							blocked = true;
						}

						nextItem.m_lanesUsed = (item.m_lanesUsed | nextLaneInfo.m_laneType);
						nextItem.m_laneID = curLaneId;

						if ((nextLaneInfo.m_laneType & prevLaneType) != NetInfo.LaneType.None && (nextLaneInfo.m_vehicleType & this.m_vehicleTypes) != VehicleInfo.VehicleType.None) {
							int firstTarget = netManager.m_lanes.m_buffer[curLaneId].m_firstTarget;
							int lastTarget = netManager.m_lanes.m_buffer[curLaneId].m_lastTarget;
							if (laneIndexFromInner < firstTarget || laneIndexFromInner >= lastTarget) {
								nextItem.m_comparisonValue += Mathf.Max(1f, transitionCost * 3f - 3f) / ((prevMaxSpeed + nextLaneInfo.m_speedLimit) * 0.5f * this.m_maxLength);
							}
							if (!this.m_transportVehicle && nextLaneInfo.m_laneType == NetInfo.LaneType.TransportVehicle) {
								nextItem.m_comparisonValue += 20f / ((prevMaxSpeed + nextLaneInfo.m_speedLimit) * 0.5f * this.m_maxLength);
							}
						}

						this.AddBufferItem(nextItem, item.m_position);
					}
				} else {
					if ((nextLaneInfo.m_laneType & prevLaneType) != NetInfo.LaneType.None && (nextLaneInfo.m_vehicleType & prevVehicleType) != VehicleInfo.VehicleType.None) {
						newLaneIndexFromInner++;
					}
				}

				curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
				continue;
			}
			laneIndexFromInner = newLaneIndexFromInner;
			return blocked;
		}

		// 4
		private void ProcessItemPedBicycle(BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, ushort nextSegmentId, ref NetSegment nextSegment, ushort nextNodeId, ref NetNode nextNode, int nextLaneIndex, uint nextLaneId, ref NetLane nextLane, byte connectOffset, byte laneSwitchOffset) {
			if ((nextSegment.m_flags & this.m_disableMask) != NetSegment.Flags.None) {
				return;
			}

			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = prevSegment.Info;
			int nextNumLanes = nextSegmentInfo.m_lanes.Length;
			float distance;
			byte offset;
			if (nextSegmentId == item.m_position.m_segment) {
				Vector3 b = prevLane.CalculatePosition((float)(int)laneSwitchOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
				Vector3 a = nextLane.CalculatePosition((float)(int)connectOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
				distance = Vector3.Distance(a, b);
				offset = connectOffset;
			} else {
				NetInfo.Direction direction = (NetInfo.Direction)((nextNodeId != nextSegment.m_startNode) ? 1 : 2);
				Vector3 b = prevLane.CalculatePosition((float)(int)laneSwitchOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
				Vector3 a = ((direction & NetInfo.Direction.Forward) == NetInfo.Direction.None) ? nextLane.m_bezier.a : nextLane.m_bezier.d;
				distance = Vector3.Distance(a, b);
				offset = (byte)(((direction & NetInfo.Direction.Forward) != NetInfo.Direction.None) ? 255 : 0);
			}

			float prevMaxSpeed = 1f;
			float prevSpeed = 1f;
			NetInfo.LaneType prevLaneType = NetInfo.LaneType.None;
			if (item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[item.m_position.m_lane];
				prevMaxSpeed = prevLaneInfo.m_speedLimit;
				prevLaneType = prevLaneInfo.m_laneType;
				if ((prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None) {
					prevLaneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
				prevSpeed = this.CalculateLaneSpeed(laneSwitchOffset, item.m_position.m_offset, ref prevSegment, prevLaneInfo);
			}

			float prevLength = (prevLaneType != NetInfo.LaneType.PublicTransport) ? prevSegment.m_averageLength : prevLane.m_length;
			float offsetLength = (float)Mathf.Abs(laneSwitchOffset - item.m_position.m_offset) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * prevLength;
			float methodDistance = item.m_methodDistance + offsetLength;
			float comparisonValue = item.m_comparisonValue + offsetLength / (prevSpeed * this.m_maxLength);
			float duration = item.m_duration + offsetLength / prevMaxSpeed;

			if (!this.m_ignoreCost) {
				int ticketCost = prevLane.m_ticketCost;
				if (ticketCost != 0) {
					comparisonValue += (float)(ticketCost * this.m_pathRandomizer.Int32(2000u)) * TICKET_COST_CONVERSION_FACTOR;
				}
			}

			if (nextLaneIndex < nextNumLanes) {
				NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];
				BufferItem nextItem = default(BufferItem);
				nextItem.m_position.m_segment = nextSegmentId;
				nextItem.m_position.m_lane = (byte)nextLaneIndex;
				nextItem.m_position.m_offset = offset;

				if ((nextLaneInfo.m_laneType & prevLaneType) == NetInfo.LaneType.None) {
					nextItem.m_methodDistance = 0f;
				} else {
					if (item.m_methodDistance == 0f) {
						comparisonValue += 100f / (0.25f * this.m_maxLength);
					}
					nextItem.m_methodDistance = methodDistance + distance;
				}

				if (nextLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian && !(nextItem.m_methodDistance < 1000f) && !this.m_stablePath) {
					return;
				}

				nextItem.m_comparisonValue = comparisonValue + distance / ((prevMaxSpeed + nextLaneInfo.m_speedLimit) * 0.25f * this.m_maxLength);
				nextItem.m_duration = duration + distance / ((prevMaxSpeed + nextLaneInfo.m_speedLimit) * 0.5f);

				if ((nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
					nextItem.m_direction = NetInfo.InvertDirection(nextLaneInfo.m_finalDirection);
				} else {
					nextItem.m_direction = nextLaneInfo.m_finalDirection;
				}

				if (nextLaneId == this.m_startLaneA) {
					if (((nextItem.m_direction & NetInfo.Direction.Forward) == NetInfo.Direction.None || nextItem.m_position.m_offset < this.m_startOffsetA) ||
						((nextItem.m_direction & NetInfo.Direction.Backward) == NetInfo.Direction.None || nextItem.m_position.m_offset > this.m_startOffsetA)) {
						return;
					}
					
					float nextSpeed = this.CalculateLaneSpeed(this.m_startOffsetA, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo);
					float nextOffset = (float)Mathf.Abs(nextItem.m_position.m_offset - this.m_startOffsetA) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

					nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * this.m_maxLength);
					nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextSpeed;
				}

				if (nextLaneId == this.m_startLaneB) {
					if (((nextItem.m_direction & NetInfo.Direction.Forward) == NetInfo.Direction.None || nextItem.m_position.m_offset < this.m_startOffsetB) ||
						((nextItem.m_direction & NetInfo.Direction.Backward) == NetInfo.Direction.None || nextItem.m_position.m_offset > this.m_startOffsetB)) {
						return;
					}
					
					float nextSpeed = this.CalculateLaneSpeed(this.m_startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo);
					float nextOffset = (float)Mathf.Abs(nextItem.m_position.m_offset - this.m_startOffsetB) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

					nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * this.m_maxLength);
					nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextSpeed;
				}

				nextItem.m_laneID = nextLaneId;
				nextItem.m_lanesUsed = (item.m_lanesUsed | nextLaneInfo.m_laneType);

				this.AddBufferItem(nextItem, item.m_position);
			}
		}

		private void AddBufferItem(BufferItem item, PathUnit.Position target) {
			uint laneLocation = m_laneLocation[item.m_laneID];
			uint locPathFindIndex = laneLocation >> 16; // upper 16 bit, expected (?) path find index
			int bufferIndex = (int)(laneLocation & 65535u); // lower 16 bit
			int comparisonBufferPos;
			if (locPathFindIndex == m_pathFindIndex) {
				if (item.m_comparisonValue >= m_buffer[bufferIndex].m_comparisonValue) {
					return;
				}

				int bufferPosIndex = bufferIndex >> 6; // arithmetic shift (sign stays), upper 10 bit
				int bufferPos = bufferIndex & -64; // upper 10 bit (no shift)
				if (bufferPosIndex < m_bufferMinPos || (bufferPosIndex == m_bufferMinPos && bufferPos < m_bufferMin[bufferPosIndex])) {
					return;
				}

				comparisonBufferPos = Mathf.Max(Mathf.RoundToInt(item.m_comparisonValue * 1024f), m_bufferMinPos);
				if (comparisonBufferPos == bufferPosIndex) {
					m_buffer[bufferIndex] = item;
					m_laneTarget[item.m_laneID] = target;
					return;
				}

				int newBufferIndex = bufferPosIndex << 6 | m_bufferMax[bufferPosIndex]--;
				BufferItem bufferItem = m_buffer[newBufferIndex];
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

			bufferIndex = (comparisonBufferPos << 6 | ++m_bufferMax[comparisonBufferPos]);
			m_buffer[bufferIndex] = item;
			m_laneLocation[item.m_laneID] = (m_pathFindIndex << 16 | (uint)bufferIndex);
			m_laneTarget[item.m_laneID] = target;
		}

		private float CalculateLaneSpeed(byte startOffset, byte endOffset, ref NetSegment segment, NetInfo.Lane laneInfo) {
			NetInfo.Direction direction = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? laneInfo.m_finalDirection : NetInfo.InvertDirection(laneInfo.m_finalDirection);
			if ((direction & NetInfo.Direction.Avoid) != NetInfo.Direction.None) {
				if (endOffset > startOffset && direction == NetInfo.Direction.AvoidForward) {
					return laneInfo.m_speedLimit * 0.1f;
				}
				if (endOffset < startOffset && direction == NetInfo.Direction.AvoidBackward) {
					return laneInfo.m_speedLimit * 0.1f;
				}
				return laneInfo.m_speedLimit * 0.2f;
			}
			return laneInfo.m_speedLimit;
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
				if (Monitor.TryEnter(this.m_queueLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) {
					try {
						while (this.m_queueFirst == 0 && !this.m_terminated) {
							Monitor.Wait(this.m_queueLock);
						}
						if (!this.m_terminated) {
							this.m_calculating = this.m_queueFirst;
							this.m_queueFirst = this.m_pathUnits.m_buffer[this.m_calculating].m_nextPathUnit;
							if (this.m_queueFirst == 0) {
								this.m_queueLast = 0u;
								this.m_queuedPathFindCount = 0;
							} else {
								this.m_queuedPathFindCount--;
							}
							this.m_pathUnits.m_buffer[this.m_calculating].m_nextPathUnit = 0u;
							this.m_pathUnits.m_buffer[this.m_calculating].m_pathFindFlags = (byte)((this.m_pathUnits.m_buffer[this.m_calculating].m_pathFindFlags & -2) | 2);
							goto end_IL_001a;
						}
						return;
						end_IL_001a:;
					} finally {
						Monitor.Exit(this.m_queueLock);
					}
					try {
						this.m_pathfindProfiler.BeginStep();
						try {
							this.PathFindImplementation(this.m_calculating, ref this.m_pathUnits.m_buffer[this.m_calculating]);
						} finally {
							this.m_pathfindProfiler.EndStep();
						}
					} catch (Exception ex) {
						UIView.ForwardException(ex);
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find error: " + ex.Message + "\n" + ex.StackTrace);
						this.m_pathUnits.m_buffer[this.m_calculating].m_pathFindFlags |= 8;
					}
					while (!Monitor.TryEnter(this.m_queueLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) {
					}
					try {
						this.m_pathUnits.m_buffer[this.m_calculating].m_pathFindFlags = (byte)(this.m_pathUnits.m_buffer[this.m_calculating].m_pathFindFlags & -3);
						Singleton<PathManager>.instance.ReleasePath(this.m_calculating);
						this.m_calculating = 0u;
						Monitor.Pulse(this.m_queueLock);
					} finally {
						Monitor.Exit(this.m_queueLock);
					}
				}
			}
		}
	}
}
