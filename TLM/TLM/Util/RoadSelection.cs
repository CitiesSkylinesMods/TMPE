using System;
using System.Reflection;
using System.Collections.Generic;
using ICities;
using CSUtil.Commons;

namespace TrafficManager.Util {
    public class RoadSelection {
        // instace of singleton
        public static RoadSelection Instance { get; private set; }

        public RoadSelection():base() {
            Instance = this;
        }

        public static void Release() {
            Instance = null;
        }

        public int Length => GetPath()?.m_size ?? 0;

        /// <summary>
        /// Creates a list of selected segment IDs.
        /// Modifying the returned list has no side effects.
        /// </summary>
        public List<ushort> Selection {
            get {
                if (Length > 0) {
                    FastList<ushort> path = GetPath();
                    List<ushort> ret = new List<ushort>();
                    for (int i = 0; i < Length; ++i) {
                        ushort segmentId = path.m_buffer[i];
                        ret.Add(segmentId);
                    }
                    return ret;
                }
                return null;
            }
        }

        private NetAdjust netAdjust = NetManager.instance.NetAdjust;

        private FieldInfo field =
            typeof(NetAdjust).GetField("m_tempPath", BindingFlags.Instance | BindingFlags.NonPublic);

        private FastList<ushort> GetPath() =>
            (FastList<ushort>)field.GetValue(netAdjust);

        public delegate void Handler();

        /// <summary>
        /// Invoked every time road selection changes.
        /// </summary>
        public event Handler OnChanged;

        public class Threading: ThreadingExtensionBase {
            int prev_length = -2;
            ushort prev_segmentID = 0;

            public override void OnUpdate(float realTimeDelta, float simulationTimeDelta) {
                if (Instance == null) {
                    return;
                }
                var path = Instance.GetPath();
                int len = path?.m_size ?? -1;
                ushort segmentID = len > 0 ? path.m_buffer[0] : (ushort)0;

                // Assumptions:
                // - two different paths cannot share a segment.
                // - UI does not allow to move both ends of the selection simultanously.
                // Conclusions:
                // - If user choses another path, all segments in path.m_buffer change.
                // - If user modifies a path, the length of the path changes.
                bool changed = len != prev_length || segmentID != prev_segmentID;
                if(changed) {
                    Log._Debug("RoadSelection.Threading.OnUpdate() road selection changed\n" +
                        Environment.StackTrace);
                    prev_length = len;
                    prev_segmentID = segmentID;
                    Instance.OnChanged.Invoke();
                }
            }
        }
    } // end class
} // end namesapce
