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
using static TrafficManager.LoadingExtension;
using TrafficManager.Util;
using System.Collections.Generic;
using ColossalFramework.UI;
using System.Runtime.InteropServices;
using System.Linq;
using System.Linq.Expressions;

namespace TrafficManager {
    public sealed class ThreadingExtension : ThreadingExtensionBase {
		//int ticksSinceLastMinuteUpdate = 0;

		ITrafficLightSimulationManager tlsMan = Constants.ManagerFactory.TrafficLightSimulationManager;
		IRoutingManager routeMan = Constants.ManagerFactory.RoutingManager;
		IUtilityManager utilMan = Constants.ManagerFactory.UtilityManager;

		bool firstFrame = true;

		public override void OnCreated(IThreading threading) {
			base.OnCreated(threading);

			//ticksSinceLastMinuteUpdate = 0;
		}

		public override void OnBeforeSimulationFrame() {
			base.OnBeforeSimulationFrame();

			if (firstFrame) {
				firstFrame = false;
				Log.Info($"ThreadingExtension.OnBeforeSimulationFrame: First frame detected. Checking detours.");

				List<string> missingDetours = new List<string>();
				foreach (Detour detour in LoadingExtension.Detours) {
					if (! RedirectionHelper.IsRedirected(detour.OriginalMethod, detour.CustomMethod)) {
						missingDetours.Add($"{detour.OriginalMethod.DeclaringType.Name}.{detour.OriginalMethod.Name} with {detour.OriginalMethod.GetParameters().Length} parameters ({detour.OriginalMethod.DeclaringType.AssemblyQualifiedName})");
					}
				}

				Log.Info($"ThreadingExtension.OnBeforeSimulationFrame: First frame detected. Detours checked. Result: {missingDetours.Count} missing detours");

				if (missingDetours.Count > 0) {
					string error = "Traffic Manager: President Edition detected an incompatibility with another mod! You can continue playing but it's NOT recommended. Traffic Manager will not work as expected. See TMPE.log for technical details.";
					Log.Error(error);
					string log = "The following methods were overriden by another mod:";
					foreach (string missingDetour in missingDetours) {
						log += $"\n\t{missingDetour}";
					}
					Log.Info(log);
					Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(() => {
						UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Incompatibility Issue", error, true);
					});
				}
			}

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
