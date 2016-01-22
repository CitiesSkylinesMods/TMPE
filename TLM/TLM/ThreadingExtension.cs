using System;
using System.Reflection;
using ColossalFramework;
using ICities;
using TrafficManager.Custom.AI;
using UnityEngine;

namespace TrafficManager {
    public sealed class ThreadingExtension : ThreadingExtensionBase {
        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta) {
            base.OnUpdate(realTimeDelta, simulationTimeDelta);

            //Debug.Log("Checking for NULL LoadingExtension Instance");
            if (LoadingExtension.Instance == null || ToolsModifierControl.toolController == null) {
                return;
            }

           // Log.Message("Getting ToolMode");
            if (LoadingExtension.Instance.ToolMode != TrafficManagerMode.None &&
                ToolsModifierControl.toolController.CurrentTool != LoadingExtension.Instance.TrafficLightTool) {
                Log._Debug("Closing UI");
                LoadingExtension.Instance.UI.Close();
            }

            //Debug.Log("Checking if TrafficLightTool is Visible");
            //Log.Message("ToolController: " + ToolsModifierControl.toolController);
            if (ToolsModifierControl.toolController.CurrentTool != LoadingExtension.Instance.TrafficLightTool && (LoadingExtension.Instance.UI != null && LoadingExtension.Instance.UI.IsVisible())) {
                Log._Debug("Closing UI");
                LoadingExtension.Instance.UI.Close();
            }
			
            if (!LoadingExtension.Instance.NodeSimulationLoaded) {
                LoadingExtension.Instance.NodeSimulationLoaded = true;
                ToolsModifierControl.toolController.gameObject.AddComponent<CustomRoadAI>();
            }

            if (Input.GetKeyDown(KeyCode.Escape)) {
                LoadingExtension.Instance.UI.Close();
            }
        }
    }
}
