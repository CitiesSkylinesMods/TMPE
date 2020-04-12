namespace TrafficManager.UI.SubTools.LaneArrows {
    using System;
    using System.Collections.Generic;
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U;
    using TrafficManager.UI.MainMenu;
    using TrafficManager.Util;
    using UnityEngine;
    using static TrafficManager.Util.Shortcuts;
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// LaneArrow Tool creates ToolWindow for lane arrow buttons.
    /// </summary>
    public class LaneArrowTool : TrafficManagerSubTool {
        /// <summary>
        /// Finite State machine for the tool. Represents current UI state for Lane Arrows.
        /// </summary>
        private Util.GenericFsm<State, Trigger> fsm_;

        /// <summary>Tool states.</summary>
        private enum State {
            /// <summary>Waiting for user to select a half segment.</summary>
            Select,

            /// <summary>Showing GUI for the lane arrows and letting user click.</summary>
            EditLaneArrows,

            /// <summary>The tool is switched off.</summary>
            ToolDisabled,
        }

        /// <summary>Events which trigger state transitions.</summary>
        private enum Trigger {
            /// <summary>A segment was hovered and clicked.</summary>
            SegmentClick,

            /// <summary>Right mouse has been clicked.</summary>
            RightMouseClick,

            /// <summary>
            /// Escape has been pressed (this is handled by the MainMenu or ModUI code so might as
            /// well be a candidate for removal).
            /// </summary>
            EscapeKey,
        }

        /// <summary>If exists, contains tool panel floating on the selected node.</summary>
        private LaneArrowToolWindow ToolWindow { get; set; }

        public LaneArrowTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            fsm_ = new Util.GenericFsm<State, Trigger>(State.Select);
        }

        private static string T(string key) {
            return Translation.LaneRouting.Get(key);
        }

        /// <summary>
        /// Creates FSM ready to begin editing. Or recreates it when ESC is pressed
        /// and the tool is canceled.
        /// </summary>
        /// <returns>The new FSM in the initial state.</returns>
        private Util.GenericFsm<State, Trigger> InitFiniteStateMachine() {
            var fsm = new Util.GenericFsm<State, Trigger>(State.Select);

            // From Select mode, user can either click a segment, or Esc/rightclick to quit
            fsm.Configure(State.Select)
               .OnEntry(this.OnEnterSelectState)
               .OnLeave(this.OnLeaveSelectState)
               .TransitionOnEvent(Trigger.SegmentClick, State.EditLaneArrows)
               .TransitionOnEvent(Trigger.RightMouseClick, State.ToolDisabled)
               .TransitionOnEvent(Trigger.EscapeKey, State.ToolDisabled);

            fsm.Configure(State.EditLaneArrows)
               .OnEntry(this.OnEnterEditorState)
               .OnLeave(this.OnLeaveEditorState)
               .TransitionOnEvent(Trigger.SegmentClick, State.EditLaneArrows)
               .TransitionOnEvent(Trigger.RightMouseClick, State.Select);
            // This transition is ignored because Esc disables the tool
            //   .TransitionOnEvent(Trigger.EscapeKey, State.Select);

            fsm.Configure(State.ToolDisabled)
               .OnEntry(
                   () => {
                       // We are done here, leave the tool.
                       // This will result in this.DeactivateTool being called.
                       // MainTool.SetToolMode(ToolMode.None);
                       ModUI.Instance.MainMenu.ClickToolButton(ToolMode.LaneArrows);
                   });

            return fsm;
        }

        private void OnLeaveSelectState() {
            OnScreenDisplay.Clear();
        }

        private void OnEnterSelectState() {
            SelectedNodeId = 0;
            SelectedSegmentId = 0;
            MainTool.RequestOnscreenDisplayUpdate();
        }

        /// <summary>Called from GenericFsm when a segment is clicked to show lane arrows GUI.</summary>
        private void OnEnterEditorState() {
            int numLanes = GeometryUtil.GetSegmentNumVehicleLanes(
                SelectedSegmentId,
                SelectedNodeId,
                out int numDirections,
                LaneArrowManager.VEHICLE_TYPES);

            if (numLanes <= 0) {
                SelectedNodeId = 0;
                SelectedSegmentId = 0;
                return;
            }

            // Vector3 nodePos = Singleton<NetManager>
            //                   .instance.m_nodes.m_buffer[SelectedNodeId].m_position;
            //
            // // Hide if node position is off-screen
            //
            // bool visible = GeometryUtil.WorldToScreenPoint(nodePos, out Vector3 screenPos);
            //
            // // if (!visible) {
            //     // return;
            // // }
            //
            // Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
            // Vector3 diff = nodePos - camPos;
            //
            // if (diff.sqrMagnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE_SQR) {
            //     return; // do not draw if too distant
            // }
            // Calculate lanes and arrows
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

            CreateLaneArrowsWindow(laneList.Count);
            SetupLaneArrowsWindowButtons(laneList: laneList,
                                         startNode: (bool)startNode);
            MainTool.RequestOnscreenDisplayUpdate();
        }

        /// <summary>
        /// Creates floating tool window for Lane Arrow controls and adds keyboard hints too.
        /// </summary>
        /// <param name="numLanes">How many groups of buttons.</param>
        private void CreateLaneArrowsWindow(int numLanes) {
            var parent = UIView.GetAView();
            ToolWindow = (LaneArrowToolWindow)parent.AddUIComponent(typeof(LaneArrowToolWindow));
            ToolWindow.SetTransparency(GlobalConfig.Instance.Main.GuiTransparency);

            RepositionWindowToNode(); // reposition 1st time to avoid visible window jump

            using (var builder = new U.UiBuilder<LaneArrowToolWindow>(ToolWindow)) {
                builder.ResizeFunction(r => { r.FitToChildren(); });
                builder.SetPadding(UConst.UIPADDING);

                ToolWindow.SetupControls(builder, numLanes);

                // On Delete being pressed
                ToolWindow.eventKeyDown += (component, param) => {
                    if (KeybindSettingsBase.LaneConnectorDelete.IsPressed(param)) {
                        OnResetToDefaultPressed();
                    }
                };

                // Resize everything correctly
                // builder.Control.SetTransparency(GlobalConfig.Instance.Main.GuiTransparency);
                builder.Done();
                RepositionWindowToNode(); // reposition again 2nd time now that size is known
            }
        }

        /// <summary>
        /// Given the tool window already created with its buttons set up,
        /// go through them and assign click events, disable some, activate some etc.
        /// </summary>
        private void SetupLaneArrowsWindowButtons(IList<LanePos> laneList, bool startNode) {
            // For all lanes, go through our buttons and update their onClick, etc.
            for (var i = 0; i < laneList.Count; i++) {
                uint laneId = laneList[i].laneId;

                LaneArrowButton buttonLeft = ToolWindow.Buttons[i * 3];
                buttonLeft.LaneId = laneId;
                buttonLeft.NetlaneFlagsMask = NetLane.Flags.Left;
                buttonLeft.StartNode = startNode;
                buttonLeft.ToggleFlag = API.Traffic.Enums.LaneArrows.Left;
                buttonLeft.UpdateButtonImageAndTooltip();
                buttonLeft.ParentTool = this; // to access error reporting function on click

                LaneArrowButton buttonForward = ToolWindow.Buttons[(i * 3) + 1];
                buttonForward.LaneId = laneId;
                buttonForward.NetlaneFlagsMask = NetLane.Flags.Forward;
                buttonForward.StartNode = startNode;
                buttonForward.ToggleFlag = API.Traffic.Enums.LaneArrows.Forward;
                buttonForward.UpdateButtonImageAndTooltip();
                buttonForward.ParentTool = this; // to access error reporting function on click

                LaneArrowButton buttonRight = ToolWindow.Buttons[(i * 3) + 2];
                buttonRight.LaneId = laneId;
                buttonRight.NetlaneFlagsMask = NetLane.Flags.Right;
                buttonRight.StartNode = startNode;
                buttonRight.ToggleFlag = API.Traffic.Enums.LaneArrows.Right;
                buttonRight.UpdateButtonImageAndTooltip();
                buttonRight.ParentTool = this; // to access error reporting function on click
            }
        }

        private void UpdateAllButtons() {
            // For all lanes, go through our buttons and update states
            foreach (LaneArrowButton b in ToolWindow.Buttons) {
                b.UpdateButtonImageAndTooltip();
            }
        }

        /// <summary>Called from GenericFsm when user leaves lane arrow editor, to hide the GUI.</summary>
        private void OnLeaveEditorState() {
            OnScreenDisplay.Clear();
            DestroyToolWindow();
        }

        private void DestroyToolWindow() {
            if (ToolWindow) {
                UnityEngine.Object.Destroy(ToolWindow);
                ToolWindow = null;
            }
        }

        /// <summary>
        /// if the segment has at least one lane without outgoing lane connections, then it can be reset.
        /// </summary>
        /// <returns>true if the segemnt can be reset.</returns>
        private static bool CanReset(ushort segmentId, bool startNode) {
            foreach (var lanePos in netService.GetSortedLanes(
                segmentId,
                ref GetSeg(segmentId),
                startNode,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES)) {
                if (!LaneConnectionManager.Instance.HasConnections(lanePos.laneId)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Resets tool into its initial state for new use.</summary>
        public override void ActivateTool() {
            Log._Debug("LaneArrow: Activated tool");
            fsm_ = InitFiniteStateMachine();
            this.OnEnterSelectState(); // FSM does not call enter on initial state
        }

        /// <summary>Cleans up when tool is deactivated or user switched to another tool.</summary>
        public override void DeactivateTool() {
            Log._Debug("LaneArrow: Deactivated tool");
            DestroyToolWindow();
            SelectedNodeId = 0;
            SelectedSegmentId = 0;
            fsm_ = null;
        }

        /// <summary>
        /// If Lane Arrow operation ended with failure, pop up a guide box with an explanation.
        /// </summary>
        /// <param name="result">Result coming out of LaneArrowManager function call.</param>
        internal void InformUserAboutPossibleFailure(SetLaneArrow_Result result) {
            switch (result) {
                case SetLaneArrow_Result.HighwayArrows: {
                        MainTool.Guide.Activate("LaneArrowTool_Disabled due to highway rules");
                        break;
                    }
                case SetLaneArrow_Result.LaneConnection: {
                        MainTool.Guide.Activate("LaneArrowTool_Disabled due to lane connections");
                        break;
                    }
                case SetLaneArrow_Result.Success:
                    MainTool.Guide.Deactivate("LaneArrowTool_Disabled due to highway rules");
                    MainTool.Guide.Deactivate("LaneArrowTool_Disabled due to lane connections");
                    break;
            }
        }

        /// <summary>Called from the Main Tool when left mouse button clicked.</summary>
        public override void OnToolLeftClick() {
            if (ToolWindow != null && MainTool.GetToolController().IsInsideUI) {
                return; // ignore clicks landing into some UI, only consume map clicks
            }

            Log._Debug($"LaneArrow({fsm_.State}): left click");
            switch (fsm_.State) {
                case State.Select:
                    OnToolLeftClick_Select();
                    break;
                case State.EditLaneArrows:
                    if (HoveredSegmentId != 0) {
                        // Allow selecting other segments while doing lane editing
                        // fsm_.SendTrigger(Trigger.ReenterSameState);
                        OnToolLeftClick_Select();
                    }
                    break;
            }
        }

        private void OnToolLeftClick_Select() {
            if (HoveredNodeId == 0 || HoveredSegmentId == 0) {
                return;
            }

            // Clicked on something which was hovered
            NetNode.Flags netFlags =
                Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags;

            // Not interested in clicking anything other than a junction
            if ((netFlags & NetNode.Flags.Junction) == NetNode.Flags.None) {
                return;
            }

            // Not interested in segments which don't start at the hovered node
            NetSegment[] segmentsBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;

            if (segmentsBuffer[HoveredSegmentId].m_startNode != HoveredNodeId &&
                segmentsBuffer[HoveredSegmentId].m_endNode != HoveredNodeId) {
                return;
            }

            bool ctrlDown = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool altDown = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (altDown) {
                // Holding Alt will set separate lanes for selected segment
                LaneArrowManager.SeparateTurningLanes.SeparateSegmentLanes(
                    HoveredSegmentId,
                    HoveredNodeId,
                    out var res);
                InformUserAboutPossibleFailure(res);
            } else if (ctrlDown) {
                // Holding Ctrl will set separate lanes for node
                LaneArrowManager.SeparateTurningLanes.SeparateNode(
                    HoveredNodeId,
                    out var res);
                InformUserAboutPossibleFailure(res);
            } else if (HasHoverLaneArrows()) {
                SelectedSegmentId = HoveredSegmentId;
                SelectedNodeId = HoveredNodeId;
                fsm_.SendTrigger(Trigger.SegmentClick);
            }
        }

        /// <summary>Called from the Main Tool when right mouse button clicked.</summary>
        public override void OnToolRightClick() {
            // FSM will either cancel the edit mode, or switch off the tool.
            SelectedSegmentId = 0;
            SelectedNodeId = 0;
            State oldState = State.ToolDisabled;

            if (fsm_ != null) {
                oldState = fsm_.State;
                fsm_.SendTrigger(Trigger.RightMouseClick);
            }

            // Right click might reset fsm_ to null, so check again
            if (fsm_ != null) {
                Log._Debug($"LaneArrow right click state={oldState}, new={fsm_.State}");
            } else {
                Log._Debug($"LaneArrow(fsm=null): right click");
            }
        }

        public override void UpdateEveryFrame() {
            // The following code only works if tool window exists and state is when we edit arrows
            if (fsm_ == null ||
                fsm_.State != State.EditLaneArrows)
            {
                return;
            }

            RepositionWindowToNode();
        }

        /// <summary>
        /// Called from the <see cref="TrafficManagerTool"/> when update for the Keybinds panel
        /// in MainMenu is requested. Or when we need to change state.
        /// Never call this directly, only as: MainTool.RequestOnscreenDisplayUpdate();
        /// </summary>
        public override void UpdateOnscreenDisplayPanel() {
            if (fsm_ == null) {
                OnScreenDisplay.Clear();
                return;
            }

            switch (fsm_.State) {
                case State.Select: {
                    OnScreenDisplay.Begin();
                    OnScreenDisplay.Click(
                        shift: false,
                        ctrl: true,
                        alt: false,
                        localizedText: T("LaneArrows.Click:Separate lanes for entire junction"));
                    OnScreenDisplay.Click(
                        shift: false,
                        ctrl: false,
                        alt: true,
                        localizedText: T("LaneArrows.Click:Separate lanes for segment"));
                    OnScreenDisplay.Done();
                    return;
                }
                case State.EditLaneArrows: {
                    OnScreenDisplay.Begin();
                    OnScreenDisplay.Shortcut(
                        kbSetting: KeybindSettingsBase.LaneConnectorDelete,
                        localizedText: T("LaneConnector.Label:Reset to default"));
                    OnScreenDisplay.Done();
                    return;
                }
            }
            OnScreenDisplay.Clear();
        }

        private void RepositionWindowToNode() {
            if (ToolWindow == null || SelectedNodeId == 0) {
                return;
            }

            Vector3 nodePos = Singleton<NetManager>
                              .instance.m_nodes.m_buffer[SelectedNodeId].m_position;

            // Cast to screen and center the window on node
            GeometryUtil.WorldToScreenPoint(nodePos, out Vector3 screenPixelPos);
            Vector2 guiPosition = UIScaler.ScreenPointToGuiPoint(screenPixelPos);
            ToolWindow.absolutePosition =
                guiPosition - new Vector2(ToolWindow.size.x * 0.5f, ToolWindow.size.y * 0.5f);
        }

        /// <summary>
        /// Determines whether or not the hovered segment end has lane arrows.
        /// </summary>
        private bool HasHoverLaneArrows() => HasSegmentEndLaneArrows(HoveredSegmentId, HoveredNodeId);

        /// <summary>
        /// Determines whether or not the given segment end has lane arrows.
        /// </summary>
        private bool HasSegmentEndLaneArrows(ushort segmentId, ushort nodeId) {
            if(nodeId == 0 || segmentId == 0) {
                return false;
            }
#if DEBUG
            if(!Constants.ServiceFactory.NetService.IsNodeValid(nodeId) ||
               !Constants.ServiceFactory.NetService.IsSegmentValid(segmentId)) {
                Debug.LogError("Invalid node or segment ID");
            }
#endif
            ExtSegmentEndManager segEndMan = ExtSegmentEndManager.Instance;
            int segmentEndId = segEndMan.GetIndex(segmentId, nodeId);
            if (segmentEndId <0) {
                Log._Debug($"Node {nodeId} is not connected to segment {segmentId}");
                return false;
            }
            ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];
            NetNode[] nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            bool bJunction = (nodesBuffer[nodeId].m_flags & NetNode.Flags.Junction) != 0;

            // Outgoing lanes toward the node is incomming lanes to the segment end.
            return bJunction && segEnd.incoming;
        }

        protected override ushort HoveredNodeId {
        get {
                bool ctrlDown = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (ctrlDown) {
                    // When control is down, we are selecting node.
                    return base.HoveredNodeId;
                }
                // if the current segment end does not have lane arrows
                // and the other end of the segment does have lane arrows, then
                // assume the user intends to hover over that one.
                // This code makes it easier to hover over small segments.
                if (!HasSegmentEndLaneArrows(HoveredSegmentId, base.HoveredNodeId))
                {
                    ref NetSegment segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId];
                    ushort otherNodeId = segment.GetOtherNode(base.HoveredNodeId);
                    if (HasSegmentEndLaneArrows(HoveredSegmentId, otherNodeId)) {
                        return otherNodeId;
                    }
                }
                return base.HoveredNodeId;
            }
        }

        /// <summary>
        /// Draws a half sausage to highlight the segment end.
        /// </summary>
        private void DrawSegmentEnd(
                       RenderManager.CameraInfo cameraInfo,
                       ushort segmentId,
                       bool bStartNode,
                       Color color,
                       bool alpha = false) {
            ref NetSegment segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];

            // if only one side of the segment has lane arrows then the length of the
            // is 1. but the highlight still looks like a sausage which is cut at one end.
            // this is important to give user visual feedback which area is hoverable.
            bool con =
                HasSegmentEndLaneArrows(segmentId, segment.m_startNode) ^
                HasSegmentEndLaneArrows(segmentId, segment.m_endNode);
            float cut = con ? 1f : 0.5f;

            MainTool.DrawCutSegmentEnd(cameraInfo, segmentId, cut, bStartNode, color, alpha);
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            switch (fsm_.State) {
                case State.Select:
                    RenderOverlay_Select(cameraInfo);
                    break;
                case State.EditLaneArrows:
                    RenderOverlay_Select(cameraInfo); // show potential half-segments to select
                    RenderOverlay_EditLaneArrows(cameraInfo);
                    break;
            }
        }

        /// <summary>Render info overlay for active tool, when UI is in Select state.</summary>
        /// <param name="cameraInfo">The camera.</param>
        private void RenderOverlay_Select(RenderManager.CameraInfo cameraInfo) {
            NetManager netManager = Singleton<NetManager>.instance;

            bool ctrlDown = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            // If CTRL is held, and hovered something: Draw hovered node
            if (ctrlDown && HoveredNodeId != 0) {
                MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId, Input.GetMouseButton(0));
                return;
            }

            bool altDown = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool leftMouseDown = Input.GetMouseButton(0);

            // Log._Debug($"LaneArrow Overlay: {HoveredNodeId} {HoveredSegmentId} {SelectedNodeId} {SelectedSegmentId}");

            // If hovered something which has hover lane arrows, and it is not the same as selected
            // Then: Draw alternative selection variant with different color, to be clicked by the user
            if (HasHoverLaneArrows()
                && HoveredSegmentId != 0
                && HoveredNodeId != 0
                && (HoveredSegmentId != SelectedSegmentId
                    || HoveredNodeId != SelectedNodeId))
            {
                NetNode.Flags nodeFlags = netManager.m_nodes.m_buffer[HoveredNodeId].m_flags;

                if ((netManager.m_segments.m_buffer[HoveredSegmentId].m_startNode == HoveredNodeId
                     || netManager.m_segments.m_buffer[HoveredSegmentId].m_endNode == HoveredNodeId)
                    && (nodeFlags & NetNode.Flags.Junction) != NetNode.Flags.None)
                {
                    bool bStartNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(HoveredSegmentId, HoveredNodeId);
                    Color color = MainTool.GetToolColor(leftMouseDown, false);
                    bool alpha = !altDown;
                    DrawSegmentEnd(cameraInfo, HoveredSegmentId, bStartNode, color, alpha);
                }
            }
        }

        /// <summary>Render info overlay for active tool, when UI is in Edit Lane Arrows state.</summary>
        /// <param name="cameraInfo">The camera.</param>
        private void RenderOverlay_EditLaneArrows(RenderManager.CameraInfo cameraInfo) {
            bool altDown = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (SelectedSegmentId != 0) {
                Color color = MainTool.GetToolColor(true, false);
                bool bStartNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(SelectedSegmentId, SelectedNodeId);
                bool alpha = !altDown && HoveredSegmentId == SelectedSegmentId;
                DrawSegmentEnd(cameraInfo, SelectedSegmentId, bStartNode, color, alpha);
            }
        }

        private void OnResetToDefaultPressed() {
            bool? startNode = Constants.ServiceFactory.NetService.IsStartNode(SelectedSegmentId, SelectedNodeId);
            if (!CanReset(SelectedSegmentId, (bool)startNode)) {
                return;
            }

            Log._Debug(
                "deleting lane arrows: " +
                $"SelectedSegmentId={SelectedSegmentId} SelectedNodeId={SelectedNodeId} startNode={startNode}");
            LaneArrowManager.Instance.ResetLaneArrows(SelectedSegmentId, startNode);

            // Update button states
            this.UpdateAllButtons();
        }
    }
}
