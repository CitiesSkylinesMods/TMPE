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

namespace TrafficManager
{
    public enum TrafficManagerMode
    {
        None = 0,
        TrafficLight = 1
    }

    public class TrafficManagerMod : IUserMod
    {
        public string Name => "Traffic Manager";

        public string Description => "Manage traffic junctions";
    }

    public sealed class ThreadingExtension : ThreadingExtensionBase
    {
        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            base.OnUpdate(realTimeDelta, simulationTimeDelta);

            if (LoadingExtension.Instance == null)
            {
                return;
            }

            if (LoadingExtension.Instance.ToolMode != TrafficManagerMode.None &&
                ToolsModifierControl.toolController.CurrentTool != LoadingExtension.Instance.TrafficLightTool)
            {
                LoadingExtension.Instance.UI.Close();
            }

            if (ToolsModifierControl.toolController.CurrentTool != LoadingExtension.Instance.TrafficLightTool &&
                LoadingExtension.Instance.UI.isVisible())
            {
                LoadingExtension.Instance.UI.Close();
            }

            if (!LoadingExtension.Instance.DetourInited)
            {
                LoadingExtension.Instance.RevertMethods[0] = RedirectionHelper.RedirectCalls(
                    typeof (CarAI).GetMethod("CalculateSegmentPosition",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new[]
                        {
                            typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (PathUnit.Position),
                            typeof (PathUnit.Position), typeof (uint), typeof (byte), typeof (PathUnit.Position),
                            typeof (uint), typeof (byte), typeof (Vector3).MakeByRefType(),
                            typeof (Vector3).MakeByRefType(), typeof (float).MakeByRefType()
                        },
                        null),
                    typeof (CustomCarAI).GetMethod("CalculateSegmentPosition"));

                LoadingExtension.Instance.RevertMethods[1] = RedirectionHelper.RedirectCalls(
                    typeof (RoadBaseAI).GetMethod("SimulationStep",
                        new[] {typeof (ushort), typeof (NetNode).MakeByRefType()}),
                    typeof (CustomRoadAI).GetMethod("SimulationStep", BindingFlags.NonPublic | BindingFlags.Instance));

                LoadingExtension.Instance.RevertMethods[2] =
                    RedirectionHelper.RedirectCalls(typeof (HumanAI).GetMethod("CheckTrafficLights",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new[] {typeof (ushort), typeof (ushort)},
                        null),
                        typeof (CustomHumanAI).GetMethod("CheckTrafficLights"));

                if (!LoadingExtension.PathfinderIncompatibility)
                {
                    LoadingExtension.Instance.RevertMethods[3] =
                        RedirectionHelper.RedirectCalls(
                            typeof (CarAI).GetMethod("SimulationStep",
                                new[] {typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (Vector3)}),
                            typeof (CustomCarAI).GetMethod("SimulationStep",
                                BindingFlags.NonPublic | BindingFlags.Instance));


                    LoadingExtension.Instance.RevertMethods[4] =
                        RedirectionHelper.RedirectCalls(
                            typeof (PassengerCarAI).GetMethod("SimulationStep",
                                new[] {typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (Vector3)}),
                            typeof (CustomPassengerCarAI).GetMethod("SimulationStep",
                                BindingFlags.NonPublic | BindingFlags.Instance));

                    LoadingExtension.Instance.RevertMethods[5] =
                        RedirectionHelper.RedirectCalls(
                            typeof (CargoTruckAI).GetMethod("SimulationStep",
                                new[] {typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (Vector3)}),
                            typeof (CustomCargoTruckAI).GetMethod("SimulationStep",
                                BindingFlags.NonPublic | BindingFlags.Instance));

                    LoadingExtension.Instance.RevertMethods[6] =
                        RedirectionHelper.RedirectCalls(typeof (CarAI).GetMethod("CalculateSegmentPosition",
                            BindingFlags.NonPublic | BindingFlags.Instance,
                            null,
                            new[]
                            {
                                typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (PathUnit.Position),
                                typeof (uint),
                                typeof (byte), typeof (Vector3).MakeByRefType(), typeof (Vector3).MakeByRefType(),
                                typeof (float).MakeByRefType()
                            },
                            null),
                            typeof (CustomCarAI).GetMethod("CalculateSegmentPosition2"));
                }

                LoadingExtension.Instance.DetourInited = true;
            }

