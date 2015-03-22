using System;
using System.Collections.Generic;
using System.Text;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace TrafficManager
{
    public enum LaneNode
    {
        None = 0,
        Start = 1,
        End = 2
    }

    public class ToolLaneChange : DefaultTool
    {
        private GameObject myGameObject;

        private Rect _windowRect = new Rect(828, 128, 300, 300);

        public ToolMode toolMode = ToolMode.None;

        public bool isHoveringSegment = false;
        public bool mouseDown = false;
        public bool mouseClicked = false;

        public int segmentIndex;
        public NetSegment segment;

        public ushort nodeID;
        public NetNode node;

        public LaneNode LaneChangeNode;

        public NetInfo newPrefab;

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
        }

        // Overridden to disable base class behavior
        protected override void OnDisable()
        {
        }

        public override void RenderGeometry(RenderManager.CameraInfo cameraInfo)
        {
            base.RenderGeometry(cameraInfo);

            if (isHoveringSegment)
            {
                m_toolController.RenderCollidingNotifications(cameraInfo, 0, 0);
            }
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            base.RenderOverlay(cameraInfo);

            if (isHoveringSegment)
            {
                NetInfo info = segment.Info;

                if (info == null)
                {
                    return;
                }
                if ((segment.m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None && !info.m_overlayVisible)
                {
                    return;
                }

                Bezier3 bezier;

                if (LaneChangeNode == LaneNode.Start)
                {
                    bezier.a = Singleton<NetManager>.instance.m_nodes.m_buffer[(int)segment.m_startNode].m_position;
                    bezier.d = Singleton<NetManager>.instance.m_nodes.m_buffer[(int)segment.m_startNode].m_position;
                    NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d, segment.m_endDirection, false, false, out bezier.b, out bezier.c);
                    _renderOverlay(cameraInfo, info, bezier);
                }
                else if (LaneChangeNode == LaneNode.End)
                {
                    bezier.a = Singleton<NetManager>.instance.m_nodes.m_buffer[(int)segment.m_endNode].m_position;
                    bezier.d = Singleton<NetManager>.instance.m_nodes.m_buffer[(int)segment.m_endNode].m_position;
                    NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d, segment.m_endDirection, false, false, out bezier.b, out bezier.c);
                    _renderOverlay(cameraInfo, info, bezier);
                }
            }
        }

        public void _renderOverlay(RenderManager.CameraInfo cameraInfo, NetInfo info, Bezier3 bezier)
        {
            Color color = GetToolColor(false, false);

            float width = 8f;

            ToolManager expr_EA_cp_0 = Singleton<ToolManager>.instance;
            expr_EA_cp_0.m_drawCallData.m_overlayCalls = expr_EA_cp_0.m_drawCallData.m_overlayCalls + 1;
            Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(cameraInfo, color, bezier,
                width * 2f, width, width, -1f, 1280f, false, false);

            // 8 - small roads; 16 - big roads
        }

        // Expose protected method
        public new static bool RayCast(ToolBase.RaycastInput input, out ToolBase.RaycastOutput output)
        {
            return NetTool.RayCast(input, out output);
        }

        protected override void OnToolUpdate()
        {
            mouseDown = Input.GetMouseButton(0);

            if (mouseDown)
            {
                if (!mouseClicked)
                {
                    mouseClicked = true;

                    if (isHoveringSegment)
                    {
                        clickTrafficLight();
                    }
                }
            }
            else
            {
                mouseClicked = false;
            }
        }

        public void clickTrafficLight()
        {
            if (LaneChangeNode == LaneNode.Start)
            {
                nodeID = segment.m_startNode;
            }
            else if (LaneChangeNode == LaneNode.End)
            {
                nodeID = segment.m_endNode;
            }

            node = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeID];

            if (myGameObject == null)
            {
                myGameObject = new GameObject();
                myGameObject.AddComponent<CustomRoadAI>();
            }

            if (getNodeSimulation(nodeID) == null)
            {
                Log.Warning("Intersection set");
                node.Info.m_netAI = myGameObject.GetComponent<CustomRoadAI>();
                node.Info.m_netAI.m_info = node.Info;
                node.Info.m_netAI.InitializePrefab();
                addNodeToSimulation(nodeID);
            }
            else
            {
                //var customAI = node.Info.m_netAI as CustomRoadAI;

                //customAI.setTfFLag();
            }
        }

        public void getEachTrafficLight(ushort nodeID)
        {
            var test = "";
            ushort segm;

            for (int i = 0; i < 8; i++)
            {
                segm =
                    Singleton<NetManager>.instance.m_nodes.m_buffer[(int)segment.m_startNode]
                        .GetSegment(i);

                if (segm != 0)
                {
                    test += "Int " + i + ": " +
                            Singleton<NetManager>.instance.m_segments.m_buffer[segm].m_trafficLightState0 + " " +
                            Singleton<NetManager>.instance.m_segments.m_buffer[segm].m_trafficLightState1 + ";";
                }
            }
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
