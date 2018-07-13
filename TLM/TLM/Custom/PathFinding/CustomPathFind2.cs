using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using TrafficManager.Manager;
using TrafficManager.Manager.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using TrafficManager.TrafficLight;
using UnityEngine;
using static TrafficManager.Custom.PathFinding.CustomPathManager;

namespace TrafficManager.Custom.PathFinding {
	public class CustomPathFind2 : PathFind {
		private const float BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR = 0.003921569f;
		private const float TICKET_COST_CONVERSION_FACTOR = BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * 0.0001f;

#if ROUTING
		private readonly RoutingManager m_routingManager = RoutingManager.Instance;
#endif
#if JUNCTIONRESTRICTIONS
		private readonly JunctionRestrictionsManager m_junctionManager = JunctionRestrictionsManager.Instance;
#endif
#if VEHICLERESTRICTIONS
		private readonly VehicleRestrictionsManager m_vehicleRestrictionsManager = VehicleRestrictionsManager.Instance;
#endif
#if SPEEDLIMITS
		private readonly SpeedLimitManager m_speedLimitManager = SpeedLimitManager.Instance;
#endif
#if CUSTOMTRAFFICLIGHTS
		private readonly CustomSegmentLightsManager m_customTrafficLightsManager = CustomSegmentLightsManager.Instance;
#endif
#if ADVANCEDAI && ROUTING
		private readonly TrafficMeasurementManager m_trafficMeasurementManager = TrafficMeasurementManager.Instance;
#endif
		private GlobalConfig m_conf = null;

		private struct BufferItem {
			public PathUnit.Position m_position;
			public float m_comparisonValue;
			public float m_methodDistance;
			public float m_duration;
			public uint m_laneID;
			public NetInfo.Direction m_direction;
			public NetInfo.LaneType m_lanesUsed;
#if PARKINGAI
			public VehicleInfo.VehicleType m_vehiclesUsed;
#endif
#if ADVANCEDAI && ROUTING
			public float m_trafficRand;
#endif
		}

		private enum LaneChangingCostCalculationMode {
			None,
			ByLaneDistance,
			ByGivenDistance
		}

		// private stock fields
		FieldInfo pathUnitsField;
		FieldInfo queueFirstField;
		FieldInfo queueLastField;
		FieldInfo queueLockField;
		FieldInfo calculatingField;
		FieldInfo terminatedField;
		FieldInfo pathFindThreadField;

		private Array32<PathUnit> m_pathUnits {
			get { return pathUnitsField.GetValue(this) as Array32<PathUnit>; }
			set { pathUnitsField.SetValue(this, value); }
		}

		private uint m_queueFirst {
			get { return (uint)queueFirstField.GetValue(this); }
			set { queueFirstField.SetValue(this, value); }
		}

		private uint m_queueLast {
			get { return (uint)queueLastField.GetValue(this); }
			set { queueLastField.SetValue(this, value); }
		}

		private uint m_calculating {
			get { return (uint)calculatingField.GetValue(this); }
			set { calculatingField.SetValue(this, value); }
		}

		private object m_queueLock {
			get { return queueLockField.GetValue(this); }
			set { queueLockField.SetValue(this, value); }
		}

		private Thread m_customPathFindThread {
			get { return (Thread)pathFindThreadField.GetValue(this); }
			set { pathFindThreadField.SetValue(this, value); }
		}

		private bool m_terminated {
			get { return (bool)terminatedField.GetValue(this); }
			set { terminatedField.SetValue(this, value); }
		}

		// stock fields
		public ThreadProfiler m_pathfindProfiler;
		private object m_bufferLock;
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
		private uint m_endLaneA;
		private uint m_endLaneB;
		private uint m_vehicleLane;
		private byte m_startOffsetA;
		private byte m_startOffsetB;
		private byte m_vehicleOffset;
		private NetSegment.Flags m_carBanMask;
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

		// custom fields
		private PathUnitQueueItem m_queueItem;
		private bool m_isHeavyVehicle;
#if DEBUG
		public uint m_failedPathFinds = 0;
		public uint m_succeededPathFinds = 0;
#endif
#if PARKINGAI
		private ushort m_startSegmentA;
		private ushort m_startSegmentB;
#endif
#if ROUTING
		private bool m_isRoadVehicle;
		private bool m_isLaneArrowObeyingEntity;
		//private bool m_isLaneConnectionObeyingEntity;
#endif

		private void Awake() {
			Type stockPathFindType = typeof(PathFind);
			const BindingFlags fieldFlags = BindingFlags.NonPublic | BindingFlags.Instance;

			pathUnitsField = stockPathFindType.GetField("m_pathUnits", fieldFlags);
			queueFirstField = stockPathFindType.GetField("m_queueFirst", fieldFlags);
			queueLastField = stockPathFindType.GetField("m_queueLast", fieldFlags);
			queueLockField = stockPathFindType.GetField("m_queueLock", fieldFlags);
			terminatedField = stockPathFindType.GetField("m_terminated", fieldFlags);
			calculatingField = stockPathFindType.GetField("m_calculating", fieldFlags);
			pathFindThreadField = stockPathFindType.GetField("m_pathFindThread", fieldFlags);

			m_pathfindProfiler = new ThreadProfiler();
			m_laneLocation = new uint[262144];
			m_laneTarget = new PathUnit.Position[262144];
			m_buffer = new BufferItem[65536];
			m_bufferMin = new int[1024];
			m_bufferMax = new int[1024];
			m_queueLock = new object();
			m_bufferLock = Singleton<PathManager>.instance.m_bufferLock;
			m_pathUnits = Singleton<PathManager>.instance.m_pathUnits;
			m_customPathFindThread = new Thread(PathFindThread);
			m_customPathFindThread.Name = "Pathfind";
			m_customPathFindThread.Priority = SimulationManager.SIMULATION_PRIORITY;
			m_customPathFindThread.Start();

			if (!m_customPathFindThread.IsAlive) {
				CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find thread failed to start!");
			}
		}

		private void OnDestroy() {
			try {
				Monitor.Enter(m_queueLock);
				m_terminated = true;
				Monitor.PulseAll(m_queueLock);
			} finally {
				Monitor.Exit(m_queueLock);
			}
		}

		public new bool CalculatePath(uint unit, bool skipQueue) {
			return ExtCalculatePath(unit, skipQueue);
		}

		public bool ExtCalculatePath(uint unit, bool skipQueue) {
			if (CustomPathManager._instance.AddPathReference(unit)) {
				try {
					Monitor.Enter(m_queueLock);

					if (skipQueue) {
						if (m_queueLast == 0) {
							m_queueLast = unit;
						} else {
							// NON-STOCK CODE START
							CustomPathManager._instance.queueItems[unit].nextPathUnitId = m_queueFirst;
							// NON-STOCK CODE END
							// PathUnits.m_buffer[unit].m_nextPathUnit = QueueFirst; // stock code commented
						}
						m_queueFirst = unit;
					} else {
						if (m_queueLast == 0) {
							m_queueFirst = unit;
						} else {
							// NON-STOCK CODE START
							CustomPathManager._instance.queueItems[m_queueLast].nextPathUnitId = unit;
							// NON-STOCK CODE END
							// PathUnits.m_buffer[QueueLast].m_nextPathUnit = unit; // stock code commented
						}
						m_queueLast = unit;
					}

					m_pathUnits.m_buffer[unit].m_pathFindFlags |= 1;
					m_queuedPathFindCount++;

					Monitor.Pulse(m_queueLock);
				} finally {
					Monitor.Exit(m_queueLock);
				}
				return true;
			}
			return false;
		}

