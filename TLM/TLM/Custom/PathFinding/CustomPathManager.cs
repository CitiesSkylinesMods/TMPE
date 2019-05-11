#define QUEUEDSTATSx
#define DEBUGPF3x

using System;
using System.Reflection;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Math;
using JetBrains.Annotations;
using UnityEngine;
using TrafficManager.Geometry;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Util;
using CSUtil.Commons;
using static TrafficManager.Traffic.Data.ExtCitizenInstance;
using TrafficManager.Traffic.Data;

// ReSharper disable InconsistentNaming

namespace TrafficManager.Custom.PathFinding {
	public class CustomPathManager : PathManager {
		public struct PathCreationArgs {
			/// <summary>
			/// Extended path type
			/// </summary>
			public ExtCitizenInstance.ExtPathType extPathType;

			/// <summary>
			/// Extended vehicle type
			/// </summary>
			public ExtVehicleType extVehicleType;

			/// <summary>
			/// (optional) vehicle id
			/// </summary>
			public ushort vehicleId;

			/// <summary>
			/// is entity alredy spawned?
			/// </summary>
			public bool spawned;

			/// <summary>
			/// Current build index
			/// </summary>
			public uint buildIndex;

			/// <summary>
			/// Start position (first alternative)
			/// </summary>
			public PathUnit.Position startPosA;

			/// <summary>
			/// Start position (second alternative, opposite road side)
			/// </summary>
			public PathUnit.Position startPosB;

			/// <summary>
			/// End position (first alternative)
			/// </summary>
			public PathUnit.Position endPosA;

			/// <summary>
			/// End position (second alternative, opposite road side)
			/// </summary>
			public PathUnit.Position endPosB;

			/// <summary>
			/// (optional) position of the parked vehicle
			/// </summary>
			public PathUnit.Position vehiclePosition;

			/// <summary>
			/// Allowed set of lane types
			/// </summary>
			public NetInfo.LaneType laneTypes;

			/// <summary>
			/// Allowed set of vehicle types
			/// </summary>
			public VehicleInfo.VehicleType vehicleTypes;

			/// <summary>
			/// Maximum allowed path length
			/// </summary>
			public float maxLength;

			/// <summary>
			/// Is the path calculated for a heavy vehicle?
			/// </summary>
			public bool isHeavyVehicle;

			/// <summary>
			/// Is the path calculated for a vehicle with a combustion engine?
			/// </summary>
			public bool hasCombustionEngine;

			/// <summary>
			/// Should blocked segments be ignored?
			/// </summary>
			public bool ignoreBlocked;

			/// <summary>
			/// Should flooded segments be ignored?
			/// </summary>
			public bool ignoreFlooded;

			/// <summary>
			/// Should path costs be ignored?
			/// </summary>
			public bool ignoreCosts;

			/// <summary>
			/// Should random parking apply?
			/// </summary>
			public bool randomParking;

			/// <summary>
			/// Should the path be stable (and not randomized)?
			/// </summary>
			public bool stablePath;

			/// <summary>
			/// Is this a high priority path?
			/// </summary>
			public bool skipQueue;
		}

		public struct PathUnitQueueItem {
			public uint nextPathUnitId; // access requires acquisition of CustomPathFind.QueueLock
			public ExtVehicleType vehicleType; // access requires acquisition of m_bufferLock
			public ExtPathType pathType; // access requires acquisition of m_bufferLock
			public ushort vehicleId; // access requires acquisition of m_bufferLock
			public bool queued; // access requires acquisition of m_bufferLock
			public bool spawned; // access requires acquisition of m_bufferLock

			//public void Reset() {
			//	vehicleType = ExtVehicleType.None;
			//	pathType = ExtPathType.None;
			//	vehicleId = 0;
			//}

			public override string ToString() {
				return $"[PathUnitQueueItem\n" +
				"\t" + $"nextPathUnitId={nextPathUnitId}\n" +
				"\t" + $"vehicleType={vehicleType}\n" +
				"\t" + $"pathType={pathType}\n" +
				"\t" + $"vehicleId={vehicleId}\n" +
				"\t" + $"queued={queued}\n" +
				"\t" + $"spawned={spawned}\n" +
				"PathUnitQueueItem]";
			}
		}

		/// <summary>
		/// Holds a linked list of path units waiting to be calculated
		/// </summary>
		internal PathUnitQueueItem[] queueItems;

#if PF2
		private CustomPathFind2[] _replacementPathFinds;
#else
		private CustomPathFind[] _replacementPathFinds;
#endif

