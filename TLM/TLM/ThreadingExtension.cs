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
using CSUtil.Commons.Benchmark;
using TrafficManager.UI.MainMenu;

namespace TrafficManager {
    public sealed class ThreadingExtension : ThreadingExtensionBase {
		//int ticksSinceLastMinuteUpdate = 0;

		ITrafficLightSimulationManager tlsMan = Constants.ManagerFactory.TrafficLightSimulationManager;
		IRoutingManager routeMan = Constants.ManagerFactory.RoutingManager;
		IUtilityManager utilMan = Constants.ManagerFactory.UtilityManager;

		public override void OnCreated(IThreading threading) {
			base.OnCreated(threading);

			//ticksSinceLastMinuteUpdate = 0;
		}

		public override void OnBeforeSimulationFrame() {
			base.OnBeforeSimulationFrame();
#if BENCHMARK
			using (var bm = new Benchmark(null, "RoutingManager.SimulationStep")) {
#endif
				routeMan.SimulationStep();
#if BENCHMARK
			}
#endif

#if BENCHMARK
			using (var bm = new Benchmark(null, "TrafficLightSimulationManager.SimulationStep")) {
#endif

				if (Options.timedLightsEnabled) {
					//try {
						tlsMan.SimulationStep();
					/*} catch (Exception ex) {
						Log.Warning($"Error occured while simulating traffic lights: {ex.ToString()}");
					}*/
				}
#if BENCHMARK
			}
#endif
		}

		/*public override void OnAfterSimulationFrame() {
			base.OnAfterSimulationFrame();

			routeMan.SimulationStep();

			++ticksSinceLastMinuteUpdate;
			if (ticksSinceLastMinuteUpdate > 60 * 60) {
				ticksSinceLastMinuteUpdate = 0;
				GlobalConfig.Instance.SimulationStep();
#if DEBUG
				DebugMenuPanel.PrintTransportStats();
#endif
			}
		}*/

		public override void OnUpdate(float realTimeDelta, float simulationTimeDelta) {
            base.OnUpdate(realTimeDelta, simulationTimeDelta);

#if !TAM
#if BENCHMARK
			using (var bm = new Benchmark()) {
#endif

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
#if BENCHMARK
			}
#endif
#endif
		}
	}
}
