using System;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using ICities;
using UnityEngine;

namespace TrafficManager
{
    public enum TrafficManagerMode
    {
        None = 0,
        TrafficLight = 1
    }

    public class TrafficManagerMod : IUserMod
    {
        public string Name
        {
            get
            {

                return "Traffic Manager";
            }
        }

        public string Description
        {
            get { return "Manage traffic junctions"; }
        }
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

            if (LoadingExtension.Instance.ToolMode != TrafficManagerMode.None && ToolsModifierControl.toolController.CurrentTool != LoadingExtension.Instance.TrafficLightTool)
            {
                LoadingExtension.Instance.UI.Close();
            }

            if (ToolsModifierControl.toolController.CurrentTool != LoadingExtension.Instance.TrafficLightTool && LoadingExtension.Instance.UI.isVisible())
            {
                LoadingExtension.Instance.UI.Close();
            }

            if (!LoadingExtension.Instance.detourInited)
            {
                LoadingExtension.Instance.revertMethods[0] = RedirectionHelper.RedirectCalls(
                    typeof (CarAI).GetMethod("CalculateSegmentPosition",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new Type[]
                        {
                            typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (PathUnit.Position),
                            typeof (PathUnit.Position), typeof (uint), typeof (byte), typeof (PathUnit.Position),
                            typeof (uint), typeof (byte), typeof (Vector3).MakeByRefType(),
                            typeof (Vector3).MakeByRefType(), typeof (float).MakeByRefType()
                        },
                        null),
                    typeof (CustomCarAI).GetMethod("CalculateSegmentPosition"));

                LoadingExtension.Instance.revertMethods[1] = RedirectionHelper.RedirectCalls(
                    typeof (RoadBaseAI).GetMethod("SimulationStep",
                        new Type[] {typeof (ushort), typeof (NetNode).MakeByRefType()}),
                    typeof (CustomRoadAI).GetMethod("SimulationStep", BindingFlags.NonPublic | BindingFlags.Instance));

                LoadingExtension.Instance.revertMethods[2] = RedirectionHelper.RedirectCalls(typeof (HumanAI).GetMethod("CheckTrafficLights",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new Type[] {typeof (ushort), typeof (ushort)},
                    null),
                    typeof (CustomHumanAI).GetMethod("CheckTrafficLights"));

                if (!LoadingExtension.PathfinderIncompatibility) {
                    LoadingExtension.Instance.revertMethods[3] =
                    RedirectionHelper.RedirectCalls(
                        typeof(CarAI).GetMethod("SimulationStep",
                            new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3) }),
                        typeof(CustomCarAI).GetMethod("SimulationStep", BindingFlags.NonPublic | BindingFlags.Instance));


