namespace TrafficManager.Util {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using UnityEngine;

    //TODO should I rename this to Extensions or Helpers?
    internal static class Shortcuts {
        /// <summary>
        /// returns a new calling Clone() on all items.
        /// </summary>
        /// <typeparam name="T">item time must be IClonable</typeparam>
        internal static IList<T> Clone<T>(this IList<T> listToClone) where T : ICloneable =>
            listToClone.Select(item => (T)item.Clone()).ToList();

        private static NetNode[] _nodeBuffer => Singleton<NetManager>.instance.m_nodes.m_buffer;

        private static NetSegment[] _segBuffer => Singleton<NetManager>.instance.m_segments.m_buffer;

        private static ExtSegmentEnd[] _segEndBuff => segEndMan.ExtSegmentEnds;

        internal static IExtSegmentEndManager segEndMan => Constants.ManagerFactory.ExtSegmentEndManager;

        internal static IExtSegmentManager segMan => Constants.ManagerFactory.ExtSegmentManager;

        internal static INetService netService => Constants.ServiceFactory.NetService;

        internal static ref NetNode GetNode(ushort nodeId) => ref _nodeBuffer[nodeId];

        internal static ref NetNode ToNode(this ushort nodeId) => ref GetNode(nodeId);

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


        internal static bool ShiftIsPressed => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        internal static bool ControlIsPressed => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        internal static bool AltIsPressed => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        #region directions
        internal static bool lht => LaneArrowManager.Instance.Services.SimulationService.TrafficDrivesOnLeft;
        internal static bool rht => !lht;

        internal static LaneArrows LaneArrows_Near => rht ? LaneArrows.Right : LaneArrows.Left;
        internal static LaneArrows LaneArrows_Far  => rht ? LaneArrows.Left  : LaneArrows.Right;
        internal static LaneArrows LaneArrows_NearForward => LaneArrows_Near | LaneArrows.Forward;
        internal static LaneArrows LaneArrows_FarForward  => LaneArrows_Far  | LaneArrows.Forward;

        internal static ArrowDirection ArrowDirection_Near => rht ? ArrowDirection.Right : ArrowDirection.Left;
        internal static ArrowDirection ArrowDirection_Far  => rht ? ArrowDirection.Left  : ArrowDirection.Right;
        #endregion
    }
}
