namespace TrafficManager.Util {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using UnityEngine;

    internal static class Shortcuts {
        /// <summary>
        /// returns a new calling Clone() on all items.
        /// </summary>
        /// <typeparam name="T">item time must be IClonable</typeparam>
        internal static IList<T> Clone<T>(this IList<T> listToClone) where T : ICloneable =>
            listToClone.Select(item => (T)item.Clone()).ToList();

        internal static void Swap<T>(ref T a, ref T b) {
            T temp = a;
            a = b;
            b = temp;
        }

        internal static void Swap<T>(this T[] array, int index1, int index2) {
            T temp = array[index1];
            array[index1] = array[index2];
            array[index2] = temp;
        }

        internal static void Swap<T>(this List<T> list, int index1, int index2) {
            T temp = list[index1];
            list[index1] = list[index2];
            list[index2] = temp;
        }

        private static NetNode[] _nodeBuffer => Singleton<NetManager>.instance.m_nodes.m_buffer;

        private static NetSegment[] _segBuffer => Singleton<NetManager>.instance.m_segments.m_buffer;

        public static NetLane[] laneBuffer => NetManager.instance.m_lanes.m_buffer;

        private static ExtSegmentEnd[] _segEndBuff => segEndMan.ExtSegmentEnds;

        internal static IExtSegmentEndManager segEndMan => Constants.ManagerFactory.ExtSegmentEndManager;

        internal static IExtSegmentManager segMan => Constants.ManagerFactory.ExtSegmentManager;

        internal static INetService netService => Constants.ServiceFactory.NetService;

        internal static ref NetNode GetNode(ushort nodeId) => ref _nodeBuffer[nodeId];

        internal static ref NetNode ToNode(this ushort nodeId) => ref GetNode(nodeId);

        public static ref NetLane ToLane(this uint laneId) => ref laneBuffer[laneId];

        internal static ref NetSegment GetSeg(ushort segmentId) => ref _segBuffer[segmentId];

        internal static ref NetSegment ToSegment(this ushort segmentId) => ref GetSeg(segmentId);

        internal static ref ExtSegmentEnd GetSegEnd(ushort segmentId, ushort nodeId) =>
            ref _segEndBuff[segEndMan.GetIndex(segmentId, nodeId)];

        internal static ref ExtSegmentEnd GetSegEnd(ushort segmentId, bool startNode) =>
            ref _segEndBuff[segEndMan.GetIndex(segmentId, startNode)];

        internal static bool HasJunctionFlag(ushort nodeId) => HasJunctionFlag(ref GetNode(nodeId));

        internal static bool HasJunctionFlag(ref NetNode node) =>
            (node.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;

        internal static Func<bool, int> Int = (bool b) => b ? 1 : 0;

        public static void SetBit(this ref byte b, int idx) => b |= (byte)(1 << idx);
        public static void ClearBit(this ref byte b, int idx) => b &= ((byte)~(1 << idx));
        public static bool GetBit(this byte b, int idx) => (b & (byte)(1 << idx)) != 0;
        public static void SetBit(this ref byte b, int idx, bool value) {
            if (value)
                b.SetBit(idx);
            else
                b.ClearBit(idx);
        }

        /// <summary>
        /// useful for easily debugin inline functions
        /// to be used like this example:
        /// TYPE inlinefunctionname(...) => expression
        /// TYPE inlinefunctionname(...) => expression.LogRet("messege");
        /// </summary>
        internal static T LogRet<T>(this T a, string m) {
            Log._Debug(m + a);
            return a;
        }

        internal static string CenterString(this string stringToCenter, int totalLength) {
            int leftPadding = ((totalLength - stringToCenter.Length) / 2) + stringToCenter.Length;
            return stringToCenter.PadLeft(leftPadding).PadRight(totalLength);
        }

        internal static string ToSTR<T>(this IEnumerable<T> segmentList) {
            string ret = "{ ";
            foreach (T segmentId in segmentList) {
                ret += $"{segmentId}, ";
            }
            ret.Remove(ret.Length - 2, 2);
            ret += " }";
            return ret;
        }

        internal static string ToSTR(this List<LanePos> laneList) =>
            (from lanePos in laneList select lanePos.laneId).ToSTR();

        internal static void AssertEq<T>(T a, T b, string m = "") where T:IComparable {
            if (a.CompareTo(b) != 0) {
                Log.Error($"Assertion failed. Expected {a} == {b} | " + m);
            }
        }

        internal static void AssertNEq<T>(T a, T b, string m = "") where T : IComparable {
            if (a.CompareTo(b) == 0) {
                Log.Error($"Assertion failed. Expected {a} != {b} | " + m);
            }
        }

        internal static void AssertNotNull<T>(T a, string varName) {
#if DEBUG
            if (a==null) {
                Log.Error($"Assertion failed. Expected {varName} != null");
            }
#endif
        }

        internal static void Assert(bool con, string m = "") {
#if DEBUG
            if(!con) {
                Log.Error("Assertion failed: " + m);
            }
#endif 
    }

        internal static string StackTrace => System.Environment.StackTrace;

        internal static bool ShiftIsPressed => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        internal static bool ControlIsPressed => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        internal static bool AltIsPressed => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        internal static bool InSimulationThread() =>
            System.Threading.Thread.CurrentThread == SimulationManager.instance.m_simulationThread;

        #region directions
        internal static bool LHT => Constants.ServiceFactory.SimulationService.TrafficDrivesOnLeft;
        internal static bool RHT => !LHT;

        internal static ushort GetNearSegment(this ref NetSegment segment, ushort nodeId) =>
            RHT ? segment.GetRightSegment(nodeId) : segment.GetLeftSegment(nodeId);

        internal static ushort GetFarSegment(this ref NetSegment segment, ushort nodeId) =>
            LHT ? segment.GetRightSegment(nodeId) : segment.GetLeftSegment(nodeId);

        #endregion
    }
}
