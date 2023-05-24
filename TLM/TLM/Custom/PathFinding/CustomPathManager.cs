// #define QUEUEDSTATS
// #define DEBUGPF3

namespace TrafficManager.Custom.PathFinding {
    using ColossalFramework.Math;
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Reflection;
    using System;
    using System.Threading;
    using API.Traffic.Enums;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using UnityEngine;
    using ColossalFramework.UI;
    using Lifecycle;

    public class CustomPathManager : PathManager {
        public const int DEFAULT_SIM_SLEEP_TIME = 100;
        /// <summary>
        /// Holds a linked list of path units waiting to be calculated
        /// </summary>
        internal PathUnitQueueItem[] QueueItems; // TODO move to ExtPathManager

        private CustomPathFind[] _replacementPathFinds;

        public static CustomPathManager _instance;

        private PathManager stockPathManager_;

        private bool terminated_;

        private int simSleepMultiplier_ = DEFAULT_SIM_SLEEP_TIME;//vanilla value

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

        /// <summary>
        /// Initializes CustomPathManager if necessary, applies PF compatibility patches
        /// </summary>
        public static void Initialize() {
            try {
                Log.Info("CustomPathManager.Initialize() called.");
                var pathManager = PathManager.instance.gameObject.GetComponent<CustomPathManager>();
                if (!pathManager) {
                    Log.Info("CustomPathManager.Initialize() CustomPathManager not found, creating new instance.");
                    PathManager.instance.gameObject.AddComponent<CustomPathManager>();
                }
            } catch (Exception ex) {
                string error =
                    "Traffic Manager: President Edition failed to load. You can continue " +
                    "playing but it's NOT recommended. Traffic Manager will not work as expected.";
                Log.Error(error);
                Log.Error($"Path manager replacement error: {ex}");
                UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel")
                      .SetMessage("TM:PE failed to load", error, true);
            }
        }

        /// <summary>
        /// Either initializes the custom pathfinding or destroys already created and instantiates it again
        /// The main use of this method is when other mods need to patch TM:PE pathfinding in some way
        /// and ensure that the mod will use patched methods, and process of patching might be dependant on order of loading mods
        /// </summary>
        [UsedImplicitly]
        public static void Reinitialize(Array32<PathUnit> pathUnits) {
            try {
                Log.Info("CustomPathManager.Reinitialize() called.");
                var pathManager = PathManager.instance.gameObject.GetComponent<CustomPathManager>();
                if (!pathManager) {
                    Log.Info("CustomPathManager.Reinitialize() CustomPathManager not found, creating new instance.");
                    PathManager.instance.gameObject.AddComponent<CustomPathManager>();
                    _instance.UpdatePathUnitsReference(pathUnits);
                } else {
                    Log.Warning("CustomPathManager.Reinitialize() CustomPathManager found, destroying old instance.");
                    DestroyImmediate(pathManager);
                    Log.Info("CustomPathManager.Reinitialize() Creating new instance...");
                    PathManager.instance.gameObject.AddComponent<CustomPathManager>();
                    _instance.UpdatePathUnitsReference(pathUnits);
                    Log.Info("CustomPathManager.Reinitialize() Reinitialization finished.");
                }
            } catch (Exception ex) {
                string error =
                    "Traffic Manager: President Edition failed to reinitialize custom pathfinding.\n" +
                    "Traffic Manager will not work as expected.";
                Log.Error(error);
                Log.Error($"Path manager reinitialization error: {ex}");
                UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel")
                      .SetMessage("TM:PE failed to load", error, true);
            }
        }

