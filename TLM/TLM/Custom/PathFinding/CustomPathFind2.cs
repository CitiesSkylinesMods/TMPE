using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using CSUtil.Commons;
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
			public override string ToString() {
				return $"[BufferItem\n" +
				"\t" + $"m_position=(s#({m_position.m_segment}), l#({m_position.m_lane}), o#({m_position.m_offset}))\n" +
				"\t" + $"m_laneID={m_laneID}\n" +
				"\t" + $"m_comparisonValue={m_comparisonValue}\n" +
				"\t" + $"m_methodDistance={m_methodDistance}\n" +
				"\t" + $"m_duration={m_duration}\n" +
				"\t" + $"m_direction={m_direction}\n" +
				"\t" + $"m_lanesUsed={m_lanesUsed}\n" +
#if PARKINGAI
				"\t" + $"m_vehiclesUsed={m_vehiclesUsed}\n" +
#endif
#if ADVANCEDAI && ROUTING
				"\t" + $"m_trafficRand={m_trafficRand}\n" +
#endif
				"BufferItem]";
			}
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
		private bool m_debug = false;
		private IDictionary<ushort, IList<ushort>> m_debugPositions = null;
#endif
#if PARKINGAI || JUNCTIONRESTRICTIONS
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

					m_pathUnits.m_buffer[unit].m_pathFindFlags |= PathUnit.FLAG_CREATED;
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
				(m_queueItem.vehicleType & LaneArrowManager.EXT_VEHICLE_TYPES) != ExtVehicleType.None
			;
#endif

#if DEBUG
			m_debug = m_conf.Debug.Switches[0] &&
				(m_conf.Debug.ExtVehicleType == ExtVehicleType.None || m_queueItem.vehicleType == m_conf.Debug.ExtVehicleType) &&
				(m_conf.Debug.StartSegmentId == 0 || data.m_position00.m_segment == m_conf.Debug.StartSegmentId || data.m_position02.m_segment == m_conf.Debug.StartSegmentId) &&
				(m_conf.Debug.EndSegmentId == 0 || data.m_position01.m_segment == m_conf.Debug.EndSegmentId || data.m_position03.m_segment == m_conf.Debug.EndSegmentId) &&
				(m_conf.Debug.VehicleId == 0 || m_queueItem.vehicleId == m_conf.Debug.VehicleId)
			;
			if (m_debug) {
				m_debugPositions = new Dictionary<ushort, IList<ushort>>();
			}
#endif

			int posCount = m_pathUnits.m_buffer[unit].m_positionCount & 0xF;
			int vehiclePosIndicator = m_pathUnits.m_buffer[unit].m_positionCount >> 4;
			BufferItem bufferItemStartA = default(BufferItem);
			if (data.m_position00.m_segment != 0 && posCount >= 1) {
#if PARKINGAI || JUNCTIONRESTRICTIONS
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
#if PARKINGAI || JUNCTIONRESTRICTIONS
				m_startSegmentA = 0; // NON-STOCK CODE
#endif
				m_startLaneA = 0u;
				m_startOffsetA = 0;
			}

			BufferItem bufferItemStartB = default(BufferItem);
			if (data.m_position02.m_segment != 0 && posCount >= 3) {
#if PARKINGAI || JUNCTIONRESTRICTIONS
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
#if PARKINGAI || JUNCTIONRESTRICTIONS
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

#if DEBUG
			bool detourMissing = (m_vehicleTypes & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Monorail | VehicleInfo.VehicleType.Metro)) != VehicleInfo.VehicleType.None && !m_queueItem.queued;
			if (detourMissing) {
				Log.Warning($"Path-finding for unhandled vehicle requested!");
			}

			if (m_debug || detourMissing) {
				Debug(unit, $"PathFindImplementation: Preparing calculation:\n" +
					$"\tbufferItemStartA: segment={bufferItemStartA.m_position.m_segment} lane={bufferItemStartA.m_position.m_lane} off={bufferItemStartA.m_position.m_offset} laneId={bufferItemStartA.m_laneID}\n" +
					$"\tbufferItemStartB: segment={bufferItemStartB.m_position.m_segment} lane={bufferItemStartB.m_position.m_lane} off={bufferItemStartB.m_position.m_offset} laneId={bufferItemStartB.m_laneID}\n" +
					$"\tbufferItemEndA: segment={bufferItemEndA.m_position.m_segment} lane={bufferItemEndA.m_position.m_lane} off={bufferItemEndA.m_position.m_offset} laneId={bufferItemEndA.m_laneID}\n" +
					$"\tbufferItemEndB: segment={bufferItemEndB.m_position.m_segment} lane={bufferItemEndB.m_position.m_lane} off={bufferItemEndB.m_position.m_offset} laneId={bufferItemEndB.m_laneID}\n" +
					$"\tvehicleItem: segment={data.m_position11.m_segment} lane={data.m_position11.m_lane} off={data.m_position11.m_offset} laneId={m_vehicleLane} vehiclePosIndicator={vehiclePosIndicator}\n" +
					$"Properties:\n" +
					"\t" + $"m_maxLength={m_maxLength}\n" +
					"\t" + $"m_startLaneA={m_startLaneA}\n" +
					"\t" + $"m_startLaneB={m_startLaneB}\n" +
					"\t" + $"m_endLaneA={m_endLaneA}\n" +
					"\t" + $"m_endLaneB={m_endLaneB}\n" +
					"\t" + $"m_startOffsetA={m_startOffsetA}\n" +
					"\t" + $"m_startOffsetB={m_startOffsetB}\n" +
					"\t" + $"m_vehicleLane={m_vehicleLane}\n" +
					"\t" + $"m_vehicleOffset={m_vehicleOffset}\n" +
					"\t" + $"m_carBanMask={m_carBanMask}\n" +
					"\t" + $"m_disableMask={m_disableMask}\n" +
					"\t" + $"m_ignoreBlocked={m_ignoreBlocked}\n" +
					"\t" + $"m_stablePath={m_stablePath}\n" +
					"\t" + $"m_randomParking={m_randomParking}\n" +
					"\t" + $"m_transportVehicle={m_transportVehicle}\n" +
					"\t" + $"m_ignoreCost={m_ignoreCost}\n" +
					"\t" + $"m_pathFindIndex={m_pathFindIndex}\n" +
					"\t" + $"m_laneTypes={m_laneTypes}\n" +
					"\t" + $"m_vehicleTypes={m_vehicleTypes}\n" +
					"\t" + $"m_queueItem={m_queueItem}\n" +
					"\t" + $"m_isHeavyVehicle={m_isHeavyVehicle}\n" +
					"\t" + $"m_failedPathFinds={m_failedPathFinds}\n" +
					"\t" + $"m_succeededPathFinds={m_succeededPathFinds}\n" +
#if PARKINGAI || JUNCTIONRESTRICTIONS
					"\t" + $"m_startSegmentA={m_startSegmentA}\n" +
					"\t" + $"m_startSegmentB={m_startSegmentB}\n" +
#endif
#if ROUTING
					"\t" + $"m_isRoadVehicle={m_isRoadVehicle}\n" +
					"\t" + $"m_isLaneArrowObeyingEntity={m_isLaneArrowObeyingEntity}"
#endif
				);
			}
#endif

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
						ProcessItemMain(
#if DEBUG
							unit,
#endif
							candidateItem, ref netManager.m_segments.m_buffer[candidateItem.m_position.m_segment], ref netManager.m_lanes.m_buffer[candidateItem.m_laneID], startNodeId, ref netManager.m_nodes.m_buffer[startNodeId], 0, false);
					}

					if ((candidateItem.m_direction & NetInfo.Direction.Backward) != NetInfo.Direction.None) {
						ProcessItemMain(
#if DEBUG
							unit,
#endif
							candidateItem, ref netManager.m_segments.m_buffer[candidateItem.m_position.m_segment], ref netManager.m_lanes.m_buffer[candidateItem.m_laneID], endNodeId, ref netManager.m_nodes.m_buffer[endNodeId], 255, false);
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
#if DEBUG
								if (m_debug && (m_conf.Debug.NodeId <= 0 || specialNodeId == m_conf.Debug.NodeId)) {
									Debug(unit, $"PathFindImplementation: Handling special node for path unit {unit}, type {m_queueItem.vehicleType}:\n" +
										$"\tcandidateItem.m_position.m_segment={candidateItem.m_position.m_segment}\n" +
										$"\tcandidateItem.m_position.m_lane={candidateItem.m_position.m_lane}\n" +
										$"\tcandidateItem.m_laneID={candidateItem.m_laneID}\n" +
										$"\tspecialNodeId={specialNodeId}\n" +
										$"\tstartNodeId={startNodeId}\n" +
										$"\tendNodeId={endNodeId}\n"
									);
								}
#endif
								ProcessItemMain(
#if DEBUG
									unit,
#endif
									candidateItem, ref netManager.m_segments.m_buffer[candidateItem.m_position.m_segment], ref netManager.m_lanes.m_buffer[candidateItem.m_laneID], specialNodeId, ref netManager.m_nodes.m_buffer[specialNodeId], laneOffset, true);
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

				if (m_debug) {
					Debug(unit, $"PathFindImplementation: Path-find failed: Could not find path");
					string reachableBuf = "";
					string unreachableBuf = "";
					foreach (KeyValuePair<ushort, IList<ushort>> e in m_debugPositions) {
						string buf = $"{e.Key} -> {e.Value.CollectionToString()}\n";
						if (e.Value.Count <= 0) {
							unreachableBuf += buf;
						} else {
							reachableBuf += buf;
						}
					}
					Debug(unit, $"PathFindImplementation: Reachability graph:\n== REACHABLE ==\n" + reachableBuf + "\n== UNREACHABLE ==\n" + unreachableBuf);
				}
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

						if (m_debug) {
							Debug(unit, $"PathFindImplementation: Path-find succeeded");
						}
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

								if (m_debug) {
									Debug(unit, $"Path-finding failed: Could not create path unit");
								}
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

				if (m_debug) {
					Debug(unit, $"Path-finding failed: Internal loop break error");
				}
#endif
				// NON-STOCK CODE END
			}
		}

