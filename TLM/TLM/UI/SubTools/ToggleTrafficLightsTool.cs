namespace TrafficManager.UI.SubTools {
    using ColossalFramework;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util.Caching;
    using UnityEngine;

    public class ToggleTrafficLightsTool : SubTool {
        /// <summary>
        /// Stores potentially visible ids for nodes while the camera did not move
        /// </summary>
        private GenericArrayCache<ushort> CachedVisibleNodeIds { get; }

        /// <summary>
        /// Stores last cached camera position in <see cref="CachedVisibleNodeIds"/>
        /// </summary>
        private CameraTransformValue LastCachedCamera { get; set; }

        /// <summary>
        /// Size of the traffic light icon.
        /// </summary>
        private const float SIGN_SIZE = 64f;

        public ToggleTrafficLightsTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            CachedVisibleNodeIds = new GenericArrayCache<ushort>(NetManager.MAX_NODE_COUNT);
            LastCachedCamera = new CameraTransformValue();
        }

        public override void OnPrimaryClickOverlay() {
            if (IsCursorInPanel()) {
                return;
            }

            if (HoveredNodeId == 0) {
                return;
            }

            Constants.ServiceFactory.NetService.ProcessNode(
                HoveredNodeId,
                (ushort nId, ref NetNode node) => {
                    ToggleTrafficLight(HoveredNodeId, ref node);
                    return true;
                });
        }

        public void ToggleTrafficLight(ushort nodeId,
                                       ref NetNode node,
                                       bool showMessageOnError = true) {
            ToggleTrafficLightError reason;
            if (!TrafficLightManager.Instance.CanToggleTrafficLight(
                    nodeId,
                    !TrafficLightManager.Instance.HasTrafficLight(
                        nodeId,
                        ref node),
                    ref node,
                    out reason))
            {
                if (showMessageOnError) {
                    switch (reason) {
                        case ToggleTrafficLightError.HasTimedLight: {
                            MainTool.ShowError(
                                Translation.TrafficLights.Get("Dialog.Text:Node has timed TL script"));
                            break;
                        }

                        case ToggleTrafficLightError.IsLevelCrossing: {
                            MainTool.ShowError(
                                Translation.TrafficLights.Get("Dialog.Text:Node is level crossing"));
                            break;
                        }
                    }
                }

                return;
            }

            TrafficPriorityManager.Instance.RemovePrioritySignsFromNode(nodeId);
            TrafficLightManager.Instance.ToggleTrafficLight(nodeId, ref node);
        }

        public override void OnToolGUI(Event e) {
            Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
            NetManager netManager = Singleton<NetManager>.instance;
            NetNode[] nodesBuffer = netManager.m_nodes.m_buffer;

            //--------------------------------
            // Render all visible node states
            //--------------------------------
            for (int cacheIndex = CachedVisibleNodeIds.Size - 1; cacheIndex >= 0; cacheIndex--) {
                var nodeId = CachedVisibleNodeIds.Values[cacheIndex];

                // Check whether there is a traffic light and CAN be any at all
                Texture2D overlayTex;
                if (TrafficLightSimulationManager.Instance.HasTimedSimulation(nodeId)) {
                    overlayTex = TrafficLightTextures.TrafficLightEnabledTimed;
                } else
                if (TrafficLightManager.Instance.HasTrafficLight(
                    nodeId,
                    ref nodesBuffer[nodeId])) {
                    // Render traffic light icon
                    overlayTex = TrafficLightTextures.TrafficLightEnabled;
                } else {
                    // Render traffic light possible but disabled icon
                    overlayTex = TrafficLightTextures.TrafficLightDisabled;
                }

                MainTool.DrawGenericOverlayTexture(
                    overlayTex,
                    camPos,
                    nodesBuffer[nodeId].m_position,
                    SIGN_SIZE,
                    SIGN_SIZE,
                    false);
            }
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            if (MainTool.GetToolController().IsInsideUI || !Cursor.visible) {
                return;
            }

            // For current camera store its position and cast a ray via mouse position
            Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
            Camera currentCamera = Camera.main;
            // Ray mouseRay = currentCamera.ScreenPointToRay(Input.mousePosition);

            // Check if camera pos/angle has changed then re-filter the visible nodes
            // Assumption: The states checked in this loop don't change while the tool is active
            var currentCameraState = new CameraTransformValue(currentCamera);
            if (!LastCachedCamera.Equals(currentCameraState)) {
                CachedVisibleNodeIds.Clear();
                LastCachedCamera = currentCameraState;

                FilterVisibleNodes(camPos);
            }

            // Render the current hovered node as blue
            if ((HoveredNodeId != 0) && Flags.MayHaveTrafficLight(HoveredNodeId)) {
                MainTool.DrawNodeCircle(
                    cameraInfo,
                    HoveredNodeId,
                    Input.GetMouseButton(0),
                    false);
            }
        }

        /// <summary>
        /// For all nodes find those which are potentially visible, and not too far from the camera
        /// </summary>
        /// <param name="camPos">Position of the camera</param>
        private void FilterVisibleNodes(Vector3 camPos) {
            for (ushort nodeId = 1; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
                if (!Constants.ServiceFactory.NetService.IsNodeValid(nodeId)) {
                    continue;
                }

                //---------------------------------------
                // If cannot have a traffic light at all
                //---------------------------------------
                if (!Flags.MayHaveTrafficLight(nodeId)) {
                    continue;
                }

                //--------------------------------------------
                // Only allow traffic lights on normal roads, not rail or metro etc.
                //--------------------------------------------
                ItemClass connectionClass =
                    NetManager.instance.m_nodes.m_buffer[nodeId].Info.GetConnectionClass();

                if ((connectionClass == null) ||
                    (connectionClass.m_service != ItemClass.Service.Road)) {
                    continue;
                }

                //--------------------------
                // Check the camera distance
                //--------------------------
                Vector3 diff = NetManager.instance.m_nodes.m_buffer[nodeId].m_position - camPos;

                if (diff.sqrMagnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE_SQR) {
                    continue; // do not draw if too distant
                }

                // Add
                CachedVisibleNodeIds.Add(nodeId);
            }
        }
    }
}