                    LoadingExtension.Instance.revertMethods[4] =
                        RedirectionHelper.RedirectCalls(
                            typeof(PassengerCarAI).GetMethod("SimulationStep",
                                new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3) }),
                            typeof(CustomPassengerCarAI).GetMethod("SimulationStep",
                                BindingFlags.NonPublic | BindingFlags.Instance));

                    LoadingExtension.Instance.revertMethods[5] =
                        RedirectionHelper.RedirectCalls(
                            typeof(CargoTruckAI).GetMethod("SimulationStep",
                                new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3) }),
                            typeof(CustomCargoTruckAI).GetMethod("SimulationStep",
                                BindingFlags.NonPublic | BindingFlags.Instance));

                    LoadingExtension.Instance.revertMethods[6] = RedirectionHelper.RedirectCalls(typeof(CarAI).GetMethod("CalculateSegmentPosition",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new Type[]
                    {
                        typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (PathUnit.Position), typeof (uint),
                        typeof (byte), typeof (Vector3).MakeByRefType(), typeof (Vector3).MakeByRefType(),
                        typeof (float).MakeByRefType()
                    },
                        null),
                        typeof(CustomCarAI).GetMethod("CalculateSegmentPosition2"));

                    //public bool CreateNodeImpl(NetInfo info, bool needMoney, bool switchDirection, NetTool.ControlPoint startPoint, NetTool.ControlPoint middlePoint, NetTool.ControlPoint endPoint)
                    //LoadingExtension.Instance.revertMethods[7] = RedirectionHelper.RedirectCalls(typeof(NetTool).GetMethod("CreateNodeImpl",
                    //    BindingFlags.NonPublic | BindingFlags.Instance,
                    //    null,
                    //    new Type[] { typeof(NetInfo), typeof(bool), typeof(bool), typeof(NetTool.ControlPoint), typeof(NetTool.ControlPoint), typeof(NetTool.ControlPoint) },
                    //    null),
                    //    typeof(CustomNetTool).GetMethod("CreateNodeImpl"));

                    //srcMethod8 = typeof(CarAI).GetMethod("StartPathFind",
                    //    BindingFlags.NonPublic | BindingFlags.Instance,
                    //    null,
                    //    new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3), typeof(Vector3), typeof(bool), typeof(bool) },
                    //    null);

                    //destMethod[8 = typeof(CustomCarAI).GetMethod("StartPathFind");

                    //srcMethod9 = typeof (TransportLineAI).GetMethod("StartPathFind");

                    //destMethod[9 = typeof(CustomTransportLineAI).GetMethod("StartPathFind", BindingFlags.NonPublic | BindingFlags.Static);

                    //srcMethod10 = typeof(PassengerCarAI).GetMethod("StartPathFind",
                    //    BindingFlags.NonPublic | BindingFlags.Instance,
                    //    null,
                    //    new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3), typeof(Vector3), typeof(bool), typeof(bool) },
                    //    null);

                    //destMethod[10 = typeof(CustomPassengerCarAI).GetMethod("StartPathFind2");

                    //srcMethod11 = typeof(CargoTruckAI).GetMethod("StartPathFind",
                    //    BindingFlags.NonPublic | BindingFlags.Instance,
                    //    null,
                    //    new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3), typeof(Vector3), typeof(bool), typeof(bool) },
                    //    null);

                    //destMethod[11 = typeof(CustomCargoTruckAI).GetMethod("StartPathFind2");

                    //LoadingExtension.Instance.revertMethods[1] = RedirectionHelper.RedirectCalls(typeof (NetNode).GetMethod("RefreshJunctionData",
                    //    BindingFlags.NonPublic | BindingFlags.Instance,
                    //    null,
                    //    new Type[]
                    //    {
                    //        typeof (ushort), typeof (int), typeof (ushort), typeof (Vector3), typeof (uint).MakeByRefType(),
                    //        typeof (RenderManager.Instance).MakeByRefType()
                    //    },
                    //    null),
                    //    typeof (CustomNetNode).GetMethod("RefreshJunctionData"));

                    //
                }

                LoadingExtension.Instance.detourInited = true;
            }

            if (!LoadingExtension.Instance.nodeSimulationLoaded)
            {
                LoadingExtension.Instance.nodeSimulationLoaded = true;
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
        public static LoadingExtension Instance = null;

        public static bool PathfinderIncompatibility = false;

        public RedirectCallsState[] revertMethods = new RedirectCallsState[8];

        public TrafficManagerMode ToolMode = TrafficManagerMode.None;

        public TrafficLightTool TrafficLightTool = null;

        public UIBase UI;

        private ToolBase _originalTool = null;

        public bool detourInited = false;

        public bool nodeSimulationLoaded = false;

        public CustomPathManager customPathManager;

        public bool despawnEnabled = true;

        public override void OnCreated(ILoading loading)
        {
            base.OnCreated(loading);
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
                default:
                    
                    break;
            }

            if (mode == LoadMode.NewGame || mode == LoadMode.LoadGame)
            {
                if (Instance == null)
                {
                    if (Singleton<PathManager>.instance.GetType() != typeof(PathManager))
                    {
                        LoadingExtension.PathfinderIncompatibility = true;
                    }


                    Instance = this;
                }

                if (!LoadingExtension.PathfinderIncompatibility)
                {
                    FieldInfo pathManagerInstance = typeof(Singleton<PathManager>).GetField("sInstance", BindingFlags.Static | BindingFlags.NonPublic);
                    PathManager stockPathManager = PathManager.instance;
                    customPathManager = stockPathManager.gameObject.AddComponent<CustomPathManager>();
                    customPathManager.UpdateWithPathManagerValues(stockPathManager);
                    pathManagerInstance.SetValue(null, customPathManager);
                    FastList<ISimulationManager> managers = typeof(SimulationManager).GetField("m_managers", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as FastList<ISimulationManager>;
                    managers.Remove(stockPathManager);
                    managers.Add(customPathManager);
                    GameObject.Destroy(stockPathManager, 10f);
                }

                UI = ToolsModifierControl.toolController.gameObject.AddComponent<UIBase>();
                TrafficPriority.leftHandDrive = Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True;
            }
        }

        public override void OnLevelUnloading()
        {
            // TODO: revert detours
            base.OnLevelUnloading();

            //RedirectionHelper.RevertRedirect(typeof(CustomCarAI).GetMethod("CalculateSegmentPosition"), revertMethods[0]);
            //RedirectionHelper.RevertRedirect(typeof(CustomRoadAI).GetMethod("SimulationStep", BindingFlags.NonPublic | BindingFlags.Instance), revertMethods[1]);
            //RedirectionHelper.RevertRedirect(typeof(CustomHumanAI).GetMethod("CheckTrafficLights"), revertMethods[2]);
            //RedirectionHelper.RevertRedirect(typeof(CustomCarAI).GetMethod("SimulationStep", BindingFlags.NonPublic | BindingFlags.Instance), revertMethods[3]);
            //RedirectionHelper.RevertRedirect(typeof(CustomPassengerCarAI).GetMethod("SimulationStep", BindingFlags.NonPublic | BindingFlags.Instance), revertMethods[4]);
            //RedirectionHelper.RevertRedirect(typeof(CustomCargoTruckAI).GetMethod("SimulationStep", BindingFlags.NonPublic | BindingFlags.Instance), revertMethods[5]);
            //RedirectionHelper.RevertRedirect(typeof(CustomCarAI).GetMethod("CalculateSegmentPosition2"), revertMethods[6]);
            TrafficPriority.prioritySegments.Clear();
            CustomRoadAI.nodeDictionary.Clear();
            TrafficLightsManual.ManualSegments.Clear();
            TrafficLightsTimed.timedScripts.Clear();

            LoadingExtension.Instance.nodeSimulationLoaded = false;
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

                TrafficLightTool.Destroy(TrafficLightTool);
                TrafficLightTool = null;
            }
        }
    }
}