namespace TrafficManager.UI.SubTools {
    using System.Collections.Generic;
    using API.Traffic.Enums;
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using Manager.Impl;
    using State;
    using UnityEngine;

    public class LaneArrowTool : SubTool {
        private bool cursorInSecondaryPanel_;

        public LaneArrowTool(TrafficManagerTool mainTool)
            : base(mainTool) { }

        public override bool IsCursorInPanel() {
            return base.IsCursorInPanel() || cursorInSecondaryPanel_;
        }

        public override void OnPrimaryClickOverlay() {
            if ((HoveredNodeId == 0) || (HoveredSegmentId == 0)) return;

            NetNode.Flags netFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags;

            if ((netFlags & NetNode.Flags.Junction) == NetNode.Flags.None) {
                return;
            }

            NetSegment[] segmentsBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;

            if ((segmentsBuffer[HoveredSegmentId].m_startNode != HoveredNodeId) &&
                (segmentsBuffer[HoveredSegmentId].m_endNode != HoveredNodeId)) {
                return;
            }

            bool ctrlDown = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool altDown = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (altDown) {
                LaneArrowManager.Instance.SeparateSegmentLanes(HoveredSegmentId, HoveredNodeId);
                return;
            } else if (ctrlDown) {
                LaneArrowManager.Instance.SeparateNode(HoveredNodeId);
                return;
            }
            SelectedSegmentId = HoveredSegmentId;
            SelectedNodeId = HoveredNodeId;
        }

        public override void OnSecondaryClickOverlay() {
            if (!IsCursorInPanel()) {
                SelectedSegmentId = 0;
                SelectedNodeId = 0;
            }
        }

        public override void OnToolGUI(Event e) {
            // base.OnToolGUI(e);
            cursorInSecondaryPanel_ = false;

            if ((SelectedNodeId == 0) || (SelectedSegmentId == 0)) return;

            int numLanes = TrafficManagerTool.GetSegmentNumVehicleLanes(
                SelectedSegmentId,
                SelectedNodeId,
                out int numDirections,
                LaneArrowManager.VEHICLE_TYPES);

            if (numLanes <= 0) {
                SelectedNodeId = 0;
                SelectedSegmentId = 0;
                return;
            }

            Vector3 nodePos = Singleton<NetManager>
                              .instance.m_nodes.m_buffer[SelectedNodeId].m_position;

            bool visible = MainTool.WorldToScreenPoint(nodePos, out Vector3 screenPos);

            if (!visible) {
                return;
            }

            Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
            Vector3 diff = nodePos - camPos;

            if (diff.sqrMagnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE_SQR) {
                return; // do not draw if too distant
            }

            int width = numLanes * 128;
            var windowRect3 = new Rect(screenPos.x - (width / 2), screenPos.y - 70, width, 50);
            GUILayout.Window(250, windowRect3, GuiLaneChangeWindow, string.Empty, BorderlessStyle);
            cursorInSecondaryPanel_ = windowRect3.Contains(Event.current.mousePosition);
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            NetManager netManager = Singleton<NetManager>.instance;

            bool ctrlDown = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool altDown = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (ctrlDown) {
                // draw hovered node
                MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId, Input.GetMouseButton(0));
                return;
            }

            // Log._Debug($"LaneArrow Overlay: {HoveredNodeId} {HoveredSegmentId} {SelectedNodeId} {SelectedSegmentId}");
            if (!cursorInSecondaryPanel_
                && (HoveredSegmentId != 0)
                && (HoveredNodeId != 0)
                && ((HoveredSegmentId != SelectedSegmentId)
                    || (HoveredNodeId != SelectedNodeId)))
            {
                NetNode.Flags nodeFlags = netManager.m_nodes.m_buffer[HoveredNodeId].m_flags;

                if (((netManager.m_segments.m_buffer[HoveredSegmentId].m_startNode == HoveredNodeId)
                     || (netManager.m_segments.m_buffer[HoveredSegmentId].m_endNode == HoveredNodeId))
                    && ((nodeFlags & NetNode.Flags.Junction) != NetNode.Flags.None))
                {
                    NetTool.RenderOverlay(
                        cameraInfo,
                        ref Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId],
                        MainTool.GetToolColor(altDown, false),
                        MainTool.GetToolColor(altDown, false));
                }
            }

