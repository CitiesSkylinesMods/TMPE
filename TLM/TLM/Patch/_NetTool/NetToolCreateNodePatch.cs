namespace TrafficManager.Patches._NetTool {
    using CSUtil.Commons;
    using HarmonyLib;
    using System;
    using System.Reflection;
    using TrafficManager.Util;

    [HarmonyPatch]
    public class CreateNodePatch {
        //public static ToolBase.ToolErrors CreateNode(NetInfo info, NetTool.ControlPoint startPoint, NetTool.ControlPoint middlePoint, NetTool.ControlPoint endPoint, FastList<NetTool.NodePosition> nodeBuffer, int maxSegments, bool test, bool testEnds, bool visualize, bool autoFix, bool needMoney, bool invert, bool switchDir, ushort relocateBuildingID, out ushort firstNode, out ushort lastNode, out ushort segment, out int cost, out int productionRate)
        delegate ToolBase.ToolErrors TargetDelegate(NetInfo info, NetTool.ControlPoint startPoint, NetTool.ControlPoint middlePoint, NetTool.ControlPoint endPoint, FastList<NetTool.NodePosition> nodeBuffer,
            int maxSegments, bool test, bool testEnds, bool visualize, bool autoFix, bool needMoney, bool invert, bool switchDir, ushort relocateBuildingID,
            out ushort firstNode, out ushort lastNode, out ushort segment, out int cost, out int productionRate);
        public static MethodBase TargetMethod() =>
            TranspilerUtil.DeclaredMethod<TargetDelegate>(typeof(NetTool), nameof(NetTool.CreateNode));

        /// <summary>
        /// maps old ids to new ids.
        /// when mapping, this is called once per segment created based on BuildingInfo.m_paths[index].
        /// Also called by the other overload of CreateNode in which case it does not create a segment.
        /// </summary>
        /// <param name="segment">new segment</param>
        public static void Postfix(ref ushort segment, bool test, bool visualize) {
            if (test || visualize || !PlaceIntersectionUtil.Mapping)
                return;
            if (segment == 0) {
                // this code path is reached when called by the other overload of NetToolCreateNode()
                // we should ignore that.
                return;
            }

            // at this point we are sure that the caller is LoadPaths.
            Log._Debug($"CreateNodePatch.Postfix(segment={segment}");
            bool verbose = false;
            if (verbose) {
                Log._Debug($"CreateNodePatch.Postfix(): index={PlaceIntersectionUtil.Index} " +
                    $"PathNetowrkIDs={PlaceIntersectionUtil.PathNetowrkIDs.ToSTR()}");
                Log._Debug("stacktrace: " + Environment.StackTrace);
            }

            PlaceIntersectionUtil.PathNetowrkIDs[PlaceIntersectionUtil.Index++].
                MapInstanceIDs(newSegmentId: segment, map: PlaceIntersectionUtil.Map);
        }
    }
}
