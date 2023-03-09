namespace TrafficManager.UI.SubTools.LaneArrows {
    using System.Collections.Generic;
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Manager.Impl.LaneConnection;
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.MainMenu;
    using TrafficManager.UI.MainMenu.OSD;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using UnityEngine;
    using static TrafficManager.Util.Shortcuts;
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// LaneArrow Tool creates ToolWindow for lane arrow buttons.
    /// </summary>
    public class LaneArrowTool
        : TrafficManagerSubTool,
          IOnscreenDisplayProvider
    {
        const bool DEFAULT_ALT_MODE = true;
        private bool alternativeMode_ = DEFAULT_ALT_MODE;
        private int framesSeparateTurningLanesModeActivated = 0;

        public LaneArrowTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            fsm_ = new Util.GenericFsm<State, Trigger>(State.Select);
        }

        bool SeparateSegmentLanesModifierIsPressed => AltIsPressed;
        bool SeparateNodeLanesModifierIsPressed => ControlIsPressed;

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

        private static string T(string key) => Translation.LaneRouting.Get(key);

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
            OnscreenDisplay.Clear();
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

            // Vector3 nodePos = SelectedNodeId.ToNode().m_position;
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

            ref NetSegment selectedSegment = ref SelectedSegmentId.ToSegment();

            bool? startNode = selectedSegment.GetRelationToNode(SelectedNodeId);

            if (!startNode.HasValue) {
                Log.Error(
                    $"LaneArrowTool._guiLaneChangeWindow: Segment {SelectedSegmentId} " +
                    $"is not connected to node {SelectedNodeId}");
                return;
            }

            var sortedLanes = selectedSegment.GetSortedLanes(
                startNode.Value,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES,
                reverse: true);

            CreateLaneArrowsWindow(sortedLanes.Count);
            ref ExtSegmentEnd segmentEnd = ref ExtSegmentEndManager.Instance.GetEnd(SelectedSegmentId, startNode.Value);
            SetupLaneArrowsWindowButtons(laneList: sortedLanes,
                                         startNode: startNode.Value,
                                         segmentEnd.laneArrows);
            MainTool.RequestOnscreenDisplayUpdate();
        }

        /// <summary>
        /// Creates floating tool window for Lane Arrow controls and adds keyboard hints too.
        /// </summary>
        /// <param name="numLanes">How many groups of buttons.</param>
        private void CreateLaneArrowsWindow(int numLanes) {
            var builder = UBuilder.Create(
                abAtlasName: "TMPE_LaneArrowsTool_Atlas",
                abLoadingPath: "LaneArrows",
                abSizeHint: new IntVector2(256));

            ToolWindow = builder.CreateWindow<LaneArrowToolWindow>();
            RepositionWindowToNode(); // reposition 1st time to avoid visible window jump

            ToolWindow.SetOpacity(
                UOpacityValue.FromOpacity(0.01f * GlobalConfig.Instance.Main.GuiOpacity));
            ToolWindow.SetupControls(builder, numLanes);

            // Resize everything correctly
            ToolWindow.ForceUpdateLayout();
            RepositionWindowToNode(); // reposition again 2nd time now that size is known
        }

        /// <summary>
        /// Given the tool window already created with its buttons set up,
        /// go through them and assign click events, disable some, activate some etc.
        /// </summary>
        private void SetupLaneArrowsWindowButtons(IList<LanePos> laneList, bool startNode, LaneArrows availableArrows) {
            // For all lanes, go through our buttons and update their onClick, etc.
            for (var i = 0; i < laneList.Count; i++) {
                uint laneId = laneList[i].laneId;

                LaneArrowButton buttonLeft = ToolWindow.Buttons[i * 3];
                buttonLeft.LaneId = laneId;
                buttonLeft.NetlaneFlagsMask = NetLane.Flags.Left;
                buttonLeft.StartNode = startNode;
                buttonLeft.ToggleFlag = API.Traffic.Enums.LaneArrows.Left;
                buttonLeft.UpdateButtonSkinAndTooltip();
                buttonLeft.ParentTool = this; // to access error reporting function on click
                bool leftAllowed =  (availableArrows & LaneArrows.Left) != 0;
                buttonLeft.isEnabled = leftAllowed;
                buttonLeft.tooltip = !leftAllowed ? T("LaneArrows: Direction not available") : string.Empty;

                LaneArrowButton buttonForward = ToolWindow.Buttons[(i * 3) + 1];
                buttonForward.LaneId = laneId;
                buttonForward.NetlaneFlagsMask = NetLane.Flags.Forward;
                buttonForward.StartNode = startNode;
                buttonForward.ToggleFlag = API.Traffic.Enums.LaneArrows.Forward;
                buttonForward.UpdateButtonSkinAndTooltip();
                buttonForward.ParentTool = this; // to access error reporting function on click
                bool forwardAllowed =  (availableArrows & LaneArrows.Forward) != 0;
                buttonForward.isEnabled = forwardAllowed;
                buttonForward.tooltip = !forwardAllowed ? T("LaneArrows: Direction not available") : string.Empty;

                LaneArrowButton buttonRight = ToolWindow.Buttons[(i * 3) + 2];
                buttonRight.LaneId = laneId;
                buttonRight.NetlaneFlagsMask = NetLane.Flags.Right;
                buttonRight.StartNode = startNode;
                buttonRight.ToggleFlag = API.Traffic.Enums.LaneArrows.Right;
                buttonRight.UpdateButtonSkinAndTooltip();
                buttonRight.ParentTool = this; // to access error reporting function on click
                bool rightAllowed =  (availableArrows & LaneArrows.Right) != 0;
                buttonRight.isEnabled = rightAllowed;
                buttonRight.tooltip = !rightAllowed ? T("LaneArrows: Direction not available") : string.Empty;
            }
        }

        private void UpdateAllButtons() {
            // For all lanes, go through our buttons and update states
            foreach (LaneArrowButton b in ToolWindow.Buttons) {
                b.UpdateButtonSkinAndTooltip();
            }
        }

        /// <summary>Called from GenericFsm when user leaves lane arrow editor, to hide the GUI.</summary>
        private void OnLeaveEditorState() {
            OnscreenDisplay.Clear();
            DestroyToolWindow();
        }

        private void DestroyToolWindow() {
            if (ToolWindow) {
                UnityEngine.Object.Destroy(ToolWindow.gameObject);
                ToolWindow = null;
            }
        }

        /// <summary>
        /// if the segment has at least one lane without outgoing lane connections, then it can be reset.
        /// </summary>
        /// <returns>true if the segment can be reset.</returns>
        private static bool CanReset(ushort segmentId, bool startNode) {

            ref NetSegment segment = ref segmentId.ToSegment();

            var lanes = segment.GetSortedLanes(
                startNode,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES,
                sort: false);

            foreach (var lane in lanes) {
                if (!LaneConnectionManager.Instance.Road.HasOutgoingConnections(lane.laneId))
                    return true;
            }

            return false;
        }

        /// <summary>Resets tool into its initial state for new use.</summary>
        public override void OnActivateTool() {
            Log._Debug("LaneArrow: Activated tool");
            fsm_ = InitFiniteStateMachine();
            this.OnEnterSelectState(); // FSM does not call enter on initial state
        }

        /// <summary>Cleans up when tool is deactivated or user switched to another tool.</summary>
        public override void OnDeactivateTool() {
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
            NetNode.Flags netFlags = HoveredNodeId.ToNode().m_flags;

            // Not interested in clicking anything other than a junction
            if ((netFlags & NetNode.Flags.Junction) == NetNode.Flags.None) {
                return;
            }

            // Not interested in segments which don't start at the hovered node
            ref NetSegment hoveredSegment = ref HoveredSegmentId.ToSegment();
            if (hoveredSegment.m_startNode != HoveredNodeId && hoveredSegment.m_endNode != HoveredNodeId) {
                return;
            }

            if (Time.frameCount - framesSeparateTurningLanesModeActivated > 80) {
                // the mode resets after 2 seconds.
                alternativeMode_ = DEFAULT_ALT_MODE;
            }

            if (SeparateSegmentLanesModifierIsPressed) {
                SeparateTurningLanesUtil.SeparateSegmentLanes(
                    HoveredSegmentId, HoveredNodeId, out var res, alternativeMode: alternativeMode_);
                InformUserAboutPossibleFailure(res);
            } else if (SeparateNodeLanesModifierIsPressed) {
                SeparateTurningLanesUtil.SeparateNode(HoveredNodeId, out var res, alternativeMode: alternativeMode_);
                InformUserAboutPossibleFailure(res);
            } else if (HasHoverLaneArrows()) {
                SelectedSegmentId = HoveredSegmentId;
                SelectedNodeId = HoveredNodeId;
                alternativeMode_ = DEFAULT_ALT_MODE;
                fsm_.SendTrigger(Trigger.SegmentClick);
            }

            if (SeparateSegmentLanesModifierIsPressed || SeparateNodeLanesModifierIsPressed) {
                framesSeparateTurningLanesModeActivated = Time.frameCount;
                alternativeMode_ = !alternativeMode_;
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

            if (Event.current.type == EventType.KeyDown && KeybindSettingsBase.RestoreDefaultsKey.IsPressed(Event.current)) {
                OnResetToDefaultPressed();
            }

            RepositionWindowToNode();
        }

        /// <summary>
        /// Called from the <see cref="TrafficManagerTool"/> when update for the Keybinds panel
        /// in MainMenu is requested. Or when we need to change state.
        /// Never call this directly, only as: MainTool.RequestOnscreenDisplayUpdate();
        /// </summary>
        void IOnscreenDisplayProvider.UpdateOnscreenDisplayPanel() {
            if (fsm_ == null) {
                OnscreenDisplay.Clear();
                return;
            }

            switch (fsm_.State) {
                case State.Select: {
                    var items = new List<OsdItem>();
                    items.Add(
                        new MainMenu.OSD.Label(
                            localizedText: T("LaneArrows.Mode:Select")));
                    items.Add(
                        new MainMenu.OSD.HardcodedMouseShortcut(
                            button: UIMouseButton.Left,
                            shift: false,
                            ctrl: true,
                            alt: false,
                            localizedText: T("LaneArrows.Click:Separate lanes for entire junction")));
                    items.Add(
                        new MainMenu.OSD.HardcodedMouseShortcut(
                            button: UIMouseButton.Left,
                            shift: false,
                            ctrl: false,
                            alt: true,
                            localizedText: T("LaneArrows.Click:Separate lanes for segment")));
                    OnscreenDisplay.Display(items: items);
                    return;
                }
                case State.EditLaneArrows: {
                    var items = new List<OsdItem>();
                    items.Add(
                        item: new MainMenu.OSD.Shortcut(
                            keybindSetting: KeybindSettingsBase.RestoreDefaultsKey,
                            localizedText: T(key: "LaneConnector.Label:Reset to default")));
                    items.Add(item: OnscreenDisplay.RightClick_LeaveSegment());
                    OnscreenDisplay.Display(items: items);
                    return;
                }
                default: {
                    OnscreenDisplay.Clear();
                    return;
                }
            }
        }

        private void RepositionWindowToNode() {
            if (!ToolWindow || SelectedNodeId == 0) {
                return;
            }

            ToolWindow.MoveCenterToWorldPosition(SelectedNodeId.ToNode().m_position);
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
            ref NetSegment netSegment = ref segmentId.ToSegment();
            ref NetNode netNode = ref nodeId.ToNode();

#if DEBUG
            if (!netNode.IsValid() ||
               !netSegment.IsValid()) {
                Debug.LogError("Invalid node or segment ID");
            }
#endif
            ExtSegmentEndManager segEndMan = ExtSegmentEndManager.Instance;
            int segmentEndId = segEndMan.GetIndex(segmentId, nodeId);
            if (segmentEndId < 0) {
                Log._Debug($"Node {nodeId} is not connected to segment {segmentId}");
                return false;
            }
            ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];
            bool bJunction = (nodeId.ToNode().m_flags & NetNode.Flags.Junction) != 0;

            // Outgoing lanes toward the node is incoming lanes to the segment end.
            return bJunction && segEnd.incoming;
        }

        protected override ushort HoveredNodeId {
        get {
                if (SeparateNodeLanesModifierIsPressed) {
                    // When control is down, we are selecting node.
                    return base.HoveredNodeId;
                }
                // if the current segment end does not have lane arrows
                // and the other end of the segment does have lane arrows, then
                // assume the user intends to hover over that one.
                // This code makes it easier to hover over small segments.
                if (!HasSegmentEndLaneArrows(HoveredSegmentId, base.HoveredNodeId))
                {
                    ushort otherNodeId = HoveredSegmentId.ToSegment().GetOtherNode(base.HoveredNodeId);
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
            ref NetSegment segment = ref segmentId.ToSegment();

            // if only one side of the segment has lane arrows then the length of the
            // is 1. but the highlight still looks like a sausage which is cut at one end.
            // this is important to give user visual feedback which area is hoverable.
            bool con =
                HasSegmentEndLaneArrows(segmentId, segment.m_startNode) ^
                HasSegmentEndLaneArrows(segmentId, segment.m_endNode);
            float cut = con ? 1f : 0.5f;

            Highlight.DrawCutSegmentEnd(cameraInfo, segmentId, cut, bStartNode, color, alpha);
        }

        public override void RenderActiveToolOverlay(RenderManager.CameraInfo cameraInfo) {
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

        public override void RenderActiveToolOverlay_GUI() {
            // No GUI-specific info overlay for lane arrows
        }

        /// <summary>No generic overlay for other tool modes is provided by this tool, render nothing.</summary>
        public override void RenderGenericInfoOverlay(RenderManager.CameraInfo cameraInfo) {
            // No info overlay for other tools
        }

        public override void RenderGenericInfoOverlay_GUI() {
            // No GUI-specific info overlay to show while other tools active
        }

        /// <summary>Render info overlay for active tool, when UI is in Select state.</summary>
        /// <param name="cameraInfo">The camera.</param>
        private void RenderOverlay_Select(RenderManager.CameraInfo cameraInfo) {
            // If CTRL is held, and hovered something: Draw hovered node
            if (SeparateNodeLanesModifierIsPressed && HoveredNodeId != 0) {
                Highlight.DrawNodeCircle(
                    cameraInfo: cameraInfo,
                    nodeId: HoveredNodeId,
                    warning: Input.GetMouseButton(0));
                return;
            }

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
                NetNode.Flags nodeFlags = HoveredNodeId.ToNode().m_flags;
                ref NetSegment hoveredSegment = ref HoveredSegmentId.ToSegment();

                if ((hoveredSegment.m_startNode == HoveredNodeId || hoveredSegment.m_endNode == HoveredNodeId)
                    && (nodeFlags & NetNode.Flags.Junction) != NetNode.Flags.None)
                {
                    bool bStartNode = hoveredSegment.IsStartNode(HoveredNodeId);
                    Color color = MainTool.GetToolColor(leftMouseDown, false);
                    bool alpha = !SeparateSegmentLanesModifierIsPressed;
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
                bool startNode = SelectedSegmentId.ToSegment().IsStartNode(SelectedNodeId);
                bool alpha = !altDown && HoveredSegmentId == SelectedSegmentId;
                DrawSegmentEnd(cameraInfo, SelectedSegmentId, startNode, color, alpha);
            }
        }

        private void OnResetToDefaultPressed() {
            bool startNode = SelectedSegmentId.ToSegment().IsStartNode(SelectedNodeId);
            if (!CanReset(SelectedSegmentId, startNode)) {
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
