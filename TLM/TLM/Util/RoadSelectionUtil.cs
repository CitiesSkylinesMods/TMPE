using CSUtil.Commons;
using ICities;
using System.Collections.Generic;
using System.Reflection;

namespace TrafficManager.Util {
    public class RoadSelectionUtil {
        /// instance of singleton
        public static RoadSelectionUtil Instance { get; private set; }

        public RoadSelectionUtil() : base() {
            Instance = this;
        }

        public static void Release() {
            Instance.OnChanged = null;
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

        public class Threading : ThreadingExtensionBase {
            private int prev_length = -2;
            private ushort prev_segmentID = 0;

            public override void OnUpdate(float realTimeDelta, float simulationTimeDelta) {
                if (Instance == null) {
                    return;
                }
                // Performance critical part of the code:
                var path = Instance.GetPath();
                int len = path?.m_size ?? -1;
                ushort segmentID = len > 0 ? path.m_buffer[0] : (ushort)0;

                // Assumptions:
                //  A- two different paths cannot share a segment.
                //  B- UI does not allow to move both ends of the selection simultanously.
                // Conclusions:
                //  A- If user choses another path, all segments in path.m_buffer change.
                //  B- If user modifies a path, the length of the path changes.
                // Caveat: 
                //  A- Changing the center of selection without changing selected segments is
                //   detected as selection changed. (it deactivates all buttons) 
                bool changed = len != prev_length || segmentID != prev_segmentID;

                if (changed && len == prev_length) {
                    // this part is not so performance critical anymore.
                    // caveat A is addressed here: changing center of selection is not recognised as
                    // selection changed.
                    for (int i = 0; i < len; ++i) {
                        if (prev_segmentID == path.m_buffer[i])
                            changed = false;
                    }
                }
                if (changed) {
                    Log._Debug("RoadSelection.Threading.OnUpdate() road selection changed");
                    prev_length = len;
                    Instance.OnChanged?.Invoke();
                }
                prev_segmentID = segmentID;
            }
        }
    } // end class
} // end namesapce