		public static CustomPathManager _instance;

#if QUEUEDSTATS
		public static uint TotalQueuedPathFinds {
			get; private set;
		} = 0;
#endif

		public static bool InitDone {
			get; private set;
		} = false;

		//On waking up, replace the stock pathfinders with the custom one
		[UsedImplicitly]
		public new virtual void Awake() {
			_instance = this;
		}

		public void UpdateWithPathManagerValues(PathManager stockPathManager) {
			// Needed fields come from joaofarias' csl-traffic
			// https://github.com/joaofarias/csl-traffic

			m_simulationProfiler = stockPathManager.m_simulationProfiler;
			m_drawCallData = stockPathManager.m_drawCallData;
			m_properties = stockPathManager.m_properties;
			m_pathUnitCount = stockPathManager.m_pathUnitCount;
			m_renderPathGizmo = stockPathManager.m_renderPathGizmo;
			m_pathUnits = stockPathManager.m_pathUnits;
			m_bufferLock = stockPathManager.m_bufferLock;

			Log._Debug("Waking up CustomPathManager.");

			queueItems = new PathUnitQueueItem[PathManager.MAX_PATHUNIT_COUNT];

			var stockPathFinds = GetComponents<PathFind>();
			var numOfStockPathFinds = stockPathFinds.Length;
			int numCustomPathFinds = numOfStockPathFinds;

			Log._Debug("Creating " + numCustomPathFinds + " custom PathFind objects.");
#if PF2
			_replacementPathFinds = new CustomPathFind2[numCustomPathFinds];
#else
			_replacementPathFinds = new CustomPathFind[numCustomPathFinds];
#endif

			try {
				Monitor.Enter(this.m_bufferLock);

				for (var i = 0; i < numCustomPathFinds; i++) {
#if PF2
					_replacementPathFinds[i] = gameObject.AddComponent<CustomPathFind2>();
#else
					_replacementPathFinds[i] = gameObject.AddComponent<CustomPathFind>();
					_replacementPathFinds[i].pfId = i;
					if (i == 0) {
						_replacementPathFinds[i].IsMasterPathFind = true;
					}
#endif
				}

				Log._Debug("Setting _replacementPathFinds");
				var fieldInfo = typeof(PathManager).GetField("m_pathfinds", BindingFlags.NonPublic | BindingFlags.Instance);

				Log._Debug("Setting m_pathfinds to custom collection");
				fieldInfo?.SetValue(this, _replacementPathFinds);

				for (var i = 0; i < numOfStockPathFinds; i++) {
#if DEBUG
					Log._Debug($"PF {i}: {stockPathFinds[i].m_queuedPathFindCount} queued path-finds");
#endif
					//stockPathFinds[i].WaitForAllPaths(); // would cause deadlock since we have a lock on m_bufferLock
					Destroy(stockPathFinds[i]);
				}
			} finally {
				Monitor.Exit(this.m_bufferLock);
			}

			InitDone = true;
		}

