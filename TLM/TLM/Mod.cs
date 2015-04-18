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

    public class Mod : IUserMod
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
                LoadingExtension.Instance.SetToolMode(TrafficManagerMode.None);
            }

            if (ToolsModifierControl.toolController.CurrentTool != LoadingExtension.Instance.TrafficLightTool && LoadingExtension.Instance.UI.isVisible())
            {
                LoadingExtension.Instance.UI.Close();
            }

            if (!LoadingExtension.Instance.detourInited)
            {
                var srcMethod = typeof(CarAI).GetMethod("CalculateSegmentPosition", 
                    BindingFlags.NonPublic | BindingFlags.Instance, 
                    null, 
                    new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(PathUnit.Position), typeof(PathUnit.Position), typeof(uint), typeof(byte), typeof(PathUnit.Position), typeof(uint), typeof(byte), typeof(Vector3).MakeByRefType(), typeof(Vector3).MakeByRefType(), typeof(float).MakeByRefType() }, 
                    null);

                var destMethod = typeof(CustomCarAI).GetMethod("CalculateSegmentPosition");

                var srcMethod2 = typeof(RoadBaseAI).GetMethod("SimulationStep", new Type[] { typeof(ushort), typeof(NetNode).MakeByRefType() });
                var destMethod2 = typeof(CustomRoadAI).GetMethod("SimulationStep", BindingFlags.NonPublic | BindingFlags.Instance);

                var srcMethod3 = typeof(HumanAI).GetMethod("CheckTrafficLights",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(ushort), typeof(ushort) },
                    null);

                var destMethod3 = typeof(CustomHumanAI).GetMethod("CheckTrafficLights");

                var srcMethod4 = typeof(CarAI).GetMethod("SimulationStep", new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3) });
                var destMethod4 = typeof(CustomCarAI).GetMethod("SimulationStep", BindingFlags.NonPublic | BindingFlags.Instance);

                //ushort vehicleID, ref Vehicle data, Vector3 physicsLodRefPos
                var srcMethod5 = typeof(PassengerCarAI).GetMethod("SimulationStep", new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3) });
                var destMethod5 = typeof(CustomPassengerCarAI).GetMethod("SimulationStep", BindingFlags.NonPublic | BindingFlags.Instance);

                var srcMethod6 = typeof(CargoTruckAI).GetMethod("SimulationStep", new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3) });
                var destMethod6 = typeof(CustomCargoTruckAI).GetMethod("SimulationStep", BindingFlags.NonPublic | BindingFlags.Instance);

                var srcMethod7 = typeof(CarAI).GetMethod("CalculateSegmentPosition",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(PathUnit.Position), typeof(uint), typeof(byte), typeof(Vector3).MakeByRefType(), typeof(Vector3).MakeByRefType(), typeof(float).MakeByRefType() },
                    null);

                var destMethod7 = typeof(CustomCarAI).GetMethod("CalculateSegmentPosition2");

                //var srcMethod8 = typeof(CarAI).GetMethod("StartPathFind",
                //    BindingFlags.NonPublic | BindingFlags.Instance,
                //    null,
                //    new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3), typeof(Vector3), typeof(bool), typeof(bool) },
                //    null);

                //var destMethod8 = typeof(CustomCarAI).GetMethod("StartPathFind");

                //var srcMethod9 = typeof (TransportLineAI).GetMethod("StartPathFind");

                //var destMethod9 = typeof(CustomTransportLineAI).GetMethod("StartPathFind", BindingFlags.NonPublic | BindingFlags.Static);

                //var srcMethod10 = typeof(PassengerCarAI).GetMethod("StartPathFind",
                //    BindingFlags.NonPublic | BindingFlags.Instance,
                //    null,
                //    new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3), typeof(Vector3), typeof(bool), typeof(bool) },
                //    null);

                //var destMethod10 = typeof(CustomPassengerCarAI).GetMethod("StartPathFind2");

                //var srcMethod11 = typeof(CargoTruckAI).GetMethod("StartPathFind",
                //    BindingFlags.NonPublic | BindingFlags.Instance,
                //    null,
                //    new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3), typeof(Vector3), typeof(bool), typeof(bool) },
                //    null);

                //var destMethod11 = typeof(CustomCargoTruckAI).GetMethod("StartPathFind2");

                if (srcMethod != null & destMethod != null && 
                    srcMethod2 != null && destMethod2 != null &&
                    srcMethod3 != null && destMethod3 != null &&
                    srcMethod4 != null && destMethod4 != null &&
                    srcMethod5 != null && destMethod5 != null &&
                    srcMethod6 != null && destMethod6 != null &&
                    srcMethod7 != null && destMethod7 != null)
                    //srcMethod8 != null && destMethod8 != null &&
                    //srcMethod9 != null && destMethod9 != null &&
                    //srcMethod10 != null && destMethod10 != null &&
                    //srcMethod11 != null && destMethod11 != null)
                {
                    LoadingExtension.Instance.detourInited = true;
                    RedirectionHelper.RedirectCalls(srcMethod, destMethod);
                    RedirectionHelper.RedirectCalls(srcMethod2, destMethod2);
                    RedirectionHelper.RedirectCalls(srcMethod3, destMethod3);
                    RedirectionHelper.RedirectCalls(srcMethod4, destMethod4);
                    RedirectionHelper.RedirectCalls(srcMethod5, destMethod5);
                    RedirectionHelper.RedirectCalls(srcMethod6, destMethod6);
                    RedirectionHelper.RedirectCalls(srcMethod7, destMethod7);
                    //RedirectionHelper.RedirectCalls(srcMethod8, destMethod8);
                    //RedirectionHelper.RedirectCalls(srcMethod9, destMethod9);
                    //RedirectionHelper.RedirectCalls(srcMethod10, destMethod10);
                    //RedirectionHelper.RedirectCalls(srcMethod11, destMethod11);
                }

                if (!LoadingExtension.Instance.nodeSimulationLoaded)
                {
                    LoadingExtension.Instance.nodeSimulationLoaded = true;
                    ToolsModifierControl.toolController.gameObject.AddComponent<CustomRoadAI>();
                }
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

            if (Instance == null)
            {
                Instance = this;
            }

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
                case LoadMode.LoadGame:
                    OnLoaded();
                    break;
                default:
                    
                    break;
            }

            UI = ToolsModifierControl.toolController.gameObject.AddComponent<UIBase>();
        }

        public override void OnLevelUnloading()
        {
            // TODO: revert detours
            base.OnLevelUnloading();

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
                if (ToolsModifierControl.toolController.CurrentTool != TrafficLightTool)
                {
                    _originalTool = ToolsModifierControl.toolController.CurrentTool;
                }

                DestroyTool();
                EnableTool();
            }
            else
            {
                ToolsModifierControl.toolController.CurrentTool = _originalTool;
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
        }

        private void DestroyTool()
        {
            if (TrafficLightTool != null)
            {
                TrafficLightTool.Destroy(TrafficLightTool);
                TrafficLightTool = null;
            }
        }
    }
}