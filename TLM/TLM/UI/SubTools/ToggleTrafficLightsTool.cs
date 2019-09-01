namespace TrafficManager.UI.SubTools {
    using API.Traffic.Enums;
    using Manager.Impl;
    using State;
    using UnityEngine;

    public class ToggleTrafficLightsTool : SubTool {
        public ToggleTrafficLightsTool(TrafficManagerTool mainTool)
            : base(mainTool) { }

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
                                Translation.TrafficLights.Get("Error.Node has timed TL script"));
                            break;
                        }

                        case ToggleTrafficLightError.IsLevelCrossing: {
                            MainTool.ShowError(
                                Translation.TrafficLights.Get("Error.Node is level crossing"));
                            break;
                        }
                    }
                }

                return;
            }

            TrafficPriorityManager.Instance.RemovePrioritySignsFromNode(nodeId);
            TrafficLightManager.Instance.ToggleTrafficLight(nodeId, ref node);
        }

        public override void OnToolGUI(Event e) { }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            if (MainTool.GetToolController().IsInsideUI || !Cursor.visible) {
                return;
            }

            if (HoveredNodeId == 0) {
                return;
            }

            if (!Flags.mayHaveTrafficLight(HoveredNodeId)) {
                return;
            }

            MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId, Input.GetMouseButton(0), false);
        }
    }
}