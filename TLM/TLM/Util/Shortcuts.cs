namespace TrafficManager.Util {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using static Constants;

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

        internal static IExtSegmentEndManager segEndMan => ManagerFactory.ExtSegmentEndManager;

        internal static IExtSegmentManager segMan => ManagerFactory.ExtSegmentManager;

        internal static INetService netService => ServiceFactory.NetService;

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
    }
}
