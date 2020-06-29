namespace TrafficManager.State.Asset {
    using CSUtil.Commons;
    using ICities;
    using System.Collections.Generic;
    using static Util.Shortcuts;

    public class AssetDataExtension : AssetDataExtensionBase {
        public const string TMPE_RECORD_ID = "TMPE_Records";

        public static AssetDataExtension Instance;
        public Dictionary<BuildingInfo, AssetData> Asset2Data = new Dictionary<BuildingInfo, AssetData>();

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
                if (userData.TryGetValue(TMPE_RECORD_ID, out byte[] data)) {
                    Log.Info("AssetDataExtension.OnAssetLoaded(): extracted data for " + TMPE_RECORD_ID);
                    var assetData = SerializationUtil.Deserialize(data) as AssetData;
                    AssertNotNull(assetData, "assetData");
                    Asset2Data[prefab] = assetData;
                    Log._Debug("AssetDataExtension.OnAssetLoaded(): Asset Data=" + assetData);
                }
            }
        }

        public override void OnAssetSaved(string name, object asset, out Dictionary<string, byte[]> userData) {
            Log.Info($"AssetDataExtension.OnAssetSaved({name}, {asset}, userData) called");
            userData = null;
            //var info = ToolsModifierControl.toolController.m_editPrefabInfo;
            if (asset is BuildingInfo prefab) {
                Log.Info("AssetDataExtension.OnAssetSaved():  prefab is " + prefab);
                var assetData = AssetData.GetAssetData();
                Log._Debug("AssetDataExtension.OnAssetSaved(): assetData=" + assetData);
                userData.Add(TMPE_RECORD_ID, SerializationUtil.Serialize(assetData));
            }
        }
    }
}
