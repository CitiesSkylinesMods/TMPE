// #define QUEUEDSTATS
// #define DEBUGPF3

namespace TrafficManager.Custom.PathFinding {
    using ColossalFramework.Math;
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Reflection;
    using System.Threading;
    using System;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using TrafficManager.RedirectionFramework.Attributes;
    using TrafficManager.State;
    using UnityEngine;

#if !PF_DIJKSTRA
    using CustomPathFind = CustomPathFind_Old;
#endif

    [TargetType(typeof(PathManager))]
    public class CustomPathManager : PathManager {
        /// <summary>
        /// Holds a linked list of path units waiting to be calculated
        /// </summary>
        internal PathUnitQueueItem[] QueueItems; // TODO move to ExtPathManager

        private CustomPathFind[] _replacementPathFinds;

        public static CustomPathManager _instance;

#if QUEUEDSTATS
        public static uint TotalQueuedPathFinds {
            get; private set;
        }
#endif

        public static bool InitDone {
            get; private set;
        }

        // On waking up, replace the stock pathfinders with the custom one
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

            QueueItems = new PathUnitQueueItem[MAX_PATHUNIT_COUNT];

            PathFind[] stockPathFinds = GetComponents<PathFind>();
            int numOfStockPathFinds = stockPathFinds.Length;
            int numCustomPathFinds = numOfStockPathFinds;

            Log._Debug("Creating " + numCustomPathFinds + " custom PathFind objects.");
            _replacementPathFinds = new CustomPathFind[numCustomPathFinds];

            lock(m_bufferLock) {

                for (int i = 0; i < numCustomPathFinds; i++) {
                    _replacementPathFinds[i] = gameObject.AddComponent<CustomPathFind>();
#if !PF_DIJKSTRA
					_replacementPathFinds[i].pfId = i;
					if (i == 0) {
						_replacementPathFinds[i].IsMasterPathFind = true;
					}
#endif
                }

                Log._Debug("Setting _replacementPathFinds");
                FieldInfo fieldInfo = typeof(PathManager).GetField(
                    "m_pathfinds",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                Log._Debug("Setting m_pathfinds to custom collection");
                fieldInfo?.SetValue(this, _replacementPathFinds);

                for (int i = 0; i < numOfStockPathFinds; i++) {
                    Log._Debug($"PF {i}: {stockPathFinds[i].m_queuedPathFindCount} queued path-finds");

                    // would cause deadlock since we have a lock on m_bufferLock
                    // stockPathFinds[i].WaitForAllPaths();
                    Destroy(stockPathFinds[i]);
                }
            }

            InitDone = true;
        }

