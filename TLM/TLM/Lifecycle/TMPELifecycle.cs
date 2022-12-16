namespace TrafficManager.Lifecycle {
    using CitiesHarmony.API;
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
    using TrafficManager.Manager.Impl.LaneConnection;
    using TrafficManager.State;
    using TrafficManager.UI;
    using TrafficManager.Util;
    using TrafficManager.State.Asset;
    using UnityEngine.SceneManagement;
    using UnityEngine;
    using JetBrains.Annotations;
    using TrafficManager.UI.WhatsNew;
    using System.Diagnostics.CodeAnalysis;
    using TrafficManager.UI.Helpers;
    using TrafficManager.API.Traffic.Enums;
    using System.Text;

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

        public IDisposable GeometryNotifierDisposable;

        /// <summary>Cached value of <see cref="LauncherLoginData.instance.m_continue"/>.</summary>
        private static bool cache_m_continue;

        /// <summary>TMPE is in the middle of deserializing data.</summary>
        public bool Deserializing { get; set; }

        /// <summary>
        /// Contains loaded languages and lookup functions for text translations
        /// </summary>
        public Translation TranslationDatabase { get; } = new Translation();
        public List<ICustomManager> RegisteredManagers { get; } = new List<ICustomManager>();

        public bool InGameHotReload { get; private set; }
        public bool IsGameLoaded { get; private set; }

        public WhatsNew WhatsNew { get; private set; }

        public Dictionary<BuildingInfo, AssetData> Asset2Data { get; set; }

        /// <summary>
        /// determines if simulation is inside game/editor. useful to detect hot-reload.
        /// </summary>
        public static bool InGameOrEditor() =>
            SceneManager.GetActiveScene().name != "IntroScreen" &&
            SceneManager.GetActiveScene().name != "MainMenu" &&
            SceneManager.GetActiveScene().name != "Startup";

        /// <summary>
        /// Determines if modifications to segments may be published in the current state.
        /// </summary>
        /// <returns>Returns <c>true</c> if changes may be published, otherwise <c>false</c>.</returns>
        public bool MayPublishSegmentChanges()
            => InGameOrEditor() && !Instance.Deserializing;

        public static AppMode? AppMode {
            get {
                try {
                    return SimulationManager.instance.m_ManagersWrapper.loading?.currentMode;
                }
                catch {
                    // ignore, currentMode may throw NullReferenceException on return to main menu
                }

                return null;
            }
        }

        // throws null ref if used from main menu
        public static SimulationManager.UpdateMode UpdateMode => SimulationManager.instance.m_metaData.m_updateMode;

        // throws null ref if used from main menu
        public static LoadMode Mode => (LoadMode)UpdateMode;

        public static string Scene => SceneManager.GetActiveScene().name;

        /// <summary>
        /// determines whether Game mode as oppose to edit mode (eg asset editor).
        /// </summary>
        internal static bool PlayMode {
            get {
                string sceneName = SceneManager.GetActiveScene().name;
                return sceneName.Equals("Game") || sceneName.Equals("Ingame");
            }
        }

        internal static bool IsNewGame
            => PlayMode && (Mode is LoadMode.NewGame or LoadMode.NewGameFromScenario);

        internal static bool EditorMode
            => InGameOrEditor() && !PlayMode;

        internal static bool InMapOrScenarioEditor
            => EditorMode && (AppMode is ICities.AppMode.MapEditor or ICities.AppMode.ScenarioEditor);

        /// <summary>Resumes PDX launcher auto-load of last city if necessary.</summary>
        [SuppressMessage("Type Safety", "UNT0016:Unsafe way to get the method name", Justification = "Using same code as C:SL.")]
        private static void AutoResumeLastCityIfNecessary() {
            if (!cache_m_continue) {
                return;
            }

            cache_m_continue = false;

            if (InGameOrEditor()) {
                return;
            }

            try {
                MainMenu menu = FindObjectOfType<MainMenu>();
                if (menu != null) {
                    // code from global::MainMenu.Refresh() game code
                    menu.m_BackgroundImage.zOrder = int.MaxValue;
                    menu.Invoke("AutoContinue", 2.5f);
                }
            }
            catch (Exception e) {
                Log.ErrorFormat("Resume AutoContinue Failed:\n{0}", e.ToString());
            }
        }

        private static void CompatibilityCheck() {
            bool success = true;

            ModsCompatibilityChecker mcc = new ModsCompatibilityChecker();
            success &= mcc.PerformModCheck();
            success &= VersionUtil.CheckGameVersion();

            if (success) {
                AutoResumeLastCityIfNecessary();
            }
        }

        internal void Preload() {
            SavedGameOptions.Release();
            Patcher.InstallPreload();
            Asset2Data = new Dictionary<BuildingInfo, AssetData>();
            Log.Info("Preloading Managers");
            CustomPathManager.Initialize();
            RegisteredManagers.Clear();
            RegisterCustomManagers();
            API.Implementations.Reset();
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
            RegisteredManagers.Add(ExtLaneManager.Instance);

            // Texture managers
            RegisteredManagers.Add(UI.Textures.RoadSignThemeManager.Instance);
            RegisteredManagers.Add(UI.Textures.RoadUI.Instance);
            RegisteredManagers.Add(UI.Textures.TrafficLightTextures.Instance);

            // depends on TurnOnRedManager, TrafficLightManager, TrafficLightSimulationManager
            RegisteredManagers.Add(JunctionRestrictionsManager.Instance);
        }

        [UsedImplicitly]
        void Awake() {
            try {
                Log.Info("TMPELifecycle.Awake()");
                Instance = this;

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

                // initialize what's new panel data
                WhatsNew = new WhatsNew();

                HarmonyHelper.EnsureHarmonyInstalled();
                LoadingManager.instance.m_levelPreLoaded += Preload;

                InGameHotReload = InGameOrEditor();
                if (InGameHotReload) {
                    Preload();
                    AssetDataExtension.HotReload();
                    SerializableDataExtension.Load();
                    Load();
                }

#if DEBUG
                const bool installHarmonyASAP = false; // set true for fast testing
                if (installHarmonyASAP) {
                    HarmonyHelper.DoOnHarmonyReady(Patcher.Install);
                }
#endif
            } catch (Exception ex) {
                ex.LogException(true);
            }
        }

        [UsedImplicitly]
        void OnDestroy() {
            try {
                Log.Info("TMPELifecycle.OnDestroy()");
                API.Implementations.Reset();
                LoadingManager.instance.m_introLoaded -= CompatibilityCheck;
                LocaleManager.eventLocaleChanged -= Translation.HandleGameLocaleChange;
                LoadingManager.instance.m_levelPreLoaded -= Preload;

                CustomPathManager._instance?.Unload();
                if (IsGameLoaded) {
                    //Hot Unload
                    Unload();
                }
                Patcher.Uninstall(API.Harmony.HARMONY_ID_PRELOAD);
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
            // Prevent launcher auto-resume now, because we can't do it later
            // If `CompatibilityCheck()` passes, we'll invoke `AutoResumeLastCityIfNecessary()`
            cache_m_continue = LauncherLoginData.instance.m_continue;
            LauncherLoginData.instance.m_continue = false;

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

                ModUI.OnLevelLoaded();
                if (PlayMode) {
                    Log._Debug("PlayMode");
                    UIView uiView = UIView.GetAView();
                    uiView.AddUIComponent(typeof(UITransportDemand));
                    uiView.gameObject.AddComponent<RemoveVehicleButtonExtender>();
                    uiView.gameObject.AddComponent<RemoveCitizenInstanceButtonExtender>();
                    uiView.gameObject.AddComponent<RoadSelectionPanels>();
                }

                Log.Info("Notifying managers...");
                foreach (ICustomManager manager in RegisteredManagers) {
                    Log.Info($"OnLevelLoading: {manager.GetType().Name}");
                    manager.OnLevelLoading();
                }

                // must be subscribed last to notify other mods about TMPE changes
                // after all TMPE rules are applied.
                GeometryNotifierDisposable = GeometryManager.Instance.Subscribe(new GeometryNotifier());
                Notifier.Instance.OnLevelLoaded();

                if (PlayMode && (Mode is not LoadMode.NewGame or LoadMode.NewGameFromScenario)) {

                    var despawned = PathfinderUpdates.DespawnVehiclesIfNecessary();

                    if (despawned != ExtVehicleType.None) {
                        Prompt.Info(
                            T("Popup.Title:TM:PE Pathfinder Updated"),
                            T("Popup.Message:Some vehicles had broken routes:") +
                            $"\n\n{despawned}\n\n" +
                            T("Popup.Message:We've despawned them to prevent further issues. " +
                              "New vehicles will automatically spawn to replace them."));
                    }
                }

                Log.Info("OnLevelLoaded complete.");
            } catch (Exception ex) {
                ex.LogException(true);
            }
        }

        private static string T(string key) => Translation.Options.Get(key);

        internal void Unload() {
            try {
                SavedGameOptions.Release();

                GeometryNotifierDisposable?.Dispose();
                GeometryNotifierDisposable = null;

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

                if (PlayMode) {
                    // destroy immediately to prevent duplicates after hot-reload.
                    var uiviewGO = UIView.GetAView().gameObject;
                    DestroyImmediate(uiviewGO.GetComponent<RoadSelectionPanels>());
                    DestroyImmediate(uiviewGO.GetComponent<RemoveVehicleButtonExtender>());
                    DestroyImmediate(uiviewGO.GetComponent<RemoveCitizenInstanceButtonExtender>());
                    DestroyImmediate(uiviewGO.GetComponent<UITransportDemand>());
                }

                Log.Info("Removing Controls from UI.");
                if (ModUI.Instance) {
                    ModUI.Instance.CloseMainMenu(); // Hide the UI ASAP
                    ModUI.Instance.Destroy();
                    Log._Debug("removed UIBase instance.");
                }
            } catch (Exception ex) {
                ex.LogException(true);
            }

            Patcher.Uninstall(API.Harmony.HARMONY_ID);

            IsGameLoaded = false;
            InGameHotReload = false;
        }
    }
}
