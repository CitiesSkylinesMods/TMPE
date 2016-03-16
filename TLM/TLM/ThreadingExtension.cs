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
#if !TAM
			if (LoadingExtension.Instance == null || ToolsModifierControl.toolController == null || ToolsModifierControl.toolController == null || LoadingExtension.Instance.UI == null) {
                return;
            }

            if (ToolsModifierControl.toolController.CurrentTool != LoadingExtension.Instance.TrafficManagerTool && LoadingExtension.Instance.UI.IsVisible()) {
                LoadingExtension.Instance.UI.Close();
            }

            if (Input.GetKeyDown(KeyCode.Escape)) {
                LoadingExtension.Instance.UI.Close();
            }
#endif
        }
    }
}
