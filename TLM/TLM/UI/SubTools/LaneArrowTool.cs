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

        public LaneArrowTool(TrafficManagerTool mainTool)
            : base(mainTool) { }

        public override bool IsCursorInPanel() {
            return base.IsCursorInPanel() || wsGui?.RaycastMouse().Count > 0;
        }

        public override void OnPrimaryClickOverlay() {
            if (HoveredNodeId == 0 || HoveredSegmentId == 0) return;

            var netFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags;

            if ((netFlags & NetNode.Flags.Junction) == NetNode.Flags.None) return;

            if (Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId].m_startNode != HoveredNodeId &&
                Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId].m_endNode != HoveredNodeId)
                return;

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

            if (!visible)
                return;

            var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
            var diff = nodePos - camPos;

            if (diff.magnitude > TrafficManagerTool.MaxOverlayDistance)
                return; // do not draw if too distant

            // Try click something on Canvas
            wsGui?.HandleInput();
        }

        private void Deselect() {
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

                if ((netManager.m_segments.m_buffer[HoveredSegmentId].m_startNode == HoveredNodeId ||
                     netManager.m_segments.m_buffer[HoveredSegmentId].m_endNode == HoveredNodeId) &&
                    (nodeFlags & NetNode.Flags.Junction) != NetNode.Flags.None) {
                    NetTool.RenderOverlay(
                        cameraInfo, ref Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId],
                        MainTool.GetToolColor(false, false),
                        MainTool.GetToolColor(false, false));
                }
            }

            if (SelectedSegmentId == 0) return;

            var netSegment = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentId];
            NetTool.RenderOverlay(cameraInfo, ref netSegment, MainTool.GetToolColor(true, false),
                                  MainTool.GetToolColor(true, false));

            // Create UI on the ground
            if (wsGui == null) {
                CreateWorldSpaceGUI(netSegment);
            }
        }

//        private const float LANE_GROUP_WIDTH = 11f;
//        private const float LANE_BUTTON_GAP = 0.7f;
//        private const float LANE_GROUP_GAP = LANE_BUTTON_GAP * 2f;
//        private const float LANE_GROUP_HALFSIZE = (LANE_GROUP_WIDTH - LANE_BUTTON_GAP) / 2f;

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
            var nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            var lanesBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;

            var laneList = GetLaneList(SelectedSegmentId, SelectedNodeId);

            // Create the GUI form and center it according to the lane count

            // Forward is the direction of the selected segment, even if it's a curve
            var forwardVector = GetSegmentTangent(SelectedNodeId, netSegment);
            var rot = Quaternion.LookRotation(Vector3.down, forwardVector.normalized);
            var rotInverse = Quaternion.Inverse(rot); // for projecting stuff from world into the canvas

            // UI is floating 5 metres above the ground
            const float UI_FLOAT_HEIGHT = 5f;
            var adjustFloat = Vector3.up * UI_FLOAT_HEIGHT;

            // Adjust UI vertically
            var guiOriginWorldPos = nodesBuffer[SelectedNodeId].m_position + adjustFloat;
            wsGui = new WorldSpaceGUI(guiOriginWorldPos, rot);

            // -------------------------------
            // Now iterate over eligible lanes
            // -------------------------------
            var geometry = SegmentGeometry.Get(SelectedSegmentId);
            if (geometry == null) {
                Log.Error($"LaneArrowTool._guiLaneChangeWindow: No geometry information " +
                          $"available for segment {SelectedSegmentId}");
                return;
            }

            var isStartNode = geometry.StartNodeId() == SelectedNodeId;
