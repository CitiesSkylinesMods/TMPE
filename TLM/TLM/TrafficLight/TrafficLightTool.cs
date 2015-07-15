using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using TrafficManager.CustomAI;
using TrafficManager.Traffic;
using UnityEngine;

namespace TrafficManager.TrafficLight
{
    public class TrafficLightTool : DefaultTool
    {
        public static ToolMode ToolMode;

        private bool _mouseDown;
        private bool _mouseClicked;

        private ushort _hoveredNetNodeIdx;

        private int _hoveredSegmentIdx;

        public static List<ushort> SelectedNodeIndexes = new List<ushort>();
        public static List<int> SelectedSegmentIndexes = new List<int>(); 

        private readonly int[] _hoveredButton = new int[2];
        private ushort _hoveredNode;

        private bool _cursorInSecondaryPanel;

        // simple
        private Texture2D _lightSimple1;
        private Texture2D _lightSimple2;
        private Texture2D _lightSimple3;
        // forward
        private Texture2D _lightForward1;
        private Texture2D _lightForward2;
        private Texture2D _lightForward3;
        // right
        private Texture2D _lightRight1;
        private Texture2D _lightRight2;
        private Texture2D _lightRight3;
        // left
        private Texture2D _lightLeft1;
        private Texture2D _lightLeft2;
        private Texture2D _lightLeft3;
        // forwardright
        private Texture2D _lightForwardRight1;
        private Texture2D _lightForwardRight2;
        private Texture2D _lightForwardRight3;
        // forwardleft
        private Texture2D _lightForwardLeft1;
        private Texture2D _lightForwardLeft2;
        private Texture2D _lightForwardLeft3;
        // yellow
        private Texture2D _lightYellow;
        // pedestrian
        private Texture2D _pedestrianLight1;
        private Texture2D _pedestrianLight2;
        // light mode
        private Texture2D _lightMode;
        private Texture2D _lightCounter;
        // pedestrian mode
        private Texture2D _pedestrianMode1;
        private Texture2D _pedestrianMode2;

        // priority signs
        private Texture2D _signStop;
        private Texture2D _signYield;
        private Texture2D _signPriority;
        private Texture2D _signNone;

        private readonly GUIStyle _counterStyle = new GUIStyle();

        private bool _uiClickedSegment;
        private Rect _windowRect;
        private Rect _windowRect2;

        public float StepValue = 1f;

        public float[] SliderValues = new float[16] {1f,1f,1f,1f,1f,1f,1f,1f,1f,1f,1f,1f,1f,1f,1f,1f};

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

            // simple
            _lightSimple1 = LoadDllResource("light_1_1.png", 103, 243);
            _lightSimple2 = LoadDllResource("light_1_2.png", 103, 243);
            _lightSimple3 = LoadDllResource("light_1_3.png", 103, 243);
            // forward
            _lightForward1 = LoadDllResource("light_2_1.png", 103, 243);
            _lightForward2 = LoadDllResource("light_2_2.png", 103, 243);
            _lightForward3 = LoadDllResource("light_2_3.png", 103, 243);
            // right
            _lightRight1 = LoadDllResource("light_3_1.png", 103, 243);
            _lightRight2 = LoadDllResource("light_3_2.png", 103, 243);
            _lightRight3 = LoadDllResource("light_3_3.png", 103, 243);
            // left
            _lightLeft1 = LoadDllResource("light_4_1.png", 103, 243);
            _lightLeft2 = LoadDllResource("light_4_2.png", 103, 243);
            _lightLeft3 = LoadDllResource("light_4_3.png", 103, 243);
            // forwardright
            _lightForwardRight1 = LoadDllResource("light_5_1.png", 103, 243);
            _lightForwardRight2 = LoadDllResource("light_5_2.png", 103, 243);
            _lightForwardRight3 = LoadDllResource("light_5_3.png", 103, 243);
            // forwardleft
            _lightForwardLeft1 = LoadDllResource("light_6_1.png", 103, 243);
            _lightForwardLeft2 = LoadDllResource("light_6_2.png", 103, 243);
            _lightForwardLeft3 = LoadDllResource("light_6_3.png", 103, 243);
            // yellow
            _lightYellow = LoadDllResource("light_yellow.png", 103, 243);
            // pedestrian
            _pedestrianLight1 = LoadDllResource("pedestrian_light_1.png", 73, 123);
            _pedestrianLight2 = LoadDllResource("pedestrian_light_2.png", 73, 123);
            // light mode
            _lightMode = LoadDllResource("light_mode.png", 103, 95);
            _lightCounter = LoadDllResource("light_counter.png", 103, 95);
            // pedestrian mode
            _pedestrianMode1 = LoadDllResource("pedestrian_mode_1.png", 73, 70);
            _pedestrianMode2 = LoadDllResource("pedestrian_mode_2.png", 73, 73);

            // priority signs
            _signStop = LoadDllResource("sign_stop.png", 200, 200);
            _signYield = LoadDllResource("sign_yield.png", 200, 200);
            _signPriority = LoadDllResource("sign_priority.png", 200, 200);
            _signNone = LoadDllResource("sign_none.png", 200, 200);
            
            _secondPanelTexture = MakeTex(1200, 560, new Color(0.5f, 0.5f, 0.5f, 1f));

            base.Awake();
        }

        public static Texture2D LoadDllResource(string resourceName, int width, int height)
        {
            var myAssembly = Assembly.GetExecutingAssembly();
            var myStream = myAssembly.GetManifestResourceStream("TrafficManager.Resources." + resourceName);

            var texture = new Texture2D(width, height, TextureFormat.ARGB32, false);

            texture.LoadImage(ReadToEnd(myStream));

            return texture;
        }