#if DEBUG
		private void Debug(uint unit, string message) {
			Log._Debug(
				$"PF T#({Thread.CurrentThread.ManagedThreadId}) IDX#({m_pathFindIndex}):\n"
				+ $"UNIT({unit})\n"
				+ message
			);
		}

		private void Debug(uint unit, BufferItem item, string message) {
			Log._Debug(
				$"PF T#({Thread.CurrentThread.ManagedThreadId}) IDX#({m_pathFindIndex}):\n"
				+ $"UNIT({unit}): s#({item.m_position.m_segment}), l#({item.m_position.m_lane})\n"
				+ $"ITEM({item})\n"
				+ message
			);
		}

		private void Debug(uint unit, BufferItem item, ushort nextSegmentId, string message) {
			Log._Debug(
				$"PF T#({Thread.CurrentThread.ManagedThreadId}) IDX#({m_pathFindIndex}):\n"
				+ $"UNIT({unit}): s#({item.m_position.m_segment}), l#({item.m_position.m_lane}) -> s#({nextSegmentId})\n"
				+ $"ITEM({item})\n"
				+ message
			);
		}

		private void Debug(uint unit, BufferItem item, ushort nextSegmentId, int nextLaneIndex, uint nextLaneId, string message) {
			Log._Debug(
				$"PF T#({Thread.CurrentThread.ManagedThreadId}) IDX#({m_pathFindIndex}):\n"
				+ $"UNIT({unit}): s#({item.m_position.m_segment}), l#({item.m_position.m_lane}) -> s#({nextSegmentId}), l#({nextLaneIndex}), lid#({nextLaneId})\n"
				+ $"ITEM({item})\n"
				+ message
			);
		}
