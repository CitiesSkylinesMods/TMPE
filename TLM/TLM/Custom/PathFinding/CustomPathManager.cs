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
using static TrafficManager.Manager.Impl.ExtPathManager;
using TrafficManager.RedirectionFramework.Attributes;

// ReSharper disable InconsistentNaming

namespace TrafficManager.Custom.PathFinding {
	[TargetType(typeof(PathManager))]
	public class CustomPathManager : PathManager {
		/// <summary>
		/// Holds a linked list of path units waiting to be calculated
		/// </summary>
		internal PathUnitQueueItem[] queueItems; // TODO move to ExtPathManager

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

		[RedirectMethod]
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

		public bool CustomCreatePath(out uint unit, ref Randomizer randomizer, PathCreationArgs args) {
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
			if (PathManager.FindPathPosition(pos, ItemClass.Service.Road, laneTypes, vehicleTypes, allowUnderground, false, Options.parkingAI ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance : 32f, out posA, out posB, out distA, out distB) && distA < minDist) {
				minDist = distA;
				position = posA;
			}
			if (PathManager.FindPathPosition(pos, ItemClass.Service.Beautification, laneTypes, vehicleTypes, allowUnderground, false, Options.parkingAI ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance : 32f, out posA, out posB, out distA, out distB) && distA < minDist) {
				minDist = distA;
				position = posA;
			}
			if (allowTransport && PathManager.FindPathPosition(pos, ItemClass.Service.PublicTransport, laneTypes, vehicleTypes, allowUnderground, false, Options.parkingAI ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance : 32f, out posA, out posB, out distA, out distB) && distA < minDist) {
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
