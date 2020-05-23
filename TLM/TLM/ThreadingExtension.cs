namespace TrafficManager {
    using ColossalFramework.UI;
    using ColossalFramework;
    using CSUtil.Commons.Benchmark;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using static LoadingExtension;
    using System.Collections.Generic;
    using System.Reflection;
    using TrafficManager.API.Manager;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State;
    using TrafficManager.UI;
    using UnityEngine;
    using TrafficManager.UI.Helpers;

    [UsedImplicitly]
    public sealed class ThreadingExtension : ThreadingExtensionBase {
        // int ticksSinceLastMinuteUpdate = 0;
        ITrafficLightSimulationManager tlsMan =
            Constants.ManagerFactory.TrafficLightSimulationManager;

        IGeometryManager geoMan = Constants.ManagerFactory.GeometryManager;
        IRoutingManager routeMan = Constants.ManagerFactory.RoutingManager;
        IUtilityManager utilMan = Constants.ManagerFactory.UtilityManager;

        bool firstFrame = true;

        public override void OnCreated(IThreading threading) {
            base.OnCreated(threading);

            //ticksSinceLastMinuteUpdate = 0;
        }

        public override void OnBeforeSimulationTick() {
            base.OnBeforeSimulationTick();

            geoMan.SimulationStep();
            routeMan.SimulationStep();
        }

        public override void OnBeforeSimulationFrame() {
            base.OnBeforeSimulationFrame();

            if (firstFrame) {
                firstFrame = false;
                Log.Info("ThreadingExtension.OnBeforeSimulationFrame: First frame detected. Checking detours.");

                List<string> missingDetours = new List<string>();

                foreach (Detour detour in Detours) {
                    if (!RedirectionHelper.IsRedirected(
                            detour.OriginalMethod,
                            detour.CustomMethod))
                    {
                        missingDetours.Add(
                            string.Format(
                                "<Manual> {0}.{1} with {2} parameters ({3})",
                                detour.OriginalMethod.DeclaringType.Name,
                                detour.OriginalMethod.Name,
                                detour.OriginalMethod.GetParameters().Length,
                                detour.OriginalMethod.DeclaringType.AssemblyQualifiedName));
                    }
                }

                foreach (KeyValuePair<MethodBase, RedirectCallsState> entry in HarmonyMethodStates) {
                    MethodBase method = entry.Key;
                    RedirectCallsState oldState = entry.Value;
                    RedirectCallsState newState =
                        RedirectionHelper.GetState(method.MethodHandle.GetFunctionPointer());

                    if (!oldState.Equals(newState)) {
                        missingDetours.Add(
                            string.Format(
                                "<Harmony> {0}.{1} with {2} parameters ({3})",
                                method.DeclaringType.Name,
                                method.Name,
                                method.GetParameters().Length,
                                method.DeclaringType.AssemblyQualifiedName));
                    }
                }

                Log.Info($"ThreadingExtension.OnBeforeSimulationFrame: First frame detected. " +
                         $"Detours checked. Result: {missingDetours.Count} missing detours");

                if (missingDetours.Count > 0) {
                    string error =
                        "Traffic Manager: President Edition detected an incompatibility with another " +
                        "mod! You can continue playing but it's NOT recommended. Traffic Manager will " +
                        "not work as expected. See TMPE.log for technical details.";
                    Log.Error(error);
                    string log = "The following methods were overriden by another mod:";

                    foreach (string missingDetour in missingDetours) {
                        log += $"\n\t{missingDetour}";
                    }

                    Log.Info(log);

                    if (GlobalConfig.Instance.Main.ShowCompatibilityCheckErrorMessage) {
                        Prompt.Error("TM:PE Incompatibility Issue", error);
                    }
                }
            }

            if (Options.timedLightsEnabled) {
                tlsMan.SimulationStep();
            }
        }

        // public override void OnAfterSimulationFrame() {
        //        base.OnAfterSimulationFrame();
        //
        //        routeMan.SimulationStep();
        //
        //        ++ticksSinceLastMinuteUpdate;
        //        if (ticksSinceLastMinuteUpdate > 60 * 60) {
        //            ticksSinceLastMinuteUpdate = 0;
        //            GlobalConfig.Instance.SimulationStep();
        // #if DEBUG
        //            DebugMenuPanel.PrintTransportStats();
        // #endif
        //        }
        // }

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta) {
            base.OnUpdate(realTimeDelta, simulationTimeDelta);

            using (var bm = CSUtil.Commons.Benchmark.Benchmark.MaybeCreateBenchmark()) {
                if (ToolsModifierControl.toolController == null || ModUI.Instance == null) {
                    return;
                }

                TrafficManagerTool tmTool = ModUI.GetTrafficManagerTool(false);
                if (tmTool != null && ToolsModifierControl.toolController.CurrentTool != tmTool &&
                    ModUI.Instance.IsVisible()) {
                    ModUI.Instance.CloseMainMenu();
                }

                if (Input.GetKeyDown(KeyCode.Escape)) {
                    ModUI.Instance.CloseMainMenu();
                }
            } // end benchmark
        }
    } // end class
}