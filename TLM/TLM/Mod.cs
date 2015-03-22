using System;
using ColossalFramework;
using ColossalFramework.UI;
using ICities;
using UnityEngine;

namespace TrafficManager
{
    public enum ToolMode
    {
        None = 0,
        TrafficLight = 1,
        LaneChange = 2,
        All = 3
    }

    public class Mod : IUserMod
    {

        public string Name
        {
            get { return "Traffic Manager"; }
        }

        public string Description
        {
            get { return "Manage traffic junctions"; }
        }

    }

    public sealed class ThreadingExtension : ThreadingExtensionBase
    {
        public UIPanel RoadsPanel = null;
        public NetTool NetTool = null;
        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            base.OnUpdate(realTimeDelta, simulationTimeDelta);

            if (LoadingExtension.Instance == null)
            {
                return;
            }

            if (RoadsPanel == null)
            {
                RoadsPanel = UIView.Find<UIPanel>("RoadsPanel");
            }

            if (RoadsPanel == null || !RoadsPanel.isVisible)
            {
                if (LoadingExtension.Instance.ToolMode != ToolMode.None) LoadingExtension.Instance.SetToolMode(ToolMode.None);
                return;
            }

            if (LoadingExtension.Instance.ToolMode != ToolMode.None && ToolsModifierControl.toolController.CurrentTool != LoadingExtension.Instance.ToolTrafficLight && ToolsModifierControl.toolController.CurrentTool != LoadingExtension.Instance.ToolLaneChange)
            {
                LoadingExtension.Instance.SetToolMode(ToolMode.None);
            }

            if (!LoadingExtension.Instance.UI.isVisible)
            {
                LoadingExtension.Instance.UI.Show();
            }
        }
    }

    public sealed class LoadingExtension : LoadingExtensionBase
    {
        public static LoadingExtension Instance = null;

        public ToolMode ToolMode = ToolMode.None;

        public ToolTrafficLight ToolTrafficLight = null;
        public ToolLaneChange ToolLaneChange = null;

        public ModUI UI = new ModUI();

        public override void OnCreated(ILoading loading)
        {
            base.OnCreated(loading);

            if (Instance == null)
            {
                Instance = this;
            }

            UI.selectedToolModeChanged += (ToolMode newMode) =>
            {
                SetToolMode(newMode);
            };
        }
        public override void OnReleased()
        {
            base.OnReleased();

            if (ToolMode != ToolMode.None)
            {
                DestroyTool(ToolMode.All);
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
        }

        public override void OnLevelUnloading()
        {
            base.OnLevelUnloading();
        }

        private void OnLoaded()
        {
            
        }

        public void SetToolMode(ToolMode mode)
        {
            if (mode == ToolMode) return;

            UI.toolMode = mode;
            ToolMode = mode;

            DestroyTool(ToolMode.All);
            EnableTool(mode);
        }

        public void EnableTool(ToolMode mode)
        {
            if (mode == ToolMode.TrafficLight)
            {
                if (ToolTrafficLight == null)
                {
                    CreateTool(ToolMode.TrafficLight);
                }

                ToolsModifierControl.toolController.CurrentTool = ToolTrafficLight;
            }
            else if (mode == ToolMode.LaneChange)
            {
                if (ToolLaneChange == null)
                {
                    CreateTool(ToolMode.LaneChange);
                }

                ToolsModifierControl.toolController.CurrentTool = ToolLaneChange;
            }
        }
        private void CreateTool(ToolMode mode)
        {
            if (mode == ToolMode.TrafficLight)
            {
                ToolTrafficLight = ToolsModifierControl.toolController.gameObject.GetComponent<ToolTrafficLight>() ??
                        ToolsModifierControl.toolController.gameObject.AddComponent<ToolTrafficLight>();
            }
            else if (mode == ToolMode.LaneChange)
            {
                ToolLaneChange = ToolsModifierControl.toolController.gameObject.GetComponent<ToolLaneChange>() ??
                        ToolsModifierControl.toolController.gameObject.AddComponent<ToolLaneChange>();
            }
        }

        private void DestroyTool( ToolMode mode )
        {
            switch (mode)
            {
                case ToolMode.LaneChange:
                    if (ToolLaneChange != null)
                    {
                        ToolLaneChange.Destroy(ToolLaneChange);
                        ToolLaneChange = null;
                    }
                    break;
                case ToolMode.TrafficLight:
                    if (ToolTrafficLight != null)
                    {
                        ToolTrafficLight.Destroy(ToolTrafficLight);
                        ToolTrafficLight = null;
                    }
                    break;
                case ToolMode.All:
                    DestroyTool(ToolMode.LaneChange);
                    DestroyTool(ToolMode.TrafficLight);
                    break;
            }
        }
    }
}