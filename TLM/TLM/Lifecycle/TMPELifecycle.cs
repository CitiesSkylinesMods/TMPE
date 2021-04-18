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
    using System.Reflection;
    using TrafficManager.API.Manager;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.UI;
    using TrafficManager.Util;
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
        public bool Deserializing;

        // These values from `BuildConfig` class (`APPLICATION_VERSION` constants) in game file `Managed/Assembly-CSharp.dll` (use ILSpy to inspect them)
        public const uint GAME_VERSION = 188868624U;
        public const uint GAME_VERSION_A = 1u;
        public const uint GAME_VERSION_B = 13u;
        public const uint GAME_VERSION_C = 0u;
        public const uint GAME_VERSION_BUILD = 8u;

        /// <summary>
        /// Contains loaded languages and lookup functions for text translations
        /// </summary>
        public Translation TranslationDatabase { get; } = new Translation();
        public List<ICustomManager> RegisteredManagers { get; } = new List<ICustomManager>();

        public bool InGameHotReload { get; private set; }
        public bool IsGameLoaded { get; private set; }

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


        private static void CheckForIncompatibleMods() {
            ModsCompatibilityChecker mcc = new ModsCompatibilityChecker();
            mcc.PerformModCheck();
        }

        private void RegisterCustomManagers() {
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

                Log.InfoFormat(
                    "TM:PE enabled. Version {0}, Build {1} {2} for game version {3}.{4}.{5}-f{6}",
                    TrafficManagerMod.VersionString,
                    Assembly.GetExecutingAssembly().GetName().Version,
                    TrafficManagerMod.BRANCH,
                    GAME_VERSION_A,
                    GAME_VERSION_B,
                    GAME_VERSION_C,
                    GAME_VERSION_BUILD);
                Log.InfoFormat(
                    "Enabled TM:PE has GUID {0}",
                    Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId);

                // check for incompatible mods
                if (UIView.GetAView() != null) {
                    // when TM:PE is enabled in content manager
                    CheckForIncompatibleMods();
                } else {
                    // or when game first loads if TM:PE was already enabled
                    LoadingManager.instance.m_introLoaded += CheckForIncompatibleMods;
                }

                // Log Mono version
                Type monoRt = Type.GetType("Mono.Runtime");
                if (monoRt != null) {
                    MethodInfo displayName = monoRt.GetMethod(
                        "GetDisplayName",
                        BindingFlags.NonPublic | BindingFlags.Static);
                    if (displayName != null) {
                        Log.InfoFormat("Mono version: {0}", displayName.Invoke(null, null));
                    }
                }

                Log._Debug("Scene is " + SceneManager.GetActiveScene().name);

                HarmonyHelper.EnsureHarmonyInstalled();

                InGameHotReload = InGameOrEditor();
                if (InGameHotReload) {
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
                LoadingManager.instance.m_introLoaded -= CheckForIncompatibleMods;
                LocaleManager.eventLocaleChanged -= Translation.HandleGameLocaleChange;

                if (InGameOrEditor() && IsGameLoaded) {
                    //Hot Unload
                    Unload();
                }
                Instance = null;
            } catch(Exception ex) {
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
            Destroy(Instance?.gameObject);
        }

        internal void Load() {
            Log.Info($"TMPELifecycle.Load() called. Mode={Mode}, UpdateMode={UpdateMode}, Scene={Scene}");

            if (Scene == "ThemeEditor")
                return;

            InGameUtil.Instantiate();

            IsGameLoaded = false;

            if (BuildConfig.applicationVersion != BuildConfig.VersionToString(
                    GAME_VERSION,
                    false)) {
                string[] majorVersionElms = BuildConfig.applicationVersion.Split('-');
                string[] versionElms = majorVersionElms[0].Split('.');
                uint versionA = Convert.ToUInt32(versionElms[0]);
                uint versionB = Convert.ToUInt32(versionElms[1]);
                uint versionC = Convert.ToUInt32(versionElms[2]);

                Log.Info($"Detected game version v{BuildConfig.applicationVersion}");

                bool isModTooOld = GAME_VERSION_A < versionA ||
                                   (GAME_VERSION_A == versionA &&
                                    GAME_VERSION_B < versionB);


                bool isModNewer = GAME_VERSION_A < versionA ||
                                  (GAME_VERSION_A == versionA &&
                                   GAME_VERSION_B > versionB);


                if (isModTooOld) {
                    string msg = string.Format(
                        "Traffic Manager: President Edition detected that you are running " +
                        "a newer game version ({0}) than TM:PE has been built for ({1}). " +
                        "Please be aware that TM:PE has not been updated for the newest game " +
                        "version yet and thus it is very likely it will not work as expected.",
                        BuildConfig.applicationVersion,
                        BuildConfig.VersionToString(GAME_VERSION, false));

                    Log.Error(msg);
                    Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(
                            () => {
                                UIView.library
                                      .ShowModal<ExceptionPanel>("ExceptionPanel")
                                      .SetMessage(
                                          "TM:PE has not been updated yet",
                                          msg,
                                          false);
                            });
                } else if (isModNewer) {
                    string msg = string.Format(
                        "Traffic Manager: President Edition has been built for game version {0}. " +
                        "You are running game version {1}. Some features of TM:PE will not " +
                        "work with older game versions. Please let Steam update your game.",
                        BuildConfig.VersionToString(GAME_VERSION, false),
                        BuildConfig.applicationVersion);

                    Log.Error(msg);
                    Singleton<SimulationManager>
                        .instance.m_ThreadingWrapper.QueueMainThread(
                            () => {
                                UIView.library
                                      .ShowModal<ExceptionPanel>("ExceptionPanel")
                                      .SetMessage(
                                          "Your game should be updated",
                                          msg,
                                          false);
                            });
                }
            }

            IsGameLoaded = true;

            RegisterCustomManagers();
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

            // Log.Info("Fixing non-created nodes with problems...");
            // FixNonCreatedNodeProblems();
            Log.Info("Notifying managers...");
            foreach (ICustomManager manager in RegisteredManagers) {
                Log.Info($"OnLevelLoading: {manager.GetType().Name}");
                manager.OnLevelLoading();
            }

            // InitTool();
            // Log._Debug($"Current tool: {ToolManager.instance.m_properties.CurrentTool}");
            Log.Info("OnLevelLoaded complete.");
        }

        internal void Unload() {
            CustomPathManager._instance?.OnLevelUnloading();

            try {
                foreach (ICustomManager manager in RegisteredManagers.AsEnumerable().Reverse()) {
                    Log.Info($"OnLevelUnloading: {manager.GetType().Name}");
                    manager.OnLevelUnloading();
                }

                Flags.OnLevelUnloading();
                GlobalConfig.OnLevelUnloading();

                var uiviewGO = UIView.GetAView().gameObject;
                Destroy(uiviewGO.GetComponent<RoadSelectionPanels>());
                Destroy(uiviewGO.GetComponent<RemoveVehicleButtonExtender>());
                Destroy(uiviewGO.GetComponent<RemoveCitizenInstanceButtonExtender>());
                Destroy(uiviewGO.GetComponent<RemoveCitizenInstanceButtonExtender>());
                Destroy(uiviewGO.GetComponent<UITransportDemand>());

                Log.Info("Removing Controls from UI.");
                if (ModUI.Instance != null) {
                    ModUI.Instance.CloseMainMenu(); // Hide the UI ASAP
                    ModUI.Instance.Destroy();
                    Log._Debug("removed UIBase instance.");
                }
            } catch (Exception e) {
                Log.Error("Exception unloading mod. " + e.Message);

                // ignored - prevents collision with other mods
            }

            Patcher.Uninstall();
            IsGameLoaded = false;
            InGameHotReload = false;
        }
    }
}