#endif

		// 1
		private void ProcessItemMain(
#if DEBUG
			uint unitId,
#endif
			BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, ushort nextNodeId, ref NetNode nextNode, byte connectOffset, bool isMiddle) {
#if DEBUG
			bool debug = this.m_debug && (m_conf.Debug.NodeId <= 0 || nextNodeId == m_conf.Debug.NodeId);
			if (debug) {
				if (!m_debugPositions.ContainsKey(item.m_position.m_segment)) {
					m_debugPositions[item.m_position.m_segment] = new List<ushort>();
				}
			}

			if (debug) {
				Debug(unitId, item, $"ProcessItemMain called.\n"
					+ "\t" + $"nextNodeId={nextNodeId}\n"
					+ "\t" + $"connectOffset={connectOffset}\n"
					+ "\t" + $"isMiddle={isMiddle}"
				);
			}
#endif

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

#if (ADVANCEDAI || PARKINGAI) && ROUTING
				// NON-STOCK CODE START
				prevIsCarLane =
					(prevLaneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
					(prevLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None
				;
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
#if DEBUG
				if (debug) {
					Debug(unitId, item, $"ProcessItemMain: middle: Exploring middle node\n" +
						"\t" + $"nextNodeId={nextNodeId}"
					);
				}
#endif
				for (int i = 0; i < 8; i++) {
					ushort nextSegmentId = nextNode.GetSegment(i);
					if (nextSegmentId != 0) {
#if DEBUG
						if (debug) {
							Debug(unitId, item, $"ProcessItemMain: middle: Exploring next segment behind middle node\n" +
								"\t" + $"nextSegmentId={nextSegmentId}");
						}
#endif

						ProcessItemCosts(
#if DEBUG
							debug, unitId,
#endif
							item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextNodeId, ref nextNode, true, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, !prevIsPedestrianLane, prevIsPedestrianLane
						);
					}
				}
			} else if (prevIsPedestrianLane) {
				// we are going to a pedestrian lane
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
									break; // NON-STOCK CODE
								} else {
#if JUNCTIONRESTRICTIONS
									// next segment does not have pedestrian lanes but cims need to cross it to reach the next segment
									if (!m_junctionManager.IsPedestrianCrossingAllowed(leftSegmentId, netManager.m_segments.m_buffer[leftSegmentId].m_startNode == nextNodeId)) {
										break;
									}
#endif
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
									break; // NON-STOCK CODE
								} else {
#if JUNCTIONRESTRICTIONS
									// next segment does not have pedestrian lanes but cims need to cross it to reach the next segment
									if (!m_junctionManager.IsPedestrianCrossingAllowed(rightSegmentId, netManager.m_segments.m_buffer[rightSegmentId].m_startNode == nextNodeId)) {
										break;
									}
#endif
									rightSegmentId = netManager.m_segments.m_buffer[rightSegmentId].GetRightSegment(nextNodeId);
								}

								if (++numIter == 8) {
									break;
								}
							}
						}

						if (leftLaneId != 0 && (nextLeftSegmentId != prevSegmentId || canCrossStreet || isOnCenterPlatform)) {
#if DEBUG
							if (debug) {
								Debug(unitId, item, $"ProcessItemMain: ped -> ped: Exploring left pedestrian lane\n" +
									"\t" + $"leftLaneId={leftLaneId}\n" +
									"\t" + $"nextLeftSegmentId={nextLeftSegmentId}\n" +
									"\t" + $"canCrossStreet={canCrossStreet}\n" +
									"\t" + $"isOnCenterPlatform={isOnCenterPlatform}"
								);
							}
#endif
							ProcessItemPedBicycle(
#if DEBUG
							debug, unitId,
#endif
								item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextLeftSegmentId, ref netManager.m_segments.m_buffer[nextLeftSegmentId], nextNodeId, ref nextNode, leftLaneIndex, leftLaneId, ref netManager.m_lanes.m_buffer[leftLaneId], connectOffset, connectOffset);
						}

						if (rightLaneId != 0 && rightLaneId != leftLaneId && (nextRightSegmentId != prevSegmentId || canCrossStreet || isOnCenterPlatform)) {
#if DEBUG
							if (debug) {
								Debug(unitId, item, $"ProcessItemMain: ped -> ped: Exploring right pedestrian lane\n" +
									"\t" + $"leftLaneId={leftLaneId}\n" +
									"\t" + $"rightLaneId={rightLaneId}\n" +
									"\t" + $"nextRightSegmentId={nextRightSegmentId}\n" +
									"\t" + $"canCrossStreet={canCrossStreet}\n" +
									"\t" + $"isOnCenterPlatform={isOnCenterPlatform}"
								);
							}
#endif
							ProcessItemPedBicycle(
#if DEBUG
							debug, unitId,
#endif
								item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextRightSegmentId, ref netManager.m_segments.m_buffer[nextRightSegmentId], nextNodeId, ref nextNode, rightLaneIndex, rightLaneId, ref netManager.m_lanes.m_buffer[rightLaneId], connectOffset, connectOffset);
						}

						// switch from bicycle lane to pedestrian lane
						int nextLaneIndex;
						uint nextLaneId;
						if ((m_vehicleTypes & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None &&
							prevSegment.GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Bicycle, out nextLaneIndex, out nextLaneId)) {
#if DEBUG
							if (debug) {
								Debug(unitId, item, $"ProcessItemMain: bicycle -> ped: Exploring bicycle switch\n" +
									"\t" + $"leftLaneId={leftLaneId}\n" +
									"\t" + $"rightLaneId={rightLaneId}\n" +
									"\t" + $"nextRightSegmentId={nextRightSegmentId}\n" +
									"\t" + $"canCrossStreet={canCrossStreet}\n" +
									"\t" + $"isOnCenterPlatform={isOnCenterPlatform}"
								);
							}
#endif
							ProcessItemPedBicycle(
#if DEBUG
							debug, unitId,
#endif
								item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, prevSegmentId, ref prevSegment, nextNodeId, ref nextNode, nextLaneIndex, nextLaneId, ref netManager.m_lanes.m_buffer[nextLaneId], connectOffset, connectOffset);
						}
					} else {
#if DEBUG
						if (debug) {
							Debug(unitId, item, $"ProcessItemMain: beautification -> ped: Exploring pedestrian lane to beautficiation node\n" +
								"\t" + $"nextNodeId={nextNodeId}"
							);
						}
#endif

						// we are going from pedestrian lane to a beautification node
						for (int j = 0; j < 8; j++) {
							ushort nextSegmentId = nextNode.GetSegment(j);
							if (nextSegmentId != 0 && nextSegmentId != prevSegmentId) {
#if DEBUG
								if (debug) {
									Debug(unitId, item, $"ProcessItemMain: beautification -> ped: Exploring next segment behind beautification node\n" +
										"\t" + $"nextSegmentId={nextSegmentId}"
									);
								}
#endif

								ProcessItemCosts(
#if DEBUG
									debug, unitId,
#endif
									item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextNodeId, ref nextNode, false, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, false, true);
							}
						}
					}

					// prepare switching from a vehicle to pedestrian lane
					NetInfo.LaneType nextLaneType = m_laneTypes & ~NetInfo.LaneType.Pedestrian;
					VehicleInfo.VehicleType nextVehicleType = m_vehicleTypes & ~VehicleInfo.VehicleType.Bicycle;
					if ((item.m_lanesUsed & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None) {
						nextLaneType &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
					}

#if DEBUG
					if (debug) {
						Debug(unitId, item, $"ProcessItemMain: vehicle -> ped: Prepared parameters\n" +
							"\t" + $"m_queueItem.vehicleType={m_queueItem.vehicleType}\n" +
							"\t" + $"nextVehicleType={nextVehicleType}\n" +
							"\t" + $"nextLaneType={nextLaneType}"
						);
					}
#endif

					// NON-STOCK CODE START
					bool parkingAllowed = true;

#if PARKINGAI
					// Parking AI: Determine if parking is allowed
					if (Options.prohibitPocketCars) {
#if DEBUG
						if (debug) {
							Debug(unitId, item, $"ProcessItemMain: vehicle -> ped: Parking AI: Determining if parking is allowed here\n" +
								"\t" + $"m_queueItem.vehicleType={m_queueItem.vehicleType}\n" +
								"\t" + $"nextVehicleType={nextVehicleType}\n" +
								"\t" + $"nextLaneType={nextLaneType}\n" +
								"\t" + $"item.m_lanesUsed={item.m_lanesUsed}\n" +
								"\t" + $"m_endLaneA={m_endLaneA}\n" +
								"\t" + $"m_endLaneB={m_endLaneB}"
							);
						}
#endif

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

#if DEBUG
						if (debug) {
							Debug(unitId, item, $"ProcessItemMain: vehicle -> ped: Parking AI: Parking allowed here? {parkingAllowed}");
						}
#endif
					}
#endif
					// NON-STOCK CODE END

					int sameSegLaneIndex;
					uint sameSegLaneId;
					if (parkingAllowed && // NON-STOCK CODE
						nextLaneType != NetInfo.LaneType.None &&
						nextVehicleType != VehicleInfo.VehicleType.None &&
						prevSegment.GetClosestLane(prevLaneIndex, nextLaneType, nextVehicleType, out sameSegLaneIndex, out sameSegLaneId)
					) {
						NetInfo.Lane sameSegLaneInfo = prevSegmentInfo.m_lanes[sameSegLaneIndex];
						byte sameSegConnectOffset = (byte)(((prevSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None == ((sameSegLaneInfo.m_finalDirection & NetInfo.Direction.Backward) != NetInfo.Direction.None)) ? 1 : 254);
						BufferItem nextItem = item;
						if (m_randomParking) {
							nextItem.m_comparisonValue += (float)m_pathRandomizer.Int32(300u) / m_maxLength;
						}

#if DEBUG
						if (debug) {
							Debug(unitId, item, $"ProcessItemMain: vehicle -> ped: Exploring parking\n" +
								"\t" + $"nextLaneType={nextLaneType}\n" +
								"\t" + $"nextVehicleType={nextVehicleType}\n" +
								"\t" + $"nextLaneType={nextLaneType}\n" +
								"\t" + $"sameSegConnectOffset={sameSegConnectOffset}\n" +
								"\t" + $"m_randomParking={m_randomParking}"
							);
						}
#endif

						ProcessItemPedBicycle(
#if DEBUG
							debug, unitId,
#endif
							nextItem, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, prevSegmentId, ref prevSegment, nextNodeId, ref nextNode, sameSegLaneIndex, sameSegLaneId, ref netManager.m_lanes.m_buffer[sameSegLaneId], sameSegConnectOffset, 128);
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
#if DEBUG
						if (debug) {
							Debug(unitId, item, $"ProcessItemMain: ped -> vehicle: Switching to a bicycle may be allowed here\n" +
								"\t" + $"switchConnectOffset={switchConnectOffset}\n" +
								"\t" + $"allowBicycle={allowBicycle}"
							);
						}
#endif
					} else if (m_vehicleLane != 0) {
						// there is a parked vehicle position
						if (m_vehicleLane != item.m_laneID) {
							// we have not reached the parked vehicle yet
							allowPedestrian = false;
#if DEBUG
							if (debug) {
								Debug(unitId, item, $"ProcessItemMain: ped -> vehicle: Entering a parked vehicle is not allowed here");
							}
#endif
						} else {
							// pedestrian switches to parked vehicle
							switchConnectOffset = m_vehicleOffset;
#if DEBUG
							if (debug) {
								Debug(unitId, item, $"ProcessItemMain: ped -> vehicle: Entering a parked vehicle is allowed here\n" +
									"\t" + $"switchConnectOffset={switchConnectOffset}"
								);
							}
#endif
						}
					} else if (m_stablePath) {
						// enter a bus
						switchConnectOffset = 128;
#if DEBUG
						if (debug) {
							Debug(unitId, item, $"ProcessItemMain: ped -> vehicle: Entering a bus is allowed here\n" +
								"\t" + $"switchConnectOffset={switchConnectOffset}"
							);
						}
#endif
					} else {
						// pocket car spawning
#if PARKINGAI
						if (
							Options.prohibitPocketCars
						) {
#if DEBUG
							if (debug) {
								Debug(unitId, item, $"ProcessItemMain: ped -> vehicle: Parking AI: Determining if spawning pocket cars is allowed\n" +
									"\t" + $"m_queueItem.pathType={m_queueItem.pathType}\n" +
									"\t" + $"prevIsCarLane={prevIsCarLane}\n" +
									"\t" + $"m_queueItem.vehicleType={m_queueItem.vehicleType}\n" +
									"\t" + $"m_startSegmentA={m_startSegmentA}\n" +
									"\t" + $"m_startSegmentB={m_startSegmentB}"
								);
							}
#endif

							if (
								(m_queueItem.pathType == ExtCitizenInstance.ExtPathType.WalkingOnly && prevIsCarLane) || 
								(
									m_queueItem.pathType == ExtCitizenInstance.ExtPathType.DrivingOnly &&
									m_queueItem.vehicleType == ExtVehicleType.PassengerCar &&
									((item.m_position.m_segment != m_startSegmentA && item.m_position.m_segment != m_startSegmentB) || !prevIsCarLane)
								)
							) {
								/* allow pocket cars only if an instant driving path is required and we are at the start segment */
								/* disallow pocket cars on walking paths */
								allowPedestrian = false;

#if DEBUG
								if (debug) {
									Debug(unitId, item, $"ProcessItemMain: ped -> vehicle: Parking AI: Spawning pocket cars is not allowed here");
								}
#endif
							} else {
								switchConnectOffset = (byte)m_pathRandomizer.UInt32(1u, 254u);

#if DEBUG
								if (debug) {
									Debug(unitId, item, $"ProcessItemMain: ped -> vehicle: Parking AI: Spawning pocket cars is allowed here\n" +
										"\t" + $"switchConnectOffset={switchConnectOffset}"
									);
								}
#endif
							}
						} else {
#endif
							switchConnectOffset = (byte)m_pathRandomizer.UInt32(1u, 254u);
#if DEBUG
							if (debug) {
								Debug(unitId, item, $"ProcessItemMain: ped -> vehicle: Spawning pocket cars is allowed here\n" +
									"\t" + $"switchConnectOffset={switchConnectOffset}"
								);
							}
#endif

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

#if DEBUG
					if (debug) {
						Debug(unitId, item, $"ProcessItemMain: vehicle -> vehicle: Exploring ferry routes");
					}
#endif

					bool isUturnAllowedHere = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None;
					for (int k = 0; k < 8; k++) {
						nextSegmentId = nextNode.GetSegment(k);
						if (nextSegmentId != 0 && nextSegmentId != prevSegmentId) {
#if DEBUG
							if (debug) {
								Debug(unitId, item, $"ProcessItemMain: vehicle -> vehicle: Exploring ferry route\n" +
									"\t" + $"nextSegmentId={nextSegmentId}"
								);
							}
#endif

							ProcessItemCosts(
#if DEBUG
							debug, unitId,
#endif
								item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextNodeId, ref nextNode, false, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, true, allowBicycle);
						}
					}

					if (isUturnAllowedHere
#if !ROUTING
						&& (m_vehicleTypes & VehicleInfo.VehicleType.Monorail) == VehicleInfo.VehicleType.None
#endif
						) {
#if DEBUG
						if (debug) {
							Debug(unitId, item, $"ProcessItemMain: vehicle -> vehicle: Exploring ferry u-turn");
						}
#endif

						nextSegmentId = prevSegmentId;
						ProcessItemCosts(
#if DEBUG
							debug, unitId,
#endif
							item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextNodeId, ref nextNode, false, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset, true, false);
					}
				} else {
					// road vehicles / trams / trains / metros (/ monorails) / etc.
#if DEBUG
					if (debug) {
						Debug(unitId, item, $"ProcessItemMain: vehicle -> vehicle: Exploring vehicle routes");
					}
#endif


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
#if DEBUG
						if (debug) {
							Debug(unitId, item, $"ProcessItemMain: vehicle -> vehicle: Is previous segment routed? {prevIsRouted}");
						}
#endif
					}

					if (allowBicycle || !prevIsRouted) {
						/*
						* pedestrian to bicycle lane switch or no routing information available:
						*		if pedestrian lanes should be explored (allowBicycle == true): do this here
						*		if previous segment has custom routing (prevIsRouted == true): do NOT explore vehicle lanes here, else: vanilla exploration of vehicle lanes
						*/

#if DEBUG
						if (debug) {
							Debug(unitId, item, $"ProcessItemMain: bicycle -> vehicle / stock vehicle routing\n"
								+ "\t" + $"prevIsRouted={prevIsRouted}\n"
								+ "\t" + $"allowBicycle={allowBicycle}"
							);
						}
#endif

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

#if DEBUG
							if (debug) {
								Debug(unitId, item, $"ProcessItemMain: bicycle -> vehicle / stock vehicle routing: exploring next segment\n"
									+ "\t" + $"nextSegmentId={nextSegmentId}"
								);
							}
#endif

							if (ProcessItemCosts(
#if DEBUG
								debug, unitId,
#endif
								item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextNodeId, ref nextNode, false, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], ref prevRelSimilarLaneIndex, connectOffset,
#if ROUTING
								!prevIsRouted // NON-STOCK CODE
#else
								true
#endif
								, allowBicycle)
							) {
								exploreUturn = true; // allow exceptional u-turns
#if DEBUG
								if (debug) {
									Debug(unitId, item, $"ProcessItemMain: bicycle -> vehicle / stock vehicle routing: exceptional u-turn allowed\n"
										+ "\t" + $"nextSegmentId={nextSegmentId}"
									);
								}
#endif
							}

							nextSegmentId = netManager.m_segments.m_buffer[nextSegmentId].GetRightSegment(nextNodeId);
						}
#if ROUTING
					} // NON-STOCK CODE
#endif

#if DEBUG
					if (debug) {
						Debug(unitId, item, $"ProcessItemMain: vehicle -> vehicle: Custom routing\n"
							+ "\t" + $"Options.advancedAI={Options.advancedAI}\n"
							+ "\t" + $"prevIsRouted={prevIsRouted}\n"
							+ "\t" + $"m_isRoadVehicle={m_isRoadVehicle}\n"
							+ "\t" + $"prevIsCarLane={prevIsCarLane}\n"
							+ "\t" + $"m_stablePath={Options.advancedAI}"
						);
					}
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
						prevIsRouted &&
						m_isRoadVehicle &&
						prevIsCarLane
					) {
						enableAdvancedAI = true;
						if (!m_stablePath) {
							CalculateAdvancedAiCostFactors(
#if DEBUG
								debug, unitId,
#endif
								ref item, ref prevSegment, ref prevLane, nextNodeId, ref nextNode, ref segmentSelectionCost, ref laneSelectionCost, ref laneChangingCost
							);

#if DEBUG
							if (debug) {
								Debug(unitId, item, $"ProcessItemMain: vehicle -> vehicle: Custom routing with activated Advanced Vehicle AI: Calculated cost factors\n"
									+ "\t" + $"segmentSelectionCost={segmentSelectionCost}\n"
									+ "\t" + $"laneSelectionCost={laneSelectionCost}\n"
									+ "\t" + $"laneChangingCost={laneChangingCost}"
								);
							}
#endif
						} else {
#if DEBUG
							if (debug) {
								Debug(unitId, item, $"ProcessItemMain: vehicle -> vehicle: Custom routing with activated Advanced Vehicle AI and stable path: Using default cost factors\n"
									+ "\t" + $"segmentSelectionCost={segmentSelectionCost}\n"
									+ "\t" + $"laneSelectionCost={laneSelectionCost}\n"
									+ "\t" + $"laneChangingCost={laneChangingCost}"
								);
							}
#endif
						}
					}
