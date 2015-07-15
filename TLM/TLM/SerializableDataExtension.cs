using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using ColossalFramework;
using ICities;
using TrafficManager.CustomAI;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;
using Object = System.Object;
using Random = UnityEngine.Random;

namespace TrafficManager
{
    public class SerializableDataExtension : ISerializableDataExtension
    {
        public static string DataId = "TrafficManager_v0.9";
        public static uint UniqueId;

        public static ISerializableData SerializableData;

        private static Timer _timer;
        
        public void OnCreated(ISerializableData serializableData)
        {
            UniqueId = 0u;
            SerializableData = serializableData;
        }

        public void OnReleased()
        {
        }

        public static void GenerateUniqueId()
        {
            UniqueId = (uint)Random.Range(1000000f, 2000000f);

            while (File.Exists(Path.Combine(Application.dataPath, "trafficManagerSave_" + UniqueId + ".xml")))
            {
                UniqueId = (uint)Random.Range(1000000f, 2000000f);
            }
        }

        public void OnLoadData()
        {
            byte[] data = SerializableData.LoadData(DataId);

            if (data == null)
            {
                GenerateUniqueId();
            }
            else
            {
                _timer = new Timer(2000);
                // Hook up the Elapsed event for the timer. 
                _timer.Elapsed += OnLoadDataTimed;
                _timer.Enabled = true;
            }
        }

        private static void OnLoadDataTimed(Object source, ElapsedEventArgs e)
        {
            byte[] data = SerializableData.LoadData(DataId);

            UniqueId = 0u;

            for (var i = 0; i < data.Length - 3; i++)
            {
                UniqueId = BitConverter.ToUInt32(data, i);
            }

            var filepath = Path.Combine(Application.dataPath, "trafficManagerSave_" + UniqueId + ".xml");
            _timer.Enabled = false;

            if (!File.Exists(filepath))
            {
                
                return;
            }

            var configuration = Configuration.LoadConfigurationFromFile(filepath);

            foreach (var segment in configuration.PrioritySegments.Where(segment => !TrafficPriority.IsPrioritySegment((ushort)segment[0],
                segment[1])))
            {
                TrafficPriority.AddPrioritySegment((ushort)segment[0],
                    segment[1],
                    (PrioritySegment.PriorityType)segment[2]);
            }

            foreach (var node in configuration.NodeDictionary.Where(node => CustomRoadAI.GetNodeSimulation((ushort)node[0]) == null))
            {
                CustomRoadAI.AddNodeToSimulation((ushort)node[0]);
                var nodeDict = CustomRoadAI.GetNodeSimulation((ushort)node[0]);

                nodeDict.ManualTrafficLights = Convert.ToBoolean(node[1]);
                nodeDict.TimedTrafficLights = Convert.ToBoolean(node[2]);
                nodeDict.TimedTrafficLightsActive = Convert.ToBoolean(node[3]);
            }

            foreach (var segmentData in configuration.ManualSegments.Where(segmentData => !TrafficLightsManual.IsSegmentLight((ushort)segmentData[0], segmentData[1])))
            {
                TrafficLightsManual.AddSegmentLight((ushort)segmentData[0], segmentData[1],
                    RoadBaseAI.TrafficLightState.Green);
                var segment = TrafficLightsManual.GetSegmentLight((ushort)segmentData[0], segmentData[1]);
                segment.CurrentMode = (ManualSegmentLight.Mode)segmentData[2];
                segment.LightLeft = (RoadBaseAI.TrafficLightState)segmentData[3];
                segment.LightMain = (RoadBaseAI.TrafficLightState)segmentData[4];
                segment.LightRight = (RoadBaseAI.TrafficLightState)segmentData[5];
                segment.LightPedestrian = (RoadBaseAI.TrafficLightState)segmentData[6];
                segment.LastChange = (uint)segmentData[7];
                segment.LastChangeFrame = (uint)segmentData[8];
                segment.PedestrianEnabled = Convert.ToBoolean(segmentData[9]);
            }

            var timedStepCount = 0;
            var timedStepSegmentCount = 0;

            for (var i = 0; i < configuration.TimedNodes.Count; i++)
            {
                var nodeid = (ushort)configuration.TimedNodes[i][0];

                var nodeGroup = new List<ushort>();
                for (var j = 0; j < configuration.TimedNodeGroups[i].Length; j++)
                {
                    nodeGroup.Add(configuration.TimedNodeGroups[i][j]);
                }

                if (!TrafficLightsTimed.IsTimedLight(nodeid))
                {
                    TrafficLightsTimed.AddTimedLight(nodeid, nodeGroup);
                    var timedNode = TrafficLightsTimed.GetTimedLight(nodeid);

                    timedNode.CurrentStep = configuration.TimedNodes[i][1];

                    for (var j = 0; j < configuration.TimedNodes[i][2]; j++)
                    {
                        var cfgstep = configuration.TimedNodeSteps[timedStepCount];

                        timedNode.AddStep(cfgstep[0]);

                        var step = timedNode.Steps[j];

                        for (var k = 0; k < cfgstep[1]; k++)
                        {
                            step.LightLeft[k] = (RoadBaseAI.TrafficLightState)configuration.TimedNodeStepSegments[timedStepSegmentCount][0];
                            step.LightMain[k] = (RoadBaseAI.TrafficLightState)configuration.TimedNodeStepSegments[timedStepSegmentCount][1];
                            step.LightRight[k] = (RoadBaseAI.TrafficLightState)configuration.TimedNodeStepSegments[timedStepSegmentCount][2];
                            step.LightPedestrian[k] = (RoadBaseAI.TrafficLightState)configuration.TimedNodeStepSegments[timedStepSegmentCount][3];

                            timedStepSegmentCount++;
                        }

                        timedStepCount++;
                    }

                    if (Convert.ToBoolean(configuration.TimedNodes[i][3]))
                    {
                        timedNode.Start();
                    }
                }
            }

            var j1 = 0;
            for (var i1 = 0; i1 < 32768; i1++)
            {
                if (Singleton<NetManager>.instance.m_nodes.m_buffer[i1].Info.m_class.m_service ==
                    ItemClass.Service.Road && Singleton<NetManager>.instance.m_nodes.m_buffer[i1].m_flags != 0)
                {
                    var trafficLight = configuration.NodeTrafficLights[j1];

                    if (trafficLight == '1')
                    {
                        Singleton<NetManager>.instance.m_nodes.m_buffer[i1].m_flags |= NetNode.Flags.TrafficLights;
                    }
                    else
                    {
                        Singleton<NetManager>.instance.m_nodes.m_buffer[i1].m_flags &= ~NetNode.Flags.TrafficLights;
                    }

                    j1++;
                }
            }

            var j2 = 0;
            for (var i2 = 0; i2 < 32768; i2++)
            {
                if (Singleton<NetManager>.instance.m_nodes.m_buffer[i2].Info.m_class.m_service ==
                    ItemClass.Service.Road && Singleton<NetManager>.instance.m_nodes.m_buffer[i2].m_flags != 0)
                {
                    var crossWalk = configuration.NodeCrosswalk[j2];

                    if (crossWalk == '1')
                    {
                        Singleton<NetManager>.instance.m_nodes.m_buffer[i2].m_flags |= NetNode.Flags.Junction;
                    }
                    else
                    {
                        Singleton<NetManager>.instance.m_nodes.m_buffer[i2].m_flags &= ~NetNode.Flags.Junction;
                    }

                    j2++;
                }
            }

            var lanes = configuration.LaneFlags.Split(',');

            foreach (var split in lanes.Select(lane => lane.Split(':')))
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[Convert.ToInt32(split[0])].m_flags =
                    Convert.ToUInt16(split[1]);
            }
        }

