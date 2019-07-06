namespace TrafficManager.UI.SubTools {
    using Manager;
    using Manager.Impl;
    using State;
    using UnityEngine;

    public class ToggleTrafficLightsTool : SubTool {
        public ToggleTrafficLightsTool(TrafficManagerTool mainTool) : base(mainTool) {

        }

        public override void OnPrimaryClickOverlay() {
            if (IsCursorInPanel())
                return;
            if (HoveredNodeId == 0)
                return;

            Constants.ServiceFactory.NetService.ProcessNode(HoveredNodeId, delegate (ushort nId, ref NetNode node) {
                ToggleTrafficLight(HoveredNodeId, ref node);
                return true;
            });
        }

        public void ToggleTrafficLight(ushort nodeId, ref NetNode node, bool showMessageOnError=true) {
            UnableReason reason;
            if (!TrafficLightManager.Instance.IsTrafficLightToggleable(nodeId, !TrafficLightManager.Instance.HasTrafficLight(nodeId, ref node), ref node, out reason)) {
                if (showMessageOnError) {
                    switch (reason) {
                        case UnableReason.HasTimedLight:
                            MainTool.ShowTooltip(Translation.GetString("NODE_IS_TIMED_LIGHT"));
                            break;
                        case UnableReason.IsLevelCrossing:
                            MainTool.ShowTooltip(Translation.GetString("Node_is_level_crossing"));
                            break;
                        default:
                            break;
                    }
                }
                return;
            }

            TrafficPriorityManager.Instance.RemovePrioritySignsFromNode(nodeId);
            TrafficLightManager.Instance.ToggleTrafficLight(nodeId, ref node);
        }

        public override void OnToolGUI(Event e) {

        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            if (MainTool.GetToolController().IsInsideUI || !Cursor.visible) {
                return;
            }

            if (HoveredNodeId == 0) return;

            if (!Flags.mayHaveTrafficLight(HoveredNodeId)) return;

            MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId, Input.GetMouseButton(0), false);
        }
    }
}