#endif

#if ROUTING
					if (prevIsRouted) {
#if DEBUG
						if (debug) {
							Debug(unitId, item, $"ProcessItemMain: vehicle -> vehicle: Custom routing: Exploring custom routes");
						}
#endif

						exploreUturn = false; // custom routing processes regular u-turns
						if (ProcessItemRouted(
#if DEBUG
							debug, unitId,
#endif
							item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed
#if ADVANCEDAI
							, enableAdvancedAI, laneChangingCost,
#endif
							segmentSelectionCost, laneSelectionCost, nextNodeId, ref nextNode, false, m_routingManager.segmentRoutings[prevSegmentId], m_routingManager.laneEndBackwardRoutings[laneRoutingIndex], connectOffset, prevRelSimilarLaneIndex
						)) {
							exploreUturn = true; // allow exceptional u-turns
						}
					} else {
#if DEBUG
						if (debug) {
							Debug(unitId, item, $"ProcessItemMain: vehicle -> vehicle: Custom routing: No custom routing present");
						}
#endif

						if (!exploreUturn) {
							// no exceptional u-turns allowed: allow regular u-turns
							exploreUturn = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;

#if DEBUG
							if (debug) {
								Debug(unitId, item, $"ProcessItemMain: vehicle -> vehicle: Custom routing: Allowing regular u-turns:\n"
									+ "\t" + $"exploreUturn={exploreUturn}\n"
								);
							}
#endif
						}
					}
#endif

					if (exploreUturn && (m_vehicleTypes & VehicleInfo.VehicleType.Tram) == VehicleInfo.VehicleType.None) {
#if DEBUG
						if (debug) {
							Debug(unitId, item, $"ProcessItemMain: vehicle -> vehicle: Exploring stock u-turn\n"
								+ "\t" + $"exploreUturn={exploreUturn}\n"
							);
						}
#endif

						ProcessItemCosts(
#if DEBUG
							debug, unitId,
#endif
							item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed,
#if ADVANCEDAI && ROUTING
							false, 0f,
#endif
							nextNodeId, ref nextNode, false, prevSegmentId, ref prevSegment,
#if ROUTING
							segmentSelectionCost, laneSelectionCost, null,
#endif
							ref prevRelSimilarLaneIndex, connectOffset, true, false
						);
					}
				}

				if (allowPedestrian) {
					int nextLaneIndex;
					uint nextLaneId;
					if (prevSegment.GetClosestLane((int)item.m_position.m_lane, NetInfo.LaneType.Pedestrian, m_vehicleTypes, out nextLaneIndex, out nextLaneId)) {
#if DEBUG
						if (debug) {
							Debug(unitId, item, $"ProcessItemMain: ped -> vehicle: Exploring switch\n"
								+ "\t" + $"nextLaneIndex={nextLaneIndex}\n"
								+ "\t" + $"nextLaneId={nextLaneId}"
							);
						}
#endif

						ProcessItemPedBicycle(
#if DEBUG
							debug, unitId,
#endif
							item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, prevSegmentId, ref prevSegment, nextNodeId, ref nextNode, nextLaneIndex, nextLaneId, ref netManager.m_lanes.m_buffer[nextLaneId], switchConnectOffset, switchConnectOffset);
					}
				}
			}

			if (nextNode.m_lane != 0) {
				bool targetDisabled = (nextNode.m_flags & (NetNode.Flags.Disabled | NetNode.Flags.DisableOnlyMiddle)) == NetNode.Flags.Disabled;
				ushort nextSegmentId = netManager.m_lanes.m_buffer[nextNode.m_lane].m_segment;
				if (nextSegmentId != 0 && nextSegmentId != prevSegmentId) {
#if DEBUG
					if (debug) {
						Debug(unitId, item, $"ProcessItemMain: transport -> *: Exploring special node\n"
							+ "\t" + $"nextSegmentId={nextSegmentId}\n"
							+ "\t" + $"nextNode.m_lane={nextNode.m_lane}\n"
							+ "\t" + $"targetDisabled={targetDisabled}\n"
							+ "\t" + $"nextNodeId={nextNodeId}"
						);
					}
#endif

					ProcessItemPublicTransport(
#if DEBUG
						debug, unitId,
#endif
						item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed, nextNodeId, targetDisabled, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], nextNode.m_lane, nextNode.m_laneOffset, connectOffset);
				}
			}
		}

		// 2
		private void ProcessItemPublicTransport(
#if DEBUG
			bool debug, uint unitId,
#endif
			BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, float prevMaxSpeed, float prevLaneSpeed, ushort nextNodeId, bool targetDisabled, ushort nextSegmentId, ref NetSegment nextSegment, uint nextLaneId, byte offset, byte connectOffset) {

#if DEBUG
			if (debug) {
				Debug(unitId, item, nextSegmentId, $"ProcessItemPublicTransport called.\n"
					+ "\t" + $"prevMaxSpeed={prevMaxSpeed}\n"
					+ "\t" + $"prevLaneSpeed={prevLaneSpeed}\n"
					+ "\t" + $"nextNodeId={nextNodeId}\n"
					+ "\t" + $"targetDisabled={targetDisabled}\n"
					+ "\t" + $"nextLaneId={nextLaneId}\n"
					+ "\t" + $"offset={offset}\n"
					+ "\t" + $"connectOffset={connectOffset}"
				);
			}
#endif

			if ((nextSegment.m_flags & m_disableMask) != NetSegment.Flags.None) {
#if DEBUG
				if (debug) {
					Debug(unitId, item, nextSegmentId, $"ProcessItemPublicTransport: Aborting: Disable mask\n"
						+ "\t" + $"m_disableMask={m_disableMask}\n"
						+ "\t" + $"nextSegment.m_flags={nextSegment.m_flags}\n");
				}
#endif
				return;
			}

			NetManager netManager = Singleton<NetManager>.instance;
			if (targetDisabled && ((netManager.m_nodes.m_buffer[nextSegment.m_startNode].m_flags | netManager.m_nodes.m_buffer[nextSegment.m_endNode].m_flags) & NetNode.Flags.Disabled) == NetNode.Flags.None) {
#if DEBUG
				if (debug) {
					Debug(unitId, item, nextSegmentId, $"ProcessItemPublicTransport: Aborting: Target disabled");
				}
#endif
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
#if DEBUG
				if (debug) {
					Debug(unitId, item, nextSegmentId, $"ProcessItemPublicTransport: Aborting: Next lane not found");
				}
#endif
				return;
			}

#if DEBUG
			if (debug) {
				Debug(unitId, item, nextSegmentId, nextLaneIndex, curLaneId, $"ProcessItemPublicTransport: Exploring next lane\n"
					+ "\t" + $"nextLaneIndex={nextLaneIndex}\n"
					+ "\t" + $"nextLaneId={nextLaneId}"
				);
			}
#endif

			NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];
			if (nextLaneInfo.CheckType(m_laneTypes, m_vehicleTypes)) {

#if DEBUG
				if (debug) {
					Debug(unitId, item, nextSegmentId, nextLaneIndex, curLaneId, $"ProcessItemPublicTransport: Next lane compatible\n"
						+ "\t" + $"nextLaneInfo.m_vehicleType={nextLaneInfo.m_vehicleType}\n"
						+ "\t" + $"nextLaneInfo.m_laneType={nextLaneInfo.m_laneType}"
					);
				}
#endif

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
#if DEBUG
					if (debug) {
						Debug(unitId, item, nextSegmentId, nextLaneIndex, curLaneId, $"ProcessItemPublicTransport: Aborting: Max. walking distance exceeded\n"
							+ "\t" + $"nextItem.m_methodDistance={nextItem.m_methodDistance}"
						);
					}
#endif
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
#if DEBUG
						if (debug) {
							Debug(unitId, item, nextSegmentId, nextLaneIndex, curLaneId, $"ProcessItemPublicTransport: Aborting: Invalid offset/direction on start lane A\n"
								+ "\t" + $"nextItem.m_direction={nextItem.m_direction}\n"
								+ "\t" + $"nextItem.m_position.m_offset={nextItem.m_position.m_offset}\n"
								+ "\t" + $"m_startOffsetA={m_startOffsetA}"
							);
						}
#endif
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
#if DEBUG
						if (debug) {
							Debug(unitId, item, nextSegmentId, nextLaneIndex, curLaneId, $"ProcessItemPublicTransport: Aborting: Invalid offset/direction on start lane B\n"
								+ "\t" + $"nextItem.m_direction={nextItem.m_direction}\n"
								+ "\t" + $"nextItem.m_position.m_offset={nextItem.m_position.m_offset}\n"
								+ "\t" + $"m_startOffsetB={m_startOffsetB}"
							);
						}
#endif
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

#if DEBUG
				if (debug) {
					Debug(unitId, item, nextSegmentId, nextLaneIndex, curLaneId, $"ProcessItemPublicTransport: Adding next item\n"
						+ "\t" + $"nextItem={nextItem}"
					);
				}
#endif

				AddBufferItem(
#if DEBUG
					debug,
#endif
					nextItem, item.m_position
				);
			}
		}