        static byte[] ReadToEnd(Stream stream)
        {
            long originalPosition = stream.Position;
            stream.Position = 0;

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length*2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte) nextByte);
                            readBuffer = temp;
                            totalBytesRead++;

                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                return buffer;
            }
            finally
            {
                stream.Position = originalPosition;
            }
        }

        // Expose protected property
        public new CursorInfo ToolCursor
        {
            get { return base.ToolCursor; }
            set { base.ToolCursor = value; }
        }

        public static ushort SelectedNode { get; private set; }

        public static int SelectedSegment { get; private set; }

        public static void SetToolMode(ToolMode mode)
        {
            ToolMode = mode;

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
                SelectedSegmentIndexes.Clear();
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
            if (ToolMode == ToolMode.SwitchTrafficLight)
            {
                if (m_toolController.IsInsideUI || !Cursor.visible)
                {
                    return;
                }

                _renderOverlaySwitch(cameraInfo);
            }
            else if (ToolMode == ToolMode.AddPrioritySigns)
            {
                _renderOverlayPriority(cameraInfo);
            }
            else if (ToolMode == ToolMode.ManualSwitch)
            {
                _renderOverlayManual(cameraInfo);
            }
            else if (ToolMode == ToolMode.TimedLightsSelectNode)
            {
                _renderOverlayTimedSelectNodes(cameraInfo);
            }
            else if (ToolMode == ToolMode.TimedLightsShowLights)
            {
                
            }
            else if (ToolMode == ToolMode.LaneChange)
            {
                _renderOverlayLaneChange(cameraInfo);
            }
            else if (ToolMode == ToolMode.LaneRestrictions)
            {
                _renderOverlayLaneRestrictions(cameraInfo);
            }
            else if (ToolMode == ToolMode.Crosswalk)
            {
                _renderOverlayCrosswalk(cameraInfo);
            }
            else
            {
                base.RenderOverlay(cameraInfo);
            }
        }

        public void _renderOverlaySwitch(RenderManager.CameraInfo cameraInfo)
        {
            if (_hoveredNetNodeIdx != 0)
            {
                var node = GetNetNode(_hoveredNetNodeIdx);

                if ((node.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None)
                {
                    Bezier3 bezier;

                    var segment = Singleton<NetManager>.instance.m_segments.m_buffer[node.m_segment0];

                    bezier.a = node.m_position;
                    bezier.d = node.m_position;

                    var color = GetToolColor(_mouseDown, false);

                    NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
                        segment.m_endDirection,
                        false, false, out bezier.b, out bezier.c);
                    _renderOverlayDraw(cameraInfo, bezier, color);
                }
            }
        }

        public void _renderOverlayPriority(RenderManager.CameraInfo cameraInfo)
        {
            if (_hoveredNetNodeIdx != 0 && _hoveredNetNodeIdx != SelectedNode)
            {
                var node = GetNetNode(_hoveredNetNodeIdx);
                var segment = Singleton<NetManager>.instance.m_segments.m_buffer[node.m_segment0];

                if ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None)
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
        }

        public void _renderOverlayManual(RenderManager.CameraInfo cameraInfo)
        {
            if (SelectedNode != 0)
            {
                var node = GetNetNode(SelectedNode);

                var colorGray = new Color(0.25f, 0.25f, 0.25f, 0.25f);

                var color2 = colorGray;

                var nodeSimulation = CustomRoadAI.GetNodeSimulation(SelectedNode);

                for (var i = 0; i < 8; i++)
                {
                    int segmentId = node.GetSegment(i);

                    if (segmentId != 0)
                    {
                        var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];

                        var position = node.m_position;

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

                        if (nodeSimulation == null || !TrafficLightsManual.IsSegmentLight(SelectedNode, segmentId))
                        {
                            var width = _hoveredButton[0] == segmentId ? 11.25f : 10f;

                            _renderOverlayDraw(cameraInfo, color2, position, width, segmentId != _hoveredButton[0]);
                        }
                    }
                }
            }
            else
            {
                if (_hoveredNetNodeIdx != 0)
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
            }
        }

        public void _renderOverlayTimedSelectNodes(RenderManager.CameraInfo cameraInfo)
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

            if (SelectedNodeIndexes.Count > 0)
            {
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
        }

        public void _renderOverlayLaneChange(RenderManager.CameraInfo cameraInfo)
        {

            if (_hoveredSegmentIdx != 0 && _hoveredNetNodeIdx != 0 && (_hoveredSegmentIdx != SelectedSegment || _hoveredNetNodeIdx != SelectedNode))
            {
                var netFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_flags;

                if ((netFlags & NetNode.Flags.Junction) != NetNode.Flags.None)
                {

                    var segment = Singleton<NetManager>.instance.m_segments.m_buffer[_hoveredSegmentIdx];

                    NetTool.RenderOverlay(cameraInfo, ref segment, GetToolColor(false, false),
                        GetToolColor(false, false));
                }
            }

            if (SelectedSegment != 0)
            {
                var segment = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegment];

                NetTool.RenderOverlay(cameraInfo, ref segment, GetToolColor(true, false), GetToolColor(true, false));
            }
        }

        public void _renderOverlayLaneRestrictions(RenderManager.CameraInfo cameraInfo)
        {
            if (SelectedSegmentIndexes.Count > 0)
            {
                // ReSharper disable once LoopCanBePartlyConvertedToQuery - can't be converted because segment is pass by ref
                foreach (var index in SelectedSegmentIndexes)
                {
                    var segment = Singleton<NetManager>.instance.m_segments.m_buffer[index];

                    NetTool.RenderOverlay(cameraInfo, ref segment, GetToolColor(true, false),
                        GetToolColor(true, false));
                }
            }

            if (_hoveredSegmentIdx != 0)
            {
                var segment = Singleton<NetManager>.instance.m_segments.m_buffer[_hoveredSegmentIdx];

                    NetTool.RenderOverlay(cameraInfo, ref segment, GetToolColor(false, false),
                        GetToolColor(false, false));
            }
        }

        public void _renderOverlayCrosswalk(RenderManager.CameraInfo cameraInfo)
        {
            if (_hoveredSegmentIdx != 0)
            {
                var segment = Singleton<NetManager>.instance.m_segments.m_buffer[_hoveredSegmentIdx];

                if (ValidCrosswalkNode(segment.m_startNode, GetNetNode(segment.m_startNode)) ||
                    ValidCrosswalkNode(segment.m_endNode, GetNetNode(segment.m_endNode)) )
                {

                    NetTool.RenderOverlay(cameraInfo, ref segment, GetToolColor(_mouseDown, false),
                        GetToolColor(_mouseDown, false));
                }
            }
        }

        public void _renderOverlayDraw(RenderManager.CameraInfo cameraInfo, Bezier3 bezier, Color color)
        {
            const float width = 8f;

            var exprEaCp0 = Singleton<ToolManager>.instance;
            exprEaCp0.m_drawCallData.m_overlayCalls = exprEaCp0.m_drawCallData.m_overlayCalls + 1;
            Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(cameraInfo, color, bezier,
                width * 2f, width, width, -1f, 1280f, false, false);

            // 8 - small roads; 16 - big roads
        }

        public void _renderOverlayDraw(RenderManager.CameraInfo cameraInfo, Color color, Vector3 position, float width, bool alpha)
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


            if (ToolMode == ToolMode.None)
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
                if (!_mouseClicked)
                {
                    _mouseClicked = true;

                    if (m_toolController.IsInsideUI || !Cursor.visible || _cursorInSecondaryPanel)
                    {
                        return;
                    }

                    if (_hoveredNetNodeIdx != 0)
                    {
                        var node = GetNetNode(_hoveredNetNodeIdx);

                        if (ToolMode == ToolMode.SwitchTrafficLight)
                        {
                            if ((node.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None)
                            {
                                _switchTrafficLights();
                            }
                        }
                        else if (ToolMode == ToolMode.AddPrioritySigns)
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
                        else if (ToolMode == ToolMode.ManualSwitch)
                        {
                            if (SelectedNode == 0)
                            {
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
                        }
                        else if (ToolMode == ToolMode.TimedLightsSelectNode)
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
                        else if (ToolMode == ToolMode.LaneChange)
                        {
                            if (_hoveredNetNodeIdx != 0 && _hoveredSegmentIdx != 0)
                            {
                                var netFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[_hoveredNetNodeIdx].m_flags;

                                if ((netFlags & NetNode.Flags.Junction) != NetNode.Flags.None)
                                {
                                    SelectedSegment = _hoveredSegmentIdx;
                                    SelectedNode = _hoveredNetNodeIdx;
                                }
                            }
                        }
                    }
                    if (_hoveredSegmentIdx != 0)
                    {
                        if (ToolMode == ToolMode.Crosswalk)
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
                            if (!result && (ValidCrosswalkNode(segment.m_startNode, startNode) || ValidCrosswalkNode(segment.m_endNode, endNode)) && (endNode.m_flags & NetNode.Flags.Junction) == NetNode.Flags.None)
                            {
                                if (ValidCrosswalkNode(segment.m_startNode, startNode) && (startNode.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None)
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
                                if (ValidCrosswalkNode(segment.m_startNode, startNode) && (startNode.m_flags & NetNode.Flags.Junction) == NetNode.Flags.None)
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
                                if (ValidCrosswalkNode(segment.m_startNode, startNode) && (startNode.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None)
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
                        else if (ToolMode == ToolMode.LaneRestrictions)
                        {
                            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[_hoveredSegmentIdx];
                            var info = segment.Info;

                            if (TrafficRoadRestrictions.IsSegment(_hoveredSegmentIdx))
                            {
                                if (SelectedSegmentIndexes.Count > 0)
                                {
                                    ShowToolInfo(true, "Road is already in a group!",
                                        Singleton<NetManager>.instance.m_nodes.m_buffer[segment.m_startNode]
                                            .m_position);
                                }
                                else
                                {
                                    var restSegment = TrafficRoadRestrictions.GetSegment(_hoveredSegmentIdx);

                                    SelectedSegmentIndexes = new List<int>(restSegment.SegmentGroup);
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
                                    if (SelectedSegmentIndexes.Count > 0)
                                    {
                                        var segment2 =
                                            Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegmentIndexes[0]
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
                    }
                }
            }
            else
            {
                ShowToolInfo(false, null, Vector3.zero);
                _mouseClicked = false;
            }
        }

        protected bool ValidCrosswalkNode(ushort nodeid, NetNode node)
        {
            return nodeid != 0 && (node.m_flags & (NetNode.Flags.Transition | NetNode.Flags.TrafficLights)) == NetNode.Flags.None;
        }

        protected override void OnToolGUI()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                _uiClickedSegment = false;
            }

            if (ToolMode == ToolMode.AddPrioritySigns)
            {
                _guiPrioritySigns();
            }
            else if (ToolMode == ToolMode.ManualSwitch)
            {
                _guiManualTrafficLights();
            }
            else if (ToolMode == ToolMode.TimedLightsSelectNode)
            {
                _guiTimedTrafficLightsNode();
            }
            else if (ToolMode == ToolMode.TimedLightsShowLights)
            {
                _guiTimedTrafficLights();
            }
            else if (ToolMode == ToolMode.LaneChange)
            {
                _guiLaneChange();
            }
            else if (ToolMode == ToolMode.LaneRestrictions)
            {
                _guiLaneRestrictions();
            }
        }

        protected void _guiManualTrafficLights()
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

                    if (segmentId != 0 && nodeSimulation != null && TrafficLightsManual.IsSegmentLight(SelectedNode, segmentId))
                    {
                        var segmentDict = TrafficLightsManual.GetSegmentLight(SelectedNode, segmentId);

                        var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];

                        var position = node.m_position;

                        var offset = 25f;

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

                        Vector3 diff = position - Camera.main.transform.position;
                        float zoom = 1.0f/diff.magnitude*100f;

                        // original / 2.5
                        var lightWidth = 41f*zoom;
                        var lightHeight = 97f*zoom;

                        // SWITCH MODE BUTTON
                        var modeWidth = 41f*zoom;
                        var modeHeight = 38f*zoom;
                                    

                        guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == -1 ? 0.92f : 0.45f;

                        GUI.color = guiColor;

                        Rect myRect1 = new Rect(screenPos.x - modeWidth / 2, screenPos.y - modeHeight / 2 + modeHeight - 7f*zoom, modeWidth, modeHeight);

                        GUI.DrawTexture(myRect1, _lightMode);

                        if (myRect1.Contains(Event.current.mousePosition))
                        {
                            _hoveredButton[0] = segmentId;
                            _hoveredButton[1] = -1;
                            hoveredSegment = true;

                            if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                            {
                                _uiClickedSegment = true;
                                segmentDict.ChangeMode();
                            }
                        }

                        // COUNTER
                        guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 0 ? 0.92f : 0.45f;

                        Rect myRectCounter = new Rect(screenPos.x - modeWidth / 2, screenPos.y - modeHeight / 2 - 6f*zoom, modeWidth, modeHeight);

                        GUI.DrawTexture(myRectCounter, _lightCounter);

                        float counterSize = 20f * zoom;

                        var counter = segmentDict.LastChange;

                        Rect myRectCounterNum = new Rect(screenPos.x - counterSize + 15f * zoom + (counter >= 10 ? -5*zoom : 0f), screenPos.y - counterSize + 11f * zoom, counterSize, counterSize);

                        _counterStyle.fontSize = (int)(18f*zoom);
                        _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                        GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

                        if (myRectCounter.Contains(Event.current.mousePosition))
                        {
                            _hoveredButton[0] = segmentId;
                            _hoveredButton[1] = 0;
                            hoveredSegment = true;
                        }

                        // SWITCH MANUAL PEDESTRIAN LIGHT BUTTON
                        var manualPedestrianWidth = 36f*zoom;
                        var manualPedestrianHeight = 35f*zoom;

                        guiColor.a = _hoveredButton[0] == segmentId && (_hoveredButton[1] == 1 || _hoveredButton[1] == 2) ? 0.92f : 0.45f;

                        GUI.color = guiColor;

                        var myRect2 = new Rect(screenPos.x - manualPedestrianWidth / 2 - lightWidth + 5f*zoom, screenPos.y - manualPedestrianHeight / 2 - 9f*zoom, manualPedestrianWidth, manualPedestrianHeight);

                        if (segmentDict.PedestrianEnabled)
                            GUI.DrawTexture(myRect2, _pedestrianMode2);
                        else
                            GUI.DrawTexture(myRect2, _pedestrianMode1);

                        if (myRect2.Contains(Event.current.mousePosition))
                        {
                            _hoveredButton[0] = segmentId;
                            _hoveredButton[1] = 1;
                            hoveredSegment = true;

                            if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                            {
                                _uiClickedSegment = true;
                                segmentDict.ManualPedestrian();
                            }
                        }

                        // SWITCH PEDESTRIAN LIGHT
                        var pedestrianWidth = 36f * zoom;
                        var pedestrianHeight = 61f * zoom;

                        guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 2 && segmentDict.PedestrianEnabled ? 0.92f : 0.45f;

                        GUI.color = guiColor;

                        var myRect3 = new Rect(screenPos.x - pedestrianWidth / 2 - lightWidth + 5f*zoom, screenPos.y - pedestrianHeight / 2 + 22f*zoom, pedestrianWidth, pedestrianHeight);

                        if (segmentDict.LightPedestrian == RoadBaseAI.TrafficLightState.Green)
                            GUI.DrawTexture(myRect3, _pedestrianLight2);
                        else if (segmentDict.LightPedestrian == RoadBaseAI.TrafficLightState.Red)
                            GUI.DrawTexture(myRect3, _pedestrianLight1);

                        if (myRect3.Contains(Event.current.mousePosition))
                        {
                            _hoveredButton[0] = segmentId;
                            _hoveredButton[1] = 2;
                            hoveredSegment = true;

                            if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
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

                        if (!TrafficLightsManual.SegmentIsIncomingOneWay(segmentId, SelectedNode))
                        {
                            var hasLeftSegment = TrafficPriority.HasLeftSegment(segmentId, SelectedNode) && TrafficPriority.HasLeftLane(SelectedNode, segmentId);
                            var hasForwardSegment = TrafficPriority.HasForwardSegment(segmentId, SelectedNode) && TrafficPriority.HasForwardLane(SelectedNode, segmentId);
                            var hasRightSegment = TrafficPriority.HasRightSegment(segmentId, SelectedNode) && TrafficPriority.HasRightLane(SelectedNode, segmentId);

                            if (segmentDict.CurrentMode == ManualSegmentLight.Mode.Simple)
                            {
                                // no arrow light
                                guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 3 ? 0.92f : 0.45f;

                                GUI.color = guiColor;

                                var myRect4 =
                                    new Rect(screenPos.x - lightWidth/2 - lightWidth - pedestrianWidth + 5f*zoom,
                                        screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                    GUI.DrawTexture(myRect4, _lightSimple3);
                                else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                    GUI.DrawTexture(myRect4, _lightSimple1);

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
                            }
                            else if (segmentDict.CurrentMode == ManualSegmentLight.Mode.LeftForwardR)
                            {
                                if (hasLeftSegment)
                                {
                                    // left arrow light
                                    guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 3 ? 0.92f : 0.45f;

                                    GUI.color = guiColor;

                                    Rect myRect4 =
                                        new Rect(screenPos.x - lightWidth/2 - lightWidth*2 - pedestrianWidth + 5f*zoom,
                                            screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                    if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect4, _lightLeft3);
                                    else if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect4, _lightLeft1);

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
                                guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 4 ? 0.92f : 0.45f;

                                GUI.color = guiColor;

                                Rect myRect5 =
                                    new Rect(screenPos.x - lightWidth/2 - lightWidth - pedestrianWidth + 5f*zoom,
                                        screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                if (hasForwardSegment && hasRightSegment)
                                {
                                    if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect5, _lightForwardRight3);
                                    else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect5, _lightForwardRight1);
                                }
                                else if (!hasRightSegment)
                                {
                                    if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect5, _lightForward3);
                                    else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect5, _lightForward1);
                                }
                                else
                                {
                                    if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect5, _lightRight3);
                                    else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect5, _lightRight1);
                                }

                                if (myRect5.Contains(Event.current.mousePosition))
                                {
                                    _hoveredButton[0] = segmentId;
                                    _hoveredButton[1] = 4;
                                    hoveredSegment = true;

                                    if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                                    {
                                        _uiClickedSegment = true;
                                        segmentDict.ChangeLightMain();
                                    }
                                }
                            }
                            else if (segmentDict.CurrentMode == ManualSegmentLight.Mode.RightForwardL)
                            {
                                // forward-left light
                                guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 3 ? 0.92f : 0.45f;

                                GUI.color = guiColor;

                                Rect myRect4 = new Rect(screenPos.x - lightWidth/2 - lightWidth*2 - pedestrianWidth + 5f*zoom,
                                        screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                if (hasForwardSegment && hasLeftSegment)
                                {
                                    if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect4, _lightForwardLeft3);
                                    else if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect4, _lightForwardLeft1);
                                }
                                else if (!hasLeftSegment)
                                {
                                    if (!hasRightSegment)
                                    {
                                        myRect4 = new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
                                        screenPos.y - lightHeight / 2, lightWidth, lightHeight);
                                    }

                                    if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect4, _lightForward3);
                                    else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect4, _lightForward1);
                                }
                                else
                                {
                                    if (!hasRightSegment)
                                    {
                                        myRect4 = new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
                                        screenPos.y - lightHeight / 2, lightWidth, lightHeight);
                                    }

                                    if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect4, _lightLeft3);
                                    else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect4, _lightLeft1);
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

                                // right arrow light
                                if (hasRightSegment)
                                guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 4 ? 0.92f : 0.45f;

                                GUI.color = guiColor;

                                Rect myRect5 =
                                    new Rect(screenPos.x - lightWidth/2 - lightWidth - pedestrianWidth + 5f*zoom,
                                        screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                if (segmentDict.LightRight == RoadBaseAI.TrafficLightState.Green)
                                    GUI.DrawTexture(myRect5, _lightRight3);
                                else if (segmentDict.LightRight == RoadBaseAI.TrafficLightState.Red)
                                    GUI.DrawTexture(myRect5, _lightRight1);


                                if (myRect5.Contains(Event.current.mousePosition))
                                {
                                    _hoveredButton[0] = segmentId;
                                    _hoveredButton[1] = 4;
                                    hoveredSegment = true;

                                    if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                                    {
                                        _uiClickedSegment = true;
                                        segmentDict.ChangeLightRight();
                                    }
                                }
                            }
                            else // all
                            {
                                // left arrow light
                                if (hasLeftSegment)
                                {
                                    guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 3 ? 0.92f : 0.45f;

                                    GUI.color = guiColor;

                                    var offsetLight = lightWidth;

                                    if (hasRightSegment)
                                        offsetLight += lightWidth;

                                    if (hasForwardSegment)
                                        offsetLight += lightWidth;

                                    Rect myRect4 =
                                        new Rect(screenPos.x - lightWidth / 2 - offsetLight - pedestrianWidth + 5f * zoom,
                                            screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                    if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect4, _lightLeft3);
                                    else if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect4, _lightLeft1);

                                    if (myRect4.Contains(Event.current.mousePosition))
                                    {
                                        _hoveredButton[0] = segmentId;
                                        _hoveredButton[1] = 3;
                                        hoveredSegment = true;

                                        if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                                        {
                                            _uiClickedSegment = true;
                                            segmentDict.ChangeLightLeft();

                                            if (!hasForwardSegment)
                                            {
                                                segmentDict.ChangeLightMain();
                                            }
                                        }
                                    }
                                }

                                // forward arrow light
                                if (hasForwardSegment)
                                {
                                    guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 4 ? 0.92f : 0.45f;

                                    GUI.color = guiColor;

                                    var offsetLight = lightWidth;

                                    if (hasRightSegment)
                                        offsetLight += lightWidth;

                                    Rect myRect6 =
                                        new Rect(screenPos.x - lightWidth/2 - offsetLight - pedestrianWidth + 5f*zoom,
                                            screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                    if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect6, _lightForward3);
                                    else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect6, _lightForward1);

                                    if (myRect6.Contains(Event.current.mousePosition))
                                    {
                                        _hoveredButton[0] = segmentId;
                                        _hoveredButton[1] = 4;
                                        hoveredSegment = true;

                                        if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                                        {
                                            _uiClickedSegment = true;
                                            segmentDict.ChangeLightMain();
                                        }
                                    }
                                }

                                // right arrow light
                                if (hasRightSegment)
                                {
                                    guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 5 ? 0.92f : 0.45f;

                                    GUI.color = guiColor;

                                    Rect myRect5 =
                                        new Rect(screenPos.x - lightWidth/2 - lightWidth - pedestrianWidth + 5f*zoom,
                                            screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                    if (segmentDict.LightRight == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect5, _lightRight3);
                                    else if (segmentDict.LightRight == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect5, _lightRight1);

                                    if (myRect5.Contains(Event.current.mousePosition))
                                    {
                                        _hoveredButton[0] = segmentId;
                                        _hoveredButton[1] = 5;
                                        hoveredSegment = true;

                                        if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                                        {
                                            _uiClickedSegment = true;
                                            segmentDict.ChangeLightRight();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (!hoveredSegment)
            {
                _hoveredButton[0] = 0;
                _hoveredButton[1] = 0;
            }
        }

        protected void _guiManualTrafficLightsCrosswalk(NetNode node)
        {
            var hoveredSegment = false;

            int segment1 = 0;
            int segment2 = 0;

            for (var i = 0; i < 8; i++)
            {
                var segmentId = node.GetSegment(i);

                if (segmentId != 0)
                {
                    if (segment1 == 0)
                    {
                        segment1 = segmentId;
                    }
                    else
                    {
                        segment2 = segmentId;
                    }
                }
            }

            var segmentDict1 = TrafficLightsManual.GetSegmentLight(SelectedNode, segment1);
            var segmentDict2 = TrafficLightsManual.GetSegmentLight(SelectedNode, segment2);

            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segment1];

            var position = node.m_position;

            var offset = 0f;

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

            Vector3 diff = position - Camera.main.transform.position;
            float zoom = 1.0f/diff.magnitude*100f;

            // original / 2.5
            var lightWidth = 41f*zoom;
            var lightHeight = 97f*zoom;

            // SWITCH PEDESTRIAN LIGHT
            var pedestrianWidth = 36f * zoom;
            var pedestrianHeight = 61f * zoom;

            guiColor.a = _hoveredButton[0] == segment1 && _hoveredButton[1] == 0 ? 0.92f : 0.45f;

            GUI.color = guiColor;

            Rect myRect3 = new Rect(screenPos.x - pedestrianWidth / 2 - lightWidth + 5f * zoom, screenPos.y - pedestrianHeight / 2 + 22f * zoom, pedestrianWidth, pedestrianHeight);

            if (segmentDict1.LightPedestrian == RoadBaseAI.TrafficLightState.Green)
                GUI.DrawTexture(myRect3, _pedestrianLight2);
            else if (segmentDict1.LightPedestrian == RoadBaseAI.TrafficLightState.Red)
                GUI.DrawTexture(myRect3, _pedestrianLight1);

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

            Rect myRect4 =
                new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
                    screenPos.y - lightHeight / 2, lightWidth, lightHeight);

            if (segmentDict1.LightMain == RoadBaseAI.TrafficLightState.Green)
                GUI.DrawTexture(myRect4, _lightSimple3);
            else if (segmentDict1.LightMain == RoadBaseAI.TrafficLightState.Red)
                GUI.DrawTexture(myRect4, _lightSimple1);

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

            if (!hoveredSegment)
            {
                _hoveredButton[0] = 0;
                _hoveredButton[1] = 0;
            }
        }

        protected void _guiTimedTrafficLights()
        {
            GUILayout.Window(253, _windowRect, _guiTimedControlPanel, "Timed traffic lights manager");

            if (_windowRect.Contains(Event.current.mousePosition))
            {
                _cursorInSecondaryPanel = true;
            }
            else
            {
                _cursorInSecondaryPanel = false;
            }

            var hoveredSegment = false;

            foreach (var index in SelectedNodeIndexes)
            {
                var node = GetNetNode(index);

                var nodeSimulation = CustomRoadAI.GetNodeSimulation(index);

                for (var i = 0; i < 8; i++)
                {
                    int segmentId = node.GetSegment(i);

                    if (segmentId != 0 && nodeSimulation != null && TrafficLightsManual.IsSegmentLight(index, segmentId))
                    {
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

                        Vector3 diff = position - Camera.main.transform.position;
                        float zoom = 1.0f/diff.magnitude*100f;

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

                            Rect myRect1 = new Rect(screenPos.x - modeWidth/2,
                                screenPos.y - modeHeight/2 + modeHeight - 7f*zoom, modeWidth, modeHeight);

                            GUI.DrawTexture(myRect1, _lightMode);

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

                            Rect myRect2 = new Rect(screenPos.x - manualPedestrianWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) + 5f * zoom,
                                screenPos.y - manualPedestrianHeight/2 - 9f*zoom, manualPedestrianWidth,
                                manualPedestrianHeight);

                            if (segmentDict.PedestrianEnabled)
                                GUI.DrawTexture(myRect2, _pedestrianMode2);
                            else
                                GUI.DrawTexture(myRect2, _pedestrianMode1);

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

                        Rect myRect3 = new Rect(screenPos.x - pedestrianWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) + 5f * zoom, screenPos.y - pedestrianHeight / 2 + 22f * zoom, pedestrianWidth, pedestrianHeight);

                        if (segmentDict.LightPedestrian == RoadBaseAI.TrafficLightState.Green)
                            GUI.DrawTexture(myRect3, _pedestrianLight2);
                        else if (segmentDict.LightPedestrian == RoadBaseAI.TrafficLightState.Red)
                            GUI.DrawTexture(myRect3, _pedestrianLight1);

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
                            float counterSize = 20f * zoom;

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

                            Rect myRectCounterNum =
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

                        if (!TrafficLightsManual.SegmentIsIncomingOneWay(segmentId, index))
                        {
                            var hasLeftSegment = TrafficPriority.HasLeftSegment(segmentId, index) && TrafficPriority.HasLeftLane(index, segmentId);
                            var hasForwardSegment = TrafficPriority.HasForwardSegment(segmentId, index) && TrafficPriority.HasForwardLane(index, segmentId);
                            var hasRightSegment = TrafficPriority.HasRightSegment(segmentId, index) && TrafficPriority.HasRightLane(index, segmentId);

                            if (segmentDict.CurrentMode == ManualSegmentLight.Mode.Simple)
                            {
                                // no arrow light
                                guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 3 && _hoveredNode == index ? 0.92f : 0.45f;

                                GUI.color = guiColor;

                                Rect myRect4 =
                                    new Rect(screenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) - pedestrianWidth + 5f * zoom,
                                        screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                    GUI.DrawTexture(myRect4, _lightSimple3);
                                else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                    GUI.DrawTexture(myRect4, _lightSimple1);

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
                                    float counterSize = 20f*zoom;

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

                                    Rect myRectCounterNum =
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
                            else if (segmentDict.CurrentMode == ManualSegmentLight.Mode.LeftForwardR)
                            {
                                if (hasLeftSegment)
                                {
                                    // left arrow light
                                    guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 3 && _hoveredNode == index ? 0.92f : 0.45f;

                                    GUI.color = guiColor;

                                    Rect myRect4 =
                                        new Rect(screenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth * 2 : lightWidth) - pedestrianWidth + 5f * zoom,
                                            screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                    if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect4, _lightLeft3);
                                    else if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect4, _lightLeft1);

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
                                        float counterSize = 20f * zoom;

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

                                        Rect myRectCounterNum =
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

                                Rect myRect5 =
                                    new Rect(screenPos.x - lightWidth / 2 - pedestrianWidth - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) + 5f * zoom,
                                        screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                if (hasForwardSegment && hasRightSegment)
                                {
                                    if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect5, _lightForwardRight3);
                                    else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect5, _lightForwardRight1);
                                }
                                else if (!hasRightSegment)
                                {
                                    if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect5, _lightForward3);
                                    else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect5, _lightForward1);
                                }
                                else
                                {
                                    if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect5, _lightRight3);
                                    else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect5, _lightRight1);
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
                                    float counterSize = 20f * zoom;

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

                                    Rect myRectCounterNum =
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
                            }
                            else if (segmentDict.CurrentMode == ManualSegmentLight.Mode.RightForwardL)
                            {
                                // forward-left light
                                guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 3 && _hoveredNode == index ? 0.92f : 0.45f;

                                GUI.color = guiColor;

                                Rect myRect4 = new Rect(screenPos.x - lightWidth/2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth*2 : lightWidth) - pedestrianWidth + 5f*zoom,
                                    screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                var lightType = 0;

                                if (hasForwardSegment && hasLeftSegment)
                                {
                                    if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect4, _lightForwardLeft3);
                                    else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect4, _lightForwardLeft1);

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
                                        GUI.DrawTexture(myRect4, _lightForward3);
                                    else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect4, _lightForward1);
                                }
                                else
                                {
                                    if (!hasRightSegment)
                                    {
                                        myRect4 = new Rect(screenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
                                            screenPos.y - lightHeight / 2, lightWidth, lightHeight);
                                    }

                                    if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect4, _lightLeft3);
                                    else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect4, _lightLeft1);
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
                                    float counterSize = 20f * zoom;

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

                                    Rect myRectCounterNum =
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

                                    Rect myRect5 =
                                        new Rect(screenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
                                            screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                    if (segmentDict.LightRight == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect5, _lightRight3);
                                    else if (segmentDict.LightRight == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect5, _lightRight1);


                                    if (myRect5.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
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
                                        float counterSize = 20f*zoom;

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

                                        Rect myRectCounterNum =
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
                            else // all
                            {
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

                                    Rect myRect4 =
                                        new Rect(screenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth) - pedestrianWidth + 5f * zoom,
                                            screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                    if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect4, _lightLeft3);
                                    else if (segmentDict.LightLeft == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect4, _lightLeft1);

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
                                        float counterSize = 20f * zoom;

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

                                        Rect myRectCounterNum =
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

                                    Rect myRect6 =
                                        new Rect(screenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth) - pedestrianWidth + 5f * zoom,
                                            screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                    if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect6, _lightForward3);
                                    else if (segmentDict.LightMain == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect6, _lightForward1);

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
                                        float counterSize = 20f * zoom;

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

                                        Rect myRectCounterNum =
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

                                    Rect myRect5 =
                                        new Rect(screenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
                                            screenPos.y - lightHeight/2, lightWidth, lightHeight);

                                    if (segmentDict.LightRight == RoadBaseAI.TrafficLightState.Green)
                                        GUI.DrawTexture(myRect5, _lightRight3);
                                    else if (segmentDict.LightRight == RoadBaseAI.TrafficLightState.Red)
                                        GUI.DrawTexture(myRect5, _lightRight1);

                                    if (myRect5.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel)
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
                                        float counterSize = 20f * zoom;

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

                                        Rect myRectCounterNum =
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
                            }
                        }
                    }
                }
            }

            if (!hoveredSegment)
            {
                _hoveredButton[0] = 0;
                _hoveredButton[1] = 0;
            }
        }

        protected void _guiLaneChange()
        {
            if (SelectedNode != 0 && SelectedSegment != 0)
            {
                var segment = Singleton<NetManager>.instance.m_segments.m_buffer[SelectedSegment];
                
                var info = segment.Info;
                uint num2 = segment.m_lanes;
                int num3 = 0;

                NetInfo.Direction dir = NetInfo.Direction.Forward;
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

                if (windowRect3.Contains(Event.current.mousePosition))
                {
                    _cursorInSecondaryPanel = true;
                }
                else
                {
                    _cursorInSecondaryPanel = false;
                }
            }
        }

        protected void _guiLaneChangeWindow(int num)
        {
            NetManager instance = Singleton<NetManager>.instance;

            var segment = instance.m_segments.m_buffer[SelectedSegment];

            var info = segment.Info;

            uint num2 = segment.m_lanes;
            int num3 = 0;

            List<float[]> laneList = new List<float[]>();

            NetInfo.Direction dir = NetInfo.Direction.Forward;
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

        protected void _guiLaneRestrictions()
        {
            if (SelectedSegmentIndexes.Count < 1)
            {
                return;
            }

            NetManager instance = Singleton<NetManager>.instance;

            var segment2 = instance.m_segments.m_buffer[SelectedSegmentIndexes[0]];

            var info2 = segment2.Info;

            uint num2 = segment2.m_lanes;
            int num3 = 0;

            var numLanes = 0;

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

            var width = !TrafficRoadRestrictions.IsSegment(SelectedSegmentIndexes[0]) ? 120 : numLanes*120;

            var windowRect3 = new Rect(275, 80, width, 185);

            if (TrafficLightsManual.SegmentIsOneWay(SelectedSegment))
            {
                GUILayout.Window(251, windowRect3, _guiLaneRestrictionsOneWayWindow, "", style);
            }

            if (windowRect3.Contains(Event.current.mousePosition))
            {
                _cursorInSecondaryPanel = true;
            }
            else
            {
                _cursorInSecondaryPanel = false;
            }
        }

        private int _setSpeed = -1;

        protected void _guiLaneRestrictionsOneWayWindow(int num)
        {
            if (!TrafficRoadRestrictions.IsSegment(SelectedSegmentIndexes[0]))
            {
                if (GUILayout.Button("Create group"))
                {
                    for (var i = 0; i < SelectedSegmentIndexes.Count; i++)
                    {
                        TrafficRoadRestrictions.AddSegment(SelectedSegmentIndexes[i], SelectedSegmentIndexes);

                        NetManager instance0 = Singleton<NetManager>.instance;

                        var segment0 = instance0.m_segments.m_buffer[SelectedSegmentIndexes[i]];

                        var info0 = segment0.Info;

                        uint num20 = segment0.m_lanes;
                        int num30 = 0;

                        var restSegment = TrafficRoadRestrictions.GetSegment(SelectedSegmentIndexes[i]);

                        List<float[]> laneList0 = new List<float[]>();
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

                        if (!TrafficLightsManual.SegmentIsOneWay(SelectedSegmentIndexes[i]))
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

                        foreach (float[] lane in laneList0)
                        {
                            restSegment.AddLane((uint)lane[0], (int)lane[2], info0.m_lanes[(int)lane[2]].m_finalDirection);
                        }
                    }
                }
                return;
            }

            if (GUILayout.Button("Delete group"))
            {
                foreach (var selectedSegmentIndex in SelectedSegmentIndexes)
                {
                    TrafficRoadRestrictions.RemoveSegment(selectedSegmentIndex);
                }

                SelectedSegmentIndexes.Clear();
                return;
            }

            if (GUILayout.Button("Add zoning", GUILayout.Width(140)))
            {
                foreach (var selectedSegmentIndex in SelectedSegmentIndexes)
                {
                    var segment = Singleton<NetManager>.instance.m_segments.m_buffer[selectedSegmentIndex];
                    var info = segment.Info;

                    CreateZoneBlocks(selectedSegmentIndex, ref Singleton<NetManager>.instance.m_segments.m_buffer[selectedSegmentIndex], info);
                }
            }

            if (GUILayout.Button("Remove zoning", GUILayout.Width(140)))
            {
                foreach (var selectedSegmentIndex in SelectedSegmentIndexes)
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

            var segment2 = instance.m_segments.m_buffer[SelectedSegmentIndexes[0]];

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

            if (!TrafficLightsManual.SegmentIsOneWay(SelectedSegmentIndexes[0]))
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
                    var resSegment = TrafficRoadRestrictions.GetSegment(SelectedSegmentIndexes[0]);
                    var resSpeed = resSegment.SpeedLimits[(int) laneList[i][2]];

                    if (_setSpeed == (int)laneList[i][2])
                    {
                        SliderValues[(int) laneList[i][2]] =
                            GUILayout.HorizontalSlider(SliderValues[(int) laneList[i][2]],
                                20f, 150f, GUILayout.Height(20));

                        if (GUILayout.Button("Set Speed " + (int)SliderValues[(int) laneList[i][2]]))
                        {
                            foreach (var restrictionSegment in SelectedSegmentIndexes.Select(TrafficRoadRestrictions.GetSegment))
                            {
                                restrictionSegment.SpeedLimits[(int) laneList[i][2]] =
                                    SliderValues[(int) laneList[i][2]]/
                                    50f;
                            }

                            _setSpeed = -1;
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Max speed " + (int)(resSpeed > 0.1f ? resSpeed*50f : info2.m_lanes[(int) laneList[i][2]].m_speedLimit*50f)))
                        {
                            SliderValues[(int) laneList[i][2]] = info2.m_lanes[(int) laneList[i][2]].m_speedLimit*50f;
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

        public void LaneFlag(uint laneId, NetLane.Flags flag)
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
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }

        private void CreateZoneBlocks(int segment, ref NetSegment data, NetInfo info)
        {
            NetManager instance = Singleton<NetManager>.instance;
            Randomizer randomizer = new Randomizer(segment);
            Vector3 position = instance.m_nodes.m_buffer[data.m_startNode].m_position;
            Vector3 position2 = instance.m_nodes.m_buffer[data.m_endNode].m_position;
            Vector3 startDirection = data.m_startDirection;
            Vector3 endDirection = data.m_endDirection;
            float num = startDirection.x * endDirection.x + startDirection.z * endDirection.z;
            bool flag = !NetSegment.IsStraight(position, startDirection, position2, endDirection);
            float num2 = Mathf.Max(8f, info.m_halfWidth);
            float num3 = 32f;
            if (flag)
            {
                float num4 = VectorUtils.LengthXZ(position2 - position);
                bool flag2 = startDirection.x * endDirection.z - startDirection.z * endDirection.x > 0f;
                bool flag3 = num < -0.8f || num4 > 50f;
                if (flag2)
                {
                    num2 = -num2;
                    num3 = -num3;
                }
                Vector3 vector = position - new Vector3(startDirection.z, 0f, -startDirection.x) * num2;
                Vector3 vector2 = position2 + new Vector3(endDirection.z, 0f, -endDirection.x) * num2;
                Vector3 vector3;
                Vector3 vector4;
                NetSegment.CalculateMiddlePoints(vector, startDirection, vector2, endDirection, true, true, out vector3, out vector4);
                if (flag3)
                {
                    float num5 = num * 0.025f + 0.04f;
                    float num6 = num * 0.025f + 0.06f;
                    if (num < -0.9f)
                    {
                        num6 = num5;
                    }
                    Bezier3 bezier = new Bezier3(vector, vector3, vector4, vector2);
                    vector = bezier.Position(num5);
                    vector3 = bezier.Position(0.5f - num6);
                    vector4 = bezier.Position(0.5f + num6);
                    vector2 = bezier.Position(1f - num5);
                }
                else
                {
                    Bezier3 bezier2 = new Bezier3(vector, vector3, vector4, vector2);
                    vector3 = bezier2.Position(0.86f);
                    vector = bezier2.Position(0.14f);
                }
                float num7;
                Vector3 vector5 = VectorUtils.NormalizeXZ(vector3 - vector, out num7);
                int num8 = Mathf.FloorToInt(num7 / 8f + 0.01f);
                float num9 = num7 * 0.5f + (num8 - 8) * ((!flag2) ? -4f : 4f);
                if (num8 != 0)
                {
                    float angle = (!flag2) ? Mathf.Atan2(vector5.x, -vector5.z) : Mathf.Atan2(-vector5.x, vector5.z);
                    Vector3 position3 = vector + new Vector3(vector5.x * num9 - vector5.z * num3, 0f, vector5.z * num9 + vector5.x * num3);
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
                        float angle2 = (!flag2) ? Mathf.Atan2(vector5.x, -vector5.z) : Mathf.Atan2(-vector5.x, vector5.z);
                        Vector3 position4 = vector4 + new Vector3(vector5.x * num9 - vector5.z * num3, 0f, vector5.z * num9 + vector5.x * num3);
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
                Vector3 vector6 = position + new Vector3(startDirection.z, 0f, -startDirection.x) * num2;
                Vector3 vector7 = position2 - new Vector3(endDirection.z, 0f, -endDirection.x) * num2;
                Vector3 b;
                Vector3 c;
                NetSegment.CalculateMiddlePoints(vector6, startDirection, vector7, endDirection, true, true, out b, out c);
                Bezier3 bezier3 = new Bezier3(vector6, b, c, vector7);
                Vector3 vector8 = bezier3.Position(0.5f);
                Vector3 vector9 = bezier3.Position(0.25f);
                vector9 = Line2.Offset(VectorUtils.XZ(vector6), VectorUtils.XZ(vector8), VectorUtils.XZ(vector9));
                Vector3 vector10 = bezier3.Position(0.75f);
                vector10 = Line2.Offset(VectorUtils.XZ(vector7), VectorUtils.XZ(vector8), VectorUtils.XZ(vector10));
                Vector3 vector11 = vector6;
                Vector3 a = vector7;
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
                Vector3 vector12 = VectorUtils.NormalizeXZ(vector8 - vector6, out num11);
                int num12 = Mathf.FloorToInt(num11 / 8f + 0.01f);
                float num13 = num11 * 0.5f + (num12 - 8) * ((!flag2) ? 4f : -4f);
                if (num12 != 0)
                {
                    float angle3 = (!flag2) ? Mathf.Atan2(-vector12.x, vector12.z) : Mathf.Atan2(vector12.x, -vector12.z);
                    Vector3 position5 = vector6 + new Vector3(vector12.x * num13 + vector12.z * num3, 0f, vector12.z * num13 - vector12.x * num3);
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
                if (num12 != 0)
                {
                    float angle4 = (!flag2) ? Mathf.Atan2(-vector12.x, vector12.z) : Mathf.Atan2(vector12.x, -vector12.z);
                    Vector3 position6 = vector8 + new Vector3(vector12.x * num13 + vector12.z * num3, 0f, vector12.z * num13 - vector12.x * num3);
                    if (flag2)
                    {
                        Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndLeft, ref randomizer, position6, angle4, num12, data.m_buildIndex + 1u);
                    }
                    else
                    {
                        Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndRight, ref randomizer, position6, angle4, num12, data.m_buildIndex + 1u);
                    }
                }
            }
            else
            {
                num2 += num3;
                Vector2 vector13 = new Vector2(position2.x - position.x, position2.z - position.z);
                float magnitude = vector13.magnitude;
                int num14 = Mathf.FloorToInt(magnitude / 8f + 0.1f);
                int num15 = (num14 <= 8) ? num14 : (num14 + 1 >> 1);
                int num16 = (num14 <= 8) ? 0 : (num14 >> 1);
                if (num15 > 0)
                {
                    float num17 = Mathf.Atan2(startDirection.x, -startDirection.z);
                    Vector3 position7 = position + new Vector3(startDirection.x * 32f - startDirection.z * num2, 0f, startDirection.z * 32f + startDirection.x * num2);
                    Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartLeft, ref randomizer, position7, num17, num15, data.m_buildIndex);
                    position7 = position + new Vector3(startDirection.x * (num15 - 4) * 8f + startDirection.z * num2, 0f, startDirection.z * (num15 - 4) * 8f - startDirection.x * num2);
                    Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartRight, ref randomizer, position7, num17 + 3.14159274f, num15, data.m_buildIndex);
                }
                if (num16 > 0)
                {
                    float num18 = magnitude - num14 * 8f;
                    float num19 = Mathf.Atan2(endDirection.x, -endDirection.z);
                    Vector3 position8 = position2 + new Vector3(endDirection.x * (32f + num18) - endDirection.z * num2, 0f, endDirection.z * (32f + num18) + endDirection.x * num2);
                    Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndLeft, ref randomizer, position8, num19, num16, data.m_buildIndex + 1u);
                    position8 = position2 + new Vector3(endDirection.x * ((num16 - 4) * 8f + num18) + endDirection.z * num2, 0f, endDirection.z * ((num16 - 4) * 8f + num18) - endDirection.x * num2);
                    Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndRight, ref randomizer, position8, num19 + 3.14159274f, num16, data.m_buildIndex + 1u);
                }
            }
        }

        protected void _guiTimedTrafficLightsNode()
        {
            GUILayout.Window(252, _windowRect2, _guiTimedTrafficLightsNodeWindow, "Select nodes");

            if (_windowRect2.Contains(Event.current.mousePosition))
            {
                _cursorInSecondaryPanel = true;
            }
            else
            {
                _cursorInSecondaryPanel = false;
            }
        }

        protected void _guiTimedTrafficLightsNodeWindow(int num)
        {
            if (SelectedNodeIndexes.Count < 1)
            {
                GUILayout.Label("Select nodes");
            }
            else
            {
                var txt = SelectedNodeIndexes.Aggregate("", (current, t) => current + ("Node " + t + "\n"));

                GUILayout.Label(txt);

                if(GUILayout.Button("Next"))
                {
                    foreach (ushort selectedNodeIndex in SelectedNodeIndexes)
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
        }

        private bool _timedPanelAdd;
        private int _timedEditStep = -1;

        protected void _guiTimedControlPanel(int num)
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
                                StepValue = timedNodeMain.GetStep(i).NumSteps;

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
                    GUILayout.Label("Time: " + (int)StepValue, GUILayout.Width(60));
                    StepValue = GUILayout.HorizontalSlider(StepValue, 1f, 120f, GUILayout.Height(20));
                    if (GUILayout.Button("Save", GUILayout.Width(45)))
                    {
                        foreach (var timeNode in SelectedNodeIndexes.Select(TrafficLightsTimed.GetTimedLight))
                        {
                            timeNode.GetStep(_timedEditStep).NumSteps = (int)StepValue;
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
                    GUILayout.Label("Time: " + (int) StepValue, GUILayout.Width(60));
                    StepValue = GUILayout.HorizontalSlider(StepValue, 1f, 120f, GUILayout.Height(20));
                    if (GUILayout.Button("Add", GUILayout.Width(45)))
                    {
                        foreach (var timedNode in SelectedNodeIndexes.Select(TrafficLightsTimed.GetTimedLight))
                        {
                            timedNode.AddStep((int) StepValue);
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

            if (_timedEditStep < 0)
            {
                if (GUILayout.Button("REMOVE"))
                {
                    DisableTimed();
                    SelectedNodeIndexes.Clear();
                    SetToolMode(ToolMode.None);
                }
            }
        }

        protected void _guiPrioritySigns()
        {
            var hoveredSegment = false;

            if (SelectedNode != 0)
            {
                var node = GetNetNode(SelectedNode);

                for (var i = 0; i < 8; i++)
                {
                    int segmentId = node.GetSegment(i);

                    if (segmentId != 0)
                    {
                        var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];

                        var position = node.m_position;

                        if (segment.m_startNode == SelectedNode)
                        {
                            position.x += segment.m_startDirection.x * 10f;
                            position.y += segment.m_startDirection.y * 10f;
                            position.z += segment.m_startDirection.z * 10f;
                        }
                        else
                        {
                            position.x += segment.m_endDirection.x * 10f;
                            position.y += segment.m_endDirection.y * 10f;
                            position.z += segment.m_endDirection.z * 10f;
                        }

                        var screenPos = Camera.main.WorldToScreenPoint(position);

                        Vector3 diff = position - Camera.main.transform.position;
                        float zoom = 1.0f / diff.magnitude * 100f;

                        var size = 85f * zoom;

                        screenPos.y = Screen.height - screenPos.y;

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

                        Rect myRect = new Rect(screenPos.x - size/2, screenPos.y - size/2, size, size);

                        var isPrioritySegment = TrafficPriority.IsPrioritySegment(SelectedNode, segmentId);

                        if (isPrioritySegment)
                        {
                            var prioritySegment = TrafficPriority.GetPrioritySegment(SelectedNode, segmentId);

                            if (prioritySegment.Type == PrioritySegment.PriorityType.Main)
                            {
                                GUI.DrawTexture(myRect, _signPriority);

                                if (myRect.Contains(Event.current.mousePosition))
                                {
                                    _hoveredButton[0] = segmentId;
                                    _hoveredButton[1] = 0;
                                    hoveredSegment = true;

                                    if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                                    {
                                        _uiClickedSegment = true;
                                        prioritySegment.Type = PrioritySegment.PriorityType.Yield;
                                    }
                                }
                            }
                            else if (prioritySegment.Type == PrioritySegment.PriorityType.Yield)
                            {
                                GUI.DrawTexture(myRect, _signYield);

                                if (myRect.Contains(Event.current.mousePosition))
                                {
                                    _hoveredButton[0] = segmentId;
                                    _hoveredButton[1] = 0;
                                    hoveredSegment = true;

                                    if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                                    {
                                        _uiClickedSegment = true;
                                        prioritySegment.Type = PrioritySegment.PriorityType.Stop;
                                    }
                                }
                            }
                            else if (prioritySegment.Type == PrioritySegment.PriorityType.Stop)
                            {
                                GUI.DrawTexture(myRect, _signStop);

                                if (myRect.Contains(Event.current.mousePosition))
                                {
                                    _hoveredButton[0] = segmentId;
                                    _hoveredButton[1] = 0;
                                    hoveredSegment = true;

                                    if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                                    {
                                        _uiClickedSegment = true;

                                        prioritySegment.Type = PrioritySegment.PriorityType.None;
                                    }
                                }
                            }
                            else
                            {
                                GUI.DrawTexture(myRect, _signNone);

                                if (myRect.Contains(Event.current.mousePosition))
                                {
                                    _hoveredButton[0] = segmentId;
                                    _hoveredButton[1] = 0;
                                    hoveredSegment = true;

                                    if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                                    {
                                        _uiClickedSegment = true;

                                        var numMainRoads = 0;

                                        for (var s = 0; s < 8; s++)
                                        {
                                            var segmentId2 = node.GetSegment(s);

                                            if (segmentId2 != 0 && TrafficPriority.IsPrioritySegment(SelectedNode, segmentId2))
                                            {
                                                var prioritySegment2 = TrafficPriority.GetPrioritySegment(SelectedNode, segmentId2);

                                                if (prioritySegment2.Type == PrioritySegment.PriorityType.Main)
                                                {
                                                    numMainRoads++;
                                                }
                                            }
                                        }

                                        prioritySegment.Type = numMainRoads >= 2 ? PrioritySegment.PriorityType.Yield : PrioritySegment.PriorityType.Main;
                                    }
                                }
                            }
                        }
                        else
                        {
                            GUI.DrawTexture(myRect, _signNone);

                            if (myRect.Contains(Event.current.mousePosition))
                            {
                                _hoveredButton[0] = segmentId;
                                _hoveredButton[1] = 0;
                                hoveredSegment = true;

                                if (Input.GetMouseButtonDown(0) && !_uiClickedSegment)
                                {
                                    _uiClickedSegment = true;

                                    var numMainRoads = 0;

                                    for (var s = 0; s < 8; s++)
                                    {
                                        var segmentId2 = node.GetSegment(s);

                                        if (segmentId2 != 0 && TrafficPriority.IsPrioritySegment(SelectedNode, segmentId2))
                                        {
                                            var prioritySegment2 = TrafficPriority.GetPrioritySegment(SelectedNode, segmentId2);

                                            if (prioritySegment2.Type == PrioritySegment.PriorityType.Main)
                                            {
                                                numMainRoads++;
                                            }
                                        }
                                    }

                                    TrafficPriority.AddPrioritySegment(SelectedNode, segmentId, numMainRoads >= 2 ? PrioritySegment.PriorityType.Yield : PrioritySegment.PriorityType.Main);
                                }
                            }
                        }
                    }
                }
            }

            if (!hoveredSegment)
            {
                _hoveredButton[0] = 0;
                _hoveredButton[1] = 0;
            }
        }

        protected void _switchTrafficLights()
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

                for (int s = 0; s < 8; s++)
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
            if (SelectedNode != 0)
            {
                var node = GetNetNode(SelectedNode);
                var nodeSimulation = CustomRoadAI.GetNodeSimulation(SelectedNode);

                if (nodeSimulation == null)
                {
                    //node.Info.m_netAI = _myGameObject.GetComponent<CustomRoadAI>();
                    //node.Info.m_netAI.m_info = node.Info;
                    CustomRoadAI.AddNodeToSimulation(SelectedNode);
                    nodeSimulation = CustomRoadAI.GetNodeSimulation(SelectedNode);
                    nodeSimulation.FlagManualTrafficLights = true;

                    for (int s = 0; s < 8; s++)
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

                for (int s = 0; s < 8; s++)
                {
                    var segment = node.GetSegment(s);

                    if (segment != 0 && !TrafficPriority.IsPrioritySegment(SelectedNode, segment))
                    {
                        TrafficPriority.AddPrioritySegment(SelectedNode, segment, PrioritySegment.PriorityType.None);
                    }
                }
            }

            return false;
        }

        public static void DisableManual()
        {
            if (SelectedNode != 0)
            {
                var nodeSimulation = CustomRoadAI.GetNodeSimulation(SelectedNode);

                if (nodeSimulation != null && nodeSimulation.FlagManualTrafficLights)
                {
                    nodeSimulation.FlagManualTrafficLights = false;
                    CustomRoadAI.RemoveNodeFromSimulation(SelectedNode);
                }
            }
        }

        public void DisableTimed()
        {
            if (SelectedNodeIndexes.Count > 0)
            {
                foreach (var selectedNodeIndex in SelectedNodeIndexes)
                {
                    GetNetNode(selectedNodeIndex);
                    var nodeSimulation = CustomRoadAI.GetNodeSimulation(selectedNodeIndex);

                    TrafficLightsTimed.RemoveTimedLight(selectedNodeIndex);

                    if (nodeSimulation != null)
                    {
                        nodeSimulation.FlagTimedTrafficLights = false;
                        CustomRoadAI.RemoveNodeFromSimulation(selectedNodeIndex);
                    }
                }
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

        public static void AddListNode(ushort node)
        {
            SelectedNodeIndexes.Add(node);
        }

        public static bool ContainsListNode(ushort node)
        {
            return SelectedNodeIndexes.Contains(node);
        }

        public static void RemoveListNode(ushort node)
        {
            SelectedNodeIndexes.Remove(node);
        }

        public static void AddListSegment(int segment)
        {
            SelectedSegmentIndexes.Add(segment);
        }

        public static bool ContainsListSegment(int segment)
        {
            return SelectedSegmentIndexes.Contains(segment);
        }

        public static void RemoveListSegment(int segment)
        {
            SelectedSegmentIndexes.Remove(segment);
        }
    }
}
