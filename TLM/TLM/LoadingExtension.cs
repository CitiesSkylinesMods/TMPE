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
    using Object = UnityEngine.Object;

    [UsedImplicitly]
    public class LoadingExtension : LoadingExtensionBase {
        static LoadingExtension() {
            TranslationDatabase.LoadAllTranslations();
        }

        public LoadingExtension() {
        }

        internal static LoadingExtension Instance = null;

        FastList<ISimulationManager> simManager =>
            typeof(SimulationManager).GetField("m_managers", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) as FastList<ISimulationManager>;

        /// <summary>
        /// determines whether Game mode as oppose to edit mode (eg asset editor).
        /// </summary>
        internal static bool PlayMode => Instance.loadingManager.currentMode == AppMode.Game;

        public static CustomPathManager CustomPathManager { get; set; }

        /// <summary>
        /// Contains loaded languages and lookup functions for text translations
        /// </summary>
        public static Translation TranslationDatabase = new Translation();

        public static UITransportDemand TransportDemandUI { get; private set; }

        public static List<ICustomManager> RegisteredManagers { get; private set; }

        public static bool IsGameLoaded { get; private set; }

        public static bool IsPathManagerReplaced {
            get; private set;
        }

        public override void OnCreated(ILoading loading) {
            Log._Debug("LoadingExtension.OnCreated() called");

            // SelfDestruct.DestructOldInstances(this);
            base.OnCreated(loading);
            if (IsGameLoaded) {
                // When another mod is detected, OnCreated is called again for god - or CS team - knows what reason!
                Log._Debug("Hot reload of another mod detected. Skipping LoadingExtension.OnCreated() ...");
                return;
            }

            RegisteredManagers = new List<ICustomManager>();
            CustomPathManager = new CustomPathManager();

            RegisterCustomManagers();

            Instance = this;
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

        public override void OnReleased() {
            TrafficManagerMod.Instance.InGameHotReload = false;
            Instance = null;
            base.OnReleased();
        }

        public override void OnLevelUnloading() {
            Log.Info("OnLevelUnloading");
            base.OnLevelUnloading();
            if (IsPathManagerReplaced) {
                CustomPathManager._instance.WaitForAllPaths();
            }

            try {
                var reverseManagers = new List<ICustomManager>(RegisteredManagers);
                reverseManagers.Reverse();

                foreach (ICustomManager manager in reverseManagers) {
                    Log.Info($"OnLevelUnloading: {manager.GetType().Name}");
                    manager.OnLevelUnloading();
                }

                Flags.OnLevelUnloading();
                GlobalConfig.OnLevelUnloading();

                var gameObject = UIView.GetAView().gameObject;

                void Destroy<T>() where T : MonoBehaviour {
                    Object obj = (Object)gameObject.GetComponent<T>();
                    if (obj != null) {
                        Object.Destroy(obj);
                    }
                }

                Destroy<RoadSelectionPanels>();
                Destroy<RemoveVehicleButtonExtender>();
                Destroy<RemoveCitizenInstanceButtonExtender>();

                //It's MonoBehaviour - comparing to null is wrong
                if (TransportDemandUI) {
                    Object.Destroy(TransportDemandUI);
                    TransportDemandUI = null;
                }

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

            Patcher.Instance?.Uninstall();
            IsGameLoaded = false;
        }

        public override void OnLevelLoaded(LoadMode mode) {
            SimulationManager.UpdateMode updateMode = SimulationManager.instance.m_metaData.m_updateMode;
            Log.Info($"OnLevelLoaded({mode}) called. updateMode={updateMode}");
            base.OnLevelLoaded(mode);

            Log._Debug("OnLevelLoaded Returned from base, calling custom code.");

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

            //it will replace stock PathManager or already Replaced before HotReload
            if (!IsPathManagerReplaced || TrafficManagerMod.Instance.InGameHotReload) {
                try {
                    Log.Info("Pathfinder Compatible. Setting up CustomPathManager and SimManager.");
                    FieldInfo pathManagerInstance = typeof(Singleton<PathManager>).GetField(
                        "sInstance",
                        BindingFlags.Static | BindingFlags.NonPublic);
                    if (pathManagerInstance == null) {
                        throw new Exception("pathManagerInstance is null");
                    }


                    PathManager stockPathManager = PathManager.instance;
                    if (stockPathManager == null) {
                        throw new Exception("stockPathManager is null");
                    }

                    Log._Debug($"Got stock PathManager instance {stockPathManager?.GetName()}");

                    CustomPathManager =
                        stockPathManager.gameObject.AddComponent<CustomPathManager>();
                    Log._Debug("Added CustomPathManager to gameObject List");

                    if (CustomPathManager == null) {
                        Log.Error("CustomPathManager null. Error creating it.");
                        return;
                    }

                    CustomPathManager.UpdateWithPathManagerValues(stockPathManager);
                    Log._Debug("UpdateWithPathManagerValues success");

                    pathManagerInstance.SetValue(null, CustomPathManager);

                    Log._Debug("Getting Current SimulationManager");
                    var simManager = this.simManager;
                    if (simManager == null) {
                        throw new Exception("simManager is null");
                    }

                    Log._Debug("Removing Stock PathManager");
                    simManager.Remove(stockPathManager);

                    Log._Debug("Adding Custom PathManager");
                    simManager.Add(CustomPathManager);

                    Object.Destroy(stockPathManager, 10f);

                    Log._Debug("Should be custom: " + Singleton<PathManager>.instance.GetType());

                    IsPathManagerReplaced = true;
                }
                catch (Exception ex) {
                    string error =
                        "Traffic Manager: President Edition failed to load. You can continue " +
                        "playing but it's NOT recommended. Traffic Manager will not work as expected.";
                    Log.Error(error);
                    Log.Error($"Path manager replacement error: {ex}");

                    Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(
                        () => {
                            UIView.library
                                  .ShowModal<ExceptionPanel>(
                                      "ExceptionPanel")
                                  .SetMessage(
                                      "TM:PE failed to load",
                                      error,
                                      true);
                        });
                }
            }

            ModUI.OnLevelLoaded();
            if (PlayMode) {
                // Init transport demand UI
                if (TransportDemandUI == null) {
                    UIView uiView = UIView.GetAView();
                    TransportDemandUI = (UITransportDemand)uiView.AddUIComponent(typeof(UITransportDemand));
                }

                // add "remove vehicle" button
                UIView.GetAView().gameObject.AddComponent<RemoveVehicleButtonExtender>();

                // add "remove citizen instance" button
                UIView.GetAView().gameObject.AddComponent<RemoveCitizenInstanceButtonExtender>();

                UIView.GetAView().gameObject.AddComponent<RoadSelectionPanels>();
            }

            Patcher.Create().Install();

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