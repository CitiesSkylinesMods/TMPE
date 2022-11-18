namespace TrafficManager.Util {
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using ColossalFramework;
    using ICities;
    using CSUtil.Commons;
    using TrafficManager.UI;

    public class RoadSelectionUtil {
        public RoadSelectionUtil() : base() {
            Instance = this;
        }

        // instance of singleton
        public static RoadSelectionUtil Instance { get; private set; }

        /// <summary>
        /// Enables and refreshes overlay for various traffic rules influenced by road selection panel.
        /// </summary>
        public static void ShowMassEditOverlay() {
            var tmTool = ModUI.GetTrafficManagerTool();
            if (tmTool == null) {
                Log.Error("ModUI.GetTrafficManagerTool() returned null");
                return;
            }
            UI.SubTools.PrioritySigns.MassEditOverlay.Show = true;
            tmTool.SetToolMode(ToolMode.None);
            tmTool.InitializeSubTools();
            Log._Debug("Mass edit overlay enabled");
        }

        public static void Release() {
            if (Instance != null) {
                Instance.OnChanged = null;
                Instance = null;
            }
        }

        /// <summary>
        /// Copies road name from <paramref name="sourceSegmentID"/> to <paramref name="targetSegmentID"/>
        /// This will join target segment to the same road as source segment.
        /// </summary>
        public static void CopySegmentName(ushort sourceSegmentID, ushort targetSegmentID) {
            SimulationManager.instance.AddAction(() => {
                string sourceName = NetManager.instance.GetSegmentName(sourceSegmentID);
                string targetName = NetManager.instance.GetSegmentName(targetSegmentID);
                if (sourceName != targetName) {
                    NetManager.instance.SetSegmentName(targetSegmentID, sourceName);
                }
            });
        }

        /// <summary>
        /// Joins all segments from <paramref name="segmentIDs"/> to the road
        /// containing <paramref name="segmentId"/>
        /// </summary>
        public static void SetRoad(ushort segmentId, IEnumerable<ushort> segmentIDs) {
            foreach(var targetSegmentID in segmentIDs) {
                if(targetSegmentID != segmentId && targetSegmentID != 0) {
                    CopySegmentName(segmentId, targetSegmentID);
                }
            }
        }

        internal const int NETADJUST_INFO_INDEX = 2;
        internal static bool IsNetAdjustMode() =>
            IsNetAdjustMode(InfoManager.instance.CurrentMode, (int)InfoManager.instance.CurrentSubMode);

        internal static bool IsNetAdjustMode(InfoManager.InfoMode mode, int subModeIndex) =>
            (mode == InfoManager.InfoMode.TrafficRoutes) &
            (subModeIndex == NETADJUST_INFO_INDEX);

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
                        ret.Add(path.m_buffer[i]);
                    }
                    return ret;
                }
                return null;
            }
        }

        private NetAdjust netAdjust = NetManager.instance.NetAdjust ??
            throw new Exception("netAdjust not found!");

        private FieldInfo m_tempPath_field =
            typeof(NetAdjust).GetField("m_tempPath", BindingFlags.Instance | BindingFlags.NonPublic);

        private FastList<ushort> GetPath() =>
            (FastList<ushort>)m_tempPath_field.GetValue(netAdjust);

        private MethodInfo mCalculatePath = typeof(NetAdjust).GetMethod(
            "CalculatePath",
            BindingFlags.Instance | BindingFlags.NonPublic,
            Type.DefaultBinder,
            new Type[] { typeof(ushort), typeof(int) },
            null) ??
            throw new Exception("mCalculatePath not found!");

        private void CalculatePath(ushort segmentID, int modifyIndex) {
            mCalculatePath.Invoke(netAdjust, new object[] { segmentID, modifyIndex });
        }

        public delegate void Handler();

        /// <summary>
        /// Invoked every time road selection changes.
        /// </summary>
        public event Handler OnChanged;

        public class Threading : ThreadingExtensionBase {
            private int prev_length = -2;
            private ushort prev_segmentID = 0;
            private string  prev_name = string.Empty;

            void UpdatePath() {
                ushort selectedSegmentID = Singleton<InstanceManager>.instance.GetSelectedInstance().NetSegment;
                if (selectedSegmentID == 0) {
                    return;
                }
                string name = NetManager.instance.GetSegmentName(selectedSegmentID);
                if(prev_name != name) {
                    Log._Debug($"name={name} prev_name={prev_name}");
                    prev_name = name;
                    RoadSelectionUtil.Instance.CalculatePath(selectedSegmentID, 0);
                }
            }

            public override void OnUpdate(float realTimeDelta, float simulationTimeDelta) {
                try {
                    if (RoadSelectionUtil.Instance == null) {
                        return;
                    }
                    if (!RoadSelectionUtil.Instance.netAdjust.PathVisible) {
                        // RoadWorldInfoPanel does not update path. therefore we do it manually.
                        UpdatePath();
                    }
                    // Performance critical part of the code:
                    var path = RoadSelectionUtil.Instance.GetPath();
                    int len = path?.m_size ?? -1;
                    ushort segmentID = len > 0 ? path.m_buffer[0] : (ushort)0;

                    // Assumptions:
                    //  A- two different paths cannot share a segment.
                    //  B- UI does not allow to move both ends of the selection simultaneously.
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
                        RoadSelectionUtil.Instance.OnChanged?.Invoke();
                    }
                    prev_segmentID = segmentID;
                } catch(Exception e) {
                    Log.Error(e.Message);
                }
            }
        }
    } // end class
} // end namesapce
