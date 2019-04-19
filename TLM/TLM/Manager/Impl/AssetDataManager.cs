using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AssetManager;
using TrafficManager.State;

namespace TrafficManager.Manager.Impl
{
    /* Pairs assets with their TMPE settings */
    public class AssetDataManager : AbstractCustomManager
    {
        public static AssetDataManager Instance { get; private set; } = new AssetDataManager();

        public Dictionary<string, TmpeAssetData> AssetsWithData { get; private set; } = new Dictionary<string, TmpeAssetData>();

        public override void OnLevelUnloading()
        {
            base.OnLevelUnloading();
            AssetsWithData = new Dictionary<string, TmpeAssetData>();
        }
    }
}
