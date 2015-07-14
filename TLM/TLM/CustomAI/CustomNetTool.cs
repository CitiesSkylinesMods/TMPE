using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace TrafficManager.CustomAI
{
    class CustomNetTool : NetTool
    {
         //m_elevation
         //m_controlPoints
         //m_controlPointCount
         //m_upgrading
         //m_bulldozerTool

        public static FieldInfo fieldControlPoints;
        public static FieldInfo fieldControlPointCount;
        public static FieldInfo fieldElevation;
        public static FieldInfo fieldUpgrading;
        public static FieldInfo fieldBulldozerTool;
        public static FieldInfo fieldUpgradedSegments;

        public static FieldInfo getField(string field)
        {
            Type stockNetTool = typeof(NetTool);
            BindingFlags fieldFlags = BindingFlags.NonPublic | BindingFlags.Instance;

            return stockNetTool.GetField(field, fieldFlags);
        }

        public static NetTool.ControlPoint[] FieldControlPoints
        {
            get { return (NetTool.ControlPoint[])getField("m_controlPoints").GetValue(ToolsModifierControl.GetTool<NetTool>()); }
            set { fieldControlPoints = getField("m_controlPoints"); getField("m_controlPoints").SetValue(ToolsModifierControl.GetTool<NetTool>(), value); }
        }

        public static int FieldControlPointCount
        {
            get { return (int)getField("m_controlPointCount").GetValue(ToolsModifierControl.GetTool<NetTool>()); }
            set { fieldControlPointCount = getField("m_controlPointCount"); getField("m_controlPointCount").SetValue(ToolsModifierControl.GetTool<NetTool>(), value); }
        }

        public static int FieldElevation
        {
            get { return (int)getField("m_elevation").GetValue(ToolsModifierControl.GetTool<NetTool>()); }
            set { fieldElevation = getField("m_elevation"); getField("m_elevation").SetValue(ToolsModifierControl.GetTool<NetTool>(), value); }
        }

        public static bool FieldUpgrading
        {
            get { return Convert.ToBoolean(getField("m_upgrading").GetValue(ToolsModifierControl.GetTool<NetTool>())); }
            set { fieldUpgrading = getField("m_upgrading"); getField("m_upgrading").SetValue(ToolsModifierControl.GetTool<NetTool>(), value); }
        }

        public static BulldozeTool FieldBulldozerTool
        {
            get { return (BulldozeTool)getField("m_bulldozerTool").GetValue(ToolsModifierControl.GetTool<NetTool>()); }
            set { fieldBulldozerTool = getField("m_bulldozerTool"); getField("m_bulldozerTool").SetValue(ToolsModifierControl.GetTool<NetTool>(), value); }
        }

        public static HashSet<ushort> FieldUpgradedSegments
        {
            get { return (HashSet<ushort>)getField("m_upgradedSegments").GetValue(ToolsModifierControl.GetTool<NetTool>()); }
            set { fieldUpgradedSegments = getField("m_upgradedSegments"); getField("m_upgradedSegments").SetValue(ToolsModifierControl.GetTool<NetTool>(), value); }
        }

        public bool CreateNodeImpl(NetInfo info, bool needMoney, bool switchDirection, NetTool.ControlPoint startPoint, NetTool.ControlPoint middlePoint, NetTool.ControlPoint endPoint)
        {
            bool flag = endPoint.m_node != 0 || endPoint.m_segment != 0;
            ushort num;
            ushort num2;
            int num3;
            int num4;
            if (NetTool.CreateNode(info, startPoint, middlePoint, endPoint, NetTool.m_nodePositionsSimulation, 1000, true, false, true, needMoney, false, switchDirection, 0, out num, out num2, out num3, out num4) == ToolBase.ToolErrors.None)
            {
                NetTool.CreateNode(info, startPoint, middlePoint, endPoint, NetTool.m_nodePositionsSimulation, 1000, false, false, true, needMoney, false, switchDirection, 0, out num, out num2, out num3, out num4);
                NetManager instance = Singleton<NetManager>.instance;
                endPoint.m_segment = 0;
                endPoint.m_node = num;
                if (num2 != 0)
                {
                    if (CustomNetTool.FieldUpgrading)
                    {
                        while (!Monitor.TryEnter(CustomNetTool.FieldUpgradedSegments, SimulationManager.SYNCHRONIZE_TIMEOUT))
                        {
                        }
                        try
                        {
                            CustomNetTool.FieldUpgradedSegments.Add(num2);
                        }
                        finally
                        {
                            Monitor.Exit(CustomNetTool.FieldUpgradedSegments);
                        }
                    }
                    if (instance.m_segments.m_buffer[(int)num2].m_startNode == num)
                    {
                        endPoint.m_direction = -instance.m_segments.m_buffer[(int)num2].m_startDirection;
                    }
                    else if (instance.m_segments.m_buffer[(int)num2].m_endNode == num)
                    {
                        endPoint.m_direction = -instance.m_segments.m_buffer[(int)num2].m_endDirection;
                    }
                }
                CustomNetTool.FieldControlPoints[0] = endPoint;
                CustomNetTool.FieldElevation = Mathf.Max(0, Mathf.RoundToInt(endPoint.m_elevation / 12f));
                if (num != 0 && (instance.m_nodes.m_buffer[(int)num].m_flags & NetNode.Flags.Outside) != NetNode.Flags.None)
                {
                    CustomNetTool.FieldControlPointCount = 0;
                }
                else if (this.m_mode == NetTool.Mode.Freeform && CustomNetTool.FieldControlPointCount == 2)
                {
                    middlePoint.m_position = endPoint.m_position * 2f - middlePoint.m_position;
                    middlePoint.m_elevation = endPoint.m_elevation * 2f - middlePoint.m_elevation;
                    middlePoint.m_direction = endPoint.m_direction;
                    middlePoint.m_node = 0;
                    middlePoint.m_segment = 0;
                    CustomNetTool.FieldControlPoints[1] = middlePoint;
                    CustomNetTool.FieldControlPointCount = 2;
                }
                else
                {
                    CustomNetTool.FieldControlPointCount = 1;
                }
                if (info.m_class.m_service > ItemClass.Service.Office)
                {
                    int num5 = info.m_class.m_service - ItemClass.Service.Office - 1;
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
                if (CustomNetTool.FieldUpgrading)
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
                    info.m_netAI.ConnectionSucceeded(num, ref Singleton<NetManager>.instance.m_nodes.m_buffer[(int)num]);
                }
                Singleton<GuideManager>.instance.m_notEnoughMoney.Deactivate();
                if (Singleton<GuideManager>.instance.m_properties != null && !CustomNetTool.FieldUpgrading && num2 != 0 && CustomNetTool.FieldBulldozerTool != null && CustomNetTool.FieldBulldozerTool.m_lastNetInfo != null && CustomNetTool.FieldBulldozerTool.m_lastNetInfo.m_netAI.CanUpgradeTo(info))
                {
                    ushort startNode = instance.m_segments.m_buffer[(int)num2].m_startNode;
                    ushort endNode = instance.m_segments.m_buffer[(int)num2].m_endNode;
                    Vector3 position = instance.m_nodes.m_buffer[(int)startNode].m_position;
                    Vector3 position2 = instance.m_nodes.m_buffer[(int)endNode].m_position;
                    Vector3 startDirection = instance.m_segments.m_buffer[(int)num2].m_startDirection;
                    Vector3 endDirection = instance.m_segments.m_buffer[(int)num2].m_endDirection;
                    if (Vector3.SqrMagnitude(CustomNetTool.FieldBulldozerTool.m_lastStartPos - position) < 1f && Vector3.SqrMagnitude(CustomNetTool.FieldBulldozerTool.m_lastEndPos - position2) < 1f && Vector2.Dot(VectorUtils.XZ(CustomNetTool.FieldBulldozerTool.m_lastStartDir), VectorUtils.XZ(startDirection)) > 0.99f && Vector2.Dot(VectorUtils.XZ(CustomNetTool.FieldBulldozerTool.m_lastEndDir), VectorUtils.XZ(endDirection)) > 0.99f)
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