        [UsedImplicitly]
        protected override void Awake() {
            // On waking up, replace the stock pathfinders with the custom one
            // but retain the original version for future replace
            // also suppress call to base class.
            _instance = this;
            //install PathFinding patches
            Patcher.InstallPathFinding();

            stockPathManager_ = PathManager.instance
                                ?? throw new Exception("stockPathManager is null");
            Log._Debug($"Got stock PathManager instance: {stockPathManager_?.GetType().FullName}");

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
            // utilize more threads if possible (systems with 8+) otherwise use vanilla calculation
            // <=8t CPU -> 1..4 PT threads (vanilla)
            // 10t CPU -> 5 PT threads
            // 16t CPU -> 8 PT threads (2x vanilla)
            // >=20t CPU -> 10 PT threads
            int numCustomPathFinds = Mathf.Clamp(SystemInfo.processorCount / 2, 1, 10);
            simSleepMultiplier_ = CalculateBestSimSleepMultiplier(numCustomPathFinds);

            Log._Debug($"Detected {SystemInfo.processorCount} CPU thread(s). Creating {numCustomPathFinds} custom PathFind objects. Number of stock Pathfinds {numOfStockPathFinds}");
            _replacementPathFinds = new CustomPathFind[numCustomPathFinds];
            FieldInfo f_pathfinds = typeof(PathManager).GetField(
                                        "m_pathfinds",
                                        BindingFlags.NonPublic | BindingFlags.Instance)
                                    ?? throw new Exception("f_pathFinds is null");

            lock (m_bufferLock) {

                for (int i = 0; i < numCustomPathFinds; i++) {
                    _replacementPathFinds[i] = gameObject.AddComponent<CustomPathFind>();
                }

                f_pathfinds?.SetValue(this, _replacementPathFinds);

                Log._Debug("Destroying " + numOfStockPathFinds + " PathFind objects.");
                for (int i = 0; i < numOfStockPathFinds; i++) {
                    Log._Debug($"PF {i}: {stockPathFinds[i].m_queuedPathFindCount} queued path-finds. PF: {stockPathFinds[i]?.GetType().FullName ?? "<null>"} active: ({(bool)stockPathFinds[i]})");

                    // would cause deadlock since we have a lock on m_bufferLock
                    // stockPathFinds[i].WaitForAllPaths();
                    DestroyImmediate(stockPathFinds[i]);
                }
            }
        }

        public void UpdateOldPathManagerValues(PathManager stockPathManager) {
            stockPathManager.m_drawCallData = m_drawCallData;
            stockPathManager.m_pathUnitCount = m_pathUnitCount;
            stockPathManager.m_renderPathGizmo = m_renderPathGizmo;

            int n = _replacementPathFinds.Length;

            // both stock and custom PathMangers use the same lock object
            lock (m_bufferLock) {
                Log._Debug("Destroying " + n + " PathFind objects.");
                for (int i = 0; i < n; i++) {
                    Log._Debug($"PF {i}: {_replacementPathFinds[i].m_queuedPathFindCount} queued path-finds.  PF: {_replacementPathFinds[i]?.GetType().FullName ?? "<null>"} active: ({(bool)_replacementPathFinds[i]})");

                    // would cause deadlock since we have a lock on m_bufferLock
                    // customPathFinds[i].WaitForAllPaths();
                    DestroyImmediate(_replacementPathFinds[i]);
                }

                // revert to vanilla number of threads
                n = Mathf.Clamp(SystemInfo.processorCount / 2, 1, 4);
                Log._Debug("Creating " + n + " stock PathFind objects.");
                PathFind[] stockPathFinds = new PathFind[n];
                for (int i = 0; i < n; i++) {
                    stockPathFinds[i] = gameObject.AddComponent<PathFind>();
                }

                FieldInfo f_pathfinds = typeof(PathManager).GetField(
                                            "m_pathfinds",
                                            BindingFlags.NonPublic | BindingFlags.Instance)
                                        ?? throw new Exception("f_pathFinds is null");
                f_pathfinds?.SetValue(stockPathManager, stockPathFinds);
            }
        }

        private void UpdatePathUnitsReference(Array32<PathUnit> pathUnits) {
            if (_instance) {
                lock (m_bufferLock) {
                    // update array reference on cached vanilla(stock) PathManager
                    if (stockPathManager_) {
                        stockPathManager_.m_pathUnits = pathUnits;
                    }

                    _instance.m_pathUnits = pathUnits;
                    // update path unit queue in case of different size
                    if (_instance.QueueItems.Length != pathUnits.m_size) {
                        _instance.QueueItems = new PathUnitQueueItem[pathUnits.m_size];
                    }

                    // update array reference for all custom pathfind threads
                    for (var i = 0; i < _replacementPathFinds.Length; i++) {
                       _replacementPathFinds[i].SetPathUnits(pathUnits);
                   }
                }
            }
        }

        /// <summary>
        /// Just in case other mod call ReleasePath on CustomPathManager
        /// </summary>
        /// <param name="unit"></param>
        public new void ReleasePath(uint unit) {
            CustomReleasePath(unit);
        }

