using CSUtil.Commons;
using ICities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using TrafficManager.Manager.Impl;
using TrafficManager.State;
using UnityEngine;

namespace TrafficManager.Custom.AssetManager
{
    public struct TmpeAssetData
    {
        public List<IdentificationTool.AssetSegment> Segments;

        // We keep the data serialized, so we can create copy of them every time the asset is used
        public byte[] TmpeConfiguration;
    }

    public class AssetDataExtension : IAssetDataExtension
    {
        private const string segmentsString = "_segments";
        private const string dataString = "_data";

        /* Called when the savegame/asset is loading. Does not work when Loading Screen mod is enabled due to a bug */
        public void OnAssetLoaded(string name, object asset, Dictionary<string, byte[]> userData)
        {
            BuildingInfo info = asset as BuildingInfo;
            if (info != null)
            {
                try
                {
                    TmpeAssetData data = new TmpeAssetData();
                    data.TmpeConfiguration = userData[SerializableDataExtension.DataId + dataString];
                    //data.TmpeConfiguration = SerializableDataExtension.DeserializeData(userData[SerializableDataExtension.DataId + dataString], out bool error);
                    data.Segments = Deserialize<List<IdentificationTool.AssetSegment>>(userData[SerializableDataExtension.DataId + segmentsString]);

                    AssetDataManager.Instance.AssetsWithData[IdentificationTool.GetNameWithoutPrefix(info.name)] = data;
#if DEBUG
                    Debug.Log("Successfully deserialized TMPE asset data (" + info.name + ")");
#endif

                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
            }
        }

        public static T Deserialize<T>(byte[] data)
            where T : class
        {
            try
            {
                if (data != null && data.Length != 0)
                {
                    Log.Info($"Loading Data from New Load Routine! Length={data.Length}");
                    var memoryStream = new MemoryStream();
                    memoryStream.Write(data, 0, data.Length);
                    memoryStream.Position = 0;

                    var binaryFormatter = new BinaryFormatter();
                    return (T)binaryFormatter.Deserialize(memoryStream);
                }
                else
                {
                    Log.Info("No data to deserialize!");

                }
            }
            catch (Exception e)
            {
                Log.Error($"Error deserializing data: {e.ToString()}");
                Log.Info(e.StackTrace);
                throw;
            }
            return null;
        }

        /* Called when an asset is saved */
        public void OnAssetSaved(string name, object asset, out Dictionary<string, byte[]> userData)
        {
            Debug.Log("TMPE: OnAssetSaved");

            userData = null;

            if (!LoadingExtension.IsInAssetEditor)
                return;

            BuildingInfo info = asset as BuildingInfo;
            if (info == null)
                return;

            bool success = true;

            TmpeAssetData data = new TmpeAssetData();
            data.Segments = BuildingDecorationPatch.m_pendingSegments;
            Configuration config = SerializableDataExtension.CreateConfiguration(ref success);

            userData = new Dictionary<string, byte[]>();

            // ??? !!!
            SerializableDataExtension.OnBeforeGameSaved(ref success);
            data.TmpeConfiguration = SerializableDataExtension.Serialize(config, ref success);
            SerializableDataExtension.OnAfterGameSaved(ref success);

            if (!success)
                return;

            if (BuildingDecorationPatch.m_pendingSegments?.Count > 0)
            {

                userData.Add(SerializableDataExtension.DataId + segmentsString, SerializableDataExtension.Serialize<List<IdentificationTool.AssetSegment>>(data.Segments, ref success));
                userData.Add(SerializableDataExtension.DataId + dataString, data.TmpeConfiguration);
                AssetDataManager.Instance.AssetsWithData[IdentificationTool.GetNameWithoutPrefix(info.name)] = data;
            }
        }

        public void OnCreated(IAssetData assetData)
        {

        }

        public void OnReleased()
        {

        }
    }
}
