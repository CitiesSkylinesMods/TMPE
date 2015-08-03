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
    public class LoadingExtension : LoadingExtensionBase
    {
        public static LoadingExtension Instance;
        public static bool IsPathManagerCompatibile = true;
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
        }

        public override void OnCreated(ILoading loading)
        {
            SelfDestruct.DestructOldInstances(this);

            base.OnCreated(loading);

            Log.Message("Setting ToolMode");
            ToolMode = TrafficManagerMode.None;

            Log.Message("Init RevertMethods");
            RevertMethods = new RedirectCallsState[8];

            Log.Message("Setting Despawn to False");
            DespawnEnabled = true;

            Log.Message("Init DetourInited");
            DetourInited = false;

            Log.Message("Init Custom PathManager");
            CustomPathManager = new CustomPathManager();
        }

        public override void OnReleased()
        {
            base.OnReleased();

            if (ToolMode != TrafficManagerMode.None)
            {
                ToolMode = TrafficManagerMode.None;
                UITrafficManager.UIState = UIState.None;
                DestroyTool();
            }
        }

        public override void OnLevelLoaded(LoadMode mode)
        {
            Log.Message("OnLevelLoaded calling base method");
            base.OnLevelLoaded(mode);
            Log.Message("OnLevelLoaded Returned from base, calling custom code.");

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
                    Log.Message("Instance is NULL. Set Instance to this.");
                    if (Singleton<PathManager>.instance.GetType() != typeof (PathManager))
                    {
                        Log.Message("Traffic++ Detected. Disable Pathfinder");
                        IsPathManagerCompatibile = false;
                    }
                    
                    Instance = this;
                }

                if (IsPathManagerCompatibile)
                {
                    Log.Message("Pathfinder Compatible. Setting up CustomPathManager and SimManager.");
                    var pathManagerInstance = typeof (Singleton<PathManager>).GetField("sInstance",
                        BindingFlags.Static | BindingFlags.NonPublic);

                    var stockPathManager = PathManager.instance;
                    Log.Message($"Got stock PathManager instance {stockPathManager.GetName()}");

                    CustomPathManager = stockPathManager.gameObject.AddComponent<CustomPathManager>();
                    Log.Message("Added CustomPathManager to gameObject List");

                    if (CustomPathManager == null)
                    {
                        Log.Error("CustomPathManager null. Error creating it.");
                        return;
                    }

                    CustomPathManager.UpdateWithPathManagerValues(stockPathManager);
                    Log.Message("UpdateWithPathManagerValues success");

                    pathManagerInstance?.SetValue(null, CustomPathManager);

                    Log.Message("Getting Current SimulationManager");
                    var simManager =
                        typeof (SimulationManager).GetField("m_managers", BindingFlags.Static | BindingFlags.NonPublic)?
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

        public override void OnLevelUnloading()
        {
            // TODO: revert detours
            base.OnLevelUnloading();

            //if (Instance == null)
            //{
            //    Log.Message("Instance is NULL. Set Instance to this.");
            //    if (Singleton<PathManager>.instance.GetType() != typeof(PathManager))
            //    {
            //        Log.Message("Traffic++ Detected. Disable Pathfinder");
            //        IsPathManagerCompatibile = false;
            //    }

            //    Instance = this;
            //}
            //try
            //{
            //    RedirectionHelper.RevertRedirect(typeof(CarAI).GetMethod("CalculateSegmentPosition",
            //            BindingFlags.NonPublic | BindingFlags.Instance,
            //            null,
            //            new[]
            //            {
            //                typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (PathUnit.Position),
            //                typeof (PathUnit.Position), typeof (uint), typeof (byte), typeof (PathUnit.Position),
            //                typeof (uint), typeof (byte), typeof (Vector3).MakeByRefType(),
            //                typeof (Vector3).MakeByRefType(), typeof (float).MakeByRefType()
            //            },
            //            null), LoadingExtension.Instance.RevertMethods[0]);

            //    RedirectionHelper.RevertRedirect(typeof (RoadBaseAI).GetMethod("SimulationStep",
            //        new[] {typeof (ushort), typeof (NetNode).MakeByRefType()}),
            //        LoadingExtension.Instance.RevertMethods[1]);

            //    Log.Message("Redirecting Human AI Calls");
            //    RedirectionHelper.RevertRedirect(typeof(HumanAI).GetMethod("CheckTrafficLights",
            //            BindingFlags.NonPublic | BindingFlags.Instance,
            //            null,
            //            new[] { typeof(ushort), typeof(ushort) },
            //            null),
            //            LoadingExtension.Instance.RevertMethods[2]);

            //    if (IsPathManagerCompatibile)
            //    {
            //        RedirectionHelper.RevertRedirect(
            //                typeof(CarAI).GetMethod("SimulationStep",
            //                    new[] {
            //                        typeof (ushort),
            //                        typeof (Vehicle).MakeByRefType(),
            //                        typeof (Vector3)
            //                    }),
            //                Instance.RevertMethods[3]);

            //        Log.Message("Redirecting PassengerCarAI Simulation Step Calls");
            //        RedirectionHelper.RevertRedirect(
            //                typeof(PassengerCarAI).GetMethod("SimulationStep",
            //                    new[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3) }),
            //                Instance.RevertMethods[4]);

            //        Log.Message("Redirecting CargoTruckAI Simulation Step Calls");
            //        RedirectionHelper.RevertRedirect(
            //                typeof(CargoTruckAI).GetMethod("SimulationStep",
            //                    new[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3) }),
            //                Instance.RevertMethods[5]);

            //        Log.Message("Redirection CarAI Calculate Segment Position calls for non-Traffic++");
            //        RedirectionHelper.RevertRedirect(typeof(CarAI).GetMethod("CalculateSegmentPosition",
            //                BindingFlags.NonPublic | BindingFlags.Instance,
            //                null,
            //                new[]
            //                {
            //                    typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (PathUnit.Position),
            //                    typeof (uint),
            //                    typeof (byte), typeof (Vector3).MakeByRefType(), typeof (Vector3).MakeByRefType(),
            //                    typeof (float).MakeByRefType()
            //                },
            //                null),
            //                Instance.RevertMethods[6]);

            //        LoadingExtension.Instance.DetourInited = false;
            //        Log.Message("Pathfinder Compatible. Setting up CustomPathManager and SimManager.");
            //        var pathManagerInstance = typeof(Singleton<PathManager>).GetField("sInstance",
            //            BindingFlags.Static | BindingFlags.NonPublic);

            //        var stockPathManager = PathManager.instance;
            //        CustomPathManager = stockPathManager.gameObject.GetComponent<CustomPathManager>();
            //        //CustomPathManager.UpdateWithPathManagerValues(stockPathManager);

            //        pathManagerInstance?.SetValue(null, CustomPathManager);

            //        Log.Message("Getting Current SimulationManager");
            //        var simManager =
            //            typeof(SimulationManager).GetField("m_managers", BindingFlags.Static | BindingFlags.NonPublic)?
            //                .GetValue(null) as FastList<ISimulationManager>;

            //        Log.Message("Removing Stock PathManager");
            //        simManager?.Remove(CustomPathManager);

            //        Log.Message("Adding Custom PathManager");
            //        simManager?.Add(stockPathManager);
            //    }

            //}
            //catch (Exception e)
            //{
            //    Log.Error("Error unloading. " + e.Message);
                
            //}
            

            try
            {
                TrafficPriority.PrioritySegments.Clear();
                CustomRoadAI.NodeDictionary.Clear();
                TrafficLightsManual.ManualSegments.Clear();
                TrafficLightsTimed.TimedScripts.Clear();

                Instance.NodeSimulationLoaded = false;
            }
            catch (Exception e)
            {
                Log.Error("Exception unloading mod. " + e.Message);
                // ignored - prevents collision with other mods
            }
        }

        protected virtual void OnNewGame()
        {
            Log.Message("New Game Started");
        }

        protected virtual void OnLoaded()
        {
            Log.Message("Loaded save game.");
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
