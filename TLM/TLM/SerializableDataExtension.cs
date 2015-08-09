using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using ColossalFramework;
using ColossalFramework.HTTP;
using ICities;
using NLog;
using TrafficManager.CustomAI;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;
using Object = System.Object;
using Random = UnityEngine.Random;
using Timer = System.Timers.Timer;

namespace TrafficManager
{
    public class SerializableDataExtension : SerializableDataExtensionBase
    {
        public const string LegacyDataId = "TrafficManager_v0.9";
        public const string DataId = "TrafficManager_v1.0";
        public static uint UniqueId;

        public static ISerializableData SerializableData;
        private static Configuration _configuration;
        public static bool ConfigLoaded = false;
        public static bool StateLoaded = false;

        public override void OnCreated(ISerializableData serializableData)
        {
            UniqueId = 0u;
            SerializableData = serializableData;
        }

        public override void OnReleased()
        {
        }

        [Obsolete("Part of the old save system. Will be removed eventually.")]
        private static void GenerateUniqueId()
        {
            UniqueId = (uint)Random.Range(1000000f, 2000000f);

            while (File.Exists(Path.Combine(Application.dataPath, "trafficManagerSave_" + UniqueId + ".xml")))
            {
                UniqueId = (uint)Random.Range(1000000f, 2000000f);
            }
        }

        public override void OnLoadData()
        {
            Log.Message("Loading Mod Data");
            var keys = SerializableData.EnumerateData().Where(k => k.StartsWith("TrafficManager"));
            byte[] data = null;
            foreach (var key in keys)
            {
                Log.Message($"Checking for save data at key: {key}");
                data = SerializableData.LoadData(key);
                if (data != null && data.Length > 0)
                {
                    Log.Message($"Save Data Found. Deserializing.");
                    break;
                }
            }
            if (data == null)
            {
                Log.Message($"No Save Data Found. Possibly a new game?");
                return;
            }
            DeserializeData(data);
        }

        private string LoadLegacyData(byte[] data)
        {
            UniqueId = 0u;

            for (var i = 0; i < data.Length - 3; i++)
            {
                UniqueId = BitConverter.ToUInt32(data, i);
            }

            Log.Message($"Looking for legacy TrafficManagerSave file trafficManagerSave_{UniqueId}.xml");
            var filepath = Path.Combine(Application.dataPath, "trafficManagerSave_" + UniqueId + ".xml");

            if (File.Exists(filepath))
                return filepath;

            Log.Message("Legacy Save Data doesn't exist. Expected: " + filepath);
            throw new FileNotFoundException("Legacy data not present.");
        }
        