        internal void CustomReleasePath(uint unit) {
#if DEBUGPF3
			Log.Warning($"CustomPathManager.CustomReleasePath({unit}) called.");
#endif

            if (m_pathUnits.m_buffer[unit].m_simulationFlags == 0) {
                return;
            }

            lock (m_bufferLock) {

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
            lock (m_bufferLock) {

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
                        CustomReleasePath(pathUnitId);

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
            m_pathUnits.m_buffer[unit].m_vehicleCategories = (long)args.vehicleCategories;
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
            lock (m_bufferLock) {

                QueueItems[pathUnitId].queued = false;
                // NON-STOCK CODE END
                CustomReleasePath(unit);

                // NON-STOCK CODE START
                m_pathUnitCount = (int)(m_pathUnits.ItemCount() - 1u);
            }

            // NON-STOCK CODE END
            return false;
        }

        /// <summary>
        /// Builds Creates Path for TransportLineAI
        /// </summary>
        /// <param name="path"></param>
        /// <param name="startPosA"></param>
        /// <param name="startPosB"></param>
        /// <param name="endPosA"></param>
        /// <param name="endPosB"></param>
        /// <param name="vehicleType"></param>
        /// <param name="skipQueue"></param>
        /// <returns>bool</returns>
        public bool CreateTransportLinePath(
                out uint path,
                PathUnit.Position startPosA,
                PathUnit.Position startPosB,
                PathUnit.Position endPosA,
                PathUnit.Position endPosB,
                VehicleInfo.VehicleType vehicleType,
                VehicleInfo.VehicleCategory vehicleCategory,
                bool skipQueue) {

            PathCreationArgs args = new PathCreationArgs {
                extPathType = ExtPathType.None,
                extVehicleType = ExtVehicleManager.ConvertToExtVehicleType(vehicleType, vehicleCategory),
                vehicleId = 0,
                spawned = true,
                buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex,
                startPosA = startPosA,
                startPosB = startPosB,
                endPosA = endPosA,
                endPosB = endPosB,
                vehiclePosition = default,
                vehicleTypes = vehicleType,
                vehicleCategories = vehicleCategory,
                isHeavyVehicle = false,
                hasCombustionEngine = false,
                ignoreBlocked = true,
                ignoreFlooded = false,
                ignoreCosts = false,
                randomParking = false,
                stablePath = true,
                skipQueue = skipQueue
            };

            if (vehicleType == VehicleInfo.VehicleType.None) {
                args.laneTypes = NetInfo.LaneType.Pedestrian;
                args.maxLength = 160000f;
            } else {
                args.laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
                args.maxLength = 20000f;
            }

            return CustomCreatePath(out path, ref SimulationManager.instance.m_randomizer, args);
        }

        /// <summary>
        /// Unloads CustomPathManager - destroys the instance, removes PF patches,
        /// recovers vanilla PathManager along with vanilla PF threads,
        /// </summary>
        public void Unload() {
            Log.Info("Unloading CustomPathManager");
            DestroyImmediate(this);
        }

        public int CalculateBestSimSleepMultiplier(int numberOfThreads) {
            if (numberOfThreads < 4)
                return DEFAULT_SIM_SLEEP_TIME; //vanilla value

            /*
             * 4 - 200
             * 5 - 250
             * 6 - 300
             */
            return numberOfThreads * 50;
        }

        /// <summary>
        /// Override required to tweak
        /// </summary>
        /// <param name="subStep"></param>
        protected override void SimulationStepImpl(int subStep) {
            int num = 0;
            for (int i = 0; i < _replacementPathFinds.Length; i++) {
                num = Mathf.Max(num, _replacementPathFinds[i].m_queuedPathFindCount);
            }

            // put simulation thread to sleep when queued path exceed limit
            if (num >= simSleepMultiplier_ && !terminated_) {
                Thread.Sleep((num - simSleepMultiplier_) / simSleepMultiplier_ + 1);
            }
        }

        /// <summary>
        /// Waits for all custom PF threads to finish their work
        /// </summary>
        public new void WaitForAllPaths() {
            Log.Info("CustomPathManager.WaitForAllPaths()");
            for (int i = 0; i < _replacementPathFinds.Length; i++) {
                _replacementPathFinds[i].WaitForAllPaths();
            }
        }

        protected virtual void OnDestroy() {
            Log._Debug("CustomPathManager: OnDestroy");
            WaitForAllPaths();

            // Uninstall pathfinding patches
            Patcher.Uninstall(API.Harmony.HARMONY_ID_PATHFINDING);

            PathManagerInstance.SetValue(null, stockPathManager_);
            Log.Info("Should be stock: " + PathManager.instance.GetType().FullName);

            UpdateOldPathManagerValues(stockPathManager_);
            var simManagers = GetSimulationManagers();

            simManagers.Remove(this);

            simManagers.Add(stockPathManager_);
            terminated_ = true;
            _instance = null;
            Log._Debug("CustomPathManager: Destroyed");
        }
    }
}