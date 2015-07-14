using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace TrafficManager
{
    class CustomNetTool : NetTool
    {
        private static FieldInfo _fieldControlPoints;
        private static FieldInfo _fieldControlPointCount;
        private static FieldInfo _fieldElevation;
        private static FieldInfo _fieldUpgrading;
        private static FieldInfo _fieldBulldozerTool;
        private static FieldInfo _fieldUpgradedSegments;

        public static FieldInfo GetField(string field)
        {
            var stockNetTool = typeof(NetTool);
            const BindingFlags fieldFlags = BindingFlags.NonPublic | BindingFlags.Instance;

            return stockNetTool.GetField(field, fieldFlags);
        }

        public static ControlPoint[] FieldControlPoints
        {
            get { return (ControlPoint[])GetField("m_controlPoints").GetValue(ToolsModifierControl.GetTool<NetTool>()); }
            set
            {
                _fieldControlPoints = GetField("m_controlPoints");
                GetField("m_controlPoints").SetValue(ToolsModifierControl.GetTool<NetTool>(), value);
            }
        }

        public static int FieldControlPointCount
        {
            get { return (int)GetField("m_controlPointCount").GetValue(ToolsModifierControl.GetTool<NetTool>()); }
            set
            {
                _fieldControlPointCount = GetField("m_controlPointCount");
                GetField("m_controlPointCount").SetValue(ToolsModifierControl.GetTool<NetTool>(), value);
            }
        }

        public static int FieldElevation
        {
            get { return (int)GetField("m_elevation").GetValue(ToolsModifierControl.GetTool<NetTool>()); }
            set
            {
                _fieldElevation = GetField("m_elevation");
                GetField("m_elevation").SetValue(ToolsModifierControl.GetTool<NetTool>(), value);
            }
        }

        public static bool FieldUpgrading
        {
            get { return Convert.ToBoolean(GetField("m_upgrading").GetValue(ToolsModifierControl.GetTool<NetTool>())); }
            set
            {
                _fieldUpgrading = GetField("m_upgrading");
                GetField("m_upgrading").SetValue(ToolsModifierControl.GetTool<NetTool>(), value);
            }
        }

        public static BulldozeTool FieldBulldozerTool
        {
            get { return (BulldozeTool)GetField("m_bulldozerTool").GetValue(ToolsModifierControl.GetTool<NetTool>()); }
            set
            {
                _fieldBulldozerTool = GetField("m_bulldozerTool");
                GetField("m_bulldozerTool").SetValue(ToolsModifierControl.GetTool<NetTool>(), value);
            }
        }

        public static HashSet<ushort> FieldUpgradedSegments
        {
            get { return (HashSet<ushort>)GetField("m_upgradedSegments").GetValue(ToolsModifierControl.GetTool<NetTool>()); }
            set
            {
                _fieldUpgradedSegments = GetField("m_upgradedSegments");
                GetField("m_upgradedSegments").SetValue(ToolsModifierControl.GetTool<NetTool>(), value);
            }
        }

        public bool CreateNodeImpl(NetInfo info, bool needMoney, bool switchDirection, ControlPoint startPoint, ControlPoint middlePoint, ControlPoint endPoint)
        {
            var flag = endPoint.m_node != 0 || endPoint.m_segment != 0;
            ushort num;
            ushort num2;
            int num3;
            int num4;
            if (CreateNode(info, startPoint, middlePoint, endPoint, m_nodePositionsSimulation, 1000, true, false, true, needMoney, false, switchDirection, 0, out num, out num2, out num3, out num4) == ToolErrors.None)
            {
                CreateNode(info, startPoint, middlePoint, endPoint, m_nodePositionsSimulation, 1000, false, false, true, needMoney, false, switchDirection, 0, out num, out num2, out num3, out num4);
                var instance = Singleton<NetManager>.instance;
                endPoint.m_segment = 0;
                endPoint.m_node = num;
                if (num2 != 0)
                {
                    if (FieldUpgrading)
                    {
                        while (!Monitor.TryEnter(FieldUpgradedSegments, SimulationManager.SYNCHRONIZE_TIMEOUT))
                        {
                        }
                        try
                        {
                            FieldUpgradedSegments.Add(num2);
                        }
                        finally
                        {
                            Monitor.Exit(FieldUpgradedSegments);
                        }
                    }
                    if (instance.m_segments.m_buffer[num2].m_startNode == num)
                    {
                        endPoint.m_direction = -instance.m_segments.m_buffer[num2].m_startDirection;
                    }
                    else if (instance.m_segments.m_buffer[num2].m_endNode == num)
                    {
                        endPoint.m_direction = -instance.m_segments.m_buffer[num2].m_endDirection;
                    }
                }
                FieldControlPoints[0] = endPoint;
                FieldElevation = Mathf.Max(0, Mathf.RoundToInt(endPoint.m_elevation / 12f));
                if (num != 0 && (instance.m_nodes.m_buffer[num].m_flags & NetNode.Flags.Outside) != NetNode.Flags.None)
                {
                    FieldControlPointCount = 0;
                }
                else if (m_mode == Mode.Freeform && FieldControlPointCount == 2)
                {
                    middlePoint.m_position = endPoint.m_position * 2f - middlePoint.m_position;
                    middlePoint.m_elevation = endPoint.m_elevation * 2f - middlePoint.m_elevation;
                    middlePoint.m_direction = endPoint.m_direction;
                    middlePoint.m_node = 0;
                    middlePoint.m_segment = 0;
                    FieldControlPoints[1] = middlePoint;
                    FieldControlPointCount = 2;
                }
                else
                {
                    FieldControlPointCount = 1;
                }
                if (info.m_class.m_service > ItemClass.Service.Office)
                {
                    var num5 = info.m_class.m_service - ItemClass.Service.Office - 1;
                    Singleton<GuideManager>.instance.m_serviceNotUsed[num5].Disable();
                    Singleton<GuideManager>.instance.m_serviceNeeded[num5].Deactivate();
                }
                if (info.m_class.m_service == ItemClass.Service.Road)
                {
                    Singleton<CoverageManager>.instance.CoverageUpdated(ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Level.None);
                    Singleton<NetManager>.instance.m_roadsNotUsed.Disable();
                }
                if ((info.m_class.m_service == ItemClass.Service.Road || info.m_class.m_service == ItemClass.Service.PublicTransport || info.m_class.m_service == ItemClass.Service.Beautification) && (info.m_hasForwardVehicleLanes || info.m_hasBackwardVehicleLanes) && (!info.m_hasForwardVehicleLanes || !info.m_hasBackwardVehicleLanes))
                {
                    Singleton<NetManager>.instance.m_onewayRoadPlacement.Disable();
                }
                if (FieldUpgrading)
                {
                    //var priorityList = new Dictionary<ushort, int>();

                    //if (CustomRoadAI.GetNodeSimulation(startPoint.m_node) != null)
                    //{
                    //    var nodeDict = CustomRoadAI.GetNodeSimulation(startPoint.m_node);

                    //    if (nodeDict.FlagManualTrafficLights)
                    //    {
                    //        nodeDict.FlagManualTrafficLights = false;
                    //    }
                    //    if (nodeDict.FlagTimedTrafficLights)
                    //    {
                    //        nodeDict.FlagTimedTrafficLights = false;
                    //    }

                    //    CustomRoadAI.RemoveNodeFromSimulation(startPoint.m_node);
                    //}

                    //foreach (var prioritySegment in TrafficPriority.prioritySegments.Values)
                    //{
                    //    if (prioritySegment.node_1 == startPoint.m_node && !priorityList.ContainsKey(startPoint.m_node))
                    //    {
                    //        priorityList.Add(startPoint.m_node, prioritySegment.segment);
                    //    }
                    //    if (prioritySegment.node_2 == startPoint.m_node && !priorityList.ContainsKey(startPoint.m_node))
                    //    {
                    //        priorityList.Add(startPoint.m_node, prioritySegment.segment);
                    //    }
                    //}

                    //if (CustomRoadAI.GetNodeSimulation(endPoint.m_node) != null)
                    //{

                    //    var nodeDict = CustomRoadAI.GetNodeSimulation(endPoint.m_node);

                    //    if (nodeDict.FlagManualTrafficLights)
                    //    {
                    //        nodeDict.FlagManualTrafficLights = false;
                    //    }
                    //    if (nodeDict.FlagTimedTrafficLights)
                    //    {
                    //        nodeDict.FlagTimedTrafficLights = false;
                    //    }

                    //    CustomRoadAI.RemoveNodeFromSimulation(endPoint.m_node);
                    //}

                    //foreach (var prioritySegment in TrafficPriority.prioritySegments.Values)
                    //{
                    //    if (prioritySegment.node_1 == endPoint.m_node && !priorityList.ContainsKey(endPoint.m_node))
                    //    {
                    //        priorityList.Add(endPoint.m_node, prioritySegment.segment);
                    //    }
                    //    if (prioritySegment.node_2 == endPoint.m_node && !priorityList.ContainsKey(endPoint.m_node))
                    //    {
                    //        priorityList.Add(endPoint.m_node, prioritySegment.segment);
                    //    }
                    //}

                    //foreach (var pr in priorityList)
                    //{
                    //    TrafficPriority.removePrioritySegment(pr.Key, pr.Value);
                    //}

                    info.m_netAI.UpgradeSucceeded();
                }
                else if (flag && num != 0)
                {
                    info.m_netAI.ConnectionSucceeded(num, ref Singleton<NetManager>.instance.m_nodes.m_buffer[num]);
                }
                Singleton<GuideManager>.instance.m_notEnoughMoney.Deactivate();
                if (Singleton<GuideManager>.instance.m_properties != null && !FieldUpgrading && num2 != 0 && FieldBulldozerTool != null && FieldBulldozerTool.m_lastNetInfo != null && FieldBulldozerTool.m_lastNetInfo.m_netAI.CanUpgradeTo(info))
                {
                    var startNode = instance.m_segments.m_buffer[num2].m_startNode;
                    var endNode = instance.m_segments.m_buffer[num2].m_endNode;
                    var position = instance.m_nodes.m_buffer[startNode].m_position;
                    var position2 = instance.m_nodes.m_buffer[endNode].m_position;
                    var startDirection = instance.m_segments.m_buffer[num2].m_startDirection;
                    var endDirection = instance.m_segments.m_buffer[num2].m_endDirection;
                    if (Vector3.SqrMagnitude(FieldBulldozerTool.m_lastStartPos - position) < 1f && Vector3.SqrMagnitude(FieldBulldozerTool.m_lastEndPos - position2) < 1f && Vector2.Dot(VectorUtils.XZ(FieldBulldozerTool.m_lastStartDir), VectorUtils.XZ(startDirection)) > 0.99f && Vector2.Dot(VectorUtils.XZ(FieldBulldozerTool.m_lastEndDir), VectorUtils.XZ(endDirection)) > 0.99f)
                    {
                        Singleton<NetManager>.instance.m_manualUpgrade.Activate(Singleton<GuideManager>.instance.m_properties.m_manualUpgrade, info.m_class.m_service);
                    }
                }
                return true;
            }
            return false;
        }
    }
}