#if ADVANCEDAI && ROUTING
		// 3a (non-routed, no adv. AI)
		private bool ProcessItemCosts(
#if DEBUG
			bool debug, uint unitId,
#endif
			BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, float prevMaxSpeed, float prevLaneSpeed, ushort nextNodeId, ref NetNode nextNode, bool isMiddle, ushort nextSegmentId, ref NetSegment nextSegment, ref int laneIndexFromInner, byte connectOffset, bool enableVehicle, bool enablePedestrian) {
			return ProcessItemCosts(
#if DEBUG
				debug, unitId,
#endif
				item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed,
#if ADVANCEDAI && ROUTING
				false, 0f,
#endif
				nextNodeId, ref nextNode, isMiddle, nextSegmentId, ref nextSegment,
#if ROUTING
				1f, 1f, null,
#endif
			ref laneIndexFromInner, connectOffset, enableVehicle, enablePedestrian);
		}
#endif

		// 3b
		private bool ProcessItemCosts(
#if DEBUG
			bool debug, uint unitId,
#endif
			BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, float prevMaxSpeed, float prevLaneSpeed,
#if ADVANCEDAI && ROUTING
			bool enableAdvancedAI, float laneChangingCost,
#endif
			ushort nextNodeId, ref NetNode nextNode, bool isMiddle, ushort nextSegmentId, ref NetSegment nextSegment,
#if ROUTING
			float segmentSelectionCost, float laneSelectionCost, LaneTransitionData? transition,
#endif
			ref int laneIndexFromInner, byte connectOffset, bool enableVehicle, bool enablePedestrian
		) {

#if DEBUG
			if (debug) {
				Debug(unitId, item, nextSegmentId, $"ProcessItemCosts called.\n"
					+ "\t" + $"prevMaxSpeed={prevMaxSpeed}\n"
					+ "\t" + $"prevLaneSpeed={prevLaneSpeed}\n"
#if ADVANCEDAI && ROUTING
					+ "\t" + $"enableAdvancedAI={enableAdvancedAI}\n"
					+ "\t" + $"laneChangingCost={laneChangingCost}\n"
#endif
					+ "\t" + $"nextNodeId={nextNodeId}\n"
					+ "\t" + $"isMiddle={isMiddle}\n"
					+ "\t" + $"nextSegmentId={nextSegmentId}\n"
#if ROUTING
					+ "\t" + $"segmentSelectionCost={segmentSelectionCost}\n"
					+ "\t" + $"laneSelectionCost={laneSelectionCost}\n"
					+ "\t" + $"transition={transition}\n"
#endif
					+ "\t" + $"laneIndexFromInner={laneIndexFromInner}\n"
					+ "\t" + $"connectOffset={connectOffset}\n"
					+ "\t" + $"enableVehicle={enableVehicle}\n"
					+ "\t" + $"enablePedestrian={enablePedestrian}"
				);
			}
#endif

			bool blocked = false;
			if ((nextSegment.m_flags & m_disableMask) != NetSegment.Flags.None) {
#if DEBUG
				if (debug) {
					Debug(unitId, item, nextSegmentId, $"ProcessItemCosts: Aborting: Disable mask\n"
						+ "\t" + $"m_disableMask={m_disableMask}\n"
						+ "\t" + $"nextSegment.m_flags={nextSegment.m_flags}\n");
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
			// float prevMaxSpeed = 1f; // stock code commented
			// float prevLaneSpeed = 1f; // stock code commented
			NetInfo.LaneType prevLaneType = NetInfo.LaneType.None;
			VehicleInfo.VehicleType prevVehicleType = VehicleInfo.VehicleType.None;
			if (item.m_position.m_lane < prevSegmentInfo.m_lanes.Length) {
				NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[item.m_position.m_lane];
				prevLaneType = prevLaneInfo.m_laneType;
				prevVehicleType = prevLaneInfo.m_vehicleType;
				// prevMaxSpeed = prevLaneInfo.m_speedLimit; // stock code commented
				// prevLaneSpeed = CalculateLaneSpeed(prevMaxSpeed, connectOffset, item.m_position.m_offset, ref prevSegment, prevLaneInfo); // stock code commented
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

#if DEBUG
					if (debug) {
						Debug(unitId, item, nextSegmentId, $"ProcessItemCosts: Applied stock segment randomization cost factor\n"
							+ "\t" + $"offsetLength={offsetLength}"
						);
					}
#endif
#if ADVANCEDAI && ROUTING
				}
#endif
			}

			if ((prevLaneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None && (prevVehicleType & m_vehicleTypes) == VehicleInfo.VehicleType.Car && (prevSegment.m_flags & m_carBanMask) != NetSegment.Flags.None) {
				offsetLength *= 7.5f;

#if DEBUG
				if (debug) {
					Debug(unitId, item, nextSegmentId, $"ProcessItemCosts: Applied stock car ban cost factor\n"
						+ "\t" + $"offsetLength={offsetLength}"
					);
				}
#endif
			}

			if (m_transportVehicle && prevLaneType == NetInfo.LaneType.TransportVehicle) {
				offsetLength *= 0.95f;

#if DEBUG
				if (debug) {
					Debug(unitId, item, nextSegmentId, $"ProcessItemCosts: Applied stock transport vehicle cost factor\n"
						+ "\t" + $"offsetLength={offsetLength}"
					);
				}
#endif
			}

#if ROUTING
#if DEBUG
			if (debug) {
				Debug(unitId, item, nextSegmentId, $"ProcessItemCosts: Applying custom selection cost factors\n"
					+ "\t" + $"offsetLength={offsetLength}\n"
					+ "\t" + $"segmentSelectionCost={segmentSelectionCost}\n"
					+ "\t" + $"laneSelectionCost={laneSelectionCost}\n"
				);
			}
#endif
			offsetLength *= segmentSelectionCost;
			offsetLength *= laneSelectionCost;
#if DEBUG
			if (debug) {
				Debug(unitId, item, nextSegmentId, $"ProcessItemCosts: Applied custom selection cost factors\n"
					+ "\t" + $"offsetLength={offsetLength}"
				);
			}
#endif
#endif

			float baseLength = offsetLength / (prevLaneSpeed * m_maxLength); // NON-STOCK CODE
			float comparisonValue = item.m_comparisonValue; // NON-STOCK CODE
#if ROUTING
#if DEBUG
			if (debug) {
				Debug(unitId, item, nextSegmentId, $"ProcessItemCosts: Calculated base length\n"
					+ "\t" + $"baseLength={baseLength}"
				);
			}
#endif
			if (
#if ADVANCEDAI
				!enableAdvancedAI &&
#endif
				!m_stablePath) {
				comparisonValue += baseLength;
			}
#endif
			int ticketCost = prevLane.m_ticketCost;
			if (!m_ignoreCost && ticketCost != 0) {
				comparisonValue += (float)(ticketCost * m_pathRandomizer.Int32(2000u)) * TICKET_COST_CONVERSION_FACTOR;
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

#if DEBUG
			if (debug) {
				Debug(unitId, item, nextSegmentId, $"ProcessItemCosts: Shall apply transport transfer penalty?\n"
					+ "\t" + $"applyTransportTransferPenalty={applyTransportTransferPenalty}\n"
					+ "\t" + $"Options.realisticPublicTransport={Options.realisticPublicTransport}\n"
					+ "\t" + $"allowedLaneTypes={allowedLaneTypes}\n"
					+ "\t" + $"allowedVehicleTypes={allowedVehicleTypes}\n"
					+ "\t" + $"m_conf.PathFinding.PublicTransportTransitionMinPenalty={m_conf.PathFinding.PublicTransportTransitionMinPenalty}\n"
					+ "\t" + $"m_conf.PathFinding.PublicTransportTransitionMaxPenalty={m_conf.PathFinding.PublicTransportTransitionMaxPenalty}"
				);
			}
#endif

			int nextLaneIndex = 0;
			uint nextLaneId = nextSegment.m_lanes;
			int maxNextLaneIndex = nextNumLanes - 1;
#if ADVANCEDAI && ROUTING
			byte laneDist = 0;
#endif
#if ROUTING
			if (transition != null) {
				LaneTransitionData trans = (LaneTransitionData)transition;
				if (trans.laneIndex >= 0 && trans.laneIndex <= maxNextLaneIndex) {
					nextLaneIndex = trans.laneIndex;
					nextLaneId = trans.laneId;
					maxNextLaneIndex = nextLaneIndex;
				} else {
#if DEBUG
					if (debug) {
						Debug(unitId, item, nextSegmentId, $"ProcessItemCosts: Invalid transition detected. Skipping.");
					}
#endif
					return blocked;
				}

				laneDist = trans.distance;
#if DEBUG
				if (debug) {
					Debug(unitId, item, nextSegmentId, $"ProcessItemCosts: Custom transition given\n"
						+ "\t" + $"nextLaneIndex={nextLaneIndex}\n"
						+ "\t" + $"nextLaneId={nextLaneId}\n"
						+ "\t" + $"maxNextLaneIndex={maxNextLaneIndex}\n"
						+ "\t" + $"laneDist={laneDist}"
					);
				}
#endif
			} else {
#if DEBUG
				if (debug) {
					Debug(unitId, item, nextSegmentId, $"ProcessItemCosts: No custom transition given");
				}
#endif
			}
#endif
			// NON-STOCK CODE END

			for (; nextLaneIndex <= maxNextLaneIndex && nextLaneId != 0; nextLaneIndex++) {
				NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];
				if ((nextLaneInfo.m_finalDirection & nextFinalDir) != NetInfo.Direction.None) {
					if (nextLaneInfo.CheckType(allowedLaneTypes, allowedVehicleTypes) && (nextSegmentId != item.m_position.m_segment || nextLaneIndex != item.m_position.m_lane)) {
						if (acuteTurningAngle && nextLaneInfo.m_laneType == NetInfo.LaneType.Vehicle && (nextLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) == VehicleInfo.VehicleType.None) {
							continue;
						}

						BufferItem nextItem = default(BufferItem);

						Vector3 a = ((nextDir & NetInfo.Direction.Forward) == NetInfo.Direction.None) ? netManager.m_lanes.m_buffer[nextLaneId].m_bezier.a : netManager.m_lanes.m_buffer[nextLaneId].m_bezier.d;
						float transitionCost = Vector3.Distance(a, b);
						if (isTransition) {
							transitionCost *= 2f;
						}
						if (ticketCost != 0 && netManager.m_lanes.m_buffer[nextLaneId].m_ticketCost != 0) {
							transitionCost *= 10f;
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
#if ADVANCEDAI && ROUTING
						if (!enableAdvancedAI) {
#endif
							if (!this.m_stablePath && (netManager.m_lanes.m_buffer[nextLaneId].m_flags & (ushort)NetLane.Flags.Merge) != 0) {
								int firstTarget = netManager.m_lanes.m_buffer[nextLaneId].m_firstTarget;
								int lastTarget = netManager.m_lanes.m_buffer[nextLaneId].m_lastTarget;
								transitionCostOverMeanMaxSpeed *= (float)new Randomizer(this.m_pathFindIndex ^ nextLaneId).Int32(1000, (lastTarget - firstTarget + 2) * 1000) * 0.001f;
							}
#if ADVANCEDAI && ROUTING
						}
#endif
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

#if DEBUG
								if (debug) {
									Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemCosts: Applied transport transfer penalty on PT change\n"
										+ "\t" + $"transportTransitionPenalty={transportTransitionPenalty}\n"
										+ "\t" + $"transitionCostOverMeanMaxSpeed={transitionCostOverMeanMaxSpeed}\n"
										+ "\t" + $"isMiddle={isMiddle}\n"
										+ "\t" + $"nextLaneInfo.m_laneType={nextLaneInfo.m_laneType}\n"
										+ "\t" + $"prevLaneType={prevLaneType}\n"
										+ "\t" + $"item.m_lanesUsed={item.m_lanesUsed}\n"
										+ "\t" + $"nextLaneInfo.m_laneType={nextLaneInfo.m_laneType}"
									);
								}
#endif
							} else if (
								(nextLaneId == m_startLaneA || nextLaneId == m_startLaneB) &&
								(item.m_lanesUsed & (NetInfo.LaneType.Pedestrian | NetInfo.LaneType.PublicTransport)) == NetInfo.LaneType.Pedestrian
							) {
								// account for public tranport transition costs on non-PT paths
								float transportTransitionPenalty = (2f * m_conf.PathFinding.PublicTransportTransitionMaxPenalty) / (0.5f * this.m_maxLength);
								transitionCostOverMeanMaxSpeed += transportTransitionPenalty;
#if DEBUG
								if (debug) {
									Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemCosts: Applied transport transfer penalty on non-PT path\n"
										+ "\t" + $"transportTransitionPenalty={transportTransitionPenalty}\n"
										+ "\t" + $"transitionCostOverMeanMaxSpeed={transitionCostOverMeanMaxSpeed}"
									);
								}
#endif
							}
						}
						// NON-STOCK CODE END

						nextItem.m_comparisonValue = comparisonValue + transitionCostOverMeanMaxSpeed;
						nextItem.m_duration = duration + transitionCost / ((prevMaxSpeed + nextMaxSpeed) * 0.5f);
						nextItem.m_direction = nextDir;

						if (nextLaneId == m_startLaneA) {
							if (((nextItem.m_direction & NetInfo.Direction.Forward) == NetInfo.Direction.None || nextItem.m_position.m_offset < m_startOffsetA) &&
								((nextItem.m_direction & NetInfo.Direction.Backward) == NetInfo.Direction.None || nextItem.m_position.m_offset > m_startOffsetA)) {
#if DEBUG
								if (debug) {
									Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemCosts: Skipping: Invalid offset/direction on start lane A\n"
										+ "\t" + $"nextItem.m_direction={nextItem.m_direction}\n"
										+ "\t" + $"nextItem.m_position.m_offset={nextItem.m_position.m_offset}\n"
										+ "\t" + $"m_startOffsetA={m_startOffsetA}"
									);
								}
#endif

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
#if DEBUG
								if (debug) {
									Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemCosts: Skipping: Invalid offset/direction on start lane B\n"
										+ "\t" + $"nextItem.m_direction={nextItem.m_direction}\n"
										+ "\t" + $"nextItem.m_position.m_offset={nextItem.m_position.m_offset}\n"
										+ "\t" + $"m_startOffsetB={m_startOffsetB}"
									);
								}
#endif

								nextLaneId = netManager.m_lanes.m_buffer[nextLaneId].m_nextLane;
								continue;
							}

							float nextLaneSpeed = CalculateLaneSpeed(nextMaxSpeed, m_startOffsetB, nextItem.m_position.m_offset, ref nextSegment, nextLaneInfo);
							float nextOffset = (float)Mathf.Abs(nextItem.m_position.m_offset - m_startOffsetB) * BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

							nextItem.m_comparisonValue += nextOffset * nextSegment.m_averageLength / (nextLaneSpeed * m_maxLength);
							nextItem.m_duration += nextOffset * nextSegment.m_averageLength / nextLaneSpeed;
						}

						if (
							!m_ignoreBlocked && (nextSegment.m_flags & NetSegment.Flags.Blocked) != NetSegment.Flags.None &&
							(nextLaneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None
						) {
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

#if ROUTING
#if ADVANCEDAI
						if (enableAdvancedAI) {
							float adjustedBaseLength = baseLength;
							if (m_queueItem.spawned || (nextLaneId != m_startLaneA && nextLaneId != m_startLaneB)) {
								if (laneDist != 0) {
									// apply lane changing costs
									adjustedBaseLength *=
										1f +
										laneDist *
										laneChangingCost *
										(laneDist > 1 ? m_conf.AdvancedVehicleAI.MoreThanOneLaneChangingCostFactor : 1f); // additional costs for changing multiple lanes at once
								}
							}

							nextItem.m_comparisonValue += adjustedBaseLength;

#if DEBUG
							if (debug) {
								Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemCosts: Applied Advanced Vehicle AI\n"
									+ "\t" + $"baseLength={baseLength}\n"
									+ "\t" + $"adjustedBaseLength={adjustedBaseLength}\n"
									+ "\t" + $"laneDist={laneDist}\n"
									+ "\t" + $"laneChangingCost={laneChangingCost}"
								);
							}
#endif
						} else
#endif
						if (m_stablePath) {
							// all non-road vehicles with stable paths (trains, trams, etc.): apply lane distance factor
							float adjustedBaseLength = baseLength;
							adjustedBaseLength *= 1 + laneDist;
							nextItem.m_comparisonValue += adjustedBaseLength;

#if DEBUG
							if (debug) {
								Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemCosts: Applied stable path lane distance costs\n"
									+ "\t" + $"baseLength={baseLength}\n"
									+ "\t" + $"adjustedBaseLength={adjustedBaseLength}\n"
									+ "\t" + $"laneDist={laneDist}"
								);
							}
#endif
						}
#endif

						if (
							(nextLaneInfo.m_laneType & prevLaneType) != NetInfo.LaneType.None &&
							(nextLaneInfo.m_vehicleType & m_vehicleTypes) != VehicleInfo.VehicleType.None
						) {
#if ADVANCEDAI && ROUTING
							if (! enableAdvancedAI) {
#endif
								int firstTarget = netManager.m_lanes.m_buffer[nextLaneId].m_firstTarget;
								int lastTarget = netManager.m_lanes.m_buffer[nextLaneId].m_lastTarget;
								if (laneIndexFromInner < firstTarget || laneIndexFromInner >= lastTarget) {
									nextItem.m_comparisonValue += Mathf.Max(1f, transitionCost * 3f - 3f) / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * m_maxLength);
								}

#if DEBUG
								if (debug) {
									Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemCosts: stock lane change costs\n"
										+ "\t" + $"firstTarget={firstTarget}\n"
										+ "\t" + $"lastTarget={lastTarget}\n"
										+ "\t" + $"laneIndexFromInner={laneIndexFromInner}"
									);
								}
#endif
#if ADVANCEDAI && ROUTING
							}
