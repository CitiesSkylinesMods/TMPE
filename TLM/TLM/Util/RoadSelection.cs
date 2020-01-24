using System;
using ColossalFramework;
using System.Reflection;
using System.Collections.Generic;
using ICities;
using UnityEngine;

namespace TrafficManager.Util {
    public class RoadSelection : MonoBehaviour {
        public  static RoadSelection Instance { get; private set; } = null;

        public RoadSelection():base() { Instance = this; }

        public void OnDestroy() {
            Instance = null;
        }

        public int Length => GetPath()?.m_size ?? 0;

        public List<ushort> Selection {
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

        private NetAdjust netAdjust = NetManager.instance.NetAdjust;

        private FieldInfo field =
            typeof(NetAdjust).GetField("m_tempPath", BindingFlags.Instance | BindingFlags.NonPublic);

        private FastList<ushort> GetPath() =>
            (FastList<ushort>)field.GetValue(netAdjust);

        public string PrintSelection() {
            FastList<ushort> path = GetPath();
            string ret = $"path[{path?.m_size}] = ";
            for (int i = 0; i < Length; ++i) {
                ushort segID = path.m_buffer[i];
                ret += $"{segID} ";
            }
            return ret;
        }

        public delegate void Handler();
        public event Handler OnChanged;


        private class Threading: ThreadingExtensionBase {
            int prev_length = -2;
            ushort prev_segmentID = 0;
            public override void OnUpdate(float realTimeDelta, float simulationTimeDelta) {
                if (Instance == null)
                    return;
                // Assumptions:
                // - two different paths cannot share a segment.
                // - UI does not allow to move both ends of the selection simultanously.
                var path = Instance.GetPath();
                int len = path?.m_size ?? -1;
                ushort segmentID = len > 0 ? path.m_buffer[0] : (ushort)0;
                bool changed = len != prev_length && segmentID != prev_segmentID;
                if(changed) {
                    prev_length = len;
                    prev_segmentID = len > 0 ? path.m_buffer[0] : (ushort)0;
                    Instance.OnChanged.Invoke();
                }
            }
        }
    } // end class
} // end namesapce
