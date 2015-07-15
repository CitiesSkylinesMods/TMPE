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
    }
}