#endif

							if (
								!m_transportVehicle &&
								nextLaneInfo.m_laneType == NetInfo.LaneType.TransportVehicle
							) {
								nextItem.m_comparisonValue += 20f / ((prevMaxSpeed + nextMaxSpeed) * 0.5f * m_maxLength);
							}
						}

#if DEBUG
						if (debug) {
							Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemCosts: Adding next item\n"
								+ "\t" + $"nextItem={nextItem}"
							);
						}
#endif

						AddBufferItem(
#if DEBUG
							debug,
#endif
							nextItem, item.m_position
						);
					} else {
#if DEBUG
						if (debug) {
							Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemCosts: Lane type and/or vehicle type mismatch or same segment/lane. Skipping."
								+ "\t" + $"allowedLaneTypes={allowedLaneTypes}\n"
								+ "\t" + $"allowedVehicleTypes={allowedVehicleTypes}"
							);
						}
#endif
					}
				} else {
#if DEBUG
					if (debug) {
						Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemCosts: Lane direction mismatch. Skipping."
							+ "\t" + $"nextLaneInfo.m_finalDirection={nextLaneInfo.m_finalDirection}\n"
							+ "\t" + $"nextFinalDir={nextFinalDir}"
						);
					}
#endif

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
		private void ProcessItemPedBicycle(
#if DEBUG
			bool debug, uint unitId,
#endif
			BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, float prevMaxSpeed, float prevLaneSpeed, ushort nextSegmentId, ref NetSegment nextSegment, ushort nextNodeId, ref NetNode nextNode, int nextLaneIndex, uint nextLaneId, ref NetLane nextLane, byte connectOffset, byte laneSwitchOffset) {

#if DEBUG
			if (debug) {
				Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemPedBicycle called.\n"
					+ "\t" + $"prevMaxSpeed={prevMaxSpeed}\n"
					+ "\t" + $"prevLaneSpeed={prevLaneSpeed}\n"
					+ "\t" + $"nextSegmentId={nextSegmentId}\n"
					+ "\t" + $"nextNodeId={nextNodeId}\n"
					+ "\t" + $"nextLaneIndex={nextLaneIndex}\n"
					+ "\t" + $"nextLaneId={nextLaneId}\n"
					+ "\t" + $"connectOffset={connectOffset}\n"
					+ "\t" + $"laneSwitchOffset={laneSwitchOffset}"
				);
			}
#endif

			if ((nextSegment.m_flags & m_disableMask) != NetSegment.Flags.None) {
#if DEBUG
				if (debug) {
					Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemPedBicycle: Aborting: Disable mask\n"
						+ "\t" + $"m_disableMask={m_disableMask}\n"
						+ "\t" + $"nextSegment.m_flags={nextSegment.m_flags}\n");
				}
