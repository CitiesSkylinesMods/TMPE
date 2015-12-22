using System;
using System.Reflection;
using ColossalFramework;
using ICities;
using TrafficManager.CustomAI;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using TrafficManager.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrafficManager {
    public class LoadingExtension : LoadingExtensionBase {
        public static LoadingExtension Instance;
        public static bool IsPathManagerCompatibile = true;
        public CustomPathManager CustomPathManager { get; set; }
        public bool DespawnEnabled { get; set; }
        public bool DetourInited { get; set; }
        public bool NodeSimulationLoaded { get; set; }
		public MethodInfo[] OriginalMethods { get; set; }
		public RedirectCallsState[] CustomMethods { get; set; }
        public TrafficManagerMode ToolMode { get; set; }
        public TrafficLightTool TrafficLightTool { get; set; }
        public UIBase UI { get; set; }

        public LoadingExtension() {
        }

		public void revertDetours() {
			if (LoadingExtension.Instance.DetourInited) {
				Log.Message("Revert detours");
				for (int i = 0; i < 7; ++i) {
					if (LoadingExtension.Instance.OriginalMethods[i] != null)
						RedirectionHelper.RevertRedirect(LoadingExtension.Instance.OriginalMethods[i], LoadingExtension.Instance.CustomMethods[i]);
				}
				LoadingExtension.Instance.DetourInited = false;
			}
		}

		public void initDetours() {
			if (!LoadingExtension.Instance.DetourInited) {
				Log.Message("Redirecting Car AI Calculate Segment Calls");
				try {

					LoadingExtension.Instance.OriginalMethods[0] = typeof(CarAI).GetMethod("CalculateSegmentPosition",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (PathUnit.Position),
								typeof (PathUnit.Position), typeof (uint), typeof (byte), typeof (PathUnit.Position),
								typeof (uint), typeof (byte), typeof (Vector3).MakeByRefType(),
								typeof (Vector3).MakeByRefType(), typeof (float).MakeByRefType()
							},
							null);
					LoadingExtension.Instance.CustomMethods[0] = RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[0], typeof(CustomCarAI).GetMethod("TmCalculateSegmentPosition"));
				} catch (Exception) {
					Log.Error("Could not redirect CarAI::CalculateSegmentPosition.");
				}

				Log.Message("Redirecting SimulationStep");
				try {
					LoadingExtension.Instance.OriginalMethods[1] = typeof(RoadBaseAI).GetMethod("SimulationStep", new[] { typeof(ushort), typeof(NetNode).MakeByRefType() });
					LoadingExtension.Instance.CustomMethods[1] = RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[1], typeof(CustomRoadAI).GetMethod("CustomSimulationStep"));
				} catch (Exception) {
					Log.Error("Could not redirect RoadBaseAI::SimulationStep.");
				}

				Log.Message("Redirecting Human AI Calls");
				try {
					LoadingExtension.Instance.OriginalMethods[2] = typeof(HumanAI).GetMethod("CheckTrafficLights",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[] { typeof(ushort), typeof(ushort) },
							null);
					LoadingExtension.Instance.CustomMethods[2] = RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[2], typeof(CustomHumanAI).GetMethod("CustomCheckTrafficLights"));
				} catch (Exception) {
					Log.Error("Could not redirect HumanAI::CheckTrafficLights.");
				}

				if (LoadingExtension.IsPathManagerCompatibile) {
					Log.Message("Traffic++ Not detected. Loading Pathfinder.");
					Log.Message("Redirecting CarAI Simulation Step Calls");
					try {
						LoadingExtension.Instance.OriginalMethods[3] = typeof(CarAI).GetMethod("SimulationStep",
									new[] {
										typeof (ushort),
										typeof (Vehicle).MakeByRefType(),
										typeof (Vector3)
									});
						LoadingExtension.Instance.CustomMethods[3] = RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[3], typeof(CustomCarAI).GetMethod("TrafficManagerSimulationStep"));
					} catch (Exception) {
						Log.Error("Could not redirect CarAI::SimulationStep.");
					}

					Log.Message("Redirecting PassengerCarAI Simulation Step Calls");
					try {
						LoadingExtension.Instance.OriginalMethods[4] = typeof(PassengerCarAI).GetMethod("SimulationStep",
								new[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3) });
						LoadingExtension.Instance.CustomMethods[4] = RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[4], typeof(CustomPassengerCarAI).GetMethod("CustomSimulationStep"));
					} catch (Exception) {
						Log.Error("Could not redirect PassengerCarAI::SimulationStep.");
					}

					Log.Message("Redirecting CargoTruckAI Simulation Step Calls");
					try {
						LoadingExtension.Instance.OriginalMethods[5] = typeof(CargoTruckAI).GetMethod("SimulationStep",
									new[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3) });
						LoadingExtension.Instance.CustomMethods[5] = RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[5], typeof(CustomCargoTruckAI).GetMethod("CustomSimulationStep"));
					} catch (Exception) {
						Log.Error("Could not redirect CargoTruckAI::SimulationStep.");
					}

					Log.Message("Redirection CarAI Calculate Segment Position calls for non-Traffic++");
					try {
						LoadingExtension.Instance.OriginalMethods[6] = typeof(CarAI).GetMethod("CalculateSegmentPosition",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (PathUnit.Position),
									typeof (uint),
									typeof (byte), typeof (Vector3).MakeByRefType(), typeof (Vector3).MakeByRefType(),
									typeof (float).MakeByRefType()
								},
								null);
						LoadingExtension.Instance.CustomMethods[6] =
							RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[6], typeof(CustomCarAI).GetMethod("TmCalculateSegmentPositionPathFinder"));
					} catch (Exception) {
						Log.Error("Could not redirect CarAI::CalculateSegmentPosition");
					}
				}

				LoadingExtension.Instance.DetourInited = true;
			}
		}

        public override void OnCreated(ILoading loading) {
            SelfDestruct.DestructOldInstances(this);

            base.OnCreated(loading);

            Log.Message("Setting ToolMode");
            ToolMode = TrafficManagerMode.None;

            Log.Message("Init RevertMethods");
			OriginalMethods = new MethodInfo[7];
			CustomMethods = new RedirectCallsState[7];

            Log.Message("Setting Despawn to False");
            DespawnEnabled = true;

            Log.Message("Init DetourInited");
            DetourInited = false;

            Log.Message("Init Custom PathManager");
            CustomPathManager = new CustomPathManager();
        }

        public override void OnReleased() {
            base.OnReleased();

            if (ToolMode != TrafficManagerMode.None) {
                ToolMode = TrafficManagerMode.None;
                UITrafficManager.UIState = UIState.None;
                DestroyTool();
            }
        }

        public override void OnLevelLoaded(LoadMode mode) {
            Log.Message("OnLevelLoaded calling base method");
            base.OnLevelLoaded(mode);
            Log.Message("OnLevelLoaded Returned from base, calling custom code.");

            switch (mode) {
                case LoadMode.NewGame:
                    OnNewGame();
                    break;
                case LoadMode.LoadGame:
                    OnLoaded();
                    break;
            }

            if (mode == LoadMode.NewGame || mode == LoadMode.LoadGame) {
				Log.Message("Instance is NULL. Set Instance to this.");
				if (Singleton<PathManager>.instance.GetType() != typeof(PathManager)) {
					Log.Message("Traffic++ Detected. Disable Pathfinder");
					IsPathManagerCompatibile = false;
				}

				if (Instance != null) {
					revertDetours();
				}

                Instance = this;
				initDetours();

                if (IsPathManagerCompatibile) {
                    Log.Message("Pathfinder Compatible. Setting up CustomPathManager and SimManager.");
                    var pathManagerInstance = typeof(Singleton<PathManager>).GetField("sInstance",
                        BindingFlags.Static | BindingFlags.NonPublic);

                    var stockPathManager = PathManager.instance;
                    Log.Message($"Got stock PathManager instance {stockPathManager.GetName()}");

                    CustomPathManager = stockPathManager.gameObject.AddComponent<CustomPathManager>();
                    Log.Message("Added CustomPathManager to gameObject List");

                    if (CustomPathManager == null) {
                        Log.Error("CustomPathManager null. Error creating it.");
                        return;
                    }

                    CustomPathManager.UpdateWithPathManagerValues(stockPathManager);
                    Log.Message("UpdateWithPathManagerValues success");

                    pathManagerInstance?.SetValue(null, CustomPathManager);

                    Log.Message("Getting Current SimulationManager");
                    var simManager =
                        typeof(SimulationManager).GetField("m_managers", BindingFlags.Static | BindingFlags.NonPublic)?
                            .GetValue(null) as FastList<ISimulationManager>;

                    Log.Message("Removing Stock PathManager");
                    simManager?.Remove(stockPathManager);

                    Log.Message("Adding Custom PathManager");
                    simManager?.Add(CustomPathManager);

                    Object.Destroy(stockPathManager, 10f);
                }

                Log.Message("Adding Controls to UI.");
                UI = ToolsModifierControl.toolController.gameObject.AddComponent<UIBase>();
                TrafficPriority.LeftHandDrive = Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic ==
                                                SimulationMetaData.MetaBool.True;
            }
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();
			if (Instance == null)
				Instance = this;
			revertDetours();

			try {
				if (TrafficPriority.PrioritySegments != null)
					TrafficPriority.PrioritySegments.Clear();
				if (CustomRoadAI.NodeDictionary != null)
					CustomRoadAI.NodeDictionary.Clear();
				if (TrafficLightsManual.ManualSegments != null)
					TrafficLightsManual.ManualSegments.Clear();
				if (TrafficLightsTimed.TimedScripts != null)
					TrafficLightsTimed.TimedScripts.Clear();

				if (Instance != null)
					Instance.NodeSimulationLoaded = false;
            } catch (Exception e) {
                Log.Error("Exception unloading mod. " + e.Message);
                // ignored - prevents collision with other mods
            }
        }

        protected virtual void OnNewGame() {
            Log.Message("New Game Started");
        }

        protected virtual void OnLoaded() {
            Log.Message("Loaded save game.");
        }

        public void SetToolMode(TrafficManagerMode mode) {
            if (mode == ToolMode) return;

            //UI.toolMode = mode;
            ToolMode = mode;

            if (mode != TrafficManagerMode.None) {
                DestroyTool();
                EnableTool();
            } else {
                DestroyTool();
            }
        }

        public void EnableTool() {
            if (TrafficLightTool == null) {
                TrafficLightTool = ToolsModifierControl.toolController.gameObject.GetComponent<TrafficLightTool>() ??
                                   ToolsModifierControl.toolController.gameObject.AddComponent<TrafficLightTool>();
            }

            ToolsModifierControl.toolController.CurrentTool = TrafficLightTool;
            ToolsModifierControl.SetTool<TrafficLightTool>();
        }

        private void DestroyTool() {
            if (TrafficLightTool != null) {
                ToolsModifierControl.toolController.CurrentTool = ToolsModifierControl.GetTool<DefaultTool>();
                ToolsModifierControl.SetTool<DefaultTool>();

                Object.Destroy(TrafficLightTool);
                TrafficLightTool = null;
            }
        }
    }
}