            if (SelectedSegmentId == 0) return;

            Color color;
            if (altDown && HoveredSegmentId == SelectedSegmentId)
                color = MainTool.GetToolColor(true, true);
            else
                color = MainTool.GetToolColor(true, false);
            NetTool.RenderOverlay(
                cameraInfo,
                ref Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId],
                color,
                color);
        }

        private void GuiLaneChangeWindow(int num) {
            NetSegment[] segmentsBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            IList<LanePos> laneList = Constants.ServiceFactory.NetService.GetSortedLanes(
                SelectedSegmentId,
                ref segmentsBuffer[SelectedSegmentId],
                segmentsBuffer[SelectedSegmentId].m_startNode == SelectedNodeId,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES,
                true);

            bool? startNode = Constants.ServiceFactory.NetService.IsStartNode(SelectedSegmentId, SelectedNodeId);
            if (startNode == null) {
                Log.Error(
                    $"LaneArrowTool._guiLaneChangeWindow: Segment {SelectedSegmentId} " +
                    $"is not connected to node {SelectedNodeId}");
                return;
            }

            GUILayout.BeginHorizontal();

            for (var i = 0; i < laneList.Count; i++) {
                var flags = (NetLane.Flags)Singleton<NetManager>
                                           .instance.m_lanes.m_buffer[laneList[i].laneId].m_flags;

                var style1 = new GUIStyle("button");
                var style2 = new GUIStyle("button") {
                    normal = { textColor = new Color32(255, 0, 0, 255) },
                    hover = { textColor = new Color32(255, 0, 0, 255) },
                    focused = { textColor = new Color32(255, 0, 0, 255) }
                };

                var laneStyle = new GUIStyle { contentOffset = new Vector2(12f, 0f) };

                var laneTitleStyle = new GUIStyle {
                    contentOffset = new Vector2(36f, 2f),
                    normal = { textColor = new Color(1f, 1f, 1f) }
                };

                GUILayout.BeginVertical(laneStyle);
                GUILayout.Label(
                    Translation.LaneRouting.Get("Format.Label:Lane") + " " + (i + 1),
                    laneTitleStyle);
                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();

                if (!Flags.ApplyLaneArrowFlags(laneList[i].laneId)) {
                    Flags.RemoveLaneArrowFlags(laneList[i].laneId);
                }

                SetLaneArrowError res = SetLaneArrowError.Invalid;
                bool buttonClicked = false;

                if (GUILayout.Button(
                    "←",
                    ((flags & NetLane.Flags.Left) == NetLane.Flags.Left ? style1 : style2),
                    GUILayout.Width(35),
                    GUILayout.Height(25))) {
                    buttonClicked = true;
                    LaneArrowManager.Instance.ToggleLaneArrows(
                        laneList[i].laneId,
                        (bool)startNode,
                        LaneArrows.Left,
                        out res);
                }

                if (GUILayout.Button(
                    "↑",
                    ((flags & NetLane.Flags.Forward) == NetLane.Flags.Forward ? style1 : style2),
                    GUILayout.Width(25),
                    GUILayout.Height(35))) {
                    buttonClicked = true;
                    LaneArrowManager.Instance.ToggleLaneArrows(
                        laneList[i].laneId,
                        (bool)startNode,
                        LaneArrows.Forward,
                        out res);
                }

                if (GUILayout.Button(
                    "→",
                    ((flags & NetLane.Flags.Right) == NetLane.Flags.Right ? style1 : style2),
                    GUILayout.Width(35),
                    GUILayout.Height(25))) {
                    buttonClicked = true;
                    LaneArrowManager.Instance.ToggleLaneArrows(
                        laneList[i].laneId,
                        (bool)startNode,
                        LaneArrows.Right,
                        out res);
                }

                if (buttonClicked) {
                    switch (res) {
                        case SetLaneArrowError.HighwayArrows: {
                            MainTool.ShowError(
                                Translation.LaneRouting.Get("Dialog.Text:Disabled due to highway rules"));
                            break;
                        }

                        case SetLaneArrowError.LaneConnection: {
                            MainTool.ShowError(
                                Translation.LaneRouting.Get("Dialog.Text:Disabled due to manual connection"));
                            break;
                        }
                    }
                }

                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();
        }
    }
}
