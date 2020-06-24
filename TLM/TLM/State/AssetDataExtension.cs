namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using HarmonyLib;
    using ICities;
    using System.Collections.Generic;
    using TrafficManager.Util.Record;
    using static Util.Shortcuts;

    // Credits to boformer
    [HarmonyPatch(typeof(LoadAssetPanel), "OnLoad")]
    public static class OnLoadPatch {
        /// <summary>
        /// when loading asset from a file, IAssetData.OnAssetLoaded() is called for all assets but the one that is loaded from the file.
        /// this postfix calls IAssetData.OnAssetLoaded() for asset loaded from file.
        /// </summary>
        public static void Postfix(LoadAssetPanel __instance, UIListBox ___m_SaveList) {
            // Taken from LoadAssetPanel.OnLoad
            var selectedIndex = ___m_SaveList.selectedIndex;
            var getListingMetaDataMethod = typeof(LoadSavePanelBase<CustomAssetMetaData>).GetMethod(
                "GetListingMetaData", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var listingMetaData = (CustomAssetMetaData)getListingMetaDataMethod.Invoke(__instance, new object[] { selectedIndex });

            // Taken from LoadingManager.LoadCustomContent
            if (listingMetaData.userDataRef != null) {
                AssetDataWrapper.UserAssetData userAssetData = listingMetaData.userDataRef.Instantiate() as AssetDataWrapper.UserAssetData;
                if (userAssetData == null) {
                    userAssetData = new AssetDataWrapper.UserAssetData();
                }
                AssetDataExtension.Instance.OnAssetLoaded(listingMetaData.name, ToolsModifierControl.toolController.m_editPrefabInfo, userAssetData.Data);
            }
        }
    }

    public class AssetDataExtension : AssetDataExtensionBase {
        public const string TMPE_RECORD_ID = "TMPE_Records";
        static Building[] buildingBuffer = BuildingManager.instance.m_buildings.m_buffer;

        public static AssetDataExtension Instance;
        public Dictionary<BuildingInfo, IRecordable> Asset2Record = new Dictionary<BuildingInfo, IRecordable>();

        public override void OnCreated(IAssetData assetData) {
            base.OnCreated(assetData);
            Instance = this;
        }

        public override void OnReleased() {
            Instance = null;
        }

        public override void OnAssetLoaded(string name, object asset, Dictionary<string, byte[]> userData) {
            Log.Info($"AssetDataExtension.OnAssetLoaded({name}, {asset}, userData) called");
            if (asset is BuildingInfo prefab) {
                Log._Debug("AssetDataExtension.OnAssetLoaded():  prefab is " + prefab);
                if (userData.TryGetValue(TMPE_RECORD_ID, out byte[] recordData)) {
                    Log._Debug("AssetDataExtension.OnAssetLoaded():  extracted data for " + TMPE_RECORD_ID);
                    var record = RecordUtil.Deserialize(recordData) as IRecordable;
                    AssertNotNull(record, "nodedatas");
                    Asset2Record[prefab] = record;
                    Log._Debug("AssetDataExtension.OnAssetLoaded() record=" + record);
                }
            }
        }

        public override void OnAssetSaved(string name, object asset, out Dictionary<string, byte[]> userData) {
            Log.Info($"AssetDataExtension.OnAssetSaved({name}, {asset}, userData) called");
            userData = null;
            //var info = ToolsModifierControl.toolController.m_editPrefabInfo;
            if (asset is BuildingInfo prefab) {
                Log._Debug("AssetDataExtension.OnAssetSaved():  prefab is " + prefab);
                IRecordable record = RecordAll();
                Log._Debug("AssetDataExtension.OnAssetSaved(): record=" + record);
                userData.Add(TMPE_RECORD_ID, RecordUtil.Serialize(record));
            }
        }

        public static IRecordable RecordAll() {
            NetSegment[] segmentBuffer = NetManager.instance.m_segments.m_buffer;
            TrafficRulesRecord record = new TrafficRulesRecord();
            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                bool valid = netService.IsSegmentValid(segmentId) && segmentBuffer[segmentId].Info;
                if (!valid)
                    continue;
                record.AddCompleteSegment(segmentId);
            }
            record.Record();
            return record;
        }
    }
}
