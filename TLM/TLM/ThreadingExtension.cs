using System;
using System.Reflection;
using ColossalFramework;
using ICities;
using TrafficManager.Custom.AI;
using UnityEngine;
using TrafficManager.State;
using TrafficManager.Manager;
using TrafficManager.UI;
using CSUtil.Commons;

namespace TrafficManager {
    public sealed class ThreadingExtension : ThreadingExtensionBase {
		int ticksSinceLastMinuteUpdate = 0;
		int ticksSinceLastSecondUpdate = 0;

		ITrafficLightSimulationManager tlsMan = Constants.ManagerFactory.TrafficLightSimulationManager;
		IRoutingManager routeMan = Constants.ManagerFactory.RoutingManager;
		IUtilityManager utilMan = Constants.ManagerFactory.UtilityManager;

		public override void OnCreated(IThreading threading) {
			base.OnCreated(threading);

			ticksSinceLastMinuteUpdate = 0;
			ticksSinceLastSecondUpdate = 0;
		}

		public override void OnBeforeSimulationFrame() {
			base.OnBeforeSimulationFrame();
			tlsMan.SimulationStep();
		}

		public override void OnAfterSimulationFrame() {
			try {
				routeMan.SimulationStep();
			} catch (Exception e) {
				Log.Error($"Error occured while performing first update: " + e.ToString());
			}

			++ticksSinceLastMinuteUpdate;
			if (ticksSinceLastMinuteUpdate > 60 * 60) {
				ticksSinceLastMinuteUpdate = 0;
				GlobalConfig.Instance.SimulationStep();
#if DEBUG
				DebugMenuPanel.PrintTransportStats();
#endif
			}

			++ticksSinceLastSecondUpdate;
			if (ticksSinceLastSecondUpdate > 60) {
				ticksSinceLastSecondUpdate = 0;
				utilMan.SimulationStep();
			}
		}

		public override void OnUpdate(float realTimeDelta, float simulationTimeDelta) {
            base.OnUpdate(realTimeDelta, simulationTimeDelta);
#if !TAM
			if (ToolsModifierControl.toolController == null || LoadingExtension.BaseUI == null) {
                return;
            }

			TrafficManagerTool tmTool = UIBase.GetTrafficManagerTool(false);
			if (tmTool != null && ToolsModifierControl.toolController.CurrentTool != tmTool && LoadingExtension.BaseUI.IsVisible()) {
                LoadingExtension.BaseUI.Close();
            }

            if (Input.GetKeyDown(KeyCode.Escape)) {
                LoadingExtension.BaseUI.Close();
            }
#endif
		}
    }
}