            if (!LoadingExtension.Instance.NodeSimulationLoaded)
            {
                LoadingExtension.Instance.NodeSimulationLoaded = true;
                ToolsModifierControl.toolController.gameObject.AddComponent<CustomRoadAI>();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                LoadingExtension.Instance.UI.Close();
            }
        }
    }

    public sealed class LoadingExtension : LoadingExtensionBase
    {
        public static LoadingExtension Instance;
        public static bool PathfinderIncompatibility;
        public CustomPathManager CustomPathManager { get; set; }
        public bool DespawnEnabled { get; set; }
        public bool DetourInited { get; set; }
        public bool NodeSimulationLoaded { get; set; }
        public RedirectCallsState[] RevertMethods { get; set; }
        public TrafficManagerMode ToolMode { get; set; }
        public TrafficLightTool TrafficLightTool { get; set; }
        public UIBase UI { get; set; }

        public LoadingExtension()
        {
            ToolMode = TrafficManagerMode.None;
            RevertMethods = new RedirectCallsState[8];
            DespawnEnabled = true;
        }

        public override void OnReleased()
        {
            base.OnReleased();

            if (ToolMode != TrafficManagerMode.None)
            {
                ToolMode = TrafficManagerMode.None;
                UITrafficManager.uistate = UITrafficManager.UIState.None;
                DestroyTool();
            }
        }

        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);

            switch (mode)
            {
                case LoadMode.NewGame:
                    OnNewGame();
                    break;
                case LoadMode.LoadGame:
                    OnLoaded();
                    break;
            }

            if (mode == LoadMode.NewGame || mode == LoadMode.LoadGame)
            {
                if (Instance == null)
                {
                    if (Singleton<PathManager>.instance.GetType() != typeof (PathManager))
                    {
                        PathfinderIncompatibility = true;
                    }


                    Instance = this;
                }

                if (!PathfinderIncompatibility)
                {
                    var pathManagerInstance = typeof (Singleton<PathManager>).GetField("sInstance",
                        BindingFlags.Static | BindingFlags.NonPublic);
                    var stockPathManager = PathManager.instance;
                    CustomPathManager = stockPathManager.gameObject.AddComponent<CustomPathManager>();
                    CustomPathManager.UpdateWithPathManagerValues(stockPathManager);
                    pathManagerInstance?.SetValue(null, CustomPathManager);
                    var simManager =
                        typeof (SimulationManager).GetField("m_managers", BindingFlags.Static | BindingFlags.NonPublic)?
                            .GetValue(null) as FastList<ISimulationManager>;
                    simManager?.Remove(stockPathManager);
                    simManager?.Add(CustomPathManager);
                    Object.Destroy(stockPathManager, 10f);
                }

                UI = ToolsModifierControl.toolController.gameObject.AddComponent<UIBase>();
                TrafficPriority.LeftHandDrive = Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic ==
                                                SimulationMetaData.MetaBool.True;
            }
        }

        public override void OnLevelUnloading()
        {
            // TODO: revert detours
            base.OnLevelUnloading();

            try
            {
                TrafficPriority.PrioritySegments.Clear();
                CustomRoadAI.NodeDictionary.Clear();
                TrafficLightsManual.ManualSegments.Clear();
                TrafficLightsTimed.TimedScripts.Clear();

                Instance.NodeSimulationLoaded = false;
            }
            catch (Exception)
            {
                // ignored - prevents collision with other mods
            }
        }

        private void OnNewGame()
        {
        }

        private void OnLoaded()
        {
        }

        public void SetToolMode(TrafficManagerMode mode)
        {
            if (mode == ToolMode) return;

            //UI.toolMode = mode;
            ToolMode = mode;

            if (mode != TrafficManagerMode.None)
            {
                DestroyTool();
                EnableTool();
            }
            else
            {
                DestroyTool();
            }
        }

        public void EnableTool()
        {
            if (TrafficLightTool == null)
            {
                TrafficLightTool = ToolsModifierControl.toolController.gameObject.GetComponent<TrafficLightTool>() ??
                                   ToolsModifierControl.toolController.gameObject.AddComponent<TrafficLightTool>();
            }

            ToolsModifierControl.toolController.CurrentTool = TrafficLightTool;
            ToolsModifierControl.SetTool<TrafficLightTool>();
        }

        private void DestroyTool()
        {
            if (TrafficLightTool != null)
            {
                ToolsModifierControl.toolController.CurrentTool = ToolsModifierControl.GetTool<DefaultTool>();
                ToolsModifierControl.SetTool<DefaultTool>();

                Object.Destroy(TrafficLightTool);
                TrafficLightTool = null;
            }
        }
    }
}