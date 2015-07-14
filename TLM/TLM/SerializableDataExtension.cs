using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Timers;
using ColossalFramework;
using ColossalFramework.IO;
using ICities;
using TrafficManager.CustomAI;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;

namespace TrafficManager
{
    public class SerializableDataExtension : ISerializableDataExtension
    {
        public static string dataID = "TrafficManager_v0.9";
        public static UInt32 uniqueID;

        public static ISerializableData SerializableData;

        private static Timer _timer;

        private static bool loaded = false;

        public void OnCreated(ISerializableData serializableData)
        {
            uniqueID = 0u;
            SerializableData = serializableData;
            loaded = false;
        }

        public void OnReleased()
        {
            loaded = false;
        }

        public static void GenerateUniqueID()
        {
            uniqueID = (uint)UnityEngine.Random.Range(1000000f, 2000000f);

            while (File.Exists(Path.Combine(Application.dataPath, "trafficManagerSave_" + uniqueID + ".xml")))
            {
                uniqueID = (uint)UnityEngine.Random.Range(1000000f, 2000000f);
            }
        }

        public void OnLoadData()
        {
            byte[] data = SerializableData.LoadData(dataID);

            if (data == null)
            {
                GenerateUniqueID();
            }
            else
            {
                _timer = new System.Timers.Timer(2000);
                // Hook up the Elapsed event for the timer. 
                _timer.Elapsed += OnLoadDataTimed;
                _timer.Enabled = true;
            }
        }

