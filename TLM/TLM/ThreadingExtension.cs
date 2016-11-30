using System;
using System.Reflection;
using ColossalFramework;
using ICities;
using TrafficManager.Custom.AI;
using UnityEngine;
using TrafficManager.State;
using TrafficManager.Manager;

namespace TrafficManager {
    public sealed class ThreadingExtension : ThreadingExtensionBase {
		int ticksSinceLastMinuteUpdate = 0;
		int ticksSinceLastSecondUpdate = 0;

		public override void OnCreated(IThreading threading) {
			base.OnCreated(threading);

			ticksSinceLastMinuteUpdate = 0;
			ticksSinceLastSecondUpdate = 0;
		}

		public override void OnAfterSimulationFrame() {
			++ticksSinceLastMinuteUpdate;
			if (ticksSinceLastMinuteUpdate > 60 * 60) {
				ticksSinceLastMinuteUpdate = 0;
				GlobalConfig.Instance.SimulationStep();
			}

			++ticksSinceLastSecondUpdate;
			if (ticksSinceLastSecondUpdate > 60) {
				ticksSinceLastSecondUpdate = 0;

				try {
					VehicleStateManager.Instance.SimulationStep();
					UtilityManager.Instance.SimulationStep();
				} catch (Exception e) {
					Log.Error($"Error occured while performing second update: " + e.ToString());
				}
			}
		}

		public override void OnUpdate(float realTimeDelta, float simulationTimeDelta) {
            base.OnUpdate(realTimeDelta, simulationTimeDelta);
#if !TAM
			if (LoadingExtension.Instance == null || ToolsModifierControl.toolController == null || ToolsModifierControl.toolController == null || LoadingExtension.Instance.BaseUI == null) {
                return;
            }

            if (ToolsModifierControl.toolController.CurrentTool != LoadingExtension.Instance.TrafficManagerTool && LoadingExtension.Instance.BaseUI.IsVisible()) {
                LoadingExtension.Instance.BaseUI.Close();
            }

            if (Input.GetKeyDown(KeyCode.Escape)) {
                LoadingExtension.Instance.BaseUI.Close();
            }
#endif
		}
    }
}
