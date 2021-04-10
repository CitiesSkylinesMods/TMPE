// #define QUEUEDSTATS
// #define DEBUGPF3

namespace TrafficManager.Custom.PathFinding {
    using ColossalFramework.Math;
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Reflection;
    using System;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using TrafficManager.RedirectionFramework.Attributes;
    using TrafficManager.State;
    using UnityEngine;
    using ColossalFramework.UI;

    [TargetType(typeof(PathManager))]
    public class CustomPathManager : PathManager {
        /// <summary>
        /// Holds a linked list of path units waiting to be calculated
        /// </summary>
        internal PathUnitQueueItem[] QueueItems; // TODO move to ExtPathManager

        private CustomPathFind[] _replacementPathFinds;

        public static CustomPathManager _instance => PathManager.instance as CustomPathManager;

        private PathManager stockPathManager_;

        private static FastList<ISimulationManager> GetSimulationManagers() =>
            typeof(SimulationManager)
            .GetField("m_managers", BindingFlags.Static | BindingFlags.NonPublic)
            ?.GetValue(null)
            as FastList<ISimulationManager>
            ?? throw new Exception("could not get SimulationManager.m_managers");

        private static FieldInfo PathManagerInstance =>
            typeof(Singleton<PathManager>)
            .GetField(
            "sInstance",
            BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new Exception("pathManagerInstance is null");

#if QUEUEDSTATS
        public static uint TotalQueuedPathFinds { get; private set; }
#endif

        public static void OnLevelLoaded() {
            try {
                Log.Info("CustomPathManager.OnLevelLoaded() called.");
                PathManager.instance.gameObject.AddComponent<CustomPathManager>();
            } catch(Exception ex) {
                string error =
                    "Traffic Manager: President Edition failed to load. You can continue " +
                    "playing but it's NOT recommended. Traffic Manager will not work as expected.";
                Log.Error(error);
                Log.Error($"Path manager replacement error: {ex}");
                UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel")
                    .SetMessage("TM:PE failed to load", error, true);
            }
        }

        [UsedImplicitly]
        protected override void Awake() {
            // On waking up, replace the stock pathfinders with the custom one
            // but retain the original version for future replace
            // also suppress call to base class.
            stockPathManager_ = PathManager.instance
                ?? throw new Exception("stockPathManager is null");
            Log._Debug($"Got stock PathManager instance {stockPathManager_?.GetName()}");
            PathManagerInstance.SetValue(null, this);
            Log._Debug("Should be custom: " + PathManager.instance.GetType());

            UpdateWithPathManagerValues(stockPathManager_);

            var simManagers = GetSimulationManagers();
            simManagers.Remove(stockPathManager_);
            simManagers.Add(this);
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

            QueueItems = new PathUnitQueueItem[MAX_PATHUNIT_COUNT];

            PathFind[] stockPathFinds = GetComponents<PathFind>();
            int numOfStockPathFinds = stockPathFinds.Length;
            int numCustomPathFinds = numOfStockPathFinds;

            Log._Debug("Creating " + numCustomPathFinds + " custom PathFind objects.");
            _replacementPathFinds = new CustomPathFind[numCustomPathFinds];
            FieldInfo f_pathfinds = typeof(PathManager).GetField(
                "m_pathfinds",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new Exception("f_pathFinds is null");

            lock(m_bufferLock) {

                for (int i = 0; i < numCustomPathFinds; i++) {
                    _replacementPathFinds[i] = gameObject.AddComponent<CustomPathFind>();
                }

                f_pathfinds?.SetValue(this, _replacementPathFinds);

                for (int i = 0; i < numOfStockPathFinds; i++) {
                    Log._Debug($"PF {i}: {stockPathFinds[i].m_queuedPathFindCount} queued path-finds");

                    // would cause deadlock since we have a lock on m_bufferLock
                    // stockPathFinds[i].WaitForAllPaths();
                    Destroy(stockPathFinds[i]);
                }
            }
        }

        public void UpdateOldPathManagerValues(PathManager stockPathManager) {
            stockPathManager.m_drawCallData = m_drawCallData;
            stockPathManager.m_pathUnitCount = m_pathUnitCount;
            stockPathManager.m_renderPathGizmo = m_renderPathGizmo;

            int n = _replacementPathFinds.Length;

            Log._Debug("Creating " + n + " stock PathFind objects.");
            PathFind[] stockPathFinds = new PathFind[n];

            FieldInfo f_pathfinds = typeof(PathManager).GetField(
                  "m_pathfinds",
                  BindingFlags.NonPublic | BindingFlags.Instance)
                  ?? throw new Exception("f_pathFinds is null");

            lock(PathManager.instance.m_bufferLock) {
                for (int i = 0; i < n; i++) {
                    stockPathFinds[i] = gameObject.AddComponent<PathFind>();
                }

                f_pathfinds?.SetValue(stockPathManager, stockPathFinds);

                for (int i = 0; i < n; i++) {
                    Log._Debug($"PF {i}: {_replacementPathFinds[i].m_queuedPathFindCount} queued path-finds");

                    // would cause deadlock since we have a lock on m_bufferLock
                    // customPathFinds[i].WaitForAllPaths();
                    Destroy(_replacementPathFinds[i]);
                }
            }
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

            if (pathFind != null && pathFind.CalculatePath(unit, args.skipQueue)) {
                return true;
            }

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

        public void OnLevelUnloading() {
            Log.Info("CustomPathManager.OnLevelUnloading()");
            DestroyImmediate(this);
        }

        protected virtual void OnDestroy() {
            Log._Debug("CustomPathManager: OnDestroy");
            WaitForAllPaths();
            UpdateOldPathManagerValues(stockPathManager_);
            var simManagers = GetSimulationManagers();

            simManagers.Remove(this);

            simManagers.Add(stockPathManager_);

            PathManagerInstance.SetValue(null, stockPathManager_);
            Log._Debug("Should be stock: " + PathManager.instance.GetType());
        }
    }
}