		public new void ReleasePath(uint unit) {
#if DEBUGPF3
			Log.Warning($"CustomPathManager.ReleasePath({unit}) called.");
#endif

			if (this.m_pathUnits.m_buffer[unit].m_simulationFlags == 0) {
				return;
			}
			try {
				Monitor.Enter(m_bufferLock);

				int numIters = 0;
				while (unit != 0u) {
					if (this.m_pathUnits.m_buffer[unit].m_referenceCount > 1) {
						--this.m_pathUnits.m_buffer[unit].m_referenceCount;
						break;
					}

					/*if (this.m_pathUnits.m_buffer[unit].m_pathFindFlags == PathUnit.FLAG_CREATED) {
						Log.Error($"Will release path unit {unit} which is CREATED!");
					}*/

					uint nextPathUnit = this.m_pathUnits.m_buffer[unit].m_nextPathUnit;
					this.m_pathUnits.m_buffer[unit].m_simulationFlags = 0;
					this.m_pathUnits.m_buffer[unit].m_pathFindFlags = 0;
					this.m_pathUnits.m_buffer[unit].m_nextPathUnit = 0u;
					this.m_pathUnits.m_buffer[unit].m_referenceCount = 0;
					this.m_pathUnits.ReleaseItem(unit);
					//queueItems[unit].Reset(); // NON-STOCK CODE
					unit = nextPathUnit;
					if (++numIters >= 262144) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
				this.m_pathUnitCount = (int)(this.m_pathUnits.ItemCount() - 1u);
			} finally {
				Monitor.Exit(this.m_bufferLock);
			}
		}

		public bool CreatePath(out uint unit, ref Randomizer randomizer, PathCreationArgs args) {
			uint pathUnitId;
			try {
				Monitor.Enter(this.m_bufferLock);

				int numIters = 0;
				while (true) { // NON-STOCK CODE
					++numIters;

					if (!this.m_pathUnits.CreateItem(out pathUnitId, ref randomizer)) {
						unit = 0u;
						return false;
					}

					this.m_pathUnits.m_buffer[pathUnitId].m_simulationFlags = 1;
					this.m_pathUnits.m_buffer[pathUnitId].m_referenceCount = 1;
					this.m_pathUnits.m_buffer[pathUnitId].m_nextPathUnit = 0u;

					// NON-STOCK CODE START
					if (queueItems[pathUnitId].queued) {
						ReleasePath(pathUnitId);

						if (numIters > 10) {
							unit = 0u;
							return false;
						}

						continue;
					}
					break;
				}

				queueItems[pathUnitId].vehicleType = args.extVehicleType;
				queueItems[pathUnitId].vehicleId = args.vehicleId;
				queueItems[pathUnitId].pathType = args.extPathType;
				queueItems[pathUnitId].spawned = args.spawned;
				queueItems[pathUnitId].queued = true;
				// NON-STOCK CODE END

				this.m_pathUnitCount = (int)(this.m_pathUnits.ItemCount() - 1u);
			} finally {
				Monitor.Exit(this.m_bufferLock);
			}
			unit = pathUnitId;

			if (args.isHeavyVehicle) {
				this.m_pathUnits.m_buffer[unit].m_simulationFlags |= 16;
			}
			if (args.ignoreBlocked || args.ignoreFlooded) {
				this.m_pathUnits.m_buffer[unit].m_simulationFlags |= 32;
			}
			if (args.stablePath) {
				this.m_pathUnits.m_buffer[unit].m_simulationFlags |= 64;
			}
			if (args.randomParking) {
				this.m_pathUnits.m_buffer[unit].m_simulationFlags |= 2;
			}
			if (args.hasCombustionEngine) {
				this.m_pathUnits.m_buffer[unit].m_simulationFlags |= 4;
			}
			if (args.ignoreCosts) {
				this.m_pathUnits.m_buffer[unit].m_simulationFlags |= 8;
			}
			this.m_pathUnits.m_buffer[unit].m_pathFindFlags = 0;
			this.m_pathUnits.m_buffer[unit].m_buildIndex = args.buildIndex;
			this.m_pathUnits.m_buffer[unit].m_position00 = args.startPosA;
			this.m_pathUnits.m_buffer[unit].m_position01 = args.endPosA;
			this.m_pathUnits.m_buffer[unit].m_position02 = args.startPosB;
			this.m_pathUnits.m_buffer[unit].m_position03 = args.endPosB;
			this.m_pathUnits.m_buffer[unit].m_position11 = args.vehiclePosition;
			this.m_pathUnits.m_buffer[unit].m_laneTypes = (byte)args.laneTypes;
			this.m_pathUnits.m_buffer[unit].m_vehicleTypes = (ushort)args.vehicleTypes;
			this.m_pathUnits.m_buffer[unit].m_length = args.maxLength;
			this.m_pathUnits.m_buffer[unit].m_positionCount = 20;
			int minQueued = 10000000;
#if PF2
			CustomPathFind2 pathFind = null;
#else
			CustomPathFind pathFind = null;
#endif

#if QUEUEDSTATS
			TotalQueuedPathFinds = 0;
#endif
			for (int i = 0; i < _replacementPathFinds.Length; ++i) {
#if PF2
				CustomPathFind2 pathFindCandidate = _replacementPathFinds[i];
#else
				CustomPathFind pathFindCandidate = _replacementPathFinds[i];
#endif

#if QUEUEDSTATS
				TotalQueuedPathFinds += (uint)pathFindCandidate.m_queuedPathFindCount;
#endif
				if (pathFindCandidate.IsAvailable
					&& pathFindCandidate.m_queuedPathFindCount < minQueued) {
					minQueued = pathFindCandidate.m_queuedPathFindCount;
					pathFind = pathFindCandidate;
				}
			}

#if PF2
			if (pathFind != null && pathFind.CalculatePath(unit, args.skipQueue)) {
				return true;
			}
#else
			if (pathFind != null && pathFind.ExtCalculatePath(unit, args.skipQueue)) {
				return true;
			}
#endif

			// NON-STOCK CODE START
			try {
				Monitor.Enter(this.m_bufferLock);
				
				queueItems[pathUnitId].queued = false;
				// NON-STOCK CODE END
				this.ReleasePath(unit);

				// NON-STOCK CODE START
				this.m_pathUnitCount = (int)(this.m_pathUnits.ItemCount() - 1u);
			} finally {
				Monitor.Exit(this.m_bufferLock);
			}
			// NON-STOCK CODE END
			return false;
		}

		public static bool FindPathPositionWithSpiralLoop(Vector3 position, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, NetInfo.LaneType otherLaneType, VehicleInfo.VehicleType otherVehicleType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPos) {
			return FindPathPositionWithSpiralLoop(position, null, service, laneType, vehicleType, otherLaneType, otherVehicleType, allowUnderground, requireConnect, maxDistance, out pathPos);
		}

		public static bool FindPathPositionWithSpiralLoop(Vector3 position, Vector3? secondaryPosition, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, NetInfo.LaneType otherLaneType, VehicleInfo.VehicleType otherVehicleType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPos) {
			PathUnit.Position position2;
			float distanceSqrA;
			float distanceSqrB;
			return FindPathPositionWithSpiralLoop(position, secondaryPosition, service, laneType, vehicleType, otherLaneType, otherVehicleType, VehicleInfo.VehicleType.None, allowUnderground, requireConnect, maxDistance, out pathPos, out position2, out distanceSqrA, out distanceSqrB);
		}

		public static bool FindPathPositionWithSpiralLoop(Vector3 position, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, NetInfo.LaneType otherLaneType, VehicleInfo.VehicleType otherVehicleType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPosA, out PathUnit.Position pathPosB, out float distanceSqrA, out float distanceSqrB) {
			return FindPathPositionWithSpiralLoop(position, null, service, laneType, vehicleType, otherLaneType, otherVehicleType, allowUnderground, requireConnect, maxDistance, out pathPosA, out pathPosB, out distanceSqrA, out distanceSqrB);
		}

		public static bool FindPathPositionWithSpiralLoop(Vector3 position, Vector3? secondaryPosition, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, NetInfo.LaneType otherLaneType, VehicleInfo.VehicleType otherVehicleType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPosA, out PathUnit.Position pathPosB, out float distanceSqrA, out float distanceSqrB) {
			return FindPathPositionWithSpiralLoop(position, secondaryPosition, service, laneType, vehicleType, otherLaneType, otherVehicleType, VehicleInfo.VehicleType.None, allowUnderground, requireConnect, maxDistance, out pathPosA, out pathPosB, out distanceSqrA, out distanceSqrB);
		}

		public static bool FindPathPositionWithSpiralLoop(Vector3 position, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, NetInfo.LaneType otherLaneType, VehicleInfo.VehicleType otherVehicleType, VehicleInfo.VehicleType stopType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPosA, out PathUnit.Position pathPosB, out float distanceSqrA, out float distanceSqrB) {
			return FindPathPositionWithSpiralLoop(position, null, service, laneType, vehicleType, otherLaneType, otherVehicleType, stopType, allowUnderground, requireConnect, maxDistance, out pathPosA, out pathPosB, out distanceSqrA, out distanceSqrB);
		}

		public static bool FindPathPositionWithSpiralLoop(Vector3 position, Vector3? secondaryPosition, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, NetInfo.LaneType otherLaneType, VehicleInfo.VehicleType otherVehicleType, VehicleInfo.VehicleType stopType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPosA, out PathUnit.Position pathPosB, out float distanceSqrA, out float distanceSqrB) {
			int iMin = Mathf.Max((int)((position.z - (float)NetManager.NODEGRID_CELL_SIZE) / (float)NetManager.NODEGRID_CELL_SIZE + (float)NetManager.NODEGRID_RESOLUTION / 2f), 0);
			int iMax = Mathf.Min((int)((position.z + (float)NetManager.NODEGRID_CELL_SIZE) / (float)NetManager.NODEGRID_CELL_SIZE + (float)NetManager.NODEGRID_RESOLUTION / 2f), NetManager.NODEGRID_RESOLUTION-1);

			int jMin = Mathf.Max((int)((position.x - (float)NetManager.NODEGRID_CELL_SIZE) / (float)NetManager.NODEGRID_CELL_SIZE + (float)NetManager.NODEGRID_RESOLUTION / 2f), 0);
			int jMax = Mathf.Min((int)((position.x + (float)NetManager.NODEGRID_CELL_SIZE) / (float)NetManager.NODEGRID_CELL_SIZE + (float)NetManager.NODEGRID_RESOLUTION / 2f), NetManager.NODEGRID_RESOLUTION - 1);

			int width = iMax-iMin+1;
			int height = jMax-jMin+1;

			int centerI = (int)(position.z / (float)NetManager.NODEGRID_CELL_SIZE + (float)NetManager.NODEGRID_RESOLUTION / 2f);
			int centerJ = (int)(position.x / (float)NetManager.NODEGRID_CELL_SIZE + (float)NetManager.NODEGRID_RESOLUTION / 2f);

			NetManager netManager = Singleton<NetManager>.instance;
			/*pathPosA.m_segment = 0;
			pathPosA.m_lane = 0;
			pathPosA.m_offset = 0;*/
			distanceSqrA = 1E+10f;
			/*pathPosB.m_segment = 0;
			pathPosB.m_lane = 0;
			pathPosB.m_offset = 0;*/
			distanceSqrB = 1E+10f;
			float minDist = float.MaxValue;

			PathUnit.Position myPathPosA = default(PathUnit.Position);
			float myDistanceSqrA = float.MaxValue;
			PathUnit.Position myPathPosB = default(PathUnit.Position);
			float myDistanceSqrB = float.MaxValue;

			int lastSpiralDist = 0;
			bool found = false;

			LoopUtil.SpiralLoop(centerI, centerJ, width, height, delegate (int i, int j) {
				if (i < 0 || i >= NetManager.NODEGRID_RESOLUTION || j < 0 || j >= NetManager.NODEGRID_RESOLUTION)
					return true;

				int spiralDist = Math.Max(Math.Abs(i - centerI), Math.Abs(j - centerJ)); // maximum norm

				if (found && spiralDist > lastSpiralDist) {
					// last iteration
					return false;
				}

				ushort segmentId = netManager.m_segmentGrid[i * NetManager.NODEGRID_RESOLUTION + j];
				int iterations = 0;
				while (segmentId != 0) {
					NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;
					if (segmentInfo != null &&
						segmentInfo.m_class.m_service == service &&
						(netManager.m_segments.m_buffer[segmentId].m_flags & (NetSegment.Flags.Collapsed | NetSegment.Flags.Flooded)) == NetSegment.Flags.None &&
						(allowUnderground || !segmentInfo.m_netAI.IsUnderground())) {

						bool otherPassed = true;
						if (otherLaneType != NetInfo.LaneType.None || otherVehicleType != VehicleInfo.VehicleType.None) {
							// check if any lane is present that matches the given conditions
							otherPassed = false;
							Constants.ServiceFactory.NetService.IterateSegmentLanes(segmentId, delegate (uint laneId, ref NetLane lane, NetInfo.Lane laneInfo, ushort segtId, ref NetSegment segment, byte laneIndex) {
								if (
									(otherLaneType == NetInfo.LaneType.None || (laneInfo.m_laneType & otherLaneType) != NetInfo.LaneType.None) &&
									(otherVehicleType == VehicleInfo.VehicleType.None || (laneInfo.m_vehicleType & otherVehicleType) != VehicleInfo.VehicleType.None)) {
									otherPassed = true;
									return false;
								} else {
									return true;
								}
							});
						}

						if (otherPassed) {
							ushort startNodeId = netManager.m_segments.m_buffer[segmentId].m_startNode;
							ushort endNodeId = netManager.m_segments.m_buffer[segmentId].m_endNode;
							Vector3 startNodePos = netManager.m_nodes.m_buffer[startNodeId].m_position;
							Vector3 endNodePos = netManager.m_nodes.m_buffer[endNodeId].m_position;
	
							Vector3 posA; int laneIndexA; float laneOffsetA;
							Vector3 posB; int laneIndexB; float laneOffsetB;

							if (netManager.m_segments.m_buffer[segmentId].GetClosestLanePosition(position, laneType, vehicleType, stopType, requireConnect, out posA, out laneIndexA, out laneOffsetA, out posB, out laneIndexB, out laneOffsetB)) {
								float dist = Vector3.SqrMagnitude(position - posA);
								if (secondaryPosition != null)
									dist += Vector3.SqrMagnitude((Vector3)secondaryPosition - posA);

								if (dist < minDist) {
									found = true;

									minDist = dist;
									myPathPosA.m_segment = segmentId;
									myPathPosA.m_lane = (byte)laneIndexA;
									myPathPosA.m_offset = (byte)Mathf.Clamp(Mathf.RoundToInt(laneOffsetA * 255f), 0, 255);
									myDistanceSqrA = dist;

									dist = Vector3.SqrMagnitude(position - posB);
									if (secondaryPosition != null)
										dist += Vector3.SqrMagnitude((Vector3)secondaryPosition - posB);

									if (laneIndexB < 0) {
										myPathPosB.m_segment = 0;
										myPathPosB.m_lane = 0;
										myPathPosB.m_offset = 0;
										myDistanceSqrB = float.MaxValue;
									} else {
										myPathPosB.m_segment = segmentId;
										myPathPosB.m_lane = (byte)laneIndexB;
										myPathPosB.m_offset = (byte)Mathf.Clamp(Mathf.RoundToInt(laneOffsetB * 255f), 0, 255);
										myDistanceSqrB = dist;
									}
								}
							}
						}
					}

					segmentId = netManager.m_segments.m_buffer[segmentId].m_nextGridSegment;
					if (++iterations >= NetManager.MAX_SEGMENT_COUNT) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}

				lastSpiralDist = spiralDist;
				return true;
			});

			pathPosA = myPathPosA;
			distanceSqrA = myDistanceSqrA;
			pathPosB = myPathPosB;
			distanceSqrB = myDistanceSqrB;
			
			return pathPosA.m_segment != 0;
		}

		/// <summary>
		/// Finds a suitable path position for a walking citizen with the given world position.
		/// </summary>
		/// <param name="pos">world position</param>
		/// <param name="laneTypes">allowed lane types</param>
		/// <param name="vehicleTypes">allowed vehicle types</param>
		/// <param name="allowTransport">public transport allowed?</param>
		/// <param name="allowUnderground">underground position allowed?</param>
		/// <param name="position">resulting path position</param>
		/// <returns><code>true</code> if a position could be found, <code>false</code> otherwise</returns>
		public static bool FindCitizenPathPosition(Vector3 pos, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, bool allowTransport, bool allowUnderground, out PathUnit.Position position) {
			// TODO move to ExtPathManager after harmony upgrade
			position = default(PathUnit.Position);
			float minDist = 1E+10f;
			PathUnit.Position posA;
			PathUnit.Position posB;
			float distA;
			float distB;
			if (PathManager.FindPathPosition(pos, ItemClass.Service.Road, laneTypes, vehicleTypes, allowUnderground, false, Options.prohibitPocketCars ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance : 32f, out posA, out posB, out distA, out distB) && distA < minDist) {
				minDist = distA;
				position = posA;
			}
			if (PathManager.FindPathPosition(pos, ItemClass.Service.Beautification, laneTypes, vehicleTypes, allowUnderground, false, Options.prohibitPocketCars ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance : 32f, out posA, out posB, out distA, out distB) && distA < minDist) {
				minDist = distA;
				position = posA;
			}
			if (allowTransport && PathManager.FindPathPosition(pos, ItemClass.Service.PublicTransport, laneTypes, vehicleTypes, allowUnderground, false, Options.prohibitPocketCars ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance : 32f, out posA, out posB, out distA, out distB) && distA < minDist) {
				minDist = distA;
				position = posA;
			}
			return position.m_segment != 0;
		}

		/*internal void ResetQueueItem(uint unit) {
			queueItems[unit].Reset();
		}*/

		private void StopPathFinds() {
#if PF2
			foreach (CustomPathFind2 pathFind in _replacementPathFinds) {
				UnityEngine.Object.Destroy(pathFind);
			}
#else
			foreach (CustomPathFind pathFind in _replacementPathFinds) {
				UnityEngine.Object.Destroy(pathFind);
			}
#endif
		}

		protected virtual void OnDestroy() {
			Log._Debug("CustomPathManager: OnDestroy");
			StopPathFinds();
		}
	}
}
