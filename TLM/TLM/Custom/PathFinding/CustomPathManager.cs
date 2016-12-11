#define QUEUEDSTATSx
#define EXTRAPFx
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

// ReSharper disable InconsistentNaming

namespace TrafficManager.Custom.PathFinding {
	public class CustomPathManager : PathManager {
		internal struct PathUnitQueueItem {
			internal uint nextPathUnitId;
		}

		/// <summary>
		/// Holds a linked list of path units waiting to be calculated
		/// </summary>
		internal PathUnitQueueItem[] queueItems;
		internal object QueueItemLock;

		internal CustomPathFind[] _replacementPathFinds;

		public static CustomPathManager _instance;

		internal ExtVehicleType?[] pathUnitExtVehicleType = null;
		internal ushort?[] pathUnitVehicleIds = null;
		internal ExtCitizenInstance.ExtPathType?[] pathUnitPathTypes = null;

#if QUEUEDSTATS
		public static uint TotalQueuedPathFinds {
			get; private set;
		} = 0;

#if EXTRAPF
		public static uint ExtraQueuedPathFinds {
			get; private set;
		} = 0;
#endif
#endif

		public static bool InitDone {
			get; private set;
		} = false;

		//On waking up, replace the stock pathfinders with the custom one
		[UsedImplicitly]
		public new virtual void Awake() {
			pathUnitExtVehicleType = new ExtVehicleType?[PathManager.MAX_PATHUNIT_COUNT];
			pathUnitVehicleIds = new ushort?[PathManager.MAX_PATHUNIT_COUNT];
			pathUnitPathTypes = new ExtCitizenInstance.ExtPathType?[PathManager.MAX_PATHUNIT_COUNT];

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

			QueueItemLock = new object();
			queueItems = new PathUnitQueueItem[PathManager.MAX_PATHUNIT_COUNT];

			var stockPathFinds = GetComponents<PathFind>();
			var numOfStockPathFinds = stockPathFinds.Length;
			int numCustomPathFinds = numOfStockPathFinds;
#if EXTRAPF
			++numCustomPathFinds;
#endif

			Log._Debug("Creating " + numCustomPathFinds + " custom PathFind objects.");
			_replacementPathFinds = new CustomPathFind[numCustomPathFinds];
			try {
				Monitor.Enter(this.m_bufferLock);

				for (var i = 0; i < numCustomPathFinds; i++) {
					_replacementPathFinds[i] = gameObject.AddComponent<CustomPathFind>();
					_replacementPathFinds[i].pfId = i;
					if (i == 0) {
						_replacementPathFinds[i].IsMasterPathFind = true;
					}
#if EXTRAPF
					else if (i == numCustomPathFinds - 1) {
						_replacementPathFinds[i].IsExtraPathFind = true;
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

			if (this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags == 0) {
				return;
			}
			Monitor.Enter(m_bufferLock);
			try {
				int num = 0;
				while (unit != 0u) {
					if (this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_referenceCount > 1) {
						--this.m_pathUnits.m_buffer[unit].m_referenceCount;
						break;
					}

					/*if (this.m_pathUnits.m_buffer[unit].m_pathFindFlags == PathUnit.FLAG_CREATED) {
						Log.Error($"Will release path unit {unit} which is CREATED!");
					}*/

					uint nextPathUnit = this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit;
					this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags = 0;
					this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_pathFindFlags = 0;
					this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit = 0u;
					this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_referenceCount = 0;
					this.m_pathUnits.ReleaseItem(unit);
					ResetPathUnit(unit); // NON-STOCK CODE
					unit = nextPathUnit;
					if (++num >= 262144) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
				this.m_pathUnitCount = (int)(this.m_pathUnits.ItemCount() - 1u);
			} finally {
				Monitor.Exit(this.m_bufferLock);
			}
		}

		public bool CreatePath(ExtVehicleType vehicleType, ushort vehicleId, ExtCitizenInstance.ExtPathType pathType, out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPos, PathUnit.Position endPos, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength) {
			PathUnit.Position position = default(PathUnit.Position);
			return this.CreatePath(false, vehicleType, vehicleId, pathType, out unit, ref randomizer, buildIndex, startPos, position, endPos, position, position, laneTypes, vehicleTypes, maxLength, false, false, false, false, false, false);
		}


		public bool CreatePath(ExtVehicleType vehicleType, ushort vehicleId, ExtCitizenInstance.ExtPathType pathType, out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength) {
			PathUnit.Position def = default(PathUnit.Position);
			return this.CreatePath(false, vehicleType, vehicleId, pathType, out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, def, laneTypes, vehicleTypes, maxLength, false, false, false, false, false, false);
		}


		public bool CreatePath(ExtVehicleType vehicleType, ushort vehicleId, ExtCitizenInstance.ExtPathType pathType, out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue) {
			PathUnit.Position def = default(PathUnit.Position);
			return this.CreatePath(false, vehicleType, vehicleId, pathType, out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, def, laneTypes, vehicleTypes, maxLength, isHeavyVehicle, ignoreBlocked, stablePath, skipQueue, false, false);
		}


		public bool CreatePath(bool recalc, ExtVehicleType vehicleType, ushort vehicleId, ExtCitizenInstance.ExtPathType pathType, out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPos, PathUnit.Position endPos, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength) {
			PathUnit.Position position = default(PathUnit.Position);
			return this.CreatePath(recalc, vehicleType, vehicleId, pathType, out unit, ref randomizer, buildIndex, startPos, position, endPos, position, position, laneTypes, vehicleTypes, maxLength, false, false, false, false, false, false);
		}

		public bool CreatePath(bool recalc, ExtVehicleType vehicleType, ushort vehicleId, ExtCitizenInstance.ExtPathType pathType, out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength) {
			PathUnit.Position def = default(PathUnit.Position);
			return this.CreatePath(recalc, vehicleType, vehicleId, pathType, out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, def, laneTypes, vehicleTypes, maxLength, false, false, false, false, false, false);
		}

		public bool CreatePath(bool recalc, ExtVehicleType vehicleType, ushort vehicleId, ExtCitizenInstance.ExtPathType pathType, out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue) {
			PathUnit.Position def = default(PathUnit.Position);
			return this.CreatePath(recalc, vehicleType, vehicleId, pathType, out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, def, laneTypes, vehicleTypes, maxLength, isHeavyVehicle, ignoreBlocked, stablePath, skipQueue, false, false);
		}

		public bool CreatePath(bool recalc, ExtVehicleType vehicleType, ushort vehicleId, ExtCitizenInstance.ExtPathType pathType, out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, PathUnit.Position vehiclePosition, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue, bool randomParking, bool ignoreFlooded) {
			uint pathUnitId;
			try {
				Monitor.Enter(this.m_bufferLock);
				if (!this.m_pathUnits.CreateItem(out pathUnitId, ref randomizer)) {
					unit = 0u;
					bool result = false;
					return result;
				}
				this.m_pathUnitCount = (int)(this.m_pathUnits.ItemCount() - 1u);
			} finally {
				Monitor.Exit(this.m_bufferLock);
			}
			unit = pathUnitId;

			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags = 1;
			if (isHeavyVehicle) {
				this.m_pathUnits.m_buffer[unit].m_simulationFlags |= 16;
			}
			if (ignoreBlocked || ignoreFlooded) {
				this.m_pathUnits.m_buffer[unit].m_simulationFlags |= 32;
			}
			if (stablePath) {
				this.m_pathUnits.m_buffer[unit].m_simulationFlags |= 64;
			}
			if (randomParking) {
				this.m_pathUnits.m_buffer[unit].m_simulationFlags |= 128;
			}
			this.m_pathUnits.m_buffer[unit].m_pathFindFlags = 0;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_buildIndex = buildIndex;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position00 = startPosA;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position01 = endPosA;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position02 = startPosB;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position03 = endPosB;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position11 = vehiclePosition;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit = 0u;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_laneTypes = (byte)laneTypes;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_vehicleTypes = (byte)vehicleTypes;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_length = maxLength;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount = 20;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_referenceCount = 1;
			int minQueued = 10000000;
			CustomPathFind pathFind = null;
#if QUEUEDSTATS
			TotalQueuedPathFinds = 0;
#if EXTRAPF
			ExtraQueuedPathFinds = 0;
#endif
#endif
			for (int i = 0; i < _replacementPathFinds.Length; ++i) {
				CustomPathFind pathFindCandidate = _replacementPathFinds[i];
#if QUEUEDSTATS
				TotalQueuedPathFinds += (uint)pathFindCandidate.m_queuedPathFindCount;
#if EXTRAPF
				if (pathFindCandidate.IsExtraPathFind)
					ExtraQueuedPathFinds += (uint)pathFindCandidate.m_queuedPathFindCount;
#endif
#endif
				if (pathFindCandidate.IsAvailable
#if EXTRAPF
					&& (!pathFindCandidate.IsExtraPathFind || recalc)
#endif
					&& pathFindCandidate.m_queuedPathFindCount < minQueued) {
					minQueued = pathFindCandidate.m_queuedPathFindCount;
					pathFind = pathFindCandidate;
				}
			}

			pathUnitExtVehicleType[unit] = vehicleType;
			pathUnitVehicleIds[unit] = vehicleId == 0 ? (ushort?)null : vehicleId;
			pathUnitPathTypes[unit] = pathType;

			if (pathFind != null && pathFind.ExtCalculatePath(unit, skipQueue)) {
				return true;
			}
			this.ReleasePath(unit);
			return false;
		}

		public static bool FindPathPositionWithSpiralLoop(Vector3 position, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPos) {
			return FindPathPositionWithSpiralLoop(position, null, service, laneType, vehicleType, allowUnderground, requireConnect, maxDistance, out pathPos);
		}

		public static bool FindPathPositionWithSpiralLoop(Vector3 position, Vector3? secondaryPosition, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPos) {
			PathUnit.Position position2;
			float distanceSqrA;
			float distanceSqrB;
			return FindPathPositionWithSpiralLoop(position, secondaryPosition, service, laneType, vehicleType, VehicleInfo.VehicleType.None, allowUnderground, requireConnect, maxDistance, out pathPos, out position2, out distanceSqrA, out distanceSqrB);
		}

		public static bool FindPathPositionWithSpiralLoop(Vector3 position, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPosA, out PathUnit.Position pathPosB, out float distanceSqrA, out float distanceSqrB) {
			return FindPathPositionWithSpiralLoop(position, null, service, laneType, vehicleType, allowUnderground, requireConnect, maxDistance, out pathPosA, out pathPosB, out distanceSqrA, out distanceSqrB);
		}

		public static bool FindPathPositionWithSpiralLoop(Vector3 position, Vector3? secondaryPosition, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPosA, out PathUnit.Position pathPosB, out float distanceSqrA, out float distanceSqrB) {
			return FindPathPositionWithSpiralLoop(position, secondaryPosition, service, laneType, vehicleType, VehicleInfo.VehicleType.None, allowUnderground, requireConnect, maxDistance, out pathPosA, out pathPosB, out distanceSqrA, out distanceSqrB);
		}

		public static bool FindPathPositionWithSpiralLoop(Vector3 position, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, VehicleInfo.VehicleType stopType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPosA, out PathUnit.Position pathPosB, out float distanceSqrA, out float distanceSqrB) {
			return FindPathPositionWithSpiralLoop(position, null, service, laneType, vehicleType, stopType, allowUnderground, requireConnect, maxDistance, out pathPosA, out pathPosB, out distanceSqrA, out distanceSqrB);
		}

		public static bool FindPathPositionWithSpiralLoop(Vector3 position, Vector3? secondaryPosition, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, VehicleInfo.VehicleType stopType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPosA, out PathUnit.Position pathPosB, out float distanceSqrA, out float distanceSqrB) {
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
						(netManager.m_segments.m_buffer[segmentId].m_flags & (NetSegment.Flags.Collapsed | NetSegment.Flags.Flooded)) == NetSegment.Flags.None && (allowUnderground || !segmentInfo.m_netAI.IsUnderground())
						) {

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

		internal void ResetPathUnit(uint unit) {
			pathUnitExtVehicleType[unit] = null;
			pathUnitVehicleIds[unit] = null;
			pathUnitPathTypes[unit] = ExtCitizenInstance.ExtPathType.None;
		}

		private void StopPathFinds() {
			foreach (CustomPathFind pathFind in _replacementPathFinds) {
				UnityEngine.Object.Destroy(pathFind);
			}
		}

		protected virtual void OnDestroy() {
			Log._Debug("CustomPathManager: OnDestroy");
			StopPathFinds();
		}
	}
}
