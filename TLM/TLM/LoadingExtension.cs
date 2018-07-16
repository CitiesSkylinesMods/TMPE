using System;
using System.Reflection;
using ColossalFramework;
using ICities;
using TrafficManager.Custom.AI;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using TrafficManager.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using TrafficManager.State;
using ColossalFramework.UI;
using ColossalFramework.Math;
using TrafficManager.Custom.PathFinding;
using TrafficManager.Util;
using TrafficManager.Manager;
using CSUtil.Commons;
using TrafficManager.Custom.Data;
using TrafficManager.Manager.Impl;
using Harmony;
using TrafficManager.RedirectionFramework;

namespace TrafficManager {
	public class LoadingExtension : LoadingExtensionBase {
		private const string HARMONY_ID = "de.viathinksoft.tmpe";

		public class Detour {
			public MethodInfo OriginalMethod;
			public MethodInfo CustomMethod;
			public RedirectCallsState Redirect;

			public Detour(MethodInfo originalMethod, MethodInfo customMethod) {
				this.OriginalMethod = originalMethod;
				this.CustomMethod = customMethod;
				this.Redirect = RedirectionHelper.RedirectCalls(originalMethod, customMethod);
			}
		}

		public class ManualHarmonyPatch {
			public MethodInfo method = null;
			public HarmonyMethod prefix = null;
			public HarmonyMethod transpiler = null;
			public HarmonyMethod postfix = null;
		}

		//public static LoadingExtension Instance;

		public static bool IsPathManagerReplaced {
			get; private set;
		} = false;

		public static CustomPathManager CustomPathManager { get; set; }
		public static bool DetourInited { get; set; }
		public static List<Detour> Detours { get; set; }
		public static HarmonyInstance HarmonyInst { get; private set; }
		//public static TrafficManagerMode ToolMode { get; set; }
		//public static TrafficManagerTool TrafficManagerTool { get; set; }
#if !TAM
		public static UIBase BaseUI { get; private set; }
#endif

		public static UITransportDemand TransportDemandUI { get; private set; }

		public static List<ICustomManager> RegisteredManagers { get; private set; }

		public static bool IsGameLoaded { get; private set; } = false;