        [RedirectMethod]
        public new void ReleasePath(uint unit) {
#if DEBUGPF3
			Log.Warning($"CustomPathManager.ReleasePath({unit}) called.");
#endif

            if (m_pathUnits.m_buffer[unit].m_simulationFlags == 0) {
                return;
            }
            lock(m_bufferLock) {

                int numIters = 0;
                while (unit != 0u) {
                    if (m_pathUnits.m_buffer[unit].m_referenceCount > 1) {
                        --m_pathUnits.m_buffer[unit].m_referenceCount;
                        break;
                    }

                    /*if (this.m_pathUnits.m_buffer[unit].m_pathFindFlags == PathUnit.FLAG_CREATED) {
                            Log.Error($"Will release path unit {unit} which is CREATED!");
                    }*/

                    uint nextPathUnit = m_pathUnits.m_buffer[unit].m_nextPathUnit;
                    m_pathUnits.m_buffer[unit].m_simulationFlags = 0;
                    m_pathUnits.m_buffer[unit].m_pathFindFlags = 0;
                    m_pathUnits.m_buffer[unit].m_nextPathUnit = 0u;
                    m_pathUnits.m_buffer[unit].m_referenceCount = 0;
                    m_pathUnits.ReleaseItem(unit);
                    //queueItems[unit].Reset(); // NON-STOCK CODE
                    unit = nextPathUnit;
                    if (++numIters >= 262144) {
                        CODebugBase<LogChannel>.Error(
                            LogChannel.Core,
                            "Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }

                m_pathUnitCount = (int)(m_pathUnits.ItemCount() - 1u);
            }
        }

        public bool CustomCreatePath(out uint unit,
                                     ref Randomizer randomizer,
                                     PathCreationArgs args) {
            uint pathUnitId;
            lock(m_bufferLock) {

                int numIters = 0;
                while (true) {
                    // NON-STOCK CODE
                    ++numIters;

                    if (!m_pathUnits.CreateItem(out pathUnitId, ref randomizer)) {
                        unit = 0u;
                        return false;
                    }

                    m_pathUnits.m_buffer[pathUnitId].m_simulationFlags = 1;
                    m_pathUnits.m_buffer[pathUnitId].m_referenceCount = 1;
                    m_pathUnits.m_buffer[pathUnitId].m_nextPathUnit = 0u;

                    // NON-STOCK CODE START
                    if (QueueItems[pathUnitId].queued) {
                        ReleasePath(pathUnitId);

                        if (numIters > 10) {
                            unit = 0u;
                            return false;
                        }

                        continue;
                    }

                    break;
                }

                QueueItems[pathUnitId].vehicleType = args.extVehicleType;
                QueueItems[pathUnitId].vehicleId = args.vehicleId;
                QueueItems[pathUnitId].pathType = args.extPathType;
                QueueItems[pathUnitId].spawned = args.spawned;
                QueueItems[pathUnitId].queued = true;
                // NON-STOCK CODE END

                m_pathUnitCount = (int)(m_pathUnits.ItemCount() - 1u);
            }

            unit = pathUnitId;

            if (args.isHeavyVehicle) {
                m_pathUnits.m_buffer[unit].m_simulationFlags |= PathUnit.FLAG_IS_HEAVY;
            }

            if (args.ignoreBlocked || args.ignoreFlooded) {
                m_pathUnits.m_buffer[unit].m_simulationFlags |= PathUnit.FLAG_IGNORE_BLOCKED;
            }

            if (args.stablePath) {
                m_pathUnits.m_buffer[unit].m_simulationFlags |= PathUnit.FLAG_STABLE_PATH;
            }

            if (args.randomParking) {
                m_pathUnits.m_buffer[unit].m_simulationFlags |= PathUnit.FLAG_RANDOM_PARKING;
            }

            if (args.ignoreFlooded) {
                m_pathUnits.m_buffer[unit].m_simulationFlags |= PathUnit.FLAG_IGNORE_FLOODED;
            }

            if (args.hasCombustionEngine) {
                m_pathUnits.m_buffer[unit].m_simulationFlags |= PathUnit.FLAG_COMBUSTION;
            }

            if (args.ignoreCosts) {
                m_pathUnits.m_buffer[unit].m_simulationFlags |= PathUnit.FLAG_IGNORE_COST;
            }

            m_pathUnits.m_buffer[unit].m_pathFindFlags = 0;
            m_pathUnits.m_buffer[unit].m_buildIndex = args.buildIndex;
            m_pathUnits.m_buffer[unit].m_position00 = args.startPosA;
            m_pathUnits.m_buffer[unit].m_position01 = args.endPosA;
            m_pathUnits.m_buffer[unit].m_position02 = args.startPosB;
            m_pathUnits.m_buffer[unit].m_position03 = args.endPosB;
            m_pathUnits.m_buffer[unit].m_position11 = args.vehiclePosition;
            m_pathUnits.m_buffer[unit].m_laneTypes = (byte)args.laneTypes;
            m_pathUnits.m_buffer[unit].m_vehicleTypes = (uint)args.vehicleTypes;
            m_pathUnits.m_buffer[unit].m_length = args.maxLength;
            m_pathUnits.m_buffer[unit].m_positionCount = 20;

            int minQueued = 10000000;
            CustomPathFind pathFind = null;

#if QUEUEDSTATS
            TotalQueuedPathFinds = 0;
#endif
            foreach (CustomPathFind pathFindCandidate in _replacementPathFinds) {
#if QUEUEDSTATS
                TotalQueuedPathFinds += (uint)pathFindCandidate.m_queuedPathFindCount;
#endif
                if (!pathFindCandidate.IsAvailable ||
                    pathFindCandidate.m_queuedPathFindCount >= minQueued) {
                    continue;
                }

                minQueued = pathFindCandidate.m_queuedPathFindCount;
                pathFind = pathFindCandidate;
            }

#if PF_DIJKSTRA
            if (pathFind != null && pathFind.CalculatePath(unit, args.skipQueue)) {
                return true;
            }
#else
			if (pathFind != null && pathFind.ExtCalculatePath(unit, args.skipQueue)) {
				return true;
			}
#endif

            // NON-STOCK CODE START
            lock(m_bufferLock) {

                QueueItems[pathUnitId].queued = false;
                // NON-STOCK CODE END
                ReleasePath(unit);

                // NON-STOCK CODE START
                m_pathUnitCount = (int)(m_pathUnits.ItemCount() - 1u);
            }

            // NON-STOCK CODE END
            return false;
        }



        /// <summary>
        /// Finds a suitable path position for a walking citizen with the given world position.
        /// If secondary lane constraints are given also checks whether there exists another lane that matches those constraints.
        /// </summary>
        /// <param name="pos">world position</param>
        /// <param name="laneTypes">allowed lane types</param>
        /// <param name="vehicleTypes">allowed vehicle types</param>
        /// <param name="otherLaneTypes">allowed lane types for secondary lane</param>
        /// <param name="otherVehicleTypes">other vehicle types for secondary lane</param>
        /// <param name="allowTransport">public transport allowed?</param>
        /// <param name="allowUnderground">underground position allowed?</param>
        /// <param name="position">resulting path position</param>
        /// <returns><code>true</code> if a position could be found, <code>false</code> otherwise</returns>
        public static bool FindCitizenPathPosition(Vector3 pos,
                                                   NetInfo.LaneType laneTypes,
                                                   VehicleInfo.VehicleType vehicleTypes,
                                                   NetInfo.LaneType otherLaneTypes,
                                                   VehicleInfo.VehicleType otherVehicleTypes,
                                                   bool allowTransport,
                                                   bool allowUnderground,
                                                   out PathUnit.Position position) {
            // TODO move to ExtPathManager after harmony upgrade
            position = default(PathUnit.Position);
            float minDist = 1E+10f;
            if (ExtPathManager.Instance.FindPathPositionWithSpiralLoop(
                    pos,
                    ItemClass.Service.Road,
                    laneTypes,
                    vehicleTypes,
                    otherLaneTypes,
                    otherVehicleTypes,
                    allowUnderground,
                    false,
                    Options.parkingAI
                        ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance
                        : 32f,
                    out PathUnit.Position posA,
                    out _,
                    out float distA,
                    out _) && distA < minDist) {
                minDist = distA;
                position = posA;
            }

            if (ExtPathManager.Instance.FindPathPositionWithSpiralLoop(
                    pos,
                    ItemClass.Service.Beautification,
                    laneTypes,
                    vehicleTypes,
                    otherLaneTypes,
                    otherVehicleTypes,
                    allowUnderground,
                    false,
                    Options.parkingAI
                        ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance
                        : 32f,
                    out posA,
                    out _,
                    out distA,
                    out _) && distA < minDist) {
                minDist = distA;
                position = posA;
            }

            if (allowTransport && ExtPathManager.Instance.FindPathPositionWithSpiralLoop(
                    pos,
                    ItemClass.Service.PublicTransport,
                    laneTypes,
                    vehicleTypes,
                    otherLaneTypes,
                    otherVehicleTypes,
                    allowUnderground,
                    false,
                    Options.parkingAI
                        ? GlobalConfig
                          .Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance
                        : 32f,
                    out posA,
                    out _,
                    out distA,
                    out _) && distA < minDist) {
                position = posA;
            }

            return position.m_segment != 0;
        }

        /*internal void ResetQueueItem(uint unit) {
                queueItems[unit].Reset();
        }*/

        private void StopPathFinds() {
            foreach (CustomPathFind pathFind in _replacementPathFinds) {
                Destroy(pathFind);
            }
        }

        protected virtual void OnDestroy() {
            Log._Debug("CustomPathManager: OnDestroy");
            StopPathFinds();
        }
    }
}
