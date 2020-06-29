namespace TrafficManager.Util {
    using System.Collections.Generic;
    using System.Linq;
    using HarmonyLib;
    using CSUtil.Commons;
    using TrafficManager.State.Asset;
    using TrafficManager.Util;

    public static class PlaceIntersectionUtil {
        /// <summary>
        /// Index == -1 : not mappin
        /// Index >= 0  : mapping segment created based on BuildingInfo.m_paths[Index].
        /// </summary>
        public static int Index = -1;

        public static BuildingInfo IntersectionInfo; 

        public static SegmentNetworkIDs[] PathNetowrkIDs;

        public static Dictionary<InstanceID, InstanceID> Map;

        public static bool Mapping => Index != -1;

        public delegate void Handler(Dictionary<InstanceID, InstanceID> map);

        /// <summary>
        /// invoked when networkIDs are mapped and provides the user with dictionary of oldNetworkIds->newNetworkIds
        /// </summary>
        public static event Handler OnNetowrksMapped;

        /// <summary>
        /// start mapping for <paramref name="intersectionInfo"/>
        /// </summary>
        public static void StartMapping(BuildingInfo intersectionInfo) {
            Log._Debug($"PlaceIntersectionUtil.Start({intersectionInfo?.ToString() ?? "null"})");

            if (!Shortcuts.InSimulationThread()) {
                Log.Error("must be called from simulation thread");
                return;
            }
            if (intersectionInfo == null) {
                Log.Error("intersectionInfo is null");
                return;
            }

            Index = 0;
            Map = new Dictionary<InstanceID, InstanceID>();
            IntersectionInfo = intersectionInfo;

            var Asset2Data = AssetDataExtension.Instance.Asset2Data;
            Log._Debug("LoadPathsPatch.Prefix(): Asset2Data.keys=" +
                Asset2Data.Select(item => item.Key).ToSTR());
            Asset2Data.TryGetValue(intersectionInfo, out var assetData);
            PathNetowrkIDs = assetData?.PathNetworkIDs;

            if (PathNetowrkIDs == null) Index = -1;

            Log._Debug($"LoadPathsPatch.Prefix(): IntersectionInfo={IntersectionInfo} PathNetowrkIDs={PathNetowrkIDs.ToSTR()} Mapping={Mapping}" +
                $"PathNetowrkIDs.Length:{PathNetowrkIDs?.Length}, IntersectionInfo.m_paths.Length:{IntersectionInfo.m_paths.Length}");
        }

        public static void ApplyTrafficRules(BuildingInfo intersectionInfo) {
            Log._Debug($"PlaceIntersectionUtil.ApplyTrafficRules({intersectionInfo?.ToString() ?? "null"}): Mapping={Mapping} ");
            if (!Shortcuts.InSimulationThread()) {
                Log.Error("must be called from simulation thread");
                return;
            }
            if (intersectionInfo == null) {
                Log.Error("intersectionInfo is null");
                return;
            }
            if (!Mapping) {
                Log.Error("PlaceIntersectionUtil is not initialized");
                return;
            }

            Index = -1;

            foreach (var item in Map)
                CalculateNetwork(item.Value);

            var Asset2Data = AssetDataExtension.Instance.Asset2Data;
            if (Asset2Data.TryGetValue(intersectionInfo, out var assetData)) {
                Log.Info("LoadPathsPatch.Postfix(): assetData =" + assetData);
                assetData.Record.Transfer(Map);
            } else {
                Log.Info("LoadPathsPatch.Postfix(): assetData not found (the asset does not have TMPE data)");
            }
        }

        /// <summary>
        /// early calculate networksso that we are able to set traffic rules without delay.
        /// </summary>
        public static void CalculateNetwork(InstanceID instanceId) {
            switch (instanceId.Type) {
                case InstanceType.NetNode:
                    ushort nodeId = instanceId.NetNode;
                    nodeId.ToNode().CalculateNode(nodeId);
                    break;
                case InstanceType.NetSegment:
                    ushort segmentId = instanceId.NetSegment;
                    segmentId.ToSegment().CalculateSegment(segmentId);
                    break;
            }
        }

    }
}
