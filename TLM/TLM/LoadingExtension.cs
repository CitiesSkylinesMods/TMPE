namespace TrafficManager {
    using ColossalFramework.UI;
    using ColossalFramework;
    using CSUtil.Commons;
    using Harmony;
    using ICities;
    using JetBrains.Annotations;
    using Object = UnityEngine.Object;
    using System.Collections.Generic;
    using System.Reflection;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.Manager.Impl;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State;
    using TrafficManager.UI.Localization;
    using TrafficManager.UI;
    using static TrafficManager.Util.Shortcuts;
    using UnityEngine;

    [UsedImplicitly]
    public class LoadingExtension : LoadingExtensionBase {
        private const string HARMONY_ID = "de.viathinksoft.tmpe";
        internal static LoadingExtension Instance = null;

        internal bool InGameHotReload { get; private set; } = false;

        internal static AppMode currentMode => SimulationManager.instance.m_ManagersWrapper.loading.currentMode;

        internal static bool InGame() {
            try {
                return currentMode == AppMode.Game;
            } catch {
                return false;
            }
        }

        FastList<ISimulationManager> simManager =>
            typeof(SimulationManager).GetField("m_managers", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) as FastList<ISimulationManager>;


        public class Detour {
            public MethodInfo OriginalMethod;
            public MethodInfo CustomMethod;
            public RedirectCallsState Redirect;

            public Detour(MethodInfo originalMethod, MethodInfo customMethod) {
                OriginalMethod = originalMethod;
                CustomMethod = customMethod;
                Redirect = RedirectionHelper.RedirectCalls(originalMethod, customMethod);
            }
        }

        public class ManualHarmonyPatch {
            public MethodInfo method;
            public HarmonyMethod prefix;
            public HarmonyMethod transpiler = null;
            public HarmonyMethod postfix = null;
        }

        public static CustomPathManager CustomPathManager { get; set; }

        public static bool DetourInited { get; set; }

        public static List<Detour> Detours { get; set; }

        public static HarmonyInstance HarmonyInst { get; private set; }

        /// <summary>
        /// Contains loaded languages and lookup functions for text translations
        /// </summary>
        public static Translation TranslationDatabase = new Translation();

        public static UIBase BaseUI { get; private set; }

        public static UITransportDemand TransportDemandUI { get; private set; }

        public static List<ICustomManager> RegisteredManagers { get; private set; }

        public static bool IsGameLoaded { get; private set; }

        /// <summary>
        /// Manually deployed Harmony patches
        /// </summary>
        public static IList<ManualHarmonyPatch> ManualHarmonyPatches { get; } =
            new List<ManualHarmonyPatch> {
                new ManualHarmonyPatch() {
                    method = typeof(CommonBuildingAI).GetMethod(
                        "SimulationStep",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new[] { typeof(ushort), typeof(Building).MakeByRefType() },
                        null),
                    prefix = new HarmonyMethod(
                        typeof(Patch._CommonBuildingAI.SimulationStepPatch).GetMethod("Prefix"))
                },
                new ManualHarmonyPatch() {
                    method = typeof(RoadBaseAI).GetMethod(
                        "TrafficLightSimulationStep",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(ushort), typeof(NetNode).MakeByRefType() },
                        null),
                    prefix = new HarmonyMethod(
                        typeof(Patch._RoadBaseAI.TrafficLightSimulationStepPatch).GetMethod(
                            "Prefix"))
                },
                new ManualHarmonyPatch() {
                    method = typeof(TrainTrackBaseAI).GetMethod(
                        "LevelCrossingSimulationStep",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(ushort), typeof(NetNode).MakeByRefType() },
                        null),
                    prefix = new HarmonyMethod(
                        typeof(Patch._TrainTrackBase.LevelCrossingSimulationStepPatch).GetMethod(
                            "Prefix"))
                },
                new ManualHarmonyPatch() {
                    method = typeof(RoadBaseAI).GetMethod(
                        "SimulationStep",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new[] { typeof(ushort), typeof(NetSegment).MakeByRefType() },
                        null),
                    prefix = new HarmonyMethod(
                        typeof(Patch._RoadBaseAI.SegmentSimulationStepPatch).GetMethod("Prefix"))
                }
            };

        /// <summary>
        /// Method redirection states for Harmony-driven patches
        /// </summary>
        public static IDictionary<MethodBase, RedirectCallsState> HarmonyMethodStates {
            get;
        } = new Dictionary<MethodBase, RedirectCallsState>();

        /// <summary>
        /// Method redirection states for attribute-driven detours
        /// </summary>
        public static IDictionary<MethodInfo, RedirectCallsState> DetouredMethodStates {
            get;
            private set;
        } = new Dictionary<MethodInfo, RedirectCallsState>();

        static LoadingExtension() {
            TranslationDatabase.LoadAllTranslations();
        }

        public LoadingExtension() {
        }

        public void RevertDetours() {
            if (!DetourInited) {
                return;
            }

            Log.Info("Reverting manual detours");
            Detours.Reverse();
            foreach (Detour d in Detours) {
                RedirectionHelper.RevertRedirect(d.OriginalMethod, d.Redirect);
            }

            Detours.Clear();

            Log.Info("Reverting attribute-driven detours");
            AssemblyRedirector.Revert();

            Log.Info("Reverting Harmony detours");
            foreach (MethodBase m in HarmonyMethodStates.Keys) {
                HarmonyInst.Unpatch(m, HarmonyPatchType.All, HARMONY_ID);
            }

            DetourInited = false;
            Log.Info("Reverting detours finished.");
        }

        private void InitDetours() {
            // TODO realize detouring with annotations
            if (DetourInited) {
                return;
            }

            Log.Info("Init detours");
            bool detourFailed = false;

            try {
                Log.Info("Deploying Harmony patches");
#if DEBUG
                HarmonyInstance.DEBUG = true;
#endif
                Assembly assembly = Assembly.GetExecutingAssembly();

                HarmonyMethodStates.Clear();

                // Harmony attribute-driven patching
                Log.Info($"Performing Harmony attribute-driven patching");
                HarmonyInst = HarmonyInstance.Create(HARMONY_ID);
                HarmonyInst.PatchAll(assembly);

                foreach (Type type in assembly.GetTypes()) {
                    object[] attributes = type.GetCustomAttributes(typeof(HarmonyPatch), true);
                    if (attributes.Length <= 0) {
                        continue;
                    }

                    foreach (object attr in attributes) {
                        HarmonyPatch harmonyPatchAttr = (HarmonyPatch)attr;
                        MethodBase info = HarmonyUtil.GetOriginalMethod(harmonyPatchAttr.info);
                        IntPtr ptr = info.MethodHandle.GetFunctionPointer();
                        RedirectCallsState state = RedirectionHelper.GetState(ptr);
                        HarmonyMethodStates[info] = state;
                    }
                }

                // Harmony manual patching
                Log.Info($"Performing Harmony manual patching");

                foreach (ManualHarmonyPatch manualPatch in ManualHarmonyPatches) {
                    Log.InfoFormat(
                        "Manually patching method {0}.{1}. Prefix: {2}, Postfix: {3}, Transpiler: {4}",
                        manualPatch.method.DeclaringType.FullName,
                        manualPatch.method.Name, manualPatch.prefix?.method,
                        manualPatch.postfix?.method, manualPatch.transpiler?.method);

                    HarmonyInst.Patch(
                        manualPatch.method,
                        manualPatch.prefix,
                        manualPatch.postfix,
                        manualPatch.transpiler);

                    IntPtr ptr = manualPatch.method.MethodHandle.GetFunctionPointer();
                    RedirectCallsState state = RedirectionHelper.GetState(ptr);
                    HarmonyMethodStates[manualPatch.method] = state;
                }
            } catch (Exception e) {
                Log.Error("Could not deploy Harmony patches");
                Log.Info(e.ToString());
                Log.Info(e.StackTrace);
                detourFailed = true;
            }

            try {
                Log.Info("Deploying attribute-driven detours");
                DetouredMethodStates = AssemblyRedirector.Deploy();
            } catch (Exception e) {
                Log.Error("Could not deploy attribute-driven detours");
                Log.Info(e.ToString());
                Log.Info(e.StackTrace);
                detourFailed = true;
            }

            if (detourFailed) {
                Log.Info("Detours failed");
                Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(
                    () => {
                        UIView.library
                              .ShowModal<ExceptionPanel>("ExceptionPanel")
                              .SetMessage(
                                "TM:PE failed to load",
                                "Traffic Manager: President Edition failed to load. You can " +
                                "continue playing but it's NOT recommended. Traffic Manager will " +
                                "not work as expected.",
                                true);
                    });
            } else {
                Log.Info("Detours successful");
            }

            DetourInited = true;
        }

        public override void OnCreated(ILoading loading) {
            Log._Debug("LoadingExtension.OnCreated() called");

            // SelfDestruct.DestructOldInstances(this);
            base.OnCreated(loading);

            Detours = new List<Detour>();
            RegisteredManagers = new List<ICustomManager>();
            DetourInited = false;
            CustomPathManager = new CustomPathManager();

            RegisterCustomManagers();

            Instance = this;
            InGameHotReload = InGame();
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
            Instance = null;
            base.OnReleased();
        }

        public override void OnLevelUnloading() {
            Log.Info("OnLevelUnloading");
            base.OnLevelUnloading();

            CustomPathManager._instance.WaitForAllPaths();

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

                // remove vehicle button
                Destroy<RemoveVehicleButtonExtender>();
                Destroy<RemoveCitizenInstanceButtonExtender>();

                // Custom path manger is destroyed when reloading. That is why the following code
                // is commented out.
                //simManager?.Remove(CustomPathManager);
                //Object.Destroy(CustomPathManager);
                //CustomPathManager = null;

                if (TransportDemandUI != null) {
                    UIView uiView = UIView.GetAView();
                    Object.Destroy(TransportDemandUI);
                    TransportDemandUI = null;
                }

                Log.Info("Removing Controls from UI.");
                if (BaseUI != null) {
                    BaseUI.Close(); // Hide the UI ASAP
                    Object.Destroy(BaseUI);
                    BaseUI = null;
                    Log._Debug("removed UIBase instance.");
                }

#if TRACE
                Singleton<CodeProfiler>.instance.OnLevelUnloading();
#endif
            }
            catch (Exception e) {
                Log.Error("Exception unloading mod. " + e.Message);

                // ignored - prevents collision with other mods
            }

            RevertDetours();
            IsGameLoaded = false;
        }

        public override void OnLevelLoaded(LoadMode mode) {
            SimulationManager.UpdateMode updateMode = SimulationManager.instance.m_metaData.m_updateMode;
            Log.Info($"OnLevelLoaded({mode}) called. updateMode={updateMode}");
            base.OnLevelLoaded(mode);

            Log._Debug("OnLevelLoaded Returned from base, calling custom code.");

            IsGameLoaded = false;

            switch (updateMode) {
                case SimulationManager.UpdateMode.NewGameFromMap:
                case SimulationManager.UpdateMode.NewGameFromScenario:
                case SimulationManager.UpdateMode.LoadGame: {
                    if (BuildConfig.applicationVersion != BuildConfig.VersionToString(
                            TrafficManagerMod.GAME_VERSION,
                            false))
                    {
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
                    break;
                }

                default: {
                    Log.Info($"OnLevelLoaded: Unsupported game mode {mode}");
                    return;
                }
            }

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

                CustomPathManager = stockPathManager.gameObject.AddComponent<CustomPathManager>();
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

            }
            catch (Exception ex) {
                string error = "Traffic Manager: President Edition failed to load. You can continue " +
                               "playing but it's NOT recommended. Traffic Manager will not work as expected.";
                Log.Error(error);
                Log.Error($"Path manager replacement error: {ex}");

                Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(
                    () => {
                        UIView.library
                              .ShowModal<ExceptionPanel>("ExceptionPanel")
                              .SetMessage(
                            "TM:PE failed to load",
                            error,
                            true);
                    });
            }

            Log.Info("Adding Controls to UI.");
            if (BaseUI == null) {
                Log._Debug("Adding UIBase instance.");
                BaseUI = ToolsModifierControl.toolController.gameObject.AddComponent<UIBase>();
            }

            // Init transport demand UI
            if (TransportDemandUI == null) {
                UIView uiView = UIView.GetAView();
                TransportDemandUI = (UITransportDemand)uiView.AddUIComponent(typeof(UITransportDemand));
            }

            // add "remove vehicle" button
            UIView.GetAView().gameObject.AddComponent<RemoveVehicleButtonExtender>();

            // add "remove citizen instance" button
            UIView.GetAView().gameObject.AddComponent<RemoveCitizenInstanceButtonExtender>();

            InitDetours();

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