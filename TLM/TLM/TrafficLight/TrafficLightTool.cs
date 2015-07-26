using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using JetBrains.Annotations;
using TrafficManager.CustomAI;
using TrafficManager.Traffic;
using UnityEngine;

namespace TrafficManager.TrafficLight
{
    [UsedImplicitly]
    public class TrafficLightTool : DefaultTool
    {
        private static ToolMode _toolMode;

        private bool _mouseDown;
        private bool _mouseClicked;

        private ushort _hoveredNetNodeIdx;

        private int _hoveredSegmentIdx;

        public static List<ushort> SelectedNodeIndexes = new List<ushort>();
        private static List<int> _selectedSegmentIndexes = new List<int>();

        private readonly int[] _hoveredButton = new int[2];
        private ushort _hoveredNode;

        private bool _cursorInSecondaryPanel;
        
        private readonly GUIStyle _counterStyle = new GUIStyle();

        private bool _uiClickedSegment;
        private Rect _windowRect;
        private Rect _windowRect2;

        private float _stepValue = 1f;

        private readonly float[] _sliderValues = {1f,1f,1f,1f,1f,1f,1f,1f,1f,1f,1f,1f,1f,1f,1f,1f};

        private Texture2D _secondPanelTexture;

        private static bool _timedShowNumbers;

        static Rect ResizeGUI(Rect rect)
        {
            var rectX = (rect.x / 800) * Screen.width;
            var rectY = (rect.y / 600) * Screen.height;

            return new Rect(rectX, rectY, rect.width, rect.height);
        }

        protected override void Awake()
        {
            _windowRect = ResizeGUI(new Rect(120, 45, 450, 350));
            _windowRect2 = ResizeGUI(new Rect(120, 45, 300, 150));

            

            _secondPanelTexture = MakeTex(1200, 560, new Color(0.5f, 0.5f, 0.5f, 1f));

            base.Awake();
        }
        
        // Expose protected property
        // ReSharper disable once MemberCanBePrivate.Global
        //public new CursorInfo ToolCursor
        //{
        //    get { return base.ToolCursor; }
        //    set { base.ToolCursor = value; }
        //} // I think this was unnecessary

        public static ushort SelectedNode { get; private set; }

        public static int SelectedSegment { get; private set; }
        
        public static void SetToolMode(ToolMode mode)
        {
            _toolMode = mode;

            if (mode != ToolMode.ManualSwitch)
            {
                DisableManual();
            }

            SelectedNode = 0;
            SelectedSegment = 0;

            if (mode != ToolMode.TimedLightsSelectNode && mode != ToolMode.TimedLightsShowLights)
            {
                SelectedNodeIndexes.Clear();
                _timedShowNumbers = false;
            }

            if (mode != ToolMode.LaneRestrictions)
            {
                _selectedSegmentIndexes.Clear();
            }
        }

        // Overridden to disable base class behavior
        protected override void OnEnable()
        {
        }

        // Overridden to disable base class behavior
        protected override void OnDisable()
        {
        }

        public override void RenderGeometry(RenderManager.CameraInfo cameraInfo)
        {
            if (_hoveredNetNodeIdx != 0)
            {
                m_toolController.RenderCollidingNotifications(cameraInfo, 0, 0);
            }
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            switch (_toolMode)
            {
                case ToolMode.SwitchTrafficLight:
                    if (m_toolController.IsInsideUI || !Cursor.visible)
                    {
                        return;
                    }

                    _renderOverlaySwitch(cameraInfo, _mouseDown);
                    break;
                case ToolMode.AddPrioritySigns:
                    _renderOverlaySwitch(cameraInfo);
                    break;
                case ToolMode.ManualSwitch:
                    _renderOverlayManual(cameraInfo);
                    break;
                case ToolMode.TimedLightsSelectNode:
                    _renderOverlayTimedSelectNodes(cameraInfo);
                    break;
                case ToolMode.TimedLightsShowLights:

                    break;
                case ToolMode.LaneChange:
                    _renderOverlayLaneChange(cameraInfo);
                    break;
                case ToolMode.LaneRestrictions:
                    _renderOverlayLaneRestrictions(cameraInfo);
                    break;
                case ToolMode.Crosswalk:
                    _renderOverlayCrosswalk(cameraInfo);
                    break;
                default:
                    base.RenderOverlay(cameraInfo);
                    break;
            }
        }

        private void _renderOverlaySwitch(RenderManager.CameraInfo cameraInfo, bool warning = false)
        {
            if (_hoveredNetNodeIdx == 0) return;

            var node = GetNetNode(_hoveredNetNodeIdx);

            if ((node.m_flags & NetNode.Flags.Junction) == NetNode.Flags.None) return;

            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[node.m_segment0];

            Bezier3 bezier;
            bezier.a = node.m_position;
            bezier.d = node.m_position;

            var color = GetToolColor(warning, false);

            NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
                segment.m_endDirection,
                false, false, out bezier.b, out bezier.c);

            _renderOverlayDraw(cameraInfo, bezier, color);
        }

        private void _renderOverlayManual(RenderManager.CameraInfo cameraInfo)
        {
            if (SelectedNode != 0)
            {
                RenderNodeOverlays(cameraInfo);
            }
            else
            {
                RenderSelectionOverlay(cameraInfo);
            }
        }

        private void RenderSelectionOverlay(RenderManager.CameraInfo cameraInfo)
        {
            if (_hoveredNetNodeIdx == 0) return;
            var node = GetNetNode(_hoveredNetNodeIdx);
            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[node.m_segment0];

            if ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) return;
            Bezier3 bezier;
            bezier.a = node.m_position;
            bezier.d = node.m_position;

            var color = GetToolColor(false, false);

            NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
                segment.m_endDirection, false, false, out bezier.b, out bezier.c);
            _renderOverlayDraw(cameraInfo, bezier, color);
        }

        private void RenderNodeOverlays(RenderManager.CameraInfo cameraInfo)
        {
            var node = GetNetNode(SelectedNode);

            var nodeSimulation = CustomRoadAI.GetNodeSimulation(SelectedNode);

            for (var i = 0; i < 8; i++)
            {
                var colorGray = new Color(0.25f, 0.25f, 0.25f, 0.25f);
                int segmentId = node.GetSegment(i);

                if (segmentId == 0 ||
                    (nodeSimulation != null && TrafficLightsManual.IsSegmentLight(SelectedNode, segmentId)))
                    continue;

                var position = CalculateNodePositionForSegment(node, segmentId);

                var width = _hoveredButton[0] == segmentId ? 11.25f : 10f;
                _renderOverlayDraw(cameraInfo, colorGray, position, width, segmentId != _hoveredButton[0]);
            }
        }

        private static Vector3 CalculateNodePositionForSegment(NetNode node, int segmentId)
        {
            var position = node.m_position;

            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            if (segment.m_startNode == SelectedNode)
            {
                position.x += segment.m_startDirection.x*10f;
                position.y += segment.m_startDirection.y*10f;
                position.z += segment.m_startDirection.z*10f;
            }
            else
            {
                position.x += segment.m_endDirection.x*10f;
                position.y += segment.m_endDirection.y*10f;
                position.z += segment.m_endDirection.z*10f;
            }
            return position;
        }

        private void _renderOverlayTimedSelectNodes(RenderManager.CameraInfo cameraInfo)
        {
            if (_hoveredNetNodeIdx != 0 && !ContainsListNode(_hoveredNetNodeIdx) && !m_toolController.IsInsideUI && Cursor.visible)
            {
                var node = GetNetNode(_hoveredNetNodeIdx);
                var segment = Singleton<NetManager>.instance.m_segments.m_buffer[node.m_segment0];

                if ((node.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None)
                {
                    Bezier3 bezier;
                    bezier.a = node.m_position;
                    bezier.d = node.m_position;

                    var color = GetToolColor(false, false);

                    NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
                        segment.m_endDirection, false, false, out bezier.b, out bezier.c);
                    _renderOverlayDraw(cameraInfo, bezier, color);
                }
            }

            if (SelectedNodeIndexes.Count <= 0) return;

            foreach (var index in SelectedNodeIndexes)
            {
                var node = GetNetNode(index);
                var segment = Singleton<NetManager>.instance.m_segments.m_buffer[node.m_segment0];

                Bezier3 bezier;

                bezier.a = node.m_position;
                bezier.d = node.m_position;

                var color = GetToolColor(true, false);

                NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
                    segment.m_endDirection, false, false, out bezier.b, out bezier.c);
                _renderOverlayDraw(cameraInfo, bezier, color);
            }
        }

        private void _renderOverlayLaneChange(RenderManager.CameraInfo cameraInfo)
        {

            if (_hoveredSegmentIdx != 0 && _hoveredNetNodeIdx != 0 && (_hoveredSegmentIdx != SelectedSegment || _hoveredNetNodeIdx != SelectedNode))
            {
                var netFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_flags;

                if ((netFlags & NetNode.Flags.Junction) != NetNode.Flags.None)
                {

                    var hoveredSegment = Singleton<NetManager>.instance.m_segments.m_buffer[_hoveredSegmentIdx];

                    NetTool.RenderOverlay(cameraInfo, ref hoveredSegment, GetToolColor(false, false),
                        GetToolColor(false, false));
                }
            }

            if (SelectedSegment == 0) return;

            var selectedSegment = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegment];

            NetTool.RenderOverlay(cameraInfo, ref selectedSegment, GetToolColor(true, false), GetToolColor(true, false));
        }

        private void _renderOverlayLaneRestrictions(RenderManager.CameraInfo cameraInfo)
        {
            if (_selectedSegmentIndexes.Count > 0)
            {
                // ReSharper disable once LoopCanBePartlyConvertedToQuery - can't be converted because segment is pass by ref
                foreach (var index in _selectedSegmentIndexes)
                {
                    var segment = Singleton<NetManager>.instance.m_segments.m_buffer[index];

                    NetTool.RenderOverlay(cameraInfo, ref segment, GetToolColor(true, false),
                        GetToolColor(true, false));
                }
            }

            if (_hoveredSegmentIdx == 0) return;

            var hoveredSegment = Singleton<NetManager>.instance.m_segments.m_buffer[_hoveredSegmentIdx];

            NetTool.RenderOverlay(cameraInfo, ref hoveredSegment, GetToolColor(false, false),
                GetToolColor(false, false));
        }

        private void _renderOverlayCrosswalk(RenderManager.CameraInfo cameraInfo)
        {
            if (_hoveredSegmentIdx == 0) return;

            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[_hoveredSegmentIdx];

            if (ValidCrosswalkNode(segment.m_startNode, GetNetNode(segment.m_startNode)) ||
                ValidCrosswalkNode(segment.m_endNode, GetNetNode(segment.m_endNode)) )
            {

                NetTool.RenderOverlay(cameraInfo, ref segment, GetToolColor(_mouseDown, false),
                    GetToolColor(_mouseDown, false));
            }
        }

        private void _renderOverlayDraw(RenderManager.CameraInfo cameraInfo, Bezier3 bezier, Color color)
        {
            const float width = 8f;

            var exprEaCp0 = Singleton<ToolManager>.instance;
            exprEaCp0.m_drawCallData.m_overlayCalls = exprEaCp0.m_drawCallData.m_overlayCalls + 1;
            Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(cameraInfo, color, bezier,
                width * 2f, width, width, -1f, 1280f, false, false);

            // 8 - small roads; 16 - big roads
        }

        private void _renderOverlayDraw(RenderManager.CameraInfo cameraInfo, Color color, Vector3 position, float width, bool alpha)
        {
            var exprEaCp0 = Singleton<ToolManager>.instance;
            exprEaCp0.m_drawCallData.m_overlayCalls = exprEaCp0.m_drawCallData.m_overlayCalls + 1;
            Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(cameraInfo, color, position, width, position.y - 100f, position.y + 100f, false, alpha);
        }

        public override void SimulationStep()
        {
            base.SimulationStep();

            var mouseRayValid = !UIView.IsInsideUI() && Cursor.visible && !_cursorInSecondaryPanel;

            if (mouseRayValid)
            {
                var mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                var mouseRayLength = Camera.main.farClipPlane;
                var rayRight = Camera.main.transform.TransformDirection(Vector3.right);

                var defaultService = new RaycastService(ItemClass.Service.Road, ItemClass.SubService.None, ItemClass.Layer.Default);
                var input = new RaycastInput(mouseRay, mouseRayLength)
                {
                    m_rayRight = rayRight,
                    m_netService = defaultService,
                    m_ignoreNodeFlags = NetNode.Flags.None,
                    m_ignoreSegmentFlags = NetSegment.Flags.Untouchable
                };
                RaycastOutput output;
                if (!RayCast(input, out output))
                {
                    _hoveredSegmentIdx = 0;
                    _hoveredNetNodeIdx = 0;
                    return;
                }

                _hoveredNetNodeIdx = output.m_netNode;

                _hoveredSegmentIdx = output.m_netSegment;
            }


            if (_toolMode == ToolMode.None)
            {
                ToolCursor = null;
            }
            else
            {
                var netTool = ToolsModifierControl.toolController.Tools.OfType<NetTool>().FirstOrDefault(nt => nt.m_prefab != null);

                if (netTool != null && mouseRayValid)
                {
                    ToolCursor = netTool.m_upgradeCursor;
                }
            }
        }

        protected override void OnToolUpdate()
        {
            _mouseDown = Input.GetMouseButton(0);

            if (_mouseDown)
            {
                if (_mouseClicked) return;

                _mouseClicked = true;

                if (m_toolController.IsInsideUI || !Cursor.visible || _cursorInSecondaryPanel)
                {
                    return;
                }
                if (_hoveredSegmentIdx == 0) return;

                var node = GetNetNode(_hoveredNetNodeIdx);

                switch (_toolMode)
                {
                    case ToolMode.SwitchTrafficLight:
                        SwitchTrafficLightToolMode(node);
                        break;
                    case ToolMode.AddPrioritySigns:
                        Debug.Log("Add Priority Signs Tool Mode");
                        AddPrioritySignsToolMode(node);
                        break;
                    case ToolMode.ManualSwitch:
                        ManualSwitchToolMode(node);
                        break;
                    case ToolMode.TimedLightsSelectNode:
                        TimedLightSelectNodeToolMode(node);
                        break;
                    case ToolMode.LaneChange:
                        LaneChangeToolMode();
                        break;
                    case ToolMode.Crosswalk:
                        CrosswalkToolMode();
                        break;
                    case ToolMode.LaneRestrictions:
                        LaneRestrictionsToolMode();
                        break;
                }
            }
            else
            {
                ShowToolInfo(false, null, Vector3.zero);
                _mouseClicked = false;
            }
        }

        private void LaneChangeToolMode()
        {
            if (_hoveredNetNodeIdx == 0 || _hoveredSegmentIdx == 0) return;

            var netFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_flags;

            if ((netFlags & NetNode.Flags.Junction) == NetNode.Flags.None) return;

            SelectedSegment = _hoveredSegmentIdx;
            SelectedNode = _hoveredNetNodeIdx;
        }

        private void TimedLightSelectNodeToolMode(NetNode node)
        {
            if (!TrafficLightsTimed.IsTimedLight(_hoveredNetNodeIdx))
            {
                if ((node.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None)
                {
                    if (ContainsListNode(_hoveredNetNodeIdx))
                    {
                        RemoveListNode(_hoveredNetNodeIdx);
                    }
                    else
                    {
                        AddListNode(_hoveredNetNodeIdx);
                    }
                }
                else
                {
                    ShowToolInfo(true, "Node is not a traffic light", node.m_position);
                }
            }
            else
            {
                if (SelectedNodeIndexes.Count == 0)
                {
                    var timedLight = TrafficLightsTimed.GetTimedLight(_hoveredNetNodeIdx);

                    SelectedNodeIndexes = new List<ushort>(timedLight.NodeGroup);
                    SetToolMode(ToolMode.TimedLightsShowLights);
                }
                else
                {
                    ShowToolInfo(true, "Node is part of timed script", node.m_position);
                }
            }
        }

        private void ManualSwitchToolMode(NetNode node)
        {
            if (SelectedNode != 0) return;

            if (!TrafficLightsTimed.IsTimedLight(_hoveredNetNodeIdx))
            {
                if ((node.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None)
                {
                    SelectedNode = _hoveredNetNodeIdx;

                    var node2 = GetNetNode(SelectedNode);

                    CustomRoadAI.AddNodeToSimulation(SelectedNode);
                    var nodeSimulation = CustomRoadAI.GetNodeSimulation(SelectedNode);
                    nodeSimulation.FlagManualTrafficLights = true;

                    for (var s = 0; s < 8; s++)
                    {
                        var segment = node2.GetSegment(s);

                        if (segment != 0 && !TrafficPriority.IsPrioritySegment(SelectedNode, segment))
                        {
                            TrafficPriority.AddPrioritySegment(SelectedNode, segment, PrioritySegment.PriorityType.None);
                        }
                    }
                }
                else
                {
                    ShowToolInfo(true, "Node is not a traffic light", node.m_position);
                }
            }
            else
            {
                if (SelectedNodeIndexes.Count == 0)
                {
                }
                ShowToolInfo(true, "Node is part of timed script", node.m_position);
            }
        }

        private void AddPrioritySignsToolMode(NetNode node)
        {
            if ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None)
            {
                _uiClickedSegment = true;
                SelectedNode = _hoveredNetNodeIdx;
            }
            else
            {
                ShowToolInfo(true, "Node should not be a traffic light", node.m_position);
            }
        }

        private void SwitchTrafficLightToolMode(NetNode node)
        {
            if ((node.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None)
            {
                _switchTrafficLights();
            }
        }

        private void CrosswalkToolMode()
        {
            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[_hoveredSegmentIdx];

            var startNode = GetNetNode(segment.m_startNode);
            var endNode = GetNetNode(segment.m_endNode);

            var result = false;

            if (!result && ValidCrosswalkNode(segment.m_startNode, startNode))
            {
                if ((startNode.m_flags & NetNode.Flags.Junction) == NetNode.Flags.None)
                {
                    startNode.m_flags |= NetNode.Flags.Junction;
                    result = true;
                }
            }
            if (!result &&
                (ValidCrosswalkNode(segment.m_startNode, startNode) || ValidCrosswalkNode(segment.m_endNode, endNode)) &&
                (endNode.m_flags & NetNode.Flags.Junction) == NetNode.Flags.None)
            {
                if (ValidCrosswalkNode(segment.m_startNode, startNode) &&
                    (startNode.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None)
                {
                    startNode.m_flags &= ~NetNode.Flags.Junction;
                    result = true;
                }
                if (ValidCrosswalkNode(segment.m_endNode, endNode) &&
                    (endNode.m_flags & NetNode.Flags.Junction) == NetNode.Flags.None)
                {
                    endNode.m_flags |= NetNode.Flags.Junction;
                    result = true;
                }
            }
            if (!result &&
                (ValidCrosswalkNode(segment.m_startNode, startNode) ||
                 ValidCrosswalkNode(segment.m_endNode, endNode)))
            {
                if (ValidCrosswalkNode(segment.m_startNode, startNode) &&
                    (startNode.m_flags & NetNode.Flags.Junction) == NetNode.Flags.None)
                {
                    startNode.m_flags |= NetNode.Flags.Junction;
                    result = true;
                }
                if (ValidCrosswalkNode(segment.m_endNode, endNode) &&
                    (endNode.m_flags & NetNode.Flags.Junction) == NetNode.Flags.None)
                {
                    endNode.m_flags |= NetNode.Flags.Junction;
                    result = true;
                }
            }
            if (!result &&
                (ValidCrosswalkNode(segment.m_startNode, startNode) ||
                 ValidCrosswalkNode(segment.m_endNode, endNode)))
            {
                if (ValidCrosswalkNode(segment.m_startNode, startNode) &&
                    (startNode.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None)
                {
                    startNode.m_flags &= ~NetNode.Flags.Junction;
                }
                if (ValidCrosswalkNode(segment.m_endNode, endNode) &&
                    (endNode.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None)
                {
                    endNode.m_flags &= ~NetNode.Flags.Junction;
                }
            }

            SetNetNode(segment.m_startNode, startNode);
            SetNetNode(segment.m_endNode, endNode);
        }

        private void LaneRestrictionsToolMode()
        {
            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[_hoveredSegmentIdx];
            var info = segment.Info;

            if (TrafficRoadRestrictions.IsSegment(_hoveredSegmentIdx))
            {
                if (_selectedSegmentIndexes.Count > 0)
                {
                    ShowToolInfo(true, "Road is already in a group!",
                        Singleton<NetManager>.instance.m_nodes.m_buffer[segment.m_startNode]
                            .m_position);
                }
                else
                {
                    var restSegment = TrafficRoadRestrictions.GetSegment(_hoveredSegmentIdx);

                    _selectedSegmentIndexes = new List<int>(restSegment.SegmentGroup);
                }
            }
            else
            {
                if (ContainsListSegment(_hoveredSegmentIdx))
                {
                    RemoveListSegment(_hoveredSegmentIdx);
                }
                else
                {
                    if (_selectedSegmentIndexes.Count > 0)
                    {
                        var segment2 =
                            Singleton<NetManager>.instance.m_segments.m_buffer[_selectedSegmentIndexes[0]
                                ];
                        var info2 = segment2.Info;

                        if (info.m_lanes.Length != info2.m_lanes.Length)
                        {
                            ShowToolInfo(true, "All selected roads must be of the same type!",
                                Singleton<NetManager>.instance.m_nodes.m_buffer[segment.m_startNode]
                                    .m_position);
                        }
                        else
                        {
                            AddListSegment(_hoveredSegmentIdx);
                        }
                    }
                    else
                    {
                        AddListSegment(_hoveredSegmentIdx);
                    }
                }
            }
        }

        private static bool ValidCrosswalkNode(ushort nodeid, NetNode node)
        {
            return nodeid != 0 && (node.m_flags & (NetNode.Flags.Transition | NetNode.Flags.TrafficLights)) == NetNode.Flags.None;
        }

        protected override void OnToolGUI()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                _uiClickedSegment = false;
            }

            switch (_toolMode)
            {
                case ToolMode.AddPrioritySigns:
                    Debug.Log("Priority Sign OnToolGUI");
                    _guiPrioritySigns();
                    break;
                case ToolMode.ManualSwitch:
                    _guiManualTrafficLights();
                    break;
                case ToolMode.TimedLightsSelectNode:
                    _guiTimedTrafficLightsNode();
                    break;
                case ToolMode.TimedLightsShowLights:
                    _guiTimedTrafficLights();
                    break;
                case ToolMode.LaneChange:
                    _guiLaneChange();
                    break;
                case ToolMode.LaneRestrictions:
                    _guiLaneRestrictions();
                    break;
            }
        }

        private void _guiManualTrafficLights()
        {
            var hoveredSegment = false;

            if (SelectedNode != 0)
            {
                var node = GetNetNode(SelectedNode);

                var nodeSimulation = CustomRoadAI.GetNodeSimulation(SelectedNode);

                if (node.CountSegments() == 2)
                {
                    _guiManualTrafficLightsCrosswalk(node);
                    return;
                }

                for (var i = 0; i < 8; i++)
                {
                    int segmentId = node.GetSegment(i);

                    if (segmentId == 0 || nodeSimulation == null ||
                        !TrafficLightsManual.IsSegmentLight(SelectedNode, segmentId)) continue;

                    var segmentDict = TrafficLightsManual.GetSegmentLight(SelectedNode, segmentId);

                    var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];

                    var position = CalculateNodePositionForSegment(node, segment);

                    var screenPos = Camera.main.WorldToScreenPoint(position);
                    screenPos.y = Screen.height - screenPos.y;

                    var diff = position - Camera.main.transform.position;
                    var zoom = 1.0f/diff.magnitude*100f;

                    // original / 2.5
                    var lightWidth = 41f*zoom;
                    var lightHeight = 97f*zoom;

                    // SWITCH MODE BUTTON
                    var modeWidth = 41f*zoom;
                    var modeHeight = 38f*zoom;

                    SetAlpha(segmentId, -1);

                    var myRect1 = new Rect(screenPos.x - modeWidth / 2, screenPos.y - modeHeight / 2 + modeHeight - 7f*zoom, modeWidth, modeHeight);

                    GUI.DrawTexture(myRect1, TrafficLightToolTextureResources.LightModeTexture2D);

                    hoveredSegment = GetHoveredSegment(myRect1, segmentId, hoveredSegment, segmentDict);

                    // COUNTER
                    hoveredSegment = RenderCounter(segmentId, screenPos, modeWidth, modeHeight, zoom, segmentDict, hoveredSegment);

                    // SWITCH MANUAL PEDESTRIAN LIGHT BUTTON
                    hoveredSegment = RenderManualPedestrianLightSwitch(zoom, segmentId, screenPos, lightWidth, segmentDict, hoveredSegment);

                    // SWITCH PEDESTRIAN LIGHT
                    var pedestrianWidth = 36f * zoom;
                    var pedestrianHeight = 61f * zoom;

                    var guiColor = GUI.color;
                    guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 2 && segmentDict.PedestrianEnabled ? 0.92f : 0.45f;
                    GUI.color = guiColor;

                    var myRect3 = new Rect(screenPos.x - pedestrianWidth / 2 - lightWidth + 5f*zoom, screenPos.y - pedestrianHeight / 2 + 22f*zoom, pedestrianWidth, pedestrianHeight);

                    switch (segmentDict.LightPedestrian)
                    {
                        case RoadBaseAI.TrafficLightState.Green:
                            GUI.DrawTexture(myRect3, TrafficLightToolTextureResources.PedestrianGreenLightTexture2D);
                            break;
                        case RoadBaseAI.TrafficLightState.Red:
                            GUI.DrawTexture(myRect3, TrafficLightToolTextureResources.PedestrianRedLightTexture2D);
                            break;
                    }

                    hoveredSegment = IsPedestrianLightHovered(myRect3, segmentId, hoveredSegment, segmentDict);

                    if (TrafficLightsManual.SegmentIsIncomingOneWay(segmentId, SelectedNode)) continue;

                    var hasLeftSegment = TrafficPriority.HasLeftSegment(segmentId, SelectedNode) && TrafficPriority.HasLeftLane(SelectedNode, segmentId);
                    var hasForwardSegment = TrafficPriority.HasForwardSegment(segmentId, SelectedNode) && TrafficPriority.HasForwardLane(SelectedNode, segmentId);
                    var hasRightSegment = TrafficPriority.HasRightSegment(segmentId, SelectedNode) && TrafficPriority.HasRightLane(SelectedNode, segmentId);

                    switch (segmentDict.CurrentMode)
                    {
                        case ManualSegmentLight.Mode.Simple:
                            hoveredSegment = SimpleManualSegmentLightMode(segmentId, screenPos, lightWidth, pedestrianWidth, zoom, lightHeight, segmentDict, hoveredSegment);
                            break;
                        case ManualSegmentLight.Mode.LeftForwardR:
                            hoveredSegment = LeftForwardRManualSegmentLightMode(hasLeftSegment, segmentId, screenPos, lightWidth, pedestrianWidth, zoom, lightHeight, segmentDict, hoveredSegment, hasForwardSegment, hasRightSegment);
                            break;
                        case ManualSegmentLight.Mode.RightForwardL:
                            hoveredSegment = RightForwardLSegmentLightMode(segmentId, screenPos, lightWidth, pedestrianWidth, zoom, lightHeight, hasForwardSegment, hasLeftSegment, segmentDict, hasRightSegment, hoveredSegment);
                            break;
                        default:
                            // left arrow light
                            if (hasLeftSegment)
                                hoveredSegment = LeftArrowLightMode(segmentId, lightWidth, hasRightSegment, hasForwardSegment, screenPos, pedestrianWidth, zoom, lightHeight, segmentDict, hoveredSegment);

                            // forward arrow light
                            if (hasForwardSegment)
                                hoveredSegment = ForwardArrowLightMode(segmentId, lightWidth, hasRightSegment, screenPos, pedestrianWidth, zoom, lightHeight, segmentDict, hoveredSegment);

                            // right arrow light
                            if (hasRightSegment)
                                hoveredSegment = RightArrowLightMode(segmentId, screenPos, lightWidth, pedestrianWidth, zoom, lightHeight, segmentDict, hoveredSegment);
                            break;
                    }
                }
            }

            if (hoveredSegment) return;
            _hoveredButton[0] = 0;
            _hoveredButton[1] = 0;
        }

        private bool RightArrowLightMode(int segmentId, Vector3 screenPos, float lightWidth,
            float pedestrianWidth, float zoom, float lightHeight, ManualSegmentLight segmentDict, bool hoveredSegment)
        {
            SetAlpha(segmentId, 5);

            var myRect5 =
                new Rect(screenPos.x - lightWidth/2 - lightWidth - pedestrianWidth + 5f*zoom,
                    screenPos.y - lightHeight/2, lightWidth, lightHeight);

            switch (segmentDict.LightRight)
            {
                case RoadBaseAI.TrafficLightState.Green:
                    GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.GreenLightRightTexture2D);
                    break;
                case RoadBaseAI.TrafficLightState.Red:
                    GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.RedLightRightTexture2D);
                    break;
            }

            if (!myRect5.Contains(Event.current.mousePosition))
                return hoveredSegment;

            _hoveredButton[0] = segmentId;
            _hoveredButton[1] = 5;

            if (!Input.GetMouseButtonDown(0) || _uiClickedSegment)
                return true;

            _uiClickedSegment = true;
            segmentDict.ChangeLightRight();
            return true;
        }

        private void SetAlpha(int segmentId, int buttonId)
        {
            var guiColor = GUI.color;

            guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == buttonId ? 0.92f : 0.45f;

            GUI.color = guiColor;
        }

        private bool ForwardArrowLightMode(int segmentId, float lightWidth, bool hasRightSegment,
            Vector3 screenPos, float pedestrianWidth, float zoom, float lightHeight, ManualSegmentLight segmentDict,
            bool hoveredSegment)
        {
            SetAlpha(segmentId, 4);

            var offsetLight = lightWidth;

            if (hasRightSegment)
                offsetLight += lightWidth;

            var myRect6 =
                new Rect(screenPos.x - lightWidth/2 - offsetLight - pedestrianWidth + 5f*zoom,
                    screenPos.y - lightHeight/2, lightWidth, lightHeight);

            switch (segmentDict.LightMain)
            {
                case RoadBaseAI.TrafficLightState.Green:
                    GUI.DrawTexture(myRect6, TrafficLightToolTextureResources.GreenLightStraightTexture2D);
                    break;
                case RoadBaseAI.TrafficLightState.Red:
                    GUI.DrawTexture(myRect6, TrafficLightToolTextureResources.RedLightStraightTexture2D);
                    break;
            }

            if (!myRect6.Contains(Event.current.mousePosition))
                return hoveredSegment;

            _hoveredButton[0] = segmentId;
            _hoveredButton[1] = 4;

            if (!Input.GetMouseButtonDown(0) || _uiClickedSegment)
                return true;

            _uiClickedSegment = true;
            segmentDict.ChangeLightMain();
            return true;
        }

        private bool LeftArrowLightMode(int segmentId, float lightWidth, bool hasRightSegment,
            bool hasForwardSegment, Vector3 screenPos, float pedestrianWidth, float zoom, float lightHeight,
            ManualSegmentLight segmentDict, bool hoveredSegment)
        {
            SetAlpha(segmentId, 3);

            var offsetLight = lightWidth;

            if (hasRightSegment)
                offsetLight += lightWidth;

            if (hasForwardSegment)
                offsetLight += lightWidth;

            var myRect4 =
                new Rect(screenPos.x - lightWidth/2 - offsetLight - pedestrianWidth + 5f*zoom,
                    screenPos.y - lightHeight/2, lightWidth, lightHeight);

            switch (segmentDict.LightLeft)
            {
                case RoadBaseAI.TrafficLightState.Green:
                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightLeftTexture2D);
                    break;
                case RoadBaseAI.TrafficLightState.Red:
                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightLeftTexture2D);
                    break;
            }

            if (!myRect4.Contains(Event.current.mousePosition))
                return hoveredSegment;
            _hoveredButton[0] = segmentId;
            _hoveredButton[1] = 3;

            if (!Input.GetMouseButtonDown(0) || _uiClickedSegment)
                return true;

            _uiClickedSegment = true;
            segmentDict.ChangeLightLeft();

            if (!hasForwardSegment)
            {
                segmentDict.ChangeLightMain();
            }
            return true;
        }

        private bool RightForwardLSegmentLightMode(int segmentId, Vector3 screenPos, float lightWidth, float pedestrianWidth,
            float zoom, float lightHeight, bool hasForwardSegment, bool hasLeftSegment, ManualSegmentLight segmentDict,
            bool hasRightSegment, bool hoveredSegment)
        {
            SetAlpha(segmentId, 3);

            var myRect4 = new Rect(screenPos.x - lightWidth/2 - lightWidth*2 - pedestrianWidth + 5f*zoom,
                screenPos.y - lightHeight/2, lightWidth, lightHeight);

            if (hasForwardSegment && hasLeftSegment)
            {
                switch (segmentDict.LightLeft)
                {
                    case RoadBaseAI.TrafficLightState.Green:
                        GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightForwardLeftTexture2D);
                        break;
                    case RoadBaseAI.TrafficLightState.Red:
                        GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightForwardLeftTexture2D);
                        break;
                }
            }
            else if (!hasLeftSegment)
            {
                if (!hasRightSegment)
                {
                    myRect4 = new Rect(screenPos.x - lightWidth/2 - lightWidth - pedestrianWidth + 5f*zoom,
                        screenPos.y - lightHeight/2, lightWidth, lightHeight);
                }

                switch (segmentDict.LightMain)
                {
                    case RoadBaseAI.TrafficLightState.Green:
                        GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightStraightTexture2D);
                        break;
                    case RoadBaseAI.TrafficLightState.Red:
                        GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightStraightTexture2D);
                        break;
                }
            }
            else
            {
                if (!hasRightSegment)
                {
                    myRect4 = new Rect(screenPos.x - lightWidth/2 - lightWidth - pedestrianWidth + 5f*zoom,
                        screenPos.y - lightHeight/2, lightWidth, lightHeight);
                }

                switch (segmentDict.LightMain)
                {
                    case RoadBaseAI.TrafficLightState.Green:
                        GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightLeftTexture2D);
                        break;
                    case RoadBaseAI.TrafficLightState.Red:
                        GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightLeftTexture2D);
                        break;
                }
            }


            if (myRect4.Contains(Event.current.mousePosition))
            {
                _hoveredButton[0] = segmentId;
                _hoveredButton[1] = 3;
                hoveredSegment = true;

                if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                {
                    _uiClickedSegment = true;
                    segmentDict.ChangeLightMain();
                }
            }

            var guiColor = GUI.color;
            // right arrow light
            if (hasRightSegment)
                guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 4 ? 0.92f : 0.45f;

            GUI.color = guiColor;

            var myRect5 =
                new Rect(screenPos.x - lightWidth/2 - lightWidth - pedestrianWidth + 5f*zoom,
                    screenPos.y - lightHeight/2, lightWidth, lightHeight);

            switch (segmentDict.LightRight)
            {
                case RoadBaseAI.TrafficLightState.Green:
                    GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.GreenLightRightTexture2D);
                    break;
                case RoadBaseAI.TrafficLightState.Red:
                    GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.RedLightRightTexture2D);
                    break;
            }


            if (!myRect5.Contains(Event.current.mousePosition))
                return hoveredSegment;

            _hoveredButton[0] = segmentId;
            _hoveredButton[1] = 4;

            if (!Input.GetMouseButtonDown(0) || _uiClickedSegment)
                return true;
            _uiClickedSegment = true;
            segmentDict.ChangeLightRight();
            return true;
        }

        private bool LeftForwardRManualSegmentLightMode(bool hasLeftSegment, int segmentId, Vector3 screenPos, float lightWidth,
            float pedestrianWidth, float zoom, float lightHeight, ManualSegmentLight segmentDict, bool hoveredSegment,
            bool hasForwardSegment, bool hasRightSegment)
        {
            if (hasLeftSegment)
            {
                // left arrow light
                SetAlpha(segmentId, 3);

                var myRect4 =
                    new Rect(screenPos.x - lightWidth/2 - lightWidth*2 - pedestrianWidth + 5f*zoom,
                        screenPos.y - lightHeight/2, lightWidth, lightHeight);

                switch (segmentDict.LightLeft)
                {
                    case RoadBaseAI.TrafficLightState.Green:
                        GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightLeftTexture2D);
                        break;
                    case RoadBaseAI.TrafficLightState.Red:
                        GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightLeftTexture2D);
                        break;
                }

                if (myRect4.Contains(Event.current.mousePosition))
                {
                    _hoveredButton[0] = segmentId;
                    _hoveredButton[1] = 3;
                    hoveredSegment = true;

                    if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                    {
                        _uiClickedSegment = true;
                        segmentDict.ChangeLightLeft();
                    }
                }
            }

            // forward-right arrow light
            SetAlpha(segmentId, 4);

            var myRect5 =
                new Rect(screenPos.x - lightWidth/2 - lightWidth - pedestrianWidth + 5f*zoom,
                    screenPos.y - lightHeight/2, lightWidth, lightHeight);

            if (hasForwardSegment && hasRightSegment)
            {
                switch (segmentDict.LightMain)
                {
                    case RoadBaseAI.TrafficLightState.Green:
                        GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.GreenLightForwardRightTexture2D);
                        break;
                    case RoadBaseAI.TrafficLightState.Red:
                        GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.RedLightForwardRightTexture2D);
                        break;
                }
            }
            else if (!hasRightSegment)
            {
                switch (segmentDict.LightMain)
                {
                    case RoadBaseAI.TrafficLightState.Green:
                        GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.GreenLightStraightTexture2D);
                        break;
                    case RoadBaseAI.TrafficLightState.Red:
                        GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.RedLightStraightTexture2D);
                        break;
                }
            }
            else
            {
                switch (segmentDict.LightMain)
                {
                    case RoadBaseAI.TrafficLightState.Green:
                        GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.GreenLightRightTexture2D);
                        break;
                    case RoadBaseAI.TrafficLightState.Red:
                        GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.RedLightRightTexture2D);
                        break;
                }
            }

            if (!myRect5.Contains(Event.current.mousePosition))
                return hoveredSegment;
            _hoveredButton[0] = segmentId;
            _hoveredButton[1] = 4;

            if (!Input.GetMouseButtonDown(0) || _uiClickedSegment)
                return true;
            _uiClickedSegment = true;
            segmentDict.ChangeLightMain();
            return true;
        }

        private bool SimpleManualSegmentLightMode(int segmentId, Vector3 screenPos, float lightWidth, float pedestrianWidth,
            float zoom, float lightHeight, ManualSegmentLight segmentDict, bool hoveredSegment)
        {
            SetAlpha(segmentId, 3);

            var myRect4 =
                new Rect(screenPos.x - lightWidth/2 - lightWidth - pedestrianWidth + 5f*zoom,
                    screenPos.y - lightHeight/2, lightWidth, lightHeight);

            switch (segmentDict.LightMain)
            {
                case RoadBaseAI.TrafficLightState.Green:
                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightTexture2D);
                    break;
                case RoadBaseAI.TrafficLightState.Red:
                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightTexture2D);
                    break;
            }

            if (!myRect4.Contains(Event.current.mousePosition))
                return hoveredSegment;
            _hoveredButton[0] = segmentId;
            _hoveredButton[1] = 3;

            if (!Input.GetMouseButtonDown(0) || _uiClickedSegment)
                return true;

            _uiClickedSegment = true;
            segmentDict.ChangeLightMain();
            return true;
        }

        private bool IsPedestrianLightHovered(Rect myRect3, int segmentId, bool hoveredSegment, ManualSegmentLight segmentDict)
        {
            if (!myRect3.Contains(Event.current.mousePosition))
                return hoveredSegment;

            _hoveredButton[0] = segmentId;
            _hoveredButton[1] = 2;

            if (!Input.GetMouseButtonDown(0) || _uiClickedSegment)
                return true;
            _uiClickedSegment = true;

            if (!segmentDict.PedestrianEnabled)
            {
                segmentDict.ManualPedestrian();
            }
            else
            {
                segmentDict.ChangeLightPedestrian();
            }
            return true;
        }

        private bool RenderManualPedestrianLightSwitch(float zoom, int segmentId, Vector3 screenPos, float lightWidth,
            ManualSegmentLight segmentDict, bool hoveredSegment)
        {
            var guiColor = GUI.color;
            var manualPedestrianWidth = 36f*zoom;
            var manualPedestrianHeight = 35f*zoom;

            guiColor.a = _hoveredButton[0] == segmentId && (_hoveredButton[1] == 1 || _hoveredButton[1] == 2) ? 0.92f : 0.45f;

            GUI.color = guiColor;

            var myRect2 = new Rect(screenPos.x - manualPedestrianWidth/2 - lightWidth + 5f*zoom,
                screenPos.y - manualPedestrianHeight/2 - 9f*zoom, manualPedestrianWidth, manualPedestrianHeight);

            GUI.DrawTexture(myRect2, segmentDict.PedestrianEnabled ? TrafficLightToolTextureResources.PedestrianModeManualTexture2D : TrafficLightToolTextureResources.PedestrianModeAutomaticTexture2D);

            if (!myRect2.Contains(Event.current.mousePosition))
                return hoveredSegment;

            _hoveredButton[0] = segmentId;
            _hoveredButton[1] = 1;

            if (!Input.GetMouseButtonDown(0) || _uiClickedSegment)
                return true;

            _uiClickedSegment = true;
            segmentDict.ManualPedestrian();
            return true;
        }

        private bool RenderCounter(int segmentId, Vector3 screenPos, float modeWidth, float modeHeight, float zoom,
            ManualSegmentLight segmentDict, bool hoveredSegment)
        {
            SetAlpha(segmentId, 0);

            var myRectCounter = new Rect(screenPos.x - modeWidth/2, screenPos.y - modeHeight/2 - 6f*zoom, modeWidth, modeHeight);

            GUI.DrawTexture(myRectCounter, TrafficLightToolTextureResources.LightCounterTexture2D);

            var counterSize = 20f*zoom;

            var counter = segmentDict.LastChange;

            var myRectCounterNum = new Rect(screenPos.x - counterSize + 15f*zoom + (counter >= 10 ? -5*zoom : 0f),
                screenPos.y - counterSize + 11f*zoom, counterSize, counterSize);

            _counterStyle.fontSize = (int) (18f*zoom);
            _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

            GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

            if (!myRectCounter.Contains(Event.current.mousePosition))
                return hoveredSegment;
            _hoveredButton[0] = segmentId;
            _hoveredButton[1] = 0;
            return true;
        }

        private bool GetHoveredSegment(Rect myRect1, int segmentId, bool hoveredSegment, ManualSegmentLight segmentDict)
        {
            if (!myRect1.Contains(Event.current.mousePosition))
                return hoveredSegment;

            Debug.Log("mouse in myRect1");
            _hoveredButton[0] = segmentId;
            _hoveredButton[1] = -1;

            if (!Input.GetMouseButtonDown(0) || _uiClickedSegment)
                return true;
            _uiClickedSegment = true;
            segmentDict.ChangeMode();
            return true;
        }

        private static Vector3 CalculateNodePositionForSegment(NetNode node, NetSegment segment)
        {
            var position = node.m_position;

            const float offset = 25f;

            if (segment.m_startNode == SelectedNode)
            {
                position.x += segment.m_startDirection.x*offset;
                position.y += segment.m_startDirection.y*offset;
                position.z += segment.m_startDirection.z*offset;
            }
            else
            {
                position.x += segment.m_endDirection.x*offset;
                position.y += segment.m_endDirection.y*offset;
                position.z += segment.m_endDirection.z*offset;
            }
            return position;
        }

        private void _guiManualTrafficLightsCrosswalk(NetNode node)
        {
            var hoveredSegment = false;

            var segment1 = 0;
            var segment2 = 0;

            for (var i = 0; i < 8; i++)
            {
                var segmentId = node.GetSegment(i);

                if (segmentId == 0) continue;

                if (segment1 == 0)
                {
                    segment1 = segmentId;
                }
                else
                {
                    segment2 = segmentId;
                }
            }

            var segmentDict1 = TrafficLightsManual.GetSegmentLight(SelectedNode, segment1);
            var segmentDict2 = TrafficLightsManual.GetSegmentLight(SelectedNode, segment2);

            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segment1];

            var position = node.m_position;

            const float offset = 0f;

            if (segment.m_startNode == SelectedNode)
            {
                position.x += segment.m_startDirection.x * offset;
                position.y += segment.m_startDirection.y * offset;
                position.z += segment.m_startDirection.z * offset;
            }
            else
            {
                position.x += segment.m_endDirection.x * offset;
                position.y += segment.m_endDirection.y * offset;
                position.z += segment.m_endDirection.z * offset;
            }

            var guiColor = GUI.color;

            var screenPos = Camera.main.WorldToScreenPoint(position);
            screenPos.y = Screen.height - screenPos.y;

            var diff = position - Camera.main.transform.position;
            var zoom = 1.0f/diff.magnitude*100f;

            // original / 2.5
            var lightWidth = 41f*zoom;
            var lightHeight = 97f*zoom;

            // SWITCH PEDESTRIAN LIGHT
            var pedestrianWidth = 36f * zoom;
            var pedestrianHeight = 61f * zoom;

            guiColor.a = _hoveredButton[0] == segment1 && _hoveredButton[1] == 0 ? 0.92f : 0.45f;

            GUI.color = guiColor;

            var myRect3 = new Rect(screenPos.x - pedestrianWidth / 2 - lightWidth + 5f * zoom, screenPos.y - pedestrianHeight / 2 + 22f * zoom, pedestrianWidth, pedestrianHeight);

            switch (segmentDict1.LightPedestrian)
            {
                case RoadBaseAI.TrafficLightState.Green:
                    GUI.DrawTexture(myRect3, TrafficLightToolTextureResources.PedestrianGreenLightTexture2D);
                    break;
                case RoadBaseAI.TrafficLightState.Red:
                    GUI.DrawTexture(myRect3, TrafficLightToolTextureResources.PedestrianRedLightTexture2D);
                    break;
            }

            if (myRect3.Contains(Event.current.mousePosition))
            {
                _hoveredButton[0] = segment1;
                _hoveredButton[1] = 0;
                hoveredSegment = true;

                if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                {
                    _uiClickedSegment = true;
                }
            }

            // no arrow light
            guiColor.a = _hoveredButton[0] == segment1 && _hoveredButton[1] == 1 ? 0.92f : 0.45f;

            GUI.color = guiColor;

            var myRect4 =
                new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
                    screenPos.y - lightHeight / 2, lightWidth, lightHeight);

            switch (segmentDict1.LightMain)
            {
                case RoadBaseAI.TrafficLightState.Green:
                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightTexture2D);
                    break;
                case RoadBaseAI.TrafficLightState.Red:
                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightTexture2D);
                    break;
            }

            if (myRect4.Contains(Event.current.mousePosition))
            {
                _hoveredButton[0] = segment1;
                _hoveredButton[1] = 1;
                hoveredSegment = true;

                if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                {
                    _uiClickedSegment = true;
                    segmentDict1.ChangeLightMain();
                    segmentDict2.ChangeLightMain();
                }
            }

            if (hoveredSegment) return;

            _hoveredButton[0] = 0;
            _hoveredButton[1] = 0;
        }

        private void _guiTimedTrafficLights()
        {
            GUILayout.Window(253, _windowRect, _guiTimedControlPanel, "Timed traffic lights manager");

            IsCursorInSecondaryPanel();

            var hoveredSegment = false;

            foreach (var index in SelectedNodeIndexes)
            {
                var node = GetNetNode(index);

                var nodeSimulation = CustomRoadAI.GetNodeSimulation(index);

                for (var i = 0; i < 8; i++)
                {
                    int segmentId = node.GetSegment(i);

                    if (segmentId == 0 || nodeSimulation == null ||
                        !TrafficLightsManual.IsSegmentLight(index, segmentId)) continue;

                    var segmentDict = TrafficLightsManual.GetSegmentLight(index, segmentId);

                    var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];

                    var position = node.m_position;

                    var offset = 25f;

                    if (segment.m_startNode == index)
                    {
                        position.x += segment.m_startDirection.x * offset;
                        position.y += segment.m_startDirection.y * offset;
                        position.z += segment.m_startDirection.z * offset;
                    }
                    else
                    {
                        position.x += segment.m_endDirection.x * offset;
                        position.y += segment.m_endDirection.y * offset;
                        position.z += segment.m_endDirection.z * offset;
                    }

                    var guiColor = GUI.color;

                    var screenPos = Camera.main.WorldToScreenPoint(position);
                    screenPos.y = Screen.height - screenPos.y;

                    var diff = position - Camera.main.transform.position;
                    var zoom = 1.0f/diff.magnitude*100f;

                    var timedActive = nodeSimulation.TimedTrafficLightsActive;

                    // original / 2.5
                    var lightWidth = 41f*zoom;
                    var lightHeight = 97f*zoom;

                    // SWITCH MODE BUTTON
                    var modeWidth = 41f*zoom;
                    var modeHeight = 38f*zoom;

                    if (!timedActive && (_timedPanelAdd || _timedEditStep >= 0))
                    {
                        guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == -1 &&
                                     _hoveredNode == index
                            ? 0.92f
                            : 0.45f;

                        GUI.color = guiColor;

                        var myRect1 = new Rect(screenPos.x - modeWidth/2,
                            screenPos.y - modeHeight/2 + modeHeight - 7f*zoom, modeWidth, modeHeight);

                        GUI.DrawTexture(myRect1, TrafficLightToolTextureResources.LightModeTexture2D);

                        if (myRect1.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                        {
                            _hoveredButton[0] = segmentId;
                            _hoveredButton[1] = -1;
                            _hoveredNode = index;
                            hoveredSegment = true;

                            if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                            {
                                _uiClickedSegment = true;
                                segmentDict.ChangeMode();
                            }
                        }
                    }

                    // SWITCH MANUAL PEDESTRIAN LIGHT BUTTON
                    var manualPedestrianWidth = 36f*zoom;
                    var manualPedestrianHeight = 35f*zoom;

                    if (!timedActive && (_timedPanelAdd || _timedEditStep >= 0))
                    {
                        guiColor.a = _hoveredButton[0] == segmentId &&
                                     (_hoveredButton[1] == 1 || _hoveredButton[1] == 2) &&
                                     _hoveredNode == index
                            ? 0.92f
                            : 0.45f;

                        GUI.color = guiColor;

                        var myRect2 = new Rect(screenPos.x - manualPedestrianWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) + 5f * zoom,
                            screenPos.y - manualPedestrianHeight/2 - 9f*zoom, manualPedestrianWidth,
                            manualPedestrianHeight);

                        GUI.DrawTexture(myRect2, segmentDict.PedestrianEnabled ? TrafficLightToolTextureResources.PedestrianModeManualTexture2D : TrafficLightToolTextureResources.PedestrianModeAutomaticTexture2D);

                        if (myRect2.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                        {
                            _hoveredButton[0] = segmentId;
                            _hoveredButton[1] = 1;
                            _hoveredNode = index;
                            hoveredSegment = true;

                            if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                            {
                                _uiClickedSegment = true;
                                segmentDict.ManualPedestrian();
                            }
                        }
                    }

                    // SWITCH PEDESTRIAN LIGHT
                    var pedestrianWidth = 36f * zoom;
                    var pedestrianHeight = 61f * zoom;

                    guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 2 && _hoveredNode == index ? 0.92f : 0.45f;

                    GUI.color = guiColor;

                    var myRect3 = new Rect(screenPos.x - pedestrianWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) + 5f * zoom, screenPos.y - pedestrianHeight / 2 + 22f * zoom, pedestrianWidth, pedestrianHeight);

                    switch (segmentDict.LightPedestrian)
                    {
                        case RoadBaseAI.TrafficLightState.Green:
                            GUI.DrawTexture(myRect3, TrafficLightToolTextureResources.PedestrianGreenLightTexture2D);
                            break;
                        case RoadBaseAI.TrafficLightState.Red:
                            GUI.DrawTexture(myRect3, TrafficLightToolTextureResources.PedestrianRedLightTexture2D);
                            break;
                    }

                    if (myRect3.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                    {
                        _hoveredButton[0] = segmentId;
                        _hoveredButton[1] = 2;
                        _hoveredNode = index;
                        hoveredSegment = true;

                        if (Input.GetMouseButtonDown(0) && !_uiClickedSegment && !timedActive && (_timedPanelAdd || _timedEditStep >= 0))
                        {
                            _uiClickedSegment = true;

                            if (!segmentDict.PedestrianEnabled)
                            {
                                segmentDict.ManualPedestrian();
                            }
                            else
                            {
                                segmentDict.ChangeLightPedestrian();
                            }
                        }
                    }

                    // COUNTER
                    if (timedActive && _timedShowNumbers)
                    {
                        var counterSize = 20f * zoom;

                        var timedSegment = TrafficLightsTimed.GetTimedLight(index);

                        var counter = timedSegment.CheckNextChange(segmentId, 3);

                        float numOffset;

                        if (segmentDict.LightPedestrian == RoadBaseAI.TrafficLightState.Red)
                        {
                            numOffset = counterSize + 53f * zoom - modeHeight * 2;
                        }
                        else
                        {
                            numOffset = counterSize + 29f * zoom - modeHeight * 2;
                        }

                        var myRectCounterNum =
                            new Rect(screenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10*zoom : -5*zoom) : 1f) + 24f * zoom - pedestrianWidth/2,
                                screenPos.y - numOffset, counterSize, counterSize);

                        _counterStyle.fontSize = (int)(15f * zoom);
                        _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                        GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

                        if (myRectCounterNum.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                        {
                            _hoveredButton[0] = segmentId;
                            _hoveredButton[1] = 2;
                            _hoveredNode = index;
                            hoveredSegment = true;
                        }
                    }

                    if (TrafficLightsManual.SegmentIsIncomingOneWay(segmentId, index)) continue;

                    var hasLeftSegment = TrafficPriority.HasLeftSegment(segmentId, index) && TrafficPriority.HasLeftLane(index, segmentId);
                    var hasForwardSegment = TrafficPriority.HasForwardSegment(segmentId, index) && TrafficPriority.HasForwardLane(index, segmentId);
                    var hasRightSegment = TrafficPriority.HasRightSegment(segmentId, index) && TrafficPriority.HasRightLane(index, segmentId);

                    switch (segmentDict.CurrentMode)
                    {
                        case ManualSegmentLight.Mode.Simple:
                        {
                            // no arrow light
                            guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 3 && _hoveredNode == index ? 0.92f : 0.45f;

                            GUI.color = guiColor;

                            var myRect4 =
                                new Rect(screenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) - pedestrianWidth + 5f * zoom,
                                    screenPos.y - lightHeight/2, lightWidth, lightHeight);

                            if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightTexture2D);
                            else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightTexture2D);

                            if (myRect4.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                            {
                                _hoveredButton[0] = segmentId;
                                _hoveredButton[1] = 3;
                                _hoveredNode = index;
                                hoveredSegment = true;

                                if (Input.GetMouseButtonDown(0) && !_uiClickedSegment && !timedActive && (_timedPanelAdd || _timedEditStep >= 0))
                                {
                                    _uiClickedSegment = true;
                                    segmentDict.ChangeLightMain();
                                }
                            }

                            // COUNTER
                            if (timedActive && _timedShowNumbers)
                            {
                                var counterSize = 20f*zoom;

                                var timedSegment = TrafficLightsTimed.GetTimedLight(index);

                                var counter = timedSegment.CheckNextChange(segmentId, 0);

                                float numOffset;

                                if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                {
                                    numOffset = counterSize + 96f*zoom - modeHeight*2;
                                }
                                else
                                {
                                    numOffset = counterSize + 40f * zoom - modeHeight * 2;
                                }

                                var myRectCounterNum =
                                    new Rect(screenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) - pedestrianWidth + 5f * zoom,
                                        screenPos.y - numOffset, counterSize, counterSize);

                                _counterStyle.fontSize = (int) (18f*zoom);
                                _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

                                if (myRectCounterNum.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                                {
                                    _hoveredButton[0] = segmentId;
                                    _hoveredButton[1] = 3;
                                    _hoveredNode = index;
                                    hoveredSegment = true;
                                }
                            }

                            GUI.color = guiColor;
                        }
                            break;
                        case ManualSegmentLight.Mode.LeftForwardR:
                            if (hasLeftSegment)
                            {
                                // left arrow light
                                guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 3 && _hoveredNode == index ? 0.92f : 0.45f;

                                GUI.color = guiColor;

                                var myRect4 =
                                    new Rect(screenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth * 2 : lightWidth) - pedestrianWidth + 5f * zoom,
                                        screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Green)
                                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightLeftTexture2D);
                                else if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Red)
                                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightLeftTexture2D);

                                if (myRect4.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                                {
                                    _hoveredButton[0] = segmentId;
                                    _hoveredButton[1] = 3;
                                    _hoveredNode = index;
                                    hoveredSegment = true;

                                    if (Input.GetMouseButtonDown(0) && !_uiClickedSegment && !timedActive && (_timedPanelAdd || _timedEditStep >= 0))
                                    {
                                        _uiClickedSegment = true;
                                        segmentDict.ChangeLightLeft();
                                    }
                                }

                                // COUNTER
                                if (timedActive && _timedShowNumbers)
                                {
                                    var counterSize = 20f * zoom;

                                    var timedSegment = TrafficLightsTimed.GetTimedLight(index);

                                    var counter = timedSegment.CheckNextChange(segmentId, 1);

                                    float numOffset;

                                    if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Red)
                                    {
                                        numOffset = counterSize + 96f * zoom - modeHeight * 2;
                                    }
                                    else
                                    {
                                        numOffset = counterSize + 40f * zoom - modeHeight * 2;
                                    }

                                    var myRectCounterNum =
                                        new Rect(screenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) - pedestrianWidth + 5f * zoom - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth * 2 : lightWidth),
                                            screenPos.y - numOffset, counterSize, counterSize);

                                    _counterStyle.fontSize = (int)(18f * zoom);
                                    _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                    GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

                                    if (myRectCounterNum.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                                    {
                                        _hoveredButton[0] = segmentId;
                                        _hoveredButton[1] = 3;
                                        _hoveredNode = index;
                                        hoveredSegment = true;
                                    }
                                }
                            }

                            // forward-right arrow light
                            guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 4 && _hoveredNode == index ? 0.92f : 0.45f;

                            GUI.color = guiColor;

                            var myRect5 =
                                new Rect(screenPos.x - lightWidth / 2 - pedestrianWidth - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) + 5f * zoom,
                                    screenPos.y - lightHeight/2, lightWidth, lightHeight);

                            if (hasForwardSegment && hasRightSegment)
                            {
                                if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                    GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.GreenLightForwardRightTexture2D);
                                else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                    GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.RedLightForwardRightTexture2D);
                            }
                            else if (!hasRightSegment)
                            {
                                if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                    GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.GreenLightStraightTexture2D);
                                else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                    GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.RedLightStraightTexture2D);
                            }
                            else
                            {
                                if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                    GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.GreenLightRightTexture2D);
                                else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                    GUI.DrawTexture(myRect5, TrafficLightToolTextureResources.RedLightRightTexture2D);
                            }

                            if (myRect5.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                            {
                                _hoveredButton[0] = segmentId;
                                _hoveredButton[1] = 4;
                                _hoveredNode = index;
                                hoveredSegment = true;

                                if (Input.GetMouseButtonDown(0) && !_uiClickedSegment && !timedActive && (_timedPanelAdd || _timedEditStep >= 0))
                                {
                                    _uiClickedSegment = true;
                                    segmentDict.ChangeLightMain();
                                }
                            }

                            // COUNTER
                            if (timedActive && _timedShowNumbers)
                            {
                                var counterSize = 20f * zoom;

                                var timedSegment = TrafficLightsTimed.GetTimedLight(index);

                                var counter = timedSegment.CheckNextChange(segmentId, 0);

                                float numOffset;

                                if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                {
                                    numOffset = counterSize + 96f * zoom - modeHeight * 2;
                                }
                                else
                                {
                                    numOffset = counterSize + 40f * zoom - modeHeight * 2;
                                }

                                var myRectCounterNum =
                                    new Rect(screenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) - pedestrianWidth + 5f * zoom - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f),
                                        screenPos.y - numOffset, counterSize, counterSize);

                                _counterStyle.fontSize = (int)(18f * zoom);
                                _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

                                if (myRectCounterNum.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                                {
                                    _hoveredButton[0] = segmentId;
                                    _hoveredButton[1] = 4;
                                    _hoveredNode = index;
                                    hoveredSegment = true;
                                }
                            }
                            break;
                        case ManualSegmentLight.Mode.RightForwardL:
                        {
                            // forward-left light
                            guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 3 && _hoveredNode == index ? 0.92f : 0.45f;

                            GUI.color = guiColor;

                            var myRect4 = new Rect(screenPos.x - lightWidth/2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth*2 : lightWidth) - pedestrianWidth + 5f*zoom,
                                screenPos.y - lightHeight/2, lightWidth, lightHeight);

                            var lightType = 0;

                            if (hasForwardSegment && hasLeftSegment)
                            {
                                if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightForwardLeftTexture2D);
                                else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightForwardLeftTexture2D);

                                lightType = 1;
                            }
                            else if (!hasLeftSegment)
                            {
                                if (!hasRightSegment)
                                {
                                    myRect4 = new Rect(screenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
                                        screenPos.y - lightHeight / 2, lightWidth, lightHeight);
                                }

                                if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightStraightTexture2D);
                                else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightStraightTexture2D);
                            }
                            else
                            {
                                if (!hasRightSegment)
                                {
                                    myRect4 = new Rect(screenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
                                        screenPos.y - lightHeight / 2, lightWidth, lightHeight);
                                }

                                if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightLeftTexture2D);
                                else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightLeftTexture2D);
                            }


                            if (myRect4.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                            {
                                _hoveredButton[0] = segmentId;
                                _hoveredButton[1] = 3;
                                _hoveredNode = index;
                                hoveredSegment = true;

                                if (Input.GetMouseButtonDown(0) && !_uiClickedSegment && !timedActive && (_timedPanelAdd || _timedEditStep >= 0))
                                {
                                    _uiClickedSegment = true;
                                    segmentDict.ChangeLightMain();
                                }
                            }

                            // COUNTER
                            if (timedActive && _timedShowNumbers)
                            {
                                var counterSize = 20f * zoom;

                                var timedSegment = TrafficLightsTimed.GetTimedLight(index);

                                var counter = timedSegment.CheckNextChange(segmentId, lightType);

                                float numOffset;

                                if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                {
                                    numOffset = counterSize + 96f * zoom - modeHeight * 2;
                                }
                                else
                                {
                                    numOffset = counterSize + 40f * zoom - modeHeight * 2;
                                }

                                var myRectCounterNum =
                                    new Rect(screenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) - pedestrianWidth + 5f * zoom - (_timedPanelAdd || _timedEditStep >= 0 ? (hasRightSegment ? lightWidth * 2 : lightWidth) : (hasRightSegment ? lightWidth : 0f)),
                                        screenPos.y - numOffset, counterSize, counterSize);

                                _counterStyle.fontSize = (int)(18f * zoom);
                                _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

                                if (myRectCounterNum.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                                {
                                    _hoveredButton[0] = segmentId;
                                    _hoveredButton[1] = 3;
                                    _hoveredNode = index;
                                    hoveredSegment = true;
                                }
                            }

                            // right arrow light
                            if (hasRightSegment)
                            {
                                guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 4 &&
                                             _hoveredNode == index
                                    ? 0.92f
                                    : 0.45f;

                                GUI.color = guiColor;

                                var rect5 =
                                    new Rect(screenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
                                        screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                if (segmentDict.LightRight == RoadBaseAI.TrafficLightState.Green)
                                    GUI.DrawTexture(rect5, TrafficLightToolTextureResources.GreenLightRightTexture2D);
                                else if (segmentDict.LightRight == RoadBaseAI.TrafficLightState.Red)
                                    GUI.DrawTexture(rect5, TrafficLightToolTextureResources.RedLightRightTexture2D);


                                if (rect5.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                                {
                                    _hoveredButton[0] = segmentId;
                                    _hoveredButton[1] = 4;
                                    _hoveredNode = index;
                                    hoveredSegment = true;

                                    if (Input.GetMouseButtonDown(0) && !_uiClickedSegment && !timedActive &&
                                        (_timedPanelAdd || _timedEditStep >= 0))
                                    {
                                        _uiClickedSegment = true;
                                        segmentDict.ChangeLightRight();
                                    }
                                }

                                // COUNTER
                                if (timedActive && _timedShowNumbers)
                                {
                                    var counterSize = 20f*zoom;

                                    var timedSegment = TrafficLightsTimed.GetTimedLight(index);

                                    var counter = timedSegment.CheckNextChange(segmentId, 2);

                                    float numOffset;

                                    if (segmentDict.LightRight == RoadBaseAI.TrafficLightState.Red)
                                    {
                                        numOffset = counterSize + 96f*zoom - modeHeight*2;
                                    }
                                    else
                                    {
                                        numOffset = counterSize + 40f*zoom - modeHeight*2;
                                    }

                                    var myRectCounterNum =
                                        new Rect(
                                            screenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) -
                                            pedestrianWidth + 5f*zoom -
                                            (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f),
                                            screenPos.y - numOffset, counterSize, counterSize);

                                    _counterStyle.fontSize = (int) (18f*zoom);
                                    _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                    GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

                                    if (myRectCounterNum.Contains(Event.current.mousePosition) &&
                                        !_cursorInSecondaryPanel)
                                    {
                                        _hoveredButton[0] = segmentId;
                                        _hoveredButton[1] = 4;
                                        _hoveredNode = index;
                                        hoveredSegment = true;
                                    }
                                }
                            }
                        }
                            break;
                        default:
                            // left arrow light
                            if (hasLeftSegment)
                            {
                                guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 3 && _hoveredNode == index ? 0.92f : 0.45f;

                                GUI.color = guiColor;

                                var offsetLight = lightWidth;

                                if (hasRightSegment)
                                    offsetLight += lightWidth;

                                if (hasForwardSegment)
                                    offsetLight += lightWidth;

                                var myRect4 =
                                    new Rect(screenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth) - pedestrianWidth + 5f * zoom,
                                        screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Green)
                                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.GreenLightLeftTexture2D);
                                else if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Red)
                                    GUI.DrawTexture(myRect4, TrafficLightToolTextureResources.RedLightLeftTexture2D);

                                if (myRect4.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                                {
                                    _hoveredButton[0] = segmentId;
                                    _hoveredButton[1] = 3;
                                    _hoveredNode = index;
                                    hoveredSegment = true;

                                    if (Input.GetMouseButtonDown(0) && !_uiClickedSegment && !timedActive && (_timedPanelAdd || _timedEditStep >= 0))
                                    {
                                        _uiClickedSegment = true;
                                        segmentDict.ChangeLightLeft();
                                    }
                                }

                                // COUNTER
                                if (timedActive && _timedShowNumbers)
                                {
                                    var counterSize = 20f * zoom;

                                    var timedSegment = TrafficLightsTimed.GetTimedLight(index);

                                    var counter = timedSegment.CheckNextChange(segmentId, 1);

                                    float numOffset;

                                    if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Red)
                                    {
                                        numOffset = counterSize + 96f * zoom - modeHeight * 2;
                                    }
                                    else
                                    {
                                        numOffset = counterSize + 40f * zoom - modeHeight * 2;
                                    }

                                    var myRectCounterNum =
                                        new Rect(
                                            screenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) -
                                            pedestrianWidth + 5f * zoom -
                                            (_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth),
                                            screenPos.y - numOffset, counterSize, counterSize);

                                    _counterStyle.fontSize = (int)(18f * zoom);
                                    _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                    GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

                                    if (myRectCounterNum.Contains(Event.current.mousePosition) &&
                                        !_cursorInSecondaryPanel)
                                    {
                                        _hoveredButton[0] = segmentId;
                                        _hoveredButton[1] = 3;
                                        _hoveredNode = index;
                                        hoveredSegment = true;
                                    }
                                }
                            }

                            // forward arrow light
                            if (hasForwardSegment)
                            {
                                guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 4 && _hoveredNode == index ? 0.92f : 0.45f;

                                GUI.color = guiColor;

                                var offsetLight = lightWidth;

                                if (hasRightSegment)
                                    offsetLight += lightWidth;

                                var myRect6 =
                                    new Rect(screenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth) - pedestrianWidth + 5f * zoom,
                                        screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                    GUI.DrawTexture(myRect6, TrafficLightToolTextureResources.GreenLightStraightTexture2D);
                                else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                    GUI.DrawTexture(myRect6, TrafficLightToolTextureResources.RedLightStraightTexture2D);

                                if (myRect6.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                                {
                                    _hoveredButton[0] = segmentId;
                                    _hoveredButton[1] = 4;
                                    _hoveredNode = index;
                                    hoveredSegment = true;

                                    if (Input.GetMouseButtonDown(0) && !_uiClickedSegment && !timedActive && (_timedPanelAdd || _timedEditStep >= 0))
                                    {
                                        _uiClickedSegment = true;
                                        segmentDict.ChangeLightMain();
                                    }
                                }

                                // COUNTER
                                if (timedActive && _timedShowNumbers)
                                {
                                    var counterSize = 20f * zoom;

                                    var timedSegment = TrafficLightsTimed.GetTimedLight(index);

                                    var counter = timedSegment.CheckNextChange(segmentId, 0);

                                    float numOffset;

                                    if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                    {
                                        numOffset = counterSize + 96f * zoom - modeHeight * 2;
                                    }
                                    else
                                    {
                                        numOffset = counterSize + 40f * zoom - modeHeight * 2;
                                    }

                                    var myRectCounterNum =
                                        new Rect(
                                            screenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) -
                                            pedestrianWidth + 5f * zoom -
                                            (_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth),
                                            screenPos.y - numOffset, counterSize, counterSize);

                                    _counterStyle.fontSize = (int)(18f * zoom);
                                    _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                    GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

                                    if (myRectCounterNum.Contains(Event.current.mousePosition) &&
                                        !_cursorInSecondaryPanel)
                                    {
                                        _hoveredButton[0] = segmentId;
                                        _hoveredButton[1] = 4;
                                        _hoveredNode = index;
                                        hoveredSegment = true;
                                    }
                                }
                            }

                            // right arrow light
                            if (hasRightSegment)
                            {
                                guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 5 && _hoveredNode == index ? 0.92f : 0.45f;

                                GUI.color = guiColor;

                                var rect6 =
                                    new Rect(screenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
                                        screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                if (segmentDict.LightRight == RoadBaseAI.TrafficLightState.Green)
                                    GUI.DrawTexture(rect6, TrafficLightToolTextureResources.GreenLightRightTexture2D);
                                else if (segmentDict.LightRight == RoadBaseAI.TrafficLightState.Red)
                                    GUI.DrawTexture(rect6, TrafficLightToolTextureResources.RedLightRightTexture2D);

                                if (rect6.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
                                {
                                    _hoveredButton[0] = segmentId;
                                    _hoveredButton[1] = 5;
                                    _hoveredNode = index;
                                    hoveredSegment = true;

                                    if (Input.GetMouseButtonDown(0) && !_uiClickedSegment && !timedActive && (_timedPanelAdd || _timedEditStep >= 0))
                                    {
                                        _uiClickedSegment = true;
                                        segmentDict.ChangeLightRight();
                                    }
                                }

                                // COUNTER
                                if (timedActive && _timedShowNumbers)
                                {
                                    var counterSize = 20f * zoom;

                                    var timedSegment = TrafficLightsTimed.GetTimedLight(index);

                                    var counter = timedSegment.CheckNextChange(segmentId, 2);

                                    float numOffset;

                                    if (segmentDict.LightRight == RoadBaseAI.TrafficLightState.Red)
                                    {
                                        numOffset = counterSize + 96f * zoom - modeHeight * 2;
                                    }
                                    else
                                    {
                                        numOffset = counterSize + 40f * zoom - modeHeight * 2;
                                    }

                                    var myRectCounterNum =
                                        new Rect(
                                            screenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) -
                                            pedestrianWidth + 5f * zoom -
                                            (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f),
                                            screenPos.y - numOffset, counterSize, counterSize);

                                    _counterStyle.fontSize = (int)(18f * zoom);
                                    _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                    GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

                                    if (myRectCounterNum.Contains(Event.current.mousePosition) &&
                                        !_cursorInSecondaryPanel)
                                    {
                                        _hoveredButton[0] = segmentId;
                                        _hoveredButton[1] = 5;
                                        _hoveredNode = index;
                                        hoveredSegment = true;
                                    }
                                }
                            }
                            break;
                    }
                }
            }

            if (!hoveredSegment)
            {
                _hoveredButton[0] = 0;
                _hoveredButton[1] = 0;
            }
        }

        private void IsCursorInSecondaryPanel()
        {
            _cursorInSecondaryPanel = _windowRect.Contains(Event.current.mousePosition);
        }

        private void _guiLaneChange()
        {
            if (SelectedNode == 0 || SelectedSegment == 0) return;
            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegment];

            var info = segment.Info;
            var num2 = segment.m_lanes;
            var num3 = 0;

            var dir = NetInfo.Direction.Forward;
            if (segment.m_startNode == SelectedNode)
                dir = NetInfo.Direction.Backward;
            var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
            var dir3 = TrafficPriority.LeftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

            var numLanes = 0;

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian &&
                    info.m_lanes[num3].m_direction == dir3)
                {
                    numLanes++;
                }

                num2 = Singleton<NetManager>.instance.m_lanes.m_buffer[(int) ((UIntPtr) num2)].m_nextLane;
                num3++;
            }

            if (numLanes == 0)
            {
                SelectedNode = 0;
                SelectedSegment = 0;
                return;
            }

            var style = new GUIStyle
            {
                normal = {background = _secondPanelTexture},
                alignment = TextAnchor.MiddleCenter,
                border =
                {
                    bottom = 2,
                    top = 2,
                    right = 2,
                    left = 2
                }
            };

            var windowRect3 = ResizeGUI(new Rect(120, 45, numLanes * 118, 60));

            GUILayout.Window(250, windowRect3, _guiLaneChangeWindow, "", style);

            _cursorInSecondaryPanel = windowRect3.Contains(Event.current.mousePosition);
        }

        private void _guiLaneChangeWindow(int num)
        {
            var instance = Singleton<NetManager>.instance;

            var segment = instance.m_segments.m_buffer[SelectedSegment];

            var info = segment.Info;

            var num2 = segment.m_lanes;
            var num3 = 0;

            var laneList = new List<float[]>();

            var dir = NetInfo.Direction.Forward;
            if (segment.m_startNode == SelectedNode)
                dir = NetInfo.Direction.Backward;
            var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
            var dir3 = TrafficPriority.LeftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

            var maxValue = 0f;

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian && info.m_lanes[num3].m_laneType != NetInfo.LaneType.Parking && info.m_lanes[num3].m_laneType != NetInfo.LaneType.None &&
                    info.m_lanes[num3].m_direction == dir3)
                {
                    laneList.Add(new[] { num2, info.m_lanes[num3].m_position, num3 });
                    maxValue = Mathf.Max(maxValue, info.m_lanes[num3].m_position);
                }

                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num3++;
            }

            if (!TrafficLightsManual.SegmentIsOneWay(SelectedSegment))
            {
                laneList.Sort(delegate(float[] x, float[] y)
                {
                    if (!TrafficPriority.LeftHandDrive)
                    {
                        if (Mathf.Abs(y[1]) > Mathf.Abs(x[1]))
                        {
                            return -1;
                        }
                        return 1;
                    }
                    if (Mathf.Abs(x[1]) > Mathf.Abs(y[1]))
                    {
                        return -1;
                    }
                    return 1;
                });
            }
            else
            {
                laneList.Sort(delegate(float[] x, float[] y)
                {
                    if (!TrafficPriority.LeftHandDrive)
                    {
                        if (dir3 == NetInfo.Direction.Forward)
                        {
                            if (y[1] + maxValue > x[1] + maxValue)
                            {
                                return -1;
                            }
                            return 1;
                        }
                        if (x[1] + maxValue > y[1] + maxValue)
                        {
                            return -1;
                        }
                        return 1;
                    }
                    if (dir3 == NetInfo.Direction.Forward)
                    {
                        if (x[1] + maxValue > y[1] + maxValue)
                        {
                            return -1;
                        }
                        return 1;
                    }
                    if (y[1] + maxValue > x[1] + maxValue)
                    {
                        return -1;
                    }
                    return 1;
                });
            }

            GUILayout.BeginHorizontal();

            for (var i = 0; i < laneList.Count; i++)
            {
                var flags = (NetLane.Flags) Singleton<NetManager>.instance.m_lanes.m_buffer[(int)laneList[i][0]].m_flags;

                var style1 = new GUIStyle("button");
                var style2 = new GUIStyle("button")
                {
                    normal = {textColor = new Color32(255, 0, 0, 255)},
                    hover = {textColor = new Color32(255, 0, 0, 255)},
                    focused = {textColor = new Color32(255, 0, 0, 255)}
                };

                var laneStyle = new GUIStyle {contentOffset = new Vector2(12f, 0f)};

                var laneTitleStyle = new GUIStyle
                {
                    contentOffset = new Vector2(36f, 2f),
                    normal = {textColor = new Color(1f, 1f, 1f)}
                };

                GUILayout.BeginVertical(laneStyle);
                GUILayout.Label("Lane " + (i + 1), laneTitleStyle);
                    GUILayout.BeginVertical();
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("←", ((flags & NetLane.Flags.Left) == NetLane.Flags.Left ? style1 : style2), GUILayout.Width(35), GUILayout.Height(25)))
                            {
                                LaneFlag((uint)laneList[i][0], NetLane.Flags.Left);
                            }
                        if (GUILayout.Button("↑", ((flags & NetLane.Flags.Forward) == NetLane.Flags.Forward ? style1 : style2), GUILayout.Width(25), GUILayout.Height(35)))
                            {
                                LaneFlag((uint)laneList[i][0], NetLane.Flags.Forward);
                            }
                        if (GUILayout.Button("→", ((flags & NetLane.Flags.Right) == NetLane.Flags.Right ? style1 : style2), GUILayout.Width(35), GUILayout.Height(25)))
                            {
                                LaneFlag((uint)laneList[i][0], NetLane.Flags.Right);
                            }
                        GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();
        }

        private void _guiLaneRestrictions()
        {
            if (_selectedSegmentIndexes.Count < 1)
            {
                return;
            }

            var instance = Singleton<NetManager>.instance;

            var segment2 = instance.m_segments.m_buffer[_selectedSegmentIndexes[0]];

            var info2 = segment2.Info;

            var num2 = segment2.m_lanes;
            const int num3 = 0;

            var numLanes = 0;

            // TODO - I think this needs to be rewritten. Doesn't make sense.
            while (num3 < info2.m_lanes.Length && num2 != 0u)
            {
                if (info2.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian &&
                    info2.m_lanes[num3].m_laneType != NetInfo.LaneType.Parking &&
                    info2.m_lanes[num3].m_laneType != NetInfo.LaneType.None)
                {
                    numLanes++;
                }


                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
            }

            var style = new GUIStyle
            {
                normal = {background = _secondPanelTexture},
                alignment = TextAnchor.MiddleCenter,
                border =
                {
                    bottom = 2,
                    top = 2,
                    right = 2,
                    left = 2
                }
            };

            var width = !TrafficRoadRestrictions.IsSegment(_selectedSegmentIndexes[0]) ? 120 : numLanes*120;

            var windowRect3 = new Rect(275, 80, width, 185);

            if (TrafficLightsManual.SegmentIsOneWay(SelectedSegment))
            {
                GUILayout.Window(251, windowRect3, _guiLaneRestrictionsOneWayWindow, "", style);
            }

            _cursorInSecondaryPanel = windowRect3.Contains(Event.current.mousePosition);
        }

        private int _setSpeed = -1;

        private void _guiLaneRestrictionsOneWayWindow(int num)
        {
            if (!TrafficRoadRestrictions.IsSegment(_selectedSegmentIndexes[0]))
            {
                if (!GUILayout.Button("Create group")) return;

                foreach (var segmentIndex in _selectedSegmentIndexes)
                {
                    TrafficRoadRestrictions.AddSegment(segmentIndex, _selectedSegmentIndexes);

                    var instance0 = Singleton<NetManager>.instance;

                    var segment0 = instance0.m_segments.m_buffer[segmentIndex];

                    var info0 = segment0.Info;

                    var num20 = segment0.m_lanes;
                    var num30 = 0;

                    var restSegment = TrafficRoadRestrictions.GetSegment(segmentIndex);

                    var laneList0 = new List<float[]>();
                    var maxValue0 = 0f;

                    while (num30 < info0.m_lanes.Length && num20 != 0u)
                    {
                        if (info0.m_lanes[num30].m_laneType != NetInfo.LaneType.Pedestrian &&
                            info0.m_lanes[num30].m_laneType != NetInfo.LaneType.Parking &&
                            info0.m_lanes[num30].m_laneType != NetInfo.LaneType.None)
                        {
                            laneList0.Add(new[] { num20, info0.m_lanes[num30].m_position, num30});
                            maxValue0 = Mathf.Max(maxValue0, info0.m_lanes[num30].m_position);
                        }

                        num20 = instance0.m_lanes.m_buffer[(int)((UIntPtr)num20)].m_nextLane;
                        num30++;
                    }

                    if (!TrafficLightsManual.SegmentIsOneWay(segmentIndex))
                    {
                        laneList0.Sort(delegate(float[] x, float[] y)
                        {
                            if (Mathf.Abs(y[1]) > Mathf.Abs(x[1]))
                            {
                                return -1;
                            }

                            return 1;
                        });
                    }
                    else
                    {
                        laneList0.Sort(delegate(float[] x, float[] y)
                        {
                            if (x[1] + maxValue0 > y[1] + maxValue0)
                            {
                                return -1;
                            }
                            return 1;
                        });
                    }

                    foreach (var lane in laneList0)
                    {
                        restSegment.AddLane((uint)lane[0], (int)lane[2], info0.m_lanes[(int)lane[2]].m_finalDirection);
                    }
                }
                return;
            }

            if (GUILayout.Button("Delete group"))
            {
                foreach (var selectedSegmentIndex in _selectedSegmentIndexes)
                {
                    TrafficRoadRestrictions.RemoveSegment(selectedSegmentIndex);
                }

                _selectedSegmentIndexes.Clear();
                return;
            }

            if (GUILayout.Button("Add zoning", GUILayout.Width(140)))
            {
                foreach (var selectedSegmentIndex in _selectedSegmentIndexes)
                {
                    var segment = Singleton<NetManager>.instance.m_segments.m_buffer[selectedSegmentIndex];
                    var info = segment.Info;

                    CreateZoneBlocks(selectedSegmentIndex, ref Singleton<NetManager>.instance.m_segments.m_buffer[selectedSegmentIndex], info);
                }
            }

            if (GUILayout.Button("Remove zoning", GUILayout.Width(140)))
            {
                foreach (var selectedSegmentIndex in _selectedSegmentIndexes)
                {
                    var segment = Singleton<NetManager>.instance.m_segments.m_buffer[selectedSegmentIndex];

                    Singleton<ZoneManager>.instance.ReleaseBlock(segment.m_blockStartLeft);
                    Singleton<ZoneManager>.instance.ReleaseBlock(segment.m_blockStartRight);
                    Singleton<ZoneManager>.instance.ReleaseBlock(segment.m_blockEndLeft);
                    Singleton<ZoneManager>.instance.ReleaseBlock(segment.m_blockEndRight);

                    Singleton<NetManager>.instance.m_segments.m_buffer[selectedSegmentIndex].m_blockStartLeft = 0;
                    Singleton<NetManager>.instance.m_segments.m_buffer[selectedSegmentIndex].m_blockStartRight = 0;
                    Singleton<NetManager>.instance.m_segments.m_buffer[selectedSegmentIndex].m_blockEndLeft = 0;
                    Singleton<NetManager>.instance.m_segments.m_buffer[selectedSegmentIndex].m_blockEndRight = 0;
                }
            }

            var instance = Singleton<NetManager>.instance;

            var segment2 = instance.m_segments.m_buffer[_selectedSegmentIndexes[0]];

            var info2 = segment2.Info;

            var num2 = segment2.m_lanes;
            var num3 = 0;

            var laneList = new List<float[]>();

            var maxValue = 0f;

            while (num3 < info2.m_lanes.Length && num2 != 0u)
            {
                if (info2.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian &&
                    info2.m_lanes[num3].m_laneType != NetInfo.LaneType.Parking &&
                    info2.m_lanes[num3].m_laneType != NetInfo.LaneType.None)
                {
                    laneList.Add(new[] {num2, info2.m_lanes[num3].m_position, num3});
                    maxValue = Mathf.Max(maxValue, info2.m_lanes[num3].m_position);
                }

                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num3++;
            }

            if (!TrafficLightsManual.SegmentIsOneWay(_selectedSegmentIndexes[0]))
            {
                laneList.Sort(delegate(float[] x, float[] y)
                {
                    if (Mathf.Abs(y[1]) > Mathf.Abs(x[1]))
                    {
                        return -1;
                    }

                    return 1;
                });
            }
            else
            {
                laneList.Sort(delegate(float[] x, float[] y)
                {
                    if (x[1] + maxValue > y[1] + maxValue)
                    {
                        return -1;
                    }
                    return 1;
                });
            }

            GUILayout.BeginHorizontal();
            for (var i = 0; i < laneList.Count; i++)
            {
                GUILayout.BeginVertical();
                GUILayout.Label("Lane " + (i+1));

                if (info2.m_lanes[(int) laneList[i][2]].m_laneType == NetInfo.LaneType.Vehicle)
                {
                    var resSegment = TrafficRoadRestrictions.GetSegment(_selectedSegmentIndexes[0]);
                    var resSpeed = resSegment.SpeedLimits[(int) laneList[i][2]];

                    if (_setSpeed == (int)laneList[i][2])
                    {
                        _sliderValues[(int) laneList[i][2]] =
                            GUILayout.HorizontalSlider(_sliderValues[(int) laneList[i][2]],
                                20f, 150f, GUILayout.Height(20));

                        if (GUILayout.Button("Set Speed " + (int)_sliderValues[(int) laneList[i][2]]))
                        {
                            foreach (var restrictionSegment in _selectedSegmentIndexes.Select(TrafficRoadRestrictions.GetSegment))
                            {
                                restrictionSegment.SpeedLimits[(int) laneList[i][2]] =
                                    _sliderValues[(int) laneList[i][2]]/
                                    50f;
                            }

                            _setSpeed = -1;
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Max speed " + (int)(resSpeed > 0.1f ? resSpeed*50f : info2.m_lanes[(int) laneList[i][2]].m_speedLimit*50f)))
                        {
                            _sliderValues[(int) laneList[i][2]] = info2.m_lanes[(int) laneList[i][2]].m_speedLimit*50f;
                            _setSpeed = (int) laneList[i][2];
                        }
                    }

                    //if (GUILayout.Button(lane.enableCars ? "Disable cars" : "Enable cars", lane.enableCars ? style1 : style2))
                    //{
                    //    lane.toggleCars();
                    //}
                    //if (GUILayout.Button(lane.enableCargo ? "Disable cargo" : "Enable cargo", lane.enableCargo ? style1 : style2))
                    //{
                    //    lane.toggleCargo();
                    //}
                    //if (GUILayout.Button(lane.enableService ? "Disable service" : "Enable service", lane.enableService ? style1 : style2))
                    //{
                    //    lane.toggleService();
                    //}
                    //if (GUILayout.Button(lane.enableTransport ? "Disable transport" : "Enable transport", lane.enableTransport ? style1 : style2))
                    //{
                    //    lane.toggleTransport();
                    //}
                }

                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
        }

        private void LaneFlag(uint laneId, NetLane.Flags flag)
        {
            if (!TrafficPriority.IsPrioritySegment(SelectedNode, SelectedSegment))
            {
                TrafficPriority.AddPrioritySegment(SelectedNode, SelectedSegment,
                    PrioritySegment.PriorityType.None);
            }

            var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags;

            if ((flags & flag) == flag)
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags = (ushort) (flags & ~flag);
            }
            else
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags = (ushort)(flags | flag);
            }
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            var pix = new Color[width * height];

            for (var i = 0; i < pix.Length; i++)
                pix[i] = col;

            var result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }

        private void CreateZoneBlocks(int segment, ref NetSegment data, NetInfo info)
        {
            var instance = Singleton<NetManager>.instance;
            var randomizer = new Randomizer(segment);
            var position = instance.m_nodes.m_buffer[data.m_startNode].m_position;
            var position2 = instance.m_nodes.m_buffer[data.m_endNode].m_position;
            var startDirection = data.m_startDirection;
            var endDirection = data.m_endDirection;
            var num = startDirection.x * endDirection.x + startDirection.z * endDirection.z;
            var flag = !NetSegment.IsStraight(position, startDirection, position2, endDirection);
            var num2 = Mathf.Max(8f, info.m_halfWidth);
            var num3 = 32f;
            if (flag)
            {
                var num4 = VectorUtils.LengthXZ(position2 - position);
                var flag2 = startDirection.x * endDirection.z - startDirection.z * endDirection.x > 0f;
                var flag3 = num < -0.8f || num4 > 50f;
                if (flag2)
                {
                    num2 = -num2;
                    num3 = -num3;
                }
                var vector = position - new Vector3(startDirection.z, 0f, -startDirection.x) * num2;
                var vector2 = position2 + new Vector3(endDirection.z, 0f, -endDirection.x) * num2;
                Vector3 vector3;
                Vector3 vector4;
                NetSegment.CalculateMiddlePoints(vector, startDirection, vector2, endDirection, true, true, out vector3, out vector4);
                if (flag3)
                {
                    var num5 = num * 0.025f + 0.04f;
                    var num6 = num * 0.025f + 0.06f;
                    if (num < -0.9f)
                    {
                        num6 = num5;
                    }
                    var bezier = new Bezier3(vector, vector3, vector4, vector2);
                    vector = bezier.Position(num5);
                    vector3 = bezier.Position(0.5f - num6);
                    vector4 = bezier.Position(0.5f + num6);
                    vector2 = bezier.Position(1f - num5);
                }
                else
                {
                    var bezier2 = new Bezier3(vector, vector3, vector4, vector2);
                    vector3 = bezier2.Position(0.86f);
                    vector = bezier2.Position(0.14f);
                }
                float num7;
                var vector5 = VectorUtils.NormalizeXZ(vector3 - vector, out num7);
                var num8 = Mathf.FloorToInt(num7 / 8f + 0.01f);
                var num9 = num7 * 0.5f + (num8 - 8) * ((!flag2) ? -4f : 4f);
                if (num8 != 0)
                {
                    var angle = (!flag2) ? Mathf.Atan2(vector5.x, -vector5.z) : Mathf.Atan2(-vector5.x, vector5.z);
                    var position3 = vector + new Vector3(vector5.x * num9 - vector5.z * num3, 0f, vector5.z * num9 + vector5.x * num3);
                    if (flag2)
                    {
                        Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartRight, ref randomizer, position3, angle, num8, data.m_buildIndex);
                    }
                    else
                    {
                        Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartLeft, ref randomizer, position3, angle, num8, data.m_buildIndex);
                    }
                }
                if (flag3)
                {
                    vector5 = VectorUtils.NormalizeXZ(vector2 - vector4, out num7);
                    num8 = Mathf.FloorToInt(num7 / 8f + 0.01f);
                    num9 = num7 * 0.5f + (num8 - 8) * ((!flag2) ? -4f : 4f);
                    if (num8 != 0)
                    {
                        var angle2 = (!flag2) ? Mathf.Atan2(vector5.x, -vector5.z) : Mathf.Atan2(-vector5.x, vector5.z);
                        var position4 = vector4 + new Vector3(vector5.x * num9 - vector5.z * num3, 0f, vector5.z * num9 + vector5.x * num3);
                        if (flag2)
                        {
                            Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndRight, ref randomizer, position4, angle2, num8, data.m_buildIndex + 1u);
                        }
                        else
                        {
                            Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndLeft, ref randomizer, position4, angle2, num8, data.m_buildIndex + 1u);
                        }
                    }
                }
                var vector6 = position + new Vector3(startDirection.z, 0f, -startDirection.x) * num2;
                var vector7 = position2 - new Vector3(endDirection.z, 0f, -endDirection.x) * num2;
                Vector3 b;
                Vector3 c;
                NetSegment.CalculateMiddlePoints(vector6, startDirection, vector7, endDirection, true, true, out b, out c);
                var bezier3 = new Bezier3(vector6, b, c, vector7);
                var vector8 = bezier3.Position(0.5f);
                var vector9 = bezier3.Position(0.25f);
                vector9 = Line2.Offset(VectorUtils.XZ(vector6), VectorUtils.XZ(vector8), VectorUtils.XZ(vector9));
                var vector10 = bezier3.Position(0.75f);
                vector10 = Line2.Offset(VectorUtils.XZ(vector7), VectorUtils.XZ(vector8), VectorUtils.XZ(vector10));
                var vector11 = vector6;
                var a = vector7;
                float d;
                float num10;
                if (Line2.Intersect(VectorUtils.XZ(position), VectorUtils.XZ(vector6), VectorUtils.XZ(vector11 - vector9), VectorUtils.XZ(vector8 - vector9), out d, out num10))
                {
                    vector6 = position + (vector6 - position) * d;
                }
                if (Line2.Intersect(VectorUtils.XZ(position2), VectorUtils.XZ(vector7), VectorUtils.XZ(a - vector10), VectorUtils.XZ(vector8 - vector10), out d, out num10))
                {
                    vector7 = position2 + (vector7 - position2) * d;
                }
                if (Line2.Intersect(VectorUtils.XZ(vector11 - vector9), VectorUtils.XZ(vector8 - vector9), VectorUtils.XZ(a - vector10), VectorUtils.XZ(vector8 - vector10), out d, out num10))
                {
                    vector8 = vector11 - vector9 + (vector8 - vector11) * d;
                }
                float num11;
                var vector12 = VectorUtils.NormalizeXZ(vector8 - vector6, out num11);
                var num12 = Mathf.FloorToInt(num11 / 8f + 0.01f);
                var num13 = num11 * 0.5f + (num12 - 8) * ((!flag2) ? 4f : -4f);
                if (num12 != 0)
                {
                    var angle3 = (!flag2) ? Mathf.Atan2(-vector12.x, vector12.z) : Mathf.Atan2(vector12.x, -vector12.z);
                    var position5 = vector6 + new Vector3(vector12.x * num13 + vector12.z * num3, 0f, vector12.z * num13 - vector12.x * num3);
                    if (flag2)
                    {
                        Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartLeft, ref randomizer, position5, angle3, num12, data.m_buildIndex);
                    }
                    else
                    {
                        Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartRight, ref randomizer, position5, angle3, num12, data.m_buildIndex);
                    }
                }
                vector12 = VectorUtils.NormalizeXZ(vector7 - vector8, out num11);
                num12 = Mathf.FloorToInt(num11 / 8f + 0.01f);
                num13 = num11 * 0.5f + (num12 - 8) * ((!flag2) ? 4f : -4f);

                if (num12 == 0) return;

                var angle4 = (!flag2) ? Mathf.Atan2(-vector12.x, vector12.z) : Mathf.Atan2(vector12.x, -vector12.z);
                var position6 = vector8 + new Vector3(vector12.x * num13 + vector12.z * num3, 0f, vector12.z * num13 - vector12.x * num3);
                if (flag2)
                {
                    Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndLeft, ref randomizer, position6, angle4, num12, data.m_buildIndex + 1u);
                }
                else
                {
                    Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndRight, ref randomizer, position6, angle4, num12, data.m_buildIndex + 1u);
                }
            }
            else
            {
                num2 += num3;
                var vector13 = new Vector2(position2.x - position.x, position2.z - position.z);
                var magnitude = vector13.magnitude;
                var num14 = Mathf.FloorToInt(magnitude / 8f + 0.1f);
                var num15 = (num14 <= 8) ? num14 : (num14 + 1 >> 1);
                var num16 = (num14 <= 8) ? 0 : (num14 >> 1);
                if (num15 > 0)
                {
                    var num17 = Mathf.Atan2(startDirection.x, -startDirection.z);
                    var position7 = position + new Vector3(startDirection.x * 32f - startDirection.z * num2, 0f, startDirection.z * 32f + startDirection.x * num2);
                    Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartLeft, ref randomizer, position7, num17, num15, data.m_buildIndex);
                    position7 = position + new Vector3(startDirection.x * (num15 - 4) * 8f + startDirection.z * num2, 0f, startDirection.z * (num15 - 4) * 8f - startDirection.x * num2);
                    Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartRight, ref randomizer, position7, num17 + 3.14159274f, num15, data.m_buildIndex);
                }

                if (num16 <= 0) return;

                var num18 = magnitude - num14 * 8f;
                var num19 = Mathf.Atan2(endDirection.x, -endDirection.z);
                var position8 = position2 + new Vector3(endDirection.x * (32f + num18) - endDirection.z * num2, 0f, endDirection.z * (32f + num18) + endDirection.x * num2);
                Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndLeft, ref randomizer, position8, num19, num16, data.m_buildIndex + 1u);
                position8 = position2 + new Vector3(endDirection.x * ((num16 - 4) * 8f + num18) + endDirection.z * num2, 0f, endDirection.z * ((num16 - 4) * 8f + num18) - endDirection.x * num2);
                Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndRight, ref randomizer, position8, num19 + 3.14159274f, num16, data.m_buildIndex + 1u);
            }
        }

        private void _guiTimedTrafficLightsNode()
        {
            GUILayout.Window(252, _windowRect2, _guiTimedTrafficLightsNodeWindow, "Select nodes");

            _cursorInSecondaryPanel = _windowRect2.Contains(Event.current.mousePosition);
        }

        private void _guiTimedTrafficLightsNodeWindow(int num)
        {
            if (SelectedNodeIndexes.Count < 1)
            {
                GUILayout.Label("Select nodes");
            }
            else
            {
                var txt = SelectedNodeIndexes.Aggregate("", (current, t) => current + ("Node " + t + "\n"));

                GUILayout.Label(txt);

                if (!GUILayout.Button("Next")) return;

                foreach (var selectedNodeIndex in SelectedNodeIndexes)
                {
                    var node2 = GetNetNode(selectedNodeIndex);
                    CustomRoadAI.AddNodeToSimulation(selectedNodeIndex);
                    var nodeSimulation = CustomRoadAI.GetNodeSimulation(selectedNodeIndex);
                    nodeSimulation.FlagTimedTrafficLights = true;

                    for (var s = 0; s < 8; s++)
                    {
                        var segment = node2.GetSegment(s);

                        if (segment != 0 && !TrafficPriority.IsPrioritySegment(selectedNodeIndex, segment))
                        {
                            TrafficPriority.AddPrioritySegment(selectedNodeIndex, segment,
                                PrioritySegment.PriorityType.None);
                        }
                    }
                }

                SetToolMode(ToolMode.TimedLightsShowLights);
            }
        }

        private bool _timedPanelAdd;
        private int _timedEditStep = -1;
        private readonly TrafficLightToolTextureResources _trafficLightToolTextureResources = new TrafficLightToolTextureResources();

        private void _guiTimedControlPanel(int num)
        {
            var nodeSimulation = CustomRoadAI.GetNodeSimulation(SelectedNodeIndexes[0]);
            var timedNodeMain = TrafficLightsTimed.GetTimedLight(SelectedNodeIndexes[0]);

            var layout = new GUIStyle {normal = {textColor = new Color(1f, 1f, 1f)}};
            var layoutGreen = new GUIStyle {normal = {textColor = new Color(0f, 1f, 0f)}};

            for (var i = 0; i < timedNodeMain.NumSteps(); i++)
            {
                GUILayout.BeginHorizontal();

                if (_timedEditStep != i)
                {
                    if (nodeSimulation.TimedTrafficLightsActive)
                    {
                        if (i == timedNodeMain.CurrentStep)
                        {
                            GUILayout.BeginVertical();
                            GUILayout.Space(5);
                            GUILayout.Label("State[" + (i + 1) + "]: " + timedNodeMain.GetStep(i).CurrentStep(), layoutGreen);
                            GUILayout.Space(5);
                            GUILayout.EndVertical();
                            if (GUILayout.Button("Skip", GUILayout.Width(45)))
                            {
                                foreach (var timedNode2 in SelectedNodeIndexes.Select(TrafficLightsTimed.GetTimedLight))
                                {
                                    timedNode2.SkipStep();
                                }
                            }
                        }
                        else
                        {
                            GUILayout.Label("State " + (i + 1) + ": " + timedNodeMain.GetStep(i).NumSteps, layout);
                        }
                    }
                    else
                    {
                        GUILayout.Label("State " + (i + 1) + ": " + timedNodeMain.GetStep(i).NumSteps);

                        if (_timedEditStep < 0)
                        {
                            GUILayout.BeginHorizontal(GUILayout.Width(100));

                            if (i > 0)
                            {
                                if (GUILayout.Button("up", GUILayout.Width(45)))
                                {
                                    foreach (var selectedNodeIndex in SelectedNodeIndexes)
                                    {
                                        var timedNode2 = TrafficLightsTimed.GetTimedLight(selectedNodeIndex);
                                        timedNode2.MoveStep(i, i - 1);
                                    }
                                }
                            }
                            else
                            {
                                GUILayout.Space(50);
                            }

                            if (i < timedNodeMain.NumSteps() - 1)
                            {
                                if (GUILayout.Button("down", GUILayout.Width(45)))
                                {
                                    foreach (var timedNode2 in SelectedNodeIndexes.Select(TrafficLightsTimed.GetTimedLight))
                                    {
                                        timedNode2.MoveStep(i, i + 1);
                                    }
                                }
                            }
                            else
                            {
                                GUILayout.Space(50);
                            }

                            GUILayout.EndHorizontal();

                            if (GUILayout.Button("View", GUILayout.Width(45)))
                            {
                                _timedPanelAdd = false;

                                foreach (var timedNode2 in SelectedNodeIndexes.Select(TrafficLightsTimed.GetTimedLight))
                                {
                                    timedNode2.GetStep(i).SetLights();
                                }
                            }

                            if (GUILayout.Button("Edit", GUILayout.Width(45)))
                            {
                                _timedPanelAdd = false;
                                _timedEditStep = i;
                                _stepValue = timedNodeMain.GetStep(i).NumSteps;

                                foreach (var timedNode2 in SelectedNodeIndexes.Select(TrafficLightsTimed.GetTimedLight))
                                {
                                    timedNode2.GetStep(i).SetLights();
                                }
                            }

                            GUILayout.Space(20);

                            if (GUILayout.Button("Delete", GUILayout.Width(60)))
                            {
                                _timedPanelAdd = false;

                                foreach (var timeNode in SelectedNodeIndexes.Select(TrafficLightsTimed.GetTimedLight))
                                {
                                    timeNode.RemoveStep(i);
                                }
                            }
                        }
                    }
                }
                else
                {
                    GUILayout.Label("Time: " + (int)_stepValue, GUILayout.Width(60));
                    _stepValue = GUILayout.HorizontalSlider(_stepValue, 1f, 120f, GUILayout.Height(20));
                    if (GUILayout.Button("Save", GUILayout.Width(45)))
                    {
                        foreach (var timeNode in SelectedNodeIndexes.Select(TrafficLightsTimed.GetTimedLight))
                        {
                            timeNode.GetStep(_timedEditStep).NumSteps = (int)_stepValue;
                            timeNode.GetStep(_timedEditStep).UpdateLights();
                        }

                        _timedEditStep = -1;
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();

            if (_timedEditStep < 0 && !nodeSimulation.TimedTrafficLightsActive)
            {
                if (_timedPanelAdd)
                {
                    GUILayout.Label("Time: " + (int) _stepValue, GUILayout.Width(60));
                    _stepValue = GUILayout.HorizontalSlider(_stepValue, 1f, 120f, GUILayout.Height(20));
                    if (GUILayout.Button("Add", GUILayout.Width(45)))
                    {
                        foreach (var timedNode in SelectedNodeIndexes.Select(TrafficLightsTimed.GetTimedLight))
                        {
                            timedNode.AddStep((int) _stepValue);
                        }
                        _timedPanelAdd = false;
                    }
                    if (GUILayout.Button("X", GUILayout.Width(22)))
                    {
                        _timedPanelAdd = false;
                    }
                }
                else
                {
                    if (_timedEditStep < 0)
                    {
                        if (GUILayout.Button("Add State"))
                        {
                            _timedPanelAdd = true;
                        }
                    }
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            if (timedNodeMain.NumSteps() > 1 && _timedEditStep < 0)
            {
                if (nodeSimulation.TimedTrafficLightsActive)
                {
                    if (GUILayout.Button(_timedShowNumbers ? "Hide counters" : "Show counters"))
                    {
                        _timedShowNumbers = !_timedShowNumbers;
                    }

                    if (GUILayout.Button("Stop"))
                    {
                        foreach (var timedNode in SelectedNodeIndexes.Select(TrafficLightsTimed.GetTimedLight))
                        {
                            timedNode.Stop();
                        }
                    }
                }
                else
                {
                    if (_timedEditStep < 0 && !_timedPanelAdd)
                    {
                        if (GUILayout.Button("Start"))
                        {
                            _timedPanelAdd = false;

                            foreach (var timedNode in SelectedNodeIndexes.Select(TrafficLightsTimed.GetTimedLight))
                            {
                                timedNode.Start();
                            }
                        }
                    }
                }
            }

            GUILayout.Space(30);

            if (_timedEditStep >= 0) return;

            if (!GUILayout.Button("REMOVE")) return;

            DisableTimed();
            SelectedNodeIndexes.Clear();
            SetToolMode(ToolMode.None);
        }

        private void _guiPrioritySigns()
        {
            var hoveredSegment = false;

            if (SelectedNode == 0)
            {
                _hoveredButton[0] = 0;
                _hoveredButton[1] = 0;
                return;
            }
            var node = GetNetNode(SelectedNode);

            for (var i = 0; i < 8; i++)
            {
                int segmentId = node.GetSegment(i);

                if (segmentId == 0) continue;

                var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];

                var nodePositionVector3 = node.m_position;

                if (segment.m_startNode == SelectedNode)
                {
                    nodePositionVector3.x += segment.m_startDirection.x*10f;
                    nodePositionVector3.y += segment.m_startDirection.y*10f;
                    nodePositionVector3.z += segment.m_startDirection.z*10f;
                }
                else
                {
                    nodePositionVector3.x += segment.m_endDirection.x*10f;
                    nodePositionVector3.y += segment.m_endDirection.y*10f;
                    nodePositionVector3.z += segment.m_endDirection.z*10f;
                }

                var nodeScreenPosition = Camera.main.WorldToScreenPoint(nodePositionVector3);

                var diff = nodePositionVector3 - Camera.main.transform.position;
                var zoom = 1.0f/diff.magnitude*100f;

                var size = 85f*zoom;

                nodeScreenPosition.y = Screen.height - nodeScreenPosition.y;

                var guiColor = GUI.color;

                if (_hoveredButton[0] == segmentId && _hoveredButton[1] == 0)
                {
                    guiColor.a = 0.8f;
                }
                else
                {
                    guiColor.a = 0.25f;
                }

                GUI.color = guiColor;

                var nodeBoundingBox = new Rect(nodeScreenPosition.x - size/2, nodeScreenPosition.y - size/2, size, size);

                var isPrioritySegment = TrafficPriority.IsPrioritySegment(SelectedNode, segmentId);
                hoveredSegment = IsMouseOverSegment(nodeBoundingBox, segmentId);

                if (isPrioritySegment)
                {
                    var prioritySegment = TrafficPriority.GetPrioritySegment(SelectedNode, segmentId);
                    switch (prioritySegment.Type)
                    {
                        case PrioritySegment.PriorityType.Main:
                            GUI.DrawTexture(nodeBoundingBox, TrafficLightToolTextureResources.SignPriorityTexture2D);
                            if (hoveredSegment && _uiClickedSegment)
                            {
                                Debug.Log("PrioritySegment.Type = Yield");
                                prioritySegment.Type = PrioritySegment.PriorityType.Yield;
                            }
                            break;
                        case PrioritySegment.PriorityType.Yield:
                            GUI.DrawTexture(nodeBoundingBox, TrafficLightToolTextureResources.SignYieldTexture2D);
                            if (hoveredSegment && _uiClickedSegment)
                                prioritySegment.Type = PrioritySegment.PriorityType.Stop;

                            break;
                        case PrioritySegment.PriorityType.Stop:
                            GUI.DrawTexture(nodeBoundingBox, TrafficLightToolTextureResources.SignStopTexture2D);
                            if (hoveredSegment && _uiClickedSegment)
                                prioritySegment.Type = PrioritySegment.PriorityType.None;
                            break;
                        case PrioritySegment.PriorityType.None:
                            GUI.DrawTexture(nodeBoundingBox, TrafficLightToolTextureResources.SignNoneTexture2D);

                            if(hoveredSegment && _uiClickedSegment)
                            {
                                Debug.Log("PrioritySegment.Type = None");
                                prioritySegment.Type = GetNumberOfMainRoads(node) >= 2
                                    ? PrioritySegment.PriorityType.Yield
                                    : PrioritySegment.PriorityType.Main;
                            }
                            break;
                    }
                }
                else
                {
                    GUI.DrawTexture(nodeBoundingBox, TrafficLightToolTextureResources.SignNoneTexture2D);

                    if (!hoveredSegment) continue;

                    if(!_uiClickedSegment) continue;

                    TrafficPriority.AddPrioritySegment(SelectedNode, segmentId,
                        GetNumberOfMainRoads(node) >= 2 ? PrioritySegment.PriorityType.Yield : 
                        PrioritySegment.PriorityType.Main);
                }
            }

            if (hoveredSegment) return;

            _hoveredButton[0] = 0;
            _hoveredButton[1] = 0;
        }

        private static int GetNumberOfMainRoads(NetNode node)
        {
            var numMainRoads = 0;
            for (var s = 0; s < 8; s++)
            {
                var segmentId2 = node.GetSegment(s);

                if (segmentId2 == 0 ||
                    !TrafficPriority.IsPrioritySegment(SelectedNode, segmentId2))
                    continue;
                var prioritySegment2 = TrafficPriority.GetPrioritySegment(SelectedNode,
                    segmentId2);

                if (prioritySegment2.Type == PrioritySegment.PriorityType.Main)
                {
                    numMainRoads++;
                }
            }
            return numMainRoads;
        }

        private bool IsMouseOverSegment(Rect nodeBoundingBox, int segmentId)
        {
            if (!nodeBoundingBox.Contains(Event.current.mousePosition))
                return false;

            _hoveredButton[0] = segmentId;
            _hoveredButton[1] = 0;

            if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
            {
                _uiClickedSegment = true;
                return true;
            }
            return false;
        }

        private void _switchTrafficLights()
        {
            var node = GetNetNode(_hoveredNetNodeIdx);

            if ((node.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None)
            {
                if (TrafficLightsTimed.IsTimedLight(_hoveredNetNodeIdx))
                {
                    ShowToolInfo(true, "Node is part of timed script", node.m_position);
                }
                else
                {
                    node.m_flags &= ~NetNode.Flags.TrafficLights;
                }
            }
            else
            {
                node.m_flags |= NetNode.Flags.TrafficLights;
            }

            SetNetNode(_hoveredNetNodeIdx, node);
        }

        public void AddTimedNodes()
        {
            foreach (var selectedNodeIndex in SelectedNodeIndexes)
            {
                var node = GetNetNode(selectedNodeIndex);
                CustomRoadAI.AddNodeToSimulation(selectedNodeIndex);
                var nodeSimulation = CustomRoadAI.GetNodeSimulation(selectedNodeIndex);
                nodeSimulation.FlagTimedTrafficLights = true;

                for (var s = 0; s < 8; s++)
                {
                    var segment = node.GetSegment(s);

                    if (segment != 0 && !TrafficPriority.IsPrioritySegment(selectedNodeIndex, segment))
                    {
                        TrafficPriority.AddPrioritySegment(selectedNodeIndex, segment, PrioritySegment.PriorityType.None);
                    }
                }
            }
        }

        public bool SwitchManual()
        {
            if (SelectedNode == 0) return false;

            var node = GetNetNode(SelectedNode);
            var nodeSimulation = CustomRoadAI.GetNodeSimulation(SelectedNode);

            if (nodeSimulation == null)
            {
                //node.Info.m_netAI = _myGameObject.GetComponent<CustomRoadAI>();
                //node.Info.m_netAI.m_info = node.Info;
                CustomRoadAI.AddNodeToSimulation(SelectedNode);
                nodeSimulation = CustomRoadAI.GetNodeSimulation(SelectedNode);
                nodeSimulation.FlagManualTrafficLights = true;

                for (var s = 0; s < 8; s++)
                {
                    var segment = node.GetSegment(s);

                    if (segment != 0 && !TrafficPriority.IsPrioritySegment(SelectedNode, segment))
                    {
                        TrafficPriority.AddPrioritySegment(SelectedNode, segment, PrioritySegment.PriorityType.None);
                    }
                }

                return true;
            }
            nodeSimulation.FlagManualTrafficLights = false;
            CustomRoadAI.RemoveNodeFromSimulation(SelectedNode);

            for (var s = 0; s < 8; s++)
            {
                var segment = node.GetSegment(s);

                if (segment != 0 && !TrafficPriority.IsPrioritySegment(SelectedNode, segment))
                {
                    TrafficPriority.AddPrioritySegment(SelectedNode, segment, PrioritySegment.PriorityType.None);
                }
            }

            return false;
        }

        private static void DisableManual()
        {
            if (SelectedNode == 0) return;
            var nodeSimulation = CustomRoadAI.GetNodeSimulation(SelectedNode);

            if (nodeSimulation == null || !nodeSimulation.FlagManualTrafficLights) return;

            nodeSimulation.FlagManualTrafficLights = false;
            CustomRoadAI.RemoveNodeFromSimulation(SelectedNode);
        }

        private void DisableTimed()
        {
            if (SelectedNodeIndexes.Count <= 0) return;

            foreach (var selectedNodeIndex in SelectedNodeIndexes)
            {
                GetNetNode(selectedNodeIndex);
                var nodeSimulation = CustomRoadAI.GetNodeSimulation(selectedNodeIndex);

                TrafficLightsTimed.RemoveTimedLight(selectedNodeIndex);

                if (nodeSimulation == null) continue;

                nodeSimulation.FlagTimedTrafficLights = false;
                CustomRoadAI.RemoveNodeFromSimulation(selectedNodeIndex);
            }
        }

        public NetNode GetCurrentNetNode()
        {
            return GetNetNode(_hoveredNetNodeIdx);
        }
        public static NetNode GetNetNode(ushort index)
        {
            return Singleton<NetManager>.instance.m_nodes.m_buffer[index];
        }

        public static void SetNetNode(ushort index, NetNode node)
        {
            Singleton<NetManager>.instance.m_nodes.m_buffer[index] = node;
        }

        private static void AddListNode(ushort node)
        {
            SelectedNodeIndexes.Add(node);
        }

        private static bool ContainsListNode(ushort node)
        {
            return SelectedNodeIndexes.Contains(node);
        }

        private static void RemoveListNode(ushort node)
        {
            SelectedNodeIndexes.Remove(node);
        }

        private static void AddListSegment(int segment)
        {
            _selectedSegmentIndexes.Add(segment);
        }

        private static bool ContainsListSegment(int segment)
        {
            return _selectedSegmentIndexes.Contains(segment);
        }

        private static void RemoveListSegment(int segment)
        {
            _selectedSegmentIndexes.Remove(segment);
        }
    }
}