//            var expectedGuiWidth = (laneList.Count * LANE_GROUP_WIDTH) +
//                                   ((laneList.Count - 1) * LANE_GROUP_GAP);
//            var offset = -expectedGuiWidth / 2f;

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
                wsGui.SetButtonSprite(laneEditButton, TextureResources.LaneArrows.GetArrowsSprite(flags));
                laneEditButton.GetComponent<Button>().onClick.AddListener(
                    () => {
                        // Ignore second click on the same control button
                        if (laneEditButton == btnCurrentControlButton) {
                            return;
                        }
                        CreateLaneControlArrows(laneEditButton, SelectedSegmentId, SelectedNodeId, laneId, isStartNode);
                    });

                if (laneList.Count == 1) {
                    // For only one lane, immediately open the arrow buttons
                    CreateLaneControlArrows(laneEditButton, SelectedSegmentId, SelectedNodeId, laneId, isStartNode);
                }

                /*
                        // TODO: Here apply LaneButtonState.Disabled if the lane cannot turn there
                        var forward = (flags & NetLane.Flags.Forward) != 0 ? LaneButtonState.On : LaneButtonState.Off;
                        var bForward = GuiAddLaneControlForward(offset, forward);
                        UnityAction clickForward = () => {
                            OnClickForward(SelectedSegmentId, SelectedNodeId, laneId, isStartNode, bForward);
                        };
                        bForward.GetComponent<Button>().onClick.AddListener(clickForward);
        
                        var left = (flags & NetLane.Flags.Left) != 0 ? LaneButtonState.On : LaneButtonState.Off;
                        var bLeft = GuiAddLaneControlLeft(offset, left);
                        UnityAction clickLeft = () => {
                            OnClickLeft(SelectedSegmentId, SelectedNodeId, laneId, isStartNode, bLeft);
                        };
                        bLeft.GetComponent<Button>().onClick.AddListener(clickLeft);
        
                        var right = (flags & NetLane.Flags.Right) != 0 ? LaneButtonState.On : LaneButtonState.Off;
                        var bRight = GuiAddLaneControlRight(offset, right);
                        UnityAction clickRight = () => {
                            OnClickRight(SelectedSegmentId, SelectedNodeId, laneId, isStartNode, bRight);
                        };
                        bRight.GetComponent<Button>().onClick.AddListener(clickRight);
                
                        offset += LANE_GROUP_WIDTH + LANE_GROUP_GAP;
                */

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

            //-----------------
            // Button FORWARD
            //-----------------
            var forward = (flags & NetLane.Flags.Forward) != 0 ? LaneButtonState.On : LaneButtonState.Off;
            GuiAddLaneArrowForward(originButton, forward);
            UnityAction clickForward = () => {
                OnClickForward(SelectedSegmentId, SelectedNodeId, laneId, isStartNode, btnLaneArrowForward);
            };
            btnLaneArrowForward.GetComponent<Button>().onClick.AddListener(clickForward);

            //-----------------
            // Button LEFT
            //-----------------
            var left = (flags & NetLane.Flags.Left) != 0 ? LaneButtonState.On : LaneButtonState.Off;
            GuiAddLaneArrowLeft(originButton, left);
            UnityAction clickLeft = () => {
                OnClickLeft(SelectedSegmentId, SelectedNodeId, laneId, isStartNode, btnLaneArrowLeft);
            };
            btnLaneArrowLeft.GetComponent<Button>().onClick.AddListener(clickLeft);

            //-----------------
            // Button RIGHT
            //-----------------
            var right = (flags & NetLane.Flags.Right) != 0 ? LaneButtonState.On : LaneButtonState.Off;
            GuiAddLaneArrowRight(originButton, right);
            UnityAction clickRight = () => {
                OnClickRight(SelectedSegmentId, SelectedNodeId, laneId, isStartNode, btnLaneArrowRight);
            };
            btnLaneArrowRight.GetComponent<Button>().onClick.AddListener(clickRight);
        }

        private void UpdateButtonGraphics(ushort segmentId, ushort nodeId, uint laneId, NetLane.Flags direction,
                                          GameObject button) {
            var lane = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId];
            var flags = (NetLane.Flags) lane.m_flags;
            var buttonState = (flags & direction) != 0 ? LaneButtonState.On : LaneButtonState.Off;
            wsGui.SetButtonImage(button, SelectControlButtonGraphics(direction, buttonState));
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
            wsGui.SetButtonSprite(button, TextureResources.LaneArrows.GetArrowsSprite(flags));
        }

        private Texture2D GetButtonTextureFromState(LaneButtonState state,
                                                    Texture2D on,
                                                    Texture2D off,
                                                    Texture2D disabled) {
            switch (state) {
                case LaneButtonState.On:
                    return on;
                case LaneButtonState.Off:
                    return off;
                case LaneButtonState.Disabled:
                default:
                    return disabled;
            }
        }

        /// <summary>
        /// Based on NetLane Flags direction and button state (on, off, disabled),
        /// return the texture to display on button.
        /// </summary>
        /// <param name="direction">Left, Right, Forward, no bit combinations</param>
        /// <param name="state">Button state (on, off, disabled)</param>
        /// <returns>The texture</returns>
        private Texture2D SelectControlButtonGraphics(NetLane.Flags direction, LaneButtonState state) {
            switch (direction) {
                case NetLane.Flags.Forward:
                    return GetButtonTextureFromState(state,
                                            TextureResources.LaneArrows.ButtonForward,
                                            TextureResources.LaneArrows.ButtonForwardOff,
                                            TextureResources.LaneArrows.ButtonForwardDisabled);
                case NetLane.Flags.Left:
                    return GetButtonTextureFromState(state,
                                            TextureResources.LaneArrows.ButtonLeft,
                                            TextureResources.LaneArrows.ButtonLeftOff,
                                            TextureResources.LaneArrows.ButtonLeftDisabled);
                case NetLane.Flags.Right:
                    return GetButtonTextureFromState(state,
                                            TextureResources.LaneArrows.ButtonRight,
                                            TextureResources.LaneArrows.ButtonRightOff,
                                            TextureResources.LaneArrows.ButtonRightDisabled);
                default:
                    Log.Error($"Trying to find texture for lane state {direction.ToString()}");
                    return null;
            }
        }

        /// <summary>
        /// When lane arrow button is destroyed we might want to decolorize the control button
        /// </summary>
        /// <param name="b">The button we are destroying, its parent is to be restored</param>
        private void DestroyLaneArrowButtons() {
            if (btnCurrentControlButton != null) {
                btnCurrentControlButton.GetComponentInParent<Image>().color = Color.white;
            }

            if (btnLaneArrowLeft != null) { UnityEngine.Object.Destroy(btnLaneArrowLeft); }
            if (btnLaneArrowForward != null) { UnityEngine.Object.Destroy(btnLaneArrowForward); }
            if (btnLaneArrowRight != null) { UnityEngine.Object.Destroy(btnLaneArrowRight); }

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

            wsGui.SetButtonImage(btnLaneArrowForward,
                                 SelectControlButtonGraphics(NetLane.Flags.Forward, forward));
        }

        /// <summary>
        /// Creates Turn Left lane control button slightly to the left of the originButton.
        /// </summary>
        /// <param name="originButton">The parent for the new button</param>
        /// <param name="left">The state of the button (on, off, disabled)</param>
        private void GuiAddLaneArrowLeft(GameObject originButton,
                                           LaneButtonState left) {
            btnLaneArrowLeft = wsGui.AddButton(new Vector3(-LANE_BUTTON_SIZE, LANE_BUTTON_SIZE, 0f),
                                                    new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE),
                                                    string.Empty,
                                                    originButton);

            wsGui.SetButtonImage(btnLaneArrowLeft,
                                 SelectControlButtonGraphics(NetLane.Flags.Left, left));
        }

        /// <summary>
        /// Creates Turn Right lane control button, slightly right, belonging to the originButton
        /// </summary>
        /// <param name="originButton">The parent for the new button</param>
        /// <param name="right">The state of the button (on, off, disabled)</param>
        private void GuiAddLaneArrowRight(GameObject originButton,
                                            LaneButtonState right) {
            btnLaneArrowRight = wsGui.AddButton(new Vector3(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE, 0f),
                                                  new Vector2(LANE_BUTTON_SIZE, LANE_BUTTON_SIZE),
                                                  string.Empty,
                                                  originButton);

            wsGui.SetButtonImage(btnLaneArrowRight,
                                 SelectControlButtonGraphics(NetLane.Flags.Right, right));
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
    }
}