        public void OnSaveData()
        {

            FastList<byte> data = new FastList<byte>();

            GenerateUniqueId(); 

            byte[] uniqueIdBytes = BitConverter.GetBytes(UniqueId);
            foreach (byte uniqueIdByte in uniqueIdBytes)
            {
                data.Add(uniqueIdByte);
            }

            byte[] dataToSave = data.ToArray();
            SerializableData.SaveData(DataId, dataToSave);

            var filepath = Path.Combine(Application.dataPath, "trafficManagerSave_" + UniqueId + ".xml");

            var configuration = new Configuration();

            for (var i = 0; i < 32768; i++)
            {
                if (TrafficPriority.PrioritySegments.ContainsKey(i))
                {
                    if (TrafficPriority.PrioritySegments[i].Node1 != 0)
                    {
                        configuration.PrioritySegments.Add(new[] { TrafficPriority.PrioritySegments[i].Node1, i, (int)TrafficPriority.PrioritySegments[i].Instance1.Type });
                    } 
                    if (TrafficPriority.PrioritySegments[i].Node2 != 0)
                    {
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

                if (TrafficLightsTimed.TimedScripts.ContainsKey((ushort)i))
                {
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
            }

            for (var i = 0; i < Singleton<NetManager>.instance.m_nodes.m_buffer.Length; i++)
            {
                var nodeFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags;

                if (nodeFlags != 0)
                {
                    if (Singleton<NetManager>.instance.m_nodes.m_buffer[i].Info.m_class.m_service ==
                        ItemClass.Service.Road)
                    {
                        configuration.NodeTrafficLights +=
                            Convert.ToInt16((nodeFlags & NetNode.Flags.TrafficLights) != NetNode.Flags.None);
                        configuration.NodeCrosswalk +=
                            Convert.ToInt16((nodeFlags & NetNode.Flags.Junction) != NetNode.Flags.None);
                    }
                }
            }

            for (var i = 0; i < Singleton<NetManager>.instance.m_lanes.m_buffer.Length; i++)
            {
                var laneSegment = Singleton<NetManager>.instance.m_lanes.m_buffer[i].m_segment;

                if (TrafficPriority.PrioritySegments.ContainsKey(laneSegment))
                {
                    configuration.LaneFlags += i + ":" + Singleton<NetManager>.instance.m_lanes.m_buffer[i].m_flags + ",";
                }
            }

            Configuration.SaveConfigurationToFile(filepath, configuration);
        }
    }
}
