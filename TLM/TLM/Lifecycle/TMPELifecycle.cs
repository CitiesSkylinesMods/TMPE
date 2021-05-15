namespace TrafficManager.Lifecycle {
    using CitiesHarmony.API;
    using ColossalFramework;
    using ColossalFramework.Globalization;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.API.Manager;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.UI;
    using TrafficManager.Util;
    using TrafficManager.State.Asset;
    using UnityEngine.SceneManagement;
    using UnityEngine;
    using JetBrains.Annotations;

    /// <summary>
    /// Do not use Singleton<TMPELifecycle>.instance to prevent memory leak.
    /// Instead use the TMPELifecycle.Instance property.
    /// </summary>
    public class TMPELifecycle : MonoBehaviour {
        public static TMPELifecycle Instance {
            /// <summary> returns instance of TMPELifecycle if it exists. returns null otherwise. </summary>
            get;
            private set;
        }

        /// <summary>TMPE is in the middle of deserializing data.</summary>
        public bool Deserializing { get; set; }

        /// <summary>
        /// Contains loaded languages and lookup functions for text translations
        /// </summary>
        public Translation TranslationDatabase { get; } = new Translation();
        public List<ICustomManager> RegisteredManagers { get; } = new List<ICustomManager>();

        public bool InGameHotReload { get; private set; }
        public bool IsGameLoaded { get; private set; }

        public Dictionary<BuildingInfo, AssetData> Asset2Data { get; set; }

        /// <summary>
        /// determines if simulation is inside game/editor. useful to detect hot-reload.
        /// </summary>
        public static bool InGameOrEditor() =>
            SceneManager.GetActiveScene().name != "IntroScreen" &&
            SceneManager.GetActiveScene().name != "MainMenu" &&
            SceneManager.GetActiveScene().name != "Startup";

        public static AppMode? AppMode => SimulationManager.instance.m_ManagersWrapper.loading?.currentMode;

        public static SimulationManager.UpdateMode UpdateMode => SimulationManager.instance.m_metaData.m_updateMode;
        public static LoadMode Mode => (LoadMode)UpdateMode;
        public static string Scene => SceneManager.GetActiveScene().name;

        /// <summary>
        /// determines whether Game mode as oppose to edit mode (eg asset editor).
        /// </summary>
        internal static bool PlayMode => AppMode != null && AppMode == ICities.AppMode.Game;

        private static void CompatibilityCheck() {
            ModsCompatibilityChecker mcc = new ModsCompatibilityChecker();
            mcc.PerformModCheck();
            VersionUtil.CheckGameVersion();
        }

        internal void Preload() {
            Asset2Data = new Dictionary<BuildingInfo, AssetData>();
            RegisterCustomManagers();
        }

        internal void RegisterCustomManagers() {
            // TODO represent data dependencies differently
            RegisteredManagers.Add(ExtNodeManager.Instance);
            RegisteredManagers.Add(ExtSegmentManager.Instance);
            RegisteredManagers.Add(ExtSegmentEndManager.Instance);
            RegisteredManagers.Add(GeometryManager.Instance);
            RegisteredManagers.Add(AdvancedParkingManager.Instance);
            RegisteredManagers.Add(CustomSegmentLightsManager.Instance);
            RegisteredManagers.Add(ExtBuildingManager.Instance);
            RegisteredManagers.Add(ExtCitizenInstanceManager.Instance);
            RegisteredManagers.Add(ExtCitizenManager.Instance);
            RegisteredManagers.Add(ExtPathManager.Instance);
            RegisteredManagers.Add(TurnOnRedManager.Instance);
            RegisteredManagers.Add(LaneArrowManager.Instance);
            RegisteredManagers.Add(LaneConnectionManager.Instance);
            RegisteredManagers.Add(OptionsManager.Instance);
            RegisteredManagers.Add(ParkingRestrictionsManager.Instance);
            RegisteredManagers.Add(RoutingManager.Instance);
            RegisteredManagers.Add(SpeedLimitManager.Instance);
            RegisteredManagers.Add(TrafficLightManager.Instance);
            RegisteredManagers.Add(TrafficLightSimulationManager.Instance);
            RegisteredManagers.Add(TrafficMeasurementManager.Instance);
            RegisteredManagers.Add(TrafficPriorityManager.Instance);
            RegisteredManagers.Add(UtilityManager.Instance);
            RegisteredManagers.Add(VehicleRestrictionsManager.Instance);
            RegisteredManagers.Add(ExtVehicleManager.Instance);

            // depends on TurnOnRedManager, TrafficLightManager, TrafficLightSimulationManager
            RegisteredManagers.Add(JunctionRestrictionsManager.Instance);
        }

        [UsedImplicitly]
        void Awake() {
            try {
                Log.Info("TMPELifecycle.Awake()");
                Instance = this;
#if BENCHMARK
            Benchmark.BenchmarkManager.Setup();
#endif
                TranslationDatabase.LoadAllTranslations();

                VersionUtil.LogEnvironmentDetails();
                Log._Debug("Scene is " + Scene);

                // check for incompatible mods
                if (UIView.GetAView() != null) {
                    // when TM:PE is enabled in content manager
                    CompatibilityCheck();
                } else {
                    // or when game first loads if TM:PE was already enabled
                    LoadingManager.instance.m_introLoaded += CompatibilityCheck;
                }

                HarmonyHelper.EnsureHarmonyInstalled();
                LoadingManager.instance.m_levelPreLoaded += Preload;

                InGameHotReload = InGameOrEditor();
                if (InGameHotReload) {
                    Preload();
                    SerializableDataExtension.Load();
                    Load();
                }

#if DEBUG
                const bool installHarmonyASAP = false; // set true for fast testing
                if (installHarmonyASAP)
                    HarmonyHelper.DoOnHarmonyReady(Patcher.Install);
#endif
            } catch (Exception ex) {
                ex.LogException(true);
            }
        }

        [UsedImplicitly]
        void OnDestroy() {
            try {
                Log.Info("TMPELifecycle.OnDestroy()");
                LoadingManager.instance.m_introLoaded -= CompatibilityCheck;
                LocaleManager.eventLocaleChanged -= Translation.HandleGameLocaleChange;
                LoadingManager.instance.m_levelPreLoaded -= Preload;

                if (IsGameLoaded) {
                    //Hot Unload
                    Unload();
                }
                Instance = null;
            } catch (Exception ex) {
                ex.LogException(true);
            }
        }

#if DEBUG
        ~TMPELifecycle() {
            // ensure there is no memory leak:
            Log._Debug("TMPELifecycle.~TMPELifecycle()");
        }
#endif

        internal static void StartMod() {
            var go = new GameObject(nameof(TMPELifecycle), typeof(TMPELifecycle));
            DontDestroyOnLoad(go); // don't destroy when scene changes.
        }

        internal static void EndMod() {
            DestroyImmediate(Instance?.gameObject);
        }

        internal void Load() {
            try {
                Log.Info($"TMPELifecycle.Load() called. Mode={Mode}, UpdateMode={UpdateMode}, Scene={Scene}");

                if (Scene == "ThemeEditor")
                    return;

                IsGameLoaded = false;

                InGameUtil.Instantiate();

                VersionUtil.CheckGameVersion();

                IsGameLoaded = true;

                CustomPathManager.OnLevelLoaded();

                ModUI.OnLevelLoaded();
                if (PlayMode) {
                    UIView uiView = UIView.GetAView();
                    uiView.AddUIComponent(typeof(UITransportDemand));
                    uiView.gameObject.AddComponent<RemoveVehicleButtonExtender>();
                    uiView.gameObject.AddComponent<RemoveCitizenInstanceButtonExtender>();
                    uiView.gameObject.AddComponent<RoadSelectionPanels>();
                }

                Patcher.Install();

                Log.Info("Notifying managers...");
                foreach (ICustomManager manager in RegisteredManagers) {
                    Log.Info($"OnLevelLoading: {manager.GetType().Name}");
                    manager.OnLevelLoading();
                }

                Log.Info("OnLevelLoaded complete.");
            } catch (Exception ex) {
                ex.LogException(true);
            }
        }

        internal void Unload() {
            try {
                CustomPathManager._instance?.OnLevelUnloading();
                foreach (ICustomManager manager in RegisteredManagers.AsEnumerable().Reverse()) {
                    try {
                        Log.Info($"OnLevelUnloading: {manager.GetType().Name}");
                        manager.OnLevelUnloading();
                    } catch (Exception ex) {
                        ex.LogException(true);
                    }
                }

                try {
                    Flags.OnLevelUnloading();
                }
                catch (Exception ex) {
                    ex.LogException(true);
                }
                
                GlobalConfig.OnLevelUnloading();

                // destroy immidately to prevent duplicates after hot-reload.
                var uiviewGO = UIView.GetAView().gameObject;
                DestroyImmediate(uiviewGO.GetComponent<RoadSelectionPanels>());
                DestroyImmediate(uiviewGO.GetComponent<RemoveVehicleButtonExtender>());
                DestroyImmediate(uiviewGO.GetComponent<RemoveCitizenInstanceButtonExtender>());
                DestroyImmediate(uiviewGO.GetComponent<RemoveCitizenInstanceButtonExtender>());
                DestroyImmediate(uiviewGO.GetComponent<UITransportDemand>());

                Log.Info("Removing Controls from UI.");
                if (ModUI.Instance) {
                    ModUI.Instance.CloseMainMenu(); // Hide the UI ASAP
                    ModUI.Instance.Destroy();
                    Log._Debug("removed UIBase instance.");
                }
            } catch (Exception ex) {
                ex.LogException(true);
            }

            Patcher.Uninstall();

            IsGameLoaded = false;
            InGameHotReload = false;
        }
    }
}