		private void PathFindImplementation(uint unit, ref PathUnit data) {
			m_conf = GlobalConfig.Instance; // NON-STOCK CODE

			NetManager netManager = Singleton<NetManager>.instance;

			m_laneTypes = (NetInfo.LaneType)m_pathUnits.m_buffer[unit].m_laneTypes;
			m_vehicleTypes = (VehicleInfo.VehicleType)m_pathUnits.m_buffer[unit].m_vehicleTypes;
			m_maxLength = m_pathUnits.m_buffer[unit].m_length;
			m_pathFindIndex = (m_pathFindIndex + 1 & 0x7FFF);
			m_pathRandomizer = new Randomizer(unit);
			m_carBanMask = NetSegment.Flags.CarBan;

			m_isHeavyVehicle = (m_pathUnits.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_IS_HEAVY) != 0; // NON-STOCK CODE (refactored)
			if (m_isHeavyVehicle) {
				m_carBanMask |= NetSegment.Flags.HeavyBan;
			}

			if ((m_pathUnits.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_READY) != 0) {
				m_carBanMask |= NetSegment.Flags.WaitingPath;
			}

			m_ignoreBlocked = ((m_pathUnits.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_IGNORE_BLOCKED) != 0);
			m_stablePath = ((m_pathUnits.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_STABLE_PATH) != 0);
			m_randomParking = ((m_pathUnits.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_RANDOM_PARKING) != 0);
			m_transportVehicle = ((m_laneTypes & NetInfo.LaneType.TransportVehicle) != NetInfo.LaneType.None);
			m_ignoreCost = (m_stablePath || (m_pathUnits.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_IGNORE_COST) != 0);
			m_disableMask = (NetSegment.Flags.Collapsed | NetSegment.Flags.PathFailed);

			if ((m_pathUnits.m_buffer[unit].m_simulationFlags & PathUnit.FLAG_IGNORE_FLOODED) == 0) {
				m_disableMask |= NetSegment.Flags.Flooded;
			}

			if ((m_laneTypes & NetInfo.LaneType.Vehicle) != NetInfo.LaneType.None) {
				m_laneTypes |= NetInfo.LaneType.TransportVehicle;
			}

#if ROUTING
			m_isRoadVehicle =
				(m_queueItem.vehicleType & ExtVehicleType.RoadVehicle) != ExtVehicleType.None;

			m_isLaneArrowObeyingEntity =
#if DEBUG
				! Options.allRelaxed && // debug option: all vehicle may ignore lane arrows
#endif
				(! Options.relaxedBusses || m_queueItem.vehicleType != ExtVehicleType.Bus) && // option: busses may ignore lane arrows
				(m_vehicleTypes & LaneArrowManager.VEHICLE_TYPES) != VehicleInfo.VehicleType.None &&
				(m_queueItem.vehicleType & LaneArrowManager.EXT_VEHICLE_TYPES) != ExtVehicleType.None;
			 
			//m_isLaneConnectionObeyingEntity =
			//	(m_vehicleTypes & LaneConnectionManager.VEHICLE_TYPES) != VehicleInfo.VehicleType.None &&
			//	(m_queueItem.vehicleType & LaneConnectionManager.EXT_VEHICLE_TYPES) != ExtVehicleType.None;
#endif

			int posCount = m_pathUnits.m_buffer[unit].m_positionCount & 0xF;
			int vehiclePosIndicator = m_pathUnits.m_buffer[unit].m_positionCount >> 4;
			BufferItem bufferItemStartA = default(BufferItem);
			if (data.m_position00.m_segment != 0 && posCount >= 1) {
#if PARKINGAI
				m_startSegmentA = data.m_position00.m_segment; // NON-STOCK CODE
#endif
				m_startLaneA = PathManager.GetLaneID(data.m_position00);
				m_startOffsetA = data.m_position00.m_offset;
				bufferItemStartA.m_laneID = m_startLaneA;
				bufferItemStartA.m_position = data.m_position00;
				GetLaneDirection(data.m_position00, out bufferItemStartA.m_direction, out bufferItemStartA.m_lanesUsed
#if PARKINGAI
					, out bufferItemStartA.m_vehiclesUsed
#endif
				);
				bufferItemStartA.m_comparisonValue = 0f;
				bufferItemStartA.m_duration = 0f;
			} else {
#if PARKINGAI
				m_startSegmentA = 0; // NON-STOCK CODE
#endif
				m_startLaneA = 0u;
				m_startOffsetA = 0;
			}

			BufferItem bufferItemStartB = default(BufferItem);
			if (data.m_position02.m_segment != 0 && posCount >= 3) {
#if PARKINGAI
				m_startSegmentB = data.m_position02.m_segment; // NON-STOCK CODE
#endif
				m_startLaneB = PathManager.GetLaneID(data.m_position02);
				m_startOffsetB = data.m_position02.m_offset;
				bufferItemStartB.m_laneID = m_startLaneB;
				bufferItemStartB.m_position = data.m_position02;
				GetLaneDirection(data.m_position02, out bufferItemStartB.m_direction, out bufferItemStartB.m_lanesUsed
#if PARKINGAI
					, out bufferItemStartB.m_vehiclesUsed
#endif
				);
				bufferItemStartB.m_comparisonValue = 0f;
				bufferItemStartB.m_duration = 0f;
			} else {
#if PARKINGAI
				m_startSegmentB = 0; // NON-STOCK CODE
#endif
				m_startLaneB = 0u;
				m_startOffsetB = 0;
			}

			BufferItem bufferItemEndA = default(BufferItem);
			if (data.m_position01.m_segment != 0 && posCount >= 2) {
				m_endLaneA = PathManager.GetLaneID(data.m_position01);
				bufferItemEndA.m_laneID = m_endLaneA;
				bufferItemEndA.m_position = data.m_position01;
				GetLaneDirection(data.m_position01, out bufferItemEndA.m_direction, out bufferItemEndA.m_lanesUsed
#if PARKINGAI
					, out bufferItemEndA.m_vehiclesUsed
#endif
				);
				bufferItemEndA.m_methodDistance = 0.01f;
				bufferItemEndA.m_comparisonValue = 0f;
				bufferItemEndA.m_duration = 0f;
			} else {
				m_endLaneA = 0u;
			}

			BufferItem bufferItemEndB = default(BufferItem);
			if (data.m_position03.m_segment != 0 && posCount >= 4) {
				m_endLaneB = PathManager.GetLaneID(data.m_position03);
				bufferItemEndB.m_laneID = m_endLaneB;
				bufferItemEndB.m_position = data.m_position03;
				GetLaneDirection(data.m_position03, out bufferItemEndB.m_direction, out bufferItemEndB.m_lanesUsed
#if PARKINGAI
					, out bufferItemEndB.m_vehiclesUsed
#endif
				);
				bufferItemEndB.m_methodDistance = 0.01f;
				bufferItemEndB.m_comparisonValue = 0f;
				bufferItemEndB.m_duration = 0f;
			} else {
				m_endLaneB = 0u;
			}

			if (data.m_position11.m_segment != 0 && vehiclePosIndicator >= 1) {
				m_vehicleLane = PathManager.GetLaneID(data.m_position11);
				m_vehicleOffset = data.m_position11.m_offset;
			} else {
				m_vehicleLane = 0u;
				m_vehicleOffset = 0;
			}

			BufferItem finalBufferItem = default(BufferItem);
			byte startOffset = 0;
			m_bufferMinPos = 0;
			m_bufferMaxPos = -1;

			if (m_pathFindIndex == 0) {
				uint num3 = 4294901760u;
				for (int i = 0; i < 262144; i++) {
					m_laneLocation[i] = num3;
				}
			}

			for (int j = 0; j < 1024; j++) {
				m_bufferMin[j] = 0;
				m_bufferMax[j] = -1;
			}

			if (bufferItemEndA.m_position.m_segment != 0) {
				m_bufferMax[0]++;
				m_buffer[++m_bufferMaxPos] = bufferItemEndA;
			}

			if (bufferItemEndB.m_position.m_segment != 0) {
				m_bufferMax[0]++;
				m_buffer[++m_bufferMaxPos] = bufferItemEndB;
			}

			bool canFindPath = false;
			while (m_bufferMinPos <= m_bufferMaxPos) {
				int bufMin = m_bufferMin[m_bufferMinPos];
				int bufMax = m_bufferMax[m_bufferMinPos];

				if (bufMin > bufMax) {
					m_bufferMinPos++;
				} else {
					m_bufferMin[m_bufferMinPos] = bufMin + 1;
					BufferItem candidateItem = m_buffer[(m_bufferMinPos << 6) + bufMin];
					if (candidateItem.m_position.m_segment == bufferItemStartA.m_position.m_segment && candidateItem.m_position.m_lane == bufferItemStartA.m_position.m_lane) {
						if ((candidateItem.m_direction & NetInfo.Direction.Forward) != NetInfo.Direction.None && candidateItem.m_position.m_offset >= m_startOffsetA) {
							finalBufferItem = candidateItem;
							startOffset = m_startOffsetA;
							canFindPath = true;
							break;
						}

						if ((candidateItem.m_direction & NetInfo.Direction.Backward) != NetInfo.Direction.None && candidateItem.m_position.m_offset <= m_startOffsetA) {
							finalBufferItem = candidateItem;
							startOffset = m_startOffsetA;
							canFindPath = true;
							break;
						}
					}

					if (candidateItem.m_position.m_segment == bufferItemStartB.m_position.m_segment && candidateItem.m_position.m_lane == bufferItemStartB.m_position.m_lane) {
						if ((candidateItem.m_direction & NetInfo.Direction.Forward) != NetInfo.Direction.None && candidateItem.m_position.m_offset >= m_startOffsetB) {
							finalBufferItem = candidateItem;
							startOffset = m_startOffsetB;
							canFindPath = true;
							break;
						}

						if ((candidateItem.m_direction & NetInfo.Direction.Backward) != NetInfo.Direction.None && candidateItem.m_position.m_offset <= m_startOffsetB) {
							finalBufferItem = candidateItem;
							startOffset = m_startOffsetB;
							canFindPath = true;
							break;
						}
					}

					ushort startNodeId = netManager.m_segments.m_buffer[candidateItem.m_position.m_segment].m_startNode;
					ushort endNodeId = netManager.m_segments.m_buffer[candidateItem.m_position.m_segment].m_endNode;

					if ((candidateItem.m_direction & NetInfo.Direction.Forward) != NetInfo.Direction.None) {
						ProcessItemMain(candidateItem, ref netManager.m_segments.m_buffer[candidateItem.m_position.m_segment], ref netManager.m_lanes.m_buffer[candidateItem.m_laneID], startNodeId, ref netManager.m_nodes.m_buffer[startNodeId], 0, false);
					}

					if ((candidateItem.m_direction & NetInfo.Direction.Backward) != NetInfo.Direction.None) {
						ProcessItemMain(candidateItem, ref netManager.m_segments.m_buffer[candidateItem.m_position.m_segment], ref netManager.m_lanes.m_buffer[candidateItem.m_laneID], endNodeId, ref netManager.m_nodes.m_buffer[endNodeId], 255, false);
					}

					int numIter = 0;
					ushort specialNodeId = netManager.m_lanes.m_buffer[candidateItem.m_laneID].m_nodes;
					if (specialNodeId != 0) {
						bool nodesDisabled = ((netManager.m_nodes.m_buffer[startNodeId].m_flags | netManager.m_nodes.m_buffer[endNodeId].m_flags) & NetNode.Flags.Disabled) != NetNode.Flags.None;

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
								ProcessItemMain(candidateItem, ref netManager.m_segments.m_buffer[candidateItem.m_position.m_segment], ref netManager.m_lanes.m_buffer[candidateItem.m_laneID], specialNodeId, ref netManager.m_nodes.m_buffer[specialNodeId], laneOffset, true);
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
				m_pathUnits.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;
				// NON-STOCK CODE START
#if DEBUG
				++m_failedPathFinds;
#endif
				// NON-STOCK CODE END
			} else {
				float duration = (m_laneTypes != NetInfo.LaneType.Pedestrian && (m_laneTypes & NetInfo.LaneType.Pedestrian) != NetInfo.LaneType.None) ? finalBufferItem.m_duration : finalBufferItem.m_methodDistance;
				m_pathUnits.m_buffer[unit].m_length = duration;
				m_pathUnits.m_buffer[unit].m_speed = (byte)Mathf.Clamp(finalBufferItem.m_methodDistance * 100f / Mathf.Max(0.01f, finalBufferItem.m_duration), 0f, 255f);
#if PARKINGAI
				m_pathUnits.m_buffer[unit].m_laneTypes = (byte)finalBufferItem.m_lanesUsed;
				m_pathUnits.m_buffer[unit].m_vehicleTypes = (ushort)finalBufferItem.m_vehiclesUsed;
#endif

				uint currentPathUnitId = unit;
				int currentItemPositionCount = 0;
				int sumOfPositionCounts = 0;
				PathUnit.Position currentPosition = finalBufferItem.m_position;

				if ((currentPosition.m_segment != bufferItemEndA.m_position.m_segment || currentPosition.m_lane != bufferItemEndA.m_position.m_lane || currentPosition.m_offset != bufferItemEndA.m_position.m_offset) &&
					(currentPosition.m_segment != bufferItemEndB.m_position.m_segment || currentPosition.m_lane != bufferItemEndB.m_position.m_lane || currentPosition.m_offset != bufferItemEndB.m_position.m_offset)) {
					if (startOffset != currentPosition.m_offset) {
						PathUnit.Position position2 = currentPosition;
						position2.m_offset = startOffset;
						m_pathUnits.m_buffer[currentPathUnitId].SetPosition(currentItemPositionCount++, position2);
					}

					m_pathUnits.m_buffer[currentPathUnitId].SetPosition(currentItemPositionCount++, currentPosition);
					currentPosition = m_laneTarget[finalBufferItem.m_laneID];
				}

				for (int k = 0; k < 262144; k++) {
					m_pathUnits.m_buffer[currentPathUnitId].SetPosition(currentItemPositionCount++, currentPosition);

					if ((currentPosition.m_segment == bufferItemEndA.m_position.m_segment && currentPosition.m_lane == bufferItemEndA.m_position.m_lane && currentPosition.m_offset == bufferItemEndA.m_position.m_offset) ||
					(currentPosition.m_segment == bufferItemEndB.m_position.m_segment && currentPosition.m_lane == bufferItemEndB.m_position.m_lane && currentPosition.m_offset == bufferItemEndB.m_position.m_offset)) {
						m_pathUnits.m_buffer[currentPathUnitId].m_positionCount = (byte)currentItemPositionCount;
						sumOfPositionCounts += currentItemPositionCount;
						if (sumOfPositionCounts != 0) {
							currentPathUnitId = m_pathUnits.m_buffer[unit].m_nextPathUnit;
							currentItemPositionCount = m_pathUnits.m_buffer[unit].m_positionCount;
							int numIter = 0;
							while (currentPathUnitId != 0) {
								m_pathUnits.m_buffer[currentPathUnitId].m_length = duration * (float)(sumOfPositionCounts - currentItemPositionCount) / (float)sumOfPositionCounts;
								currentItemPositionCount += m_pathUnits.m_buffer[currentPathUnitId].m_positionCount;
								currentPathUnitId = m_pathUnits.m_buffer[currentPathUnitId].m_nextPathUnit;
								if (++numIter >= 262144) {
									CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
									break;
								}
							}
						}
						m_pathUnits.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_READY;
						// NON-STOCK CODE START
#if DEBUG
						++m_succeededPathFinds;
#endif
						// NON-STOCK CODE END
						return;
					}
					
					if (currentItemPositionCount == 12) {
						uint createdPathUnitId;
						try {
							Monitor.Enter(m_bufferLock);

							if (!m_pathUnits.CreateItem(out createdPathUnitId, ref m_pathRandomizer)) {
								m_pathUnits.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;
								// NON-STOCK CODE START
#if DEBUG
								++m_failedPathFinds;
#endif
								// NON-STOCK CODE END
								return;
							}

							m_pathUnits.m_buffer[createdPathUnitId] = m_pathUnits.m_buffer[currentPathUnitId];
							m_pathUnits.m_buffer[createdPathUnitId].m_referenceCount = 1;
							m_pathUnits.m_buffer[createdPathUnitId].m_pathFindFlags = PathUnit.FLAG_READY;
							m_pathUnits.m_buffer[currentPathUnitId].m_nextPathUnit = createdPathUnitId;
							m_pathUnits.m_buffer[currentPathUnitId].m_positionCount = (byte)currentItemPositionCount;
#if PARKINGAI
							m_pathUnits.m_buffer[currentPathUnitId].m_laneTypes = (byte)finalBufferItem.m_lanesUsed; // (this is not accurate!)
							m_pathUnits.m_buffer[currentPathUnitId].m_vehicleTypes = (ushort)finalBufferItem.m_vehiclesUsed; // (this is not accurate!)
#endif
							sumOfPositionCounts += currentItemPositionCount;
							Singleton<PathManager>.instance.m_pathUnitCount = (int)(m_pathUnits.ItemCount() - 1);
						} finally {
							Monitor.Exit(m_bufferLock);
						}

						currentPathUnitId = createdPathUnitId;
						currentItemPositionCount = 0;
					}

					uint laneID = PathManager.GetLaneID(currentPosition);
					currentPosition = m_laneTarget[laneID];
				}

				m_pathUnits.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_FAILED;
				// NON-STOCK CODE START
#if DEBUG
				++m_failedPathFinds;
#endif
				// NON-STOCK CODE END
			}
		}

		// 1
		private void ProcessItemMain(BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, ushort nextNodeId, ref NetNode nextNode, byte connectOffset, bool isMiddle) {
			NetManager netManager = Singleton<NetManager>.instance;

			ushort prevSegmentId = item.m_position.m_segment;
			byte prevLaneIndex = item.m_position.m_lane;

			bool prevIsPedestrianLane = false;
			bool prevIsBicycleLane = false;
			bool prevIsCenterPlatform = false;
			bool prevIsElevated = false;
#if ADVANCEDAI && ROUTING
			// NON-STOCK CODE START
			bool prevIsCarLane = false;
			// NON-STOCK CODE END
#endif
			int prevRelSimilarLaneIndex = 0;
			// NON-STOCK CODE START
			float prevMaxSpeed = 1f;
			float prevLaneSpeed = 1f;
			// NON-STOCK CODE END

			NetInfo prevSegmentInfo = prevSegment.Info;
			if (prevLaneIndex < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[item.m_position.m_lane];
				prevIsPedestrianLane = (prevLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian);
				prevIsBicycleLane = (prevLaneInfo.m_laneType == NetInfo.LaneType.Vehicle && (prevLaneInfo.m_vehicleType & m_vehicleTypes) == VehicleInfo.VehicleType.Bicycle);
				prevIsCenterPlatform = prevLaneInfo.m_centerPlatform;
				prevIsElevated = prevLaneInfo.m_elevated;

#if ADVANCEDAI && ROUTING
				// NON-STOCK CODE START
				if (Options.advancedAI) {
					prevIsCarLane =
						(prevLaneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
						(prevLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None
					;
				}
				// NON-STOCK CODE END
#endif

				// NON-STOCK CODE START
#if SPEEDLIMITS
				prevMaxSpeed = m_speedLimitManager.GetLockFreeGameSpeedLimit(prevSegmentId, prevLaneIndex, item.m_laneID, prevLaneInfo);
#else
				prevMaxSpeed = prevLaneInfo.m_speedLimit;
#endif
				prevLaneSpeed = CalculateLaneSpeed(prevMaxSpeed, connectOffset, item.m_position.m_offset, ref prevSegment, prevLaneInfo);
				// NON-STOCK CODE END

				prevRelSimilarLaneIndex = (((prevLaneInfo.m_finalDirection & NetInfo.Direction.Forward) == NetInfo.Direction.None) ? (prevLaneInfo.m_similarLaneCount - prevLaneInfo.m_similarLaneIndex - 1) : prevLaneInfo.m_similarLaneIndex);
			}

			if (isMiddle) {
				for (int i = 0; i < 8; i++) {
					ushort nextSegmentId = nextNode.GetSegment(i);
					if (nextSegmentId != 0) {
						ProcessItemCosts(item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextNodeId, ref nextNode, true, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, !prevIsPedestrianLane, prevIsPedestrianLane);
					}
				}
			} else if (prevIsPedestrianLane) {
				// gwe are going to a pedestrian lane
				if (!prevIsElevated) {
					if (nextNode.Info.m_class.m_service != ItemClass.Service.Beautification) {
						bool canCrossStreet = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None;
						bool isOnCenterPlatform = prevIsCenterPlatform && (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Junction)) == NetNode.Flags.None;
						ushort nextLeftSegmentId = prevSegmentId;
						ushort nextRightSegmentId = prevSegmentId;
						int leftLaneIndex;
						int rightLaneIndex;
						uint leftLaneId;
						uint rightLaneId;

						prevSegment.GetLeftAndRightLanes(nextNodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, prevLaneIndex, isOnCenterPlatform, out leftLaneIndex, out rightLaneIndex, out leftLaneId, out rightLaneId);

						if (leftLaneId == 0 || rightLaneId == 0) {
							ushort leftSegmentId;
							ushort rightSegmentId;
							prevSegment.GetLeftAndRightSegments(nextNodeId, out leftSegmentId, out rightSegmentId);

							int numIter = 0;
							while (leftSegmentId != 0 && leftSegmentId != prevSegmentId && leftLaneId == 0) {
								int someLeftLaneIndex;
								int someRightLaneIndex;
								uint someLeftLaneId;
								uint someRightLaneId;
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
								int someLeftLaneIndex;
								int someRightLaneIndex;
								uint someLeftLaneId;
								uint someRightLaneId;
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
							ProcessItemPedBicycle(item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextLeftSegmentId, ref netManager.m_segments.m_buffer[nextLeftSegmentId], nextNodeId, ref nextNode, leftLaneIndex, leftLaneId, ref netManager.m_lanes.m_buffer[leftLaneId], connectOffset, connectOffset);
						}

						if (rightLaneId != 0 && rightLaneId != leftLaneId && (nextRightSegmentId != prevSegmentId || canCrossStreet || isOnCenterPlatform)) {
							ProcessItemPedBicycle(item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextRightSegmentId, ref netManager.m_segments.m_buffer[nextRightSegmentId], nextNodeId, ref nextNode, rightLaneIndex, rightLaneId, ref netManager.m_lanes.m_buffer[rightLaneId], connectOffset, connectOffset);
						}

						int nextLaneIndex;
						uint nextLaneId;
						if ((m_vehicleTypes & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None && prevSegment.GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Bicycle, out nextLaneIndex, out nextLaneId)) {
							ProcessItemPedBicycle(item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, prevSegmentId, ref prevSegment, nextNodeId, ref nextNode, nextLaneIndex, nextLaneId, ref netManager.m_lanes.m_buffer[nextLaneId], connectOffset, connectOffset);
						}
					} else {
						// we are going from pedestrian lane to a beautification node
						for (int j = 0; j < 8; j++) {
							ushort nextSegmentId = nextNode.GetSegment(j);
							if (nextSegmentId != 0 && nextSegmentId != prevSegmentId) {
								ProcessItemCosts(item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextNodeId, ref nextNode, false, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, false, true);
							}
						}
					}

					// prepare switching from a vehicle to pedestrian lane
					NetInfo.LaneType nextLaneType = m_laneTypes & ~NetInfo.LaneType.Pedestrian;
					VehicleInfo.VehicleType nextVehicleType = m_vehicleTypes & ~VehicleInfo.VehicleType.Bicycle;
					if ((item.m_lanesUsed & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None) {
						nextLaneType &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
					}

					// NON-STOCK CODE START
					bool parkingAllowed = true;

#if PARKINGAI
					// Parking AI: Determine if parking is allowed
					if (Options.prohibitPocketCars) {
						if (m_queueItem.vehicleType == ExtVehicleType.PassengerCar &&
							(nextVehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None &&
							((nextLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None)) {
							if ((item.m_lanesUsed & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None) {
								/* if pocket cars are prohibited, a citizen may only park their car once per path */
								parkingAllowed = false;
							} else if ((item.m_lanesUsed & NetInfo.LaneType.PublicTransport) == NetInfo.LaneType.None) {
								/* if the citizen is walking to their target (= no public transport used), the passenger car must be parked in the very last moment */
								parkingAllowed = item.m_laneID == m_endLaneA || item.m_laneID == m_endLaneB;
							}
						}
					}
#endif
					// NON-STOCK CODE END

					int sameSegLaneIndex;
					uint sameSegLaneId;
					if (parkingAllowed && // NON-STOCK CODE
						nextLaneType != NetInfo.LaneType.None && nextVehicleType != VehicleInfo.VehicleType.None && prevSegment.GetClosestLane(prevLaneIndex, nextLaneType, nextVehicleType, out sameSegLaneIndex, out sameSegLaneId)) {
						NetInfo.Lane sameSegLaneInfo = prevSegmentInfo.m_lanes[sameSegLaneIndex];
						byte sameSegConnectOffset = (byte)(((prevSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None == ((sameSegLaneInfo.m_finalDirection & NetInfo.Direction.Backward) != NetInfo.Direction.None)) ? 1 : 254);
						BufferItem nextItem = item;
						if (m_randomParking) {
							nextItem.m_comparisonValue += (float)m_pathRandomizer.Int32(300u) / m_maxLength;
						}
						ProcessItemPedBicycle(nextItem, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, prevSegmentId, ref prevSegment, nextNodeId, ref nextNode, sameSegLaneIndex, sameSegLaneId, ref netManager.m_lanes.m_buffer[sameSegLaneId], sameSegConnectOffset, 128);
					}
				}
			} else {
				// We are going to a non-pedestrian lane

				bool nextIsBeautificationNode = nextNode.Info.m_class.m_service == ItemClass.Service.Beautification; // NON-STOCK CODE (refactored)
				bool allowPedestrian = (m_laneTypes & NetInfo.LaneType.Pedestrian) != NetInfo.LaneType.None; // allow switching from pedestrian lane to a non-pedestrian lane?
				bool allowBicycle = false; // allow switching from a pedestrian lane to a bike lane?
				byte switchConnectOffset = 0; // lane switching offset
				if (allowPedestrian) {
					if (prevIsBicycleLane) {
						// we are going to a bicycle lane
						switchConnectOffset = connectOffset;
						allowBicycle = nextIsBeautificationNode;
					} else if (m_vehicleLane != 0) {
						// there is a parked vehicle position
						if (m_vehicleLane != item.m_laneID) {
							// we have not reached the parked vehicle yet
							allowPedestrian = false;
						} else {
							// pedestrian switches to parked vehicle
							switchConnectOffset = m_vehicleOffset;
						}
					} else if (m_stablePath) {
						// enter a bus
						switchConnectOffset = 128;
					} else {
						// pocket car spawning
#if PARKINGAI
						if (Options.prohibitPocketCars &&
								m_queueItem.vehicleType == ExtVehicleType.PassengerCar &&
								(m_queueItem.pathType == ExtCitizenInstance.ExtPathType.WalkingOnly ||
								(m_queueItem.pathType == ExtCitizenInstance.ExtPathType.DrivingOnly && item.m_position.m_segment != m_startSegmentA && item.m_position.m_segment != m_startSegmentB))) {
							/* disallow pocket cars on walking paths, allow only if a driving path is required and we reached the start segment */
							allowPedestrian = false;
						} else {
#endif
							switchConnectOffset = (byte)m_pathRandomizer.UInt32(1u, 254u);
#if PARKINGAI
						}
#endif
					}
				}

				ushort nextSegmentId = 0;
				if ((m_vehicleTypes & (VehicleInfo.VehicleType.Ferry
#if !ROUTING
					| VehicleInfo.VehicleType.Monorail
#endif
					)) != VehicleInfo.VehicleType.None) {
					// ferry (/ monorail)

					bool isUturnAllowedHere = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None;
					for (int k = 0; k < 8; k++) {
						nextSegmentId = nextNode.GetSegment(k);
						if (nextSegmentId != 0 && nextSegmentId != prevSegmentId) {
							ProcessItemCosts(item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextNodeId, ref nextNode, false, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, true, allowBicycle);
						}
					}

					if (isUturnAllowedHere
#if !ROUTING
						&& (m_vehicleTypes & VehicleInfo.VehicleType.Monorail) == VehicleInfo.VehicleType.None
#endif
						) {
						nextSegmentId = prevSegmentId;
						ProcessItemCosts(item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextNodeId, ref nextNode, false, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, true, false);
					}
				} else {
					// road vehicles / trams / trains / metros (/ monorails) / etc.

#if ROUTING
					bool exploreUturn = false;
#else
					bool exploreUturn = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
#endif

#if ROUTING
					bool prevIsRouted = false;
					uint laneRoutingIndex = 0;
					bool nextIsStartNode = nextNodeId == prevSegment.m_startNode;
					if (nextIsStartNode || nextNodeId == prevSegment.m_endNode) {
						laneRoutingIndex = m_routingManager.GetLaneEndRoutingIndex(item.m_laneID, nextIsStartNode);
						prevIsRouted = m_routingManager.laneEndBackwardRoutings[laneRoutingIndex].routed;
					}

					if (allowBicycle || !prevIsRouted) {
						/*
						* pedestrian to bicycle lane switch or no routing information available:
						*		if pedestrian lanes should be explored (allowBicycle == true): do this here
						*		if previous segment has custom routing (prevIsRouted == true): do NOT explore vehicle lanes here, else: vanilla exploration of vehicle lanes
						*/
						// NON-STOCK CODE END
#endif
						nextSegmentId = prevSegment.GetRightSegment(nextNodeId);
						for (int l = 0; l < 8; l++) {
							if (nextSegmentId == 0) {
								break;
							}

							if (nextSegmentId == prevSegmentId) {
								break;
							}

							if (ProcessItemCosts(item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextNodeId, ref nextNode, false, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset,
#if ROUTING
								!prevIsRouted // NON-STOCK CODE
#else
								true
#endif
								, allowBicycle)) {
								exploreUturn = true; // allow exceptional u-turns
							}

							nextSegmentId = netManager.m_segments.m_buffer[nextSegmentId].GetRightSegment(nextNodeId);
						}
#if ROUTING
					} // NON-STOCK CODE
#endif

					// NON-STOCK CODE START
					float segmentSelectionCost = 1f;
					float laneSelectionCost = 1f;
					float laneChangingCost = 1f;
					bool enableAdvancedAI = false;
					// NON-STOCK CODE END

#if ADVANCEDAI && ROUTING
					/*
					 * =======================================================================================================
					 * Calculate Advanced Vehicle AI cost factors
					 * =======================================================================================================
					 */
					if (
						Options.advancedAI &&
						m_isRoadVehicle &&
						prevIsCarLane &&
						!m_stablePath
					) {
						enableAdvancedAI = true;
						CalculateAdvancedAiCostFactors(ref item, ref prevSegment, ref prevLane, nextNodeId, ref nextNode, ref segmentSelectionCost, ref laneSelectionCost, ref laneChangingCost);
					}
#endif

#if ROUTING
					if (prevIsRouted) {
						exploreUturn = false; // custom routing processes regular u-turns
						if (ProcessItemRouted(item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed
#if ADVANCEDAI
							, enableAdvancedAI, laneChangingCost,
#endif
							segmentSelectionCost, laneSelectionCost, nextNodeId, ref nextNode, false, m_routingManager.segmentRoutings[prevSegmentId], m_routingManager.laneEndBackwardRoutings[laneRoutingIndex], connectOffset)) {
							exploreUturn = true; // allow exceptional u-turns
						}
					} else if (! exploreUturn) {
						// no exceptional u-turns allowed: allow regular u-turns
						exploreUturn = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
					}
#endif

					if (exploreUturn && (m_vehicleTypes & VehicleInfo.VehicleType.Tram) == VehicleInfo.VehicleType.None) {
						ProcessItemCosts(item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed,
#if ADVANCEDAI && ROUTING
							enableAdvancedAI, laneChangingCost,
#endif
							nextNodeId, ref nextNode, false, prevSegmentId, ref prevSegment,
#if ROUTING
							segmentSelectionCost, laneSelectionCost, null,
#endif
							ref prevRelSimilarLaneIndex, connectOffset, true, false);
					}
				}

				if (allowPedestrian) {
					int nextLaneIndex;
					uint nextLaneId;
					if (prevSegment.GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Pedestrian, m_vehicleTypes, out nextLaneIndex, out nextLaneId)) {
						ProcessItemPedBicycle(item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, prevSegmentId, ref prevSegment, nextNodeId, ref nextNode, nextLaneIndex, nextLaneId, ref netManager.m_lanes.m_buffer[nextLaneId], switchConnectOffset, switchConnectOffset);
					}
				}
			}

			if (nextNode.m_lane != 0) {
				bool targetDisabled = (nextNode.m_flags & (NetNode.Flags.Disabled | NetNode.Flags.DisableOnlyMiddle)) == NetNode.Flags.Disabled;
				ushort nextSegmentId = netManager.m_lanes.m_buffer[nextNode.m_lane].m_segment;
				if (nextSegmentId != 0 && nextSegmentId != prevSegmentId) {
					ProcessItemPublicTransport(item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextNodeId, targetDisabled, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], nextNode.m_lane, nextNode.m_laneOffset, connectOffset);
				}
			}
		}

		// 2
		private void ProcessItemPublicTransport(BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, float prevMaxSpeed, float prevLaneSpeed, ushort nextNodeId, bool targetDisabled, ushort nextSegmentId, ref NetSegment nextSegment, uint nextLaneId, byte offset, byte connectOffset) {
			if ((nextSegment.m_flags & m_disableMask) != NetSegment.Flags.None) {
				return;
			}

			NetManager netManager = Singleton<NetManager>.instance;
			if (targetDisabled && ((netManager.m_nodes.m_buffer[nextSegment.m_startNode].m_flags | netManager.m_nodes.m_buffer[nextSegment.m_endNode].m_flags) & NetNode.Flags.Disabled) == NetNode.Flags.None) {
				return;
			}

			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = prevSegment.Info;
			int nextNumLanes = nextSegmentInfo.m_lanes.Length;
			// float prevMaxSpeed = 1f; // stock code commented
			// float prevLaneSpeed = 1f; // stock code commented
			NetInfo.LaneType prevLaneType = NetInfo.LaneType.None;
			if (item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[item.m_position.m_lane];
				// prevMaxSpeed = prevLaneInfo.m_speedLimit; // stock code commented
				// prevLaneSpeed = CalculateLaneSpeed(prevMaxSpeed, connectOffset, item.m_position.m_offset, ref prevSegment, prevLaneInfo); // stock code commented
				prevLaneType = prevLaneInfo.m_laneType;
				if ((prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None) {
					prevLaneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
			}

			float prevLength = (prevLaneType != NetInfo.LaneType.PublicTransport) ? prevSegment.m_averageLength : prevLane.m_length;
			float offsetLength = (float)Mathf.Abs(connectOffset - item.m_position.m_offset) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * prevLength;
			float methodDistance = item.m_methodDistance + offsetLength;
			float comparisonValue = item.m_comparisonValue + offsetLength / (prevLaneSpeed * m_maxLength);
			float duration = item.m_duration + offsetLength / prevMaxSpeed;
			Vector3 b = prevLane.CalculatePosition((float)(int)connectOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);

			if (!m_ignoreCost) {
				int ticketCost = prevLane.m_ticketCost;
				if (ticketCost != 0) {
					comparisonValue += (float)(ticketCost * m_pathRandomizer.Int32(2000u)) * TICKET_COST_CONVERSION_FACTOR;
				}
			}

			int nextLaneIndex = 0;
			uint curLaneId = nextSegment.m_lanes;
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
			if (nextLaneInfo.CheckType(m_laneTypes, m_vehicleTypes)) {
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

				if (nextLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian && !(nextItem.m_methodDistance < m_conf.PathFinding.MaxWalkingDistance) && !m_stablePath) { // NON-STOCK CODE (custom walking distance)
					return;
				}

				float nextMaxSpeed;
#if SPEEDLIMITS
				// NON-STOCK CODE START
				nextMaxSpeed = m_speedLimitManager.GetLockFreeGameSpeedLimit(nextSegmentId, (byte)nextLaneIndex, nextLaneId, nextLaneInfo);
				// NON-STOCK CODE END
#else
				nextMaxSpeed = nextLaneInfo.m_speedLimit;
#endif

				nextItem.m_comparisonValue = comparisonValue + distance / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * m_maxLength);
				nextItem.m_duration = duration + distance / ((prevMaxSpeed + nextMaxSpeed) * 0.5f);

				if ((nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
					nextItem.m_direction = NetInfo.InvertDirection(nextLaneInfo.m_finalDirection);
				} else {
					nextItem.m_direction = nextLaneInfo.m_finalDirection;
				}

				if (nextLaneId == m_startLaneA) {
					if (((nextItem.m_direction & NetInfo.Direction.Forward) == NetInfo.Direction.None || nextItem.m_position.m_offset < m_startOffsetA) &&
						((nextItem.m_direction & NetInfo.Direction.Backward) == NetInfo.Direction.None || nextItem.m_position.m_offset > m_startOffsetA)) {
						return;
					}

					float nextSpeed = CalculateLaneSpeed(nextMaxSpeed, m_startOffsetA, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo);
					float nextOffset = (float)Mathf.Abs(nextItem.m_position.m_offset - m_startOffsetA) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

					nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * m_maxLength);
					nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextSpeed;
				}

				if (nextLaneId == m_startLaneB) {
					if (((nextItem.m_direction & NetInfo.Direction.Forward) == NetInfo.Direction.None || nextItem.m_position.m_offset < m_startOffsetB) &&
						((nextItem.m_direction & NetInfo.Direction.Backward) == NetInfo.Direction.None || nextItem.m_position.m_offset > m_startOffsetB)) {
						return;
					}

					float nextSpeed = CalculateLaneSpeed(nextMaxSpeed, m_startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo);
					float nextOffset = (float)Mathf.Abs(nextItem.m_position.m_offset - m_startOffsetB) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

					nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * m_maxLength);
					nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextSpeed;
				}

				nextItem.m_laneID = nextLaneId;
				nextItem.m_lanesUsed = (item.m_lanesUsed | nextLaneInfo.m_laneType);
#if PARKINGAI
				nextItem.m_vehiclesUsed = (item.m_vehiclesUsed | nextLaneInfo.m_vehicleType);
#endif
#if ADVANCEDAI && ROUTING
				// NON-STOCK CODE START
				nextItem.m_trafficRand = item.m_trafficRand;
				// NON-STOCK CODE END
#endif

				AddBufferItem(nextItem, item.m_position);
			}
		}

#if ADVANCEDAI && ROUTING
		// 3a (non-routed, no adv. AI)
		private bool ProcessItemCosts(BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, float prevMaxSpeed, float prevLaneSpeed, ushort nextNodeId, ref NetNode nextNode, bool isMiddle, ushort nextSegmentId, ref NetSegment nextSegment, ref int laneIndexFromInner, byte connectOffset, bool enableVehicle, bool enablePedestrian) {
			return ProcessItemCosts(item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextNodeId, ref nextNode, isMiddle, nextSegmentId, ref nextSegment, ref laneIndexFromInner, connectOffset, enableVehicle, enablePedestrian, false);
		}
#endif

#if ROUTING || ADVANCEDAI
		// 3b (non-routed, adv. AI toggleable)
		private bool ProcessItemCosts(BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, float prevMaxSpeed, float prevLaneSpeed, ushort nextNodeId, ref NetNode nextNode, bool isMiddle, ushort nextSegmentId, ref NetSegment nextSegment, ref int laneIndexFromInner, byte connectOffset, bool enableVehicle, bool enablePedestrian
#if ADVANCEDAI && ROUTING
			, bool enableAdvancedAI
#endif
		) {
			return ProcessItemCosts(item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed,
#if ADVANCEDAI && ROUTING
				enableAdvancedAI, 1f,
#endif
				nextNodeId, ref nextNode, isMiddle, nextSegmentId, ref nextSegment,
#if ROUTING
				1f, 1f, null,
#endif
			ref laneIndexFromInner, connectOffset, enableVehicle, enablePedestrian);
		}
#endif

		// 3c
		private bool ProcessItemCosts(BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, float prevMaxSpeed, float prevLaneSpeed,
#if ADVANCEDAI && ROUTING
			bool enableAdvancedAI, float laneChangingCost,
#endif
			ushort nextNodeId, ref NetNode nextNode, bool isMiddle, ushort nextSegmentId, ref NetSegment nextSegment,
#if ROUTING
			float segmentSelectionCost, float laneSelectionCost, LaneTransitionData? transition,
#endif
			ref int laneIndexFromInner, byte connectOffset, bool enableVehicle, bool enablePedestrian
		) {
			bool blocked = false;
			if ((nextSegment.m_flags & m_disableMask) != NetSegment.Flags.None) {
				return blocked;
			}

			NetManager netManager = Singleton<NetManager>.instance;
			NetInfo nextSegmentInfo = nextSegment.Info;
			NetInfo prevSegmentInfo = prevSegment.Info;
			int nextNumLanes = nextSegmentInfo.m_lanes.Length;
			NetInfo.Direction nextDir = (nextNodeId != nextSegment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
			NetInfo.Direction nextFinalDir = ((nextSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? nextDir : NetInfo.InvertDirection(nextDir);
			// float prevMaxSpeed = 1f; // stock code commented
			// float prevLaneSpeed = 1f; // stock code commented
			NetInfo.LaneType prevLaneType = NetInfo.LaneType.None;
			VehicleInfo.VehicleType prevVehicleType = VehicleInfo.VehicleType.None;
#if ADVANCEDAI && ROUTING
			int prevOuterSimilarLaneIndex = 0;
#endif
			if (item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[item.m_position.m_lane];
				prevLaneType = prevLaneInfo.m_laneType;
				prevVehicleType = prevLaneInfo.m_vehicleType;
				// prevMaxSpeed = prevLaneInfo.m_speedLimit; // stock code commented
				// prevLaneSpeed = CalculateLaneSpeed(prevMaxSpeed, connectOffset, item.m_position.m_offset, ref prevSegment, prevLaneInfo); // stock code commented
#if ADVANCEDAI && ROUTING
				prevOuterSimilarLaneIndex = m_routingManager.CalcOuterSimilarLaneIndex(prevLaneInfo);
#endif
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

			if (!m_stablePath) {
#if ADVANCEDAI && ROUTING
				if (!enableAdvancedAI) {
#endif
					offsetLength *= (float)(new Randomizer(m_pathFindIndex << 16 | item.m_position.m_segment).Int32(900, 1000 + prevSegment.m_trafficDensity * 10) + m_pathRandomizer.Int32(20u)) * 0.001f;
#if ADVANCEDAI && ROUTING
				}
#endif
			}

			if ((prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None && (prevVehicleType & m_vehicleTypes) == VehicleInfo.VehicleType.Car && (prevSegment.m_flags & m_carBanMask) != NetSegment.Flags.None) {
				offsetLength *= 7.5f;
			}

			if (m_transportVehicle && prevLaneType == NetInfo.LaneType.TransportVehicle) {
				offsetLength *= 0.95f;
			}

#if ROUTING
			offsetLength *= segmentSelectionCost;
			offsetLength *= laneSelectionCost;
#endif

			float baseLength = offsetLength / (prevLaneSpeed * m_maxLength); // NON-STOCK CODE
			float comparisonValue = item.m_comparisonValue + baseLength; // NON-STOCK CODE (refactored)
			if (!m_ignoreCost) {
				int ticketCost = prevLane.m_ticketCost;
				if (ticketCost != 0) {
					comparisonValue += (float)(ticketCost * m_pathRandomizer.Int32(2000u)) * TICKET_COST_CONVERSION_FACTOR;
				}
			}

			if ((prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None) {
				prevLaneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
			}

			Vector3 b = prevLane.CalculatePosition((float)connectOffset * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR);
			int newLaneIndexFromInner = laneIndexFromInner;
			bool isTransition = (nextNode.m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;

			NetInfo.LaneType allowedLaneTypes = m_laneTypes;
			VehicleInfo.VehicleType allowedVehicleTypes = m_vehicleTypes;
			if (!enableVehicle) {
				allowedVehicleTypes &= VehicleInfo.VehicleType.Bicycle;
				if (allowedVehicleTypes == VehicleInfo.VehicleType.None) {
					allowedLaneTypes &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
			}
			if (!enablePedestrian) {
				allowedLaneTypes &= ~NetInfo.LaneType.Pedestrian;
			}

			// NON-STOCK CODE START
			bool applyTransportTransferPenalty =
				Options.realisticPublicTransport &&
				!m_stablePath &&
				(allowedLaneTypes & (NetInfo.LaneType.PublicTransport | NetInfo.LaneType.Pedestrian)) == (NetInfo.LaneType.PublicTransport | NetInfo.LaneType.Pedestrian) &&
				m_conf.PathFinding.PublicTransportTransitionMinPenalty >= 0 &&
				m_conf.PathFinding.PublicTransportTransitionMaxPenalty > m_conf.PathFinding.PublicTransportTransitionMinPenalty
			;

			int nextLaneIndex = 0;
			uint nextLaneId = nextSegment.m_lanes;
			int maxNextLaneIndex = nextNumLanes - 1;
#if ADVANCEDAI && ROUTING
			byte laneDist = 0;
#endif
#if ROUTING
			if (transition != null) {
				LaneTransitionData trans = (LaneTransitionData)transition;
				nextLaneIndex = trans.laneIndex;
				nextLaneId = trans.laneId;
				maxNextLaneIndex = nextLaneIndex;
#if ADVANCEDAI
				laneDist = trans.distance;
#endif
			}
#endif
			// NON-STOCK CODE END

			for (; nextLaneIndex <= maxNextLaneIndex && nextLaneId != 0; nextLaneIndex++) {
				NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];
				if ((nextLaneInfo.m_finalDirection & nextFinalDir) != NetInfo.Direction.None) {
					if (nextLaneInfo.CheckType(allowedLaneTypes, allowedVehicleTypes) && (nextSegmentId != item.m_position.m_segment || nextLaneIndex != item.m_position.m_lane) && (nextLaneInfo.m_finalDirection & nextFinalDir) != NetInfo.Direction.None) {
						if (acuteTurningAngle && nextLaneInfo.m_laneType == NetInfo.LaneType.Vehicle && (nextLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) == VehicleInfo.VehicleType.None) {
							continue;
						}

						BufferItem nextItem = default(BufferItem);

						Vector3 a = ((nextDir & NetInfo.Direction.Forward) == NetInfo.Direction.None) ? netManager.m_lanes.m_buffer[nextLaneId].m_bezier.a : netManager.m_lanes.m_buffer[nextLaneId].m_bezier.d;
						float transitionCost = Vector3.Distance(a, b);
						if (isTransition) {
							transitionCost *= 2f;
						}

						float nextMaxSpeed;
#if SPEEDLIMITS
						// NON-STOCK CODE START
						nextMaxSpeed = m_speedLimitManager.GetLockFreeGameSpeedLimit(nextSegmentId, (byte)nextLaneIndex, nextLaneId, nextLaneInfo);
						// NON-STOCK CODE END
#else
						nextMaxSpeed = nextLaneInfo.m_speedLimit;
#endif

						float transitionCostOverMeanMaxSpeed = transitionCost / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * m_maxLength);
						nextItem.m_position.m_segment = nextSegmentId;
						nextItem.m_position.m_lane = (byte)nextLaneIndex;
						nextItem.m_position.m_offset = (byte)(((nextDir & NetInfo.Direction.Forward) != NetInfo.Direction.None) ? 255 : 0);
						if ((nextLaneInfo.m_laneType & prevLaneType) == NetInfo.LaneType.None) {
							nextItem.m_methodDistance = 0f;
						} else {
							nextItem.m_methodDistance = methodDistance + transitionCost;
						}

						if (nextLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian && !(nextItem.m_methodDistance < m_conf.PathFinding.MaxWalkingDistance) && !m_stablePath) { // NON-STOCK CODE (custom walking distance)
							nextLaneId = netManager.m_lanes.m_buffer[nextLaneId].m_nextLane;
							continue;
						}

						// NON-STOCK CODE START
						if (applyTransportTransferPenalty) {
							if (
								isMiddle &&
								(nextLaneInfo.m_laneType & prevLaneType) == NetInfo.LaneType.None &&
								(item.m_lanesUsed & NetInfo.LaneType.PublicTransport) != NetInfo.LaneType.None &&
								nextLaneInfo.m_laneType == NetInfo.LaneType.PublicTransport
							) {
								// apply penalty when switching between public transport lines
								float transportTransitionPenalty = (m_conf.PathFinding.PublicTransportTransitionMinPenalty + ((float)nextNode.m_maxWaitTime * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR) * (m_conf.PathFinding.PublicTransportTransitionMaxPenalty - m_conf.PathFinding.PublicTransportTransitionMinPenalty)) / (0.5f * this.m_maxLength);
								transitionCostOverMeanMaxSpeed += transportTransitionPenalty;
							} else if (
								(nextLaneId == this.m_startLaneA || nextLaneId == this.m_startLaneB) &&
								(item.m_lanesUsed & (NetInfo.LaneType.Pedestrian | NetInfo.LaneType.PublicTransport)) == NetInfo.LaneType.Pedestrian
							) {
								// account for public tranport transition costs on non-PT paths
								float transportTransitionPenalty = (2f * m_conf.PathFinding.PublicTransportTransitionMaxPenalty) / (0.5f * this.m_maxLength);
								transitionCostOverMeanMaxSpeed += transportTransitionPenalty;
							}
						}
						// NON-STOCK CODE END

#if ADVANCEDAI && ROUTING
						if (enableAdvancedAI) {
							nextItem.m_comparisonValue = transitionCostOverMeanMaxSpeed;
						} else {
#endif
							nextItem.m_comparisonValue = comparisonValue + transitionCostOverMeanMaxSpeed;
#if ADVANCEDAI && ROUTING
						}
#endif
						nextItem.m_duration = duration + transitionCost / ((prevMaxSpeed + nextMaxSpeed) * 0.5f);
						nextItem.m_direction = nextDir;

						if (nextLaneId == m_startLaneA) {
							if (((nextItem.m_direction & NetInfo.Direction.Forward) == NetInfo.Direction.None || nextItem.m_position.m_offset < m_startOffsetA) &&
								((nextItem.m_direction & NetInfo.Direction.Backward) == NetInfo.Direction.None || nextItem.m_position.m_offset > m_startOffsetA)) {
								nextLaneId = netManager.m_lanes.m_buffer[nextLaneId].m_nextLane;
								continue;
							}

							float nextLaneSpeed = CalculateLaneSpeed(nextMaxSpeed, m_startOffsetA, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo);
							float nextOffset = (float)Mathf.Abs(nextItem.m_position.m_offset - m_startOffsetA) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

							nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextLaneSpeed * m_maxLength);
							nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextLaneSpeed;
						}

						if (nextLaneId == m_startLaneB) {
							if (((nextItem.m_direction & NetInfo.Direction.Forward) == NetInfo.Direction.None || nextItem.m_position.m_offset < m_startOffsetB) &&
								((nextItem.m_direction & NetInfo.Direction.Backward) == NetInfo.Direction.None || nextItem.m_position.m_offset > m_startOffsetB)) {
								nextLaneId = netManager.m_lanes.m_buffer[nextLaneId].m_nextLane;
								continue;
							}

							float nextLaneSpeed = CalculateLaneSpeed(nextMaxSpeed, m_startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo);
							float nextOffset = (float)Mathf.Abs(nextItem.m_position.m_offset - m_startOffsetB) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

							nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextLaneSpeed * m_maxLength);
							nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextLaneSpeed;
						}

						if (!m_ignoreBlocked && (nextSegment.m_flags & NetSegment.Flags.Blocked) != NetSegment.Flags.None && (nextLaneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None) {
							nextItem.m_comparisonValue += 0.1f;
							blocked = true;
						}

						nextItem.m_laneID = nextLaneId;
						nextItem.m_lanesUsed = (item.m_lanesUsed | nextLaneInfo.m_laneType);
#if PARKINGAI
						nextItem.m_vehiclesUsed = (item.m_vehiclesUsed | nextLaneInfo.m_vehicleType);
#endif
#if ADVANCEDAI && ROUTING
						// NON-STOCK CODE START
						nextItem.m_trafficRand = item.m_trafficRand;
						// NON-STOCK CODE END
#endif

						if ((nextLaneInfo.m_laneType & prevLaneType) != NetInfo.LaneType.None && (nextLaneInfo.m_vehicleType & m_vehicleTypes) != VehicleInfo.VehicleType.None) {
#if ADVANCEDAI && ROUTING
							if (enableAdvancedAI) {
								if (m_queueItem.vehicleId != 0 || (nextLaneId != m_startLaneA && nextLaneId != m_startLaneB)) {
									if (laneDist != 0) {
										// apply lane changing costs
										comparisonValue *=
											1f +
											laneDist *
											laneChangingCost *
											(laneDist > 1 ? m_conf.AdvancedVehicleAI.MoreThanOneLaneChangingCostFactor : 1f); // additional costs for changing multiple lanes at once
									}
								}

								nextItem.m_comparisonValue += comparisonValue;
							} else {
#endif
								int firstTarget = netManager.m_lanes.m_buffer[nextLaneId].m_firstTarget;
								int lastTarget = netManager.m_lanes.m_buffer[nextLaneId].m_lastTarget;
								if (laneIndexFromInner < firstTarget || laneIndexFromInner >= lastTarget) {
									nextItem.m_comparisonValue += Mathf.Max(1f, transitionCost * 3f - 3f) / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * m_maxLength);
								}
								if (!m_transportVehicle && nextLaneInfo.m_laneType == NetInfo.LaneType.TransportVehicle) {
									nextItem.m_comparisonValue += 20f / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * m_maxLength);
								}
#if ADVANCEDAI && ROUTING
							}
#endif
						}

						AddBufferItem(nextItem, item.m_position);
					}
				} else {
					if ((nextLaneInfo.m_laneType & prevLaneType) != NetInfo.LaneType.None && (nextLaneInfo.m_vehicleType & prevVehicleType) != VehicleInfo.VehicleType.None) {
						newLaneIndexFromInner++;
					}
				}

				nextLaneId = netManager.m_lanes.m_buffer[nextLaneId].m_nextLane;
				continue;
			}
			laneIndexFromInner = newLaneIndexFromInner;
			return blocked;
		}

		// 4
		private void ProcessItemPedBicycle(BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, float prevMaxSpeed, float prevLaneSpeed, ushort nextSegmentId, ref NetSegment nextSegment, ushort nextNodeId, ref NetNode nextNode, int nextLaneIndex, uint nextLaneId, ref NetLane nextLane, byte connectOffset, byte laneSwitchOffset) {
			if ((nextSegment.m_flags & m_disableMask) != NetSegment.Flags.None) {
				return;
			}

			// NON-STOCK CODE START
#if JUNCTIONRESTRICTIONS || CUSTOMTRAFFICLIGHTS
			if (Options.junctionRestrictionsEnabled || Options.timedLightsEnabled) {
				bool nextIsStartNode = nextNodeId == nextSegment.m_startNode;
				if (nextIsStartNode || nextNodeId == nextSegment.m_endNode) {
#if JUNCTIONRESTRICTIONS
					if (Options.junctionRestrictionsEnabled) {
						// check if pedestrians are not allowed to cross here
						if (!m_junctionManager.IsPedestrianCrossingAllowed(nextSegmentId, nextIsStartNode)) {
							return;
						}
					}
#endif

#if CUSTOMTRAFFICLIGHTS
					if (Options.timedLightsEnabled) {
						// check if pedestrian light won't change to green
						ICustomSegmentLights lights = m_customTrafficLightsManager.GetSegmentLights(nextSegmentId, nextIsStartNode, false);
						if (lights != null && lights.InvalidPedestrianLight) {
							return;
						}
					}
#endif
				}
			}
#endif
			// NON-STOCK CODE END

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

			// float prevMaxSpeed = 1f; // stock code commented
			// float prevLaneSpeed = 1f; // stock code commented
			NetInfo.LaneType prevLaneType = NetInfo.LaneType.None;
			if (item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[item.m_position.m_lane];
				// prevMaxSpeed = prevLaneInfo.m_speedLimit; // stock code commented
				// prevLaneSpeed = CalculateLaneSpeed(prevMaxSpeed, laneSwitchOffset, item.m_position.m_offset, ref prevSegment, prevLaneInfo); // stock code commented
				prevLaneType = prevLaneInfo.m_laneType;
				if ((prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None) {
					prevLaneType = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				}
			}

			float prevLength = (prevLaneType != NetInfo.LaneType.PublicTransport) ? prevSegment.m_averageLength : prevLane.m_length;
			float offsetLength = (float)Mathf.Abs(laneSwitchOffset - item.m_position.m_offset) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR * prevLength;
			float methodDistance = item.m_methodDistance + offsetLength;
			float comparisonValue = item.m_comparisonValue + offsetLength / (prevLaneSpeed * m_maxLength);
			float duration = item.m_duration + offsetLength / prevMaxSpeed;

			if (!m_ignoreCost) {
				int ticketCost = prevLane.m_ticketCost;
				if (ticketCost != 0) {
					comparisonValue += (float)(ticketCost * m_pathRandomizer.Int32(2000u)) * TICKET_COST_CONVERSION_FACTOR;
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
						comparisonValue += 100f / (0.25f * m_maxLength);
					}
					nextItem.m_methodDistance = methodDistance + distance;
				}

				if (nextLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian && !(nextItem.m_methodDistance < m_conf.PathFinding.MaxWalkingDistance) && !m_stablePath) { // NON-STOCK CODE (custom walking distance)
					return;
				}

				float nextMaxSpeed;
#if SPEEDLIMITS
				// NON-STOCK CODE START
				nextMaxSpeed = m_speedLimitManager.GetLockFreeGameSpeedLimit(nextSegmentId, (byte)nextLaneIndex, nextLaneId, nextLaneInfo);
				// NON-STOCK CODE END
#else
				nextMaxSpeed = nextLaneInfo.m_speedLimit;
#endif

				nextItem.m_comparisonValue = comparisonValue + distance / ((prevMaxSpeed + nextMaxSpeed) * 0.25f * m_maxLength);
				nextItem.m_duration = duration + distance / ((prevMaxSpeed + nextMaxSpeed) * 0.5f);

				if ((nextSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
					nextItem.m_direction = NetInfo.InvertDirection(nextLaneInfo.m_finalDirection);
				} else {
					nextItem.m_direction = nextLaneInfo.m_finalDirection;
				}

				if (nextLaneId == m_startLaneA) {
					if (((nextItem.m_direction & NetInfo.Direction.Forward) == NetInfo.Direction.None || nextItem.m_position.m_offset < m_startOffsetA) &&
						((nextItem.m_direction & NetInfo.Direction.Backward) == NetInfo.Direction.None || nextItem.m_position.m_offset > m_startOffsetA)) {
						return;
					}
					
					float nextSpeed = CalculateLaneSpeed(nextMaxSpeed, m_startOffsetA, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo);
					float nextOffset = (float)Mathf.Abs(nextItem.m_position.m_offset - m_startOffsetA) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

					nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * m_maxLength);
					nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextSpeed;
				}

				if (nextLaneId == m_startLaneB) {
					if (((nextItem.m_direction & NetInfo.Direction.Forward) == NetInfo.Direction.None || nextItem.m_position.m_offset < m_startOffsetB) &&
						((nextItem.m_direction & NetInfo.Direction.Backward) == NetInfo.Direction.None || nextItem.m_position.m_offset > m_startOffsetB)) {
						return;
					}
					
					float nextSpeed = CalculateLaneSpeed(nextMaxSpeed, m_startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo);
					float nextOffset = (float)Mathf.Abs(nextItem.m_position.m_offset - m_startOffsetB) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

					nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextSpeed * m_maxLength);
					nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextSpeed;
				}

				nextItem.m_laneID = nextLaneId;
				nextItem.m_lanesUsed = (item.m_lanesUsed | nextLaneInfo.m_laneType);
#if PARKINGAI
				nextItem.m_vehiclesUsed = (item.m_vehiclesUsed | nextLaneInfo.m_vehicleType);
#endif
#if ADVANCEDAI && ROUTING
				// NON-STOCK CODE START
				nextItem.m_trafficRand = item.m_trafficRand;
				// NON-STOCK CODE END
#endif

				AddBufferItem(nextItem, item.m_position);
			}
		}

#if ROUTING
		// 5 (custom: process routed vehicle paths)
		private bool ProcessItemRouted(BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, float prevMaxSpeed, float prevLaneSpeed,
#if ADVANCEDAI && ROUTING
			bool enableAdvancedAI, float laneChangingCost,
#endif
			float segmentSelectionCost, float laneSelectionCost, ushort nextNodeId, ref NetNode nextNode, bool isMiddle, SegmentRoutingData prevSegmentRouting, LaneEndRoutingData prevLaneEndRouting, byte connectOffset) {
			/*
			 * =======================================================================================================
			 * Fetch lane end transitions, check if there are any present
			 * =======================================================================================================
			 */
			LaneTransitionData[] laneTransitions = prevLaneEndRouting.transitions;
			if (laneTransitions == null) {
				return false;
			}

			ushort prevSegmentId = item.m_position.m_segment;
			int prevLaneIndex = item.m_position.m_lane;
			NetInfo prevSegmentInfo = prevSegment.Info;
			if (prevLaneIndex >= prevSegmentInfo.m_lanes.Length) {
				return false;
			}
			NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[item.m_position.m_lane];

#if VEHICLERESTRICTIONS
			/*
			 * =======================================================================================================
			 * Check vehicle restrictions, especially bans
			 * =======================================================================================================
			 */
			bool canUseLane = CanUseLane(prevSegmentId, prevSegmentInfo, prevLaneIndex, prevLaneInfo);
			if (! canUseLane && Options.vehicleRestrictionsAggression == VehicleRestrictionsAggression.Strict) {
				// vehicle is strictly prohibited to use this lane
				return false;
			}
#endif

			bool strictLaneRouting =
				m_isLaneArrowObeyingEntity &&
				nextNode.Info.m_class.m_service != ItemClass.Service.Beautification &&
				(nextNode.m_flags & NetNode.Flags.Untouchable) == NetNode.Flags.None
			;
			bool prevIsCarLane =
				(prevLaneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
				(prevLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None
			;

			/*
			 * =======================================================================================================
			 * Check if u-turns may be performed
			 * =======================================================================================================
			 */
			bool isUturnAllowedHere = false; // is u-turn allowed at this place?
			if ((this.m_vehicleTypes & (VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Monorail)) == VehicleInfo.VehicleType.None) { // is vehicle able to perform a u-turn?
#if JUNCTIONRESTRICTIONS
				if (Options.junctionRestrictionsEnabled) {
					bool nextIsStartNode = nextNodeId == prevSegment.m_startNode;
					bool prevIsOutgoingOneWay = nextIsStartNode ? prevSegmentRouting.startNodeOutgoingOneWay : prevSegmentRouting.endNodeOutgoingOneWay;

					// determine if the vehicle may u-turn at the target node, according to customization
					isUturnAllowedHere =
						m_isRoadVehicle && // only road vehicles may perform u-turns
						prevIsCarLane && // u-turns for road vehicles only
						!m_isHeavyVehicle && // only small vehicles may perform u-turns
						!prevIsOutgoingOneWay && // do not u-turn on one-ways
						m_junctionManager.IsUturnAllowed(prevSegmentId, nextIsStartNode) // only do u-turns if allowed
					;
				} else {
#endif
					isUturnAllowedHere = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
#if JUNCTIONRESTRICTIONS
				}
#endif
			}

#if VEHICLERESTRICTIONS
			/*
			 * =======================================================================================================
			 * Apply vehicle restriction costs
			 * =======================================================================================================
			 */
			if (!canUseLane) {
				laneSelectionCost *= VehicleRestrictionsManager.PATHFIND_PENALTIES[(int)Options.vehicleRestrictionsAggression];
			}
#endif

			/*
			 * =======================================================================================================
			 * Apply costs for large vehicles using inner lanes on highways
			 * =======================================================================================================
			 */
			if (Options.preferOuterLane &&
				m_isHeavyVehicle &&
				m_isRoadVehicle &&
				prevIsCarLane &&
				prevSegmentRouting.highway &&
				prevLaneInfo.m_similarLaneCount > 1 &&
				m_pathRandomizer.Int32(m_conf.PathFinding.HeavyVehicleInnerLanePenaltySegmentSel) == 0) {

				int prevOuterSimilarLaneIndex = m_routingManager.CalcOuterSimilarLaneIndex(prevLaneInfo);
				float prevRelOuterLane = ((float)prevOuterSimilarLaneIndex / (float)(prevLaneInfo.m_similarLaneCount - 1));
				laneSelectionCost *= 1f + m_conf.PathFinding.HeavyVehicleMaxInnerLanePenalty * prevRelOuterLane;
			}

			/*
			 * =======================================================================================================
			 * Explore available lane end routings
			 * =======================================================================================================
			 */
			NetManager netManager = Singleton<NetManager>.instance;
			bool blocked = false;
			bool uturnExplored = false;
			for (int k = 0; k < laneTransitions.Length; ++k) {
				ushort nextSegmentId = laneTransitions[k].segmentId;

				if (nextSegmentId == 0) {
					continue;
				}

				if (nextSegmentId == prevSegmentId) {
					if (!isUturnAllowedHere) {
						// prevent double/forbidden exploration of previous segment by vanilla code during this method execution
						continue;
					}
					// we are going to explore a regular u-turn
					uturnExplored = true;
				}

				if (laneTransitions[k].type == LaneEndTransitionType.Invalid) {
					continue;
				}

				// allow vehicles to ignore strict lane routing when moving off
				bool relaxedLaneRouting =
					m_isRoadVehicle &&
					(m_queueItem.vehicleType & (ExtVehicleType.Service | ExtVehicleType.PublicTransport | ExtVehicleType.Emergency)) != ExtVehicleType.None &&
					m_queueItem.vehicleId == 0 &&
					(laneTransitions[k].laneId == m_startLaneA || laneTransitions[k].laneId == m_startLaneB);

				if (
					!relaxedLaneRouting &&
					(strictLaneRouting && laneTransitions[k].type == LaneEndTransitionType.Relaxed)
				) {
					continue;
				}

				bool foundForced = false;
				int dummy = -1; // not required when using custom routing
				if (
					ProcessItemCosts(item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed,
#if ADVANCEDAI && ROUTING
					enableAdvancedAI, laneChangingCost,
#endif
					nextNodeId, ref nextNode, isMiddle, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], segmentSelectionCost, laneSelectionCost, laneTransitions[k], ref dummy, connectOffset, true, false)
				) {
					blocked = true;
				}
			}

			return blocked && !uturnExplored;
		}
#endif

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

			if (comparisonBufferPos >= 1024 || comparisonBufferPos < 0) {
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

		private float CalculateLaneSpeed(float maxSpeed, byte startOffset, byte endOffset, ref NetSegment segment, NetInfo.Lane laneInfo) {
			NetInfo.Direction direction = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? laneInfo.m_finalDirection : NetInfo.InvertDirection(laneInfo.m_finalDirection);
			if ((direction & NetInfo.Direction.Avoid) != NetInfo.Direction.None) {
				if (endOffset > startOffset && direction == NetInfo.Direction.AvoidForward) {
					return maxSpeed * 0.1f;
				}
				if (endOffset < startOffset && direction == NetInfo.Direction.AvoidBackward) {
					return maxSpeed * 0.1f;
				}
				return maxSpeed * 0.2f;
			}
			return maxSpeed;
		}

		private void GetLaneDirection(PathUnit.Position pathPos, out NetInfo.Direction direction, out NetInfo.LaneType laneType
#if PARKINGAI
			, out VehicleInfo.VehicleType vehicleType
#endif
			) {
			NetManager netManager = Singleton<NetManager>.instance;
			NetInfo info = netManager.m_segments.m_buffer[pathPos.m_segment].Info;
			if (info.m_lanes.Length > pathPos.m_lane) {
				direction = info.m_lanes[pathPos.m_lane].m_finalDirection;
				laneType = info.m_lanes[pathPos.m_lane].m_laneType;
#if PARKINGAI
				vehicleType = info.m_lanes[pathPos.m_lane].m_vehicleType;
#endif
				if ((netManager.m_segments.m_buffer[pathPos.m_segment].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
					direction = NetInfo.InvertDirection(direction);
				}
			} else {
				direction = NetInfo.Direction.None;
				laneType = NetInfo.LaneType.None;
#if PARKINGAI
				vehicleType = VehicleInfo.VehicleType.None;
#endif
			}
		}

#if VEHICLERESTRICTIONS
		private bool CanUseLane(ushort segmentId, NetInfo segmentInfo, int laneIndex, NetInfo.Lane laneInfo) {
			if (!Options.vehicleRestrictionsEnabled ||
				m_queueItem.vehicleType == ExtVehicleType.None ||
				m_queueItem.vehicleType == ExtVehicleType.Tram ||
				(laneInfo.m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train)) == VehicleInfo.VehicleType.None) {
				return true;
			}

			ExtVehicleType allowedTypes = m_vehicleRestrictionsManager.GetAllowedVehicleTypes(segmentId, segmentInfo, (uint)laneIndex, laneInfo, VehicleRestrictionsMode.Configured);

			return ((allowedTypes & m_queueItem.vehicleType) != ExtVehicleType.None);
		}
#endif

#if ADVANCEDAI && ROUTING
		private void CalculateAdvancedAiCostFactors(ref BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, ushort nextNodeId, ref NetNode nextNode, ref float segmentSelectionCost, ref float laneSelectionCost, ref float laneChangingCost) {
			NetInfo prevSegmentInfo = prevSegment.Info;
			bool nextIsJunction = (nextNode.m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) == NetNode.Flags.Junction;

			if (nextIsJunction) {
				/*
				 * =======================================================================================================
				 * Calculate costs for randomized lane selection behind junctions and highway transitions
				 * =======================================================================================================
				 */
				// TODO check if highway transitions are actually covered by this code
				if (
					!m_isHeavyVehicle &&
					m_pathRandomizer.Int32(m_conf.AdvancedVehicleAI.LaneRandomizationJunctionSel) == 0 &&
					m_pathRandomizer.Int32((uint)prevSegmentInfo.m_lanes.Length) == 0
				) {
					// randomized lane selection at junctions
					laneSelectionCost *= 1f + m_conf.AdvancedVehicleAI.LaneRandomizationCostFactor;
				}

				/*
				 * =======================================================================================================
				 * Calculate junction costs
				 * =======================================================================================================
				 */
				// TODO if (prevSegmentRouting.highway) ?
				segmentSelectionCost *= 1f + m_conf.AdvancedVehicleAI.JunctionBaseCost;
			}

			bool nextIsStartNode = prevSegment.m_startNode == nextNodeId;
			bool nextIsEndNode = nextNodeId == prevSegment.m_endNode;
			if (nextIsStartNode || nextIsEndNode) { // next node is a regular node
				/*
				 * =======================================================================================================
				 * Calculate traffic measurement costs for segment selection
				 * =======================================================================================================
				 */
				NetInfo.Direction prevFinalDir = nextIsStartNode ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
				prevFinalDir = ((prevSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? prevFinalDir : NetInfo.InvertDirection(prevFinalDir);
				TrafficMeasurementManager.SegmentDirTrafficData prevDirTrafficData =
					m_trafficMeasurementManager.segmentDirTrafficData[m_trafficMeasurementManager.GetDirIndex(item.m_position.m_segment, prevFinalDir)];

				float segmentTraffic = Mathf.Clamp(1f - (float)prevDirTrafficData.meanSpeed / (float)TrafficMeasurementManager.REF_REL_SPEED + item.m_trafficRand, 0, 1f);

				segmentSelectionCost *= 1f +
					m_conf.AdvancedVehicleAI.TrafficCostFactor *
					segmentTraffic;

				if (
					m_conf.AdvancedVehicleAI.LaneDensityRandInterval > 0 &&
					nextIsJunction &&
					(nextNode.m_flags & (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)) != (NetNode.Flags.OneWayIn | NetNode.Flags.OneWayOut)
				) {
					item.m_trafficRand = 0.01f * ((float)m_pathRandomizer.Int32((uint)m_conf.AdvancedVehicleAI.LaneDensityRandInterval + 1u) - m_conf.AdvancedVehicleAI.LaneDensityRandInterval / 2f);
				}

				if (
					m_conf.AdvancedVehicleAI.LaneChangingJunctionBaseCost > 0 &&
					(Singleton<NetManager>.instance.m_nodes.m_buffer[nextIsStartNode ? prevSegment.m_endNode : prevSegment.m_startNode].m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) == NetNode.Flags.Junction // check previous node
				) {
					/*
					 * =======================================================================================================
					 * Calculate lane changing base cost factor when in front of junctions
					 * =======================================================================================================
					 */
					laneChangingCost *= m_conf.AdvancedVehicleAI.LaneChangingJunctionBaseCost;
				}

				/*
				 * =======================================================================================================
				 * Calculate general lane changing base cost factor
				 * =======================================================================================================
				 */
				 if (m_conf.AdvancedVehicleAI.LaneChangingBaseMinCost > 0 && m_conf.AdvancedVehicleAI.LaneChangingBaseMaxCost > m_conf.AdvancedVehicleAI.LaneChangingBaseMinCost) {
					float rand = (float)m_pathRandomizer.Int32(101u) / 100f;
					laneChangingCost *= m_conf.AdvancedVehicleAI.LaneChangingBaseMinCost + rand * (m_conf.AdvancedVehicleAI.LaneChangingBaseMaxCost - m_conf.AdvancedVehicleAI.LaneChangingBaseMinCost);
				}
			}
		}
#endif

		private void PathFindThread() {
			while (true) {
				try {
					Monitor.Enter(m_queueLock);

					while (m_queueFirst == 0 && !m_terminated) {
						Monitor.Wait(m_queueLock);
					}

					if (m_terminated) {
						break;
					}

					m_calculating = m_queueFirst;
					// NON-STOCK CODE START
					m_queueFirst = CustomPathManager._instance.queueItems[m_calculating].nextPathUnitId;
					// NON-STOCK CODE END
					// QueueFirst = PathUnits.m_buffer[Calculating].m_nextPathUnit; // stock code commented

					if (m_queueFirst == 0) {
						m_queueLast = 0u;
						m_queuedPathFindCount = 0;
					} else {
						m_queuedPathFindCount--;
					}

					// NON-STOCK CODE START
					CustomPathManager._instance.queueItems[m_calculating].nextPathUnitId = 0u;
					// NON-STOCK CODE END
					// PathUnits.m_buffer[Calculating].m_nextPathUnit = 0u; // stock code commented

					m_pathUnits.m_buffer[m_calculating].m_pathFindFlags = (byte)((m_pathUnits.m_buffer[m_calculating].m_pathFindFlags & ~PathUnit.FLAG_CREATED) | PathUnit.FLAG_CALCULATING);

					// NON-STOCK CODE START
					m_queueItem = CustomPathManager._instance.queueItems[m_calculating];
					// NON-STOCK CODE END
				} finally {
					Monitor.Exit(m_queueLock);
				}

				try {
					m_pathfindProfiler.BeginStep();
					try {
						PathFindImplementation(m_calculating, ref m_pathUnits.m_buffer[m_calculating]);
					} finally {
						m_pathfindProfiler.EndStep();
					}
				} catch (Exception ex) {
					UIView.ForwardException(ex);
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find error: " + ex.Message + "\n" + ex.StackTrace);
					m_pathUnits.m_buffer[m_calculating].m_pathFindFlags |= PathUnit.FLAG_FAILED;
					// NON-STOCK CODE START
#if DEBUG
					++m_failedPathFinds;
#endif
					// NON-STOCK CODE END
				}

				try {
					Monitor.Enter(m_queueLock);

					m_pathUnits.m_buffer[m_calculating].m_pathFindFlags = (byte)(m_pathUnits.m_buffer[m_calculating].m_pathFindFlags & ~PathUnit.FLAG_CALCULATING);

					// NON-STOCK CODE START
					CustomPathManager._instance.queueItems[m_calculating].queued = false;
					CustomPathManager._instance.ReleasePath(m_calculating);
					// NON-STOCK CODE END
					// Singleton<PathManager>.instance.ReleasePath(Calculating); // stock code commented

					m_calculating = 0u;
					Monitor.Pulse(m_queueLock);
				} finally {
					Monitor.Exit(m_queueLock);
				}
			}
		}
	}
}