#endif
				return;
			}

			// NON-STOCK CODE START
#if JUNCTIONRESTRICTIONS || CUSTOMTRAFFICLIGHTS
			if (Options.junctionRestrictionsEnabled || Options.timedLightsEnabled) {
				bool nextIsStartNode = nextNodeId == nextSegment.m_startNode;
				if (nextIsStartNode || nextNodeId == nextSegment.m_endNode) {
#if JUNCTIONRESTRICTIONS
					if (Options.junctionRestrictionsEnabled && item.m_position.m_segment == nextSegmentId) {
						// check if pedestrians are not allowed to cross here
						if (!m_junctionManager.IsPedestrianCrossingAllowed(nextSegmentId, nextIsStartNode)) {
#if DEBUG
							if (debug) {
								Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemPedBicycle: Aborting: Pedestrian crossing prohibited");
							}
#endif
							return;
						}
					}
#endif

#if CUSTOMTRAFFICLIGHTS
					if (Options.timedLightsEnabled) {
						// check if pedestrian light won't change to green
						ICustomSegmentLights lights = m_customTrafficLightsManager.GetSegmentLights(nextSegmentId, nextIsStartNode, false);
						if (lights != null && lights.InvalidPedestrianLight) {
#if DEBUG
							if (debug) {
								Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemPedBicycle: Aborting: Invalid pedestrian light");
							}
#endif
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
#if DEBUG
					if (debug) {
						Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemPedBicycle: Aborting: Max. walking distance exceeded\n"
							+ "\t" + $"nextItem.m_methodDistance={nextItem.m_methodDistance}"
						);
					}
#endif
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
#if DEBUG
						if (debug) {
							Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemPedBicycle: Aborting: Invalid offset/direction on start lane A\n"
								+ "\t" + $"nextItem.m_direction={nextItem.m_direction}\n"
								+ "\t" + $"nextItem.m_position.m_offset={nextItem.m_position.m_offset}\n"
								+ "\t" + $"m_startOffsetA={m_startOffsetA}"
							);
						}
#endif
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
#if DEBUG
						if (debug) {
							Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemPedBicycle: Aborting: Invalid offset/direction on start lane B\n"
								+ "\t" + $"nextItem.m_direction={nextItem.m_direction}\n"
								+ "\t" + $"nextItem.m_position.m_offset={nextItem.m_position.m_offset}\n"
								+ "\t" + $"m_startOffsetB={m_startOffsetB}"
							);
						}
#endif
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

#if DEBUG
				if (debug) {
					Debug(unitId, item, nextSegmentId, nextLaneIndex, nextLaneId, $"ProcessItemPedBicycle: Adding next item\n"
						+ "\t" + $"nextItem={nextItem}"
					);
				}
#endif

				AddBufferItem(
#if DEBUG
					debug,
#endif
					nextItem, item.m_position
				);
			}
		}

