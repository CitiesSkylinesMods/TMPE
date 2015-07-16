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
            Debug.Log("LoadingExtension constructor entry.");

            Debug.Log("Setting ToolMode");
            ToolMode = TrafficManagerMode.None;

            Debug.Log("Init RevertMethods");
            RevertMethods = new RedirectCallsState[8];

            Debug.Log("Setting Despawn to False");
            DespawnEnabled = true;

            Debug.Log("Init DetourInited");
            DetourInited = false;

            Debug.Log("Init Custom PathManager");
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
            Debug.Log("OnLevelLoaded calling base method");
            base.OnLevelLoaded(mode);
            Debug.Log("OnLevelLoaded Returned from base, calling custom code.");

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
                    Debug.Log("Instance is NULL. Set Instance to this.");
                    if (Singleton<PathManager>.instance.GetType() != typeof (PathManager))
                    {
                        Debug.Log("Traffic++ Detected. Disable Pathfinder");
                        IsPathManagerCompatibile = false;
                    }
                    
                    Instance = this;
                }

                if (IsPathManagerCompatibile)
                {
                    Debug.Log("Pathfinder Compatible. Setting up CustomPathManager and SimManager.");
                    var pathManagerInstance = typeof (Singleton<PathManager>).GetField("sInstance",
                        BindingFlags.Static | BindingFlags.NonPublic);

                    var stockPathManager = PathManager.instance;
                    CustomPathManager = stockPathManager.gameObject.AddComponent<CustomPathManager>();
                    CustomPathManager.UpdateWithPathManagerValues(stockPathManager);

                    pathManagerInstance?.SetValue(null, CustomPathManager);

                    Debug.Log("Getting Current SimulationManager");
                    var simManager =
                        typeof (SimulationManager).GetField("m_managers", BindingFlags.Static | BindingFlags.NonPublic)?
                            .GetValue(null) as FastList<ISimulationManager>;

                    Debug.Log("Removing Stock PathManager");
                    simManager?.Remove(stockPathManager);

                    Debug.Log("Adding Custom PathManager");
                    simManager?.Add(CustomPathManager);

                    Object.Destroy(stockPathManager, 10f);
                }

                Debug.Log("Adding Controls to UI.");
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
            catch (Exception e)
            {
                Debug.LogError("Exception unloading mod. " + e.Message);
                // ignored - prevents collision with other mods
            }
        }

        protected virtual void OnNewGame()
        {
            Debug.Log("New Game Started");
        }

        protected virtual void OnLoaded()
        {
            Debug.Log("Loaded save game.");
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
