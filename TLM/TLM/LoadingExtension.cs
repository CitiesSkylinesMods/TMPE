namespace TrafficManager {
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using TrafficManager.API.Manager;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.UI;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using Util;
    using Object = UnityEngine.Object;
    using System.Linq;

    [UsedImplicitly]
    public class LoadingExtension : LoadingExtensionBase {
        static LoadingExtension() {
            TranslationDatabase.LoadAllTranslations();
            RegisterCustomManagers();
        }

        internal static AppMode? AppMode => SimulationManager.instance.m_ManagersWrapper.loading.currentMode;

        public static SimulationManager.UpdateMode UpdateMode => SimulationManager.instance.m_metaData.m_updateMode;
        public static LoadMode Mode => (LoadMode)UpdateMode;
        public static string Scene => SceneManager.GetActiveScene().name;

        /// <summary>
        /// determines whether Game mode as oppose to edit mode (eg asset editor).
        /// </summary>
        internal static bool PlayMode => AppMode != null && AppMode == ICities.AppMode.Game;

        /// <summary>
        /// Contains loaded languages and lookup functions for text translations
        /// </summary>
        public static Translation TranslationDatabase = new Translation();

        public static List<ICustomManager> RegisteredManagers { get; private set; }

        public static bool IsGameLoaded { get; private set; }

        private static void RegisterCustomManagers() {
            RegisteredManagers = new List<ICustomManager>();

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

        public override void OnLevelUnloading() => Unload();

        public override void OnLevelLoaded(LoadMode mode) => Load();

        public static void Unload() {
            Log.Info("LoadingExtension.Unload()");

            CustomPathManager._instance?.OnLevelUnloading();

            try {
                foreach (ICustomManager manager in RegisteredManagers.AsEnumerable().Reverse()) {
                    Log.Info($"OnLevelUnloading: {manager.GetType().Name}");
                    manager.OnLevelUnloading();
                }

                Flags.OnLevelUnloading();
                GlobalConfig.OnLevelUnloading();

                var uiviewGO = UIView.GetAView().gameObject;
                Object.Destroy(uiviewGO.GetComponent<RoadSelectionPanels>());
                Object.Destroy(uiviewGO.GetComponent<RemoveVehicleButtonExtender>());
                Object.Destroy(uiviewGO.GetComponent<RemoveCitizenInstanceButtonExtender>());
                Object.Destroy(uiviewGO.GetComponent<RemoveCitizenInstanceButtonExtender>());
                Object.Destroy(uiviewGO.GetComponent<UITransportDemand>());

                Log.Info("Removing Controls from UI.");
                if (ModUI.Instance != null) {
                    ModUI.Instance.CloseMainMenu(); // Hide the UI ASAP
                    ModUI.Instance.Destroy();
                    Log._Debug("removed UIBase instance.");
                }
            }
            catch (Exception e) {
                Log.Error("Exception unloading mod. " + e.Message);

                // ignored - prevents collision with other mods
            }

            Patcher.Uninstall();
            IsGameLoaded = false;
            TrafficManagerMod.InGameHotReload = false;
        }

        public static void Load() {
            Log.Info($"LoadingExtension.Load() called. {Mode} called. updateMode={UpdateMode}, scene={Scene}");

            if(Scene == "ThemeEditor")
                return;

            InGameUtil.Instantiate();

            RegisterCustomManagers();

            IsGameLoaded = false;

            if (BuildConfig.applicationVersion != BuildConfig.VersionToString(
                    TrafficManagerMod.GAME_VERSION,
                    false)) {
                string[] majorVersionElms = BuildConfig.applicationVersion.Split('-');
                string[] versionElms = majorVersionElms[0].Split('.');
                uint versionA = Convert.ToUInt32(versionElms[0]);
                uint versionB = Convert.ToUInt32(versionElms[1]);
                uint versionC = Convert.ToUInt32(versionElms[2]);

                Log.Info($"Detected game version v{BuildConfig.applicationVersion}");

                bool isModTooOld = TrafficManagerMod.GAME_VERSION_A < versionA ||
                                   (TrafficManagerMod.GAME_VERSION_A == versionA &&
                                    TrafficManagerMod.GAME_VERSION_B < versionB);
                // || (TrafficManagerMod.GameVersionA == versionA
                // && TrafficManagerMod.GameVersionB == versionB
                // && TrafficManagerMod.GameVersionC < versionC);

                bool isModNewer = TrafficManagerMod.GAME_VERSION_A < versionA ||
                                  (TrafficManagerMod.GAME_VERSION_A == versionA &&
                                   TrafficManagerMod.GAME_VERSION_B > versionB);
                // || (TrafficManagerMod.GameVersionA == versionA
                // && TrafficManagerMod.GameVersionB == versionB
                // && TrafficManagerMod.GameVersionC > versionC);

                if (isModTooOld) {
                    string msg = string.Format(
                        "Traffic Manager: President Edition detected that you are running " +
                        "a newer game version ({0}) than TM:PE has been built for ({1}). " +
                        "Please be aware that TM:PE has not been updated for the newest game " +
                        "version yet and thus it is very likely it will not work as expected.",
                        BuildConfig.applicationVersion,
                        BuildConfig.VersionToString(TrafficManagerMod.GAME_VERSION, false));

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
                        BuildConfig.VersionToString(TrafficManagerMod.GAME_VERSION, false),
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

        [UsedImplicitly]
        private bool Check3rdPartyModLoaded(string namespaceStr, bool printAll = false) {
            bool thirdPartyModLoaded = false;

            FieldInfo loadingWrapperLoadingExtensionsField = typeof(LoadingWrapper).GetField(
                "m_LoadingExtensions",
                BindingFlags.NonPublic | BindingFlags.Instance);
            List<ILoadingExtension> loadingExtensions = null;

            if (loadingWrapperLoadingExtensionsField != null) {
                loadingExtensions =
                    (List<ILoadingExtension>)loadingWrapperLoadingExtensionsField.GetValue(
                        Singleton<LoadingManager>.instance.m_LoadingWrapper);
            } else {
                Log.Warning("Could not get loading extensions field");
            }

            if (loadingExtensions != null) {
                foreach (ILoadingExtension extension in loadingExtensions) {
                    if (printAll) {
                        Log.Info($"Detected extension: {extension.GetType().Name} in " +
                                 $"namespace {extension.GetType().Namespace}");
                    }

                    if (extension.GetType().Namespace == null) {
                        continue;
                    }

                    string nsStr = extension.GetType().Namespace;

                    if (namespaceStr.Equals(nsStr)) {
                        Log.Info($"The mod '{namespaceStr}' has been detected.");
                        thirdPartyModLoaded = true;
                        break;
                    }
                }
            } else {
                Log._Debug("Could not get loading extensions");
            }

            return thirdPartyModLoaded;
        }
    }
}