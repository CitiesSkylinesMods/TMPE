using System;
using System.Collections.Generic;
using System.Text;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using UnityEngine;

namespace TrafficManager
{
    public enum TrafficLightNode
    {
        None = 0,
        Start = 1,
        End = 2
    }

    public class ToolTrafficLight : DefaultTool
    {
        private GameObject _myGameObject;

        private Rect _windowRect = new Rect(828, 128, 300, 300);

        private bool _mouseDown = false;
        private bool _mouseClicked = false;

        private ushort _hoveredNetNodeIdx;
        private ushort _selectedNetNodeIdx;

        private bool _isUiShown = false;

        private bool dasdsadweew = false;

        private int _hoveredSegmentButton = 0;

        private static Dictionary<ushort, TrafficLightSimulation> nodeDictionary = new Dictionary<ushort, TrafficLightSimulation>();
        // Expose protected property
        public new CursorInfo ToolCursor
        {
            get { return base.ToolCursor; }
            set { base.ToolCursor = value; }
        }

        // Overridden to disable base class behavior
        protected override void OnEnable()
        {
            if (_myGameObject == null)
            {
                _myGameObject = new GameObject();
                _myGameObject.AddComponent<CustomRoadAI>();
            }

            Log.Warning("dsads");
        }

        // Overridden to disable base class behavior
        protected override void OnDisable()
        {
        }

        public override void RenderGeometry(RenderManager.CameraInfo cameraInfo)
        {
            base.RenderGeometry(cameraInfo);

            if (_hoveredNetNodeIdx != 0)
            {
                m_toolController.RenderCollidingNotifications(cameraInfo, 0, 0);
            }
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            base.RenderOverlay(cameraInfo);

            if (_hoveredNetNodeIdx != 0 && _hoveredNetNodeIdx != _selectedNetNodeIdx)
            {
                Bezier3 bezier;

                var node = GetNetNode(_hoveredNetNodeIdx);
                var segment = Singleton<NetManager>.instance.m_segments.m_buffer[(int)node.m_segment0];

                bezier.a = node.m_position;
                bezier.d = node.m_position;

                var color = GetToolColor(false, false);

                NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d, segment.m_endDirection, false, false, out bezier.b, out bezier.c);
                _renderOverlay(cameraInfo, bezier, color);
            }

            if (_selectedNetNodeIdx != 0)
            {
                var node = GetNetNode(_selectedNetNodeIdx);

                var colorGreen = new Color(0.1f, 1f, 0.1f, 0.25f);
                var colorYellow = new Color(1f, 1f, 0.1f, 0.25f);
                var colorRed = new Color(1f, 0.1f, 0.1f, 0.25f);
                var colorGray = new Color(0.25f, 0.25f, 0.25f, 0.25f);

                var segmentId = 0;

                var instance = Singleton<NetManager>.instance;
                var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

                var color2 = colorGray;

                for (var i = 0; i < 8; i++)
                {
                    segmentId = node.GetSegment(i);

                    if (segmentId != 0)
                    {
                        var segment = Singleton<NetManager>.instance.m_segments.m_buffer[(int)segmentId];

                        var position = node.m_position;

                        if (segment.m_startNode == _selectedNetNodeIdx)
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

                        RoadBaseAI.TrafficLightState trafficLightState3;
                        RoadBaseAI.TrafficLightState trafficLightState4;
                        bool vehicles;
                        bool pedestrians;

                        RoadBaseAI.GetTrafficLightState(_selectedNetNodeIdx, ref instance.m_segments.m_buffer[(int)segmentId],
                            currentFrameIndex - 256u, out trafficLightState3, out trafficLightState4, out vehicles,
                            out pedestrians);

                        if (trafficLightState3 == RoadBaseAI.TrafficLightState.Green)
                        {
                            color2 = colorGreen;
                        }
                        else if (trafficLightState3 == RoadBaseAI.TrafficLightState.Red)
                        {
                            color2 = colorRed;
                        }
                        else
                        {
                            color2 = colorYellow;
                        }

                        _renderOverlay(cameraInfo, color2, position, segmentId != _hoveredSegmentButton);
                    }
                }
            }
        }
        public void _renderOverlay(RenderManager.CameraInfo cameraInfo, Bezier3 bezier, Color color)
        {
            float width = 8f;

            ToolManager expr_EA_cp_0 = Singleton<ToolManager>.instance;
            expr_EA_cp_0.m_drawCallData.m_overlayCalls = expr_EA_cp_0.m_drawCallData.m_overlayCalls + 1;
            Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(cameraInfo, color, bezier,
                width * 2f, width, width, -1f, 1280f, false, false);

            // 8 - small roads; 16 - big roads
        }

        public void _renderOverlay(RenderManager.CameraInfo cameraInfo, Color color, Vector3 position, bool alpha)
        {
            float width = 8f;

            ToolManager expr_EA_cp_0 = Singleton<ToolManager>.instance;
            expr_EA_cp_0.m_drawCallData.m_overlayCalls = expr_EA_cp_0.m_drawCallData.m_overlayCalls + 1;
            Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(cameraInfo, color, position, 10f, position.y - 100f, position.y + 100f, false, alpha);
        }

