using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.Math;
using CSUtil.Commons;
using GenericGameBridge.Service;
using TrafficManager.Geometry.Impl;
using TrafficManager.Manager.Impl;
using TrafficManager.State;
using TrafficManager.UI.CanvasGUI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TrafficManager.UI.SubTools {
    public class LaneArrowTool : SubTool {
        /// <summary>
        /// Used for selecting textures for lane arrows in different states
        /// </summary>
        private enum LaneButtonState {
            On,
            Off,
            Disabled
        }

        private static WorldSpaceGUI wsGui;

        private GameObject btnCurrentControlButton;
        private GameObject btnLaneArrowForward;
        private GameObject btnLaneArrowLeft;
        private GameObject btnLaneArrowRight;

        // Used to draw lane on screen which is being edited
        private uint highlightLaneId;

        public LaneArrowTool(TrafficManagerTool mainTool)
            : base(mainTool) { }

        public override bool IsCursorInPanel() {
            return base.IsCursorInPanel() || wsGui?.RaycastMouse().Count > 0;
        }

        public override void OnPrimaryClickOverlay() {
            if (HoveredNodeId == 0 || HoveredSegmentId == 0) return;

            var netFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags;

            if ((netFlags & NetNode.Flags.Junction) == NetNode.Flags.None) return;

            var hoveredSegment = Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId];
            if (hoveredSegment.m_startNode != HoveredNodeId &&
                hoveredSegment.m_endNode != HoveredNodeId) {
                return;
            }

            Deselect();
            SelectedSegmentId = HoveredSegmentId;
            SelectedNodeId = HoveredNodeId;
        }

        public override void OnSecondaryClickOverlay() {
            if (!IsCursorInPanel()) {
                Deselect();
            }
        }

        public override void OnToolGUI(Event e) {
            if (SelectedNodeId == 0 || SelectedSegmentId == 0) return;

            int numDirections;
            int numLanes =
                TrafficManagerTool.GetSegmentNumVehicleLanes(SelectedSegmentId, SelectedNodeId, out numDirections,
                                                             LaneArrowManager.VEHICLE_TYPES);
            if (numLanes <= 0) {
                Deselect();
                return;
            }

            Vector3 nodePos = Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId].m_position;

            Vector3 screenPos;
            bool visible = MainTool.WorldToScreenPoint(nodePos, out screenPos);
            if (!visible) {
                return;
            }

            var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
            var diff = nodePos - camPos;

            if (diff.magnitude > TrafficManagerTool.MaxOverlayDistance)
                return; // do not draw if too distant

            // Try click something on Canvas
            wsGui?.HandleInput();
        }

        private void Deselect() {
            highlightLaneId = 0;
            SelectedSegmentId = 0;
            SelectedNodeId = 0;
            if (wsGui != null) {
                wsGui.DestroyCanvas();
                wsGui = null;
                btnLaneArrowLeft = btnLaneArrowRight = btnLaneArrowForward = null;
            }
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            NetManager netManager = Singleton<NetManager>.instance;
            //Log._Debug($"LaneArrow Overlay: {HoveredNodeId} {HoveredSegmentId} {SelectedNodeId} {SelectedSegmentId}");
            bool cursorInSecondaryPanel = wsGui?.RaycastMouse().Count > 0;
            if (!cursorInSecondaryPanel && HoveredSegmentId != 0 && HoveredNodeId != 0 &&
                (HoveredSegmentId != SelectedSegmentId || HoveredNodeId != SelectedNodeId)) {
                var nodeFlags = netManager.m_nodes.m_buffer[HoveredNodeId].m_flags;
                var hoveredSegment = netManager.m_segments.m_buffer[HoveredSegmentId];

                if ((hoveredSegment.m_startNode == HoveredNodeId ||
                     hoveredSegment.m_endNode == HoveredNodeId) &&
                    (nodeFlags & NetNode.Flags.Junction) != NetNode.Flags.None) {
                    NetTool.RenderOverlay(
                        cameraInfo, ref Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId],
                        MainTool.GetToolColor(false, false),
                        MainTool.GetToolColor(false, false));
                }
            }

            var netSegment = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId];
            if (highlightLaneId != 0) {
                var lane = Singleton<NetManager>.instance.m_lanes.m_buffer[highlightLaneId];
                RenderLaneOverlay(cameraInfo, ref netSegment, lane, 
                                  MainTool.GetToolColor(true, false),
                                  MainTool.GetToolColor(true, false));
            }

            if (SelectedSegmentId == 0) {
                return;
            }

            if (highlightLaneId == 0) {
                NetTool.RenderOverlay(cameraInfo, ref netSegment, MainTool.GetToolColor(true, false),
                                      MainTool.GetToolColor(true, false));
            }

            // Create UI in the ground plane, slightly above
            if (wsGui == null) {
                CreateWorldSpaceGUI(netSegment);
            }
        }

        public static void RenderLaneOverlay(RenderManager.CameraInfo cameraInfo,
                                             ref NetSegment segment,
                                             NetLane lane,
                                             Color importantColor,
                                             Color nonImportantColor) {
            var info = segment.Info;
            if (info == null ||
                ((segment.m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None
                && !info.m_overlayVisible)) {
                return;
            }

            Color color = (ItemClass.GetPrivateServiceIndex(info.m_class.m_service) == -1
                          && !info.m_autoRemove)
                          || (segment.m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None
                              ? importantColor
                              : nonImportantColor;

            ++Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls;

            Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(
                cameraInfo, color, lane.m_bezier,
                Mathf.Max(3f, segment.Info.m_lanes[0].m_width),
                -100000f, -100000f,
                -1f, 1280f, false, false);
        }

        private const float LANE_BUTTON_SIZE = 4f; // control button size, 4x4m

        /// <summary>
        /// Fill canvas with buttons for the clicked segment.
        /// The initial state is one lane control button per lane.
        /// Clicking that lane button will produce 3 arrow buttons controlling that lane.
        /// Clicking another lane button will destroy these 3 and create 3 new arrow buttons.
        /// Clicking away, or clicking another segment will hide everything.
        /// </summary>
        /// <param name="netSegment">The most recently clicked segment, will be used to position
        ///     and rotate the canvas.</param>
        private void CreateWorldSpaceGUI(NetSegment netSegment) {
            var lanesBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;
            var laneList = GetLaneList(SelectedSegmentId, SelectedNodeId);

            // Create the GUI form and center it according to the node position
            Quaternion rotInverse;
            Vector3 guiOriginWorldPos;
            CreateWorldSpaceCanvas(netSegment, out guiOriginWorldPos, out rotInverse);

            var geometry = SegmentGeometry.Get(SelectedSegmentId);
            if (geometry == null) {
                Log.Error("LaneArrowTool._guiLaneChangeWindow: No geometry information " +
                          $"available for segment {SelectedSegmentId}");
                return;
            }

            var isStartNode = geometry.StartNodeId() == SelectedNodeId;

            // Now iterate over eligible lanes
            for (var i = 0; i < laneList.Count; i++) {
                var laneId = laneList[i].laneId;
                var lane = lanesBuffer[laneId];
                var flags = (NetLane.Flags) lane.m_flags;

                if (!Flags.applyLaneArrowFlags(laneList[i].laneId)) {
                    Flags.removeLaneArrowFlags(laneList[i].laneId);
                }

                // Get position of the editable lane
                var laneEndPosition = lane.m_bezier.Position(isStartNode ? 0f : 1f);
                var buttonPositionRot = rotInverse * (laneEndPosition - guiOriginWorldPos);

                // because UI grows up (away from the node), shift it slightly back in by 3 button sizes
                // TODO: Get the distance (junction size) from netSegment.Info and step back by that
                var buttonPosition = new Vector3(buttonPositionRot.x,
                                                 buttonPositionRot.z - LANE_BUTTON_SIZE * 3f, 0f);

                var laneEditButton = wsGui.AddButton(buttonPosition,
                                                     new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE));
                wsGui.SetButtonSprite(laneEditButton,
                                      TextureResources.LaneArrows.GetLaneControlSprite(flags));
                laneEditButton.GetComponent<Button>().onClick.AddListener(
                    () => {
                        // Ignore second click on the same control button
                        if (laneEditButton == btnCurrentControlButton) {
                            return;
                        }

                        highlightLaneId = laneId;
                        CreateLaneControlArrows(laneEditButton, SelectedSegmentId, SelectedNodeId,
                                                laneId, isStartNode);
                    });

                if (laneList.Count == 1) {
                    // For only one lane, immediately open the arrow buttons
                    highlightLaneId = laneId;
                    CreateLaneControlArrows(laneEditButton, SelectedSegmentId, SelectedNodeId, laneId, isStartNode);
                }

                //	if (buttonClicked) {
                //		switch (res) {
                //			case Flags.LaneArrowChangeResult.Invalid:
                //			case Flags.LaneArrowChangeResult.Success:
                //			default:
                //				break;
                //			case Flags.LaneArrowChangeResult.HighwayArrows:
                //			MainTool.ShowTooltip(Translation.GetString("Lane_Arrow_Changer_Disabled_Highway"));
                //			break;
                //			case Flags.LaneArrowChangeResult.LaneConnection:
                //			MainTool.ShowTooltip(Translation.GetString("Lane_Arrow_Changer_Disabled_Connection"));
                //			break;
                //		}
                //	}
            }
        }

        /// <summary>
        /// Given segment being edited, and globals (selected node id) create canvas centered at that node.
        /// </summary>
        /// <param name="netSegment">Segment being edited</param>
        /// <param name="guiOriginWorldPos">Returns position in the world where the canvas is centered</param>
        /// <param name="inverse">Returns inverse rotation quaternion for later use</param>
        private void CreateWorldSpaceCanvas(NetSegment netSegment, out Vector3 guiOriginWorldPos,
                                            out Quaternion inverse) {
            var nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;

            // Forward is the direction of the selected segment, even if it's a curve
            var forwardVector = GetSegmentTangent(SelectedNodeId, netSegment);
            var rot = Quaternion.LookRotation(Vector3.down, forwardVector.normalized);
            inverse = Quaternion.Inverse(rot); // for projecting stuff from world into the canvas

            // UI is floating 5 metres above the ground
            const float UI_FLOAT_HEIGHT = 5f;
            var adjustFloat = Vector3.up * UI_FLOAT_HEIGHT;

            // Adjust UI vertically
            guiOriginWorldPos = nodesBuffer[SelectedNodeId].m_position + adjustFloat;
            wsGui?.DestroyCanvas();
            wsGui = new WorldSpaceGUI("LaneArrowTool", guiOriginWorldPos, rot);
        }

        /// <summary>
        /// Recreate Lane Arrows at the location of the given button and slightly above it.
        /// </summary>
        /// <param name="originButton">The button user clicked to create these controls</param>
        /// <param name="segmentId">Current segment being edited</param>
        /// <param name="nodeId">Current junction</param>
        /// <param name="laneId">Current lane being edited</param>
        /// <param name="isStartNode">Bool if selected node is start node of the geometry (used for
        ///     lane modifications later)</param>
        private void CreateLaneControlArrows(GameObject originButton,
                                             ushort segmentId,
                                             ushort nodeId,
                                             uint laneId,
                                             bool isStartNode) {
            // Set some nice color or effect to highlight
            originButton.GetComponent<Image>().color = Color.gray;

            var lane = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId];
            var flags = (NetLane.Flags) lane.m_flags;

            DestroyLaneArrowButtons();
            btnCurrentControlButton = originButton; // save this to decolorize it later

	    // Get eligible turn directions
            var eligibleDirections = GetAllowedTurns(nodeId, segmentId);

            //-----------------
            // Button FORWARD
            //-----------------
            var forward = (flags & NetLane.Flags.Forward) != 0 ? LaneButtonState.On : LaneButtonState.Off;
            if (eligibleDirections.Contains(ArrowDirection.Forward)) {
                GuiAddLaneArrowForward(originButton, forward);
                UnityAction clickForward = () => {
                    OnClickForward(SelectedSegmentId, SelectedNodeId, laneId, isStartNode, btnLaneArrowForward);
                };
                btnLaneArrowForward.GetComponent<Button>().onClick.AddListener(clickForward);
            } else {
                GuiAddLaneArrowForward(originButton, LaneButtonState.Disabled);
		// Note: no click handler added
            }

            //-----------------
            // Button LEFT
            //-----------------
            var left = (flags & NetLane.Flags.Left) != 0 ? LaneButtonState.On : LaneButtonState.Off;
            if (eligibleDirections.Contains(ArrowDirection.Left)) {
                GuiAddLaneArrowLeft(originButton, left);
                UnityAction clickLeft = () => {
                    OnClickLeft(SelectedSegmentId, SelectedNodeId, laneId, isStartNode, btnLaneArrowLeft);
                };
                btnLaneArrowLeft.GetComponent<Button>().onClick.AddListener(clickLeft);
            } else {
                GuiAddLaneArrowLeft(originButton, LaneButtonState.Disabled);
                // Note: no click handler added
            }

            //-----------------
            // Button RIGHT
            //-----------------
            var right = (flags & NetLane.Flags.Right) != 0 ? LaneButtonState.On : LaneButtonState.Off;
            if (eligibleDirections.Contains(ArrowDirection.Right)) {
                GuiAddLaneArrowRight(originButton, right);
                UnityAction clickRight = () => {
                    OnClickRight(SelectedSegmentId, SelectedNodeId, laneId, isStartNode, btnLaneArrowRight);
                };
                btnLaneArrowRight.GetComponent<Button>().onClick.AddListener(clickRight);
            } else {
                GuiAddLaneArrowRight(originButton, LaneButtonState.Disabled);
                // Note: no click handler added
            }
        }

        private void UpdateButtonGraphics(ushort segmentId, ushort nodeId, uint laneId, NetLane.Flags direction,
                                          GameObject button) {
            var lane = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId];
            var flags = (NetLane.Flags) lane.m_flags;
            var buttonState = (flags & direction) != 0 ? LaneButtonState.On : LaneButtonState.Off;
            wsGui.SetButtonSprite(button, SelectControlButtonSprite(direction, buttonState));
        }

        private static IList<LanePos> GetLaneList(ushort segmentId, ushort nodeId) {
            var segmentsBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            return Constants.ServiceFactory.NetService.GetSortedLanes(
                segmentId,
                ref segmentsBuffer[segmentId],
                segmentsBuffer[segmentId].m_startNode == nodeId,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES,
                true);
        }

        private void OnClickForward(ushort segmentId, ushort nodeId, uint laneId, bool startNode, GameObject button) {
            var res = Flags.LaneArrowChangeResult.Invalid;
            LaneArrowManager.Instance.ToggleLaneArrows(
                laneId,
                startNode,
                Flags.LaneArrows.Forward,
                out res);
            if (res == Flags.LaneArrowChangeResult.Invalid ||
                res == Flags.LaneArrowChangeResult.Success) {
                UpdateButtonGraphics(segmentId, nodeId, laneId, NetLane.Flags.Forward, button);
                UpdateLaneControlButton(btnLaneArrowForward.transform.parent.gameObject, laneId);
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
                UpdateButtonGraphics(segmentId, nodeId, laneId, NetLane.Flags.Left, button);
                UpdateLaneControlButton(btnLaneArrowLeft.transform.parent.gameObject, laneId);
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
                UpdateButtonGraphics(segmentId, nodeId, laneId, NetLane.Flags.Right, button);
                UpdateLaneControlButton(btnLaneArrowRight.transform.parent.gameObject, laneId);
            }
        }

        private void UpdateLaneControlButton(GameObject button, uint laneId) {
            var lane = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId];
            var flags = (NetLane.Flags) lane.m_flags;
            wsGui.SetButtonSprite(button, TextureResources.LaneArrows.GetLaneControlSprite(flags));
        }

        private Sprite GetLaneControlSprite(LaneButtonState state, ArrowDirection dir) {
            switch (state) {
                case LaneButtonState.On:
                    return TextureResources.LaneArrows.GetLaneArrowSprite(dir, true, false);
                case LaneButtonState.Off:
                    return TextureResources.LaneArrows.GetLaneArrowSprite(dir, false, false);
                case LaneButtonState.Disabled:
                default:
                    return TextureResources.LaneArrows.GetLaneArrowSprite(dir, false, true);
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
        /// When lane arrow button is destroyed we might want to decolorize the control button
        /// </summary>
        private void DestroyLaneArrowButtons() {
            if (btnCurrentControlButton != null) {
                btnCurrentControlButton.GetComponentInParent<Image>().color = Color.white;
            }

            if (btnLaneArrowLeft != null) {
                Object.Destroy(btnLaneArrowLeft);
            }

            if (btnLaneArrowForward != null) {
                Object.Destroy(btnLaneArrowForward);
            }

            if (btnLaneArrowRight != null) {
                Object.Destroy(btnLaneArrowRight);
            }

            btnCurrentControlButton = btnLaneArrowLeft = btnLaneArrowForward = btnLaneArrowRight = null;
        }

        /// <summary>
        /// Creates Turn Left lane control button slightly to the left of the originButton.
        /// </summary>
        private void GuiAddLaneArrowForward(GameObject originButton, LaneButtonState forward) {
            btnLaneArrowForward = wsGui.AddButton(new Vector3(0f, LANE_BUTTON_SIZE * 1.3f, 0f),
                                                  new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE),
                                                  string.Empty,
                                                  originButton);

            wsGui.SetButtonSprite(btnLaneArrowForward,
                                  SelectControlButtonSprite(NetLane.Flags.Forward, forward));
        }

        /// <summary>
        /// Creates Turn Left lane control button slightly to the left of the originButton.
        /// </summary>
        /// <param name="originButton">The parent for the new button</param>
        /// <param name="left">The state of the button (on, off, disabled)</param>
        private void GuiAddLaneArrowLeft(GameObject originButton,
                                         LaneButtonState left) {
            btnLaneArrowLeft = wsGui.AddButton(new Vector3(-LANE_BUTTON_SIZE, LANE_BUTTON_SIZE * 1.3f, 0f),
                                               new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE),
                                               string.Empty,
                                               originButton);

            wsGui.SetButtonSprite(btnLaneArrowLeft,
                                  SelectControlButtonSprite(NetLane.Flags.Left, left));
        }

        /// <summary>
        /// Creates Turn Right lane control button, slightly right, belonging to the originButton
        /// </summary>
        /// <param name="originButton">The parent for the new button</param>
        /// <param name="right">The state of the button (on, off, disabled)</param>
        private void GuiAddLaneArrowRight(GameObject originButton,
                                          LaneButtonState right) {
            btnLaneArrowRight = wsGui.AddButton(new Vector3(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE * 1.3f, 0f),
                                                new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE),
                                                string.Empty,
                                                originButton);

            wsGui.SetButtonSprite(btnLaneArrowRight,
                                  SelectControlButtonSprite(NetLane.Flags.Right, right));
        }

        /// <summary>
        /// For given segment and one of its end nodes, get the direction vector.
        /// </summary>
        /// <returns>Direction of the given end of the segment.</returns>
        private static Vector3 GetSegmentTangent(ushort nodeId, NetSegment segment) {
            var nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            var otherNodeId = segment.GetOtherNode(nodeId);
            var nodePos = nodesBuffer[nodeId].m_position;
            var otherNodePos = nodesBuffer[otherNodeId].m_position;

            if (segment.IsStraight()) {
                return (nodePos - otherNodePos).normalized;
            }

            // Handle some curvature, take the last tangent
            var bezier = default(Bezier3);
            bezier.a = nodesBuffer[segment.m_startNode].m_position;
            bezier.d = nodesBuffer[segment.m_endNode].m_position;
            NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection,
                                             bezier.d, segment.m_endDirection,
                                             false, false,
                                             out bezier.b, out bezier.c);
            var isStartNode = nodeId == segment.m_startNode;
            var tangent = bezier.Tangent(isStartNode ? 0f : 1f);
            return tangent;
        }

        /// <summary>
        /// For incoming segment into a node, get allowed directions to leave the segment.
        /// This is used to disable some of the lane turn buttons.
        /// </summary>
        /// <param name="nodeId">The currently edited node</param>
        /// <param name="incomingSegmentId">The currently edited segment</param>
        /// <returns>Bit combination of allowed lane turns</returns>
        private HashSet<ArrowDirection> GetAllowedTurns(ushort nodeId, ushort incomingSegmentId) {
            var result = new HashSet<ArrowDirection>();

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

            for (var i = 0; i < 8; ++i) {
                var outgoingSegId = node.GetSegment(i);
                if (outgoingSegId == 0) {
                    continue;
                }
                if (outgoingSegId == incomingSegmentId) {
                    continue;
                }

                result.Add(geometry.GetDirection(outgoingSegId, isStartNode));
            }

            return result;
        }

        public override void Cleanup() {
            Deselect();
        }
    } // class
} // namespace
