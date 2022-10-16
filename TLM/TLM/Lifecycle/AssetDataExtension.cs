namespace TrafficManager.Lifecycle {
    using CSUtil.Commons;
    using ICities;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
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
            Log._Debug($"AssetDataExtension.OnAssetLoadedImpl({name}, {asset}, userData) called");
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

        public static void OnAssetSavedImpl(string name, object asset, out Dictionary<string, byte[]> userData) {
            Log.Info($"AssetDataExtension.OnAssetSavedImpl({name}, {asset}, userData) called");
            userData = null;
            if (asset is BuildingInfo prefab) {
                Log.Info("AssetDataExtension.OnAssetSavedImpl():  prefab is " + prefab);
                var assetData = AssetData.GetAssetData(prefab);
                if (assetData == null) {
                    Log._Debug("AssetDataExtension.OnAssetSavedImpl(): Nothing to record.");
                } else {
                    Log._Debug("AssetDataExtension.OnAssetSavedImpl(): assetData=" + assetData);
                    userData = new Dictionary<string, byte[]>();
                    userData.Add(TMPE_RECORD_ID, SerializationUtil.Serialize(assetData));
                }
            }
        }

        [Conditional("DEBUG")]
        public static void HotReload() {
            var assets2UserData = Type.GetType("LoadOrderMod.LOMAssetDataExtension, LoadOrderMod", throwOnError: false)
                ?.GetField("Assets2UserData")
                ?.GetValue(null)
                as Dictionary<PrefabInfo, Dictionary<string, byte[]>>;

            if (assets2UserData == null) {
                Log.Warning("Could not hot reload assets because LoadOrderMod was not found");
                return;
            }

            var editPrefabInfo = ToolsModifierControl.toolController.m_editPrefabInfo;
            foreach (var asset2UserData in assets2UserData) {
                var asset = asset2UserData.Key;
                var userData = asset2UserData.Value;
                if (asset) {
                    if (editPrefabInfo) {
                        // asset editor work around
                        asset = FindLoadedCounterPart<NetInfo>(asset);
                    }
                    OnAssetLoadedImpl(asset.name, asset, userData);
                }
            }
        }

        /// <summary>
        /// OnLoad() calls IntializePrefab() which can create duplicates. Therefore we should match by name.
        /// </summary>
        private static PrefabInfo FindLoadedCounterPart<T>(PrefabInfo source)
            where T : PrefabInfo {
            int n = PrefabCollection<T>.LoadedCount();
            for (uint i = 0; i < n; ++i) {
                T prefab = PrefabCollection<T>.GetLoaded(i);
                if (prefab?.name == source.name) {
                    return prefab;
                }
            }
            return source;
        }
    }
}