        public override void SimulationStep()
        {
            base.SimulationStep();

            var mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            var mouseRayLength = Camera.main.farClipPlane;
            var rayRight = Camera.main.transform.TransformDirection(Vector3.right);
            var mouseRayValid = !UIView.IsInsideUI() && Cursor.visible;

            if (mouseRayValid)
            {
                var defaultService = new ToolBase.RaycastService(ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Layer.Default);
                var input = new ToolBase.RaycastInput(mouseRay, mouseRayLength)
                {
                    m_rayRight = rayRight,
                    m_netService = defaultService,
                    m_ignoreNodeFlags = NetNode.Flags.None
                };
                RaycastOutput output;
                if (!RayCast(input, out output))
                {
                    //TODO: Fehlerbehandlung?
                    _hoveredNetNodeIdx = 0;
                    return;
                }

                var node = GetNetNode(output.m_netNode);

                if ((node.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None)
                {
                    _hoveredNetNodeIdx = output.m_netNode;
                }
                else
                {
                    _hoveredNetNodeIdx = 0;
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

                    if (_hoveredNetNodeIdx != 0)
                    {
                        _selectedNetNodeIdx = _hoveredNetNodeIdx;
                        ClickJunction();
                        showUI();
                    }
                    else
                    {
                        //hideUI();
                    }
                }
            }
            else
            {
                _mouseClicked = false;
            }

            
        }

        protected override void OnToolGUI()
        {
            _renderUI();
        }

        protected void showUI()
        {
            _isUiShown = true;
        }

        protected void hideUI()
        {
            _isUiShown = false;
        }

        protected void _renderUI()
        {
            if (_isUiShown)
            {
                _windowRect = GUILayout.Window(0, _windowRect, DoUiWindow, "Traffic Light Tool Window");
            }
        }

        public void DoUiWindow(int num)
        {
            var node = GetNetNode(_selectedNetNodeIdx);

            if (GUILayout.Button("Switch traffic lights"))
            {
                if ((node.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None)
                {
                    node.m_flags &= ~NetNode.Flags.TrafficLights;
                    node.Info.m_netAI = _myGameObject.GetComponent<NetAI>();
                    node.Info.m_netAI.m_info = node.Info;
                    node.Info.m_netAI.InitializePrefab();
                    removeNodeFromSimulation(_selectedNetNodeIdx);
                    Log.Warning("Traffic lights disabled");
                }
                else
                {
                    node.m_flags |= NetNode.Flags.TrafficLights;
                    Log.Warning("Traffic lights enabled");
                }

                SetNetNode(_selectedNetNodeIdx, node);
            }

            GUILayout.Space(20);

            if (getNodeSimulation(_selectedNetNodeIdx) == null)
            {
                if((node.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None)
                {
                    if (GUILayout.Button("Convert to custom"))
                    {
                        Log.Warning("Intersection set");
                        node.Info.m_netAI = _myGameObject.GetComponent<CustomRoadAI>();
                        node.Info.m_netAI.m_info = node.Info;
                        node.Info.m_netAI.InitializePrefab();
                        addNodeToSimulation(_selectedNetNodeIdx);
                    }
                }
            }
            else
            {
                _hoveredSegmentButton = 0;

                var nodeSimulation = getNodeSimulation(_selectedNetNodeIdx);

                if (GUILayout.Button("Convert to builtin"))
                {
                    Log.Warning("Intersection unset");
                    removeNodeFromSimulation(_selectedNetNodeIdx);
                }

                GUILayout.Space(20);

                if (!nodeSimulation.ManualTrafficLights)
                {
                    if (GUILayout.Button("Turn on manual control"))
                    {
                        nodeSimulation.ManualTrafficLights = true;
                    }
                }
                else
                {
                    if (GUILayout.Button("Turn off manual control"))
                    {
                        nodeSimulation.ManualTrafficLights = false;
                    }

                    for (int s = 0; s < node.CountSegments(); s++)
                    {
                        var segment = node.GetSegment(s);

                        if (segment != 0)
                        {
                            if (GUILayout.Button("Switch Light " + s))
                            {
                                nodeSimulation.ForceTrafficLights(ref node, segment);
                            }

                            if (Event.current.type == EventType.Repaint &&
                               GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                            {
                                _hoveredSegmentButton = segment;
                            }
                        }
                    }
                }
            }
        }

        public void ClickJunction()
        {
        }

        public void getEachTrafficLight(ushort nodeID)
        {
            //var test = "";
            //ushort segm;

            //for (int i = 0; i < 8; i++)
            //{
            //    segm =
            //        Singleton<NetManager>.instance.m_nodes.m_buffer[(int)segment.m_startNode]
            //            .GetSegment(i);

            //    if (segm != 0)
            //    {
            //        test += "Int " + i + ": " +
            //                Singleton<NetManager>.instance.m_segments.m_buffer[segm].m_trafficLightState0 + " " +
            //                Singleton<NetManager>.instance.m_segments.m_buffer[segm].m_trafficLightState1 + ";";
            //    }
            //}
        }

        private NetNode GetCurrentNetNode()
        {
            return GetNetNode(_hoveredNetNodeIdx);
        }
        private static NetNode GetNetNode(ushort index)
        {
            return Singleton<NetManager>.instance.m_nodes.m_buffer[index];
        }

        private static void SetNetNode(ushort index, NetNode node)
        {
            Singleton<NetManager>.instance.m_nodes.m_buffer[index] = node;
        }

        public static void addNodeToSimulation(ushort nodeID)
        {
            nodeDictionary.Add(nodeID, new TrafficLightSimulation(nodeID));
        }

        public static void removeNodeFromSimulation(ushort nodeID)
        {
            nodeDictionary.Remove(nodeID);
        }

        public static TrafficLightSimulation getNodeSimulation(ushort nodeID)
        {
            if (nodeDictionary.ContainsKey(nodeID))
            {
                return nodeDictionary[nodeID];
            }

            return null;
        }
    }
}