        private static void OnLoadDataTimed(System.Object source, ElapsedEventArgs e)
        {
            byte[] data = SerializableData.LoadData(dataID);

            uniqueID = 0u;

            for (var i = 0; i < data.Length - 3; i++)
            {
                uniqueID = BitConverter.ToUInt32(data, i);
            }

            var filepath = Path.Combine(Application.dataPath, "trafficManagerSave_" + uniqueID + ".xml");
            _timer.Enabled = false;

            if (!File.Exists(filepath))
            {
                
                return;
            }

            var configuration = Configuration.Deserialize(filepath);

            for (var i = 0; i < configuration.prioritySegments.Count; i++)
            {
                if (
                    !TrafficPriority.IsPrioritySegment((ushort)configuration.prioritySegments[i][0],
                        configuration.prioritySegments[i][1]))
                {
                    TrafficPriority.AddPrioritySegment((ushort)configuration.prioritySegments[i][0],
                        configuration.prioritySegments[i][1],
                        (PrioritySegment.PriorityType)configuration.prioritySegments[i][2]);
                }
            }

            for (var i = 0; i < configuration.nodeDictionary.Count; i++)
            {
                if (CustomRoadAI.GetNodeSimulation((ushort)configuration.nodeDictionary[i][0]) == null)
                {
                    CustomRoadAI.AddNodeToSimulation((ushort)configuration.nodeDictionary[i][0]);
                    var nodeDict = CustomRoadAI.GetNodeSimulation((ushort)configuration.nodeDictionary[i][0]);

                    nodeDict.ManualTrafficLights = Convert.ToBoolean(configuration.nodeDictionary[i][1]);
                    nodeDict.TimedTrafficLights = Convert.ToBoolean(configuration.nodeDictionary[i][2]);
                    nodeDict.TimedTrafficLightsActive = Convert.ToBoolean(configuration.nodeDictionary[i][3]);
                }
            }

            for (var i = 0; i < configuration.manualSegments.Count; i++)
            {
                var segmentData = configuration.manualSegments[i];

                if (!TrafficLightsManual.IsSegmentLight((ushort)segmentData[0], segmentData[1]))
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
            }

            var timedStepCount = 0;
            var timedStepSegmentCount = 0;

            for (var i = 0; i < configuration.timedNodes.Count; i++)
            {
                var nodeid = (ushort)configuration.timedNodes[i][0];

                var nodeGroup = new List<ushort>();
                for (var j = 0; j < configuration.timedNodeGroups[i].Length; j++)
                {
                    nodeGroup.Add(configuration.timedNodeGroups[i][j]);
                }

                if (!TrafficLightsTimed.IsTimedLight(nodeid))
                {
                    TrafficLightsTimed.AddTimedLight(nodeid, nodeGroup);
                    var timedNode = TrafficLightsTimed.GetTimedLight(nodeid);

                    timedNode.CurrentStep = configuration.timedNodes[i][1];

                    for (var j = 0; j < configuration.timedNodes[i][2]; j++)
                    {
                        var cfgstep = configuration.timedNodeSteps[timedStepCount];

                        timedNode.AddStep(cfgstep[0]);

                        var step = timedNode.Steps[j];

                        for (var k = 0; k < cfgstep[1]; k++)
                        {
                            step.LightLeft[k] = (RoadBaseAI.TrafficLightState)configuration.timedNodeStepSegments[timedStepSegmentCount][0];
                            step.LightMain[k] = (RoadBaseAI.TrafficLightState)configuration.timedNodeStepSegments[timedStepSegmentCount][1];
                            step.LightRight[k] = (RoadBaseAI.TrafficLightState)configuration.timedNodeStepSegments[timedStepSegmentCount][2];
                            step.LightPedestrian[k] = (RoadBaseAI.TrafficLightState)configuration.timedNodeStepSegments[timedStepSegmentCount][3];

                            timedStepSegmentCount++;
                        }

                        timedStepCount++;
                    }

                    if (Convert.ToBoolean(configuration.timedNodes[i][3]))
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
                    var trafficLight = configuration.nodeTrafficLights[j1];

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
                    var crossWalk = configuration.nodeCrosswalk[j2];

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

            var lanes = configuration.laneFlags.Split(',');

            for (var i = 0; i < lanes.Length; i++)
            {
                var split = lanes[i].Split(':');

                Singleton<NetManager>.instance.m_lanes.m_buffer[Convert.ToInt32(split[0])].m_flags =
                    Convert.ToUInt16(split[1]);
            }
        }

        public void OnSaveData()
        {

            FastList<byte> data = new FastList<byte>();

            GenerateUniqueID(); 

            byte[] uniqueIdBytes = BitConverter.GetBytes(uniqueID);
            foreach (byte uniqueIdByte in uniqueIdBytes)
            {
                data.Add(uniqueIdByte);
            }

            byte[] dataToSave = data.ToArray();
            SerializableData.SaveData(dataID, dataToSave);

            var filepath = Path.Combine(Application.dataPath, "trafficManagerSave_" + uniqueID + ".xml");

            var configuration = new Configuration();

            for (var i = 0; i < 32768; i++)
            {
                if (TrafficPriority.PrioritySegments.ContainsKey(i))
                {
                    if (TrafficPriority.PrioritySegments[i].Node1 != 0)
                    {
                        configuration.prioritySegments.Add(new int[3] { TrafficPriority.PrioritySegments[i].Node1, i, (int)TrafficPriority.PrioritySegments[i].Instance1.type });
                    } 
                    if (TrafficPriority.PrioritySegments[i].Node2 != 0)
                    {
                        configuration.prioritySegments.Add(new int[3] { TrafficPriority.PrioritySegments[i].Node2, i, (int)TrafficPriority.PrioritySegments[i].Instance2.type });
                    }
                }

                if (CustomRoadAI.NodeDictionary.ContainsKey((ushort) i))
                {
                    var nodeDict = CustomRoadAI.NodeDictionary[(ushort)i];

                    configuration.nodeDictionary.Add(new int[4] {nodeDict.NodeId, Convert.ToInt32(nodeDict.ManualTrafficLights), Convert.ToInt32(nodeDict.TimedTrafficLights), Convert.ToInt32(nodeDict.TimedTrafficLightsActive)});
                }

                if (TrafficLightsManual.ManualSegments.ContainsKey(i))
                {
                    if (TrafficLightsManual.ManualSegments[i].Node1 != 0)
                    {
                        var manualSegment = TrafficLightsManual.ManualSegments[i].Instance1;

                        configuration.manualSegments.Add(new int[10]
                        {
                            (int)manualSegment.Node,
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

                        configuration.manualSegments.Add(new int[10]
                        {
                            (int)manualSegment.Node,
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

                    configuration.timedNodes.Add(new int[4] { timedNode.NodeId, timedNode.CurrentStep, timedNode.NumSteps(), Convert.ToInt32(timedNode.IsStarted())});

                    var nodeGroup = new ushort[timedNode.NodeGroup.Count];

                    for (var j = 0; j < timedNode.NodeGroup.Count; j++)
                    {
                        nodeGroup[j] = timedNode.NodeGroup[j];
                    }

                    configuration.timedNodeGroups.Add(nodeGroup);

                    for (var j = 0; j < timedNode.NumSteps(); j++)
                    {
                        configuration.timedNodeSteps.Add(new int[2]
                        {
                            timedNode.Steps[j].NumSteps,
                            timedNode.Steps[j].Segments.Count
                        });

                        for (var k = 0; k < timedNode.Steps[j].Segments.Count; k++)
                        {
                            configuration.timedNodeStepSegments.Add(new int[4]
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
                        configuration.nodeTrafficLights +=
                            Convert.ToInt16((nodeFlags & NetNode.Flags.TrafficLights) != NetNode.Flags.None);
                        configuration.nodeCrosswalk +=
                            Convert.ToInt16((nodeFlags & NetNode.Flags.Junction) != NetNode.Flags.None);
                    }
                }
            }

            for (var i = 0; i < Singleton<NetManager>.instance.m_lanes.m_buffer.Length; i++)
            {
                var laneSegment = Singleton<NetManager>.instance.m_lanes.m_buffer[i].m_segment;

                if (TrafficPriority.PrioritySegments.ContainsKey(laneSegment))
                {
                    configuration.laneFlags += i + ":" + Singleton<NetManager>.instance.m_lanes.m_buffer[i].m_flags + ",";
                }
            }

            Configuration.Serialize(filepath, configuration);
        }
    }
}