#if ROUTING
		// 5 (custom: process routed vehicle paths)
		private bool ProcessItemRouted(
#if DEBUG
			bool debug, uint unitId,
#endif
			BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, float prevMaxSpeed, float prevLaneSpeed,
#if ADVANCEDAI
			bool enableAdvancedAI, float laneChangingCost,
#endif
			float segmentSelectionCost, float laneSelectionCost, ushort nextNodeId, ref NetNode nextNode, bool isMiddle, SegmentRoutingData prevSegmentRouting, LaneEndRoutingData prevLaneEndRouting, byte connectOffset, int prevInnerSimilarLaneIndex
		) {

#if DEBUG
			if (debug) {
				Debug(unitId, item, $"ProcessItemRouted called.\n"
					+ "\t" + $"prevMaxSpeed={prevMaxSpeed}\n"
					+ "\t" + $"prevLaneSpeed={prevLaneSpeed}\n"
#if ADVANCEDAI
					+ "\t" + $"enableAdvancedAI={enableAdvancedAI}\n"
					+ "\t" + $"laneChangingCost={laneChangingCost}\n"
#endif
					+ "\t" + $"segmentSelectionCost={segmentSelectionCost}\n"
					+ "\t" + $"laneSelectionCost={laneSelectionCost}\n"
					+ "\t" + $"nextNodeId={nextNodeId}\n"
					+ "\t" + $"isMiddle={isMiddle}\n"
					+ "\t" + $"prevSegmentRouting={prevSegmentRouting}\n"
					+ "\t" + $"prevLaneEndRouting={prevLaneEndRouting}\n"
					+ "\t" + $"connectOffset={connectOffset}\n"
					+ "\t" + $"prevInnerSimilarLaneIndex={prevInnerSimilarLaneIndex}\n"
				);
			}
#endif

			/*
			 * =======================================================================================================
			 * Fetch lane end transitions, check if there are any present
			 * =======================================================================================================
			 */
			LaneTransitionData[] laneTransitions = prevLaneEndRouting.transitions;
			if (laneTransitions == null) {
#if DEBUG
				if (debug) {
					Debug(unitId, item, $"ProcessItemRouted: Aborting: No lane transitions");
				}
#endif
				return false;
			}

			ushort prevSegmentId = item.m_position.m_segment;
			int prevLaneIndex = item.m_position.m_lane;
			NetInfo prevSegmentInfo = prevSegment.Info;
			if (prevLaneIndex >= prevSegmentInfo.m_lanes.Length) {
#if DEBUG
				if (debug) {
					Debug(unitId, item, $"ProcessItemRouted: Aborting: Invalid lane index");
				}
#endif
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
#if DEBUG
				if (debug) {
					Debug(unitId, item, $"ProcessItemRouted: Vehicle restrictions: Aborting: Strict vehicle restrictions active");
				}
#endif
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

#if DEBUG
			if (debug) {
				Debug(unitId, item, $"ProcessItemRouted: Strict lane routing? {strictLaneRouting}\n"
					+ "\t" + $"m_isLaneArrowObeyingEntity={m_isLaneArrowObeyingEntity}\n"
					+ "\t" + $"nextNode.Info.m_class.m_service={nextNode.Info.m_class.m_service}\n"
					+ "\t" + $"nextNode.m_flags={nextNode.m_flags}\n"
					+ "\t" + $"prevIsCarLane={prevIsCarLane}"
				);
			}
#endif

			/*
			 * =======================================================================================================
			 * Check if u-turns may be performed
			 * =======================================================================================================
			 */
			bool isUturnAllowedHere = false; // is u-turn allowed at this place?
			if ((this.m_vehicleTypes & (VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Monorail)) == VehicleInfo.VehicleType.None) { // is vehicle able to perform a u-turn?
				bool isStockUturnPoint = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
#if JUNCTIONRESTRICTIONS
				if (Options.junctionRestrictionsEnabled) {
					bool nextIsStartNode = nextNodeId == prevSegment.m_startNode;
					bool prevIsOutgoingOneWay = nextIsStartNode ? prevSegmentRouting.startNodeOutgoingOneWay : prevSegmentRouting.endNodeOutgoingOneWay;

					// determine if the vehicle may u-turn at the target node, according to customization
					isUturnAllowedHere =
						m_isRoadVehicle && // only road vehicles may perform u-turns
						prevIsCarLane && // u-turns for road vehicles only
						(!m_isHeavyVehicle || isStockUturnPoint) && // only small vehicles may perform u-turns OR everyone at stock u-turn points
						!prevIsOutgoingOneWay && // do not u-turn on one-ways
						(
							m_junctionManager.IsUturnAllowed(prevSegmentId, nextIsStartNode)
							/*|| // only do u-turns if allowed
							(!m_queueItem.spawned && // or a yet unspawned vehicle ...
							(prevSegmentId == m_startSegmentA || prevSegmentId == m_startSegmentB)) // ... starts at the current segment*/
						)
					;

#if DEBUG
					if (debug) {
						Debug(unitId, item, $"ProcessItemRouted: Junction restrictions: Is u-turn allowed here? {isUturnAllowedHere}\n"
							+ "\t" + $"m_isRoadVehicle={m_isRoadVehicle}\n"
							+ "\t" + $"prevIsCarLane={prevIsCarLane}\n"
							+ "\t" + $"m_isHeavyVehicle={m_isHeavyVehicle}\n"
							+ "\t" + $"isStockUturnPoint={isStockUturnPoint}\n"
							+ "\t" + $"prevIsOutgoingOneWay={prevIsOutgoingOneWay}\n"
							+ "\t" + $"m_junctionManager.IsUturnAllowed(prevSegmentId, nextIsStartNode)={m_junctionManager.IsUturnAllowed(prevSegmentId, nextIsStartNode)}\n"
							+ "\t" + $"m_queueItem.vehicleId={m_queueItem.vehicleId}\n"
							+ "\t" + $"m_queueItem.spawned={m_queueItem.spawned}\n"
							+ "\t" + $"prevSegmentId={prevSegmentId}\n"
							+ "\t" + $"m_startSegmentA={m_startSegmentA}\n"
							+ "\t" + $"m_startSegmentB={m_startSegmentB}"
						);
					}
#endif
				} else {
#endif
					isUturnAllowedHere = isStockUturnPoint;

#if DEBUG
					if (debug) {
						Debug(unitId, item, $"ProcessItemRouted: Junction restrictions disabled: Is u-turn allowed here? {isUturnAllowedHere}");
					}
#endif

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
#if DEBUG
				if (debug) {
					Debug(unitId, item, $"ProcessItemRouted: Vehicle restrictions: Applied lane costs\n"
						+ "\t" + $"laneSelectionCost={laneSelectionCost}"
					);
				}
#endif
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

#if DEBUG
				if (debug) {
					Debug(unitId, item, $"ProcessItemRouted: Heavy trucks prefer outer lanes on highways: Applied lane costs\n"
						+ "\t" + $"laneSelectionCost={laneSelectionCost}\n"
						+ "\t" + $"Options.preferOuterLane={Options.preferOuterLane}\n"
						+ "\t" + $"m_isHeavyVehicle={m_isHeavyVehicle}\n"
						+ "\t" + $"m_isRoadVehicle={m_isRoadVehicle}\n"
						+ "\t" + $"prevIsCarLane={prevIsCarLane}\n"
						+ "\t" + $"prevSegmentRouting.highway={prevSegmentRouting.highway}\n"
						+ "\t" + $"prevLaneInfo.m_similarLaneCount={prevLaneInfo.m_similarLaneCount}\n"
						+ "\t" + $"prevOuterSimilarLaneIndex={prevOuterSimilarLaneIndex}\n"
						+ "\t" + $"prevRelOuterLane={prevRelOuterLane}"
					);
				}
#endif
			}

#if DEBUG
			if (debug) {
				Debug(unitId, item, $"ProcessItemRouted: Final cost factors:\n"
					+ "\t" + $"segmentSelectionCost={segmentSelectionCost}\n"
					+ "\t" + $"laneSelectionCost={laneSelectionCost}\n"
					+ "\t" + $"laneChangingCost={laneChangingCost}"
				);
			}
#endif

			/*
			 * =======================================================================================================
			 * Explore available lane end routings
			 * =======================================================================================================
			 */
			NetManager netManager = Singleton<NetManager>.instance;
			bool blocked = false;
			bool uturnExplored = false;
			for (int k = 0; k < laneTransitions.Length; ++k) {
#if DEBUG
				if (debug) {
					Debug(unitId, item, laneTransitions[k].segmentId, laneTransitions[k].laneIndex, laneTransitions[k].laneId, $"ProcessItemRouted: Exploring lane transition #{k}: {laneTransitions[k]}");
				}
#endif

				ushort nextSegmentId = laneTransitions[k].segmentId;

				if (nextSegmentId == 0) {
					continue;
				}

				if (laneTransitions[k].type == LaneEndTransitionType.Invalid) {
#if DEBUG
					if (debug) {
						Debug(unitId, item, laneTransitions[k].segmentId, laneTransitions[k].laneIndex, laneTransitions[k].laneId, $"ProcessItemRouted: Skipping transition: Transition is invalid");
					}
#endif
					continue;
				}

				if (nextSegmentId == prevSegmentId) {
					if (!isUturnAllowedHere) {
#if DEBUG
						if (debug) {
							Debug(unitId, item, laneTransitions[k].segmentId, laneTransitions[k].laneIndex, laneTransitions[k].laneId, $"ProcessItemRouted: Skipping transition: U-turn is not allowed here");
						}
#endif

						// prevent double/forbidden exploration of previous segment by vanilla code during this method execution
						continue;
					}

#if DEBUG
					if (debug) {
						Debug(unitId, item, laneTransitions[k].segmentId, laneTransitions[k].laneIndex, laneTransitions[k].laneId, $"ProcessItemRouted: Processing transition: Exploring u-turn");
					}
#endif
					// we are going to explore a regular u-turn
					uturnExplored = true;
				}

				// allow vehicles to ignore strict lane routing when moving off
				bool relaxedLaneRouting =
					m_isRoadVehicle &&
					((!m_queueItem.spawned || (m_queueItem.vehicleType & (ExtVehicleType.PublicTransport | ExtVehicleType.Emergency)) != ExtVehicleType.None) &&
					 (laneTransitions[k].laneId == m_startLaneA || laneTransitions[k].laneId == m_startLaneB));

#if DEBUG
				if (debug) {
					Debug(unitId, item, laneTransitions[k].segmentId, laneTransitions[k].laneIndex, laneTransitions[k].laneId, $"ProcessItemRouted: Relaxed lane routing? {relaxedLaneRouting}\n"
						+ "\t" + $"relaxedLaneRouting={relaxedLaneRouting}\n"
						+ "\t" + $"m_isRoadVehicle={m_isRoadVehicle}\n"
						+ "\t" + $"m_queueItem.spawned={m_queueItem.spawned}\n"
						+ "\t" + $"m_queueItem.vehicleType={m_queueItem.vehicleType}\n"
						+ "\t" + $"m_queueItem.vehicleId={m_queueItem.vehicleId}\n"
						+ "\t" + $"m_startLaneA={m_startLaneA}\n"
						+ "\t" + $"m_startLaneB={m_startLaneB}"
					);
				}
#endif

				if (
					!relaxedLaneRouting &&
					(strictLaneRouting && laneTransitions[k].type == LaneEndTransitionType.Relaxed)
				) {
#if DEBUG
					if (debug) {
						Debug(unitId, item, laneTransitions[k].segmentId, laneTransitions[k].laneIndex, laneTransitions[k].laneId, $"ProcessItemRouted: Aborting: Cannot explore relaxed lane\n"
							+ "\t" + $"relaxedLaneRouting={relaxedLaneRouting}\n"
							+ "\t" + $"strictLaneRouting={strictLaneRouting}\n"
							+ "\t" + $"laneTransitions[k].type={laneTransitions[k].type}"
						);
					}
#endif
					continue;
				}

#if DEBUG
				if (debug) {
					Debug(unitId, item, laneTransitions[k].segmentId, laneTransitions[k].laneIndex, laneTransitions[k].laneId, $"ProcessItemRouted: Exploring lane transition now\n"
#if ADVANCEDAI
						+ "\t" + $"enableAdvancedAI={enableAdvancedAI}\n"
						+ "\t" + $"laneChangingCost={laneChangingCost}\n"
#endif
						+ "\t" + $"segmentSelectionCost={segmentSelectionCost}\n"
						+ "\t" + $"laneSelectionCost={laneSelectionCost}"
					);
				}
#endif

				if (
					ProcessItemCosts(
#if DEBUG
						debug, unitId,
#endif
						item, ref prevSegment, ref prevLane, prevMaxSpeed, prevLaneSpeed,
#if ADVANCEDAI
						enableAdvancedAI, laneChangingCost,
#endif
						nextNodeId, ref nextNode, isMiddle, nextSegmentId, ref netManager.m_segments.m_buffer[nextSegmentId], segmentSelectionCost, laneSelectionCost, laneTransitions[k], ref prevInnerSimilarLaneIndex, connectOffset, true, false
					)
				) {
					blocked = true;
				}
			}

			return blocked && !uturnExplored;
		}
#endif

		private void AddBufferItem(
#if DEBUG
			bool debug,
#endif
			BufferItem item, PathUnit.Position target
		) {
#if DEBUG
			if (debug) {
				m_debugPositions[target.m_segment].Add(item.m_position.m_segment);
			}
#endif

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
		private void CalculateAdvancedAiCostFactors(
#if DEBUG
			bool debug, uint unit,
#endif
			ref BufferItem item, ref NetSegment prevSegment, ref NetLane prevLane, ushort nextNodeId, ref NetNode nextNode, ref float segmentSelectionCost, ref float laneSelectionCost, ref float laneChangingCost
		) {
#if DEBUG
			if (debug) {
				Debug(unit, item, $"CalculateAdvancedAiCostFactors called.\n" +
					"\t" + $"nextNodeId={nextNodeId}\n" +
					"\t" + $"segmentSelectionCost={segmentSelectionCost}\n" +
					"\t" + $"laneSelectionCost={laneSelectionCost}\n" +
					"\t" + $"laneChangingCost={laneChangingCost}"
				);
			}
#endif

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
					m_conf.AdvancedVehicleAI.LaneRandomizationJunctionSel > 0 &&
					m_pathRandomizer.Int32(m_conf.AdvancedVehicleAI.LaneRandomizationJunctionSel) == 0 &&
					m_pathRandomizer.Int32((uint)prevSegmentInfo.m_lanes.Length) == 0
				) {
					// randomized lane selection at junctions
					laneSelectionCost *= 1f + m_conf.AdvancedVehicleAI.LaneRandomizationCostFactor;

#if DEBUG
					if (debug) {
						Debug(unit, item, $"CalculateAdvancedAiCostFactors: Calculated randomized lane selection costs\n" +
							"\t" + $"laneSelectionCost={laneSelectionCost}"
						);
					}
#endif
				}

				/*
				 * =======================================================================================================
				 * Calculate junction costs
				 * =======================================================================================================
				 */
				// TODO if (prevSegmentRouting.highway) ?
				segmentSelectionCost *= 1f + m_conf.AdvancedVehicleAI.JunctionBaseCost;

#if DEBUG
				if (debug) {
					Debug(unit, item, $"CalculateAdvancedAiCostFactors: Calculated junction costs\n" +
						"\t" + $"segmentSelectionCost={segmentSelectionCost}"
					);
				}
#endif
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

#if DEBUG
					if (debug) {
						Debug(unit, item, $"CalculateAdvancedAiCostFactors: Calculated in-front-of-junction lane changing costs\n" +
							"\t" + $"laneChangingCost={laneChangingCost}"
						);
					}
#endif
				}

				/*
				 * =======================================================================================================
				 * Calculate general lane changing base cost factor
				 * =======================================================================================================
				 */
				if (
					m_conf.AdvancedVehicleAI.LaneChangingBaseMinCost > 0 &&
					m_conf.AdvancedVehicleAI.LaneChangingBaseMaxCost > m_conf.AdvancedVehicleAI.LaneChangingBaseMinCost
				) {
					float rand = (float)m_pathRandomizer.Int32(101u) / 100f;
					laneChangingCost *= m_conf.AdvancedVehicleAI.LaneChangingBaseMinCost + rand * (m_conf.AdvancedVehicleAI.LaneChangingBaseMaxCost - m_conf.AdvancedVehicleAI.LaneChangingBaseMinCost);

#if DEBUG
					if (debug) {
						Debug(unit, item, $"CalculateAdvancedAiCostFactors: Calculated base lane changing costs\n" +
							"\t" + $"laneChangingCost={laneChangingCost}"
						);
					}
#endif
				}
			}

#if DEBUG
			if (debug) {
				Debug(unit, item, $"CalculateAdvancedAiCostFactors: Calculated cost factors\n" +
					"\t" + $"segmentSelectionCost={segmentSelectionCost}\n" +
					"\t" + $"laneSelectionCost={laneSelectionCost}\n" +
					"\t" + $"laneChangingCost={laneChangingCost}"
				);
			}
#endif
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
					try {
						Monitor.Enter(m_bufferLock);
						CustomPathManager._instance.queueItems[m_calculating].queued = false;
						CustomPathManager._instance.ReleasePath(m_calculating);
					} finally {
						Monitor.Exit(m_bufferLock);
					}
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
