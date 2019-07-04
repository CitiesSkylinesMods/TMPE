namespace TrafficManager.UI.SubTools.LaneArrows {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CanvasGUI;
// for simpler higher order functions
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using global::TrafficManager.Geometry.Impl;
    using Manager.Impl;
    using State;
    using Texture;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.UI;

    public class LaneArrowTool : SubTool {
        private const float LANE_BUTTON_SIZE = 4f; // control button size, 4x4m
        private const int MAX_NODE_SEGMENTS = 8;

        /// <summary>
        /// One lane arrow editor with canvas, per segment
        /// </summary>
        private Dictionary<ushort, LaneArrowsEditor> laneEditors_ = new Dictionary<ushort, LaneArrowsEditor>();

        /// <summary>
        /// State for the GUI is shared with all LaneArrowsEditors in laneEditors_
        /// </summary>
        private SharedLaneArrowsGuiState sharedState_ = new SharedLaneArrowsGuiState();

        public LaneArrowTool(TrafficManagerTool mainTool)
            : base(mainTool)
        {
        }

        /// <summary>
        /// Used for selecting textures for lane arrows in different states
        /// </summary>
        private enum LaneButtonState {
            On,
            Off,
            Disabled
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

        private bool IsCursorInAnyLaneEditor() {
            return laneEditors_.Values.Any(
                laneEditor => laneEditor.Gui.RaycastMouse().Count > 0);
        }

        /// <summary>
        /// Left click on a node should enter the node edit mode
        /// </summary>
        public override void OnPrimaryClickOverlay() {
            if (HoveredNodeId == 0 || HoveredSegmentId == 0) {
                return;
            }

            var netFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags;
            if ((netFlags & NetNode.Flags.Junction) == NetNode.Flags.None) {
                return;
            }

            Deselect();
            SelectedNodeId = HoveredNodeId;
            SelectedSegmentId = 0;

//            var hoveredSegment = Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId];
//            if (hoveredSegment.m_startNode != HoveredNodeId &&
//                hoveredSegment.m_endNode != HoveredNodeId) {
//                return;
//            }
//
//            SelectedSegmentId = HoveredSegmentId;
        }

        /// <summary>
        /// Right click on the world should remove the selection
        /// </summary>
        public override void OnSecondaryClickOverlay() {
            if (!IsCursorInPanel()) {
                Deselect();
            }
        }

        public override void OnToolGUI(Event e) {
            if (SelectedNodeId == 0) {
                return;
            }

            // Cancel selection immediately if there are no vehicle lanes
//            var numLanes = TrafficManagerTool.GetSegmentNumVehicleLanes(
//                SelectedSegmentId, SelectedNodeId, out _, LaneArrowManager.VEHICLE_TYPES);
//            if (numLanes <= 0) {
//                Deselect();
//                return;
//            }

            var nodePos = Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId].m_position;
            var visible = MainTool.WorldToScreenPoint(nodePos, out _);
            if (!visible) {
                return;
            }

            var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
            var diff = nodePos - camPos;
            if (diff.magnitude > TrafficManagerTool.MaxOverlayDistance) {
                return; // do not draw if too distant
            }

            // Try click something on Canvas
            foreach (var laneEditor in laneEditors_.Values) {
                if (laneEditor.Gui.HandleInput()) {
                    break; // handle until first true is returned (consumed)
                }
            }
        }

        /// <summary>
        /// User right-clicked or other reasons, the selection is cleared
        /// </summary>
        private void Deselect() {
            SelectedSegmentId = 0;
            SelectedNodeId = 0;

            foreach (var laneEditor in laneEditors_.Values) {
                laneEditor.Destroy();
            }

            laneEditors_.Clear();
            sharedState_.Reset();
        }

        /// <summary>
        /// Render selection and hovered nodes and segments
        /// </summary>
        /// <param name="cameraInfo">The camera</param>
        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            var netManager = Singleton<NetManager>.instance;
            var nodeBuffer = netManager.m_nodes.m_buffer;
            var segmentBuffer = netManager.m_segments.m_buffer;
            var laneBuffer = netManager.m_lanes.m_buffer;

            //---------------------------------------------------
            // Highlight the other hovered node than the selected
            //---------------------------------------------------
            if (!IsCursorInAnyLaneEditor()
                && HoveredNodeId != 0
                && HoveredNodeId != SelectedNodeId) {
                var hoveredNodeFlags = nodeBuffer[HoveredNodeId].m_flags;
                if ((hoveredNodeFlags & NetNode.Flags.Junction) != NetNode.Flags.None) {
                    RenderNodeOverlay(cameraInfo, ref nodeBuffer[HoveredNodeId], Color.white);
                }
            }

            // Draw the selected node, if any
            if (SelectedNodeId != 0) {
                RenderNodeOverlay(cameraInfo, ref nodeBuffer[SelectedNodeId], new Color(0f, 0f, 1f, 0.3f));
            }

            //----------------------------------------------------
            // Draw the lane we are editing and all outgoing lanes
            //----------------------------------------------------
            if (SelectedSegmentId != 0 && sharedState_.selectedLaneId_ != 0) {
                var selectedSegment = segmentBuffer[SelectedSegmentId];
                var selectedLane = laneBuffer[sharedState_.selectedLaneId_];
                RenderLaneOverlay(cameraInfo, ref selectedSegment, selectedLane,
                                  Mathf.Max(3f, selectedSegment.Info.m_lanes[0].m_width),
                                  MainTool.GetToolColor(true, false));

                var editor = laneEditors_[SelectedSegmentId];
                if (editor.PossibleTurns != null) {
                    var turns = editor.PossibleTurns.Value;
                    foreach (var laneId in turns.GetLanesFor((NetLane.Flags)selectedLane.m_flags)) {
                        var lane = laneBuffer[laneId];
                        RenderLaneOverlay(cameraInfo, ref selectedSegment, lane, 1f, Color.green);
                    }
                }
            }

            // If no editors are created for the lanes around the selected node,
            // let's create the UI (slightly above the ground)
            if (SelectedNodeId != 0 && laneEditors_.Count == 0) {
                try {
                    CreateEditorsForAllSegments();
                }
                catch (Exception e) {
                    Log.Error($"Creating editors for node segments: {e}");
                }
            }
        }

        /// <summary>
        /// Draw a HOVER_RADIUS=10m circle on the node
        /// </summary>
        /// <param name="cameraInfo">The camera</param>
        /// <param name="node">The node to take the position</param>
        /// <param name="color">The color</param>
        private static void RenderNodeOverlay(RenderManager.CameraInfo cameraInfo,
                                              ref NetNode node,
                                              Color color) {
            ++Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls;

            const float NODE_HOVER_RADIUS = 10f;
            Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(
                cameraInfo, color, node.m_position, NODE_HOVER_RADIUS,
                -1f, 1280f, false, false);
        }

        private static void RenderLaneOverlay(RenderManager.CameraInfo cameraInfo,
                                             ref NetSegment segment,
                                             NetLane lane,
                                             float width,
                                             Color color) {
            var info = segment.Info;
            if (info == null ||
                ((segment.m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None
                 && !info.m_overlayVisible)) {
                return;
            }

            ++Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls;

            Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(
                cameraInfo, color, lane.m_bezier, width, -100000f, -100000f,
                -1f, 1280f, false, false);
        }

        /// <summary>
        /// Create canvas one per incoming segment to the selected node.
        /// Fill each canvas with lane buttons.
        /// The initial GUI state is one lane control button per each incoming lane.
        /// Clicking that lane button will produce 0..3 arrow buttons controlling that lane.
        /// Clicking another lane button will destroy these and create another 0..3 buttons.
        /// Right-clicking, or clicking another node will destroy the GUI.
        /// </summary>
        private void CreateEditorsForAllSegments() {
            var nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            var segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            var selectedNode = nodeBuffer[SelectedNodeId];
            var nodeId = SelectedNodeId;

            // For all incoming segments
            for (var i = 0; i < MAX_NODE_SEGMENTS; ++i) {
                var incomingSegmentId = selectedNode.GetSegment(i);
                if (incomingSegmentId == 0) {
                    continue;
                }

                var incomingSegment = segmentBuffer[incomingSegmentId];

                // Create the GUI form and center it according to the node position
                Quaternion rotInverse;
                Vector3 guiOriginWorldPos;
                var editor = new LaneArrowsEditor();
                editor.CreateWorldSpaceCanvas(
                    incomingSegment, nodeId, incomingSegmentId, out guiOriginWorldPos, out rotInverse);
                laneEditors_[incomingSegmentId] = editor;

                if (LaneConnectionManager.Instance.HasNodeConnections(nodeId)) {
                    // Do not allow editing lanes, because custom connections exist
                    // Create a forbidden icon button which does not click
                    CreateForbiddenButton_LaneConnections();
                    return; // do not create any more buttons
                }

                var laneList = GetIncomingLaneList(incomingSegmentId, nodeId);
                CreateLaneControlButtons(editor, nodeId, incomingSegmentId, laneList,
                                         rotInverse, guiOriginWorldPos);
            }
        }

        /// <summary>
        /// As now we know that lane arrows cannot be edited, we do not create lane control GUI,
        /// instead create a single button with a warning icon. Clicking this button
        /// will pop up an explanation message.
        /// </summary>
        private void CreateForbiddenButton_LaneConnections() {
            var gui = laneEditors_.First().Value.Gui; // exact 1 element in laneEditors
            var button = gui.AddButton(
                new Vector3(0f, 0f, 0f),
                new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE),
                string.Empty);
            WorldSpaceGUI.SetButtonSprite(button, LaneArrowsTextures.GetSprite(3, 2));

            UnityAction clickLaneConnections = () => {
                const string MESSAGE = "Lane arrows disabled for this node, because you have custom " +
                                       "lane connections set up.";
                UIView.library
                      .ShowModal<ExceptionPanel>("ExceptionPanel")
                      .SetMessage("No Lane Arrows for this node", MESSAGE, false);
            };
            button.GetComponent<Button>().onClick.AddListener(clickLaneConnections);
        }

        /// <summary>
        /// The lane editing is possible.
        /// Create buttons one per lane.
        /// </summary>
        /// <param name="editor">The editor containing canvas for these buttons</param>
        /// <param name="nodeId">Node being the center of the junction and all editors</param>
        /// <param name="segmentId">Segment we are editing in this canvas</param>
        /// <param name="laneList">List of lanes to create buttons for them</param>
        /// <param name="rotInverse">The inverse rotation quaternion for projecting world stuff into the canvas</param>
        /// <param name="guiOriginWorldPos">Where GUI 0,0 is located in the world</param>
        private void CreateLaneControlButtons(LaneArrowsEditor editor,
                                              ushort nodeId,
                                              ushort segmentId,
                                              IList<LanePos> laneList,
                                              Quaternion rotInverse,
                                              Vector3 guiOriginWorldPos) {
            var lanesBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;

            var geometry = SegmentGeometry.Get(segmentId);
            if (geometry == null) {
                Log.Error("LaneArrowTool._guiLaneChangeWindow: No geometry information " +
                          $"available for segment {segmentId}");
                return;
            }

            var isStartNode = geometry.StartNodeId() == nodeId;

            // Now iterate over eligible lanes
            for (var i = 0; i < laneList.Count; i++) {
                var laneId = laneList[i].laneId;
                var lane = lanesBuffer[laneId];
                var laneFlags = (NetLane.Flags)lane.m_flags;

                if (!Flags.applyLaneArrowFlags(laneList[i].laneId)) {
                    Flags.removeLaneArrowFlags(laneList[i].laneId);
                }

                // Get position of the editable lane
                var laneEndPosition = lane.m_bezier.Position(isStartNode ? 0f : 1f);
                var buttonPositionRot = rotInverse * (laneEndPosition - guiOriginWorldPos);

                // because UI grows up (away from the node), shift it slightly back in by 3 button sizes
                // TODO: Get the distance (junction size) from netSegment.Info and step back by that
                var buttonPosition = new Vector3(buttonPositionRot.x,
                                                 buttonPositionRot.z - (LANE_BUTTON_SIZE * 3f),
                                                 0f);

                var laneEditButton = editor.Gui.AddButton(
                    buttonPosition, new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE));

                WorldSpaceGUI.SetButtonSprite(
                    laneEditButton, LaneArrowsTextures.GetLaneControlSprite(laneFlags));

                laneEditButton.GetComponent<Button>().onClick.AddListener(
                    () => {
                        // Ignore second click on the same control button
                        if (laneEditButton == sharedState_.btnCurrentControlButton_) {
                            return;
                        }

                        SelectedSegmentId = segmentId;
                        sharedState_.selectedLaneId_ = laneId;
                        try {
                            CreateLaneArrowButtons(
                                laneEditButton, segmentId, SelectedNodeId, laneId, isStartNode);
                        }
                        catch (Exception e) {
                            Log.Error($"While creating lane arrows: {e}");
                        }
                    });

                // TODO: Fix this if only one incoming lane available in the whole junction? or delete this
//                if (laneList.Count == 1) {
//                    // For only one lane, immediately open the arrow buttons
//                    sharedState_.selectedLaneId_ = laneId;
//                    CreateLaneControlArrows(laneEditButton, segmentId, SelectedNodeId, laneId, isStartNode);
//                }
            }
        }

        /// <summary>
        /// Create world space canvas in the ground plane.
        /// Recreate Lane Arrows at the location of the given button and slightly above it.
        /// </summary>
        /// <param name="originButton">The button user clicked to create these controls</param>
        /// <param name="segmentId">Current segment being edited</param>
        /// <param name="nodeId">Current junction</param>
        /// <param name="laneId">Current lane being edited</param>
        /// <param name="isStartNode">Bool if selected node is start node of the geometry (used for
        ///     lane modifications later)</param>
        private void CreateLaneArrowButtons(GameObject originButton,
                                            ushort segmentId,
                                            ushort nodeId,
                                            uint laneId,
                                            bool isStartNode) {
            // Set some nice color or effect to highlight
            originButton.GetComponent<Image>().color = Color.gray;

            var lane = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId];
            var flags = (NetLane.Flags)lane.m_flags;

            sharedState_.DestroyLaneArrowButtons();
            sharedState_.btnCurrentControlButton_ = originButton; // save this to decolorize it later

            // Get all possible turn directions to leave the nodeId via this segment
            var editor = laneEditors_[segmentId];
            editor.PossibleTurns = GetAllTurnsOut(nodeId, segmentId);

            //-----------------
            // Button FORWARD
            //-----------------
            var forward = (flags & NetLane.Flags.Forward) != 0 ? LaneButtonState.On : LaneButtonState.Off;
            if (editor.PossibleTurns != null
                && editor.PossibleTurns.Value.Contains(ArrowDirection.Forward)) {
                GuiAddLaneArrowForward(editor, originButton, forward);
                UnityAction clickForward = () => {
                    OnClickForward(laneId, isStartNode, sharedState_.btnLaneArrowForward_);
                };

                var buttonComponent = sharedState_.btnLaneArrowForward_.GetComponent<Button>();
                buttonComponent.onClick.AddListener(clickForward);
//            } else {
//                GuiAddLaneArrowForward(originButton, LaneButtonState.Disabled);
                // Note: no click handler added
            }

            //-----------------
            // Button LEFT
            //-----------------
            var left = (flags & NetLane.Flags.Left) != 0 ? LaneButtonState.On : LaneButtonState.Off;
            if (editor.PossibleTurns != null
                && editor.PossibleTurns.Value.Contains(ArrowDirection.Left)) {
                GuiAddLaneArrowLeft(editor, originButton, left);
                UnityAction clickLeft = () => {
                    OnClickLeft(segmentId, SelectedNodeId, laneId,
                                isStartNode, sharedState_.btnLaneArrowLeft_);
                };
                var buttonComponent = sharedState_.btnLaneArrowLeft_.GetComponent<Button>();
                buttonComponent.onClick.AddListener(clickLeft);
//            } else {
//                GuiAddLaneArrowLeft(originButton, LaneButtonState.Disabled);
                // Note: no click handler added
            }

            //-----------------
            // Button RIGHT
            //-----------------
            var right = (flags & NetLane.Flags.Right) != 0 ? LaneButtonState.On : LaneButtonState.Off;
            if (editor.PossibleTurns != null
                && editor.PossibleTurns.Value.Contains(ArrowDirection.Right)) {
                GuiAddLaneArrowRight(editor, originButton, right);
                UnityAction clickRight = () => {
                    OnClickRight(segmentId, SelectedNodeId, laneId,
                                 isStartNode, sharedState_.btnLaneArrowRight_);
                };
                var buttonComponent = sharedState_.btnLaneArrowRight_.GetComponent<Button>();
                buttonComponent.onClick.AddListener(clickRight);
//            } else {
//                GuiAddLaneArrowRight(originButton, LaneButtonState.Disabled);
                // Note: no click handler added
            }
        }

        private void UpdateButtonGraphics(uint laneId,
                                          NetLane.Flags direction,
                                          GameObject button) {
            var lane = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId];
            var flags = (NetLane.Flags)lane.m_flags;
            var buttonState = (flags & direction) != 0 ? LaneButtonState.On : LaneButtonState.Off;
            WorldSpaceGUI.SetButtonSprite(button, SelectControlButtonSprite(direction, buttonState));
        }

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

        private void OnClickForward(uint laneId, bool startNode, GameObject button) {
            var res = Flags.LaneArrowChangeResult.Invalid;
            LaneArrowManager.Instance.ToggleLaneArrows(
                laneId,
                startNode,
                Flags.LaneArrows.Forward,
                out res);
            if (res == Flags.LaneArrowChangeResult.Invalid ||
                res == Flags.LaneArrowChangeResult.Success) {
                UpdateButtonGraphics(laneId, NetLane.Flags.Forward, button);
                UpdateLaneControlButton(sharedState_.btnLaneArrowForward_.transform.parent.gameObject, laneId);
            }
        }

        private void OnClickLeft(ushort segmentId, ushort nodeId, uint laneId, bool startNode, GameObject button) {
            var res = Flags.LaneArrowChangeResult.Invalid;
            LaneArrowManager.Instance.ToggleLaneArrows(
                laneId,
                startNode,
                Flags.LaneArrows.Left,
                out res);
            if (res == Flags.LaneArrowChangeResult.Invalid ||
                res == Flags.LaneArrowChangeResult.Success) {
                UpdateButtonGraphics(laneId, NetLane.Flags.Left, button);
                UpdateLaneControlButton(sharedState_.btnLaneArrowLeft_.transform.parent.gameObject, laneId);
            }
        }

        private void OnClickRight(ushort segmentId, ushort nodeId, uint laneId, bool startNode, GameObject button) {
            var res = Flags.LaneArrowChangeResult.Invalid;
            LaneArrowManager.Instance.ToggleLaneArrows(
                laneId,
                startNode,
                Flags.LaneArrows.Right,
                out res);
            if (res == Flags.LaneArrowChangeResult.Invalid ||
                res == Flags.LaneArrowChangeResult.Success) {
                UpdateButtonGraphics(laneId, NetLane.Flags.Right, button);
                UpdateLaneControlButton(sharedState_.btnLaneArrowRight_.transform.parent.gameObject, laneId);
            }
        }

        private void UpdateLaneControlButton(GameObject button, uint laneId) {
            var lane = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId];
            var flags = (NetLane.Flags)lane.m_flags;
            WorldSpaceGUI.SetButtonSprite(button, LaneArrowsTextures.GetLaneControlSprite(flags));
        }

        private Sprite GetLaneControlSprite(LaneButtonState state, ArrowDirection dir) {
            switch (state) {
                case LaneButtonState.On:
                    return LaneArrowsTextures.GetLaneArrowSprite(dir, true, false);
                case LaneButtonState.Off:
                    return LaneArrowsTextures.GetLaneArrowSprite(dir, false, false);
                default:
                    return LaneArrowsTextures.GetLaneArrowSprite(dir, false, true);
            }
        }

        /// <summary>
        /// Based on NetLane Flags direction and button state (on, off, disabled),
        /// return the texture to display on button.
        /// </summary>
        /// <param name="direction">Left, Right, Forward, no bit combinations</param>
        /// <param name="state">Button state (on, off, disabled)</param>
        /// <returns>The texture</returns>
        private Sprite SelectControlButtonSprite(NetLane.Flags direction, LaneButtonState state) {
            switch (direction) {
                case NetLane.Flags.Forward:
                    return GetLaneControlSprite(state, ArrowDirection.Forward);
                case NetLane.Flags.Left:
                    return GetLaneControlSprite(state, ArrowDirection.Left);
                case NetLane.Flags.Right:
                    return GetLaneControlSprite(state, ArrowDirection.Right);
                default:
                    Log.Error($"Trying to find texture for lane state {direction.ToString()}");
                    return null;
            }
        }

        /// <summary>
        /// Creates Turn Left lane control button slightly to the left of the originButton.
        /// </summary>
        /// <param name="originButton">The parent for the new button</param>
        /// <param name="forward">The state of the button (on, off, disabled)</param>
        private void GuiAddLaneArrowForward(LaneArrowsEditor editor,
                                            GameObject originButton,
                                            LaneButtonState forward) {
            sharedState_.btnLaneArrowForward_ = editor.Gui.AddButton(
                new Vector3(0f, LANE_BUTTON_SIZE * 1.3f, 0f),
                new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE),
                string.Empty,
                originButton);

            WorldSpaceGUI.SetButtonSprite(
                sharedState_.btnLaneArrowForward_,
                SelectControlButtonSprite(NetLane.Flags.Forward, forward));
        }

        /// <summary>
        /// Creates Turn Left lane control button slightly to the left of the originButton.
        /// </summary>
        /// <param name="originButton">The parent for the new button</param>
        /// <param name="left">The state of the button (on, off, disabled)</param>
        private void GuiAddLaneArrowLeft(LaneArrowsEditor editor,
                                         GameObject originButton,
                                         LaneButtonState left) {
            sharedState_.btnLaneArrowLeft_ = editor.Gui.AddButton(
                new Vector3(-LANE_BUTTON_SIZE, LANE_BUTTON_SIZE * 1.3f, 0f),
                new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE),
                string.Empty,
                originButton);

            WorldSpaceGUI.SetButtonSprite(
                sharedState_.btnLaneArrowLeft_,
                SelectControlButtonSprite(NetLane.Flags.Left, left));
        }

        /// <summary>
        /// Creates Turn Right lane control button, slightly right, belonging to the originButton
        /// </summary>
        /// <param name="originButton">The parent for the new button</param>
        /// <param name="right">The state of the button (on, off, disabled)</param>
        private void GuiAddLaneArrowRight(LaneArrowsEditor editor,
                                          GameObject originButton,
                                          LaneButtonState right) {
            sharedState_.btnLaneArrowRight_ = editor.Gui.AddButton(
                new Vector3(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE * 1.3f, 0f),
                new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE),
                string.Empty,
                originButton);

            WorldSpaceGUI.SetButtonSprite(
                sharedState_.btnLaneArrowRight_,
                SelectControlButtonSprite(NetLane.Flags.Right, right));
        }

        /// <summary>
        /// For incoming segment into a node, get allowed directions to leave the segment.
        /// This is used to disable some of the lane turn buttons.
        /// </summary>
        /// <param name="nodeId">The currently edited node</param>
        /// <param name="incomingSegmentId">The currently edited segment</param>
        /// <returns>Dict where keys are allowed lane turns, and values are sets of segment ids</returns>
        private PossibleTurnsOut GetAllTurnsOut(ushort nodeId, ushort incomingSegmentId) {
            var result = new PossibleTurnsOut(nodeId, incomingSegmentId);

            var geometry = SegmentGeometry.Get(incomingSegmentId);
            if (geometry == null) {
                Log.Error(
                    $"LaneArrowsTool: No geometry information available for segment {incomingSegmentId}");
                return result;
            }

            var nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            var node = nodesBuffer[nodeId];
            var incomingSegment = Singleton<NetManager>.instance.m_segments.m_buffer[incomingSegmentId];
            var isStartNode = nodeId == incomingSegment.m_startNode;

            for (var i = 0; i < MAX_NODE_SEGMENTS; ++i) {
                var outgoingSegId = node.GetSegment(i);
                if (outgoingSegId == 0) {
                    continue;
                }

                if (outgoingSegId == incomingSegmentId) {
                    continue;
                }

                result.AddTurn(geometry.GetDirection(outgoingSegId, isStartNode), outgoingSegId);
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