        private void DeserializeData(byte[] data)
        {
            string legacyFilepath = null;
            try
            {
                legacyFilepath = LoadLegacyData(data);
            }
            catch (Exception)
            {
                // data isn't legacy compatible. Probably new format or missing data.
            }
            
            if (legacyFilepath != null)
            {
                Log.Message("Converting Legacy Config Data.");
                _configuration = Configuration.LoadConfigurationFromFile(legacyFilepath);
            }
            else
            {
                if (data.Length == 0)
                {
                    Log.Message("Legacy data was empty. Checking for new Save data.");
                    data = SerializableData.LoadData(DataId);
                }

                try
                {
                    if (data.Length != 0)
                    {
                        Log.Message("Loading Data from New Load Routine!");
                        var memoryStream = new MemoryStream();
                        memoryStream.Write(data, 0, data.Length);
                        memoryStream.Position = 0;

                        var binaryFormatter = new BinaryFormatter();
                        _configuration = (Configuration)binaryFormatter.Deserialize(memoryStream);
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Error deserializing data: {e.Message}");
                }
            }
            ConfigLoaded = true;

            LoadDataState();
            StateLoaded = true;

            //Log.Message("Setting timer to load data.");
            //var timer = new Timer(1500);
            //timer.Elapsed += (sender, args) =>
            //{
            //    if (!ConfigLoaded || StateLoaded) return;
            //    Log.Message("Loading State Data from Save.");
            //    var t = new Thread(LoadDataState);
            //    t.Start();
            //    //LoadDataState();
            //    StateLoaded = true;
            //};
            //timer.Start();
        }

        public static void LoadDataState()
        {
            Log.Message("Loading State from Config");
            if (_configuration == null)
            {
                Log.Message("Configuration Null. Couldn't load save data?");
                return;
            }

            foreach (var segment in _configuration.PrioritySegments)
            {
                if(segment.Length < 3)
                    continue;
                if (TrafficPriority.IsPrioritySegment((ushort) segment[0], segment[1]))
                    continue;

                Log.Message($"Adding Priority Segment of type: {segment[2].ToString()}");
                TrafficPriority.AddPrioritySegment((ushort) segment[0], segment[1], (PrioritySegment.PriorityType) segment[2]);
            }

            foreach (var node in _configuration.NodeDictionary)
            {
                if (node.Length < 4)
                    continue;
                if (CustomRoadAI.GetNodeSimulation((ushort) node[0]) != null)
                    continue;

                Log.Message($"Adding Node do Simulation {node[0]}");
                try
                {
                    CustomRoadAI.AddNodeToSimulation((ushort) node[0]);
                    var nodeDict = CustomRoadAI.GetNodeSimulation((ushort) node[0]);

                    nodeDict.ManualTrafficLights = Convert.ToBoolean(node[1]);
                    nodeDict.TimedTrafficLights = Convert.ToBoolean(node[2]);
                    nodeDict.TimedTrafficLightsActive = Convert.ToBoolean(node[3]);
                }
                catch (Exception e)
                {
                    // if we failed, just means it's old corrupt data. Ignore it and continue.
                    Log.Warning("Error loading data from the NodeDictionary: " + e.Message);
                }
            }

            foreach (var segmentData in _configuration.ManualSegments)
            {
                if (segmentData.Length < 10)
                    continue;
                if (TrafficLightsManual.IsSegmentLight((ushort) segmentData[0], segmentData[1]))
                    continue;
                Log.Message($"Adding Light to Segment {segmentData[0]}");
                try
                {
                    TrafficLightsManual.AddSegmentLight((ushort) segmentData[0], segmentData[1], RoadBaseAI.TrafficLightState.Green);
                    var segment = TrafficLightsManual.GetSegmentLight((ushort) segmentData[0], segmentData[1]);
                    segment.CurrentMode = (ManualSegmentLight.Mode) segmentData[2];
                    segment.LightLeft = (RoadBaseAI.TrafficLightState) segmentData[3];
                    segment.LightMain = (RoadBaseAI.TrafficLightState) segmentData[4];
                    segment.LightRight = (RoadBaseAI.TrafficLightState) segmentData[5];
                    segment.LightPedestrian = (RoadBaseAI.TrafficLightState) segmentData[6];
                    segment.LastChange = (uint) segmentData[7];
                    segment.LastChangeFrame = (uint) segmentData[8];
                    segment.PedestrianEnabled = Convert.ToBoolean(segmentData[9]);
                }
                catch (Exception e)
                {
                    // if we failed, just means it's old corrupt data. Ignore it and continue.
                    Log.Warning("Error loading data from the ManualSegments: " + e.Message);
                }
            }

            var timedStepCount = 0;
            var timedStepSegmentCount = 0;

            if (_configuration.TimedNodes.Count > 0)
            {
                for (var i = 0; i < _configuration.TimedNodes.Count; i++)
                {
                    Log.Message($"Adding Timed Node {i}");
                    try
                    {
                        var nodeid = (ushort) _configuration.TimedNodes[i][0];

                        var nodeGroup = new List<ushort>();
                        for (var j = 0; j < _configuration.TimedNodeGroups[i].Length; j++)
                        {
                            nodeGroup.Add(_configuration.TimedNodeGroups[i][j]);
                        }

                        if (TrafficLightsTimed.IsTimedLight(nodeid)) continue;
                        TrafficLightsTimed.AddTimedLight(nodeid, nodeGroup);
                        var timedNode = TrafficLightsTimed.GetTimedLight(nodeid);

                        timedNode.CurrentStep = _configuration.TimedNodes[i][1];

                        for (var j = 0; j < _configuration.TimedNodes[i][2]; j++)
                        {
                            var cfgstep = _configuration.TimedNodeSteps[timedStepCount];

                            timedNode.AddStep(cfgstep[0]);

                            var step = timedNode.Steps[j];

                            for (var k = 0; k < cfgstep[1]; k++)
                            {
                                step.LightLeft[k] =
                                    (RoadBaseAI.TrafficLightState)
                                        _configuration.TimedNodeStepSegments[timedStepSegmentCount][0];
                                step.LightMain[k] =
                                    (RoadBaseAI.TrafficLightState)
                                        _configuration.TimedNodeStepSegments[timedStepSegmentCount][1];
                                step.LightRight[k] =
                                    (RoadBaseAI.TrafficLightState)
                                        _configuration.TimedNodeStepSegments[timedStepSegmentCount][2];
                                step.LightPedestrian[k] =
                                    (RoadBaseAI.TrafficLightState)
                                        _configuration.TimedNodeStepSegments[timedStepSegmentCount][3];

                                timedStepSegmentCount++;
                            }

                            timedStepCount++;
                        }

                        if (Convert.ToBoolean(_configuration.TimedNodes[i][3]))
                        {
                            timedNode.Start();
                        }
                    }
                    catch (Exception e)
                    {
                        // ignore, as it's probably corrupt save data. it'll be culled on next save
                        Log.Warning("Error loading data from the TimedNodes: " + e.Message);
                    }
                }
            }

            Log.Message($"Config Nodes: {_configuration.NodeTrafficLights.Length}\nLevel Nodes: {Singleton<NetManager>.instance.m_nodes.m_buffer.Length}");
            var saveDataIndex = 0;
            var nodeCount = Singleton<NetManager>.instance.m_nodes.m_buffer.Length;
            if (nodeCount > 0)
            {
                for (var i = 0; i < nodeCount; i++)
                {
                    //Log.Message($"Adding NodeTrafficLights iteration: {i1}");
                    try
                    {
                        if (Singleton<NetManager>.instance.m_nodes.m_buffer[i].Info.m_class.m_service !=
                            ItemClass.Service.Road ||
                            Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags == 0)
                            continue;

                        // prevent overflow
                        if (_configuration.NodeTrafficLights.Length > saveDataIndex)
                        {

                            var trafficLight = _configuration.NodeTrafficLights[saveDataIndex];
                            if (trafficLight == '1')
                            {
                                //Log.Message($"Adding Traffic Light at Segment: {Singleton<NetManager>.instance.m_nodes.m_buffer[i].Info.name}");
                                Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags |=
                                    NetNode.Flags.TrafficLights;
                            }
                            else
                            {
                                //Log.Message($"Removing Traffic Light from Segment: {Singleton<NetManager>.instance.m_nodes.m_buffer[i].Info.name}");
                                Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags &=
                                    ~NetNode.Flags.TrafficLights;
                            }
                        }

                        if (_configuration.NodeCrosswalk.Length > saveDataIndex)
                        {
                            var crossWalk = _configuration.NodeCrosswalk[saveDataIndex];

                            if (crossWalk == '1')
                            {
                                Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags |= NetNode.Flags.Junction;
                            }
                            else
                            {
                                Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags &= ~NetNode.Flags.Junction;
                            }
                        }
                        ++saveDataIndex;
                    }
                    catch (Exception e)
                    {
                        // ignore as it's probably bad save data.
                        Log.Warning("Error setting the NodeTrafficLights: " + e.Message);
                    }
                }
            }

            // For Traffic++ compatibility
            if (!LoadingExtension.IsPathManagerCompatibile)
                return;

            Log.Message($"LaneFlags: {_configuration.LaneFlags}");
            var lanes = _configuration.LaneFlags.Split(',');

            if (lanes.Length <= 1)
                return;
            foreach (var lane in lanes)
            {
                var split = lane.Split(':');
                if (split.Length <= 1)
                    continue;
                try
                {
                    Log.Message($"Split Data: {split[0]} , {split[1]}");
                    var laneIndex = Convert.ToInt32(split[0]);

                    //make sure we don't cause any overflows because of bad save data.
                    if (Singleton<NetManager>.instance.m_lanes.m_buffer.Length <= laneIndex)
                        continue;

                    if (Convert.ToInt32(split[1]) > ushort.MaxValue)
                        continue;

                    Singleton<NetManager>.instance.m_lanes.m_buffer[Convert.ToInt32(split[0])].m_flags = Convert.ToUInt16(split[1]);
                }
                catch (Exception e)
                {
                    Log.Error($"Error loading Lane Split data. Length: {split.Length} value: {split}\nError: {e.Message}");
                }
            }
        }

        public override void OnSaveData()
        {
            Log.Message("Saving Mod Data.");
            var configuration = new Configuration();

            for (var i = 0; i < 32768; i++)
            {
                if (TrafficPriority.PrioritySegments.ContainsKey(i))
                {
                    
                    if (TrafficPriority.PrioritySegments[i].Node1 != 0)
                    {
                        Log.Message($"Saving Priority Segment of type: {TrafficPriority.PrioritySegments[i].Instance1.Type}");
                        configuration.PrioritySegments.Add(new[] { TrafficPriority.PrioritySegments[i].Node1, i, (int)TrafficPriority.PrioritySegments[i].Instance1.Type });
                    }
                    if (TrafficPriority.PrioritySegments[i].Node2 != 0)
                    {
                        Log.Message($"Saving Priority Segment of type: {TrafficPriority.PrioritySegments[i].Instance2.Type}");
                        configuration.PrioritySegments.Add(new[] { TrafficPriority.PrioritySegments[i].Node2, i, (int)TrafficPriority.PrioritySegments[i].Instance2.Type });
                    }
                }

                if (CustomRoadAI.NodeDictionary.ContainsKey((ushort) i))
                {
                    var nodeDict = CustomRoadAI.NodeDictionary[(ushort)i];

                    configuration.NodeDictionary.Add(new[] {nodeDict.NodeId, Convert.ToInt32(nodeDict.ManualTrafficLights), Convert.ToInt32(nodeDict.TimedTrafficLights), Convert.ToInt32(nodeDict.TimedTrafficLightsActive)});
                }

                if (TrafficLightsManual.ManualSegments.ContainsKey(i))
                {
                    if (TrafficLightsManual.ManualSegments[i].Node1 != 0)
                    {
                        var manualSegment = TrafficLightsManual.ManualSegments[i].Instance1;

                        configuration.ManualSegments.Add(new[]
                        {
                            manualSegment.Node,
                            manualSegment.Segment,
                            (int)manualSegment.CurrentMode,
                            (int)manualSegment.LightLeft,
                            (int)manualSegment.LightMain,
                            (int)manualSegment.LightRight,
                            (int)manualSegment.LightPedestrian,
                            (int)manualSegment.LastChange,
                            (int)manualSegment.LastChangeFrame,
                            Convert.ToInt32(manualSegment.PedestrianEnabled)
                        });
                    }
                    if (TrafficLightsManual.ManualSegments[i].Node2 != 0)
                    {
                        var manualSegment = TrafficLightsManual.ManualSegments[i].Instance2;

                        configuration.ManualSegments.Add(new[]
                        {
                            manualSegment.Node,
                            manualSegment.Segment,
                            (int)manualSegment.CurrentMode,
                            (int)manualSegment.LightLeft,
                            (int)manualSegment.LightMain,
                            (int)manualSegment.LightRight,
                            (int)manualSegment.LightPedestrian,
                            (int)manualSegment.LastChange,
                            (int)manualSegment.LastChangeFrame,
                            Convert.ToInt32(manualSegment.PedestrianEnabled)
                        });
                    }
                }

                if (!TrafficLightsTimed.TimedScripts.ContainsKey((ushort) i)) continue;

                var timedNode = TrafficLightsTimed.GetTimedLight((ushort) i);

                configuration.TimedNodes.Add(new[] { timedNode.NodeId, timedNode.CurrentStep, timedNode.NumSteps(), Convert.ToInt32(timedNode.IsStarted())});

                var nodeGroup = new ushort[timedNode.NodeGroup.Count];

                for (var j = 0; j < timedNode.NodeGroup.Count; j++)
                {
                    nodeGroup[j] = timedNode.NodeGroup[j];
                }

                configuration.TimedNodeGroups.Add(nodeGroup);

                for (var j = 0; j < timedNode.NumSteps(); j++)
                {
                    configuration.TimedNodeSteps.Add(new[]
                    {
                        timedNode.Steps[j].NumSteps,
                        timedNode.Steps[j].Segments.Count
                    });

                    for (var k = 0; k < timedNode.Steps[j].Segments.Count; k++)
                    {
                        configuration.TimedNodeStepSegments.Add(new[]
                        {
                            (int)timedNode.Steps[j].LightLeft[k],
                            (int)timedNode.Steps[j].LightMain[k],
                            (int)timedNode.Steps[j].LightRight[k],
                            (int)timedNode.Steps[j].LightPedestrian[k],
                        });
                    }
                }
            }

            for (var i = 0; i < Singleton<NetManager>.instance.m_nodes.m_buffer.Length; i++)
            {
                var nodeFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags;

                if (nodeFlags == 0) continue;
                if (Singleton<NetManager>.instance.m_nodes.m_buffer[i].Info.m_class.m_service != ItemClass.Service.Road)
                    continue;
                configuration.NodeTrafficLights +=
                    Convert.ToInt16((nodeFlags & NetNode.Flags.TrafficLights) != NetNode.Flags.None);
                configuration.NodeCrosswalk +=
                    Convert.ToInt16((nodeFlags & NetNode.Flags.Junction) != NetNode.Flags.None);

                if (!LoadingExtension.IsPathManagerCompatibile)
                    continue;

                var laneSegment = Singleton<NetManager>.instance.m_lanes.m_buffer[i].m_segment;

                if (TrafficPriority.PrioritySegments.ContainsKey(laneSegment))
                {
                    configuration.LaneFlags += $"{i}:{Singleton<NetManager>.instance.m_lanes.m_buffer[i].m_flags},";
                }
            }

            // Traffic++ compatibility
            if (!LoadingExtension.IsPathManagerCompatibile)
                configuration.LaneFlags = "";

            var binaryFormatter = new BinaryFormatter();
            var memoryStream = new MemoryStream();

            try
            {
                binaryFormatter.Serialize(memoryStream, configuration);
                memoryStream.Position = 0;
                Log.Message($"Save data byte length {memoryStream.Length}");
                SerializableData.SaveData(DataId, memoryStream.ToArray());

                Log.Message("Erasing old save data.");
                SerializableData.SaveData(LegacyDataId, new byte[] {});
            }
            catch (Exception ex)
            {
                Log.Error("Unexpected error saving data: " + ex.Message);
            }
            finally
            {
                memoryStream.Close();
            }
        }
        
    }
}
