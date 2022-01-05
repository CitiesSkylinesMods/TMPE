namespace TrafficManager.UI.SubTools {
    using System.Collections.Generic;
    using ColossalFramework;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.MainMenu.OSD;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util;
    using TrafficManager.Util.Caching;
    using TrafficManager.Util.Extensions;
    using UnityEngine;

    public class ToggleTrafficLightsTool
        : LegacySubTool,
          UI.MainMenu.IOnscreenDisplayProvider
    {
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

            ToggleTrafficLight(HoveredNodeId, ref HoveredNodeId.ToNode());
        }

        public override void OnSecondaryClickOverlay() {
            MainTool.SetToolMode(ToolMode.None);
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
                            MainTool.WarningPrompt(
                                Translation.TrafficLights.Get("Dialog.Text:Node has timed TL script"));
                            break;
                        }

                        case ToggleTrafficLightError.IsLevelCrossing: {
                            MainTool.WarningPrompt(
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
            var textures = TrafficLightTextures.Instance;

            //--------------------------------
            // Render all visible node states
            //--------------------------------
            for (int cacheIndex = CachedVisibleNodeIds.Size - 1; cacheIndex >= 0; cacheIndex--) {
                var nodeId = CachedVisibleNodeIds.Values[cacheIndex];
                ref NetNode netNode = ref nodeId.ToNode();

                // Check whether there is a traffic light and CAN be any at all
                Texture2D overlayTex;
                if (TrafficLightSimulationManager.Instance.HasTimedSimulation(nodeId)) {
                    overlayTex = textures.TrafficLightEnabledTimed;
                } else
                if (TrafficLightManager.Instance.HasTrafficLight(
                    nodeId,
                    ref netNode)) {
                    // Render traffic light icon
                    overlayTex = textures.TrafficLightEnabled;
                } else {
                    // Render traffic light possible but disabled icon
                    overlayTex = textures.TrafficLightDisabled;
                }

                Highlight.DrawGenericOverlayTexture(
                    texture: overlayTex,
                    camPos: camPos,
                    worldPos: netNode.m_position,
                    width: SIGN_SIZE,
                    height: SIGN_SIZE,
                    canHover: false,
                    screenRect: out _);
            }
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            if (MainTool.GetToolController().IsInsideUI || !Cursor.visible) {
                return;
            }

            // For current camera store its position and cast a ray via mouse position
            Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;

            // Check if camera pos/angle has changed then re-filter the visible nodes
            // Assumption: The states checked in this loop don't change while the tool is active
            var currentCameraState = new CameraTransformValue(InGameUtil.Instance.CachedMainCamera);
            if (!LastCachedCamera.Equals(currentCameraState)) {
                CachedVisibleNodeIds.Clear();
                LastCachedCamera = currentCameraState;

                FilterVisibleNodes(camPos);
            }

            // Render the current hovered node as blue
            if ((HoveredNodeId != 0) && Flags.MayHaveTrafficLight(HoveredNodeId)) {
                Highlight.DrawNodeCircle(
                    cameraInfo: cameraInfo,
                    nodeId: HoveredNodeId,
                    warning: Input.GetMouseButton(0),
                    alpha: false);
            }
        }

        /// <summary>
        /// For all nodes find those which are potentially visible, and not too far from the camera
        /// </summary>
        /// <param name="camPos">Position of the camera</param>
        private void FilterVisibleNodes(Vector3 camPos) {
            for (ushort nodeId = 1; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
                ref NetNode netNode = ref nodeId.ToNode();

                if (!netNode.IsValid()) {
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
                ItemClass connectionClass = netNode.Info.GetConnectionClass();

                if ((connectionClass == null) ||
                    (connectionClass.m_service != ItemClass.Service.Road)) {
                    continue;
                }

                //--------------------------
                // Check the camera distance
                //--------------------------
                Vector3 diff = netNode.m_position - camPos;

                if (diff.sqrMagnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE_SQR) {
                    continue; // do not draw if too distant
                }

                // Add
                CachedVisibleNodeIds.Add(nodeId);
            }
        }

        private static string T(string key) => Translation.TrafficLights.Get(key);

        public void UpdateOnscreenDisplayPanel() {
            var items = new List<OsdItem>();
            items.Add(new Label(localizedText: T("ToggleTL.Mode:Click to toggle")));
            OnscreenDisplay.Display(items);
        }

        public override void OnActivate() {
            base.OnActivate();
            MainTool.RequestOnscreenDisplayUpdate();
        }
    }
}