		/// <summary>
		/// Manually deployed Harmony patches
		/// </summary>
		public static IList<ManualHarmonyPatch> ManualHarmonyPatches { get; } = new List<ManualHarmonyPatch> {
			new ManualHarmonyPatch() {
				method = typeof(CommonBuildingAI).GetMethod("SimulationStep", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(ushort), typeof(Building).MakeByRefType() }, null),
				prefix = new HarmonyMethod(typeof(Patch._CommonBuildingAI.SimulationStepPatch).GetMethod("Prefix"))
			},
			new ManualHarmonyPatch() {
				method = typeof(RoadBaseAI).GetMethod("TrafficLightSimulationStep", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ushort), typeof(NetNode).MakeByRefType() }, null),
				prefix = new HarmonyMethod(typeof(Patch._RoadBaseAI.TrafficLightSimulationStepPatch).GetMethod("Prefix"))
			},
			new ManualHarmonyPatch() {
				method = typeof(TrainTrackBaseAI).GetMethod("LevelCrossingSimulationStep", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ushort), typeof(NetNode).MakeByRefType() }, null),
				prefix = new HarmonyMethod(typeof(Patch._TrainTrackBase.LevelCrossingSimulationStepPatch).GetMethod("Prefix"))
			},
			new ManualHarmonyPatch() {
				method = typeof(RoadBaseAI).GetMethod("SimulationStep", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(ushort), typeof(NetSegment).MakeByRefType() }, null),
				prefix = new HarmonyMethod(typeof(Patch._RoadBaseAI.SegmentSimulationStepPatch).GetMethod("Prefix"))
			}
		};

		/// <summary>
		/// Method redirection states for Harmony-driven patches
		/// </summary>
		public static IDictionary<MethodBase, RedirectCallsState> HarmonyMethodStates { get; private set; } = new Dictionary<MethodBase, RedirectCallsState>();

		/// <summary>
		/// Method redirection states for attribute-driven detours
		/// </summary>
		public static IDictionary<MethodInfo, RedirectCallsState> DetouredMethodStates { get; private set; } = new Dictionary<MethodInfo, RedirectCallsState>();

		static LoadingExtension() {
			
		}

		public LoadingExtension() {
		}

		public void revertDetours() {
			if (DetourInited) {
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
					HarmonyInst.RemovePatch(m, HarmonyPatchType.All, HARMONY_ID);
				}

				DetourInited = false;
				Log.Info("Reverting detours finished.");
			}
		}

		public void initDetours() {
			// TODO realize detouring with annotations
			if (!DetourInited) {
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
						Log.Info($"Manually patching method {manualPatch.method.DeclaringType.FullName}.{manualPatch.method.Name}. Prefix: {manualPatch.prefix?.method}, Postfix: {manualPatch.postfix?.method}, Transpiler: {manualPatch.transpiler?.method}");
						HarmonyInst.Patch(manualPatch.method, manualPatch.prefix, manualPatch.postfix, manualPatch.transpiler);

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
					Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(() => {
						UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("TM:PE failed to load", "Traffic Manager: President Edition failed to load. You can continue playing but it's NOT recommended. Traffic Manager will not work as expected.", true);
					});
				} else {
					Log.Info("Detours successful");
				}

				DetourInited = true;
			}
		}

		public override void OnCreated(ILoading loading) {
			//SelfDestruct.DestructOldInstances(this);

			base.OnCreated(loading);

			Detours = new List<Detour>();
			RegisteredManagers = new List<ICustomManager>();
			DetourInited = false;
			CustomPathManager = new CustomPathManager();

			RegisterCustomManagers();
		}

		private void RegisterCustomManagers() {
			RegisteredManagers.Add(AdvancedParkingManager.Instance);
			RegisteredManagers.Add(CustomSegmentLightsManager.Instance);
			RegisteredManagers.Add(ExtBuildingManager.Instance);
			RegisteredManagers.Add(ExtCitizenInstanceManager.Instance);
			RegisteredManagers.Add(ExtCitizenManager.Instance);
			RegisteredManagers.Add(ExtPathManager.Instance);
			RegisteredManagers.Add(JunctionRestrictionsManager.Instance);
			RegisteredManagers.Add(LaneArrowManager.Instance);
			RegisteredManagers.Add(LaneConnectionManager.Instance);
			RegisteredManagers.Add(OptionsManager.Instance);
			RegisteredManagers.Add(ParkingRestrictionsManager.Instance);
			RegisteredManagers.Add(RoutingManager.Instance);
			RegisteredManagers.Add(SegmentEndManager.Instance);
			RegisteredManagers.Add(SpeedLimitManager.Instance);
			RegisteredManagers.Add(TrafficLightManager.Instance);
			RegisteredManagers.Add(TrafficLightSimulationManager.Instance);
			RegisteredManagers.Add(TrafficMeasurementManager.Instance);
			RegisteredManagers.Add(TrafficPriorityManager.Instance);
			RegisteredManagers.Add(UtilityManager.Instance);
			RegisteredManagers.Add(VehicleRestrictionsManager.Instance);
			RegisteredManagers.Add(ExtVehicleManager.Instance);
		}

		public override void OnReleased() {
			base.OnReleased();

			UIBase.ReleaseTool();
		}

		public override void OnLevelUnloading() {
			Log.Info("OnLevelUnloading");
			base.OnLevelUnloading();
			if (IsPathManagerReplaced) {
				CustomPathManager._instance.WaitForAllPaths();
			}

			/*Object.Destroy(BaseUI);
			BaseUI = null;
			Object.Destroy(TransportDemandUI);
			TransportDemandUI = null;*/

			try {
				foreach (ICustomManager manager in RegisteredManagers) {
					Log.Info($"OnLevelUnloading: {manager.GetType().Name}");
					manager.OnLevelUnloading();
				}
				Flags.OnLevelUnloading();
				Translation.OnLevelUnloading();
				GlobalConfig.OnLevelUnloading();

				// remove vehicle button
				var removeVehicleButtonExtender = UIView.GetAView().gameObject.GetComponent<RemoveVehicleButtonExtender>();
				if (removeVehicleButtonExtender != null) {
					Object.Destroy(removeVehicleButtonExtender, 10f);
				}

				// remove citizen instance button
				var removeCitizenInstanceButtonExtender = UIView.GetAView().gameObject.GetComponent<RemoveCitizenInstanceButtonExtender>();
				if (removeCitizenInstanceButtonExtender != null) {
					Object.Destroy(removeCitizenInstanceButtonExtender, 10f);
				}
#if TRACE
				Singleton<CodeProfiler>.instance.OnLevelUnloading();
#endif
			} catch (Exception e) {
				Log.Error("Exception unloading mod. " + e.Message);
				// ignored - prevents collision with other mods
			}

			revertDetours();
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
				case SimulationManager.UpdateMode.LoadGame:
					if (BuildConfig.applicationVersion != BuildConfig.VersionToString(TrafficManagerMod.GameVersion, false)) {
						string[] majorVersionElms = BuildConfig.applicationVersion.Split('-');
						string[] versionElms = majorVersionElms[0].Split('.');
						uint versionA = Convert.ToUInt32(versionElms[0]);
						uint versionB = Convert.ToUInt32(versionElms[1]);
						uint versionC = Convert.ToUInt32(versionElms[2]);

						Log.Info($"Detected game version v{BuildConfig.applicationVersion}");

						bool isModTooOld = TrafficManagerMod.GameVersionA < versionA ||
							(TrafficManagerMod.GameVersionA == versionA && TrafficManagerMod.GameVersionB < versionB)/* ||
							(TrafficManagerMod.GameVersionA == versionA && TrafficManagerMod.GameVersionB == versionB && TrafficManagerMod.GameVersionC < versionC)*/;

						bool isModNewer = TrafficManagerMod.GameVersionA < versionA ||
							(TrafficManagerMod.GameVersionA == versionA && TrafficManagerMod.GameVersionB > versionB)/* ||
							(TrafficManagerMod.GameVersionA == versionA && TrafficManagerMod.GameVersionB == versionB && TrafficManagerMod.GameVersionC > versionC)*/;

						if (isModTooOld) {
							string msg = $"Traffic Manager: President Edition detected that you are running a newer game version ({BuildConfig.applicationVersion}) than TM:PE has been built for ({BuildConfig.VersionToString(TrafficManagerMod.GameVersion, false)}). Please be aware that TM:PE has not been updated for the newest game version yet and thus it is very likely it will not work as expected.";
							Log.Error(msg);
							Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(() => {
								UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("TM:PE has not been updated yet", msg, false);
							});
						} else if (isModNewer) {
							string msg = $"Traffic Manager: President Edition has been built for game version {BuildConfig.VersionToString(TrafficManagerMod.GameVersion, false)}. You are running game version {BuildConfig.applicationVersion}. Some features of TM:PE will not work with older game versions. Please let Steam update your game.";
							Log.Error(msg);
							Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(() => {
								UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Your game should be updated", msg, false);
							});
						}
					}
					IsGameLoaded = true;
					break;
				default:
					Log.Info($"OnLevelLoaded: Unsupported game mode {mode}");
					return;
			}

			if (!IsPathManagerReplaced) {
				try {
					Log.Info("Pathfinder Compatible. Setting up CustomPathManager and SimManager.");
					var pathManagerInstance = typeof(Singleton<PathManager>).GetField("sInstance", BindingFlags.Static | BindingFlags.NonPublic);

					var stockPathManager = PathManager.instance;
					Log._Debug($"Got stock PathManager instance {stockPathManager.GetName()}");

					CustomPathManager = stockPathManager.gameObject.AddComponent<CustomPathManager>();
					Log._Debug("Added CustomPathManager to gameObject List");

					if (CustomPathManager == null) {
						Log.Error("CustomPathManager null. Error creating it.");
						return;
					}

					CustomPathManager.UpdateWithPathManagerValues(stockPathManager);
					Log._Debug("UpdateWithPathManagerValues success");

					pathManagerInstance?.SetValue(null, CustomPathManager);

					Log._Debug("Getting Current SimulationManager");
					var simManager =
						typeof(SimulationManager).GetField("m_managers", BindingFlags.Static | BindingFlags.NonPublic)?
							.GetValue(null) as FastList<ISimulationManager>;

					Log._Debug("Removing Stock PathManager");
					simManager?.Remove(stockPathManager);

					Log._Debug("Adding Custom PathManager");
					simManager?.Add(CustomPathManager);

					Object.Destroy(stockPathManager, 10f);

					Log._Debug("Should be custom: " + Singleton<PathManager>.instance.GetType().ToString());

					IsPathManagerReplaced = true;
				} catch (Exception ex) {
					string error = "Traffic Manager: President Edition failed to load. You can continue playing but it's NOT recommended. Traffic Manager will not work as expected.";
					Log.Error(error);
					Log.Error($"Path manager replacement error: {ex.ToString()}");
					Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(() => {
						UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("TM:PE failed to load", error, true);
					});
				}
			}

			Log.Info("Adding Controls to UI.");
			if (BaseUI == null) {
				Log._Debug("Adding UIBase instance.");
				BaseUI = ToolsModifierControl.toolController.gameObject.AddComponent<UIBase>();
			}

			// Init transport demand UI
			if (TransportDemandUI == null) {
				var uiView = UIView.GetAView();
				TransportDemandUI = (UITransportDemand)uiView.AddUIComponent(typeof(UITransportDemand));
			}

			// add "remove vehicle" button
			UIView.GetAView().gameObject.AddComponent<RemoveVehicleButtonExtender>();

			// add "remove citizen instance" button
			UIView.GetAView().gameObject.AddComponent<RemoveCitizenInstanceButtonExtender>();
			
			initDetours();

			//Log.Info("Fixing non-created nodes with problems...");
			//FixNonCreatedNodeProblems();

			Log.Info("Notifying managers...");
			foreach (ICustomManager manager in RegisteredManagers) {
				Log.Info($"OnLevelLoading: {manager.GetType().Name}");
				manager.OnLevelLoading();
			}

			//InitTool();
			//Log._Debug($"Current tool: {ToolManager.instance.m_properties.CurrentTool}");

			Log.Info("OnLevelLoaded complete.");
		}

		private bool Check3rdPartyModLoaded(string namespaceStr, bool printAll=false) {
			bool thirdPartyModLoaded = false;

			var loadingWrapperLoadingExtensionsField = typeof(LoadingWrapper).GetField("m_LoadingExtensions", BindingFlags.NonPublic | BindingFlags.Instance);
			List<ILoadingExtension> loadingExtensions = null;
			if (loadingWrapperLoadingExtensionsField != null) {
				loadingExtensions = (List<ILoadingExtension>)loadingWrapperLoadingExtensionsField.GetValue(Singleton<LoadingManager>.instance.m_LoadingWrapper);
			} else {
				Log.Warning("Could not get loading extensions field");
			}

			if (loadingExtensions != null) {
				foreach (ILoadingExtension extension in loadingExtensions) {
					if (printAll)
						Log.Info($"Detected extension: {extension.GetType().Name} in namespace {extension.GetType().Namespace}");
					if (extension.GetType().Namespace == null)
						continue;

					var nsStr = extension.GetType().Namespace.ToString();
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
