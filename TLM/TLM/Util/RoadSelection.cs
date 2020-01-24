using System;
using ColossalFramework;
using System.Reflection;
using System.Collections.Generic;
using ICities;

namespace TrafficManager.Util {
    public static class RoadSelection {
        public static int Length => GetPath()?.m_size ?? 0;

        public static List<ushort> Selection {
            get {
                if(Length > 0) {
                    FastList<ushort> path = GetPath();
                    var ret = new List<ushort>();
                    for (int i = 0; i < Length; ++i) {
                        ushort segID = path.m_buffer[i];
                        ret.Add(segID);
                    }
                }
                return null;
            }
        }

        private static NetAdjust netAdjust => NetManager.instance.NetAdjust;

        private static FieldInfo field =
            typeof(NetAdjust).GetField("m_tempPath", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FastList<ushort> GetPath() =>
            (FastList<ushort>)field.GetValue(netAdjust);

        public static string PrintSelection() {
            FastList<ushort> path = GetPath();
            string ret = $"path[{path?.m_size}] = ";
            for (int i = 0; i < Length; ++i) {
                ushort segID = path.m_buffer[i];
                ret += $"{segID} ";
            }
            return ret;
        }

        public delegate void Handler();
        public static event Handler OnChanged;

        private class Threading: ThreadingExtensionBase {
            int prev_length = -1;
            ushort prev_segmentID = 0;
            public override void OnUpdate(float realTimeDelta, float simulationTimeDelta) {
                // Assumptions:
                // - two different paths cannot share a segment.
                // - UI does not allow to move both ends of the selection simultanously.
                int len = Length;
                ushort segmentID = len > 0 ? GetPath().m_buffer[0] : (ushort)0;
                bool changed = len != prev_length && segmentID != prev_segmentID;
                if(changed) {
                    prev_length = len;
                    prev_segmentID = len > 0 ? GetPath().m_buffer[0] : (ushort)0;
                    OnChanged.Invoke();
                }
            }
        }
    } // end class
} // end namesapce
