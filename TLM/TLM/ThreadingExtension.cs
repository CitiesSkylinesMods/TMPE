namespace TrafficManager {
    using ColossalFramework.UI;
    using ColossalFramework;
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

            if (Options.timedLightsEnabled) {
                tlsMan.SimulationStep();
            }
        }

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta) {
            base.OnUpdate(realTimeDelta, simulationTimeDelta);

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
        }
    } // end class
}