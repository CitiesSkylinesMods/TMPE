namespace TrafficManager.Lifecycle {
    using CSUtil.Commons;
    using ICities;
    using System.Collections.Generic;
    using TrafficManager.State;
    using TrafficManager.State.Asset;
    using static TrafficManager.Util.Shortcuts;

    public class AssetDataExtension : IAssetDataExtension {
        public const string TMPE_RECORD_ID = "TMPE_Records";

        public void OnCreated(IAssetData assetData) { }

        public void OnReleased() { }

        public void OnAssetLoaded(string name, object asset, Dictionary<string, byte[]> userData) =>
            OnAssetLoadedImpl(name, asset, userData);

        // asset should be the same as ToolsModifierControl.toolController.m_editPrefabInfo
        public void OnAssetSaved(string name, object asset, out Dictionary<string, byte[]> userData) =>
            OnAssetSavedImpl(name, asset, out userData);

        public static void OnAssetLoadedImpl(string name, object asset, Dictionary<string, byte[]> userData) {
            Log.Info($"AssetDataExtension.OnAssetLoadedImpl({name}, {asset}, userData) called");
            if (asset is BuildingInfo prefab) {
                Log._Debug("AssetDataExtension.OnAssetLoadedImpl():  prefab is " + prefab);
                if (userData.TryGetValue(TMPE_RECORD_ID, out byte[] data)) {
                    Log.Info("AssetDataExtension.OnAssetLoadedImpl(): extracted data for " + TMPE_RECORD_ID);
                    var assetData = SerializationUtil.Deserialize(data) as AssetData;
                    AssertNotNull(assetData, "assetData");
                    TMPELifecycle.Instance.Asset2Data[prefab] = assetData;
                    Log._Debug("AssetDataExtension.OnAssetLoadedImpl(): Asset Data=" + assetData);
                }
            }
        }

        public void OnAssetSavedImpl(string name, object asset, out Dictionary<string, byte[]> userData) {
            Log.Info($"AssetDataExtension.OnAssetSavedImpl({name}, {asset}, userData) called");
            userData = null;
            if (asset is BuildingInfo prefab) {
                Log.Info("AssetDataExtension.OnAssetSavedImpl():  prefab is " + prefab);
                var assetData = AssetData.GetAssetData(prefab);
                Log._Debug("AssetDataExtension.OnAssetSavedImpl(): assetData=" + assetData);
                userData = new Dictionary<string, byte[]>();
                userData.Add(TMPE_RECORD_ID, SerializationUtil.Serialize(assetData));
            }
        }
    }
}
