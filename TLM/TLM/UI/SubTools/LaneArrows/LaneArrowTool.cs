namespace TrafficManager.UI.SubTools.LaneArrows {
    using System;
    using System.Collections.Generic;
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using global::TrafficManager.Geometry.Impl;
    using Manager.Impl;
    using State;
    using UnityEngine;
    using Util;

    public partial class LaneArrowTool : SubTool {
        private const int MAX_NODE_SEGMENTS = 8;

        private enum State {
            NodeSelect,         // click a node to edit
            IncomingSelect,     // click an incoming lane to edit
            OutgoingDirections, // click approximate direction to allow turns
            OutgoingLanes,      // click each allowed lane to toggle
            Off
        }

        /// <summary>
        /// Events which trigger state transitions
        /// </summary>
        private enum Trigger {
            NodeClick,
            SegmentClick,
            LaneClick,
            RightMouseClick,
        }

        /// <summary>
        /// State machine for the tool, see state graph in
        /// https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/pull/391#issuecomment-508410325
        /// </summary>
        private GenericFsm<State, Trigger> fsm_;

        /// <summary>
        /// For selected node, stores lanes incoming to that node
        /// </summary>
        private HashSet<uint> incomingLanes_;

        /// <summary>
        /// Allowed outgoing turns grouped by the direction
        /// </summary>
        private OutgoingTurnsCollection outgoingTurns_;

//        /// <summary>
//        /// Allowed outgoing lanes
//        /// </summary>
//        private HashSet<uint> outgoingLanes_;

        public LaneArrowTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            fsm_ = InitFsm();
        }

        /// <summary>
        /// Creates FSM ready to begin editing. Or recreates it when ESC is pressed
        /// and the tool is canceled.
        /// </summary>
        /// <returns>The new FSM in the initial state.</returns>
        private GenericFsm<State, Trigger> InitFsm() {
            var fsm = new GenericFsm<State, Trigger>(State.NodeSelect);

            // From Node Select mode, user can either click a node, or right click
            // to quit the tool
            fsm.Configure(State.NodeSelect)
                .OnEntry(() => { SelectedNodeId = 0; })
                .Permit(Trigger.NodeClick, State.IncomingSelect)
                .Permit(Trigger.RightMouseClick, State.Off);

            // From Incoming Select the user can click another node, or click a lane
            // or a segment, or right click to leave back to node select
            fsm.Configure(State.IncomingSelect)
                .OnEntry(OnEnterState_IncomingSelect)
                .Permit(Trigger.NodeClick, State.IncomingSelect)
                .Permit(Trigger.LaneClick, State.OutgoingDirections)
                .Permit(Trigger.RightMouseClick, State.NodeSelect);

            // In Outgoing Select the user can click an outgoing lane or an outgoing
            // segment to apply routing, or right click to return to Incoming Select,
            // or click another node
            fsm.Configure(State.OutgoingDirections)
               .OnEntry(OnEnterState_OutgoingDirections)
                .Permit(Trigger.RightMouseClick, State.IncomingSelect)
                .Permit(Trigger.NodeClick, State.IncomingSelect);

            return fsm;
        }

        /// <summary>
        /// This is linked in the ctor to be called when FSM enters Incoming Select
        /// state, and sets up the on-screen display to see the incoming lanes
        /// to the clicked node.
        /// </summary>
        private void OnEnterState_IncomingSelect() {
            SelectedLaneId = 0;
            incomingLanes_ = GetAllIncomingLanes(SelectedNodeId);
        }

        /// <summary>
        /// This is linked in the ctor to be called when FSM enters Outgoing Select
        /// state, and sets up possible outgoing lanes, and also extracts current state
        /// of the allowed outgoing lanes.
        /// </summary>
        private void OnEnterState_OutgoingDirections() {
            outgoingTurns_ = GetOutgoingTurns(SelectedNodeId, SelectedSegmentId);

            // Some sanity check?
            if (!Flags.applyLaneArrowFlags(SelectedLaneId)) {
                Flags.removeLaneArrowFlags(SelectedLaneId);
            }

//            var segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
//            var segment = segmentBuffer[SelectedSegmentId];
//            var otherNodeId = SelectedNodeId == segment.m_startNode
//                                  ? segment.m_endNode : segment.m_startNode;
//
//            outgoingLanes_ = new HashSet<uint>();
//            foreach (var ln in GetIncomingLaneList(SelectedSegmentId, otherNodeId)) {
//                outgoingLanes_.Add(ln.laneId);
//            }
        }

        /// <summary>
        /// Return true if cursor is in TM:PE menu or over any world-space UI control
        /// </summary>
        /// <returns>Cursor is over some UI</returns>
        public override bool IsCursorInPanel() {
            // True if cursor is inside TM:PE GUI or in any of the world space
            // GUIs that we have
            return base.IsCursorInPanel() || IsCursorInAnyLaneEditor();
        }

        /// <summary>
        /// Return whether mouse is over some part of the tool UI.
        /// </summary>
        /// <returns>Cursor is in some tool UI</returns>
        private bool IsCursorInAnyLaneEditor() {
            return false;
        }

        private static bool IsNodeEditable(ushort nodeId) {
            // TODO: Other node types? Basically check if the node has some incoming and some outgoing lanes
            var netFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags;
            const NetNode.Flags MASK = NetNode.Flags.Junction;
//                                       | NetNode.Flags.AsymBackward
//                                       | NetNode.Flags.AsymForward
//                                       | NetNode.Flags.Transition
                                       // Also allow middle and bend, to control the road flow
//                                       | NetNode.Flags.Bend | NetNode.Flags.Middle;
            return (netFlags & MASK) != NetNode.Flags.None;
        }

        public override void OnToolGUI(Event e) {
            switch (fsm_.State) {
                case State.NodeSelect:
                    OnToolGUI_NodeSelect();
                    break;
                case State.IncomingSelect:
                    OnToolGUI_IncomingSelect();
                    break;
                case State.OutgoingDirections:
                    OnToolGUI_OutgoingDirections();
                    break;
                case State.Off:
                    // Do nothing
                    break;
            }
        }

        /// <summary>
        /// Handle events for NodeSelect state
        /// </summary>
        private void OnToolGUI_NodeSelect() {
            // No GUI interaction in this mode
        }

        /// <summary>
        /// Handle events for IncomingSelect state
        /// </summary>
        private void OnToolGUI_IncomingSelect() {
            // No GUI interaction in this mode
        }

        // Cancel selection immediately if there are no vehicle lanes
        // var numLanes = TrafficManagerTool.GetSegmentNumVehicleLanes(
        //     SelectedSegmentId, SelectedNodeId, out _, LaneArrowManager.VEHICLE_TYPES);
        // if (numLanes <= 0) {
        //     Deselect();
        //     return;
        // }

//            var nodePos = Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId].m_position;
//            var visible = MainTool.WorldToScreenPoint(nodePos, out _);
//            if (!visible) {
//                return;
//            }

//            var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
//            var diff = nodePos - camPos;
//            if (diff.magnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE) {
//                return; // do not draw if too distant
//            }

//            // Try click something on Canvas
//            foreach (var laneEditor in laneEditors_.Values) {
//                if (laneEditor.Gui.HandleInput()) {
//                    break; // handle until first true is returned (consumed)
//                }
//            }

        /// <summary>
        /// Handle events for OutgoingDirections state
        /// </summary>
        private void OnToolGUI_OutgoingDirections() {
            // No GUI interaction in this mode
        }

        /// <summary>
        /// User right-clicked or other reasons, the selection is cleared
        /// </summary>
        private void Deselect() {
            fsm_ = InitFsm();
            SelectedSegmentId = 0;
            SelectedNodeId = 0;
            SelectedLaneId = 0;
        }

//        /// <summary>
//        /// Create canvas one per incoming segment to the selected node.
//        /// Fill each canvas with lane buttons.
//        /// The initial GUI state is one lane control button per each incoming lane.
//        /// Clicking that lane button will produce 0..3 arrow buttons controlling that lane.
//        /// Clicking another lane button will destroy these and create another 0..3 buttons.
//        /// Right-clicking, or clicking another node will destroy the GUI.
//        /// </summary>
//        private void CreateEditorsForAllSegments() {
//            var nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
//            var segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
//            var selectedNode = nodeBuffer[SelectedNodeId];
//            var nodeId = SelectedNodeId;
//
//            // For all incoming segments
//            for (var i = 0; i < MAX_NODE_SEGMENTS; ++i) {
//                var incomingSegmentId = selectedNode.GetSegment(i);
//                if (incomingSegmentId == 0) {
//                    continue;
//                }
//
//                var incomingSegment = segmentBuffer[incomingSegmentId];
//
//                // Create the GUI form and center it according to the node position
//                Quaternion rotInverse;
//                Vector3 guiOriginWorldPos;
//                var editor = new LaneArrowsEditor();
//                editor.CreateWorldSpaceCanvas(
//                    incomingSegment, nodeId, incomingSegmentId, out guiOriginWorldPos, out rotInverse);
//                laneEditors_[incomingSegmentId] = editor;
//
//                if (LaneConnectionManager.Instance.HasNodeConnections(nodeId)) {
//                    // Do not allow editing lanes, because custom connections exist
//                    // Create a forbidden icon button which does not click
//                    CreateForbiddenButton_LaneConnections();
//                    return; // do not create any more buttons
//                }
//
//                var laneList = GetIncomingLaneList(incomingSegmentId, nodeId);
//                CreateLaneControlButtons(editor, nodeId, incomingSegmentId, laneList,
//                                         rotInverse, guiOriginWorldPos);
//            }
//        }

//        /// <summary>
//        /// As now we know that lane arrows cannot be edited, we do not create lane control GUI,
//        /// instead create a single button with a warning icon. Clicking this button
//        /// will pop up an explanation message.
//        /// </summary>
//        private void CreateForbiddenButton_LaneConnections() {
//            var gui = laneEditors_.First().Value.Gui; // exact 1 element in laneEditors
//            var button = gui.AddButton(
//                new Vector3(0f, 0f, 0f),
//                new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE),
//                string.Empty);
//            WorldSpaceGUI.SetButtonSprite(button, LaneArrowsTextures.GetSprite(3, 2));
//
//            UnityAction clickLaneConnections = () => {
//                const string MESSAGE = "Lane arrows disabled for this node, because you have custom " +
//                                       "lane connections set up.";
//                UIView.library
//                      .ShowModal<ExceptionPanel>("ExceptionPanel")
//                      .SetMessage("No Lane Arrows for this node", MESSAGE, false);
//            };
//            button.GetComponent<Button>().onClick.AddListener(clickLaneConnections);
//        }

//        /// <summary>
//        /// The lane editing is possible.
//        /// Create buttons one per lane.
//        /// </summary>
//        /// <param name="editor">The editor containing canvas for these buttons</param>
//        /// <param name="nodeId">Node being the center of the junction and all editors</param>
//        /// <param name="segmentId">Segment we are editing in this canvas</param>
//        /// <param name="laneList">List of lanes to create buttons for them</param>
//        /// <param name="rotInverse">The inverse rotation quaternion for projecting world stuff into the canvas</param>
//        /// <param name="guiOriginWorldPos">Where GUI 0,0 is located in the world</param>
//        private void CreateLaneControlButtons(LaneArrowsEditor editor,
//                                              ushort nodeId,
//                                              ushort segmentId,
//                                              IList<LanePos> laneList,
//                                              Quaternion rotInverse,
//                                              Vector3 guiOriginWorldPos) {
//            var lanesBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;
//
//            var geometry = SegmentGeometry.Get(segmentId);
//            if (geometry == null) {
//                Log.Error("LaneArrowTool._guiLaneChangeWindow: No geometry information " +
//                          $"available for segment {segmentId}");
//                return;
//            }
//
//            var isStartNode = geometry.StartNodeId() == nodeId;
//
//            // Now iterate over eligible lanes
//            for (var i = 0; i < laneList.Count; i++) {
//                var laneId = laneList[i].laneId;
//                var lane = lanesBuffer[laneId];
//                var laneFlags = (NetLane.Flags)lane.m_flags;
//
//                if (!Flags.applyLaneArrowFlags(laneList[i].laneId)) {
//                    Flags.removeLaneArrowFlags(laneList[i].laneId);
//                }
//
//                // Get position of the editable lane
//                var laneEndPosition = lane.m_bezier.Position(isStartNode ? 0f : 1f);
//                var buttonPositionRot = rotInverse * (laneEndPosition - guiOriginWorldPos);
//
//                // because UI grows up (away from the node), shift it slightly back in by 3 button sizes
//                // TODO: Get the distance (junction size) from netSegment.Info and step back by that
//                var buttonPosition = new Vector3(buttonPositionRot.x,
//                                                 buttonPositionRot.z - (LANE_BUTTON_SIZE * 3f),
//                                                 0f);
//
//                var laneEditButton = editor.Gui.AddButton(
//                    buttonPosition, new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE));
//
//                WorldSpaceGUI.SetButtonSprite(
//                    laneEditButton, LaneArrowsTextures.GetLaneControlSprite(laneFlags));
//
//                laneEditButton.GetComponent<Button>().onClick.AddListener(
//                    () => {
//                        // Ignore second click on the same control button
//                        if (laneEditButton == sharedState_.btnCurrentControlButton_) {
//                            return;
//                        }
//
//                        SelectedSegmentId = segmentId;
//                        SelectedLaneId = laneId;
//                        try {
//                            CreateLaneArrowButtons(
//                                laneEditButton, segmentId, SelectedNodeId, laneId, isStartNode);
//                        }
//                        catch (Exception e) {
//                            Log.Error($"While creating lane arrows: {e}");
//                        }
//                    });
//
//                // TODO: Fix this if only one incoming lane available in the whole junction? or delete this
////                if (laneList.Count == 1) {
////                    // For only one lane, immediately open the arrow buttons
////                    sharedState_.selectedLaneId_ = laneId;
////                    CreateLaneControlArrows(laneEditButton, segmentId, SelectedNodeId, laneId, isStartNode);
////                }
//            }
//        }

//        /// <summary>
//        /// Create world space canvas in the ground plane.
//        /// Recreate Lane Arrows at the location of the given button and slightly above it.
//        /// </summary>
//        /// <param name="originButton">The button user clicked to create these controls</param>
//        /// <param name="segmentId">Current segment being edited</param>
//        /// <param name="nodeId">Current junction</param>
//        /// <param name="laneId">Current lane being edited</param>
//        /// <param name="isStartNode">Bool if selected node is start node of the geometry (used for
//        ///     lane modifications later)</param>
//        private void CreateLaneArrowButtons(GameObject originButton,
//                                            ushort segmentId,
//                                            ushort nodeId,
//                                            uint laneId,
//                                            bool isStartNode) {
//            // Set some nice color or effect to highlight
//            originButton.GetComponent<Image>().color = Color.gray;
//
//            var lane = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId];
//            var flags = (NetLane.Flags)lane.m_flags;
//
//            sharedState_.DestroyLaneArrowButtons();
//            sharedState_.btnCurrentControlButton_ = originButton; // save this to decolorize it later
//
//            // Get all possible turn directions to leave the nodeId via this segment
//            var editor = laneEditors_[segmentId];
//            editor.PossibleTurns = GetAllTurnsOut(nodeId, segmentId);
//
//            //-----------------
//            // Button FORWARD
//            //-----------------
//            var forward = (flags & NetLane.Flags.Forward) != 0 ? LaneButtonState.On : LaneButtonState.Off;
//            if (editor.PossibleTurns != null
//                && editor.PossibleTurns.Value.Contains(ArrowDirection.Forward)) {
//                GuiAddLaneArrowForward(editor, originButton, forward);
//                UnityAction clickForward = () => {
//                    OnClickForward(laneId, isStartNode, sharedState_.btnLaneArrowForward_);
//                };
//
//                var buttonComponent = sharedState_.btnLaneArrowForward_.GetComponent<Button>();
//                buttonComponent.onClick.AddListener(clickForward);
////            } else {
////                GuiAddLaneArrowForward(originButton, LaneButtonState.Disabled);
//                // Note: no click handler added
//            }
//
//            //-----------------
//            // Button LEFT
//            //-----------------
//            var left = (flags & NetLane.Flags.Left) != 0 ? LaneButtonState.On : LaneButtonState.Off;
//            if (editor.PossibleTurns != null
//                && editor.PossibleTurns.Value.Contains(ArrowDirection.Left)) {
//                GuiAddLaneArrowLeft(editor, originButton, left);
//                UnityAction clickLeft = () => {
//                    OnClickLeft(segmentId, SelectedNodeId, laneId,
//                                isStartNode, sharedState_.btnLaneArrowLeft_);
//                };
//                var buttonComponent = sharedState_.btnLaneArrowLeft_.GetComponent<Button>();
//                buttonComponent.onClick.AddListener(clickLeft);
////            } else {
////                GuiAddLaneArrowLeft(originButton, LaneButtonState.Disabled);
//                // Note: no click handler added
//            }
//
//            //-----------------
//            // Button RIGHT
//            //-----------------
//            var right = (flags & NetLane.Flags.Right) != 0 ? LaneButtonState.On : LaneButtonState.Off;
//            if (editor.PossibleTurns != null
//                && editor.PossibleTurns.Value.Contains(ArrowDirection.Right)) {
//                GuiAddLaneArrowRight(editor, originButton, right);
//                UnityAction clickRight = () => {
//                    OnClickRight(segmentId, SelectedNodeId, laneId,
//                                 isStartNode, sharedState_.btnLaneArrowRight_);
//                };
//                var buttonComponent = sharedState_.btnLaneArrowRight_.GetComponent<Button>();
//                buttonComponent.onClick.AddListener(clickRight);
////            } else {
////                GuiAddLaneArrowRight(originButton, LaneButtonState.Disabled);
//                // Note: no click handler added
//            }
//        }

//        private void UpdateButtonGraphics(uint laneId,
//                                          NetLane.Flags direction,
//                                          GameObject button) {
//            var lane = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId];
//            var flags = (NetLane.Flags)lane.m_flags;
//            var buttonState = (flags & direction) != 0 ? LaneButtonState.On : LaneButtonState.Off;
//            WorldSpaceGUI.SetButtonSprite(button, SelectControlButtonSprite(direction, buttonState));
//        }

        internal static IList<LanePos> GetIncomingLaneList(ushort segmentId, ushort nodeId) {
            var segmentsBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            return Constants.ServiceFactory.NetService.GetSortedLanes(
                segmentId,
                ref segmentsBuffer[segmentId],
                segmentsBuffer[segmentId].m_startNode == nodeId,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES,
                true);
        }

//        private void OnClickForward(uint laneId, bool startNode, GameObject button) {
//            var res = Flags.LaneArrowChangeResult.Invalid;
//            LaneArrowManager.Instance.ToggleLaneArrows(
//                laneId,
//                startNode,
//                Flags.LaneArrows.Forward,
//                out res);
//            if (res == Flags.LaneArrowChangeResult.Invalid ||
//                res == Flags.LaneArrowChangeResult.Success) {
//                UpdateButtonGraphics(laneId, NetLane.Flags.Forward, button);
//                UpdateLaneControlButton(sharedState_.btnLaneArrowForward_.transform.parent.gameObject, laneId);
//            }
//        }

//        private void OnClickLeft(ushort segmentId, ushort nodeId, uint laneId, bool startNode, GameObject button) {
//            var res = Flags.LaneArrowChangeResult.Invalid;
//            LaneArrowManager.Instance.ToggleLaneArrows(
//                laneId,
//                startNode,
//                Flags.LaneArrows.Left,
//                out res);
//            if (res == Flags.LaneArrowChangeResult.Invalid ||
//                res == Flags.LaneArrowChangeResult.Success) {
//                UpdateButtonGraphics(laneId, NetLane.Flags.Left, button);
//                UpdateLaneControlButton(sharedState_.btnLaneArrowLeft_.transform.parent.gameObject, laneId);
//            }
//        }

//        private void OnClickRight(ushort segmentId, ushort nodeId, uint laneId, bool startNode, GameObject button) {
//            var res = Flags.LaneArrowChangeResult.Invalid;
//            LaneArrowManager.Instance.ToggleLaneArrows(
//                laneId,
//                startNode,
//                Flags.LaneArrows.Right,
//                out res);
//            if (res == Flags.LaneArrowChangeResult.Invalid ||
//                res == Flags.LaneArrowChangeResult.Success) {
//                UpdateButtonGraphics(laneId, NetLane.Flags.Right, button);
//                UpdateLaneControlButton(sharedState_.btnLaneArrowRight_.transform.parent.gameObject, laneId);
//            }
//        }

//        private void UpdateLaneControlButton(GameObject button, uint laneId) {
//            var lane = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId];
//            var flags = (NetLane.Flags)lane.m_flags;
//            WorldSpaceGUI.SetButtonSprite(button, LaneArrowsTextures.GetLaneControlSprite(flags));
//        }

//        private Sprite GetLaneControlSprite(LaneButtonState state, ArrowDirection dir) {
//            switch (state) {
//                case LaneButtonState.On:
//                    return LaneArrowsTextures.GetLaneArrowSprite(dir, true, false);
//                case LaneButtonState.Off:
//                    return LaneArrowsTextures.GetLaneArrowSprite(dir, false, false);
//                default:
//                    return LaneArrowsTextures.GetLaneArrowSprite(dir, false, true);
//            }
//        }

//        /// <summary>
//        /// Based on NetLane Flags direction and button state (on, off, disabled),
//        /// return the texture to display on button.
//        /// </summary>
//        /// <param name="direction">Left, Right, Forward, no bit combinations</param>
//        /// <param name="state">Button state (on, off, disabled)</param>
//        /// <returns>The texture</returns>
//        private Sprite SelectControlButtonSprite(NetLane.Flags direction, LaneButtonState state) {
//            switch (direction) {
//                case NetLane.Flags.Forward:
//                    return GetLaneControlSprite(state, ArrowDirection.Forward);
//                case NetLane.Flags.Left:
//                    return GetLaneControlSprite(state, ArrowDirection.Left);
//                case NetLane.Flags.Right:
//                    return GetLaneControlSprite(state, ArrowDirection.Right);
//                default:
//                    Log.Error($"Trying to find texture for lane state {direction.ToString()}");
//                    return null;
//            }
//        }

//        /// <summary>
//        /// Creates Turn Left lane control button slightly to the left of the originButton.
//        /// </summary>
//        /// <param name="originButton">The parent for the new button</param>
//        /// <param name="forward">The state of the button (on, off, disabled)</param>
//        private void GuiAddLaneArrowForward(LaneArrowsEditor editor,
//                                            GameObject originButton,
//                                            LaneButtonState forward) {
//            sharedState_.btnLaneArrowForward_ = editor.Gui.AddButton(
//                new Vector3(0f, LANE_BUTTON_SIZE * 1.3f, 0f),
//                new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE),
//                string.Empty,
//                originButton);
//
//            WorldSpaceGUI.SetButtonSprite(
//                sharedState_.btnLaneArrowForward_,
//                SelectControlButtonSprite(NetLane.Flags.Forward, forward));
//        }

//        /// <summary>
//        /// Creates Turn Left lane control button slightly to the left of the originButton.
//        /// </summary>
//        /// <param name="originButton">The parent for the new button</param>
//        /// <param name="left">The state of the button (on, off, disabled)</param>
//        private void GuiAddLaneArrowLeft(LaneArrowsEditor editor,
//                                         GameObject originButton,
//                                         LaneButtonState left) {
//            sharedState_.btnLaneArrowLeft_ = editor.Gui.AddButton(
//                new Vector3(-LANE_BUTTON_SIZE, LANE_BUTTON_SIZE * 1.3f, 0f),
//                new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE),
//                string.Empty,
//                originButton);
//
//            WorldSpaceGUI.SetButtonSprite(
//                sharedState_.btnLaneArrowLeft_,
//                SelectControlButtonSprite(NetLane.Flags.Left, left));
//        }

//        /// <summary>
//        /// Creates Turn Right lane control button, slightly right, belonging to the originButton
//        /// </summary>
//        /// <param name="originButton">The parent for the new button</param>
//        /// <param name="right">The state of the button (on, off, disabled)</param>
//        private void GuiAddLaneArrowRight(LaneArrowsEditor editor,
//                                          GameObject originButton,
//                                          LaneButtonState right) {
//            sharedState_.btnLaneArrowRight_ = editor.Gui.AddButton(
//                new Vector3(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE * 1.3f, 0f),
//                new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE),
//                string.Empty,
//                originButton);
//
//            WorldSpaceGUI.SetButtonSprite(
//                sharedState_.btnLaneArrowRight_,
//                SelectControlButtonSprite(NetLane.Flags.Right, right));
//        }

        /// <summary>
        /// For incoming segment into a node, get allowed directions to leave the segment.
        /// This is used to disable some of the lane turn buttons.
        /// </summary>
        /// <param name="nodeId">The currently edited node</param>
        /// <param name="ignoreSegmentId">The currently edited segment to exclude, or 0</param>
        /// <returns>Dict where keys are allowed lane turns, and values are sets of segment ids</returns>
        private OutgoingTurnsCollection GetOutgoingTurns(ushort nodeId, ushort ignoreSegmentId) {
            var result = new OutgoingTurnsCollection(nodeId, ignoreSegmentId);

            var geometry = SegmentGeometry.Get(ignoreSegmentId);
            if (geometry == null) {
                Log.Error(
                    $"LaneArrowsTool: No geometry information available for segment {ignoreSegmentId}");
                return result;
            }

            var nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            var node = nodesBuffer[nodeId];
            var incomingSegment = Singleton<NetManager>.instance.m_segments.m_buffer[ignoreSegmentId];
            var isStartNode = nodeId == incomingSegment.m_startNode;

            for (var i = 0; i < MAX_NODE_SEGMENTS; ++i) {
                var outgoingSegId = node.GetSegment(i);
                if (outgoingSegId == 0) {
                    continue;
                }

                if (outgoingSegId == ignoreSegmentId) {
                    continue;
                }

                result.AddTurn(geometry.GetDirection(outgoingSegId, isStartNode), outgoingSegId);
            }

            return result;
        }

        /// <summary>
        /// Retrieve a unique set of all lanes that enter a given node
        /// </summary>
        /// <param name="nodeId">The node</param>
        /// <returns>The unique set of lane ids</returns>
        private HashSet<uint> GetAllIncomingLanes(ushort nodeId) {
            var result = new HashSet<uint>();

            var nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            var node = nodeBuffer[nodeId];

            for (var i = 0; i < MAX_NODE_SEGMENTS; ++i) {
                var connectedSegId = node.GetSegment(i);
                if (connectedSegId == 0) {
                    continue;
                }

                foreach (var ln in GetIncomingLaneList(connectedSegId, nodeId)) {
                    result.Add(ln.laneId);
                }
            }

            return result;
        }


        /// <summary>
        /// Escape is pressed, or the tool was closed.
        /// </summary>
        public override void Cleanup() {
            Deselect();
        }
    } // class
} // namespace