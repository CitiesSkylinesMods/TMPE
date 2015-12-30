using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using ColossalFramework;
using ICities;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;
using Random = UnityEngine.Random;
using Timer = System.Timers.Timer;

namespace TrafficManager {
	public class SerializableDataExtension : SerializableDataExtensionBase {
		private const string LegacyDataId = "TrafficManager_v0.9";
		private const string DataId = "TrafficManager_v1.0";
		private static uint _uniqueId;

		private static ISerializableData _serializableData;
		private static Configuration _configuration;
		public static bool ConfigLoaded;
		public static bool StateLoaded;

		public override void OnCreated(ISerializableData serializableData) {
			_uniqueId = 0u;
			_serializableData = serializableData;
		}

		public override void OnReleased() {
		}

		[Obsolete("Part of the old save system. Will be removed eventually.")]
		private static void GenerateUniqueId() {
			_uniqueId = (uint)Random.Range(1000000f, 2000000f);

			while (File.Exists(Path.Combine(Application.dataPath, "trafficManagerSave_" + _uniqueId + ".xml"))) {
				_uniqueId = (uint)Random.Range(1000000f, 2000000f);
			}
		}

		public override void OnLoadData() {
			Log.Warning("Loading Mod Data");
			var keys = _serializableData.EnumerateData().Where(k => k.StartsWith("TrafficManager"));
			byte[] data = null;
			foreach (var key in keys) {
				Log.Message($"Checking for save data at key: {key}");
				data = _serializableData.LoadData(key);

				if (data == null || data.Length <= 0)
					continue;

				Log.Message($"Save Data Found. Deserializing.");
				break;
			}
			if (data == null) {
				Log.Message($"No Save Data Found. Possibly a new game?");
				return;
			}
			DeserializeData(data);
		}

		private static string LoadLegacyData(byte[] data) {
			_uniqueId = 0u;

			for (var i = 0; i < data.Length - 3; i++) {
				_uniqueId = BitConverter.ToUInt32(data, i);
			}

			Log.Message($"Looking for legacy TrafficManagerSave file trafficManagerSave_{_uniqueId}.xml");
			var filepath = Path.Combine(Application.dataPath, "trafficManagerSave_" + _uniqueId + ".xml");

			if (File.Exists(filepath))
				return filepath;

			Log.Message("Legacy Save Data doesn't exist. Expected: " + filepath);
			throw new FileNotFoundException("Legacy data not present.");
		}

		private static void DeserializeData(byte[] data) {
			string legacyFilepath = null;
			try {
				legacyFilepath = LoadLegacyData(data);
			} catch (Exception) {
				// data isn't legacy compatible. Probably new format or missing data.
			}

			if (legacyFilepath != null) {
				Log.Message("Converting Legacy Config Data.");
				_configuration = Configuration.LoadConfigurationFromFile(legacyFilepath);
			} else {
				if (data.Length == 0) {
					Log.Message("Legacy data was empty. Checking for new Save data.");
					data = _serializableData.LoadData(DataId);
				}

				try {
					if (data.Length != 0) {
						Log.Message("Loading Data from New Load Routine!");
						var memoryStream = new MemoryStream();
						memoryStream.Write(data, 0, data.Length);
						memoryStream.Position = 0;

						var binaryFormatter = new BinaryFormatter();
						_configuration = (Configuration)binaryFormatter.Deserialize(memoryStream);
					}
				} catch (Exception e) {
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

		private static void LoadDataState() {
			Log.Message("Loading State from Config");
			if (_configuration == null) {
				Log.Message("Configuration NULL, Couldn't load save data. Possibly a new game?");
				return;
			}
			foreach (var segment in _configuration.PrioritySegments) {
				if (segment.Length < 3)
					continue;
				if (TrafficPriority.IsPrioritySegment((ushort)segment[0], (ushort)segment[1]))
					continue;
				Log.Message($"Adding Priority Segment of type: {segment[2].ToString()}");
				TrafficPriority.AddPrioritySegment((ushort)segment[0], (ushort)segment[1], (PrioritySegment.PriorityType)segment[2]);
			}

			foreach (var node in _configuration.NodeDictionary) {
				if (node.Length < 4)
					continue;
				if (TrafficPriority.GetNodeSimulation((ushort)node[0]) != null)
					continue;

				Log.Message($"Adding Node do Simulation {node[0]}");
				try {
					TrafficPriority.AddNodeToSimulation((ushort)node[0]);
					var nodeDict = TrafficPriority.GetNodeSimulation((ushort)node[0]);

					nodeDict.ManualTrafficLights = Convert.ToBoolean(node[1]);
					nodeDict.TimedTrafficLights = Convert.ToBoolean(node[2]);
					nodeDict.TimedTrafficLightsActive = Convert.ToBoolean(node[3]);
				} catch (Exception e) {
					// if we failed, just means it's old corrupt data. Ignore it and continue.
					Log.Warning("Error loading data from the NodeDictionary: " + e.Message);
				}
			}

			foreach (var segmentData in _configuration.ManualSegments) {
				if (segmentData.Length < 10)
					continue;

				if (TrafficLightsManual.IsSegmentLight((ushort)segmentData[0], (ushort)segmentData[1]))
					continue;

				Log.Message($"Adding Light to Segment {segmentData[0]}");
				try {
					TrafficLightsManual.AddSegmentLight((ushort)segmentData[0], (ushort)segmentData[1], RoadBaseAI.TrafficLightState.Green);
					var segment = TrafficLightsManual.GetSegmentLight((ushort)segmentData[0], (ushort)segmentData[1]);
					segment.CurrentMode = (ManualSegmentLight.Mode)segmentData[2];
					segment.LightLeft = (RoadBaseAI.TrafficLightState)segmentData[3];
					segment.LightMain = (RoadBaseAI.TrafficLightState)segmentData[4];
					segment.LightRight = (RoadBaseAI.TrafficLightState)segmentData[5];
					segment.LightPedestrian = (RoadBaseAI.TrafficLightState)segmentData[6];
					segment.LastChange = (uint)segmentData[7];
					segment.LastChangeFrame = (uint)segmentData[8];
					segment.PedestrianEnabled = Convert.ToBoolean(segmentData[9]);
				} catch (Exception e) {
					// if we failed, just means it's old corrupt data. Ignore it and continue.
					Log.Warning("Error loading data from the ManualSegments: " + e.Message);
				}
			}

			var timedStepCount = 0;
			var timedStepSegmentCount = 0;

			if (_configuration.TimedNodes.Count > 0) {
				for (var i = 0; i < _configuration.TimedNodes.Count; i++) {
					try {
						var nodeid = (ushort)_configuration.TimedNodes[i][0];
						Log.Message($"Adding Timed Node {i} at node {nodeid}");

						var nodeGroup = new List<ushort>();
						for (var j = 0; j < _configuration.TimedNodeGroups[i].Length; j++) {
							nodeGroup.Add(_configuration.TimedNodeGroups[i][j]);
						}

						if (TrafficLightsTimed.IsTimedLight(nodeid)) continue;
						TrafficLightsTimed.AddTimedLight(nodeid, nodeGroup);
						var timedNode = TrafficLightsTimed.GetTimedLight(nodeid);

						timedNode.CurrentStep = _configuration.TimedNodes[i][1];

						for (var j = 0; j < _configuration.TimedNodes[i][2]; j++) {
							var cfgstep = _configuration.TimedNodeSteps[timedStepCount];
							// old (pre 1.3.0):
							//   cfgstep[0]: time of step
							//   cfgstep[1]: number of segments
							// new (post 1.3.0):
							//   cfgstep[0]: min. time of step
							//   cfgstep[1]: max. time of step
							//   cfgstep[2]: number of segments

							int minTime = 1;
							int maxTime = 1;
							int numSegments = 0;

							if (cfgstep.Length == 2) {
								minTime = cfgstep[0];
								maxTime = cfgstep[0];
								numSegments = cfgstep[1];
							} else if (cfgstep.Length == 3) {
								minTime = cfgstep[0];
								maxTime = cfgstep[1];
								numSegments = cfgstep[2];
							}

							timedNode.AddStep(minTime, maxTime);

							var step = timedNode.Steps[j];
							if (numSegments <= step.segmentIds.Count) {
								for (var k = 0; k < numSegments; k++) {
									ushort stepSegmentId = (ushort)step.segmentIds[k];

									var leftLightState = (RoadBaseAI.TrafficLightState)_configuration.TimedNodeStepSegments[timedStepSegmentCount][0];
									var mainLightState = (RoadBaseAI.TrafficLightState)_configuration.TimedNodeStepSegments[timedStepSegmentCount][1];
									var rightLightState = (RoadBaseAI.TrafficLightState)_configuration.TimedNodeStepSegments[timedStepSegmentCount][2];
									var pedLightState = (RoadBaseAI.TrafficLightState)_configuration.TimedNodeStepSegments[timedStepSegmentCount][3];

									//ManualSegmentLight segmentLight = new ManualSegmentLight(step.NodeId, step.segmentIds[k], mainLightState, leftLightState, rightLightState, pedLightState);
									step.segmentLightStates[stepSegmentId].LightLeft = leftLightState;
									step.segmentLightStates[stepSegmentId].LightMain = mainLightState;
									step.segmentLightStates[stepSegmentId].LightRight = rightLightState;
									step.segmentLightStates[stepSegmentId].LightPedestrian = pedLightState;

									timedStepSegmentCount++;
								}
							}
							timedStepCount++;
						}

						if (Convert.ToBoolean(_configuration.TimedNodes[i][3])) {
							timedNode.Start();
						}
					} catch (Exception e) {
						// ignore, as it's probably corrupt save data. it'll be culled on next save
						Log.Warning("Error loading data from the TimedNodes: " + e.Message);
					}
				}
			}

			Log.Message($"Config Nodes: {_configuration.NodeTrafficLights.Length}\nLevel Nodes: {Singleton<NetManager>.instance.m_nodes.m_buffer.Length}");
			var saveDataIndex = 0;
			var nodeCount = Singleton<NetManager>.instance.m_nodes.m_buffer.Length;
			if (nodeCount > 0) {
				for (var i = 0; i < nodeCount; i++) {
					//Log.Message($"Adding NodeTrafficLights iteration: {i1}");
					try {
						if (Singleton<NetManager>.instance.m_nodes.m_buffer[i].Info.m_class.m_service !=
							ItemClass.Service.Road ||
							Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags == 0)
							continue;

						// prevent overflow
						if (_configuration.NodeTrafficLights.Length > saveDataIndex) {

							var trafficLight = _configuration.NodeTrafficLights[saveDataIndex];
							if (trafficLight == '1') {
								//Log.Message($"Adding Traffic Light at Segment: {Singleton<NetManager>.instance.m_nodes.m_buffer[i].Info.name}");
								Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags |=
									NetNode.Flags.TrafficLights;
							} else {
								//Log.Message($"Removing Traffic Light from Segment: {Singleton<NetManager>.instance.m_nodes.m_buffer[i].Info.name}");
								Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags &=
									~NetNode.Flags.TrafficLights;
							}
						}

						if (_configuration.NodeCrosswalk.Length > saveDataIndex) {
							var crossWalk = _configuration.NodeCrosswalk[saveDataIndex];

							if (crossWalk == '1') {
								Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags |= NetNode.Flags.Junction;
							} else {
								Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags &= ~NetNode.Flags.Junction;
							}
						}
						++saveDataIndex;
					} catch (Exception e) {
						// ignore as it's probably bad save data.
						Log.Warning("Error setting the NodeTrafficLights: " + e.Message);
					}
				}
			}

			// For Traffic++ compatibility
			if (!LoadingExtension.IsPathManagerCompatible)
				return;

			Log.Message($"LaneFlags: {_configuration.LaneFlags}");
			var lanes = _configuration.LaneFlags.Split(',');

			if (lanes.Length <= 1)
				return;
			foreach (var split in lanes.Select(lane => lane.Split(':')).Where(split => split.Length > 1)) {
				try {
					Log.Message($"Split Data: {split[0]} , {split[1]}");
					var laneIndex = Convert.ToInt32(split[0]);

					//make sure we don't cause any overflows because of bad save data.
					if (Singleton<NetManager>.instance.m_lanes.m_buffer.Length <= laneIndex)
						continue;

					if (Convert.ToInt32(split[1]) > ushort.MaxValue)
						continue;

					Log.Message("Setting flags for lane " + Convert.ToInt32(split[0]) + " to " + Convert.ToUInt16(split[1]));
					Singleton<NetManager>.instance.m_lanes.m_buffer[Convert.ToInt32(split[0])].m_flags =
						Convert.ToUInt16(split[1]);
				} catch (Exception e) {
					Log.Error(
						$"Error loading Lane Split data. Length: {split.Length} value: {split}\nError: {e.Message}");
				}
			}
		}

		public override void OnSaveData() {
			Log.Warning("Saving Mod Data.");
			var configuration = new Configuration();

			for (ushort i = 0; i < 36864; i++) {
				if (TrafficPriority.PrioritySegments != null) {
					AddPrioritySegment(i, configuration);
				}

				if (TrafficPriority.LightSimByNodeId != null) {
					AddNodeToDictionary(i, configuration);
				}

				if (TrafficLightsManual.ManualSegments != null) {
					AddManualTrafficLight(i, configuration);
				}

				if (TrafficLightsTimed.TimedScripts != null) {
					AddTimedTrafficLight(i, configuration);
				}
			}

			if (Singleton<NetManager>.instance?.m_nodes?.m_buffer != null) {
				for (var i = 0; i < Singleton<NetManager>.instance.m_nodes.m_buffer.Length; i++) {
					if (AddNodeLightsAndCrosswalks(i, configuration))
						continue;
				}
			}

			if (LoadingExtension.IsPathManagerCompatible && Singleton<NetManager>.instance?.m_lanes?.m_buffer != null) {
				for (var i = 0; i < Singleton<NetManager>.instance.m_lanes.m_buffer.Length; i++) {
					AddLaneData(i, configuration);
				}
			} else {
				// Traffic++ compatibility
				configuration.LaneFlags = "";
			}

			var binaryFormatter = new BinaryFormatter();
			var memoryStream = new MemoryStream();

			try {
				binaryFormatter.Serialize(memoryStream, configuration);
				memoryStream.Position = 0;
				Log.Message($"Save data byte length {memoryStream.Length}");
				_serializableData.SaveData(DataId, memoryStream.ToArray());

				Log.Message("Erasing old save data.");
				_serializableData.SaveData(LegacyDataId, new byte[] { });
			} catch (Exception ex) {
				Log.Error("Unexpected error saving data: " + ex.Message);
			} finally {
				memoryStream.Close();
			}
		}

		private static void AddLaneData(int i, Configuration configuration) {
			try {
				NetLane lane = Singleton<NetManager>.instance.m_lanes.m_buffer[i];
				NetLane.Flags flags = (NetLane.Flags)lane.m_flags;
				if ((flags & NetLane.Flags.LeftForwardRight) == NetLane.Flags.None) // only save lanes with explicit lane arrows
					return;
				var laneSegmentId = lane.m_segment;
				if (laneSegmentId <= 0)
					return;
				NetSegment segment = Singleton<NetManager>.instance.m_segments.m_buffer[laneSegmentId];
				if (segment.m_flags == NetSegment.Flags.None)
					return;

				//if (TrafficPriority.PrioritySegments.ContainsKey(laneSegmentId)) {
					Log.Message($"Saving lane data for lane {i}, segment {laneSegmentId}");
					configuration.LaneFlags += $"{i}:{Singleton<NetManager>.instance.m_lanes.m_buffer[i].m_flags},";
				//}
			} catch (Exception e) {
				Log.Error($"Error saving NodeLaneData {e.Message}");
			}
		}

		private static bool AddNodeLightsAndCrosswalks(int i, Configuration configuration) {
			try {
				var nodeFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags;

				if (nodeFlags == 0)
					return true;
				if (Singleton<NetManager>.instance.m_nodes.m_buffer[i].Info.m_class.m_service !=
					ItemClass.Service.Road)
					return true;
				configuration.NodeTrafficLights +=
					Convert.ToInt16((nodeFlags & NetNode.Flags.TrafficLights) != NetNode.Flags.None);
				configuration.NodeCrosswalk +=
					Convert.ToInt16((nodeFlags & NetNode.Flags.Junction) != NetNode.Flags.None);
				return false;
			} catch (Exception e) {
				Log.Error($"Error Adding Node Lights and Crosswalks {e.Message}");
				return true;
			}
		}

		private static void AddTimedTrafficLight(int i, Configuration configuration) {
			try {
				if (!TrafficLightsTimed.TimedScripts.ContainsKey((ushort)i))
					return;

				var timedNode = TrafficLightsTimed.GetTimedLight((ushort)i);

				configuration.TimedNodes.Add(new[]
				{
					timedNode.nodeId, timedNode.CurrentStep, timedNode.NumSteps(),
					Convert.ToInt32(timedNode.IsStarted())
				});

				var nodeGroup = new ushort[timedNode.NodeGroup.Count];

				for (var j = 0; j < timedNode.NodeGroup.Count; j++) {
					nodeGroup[j] = timedNode.NodeGroup[j];
				}

				configuration.TimedNodeGroups.Add(nodeGroup);

				// get segment ids which are still defined but for which real road segments are missing
				HashSet<ushort> invalidSegmentIds = timedNode.getInvalidSegmentIds();

				for (var j = 0; j < timedNode.NumSteps(); j++) {
					int validCount = timedNode.Steps[j].segmentIds.Count - invalidSegmentIds.Count;

					configuration.TimedNodeSteps.Add(new[]
					{
						timedNode.Steps[j].minTime,
						timedNode.Steps[j].maxTime,
						validCount
					});

					for (var k = 0; k < timedNode.Steps[j].segmentIds.Count; k++) {
						var segmentId = timedNode.Steps[j].segmentIds[k];

						if (invalidSegmentIds.Contains(segmentId))
							continue;

						var segLight = timedNode.Steps[j].segmentLightStates[segmentId];
						configuration.TimedNodeStepSegments.Add(new[]
						{
							(int) segLight.LightLeft,
							(int) segLight.LightMain,
							(int) segLight.LightRight,
							(int) segLight.LightPedestrian
						});
					}
				}
			} catch (Exception e) {
				Log.Error($"Error adding TimedTrafficLights to save {e.Message}");
			}
		}

		private static void AddManualTrafficLight(ushort segmentId, Configuration configuration) {
			try {
				if (!TrafficLightsManual.ManualSegments.ContainsKey(segmentId))
					return;

				if (TrafficLightsManual.ManualSegments[segmentId].Node1 != 0) {
					var manualSegment = TrafficLightsManual.ManualSegments[segmentId].Instance1;

					configuration.ManualSegments.Add(new[]
					{
						manualSegment.nodeId,
						manualSegment.SegmentId,
						(int) manualSegment.CurrentMode,
						(int) manualSegment.LightLeft,
						(int) manualSegment.LightMain,
						(int) manualSegment.LightRight,
						(int) manualSegment.LightPedestrian,
						(int) manualSegment.LastChange,
						(int) manualSegment.LastChangeFrame,
						Convert.ToInt32(manualSegment.PedestrianEnabled)
					});
				}
				if (TrafficLightsManual.ManualSegments[segmentId].Node2 == 0)
					return;
				var manualSegmentLight = TrafficLightsManual.ManualSegments[segmentId].Instance2;

				configuration.ManualSegments.Add(new[]
				{
					manualSegmentLight.nodeId,
					manualSegmentLight.SegmentId,
					(int) manualSegmentLight.CurrentMode,
					(int) manualSegmentLight.LightLeft,
					(int) manualSegmentLight.LightMain,
					(int) manualSegmentLight.LightRight,
					(int) manualSegmentLight.LightPedestrian,
					(int) manualSegmentLight.LastChange,
					(int) manualSegmentLight.LastChangeFrame,
					Convert.ToInt32(manualSegmentLight.PedestrianEnabled)
				});
			} catch (Exception e) {
				Log.Error($"Error saving ManualTraffic Lights {e.Message}");
			}
		}

		private static void AddNodeToDictionary(int i, Configuration configuration) {
			try {
				if (!TrafficPriority.LightSimByNodeId.ContainsKey((ushort)i))
					return;
				var nodeDict = TrafficPriority.LightSimByNodeId[(ushort)i];

				configuration.NodeDictionary.Add(new[]
				{
					nodeDict.nodeId, Convert.ToInt32(nodeDict.ManualTrafficLights),
					Convert.ToInt32(nodeDict.TimedTrafficLights),
					Convert.ToInt32(nodeDict.TimedTrafficLightsActive)
				});
			} catch (Exception e) {
				Log.Error($"Error adding Nodes to Dictionary {e.Message}");
			}
		}

		private static void AddPrioritySegment(ushort segmentId, Configuration configuration) {
			try {
				if (!TrafficPriority.PrioritySegments.ContainsKey(segmentId))
					return;
				if (TrafficPriority.PrioritySegments[segmentId].Node1 != 0) {
					Log.Message(
						$"Saving Priority Segment of type: {TrafficPriority.PrioritySegments[segmentId].Instance1.Type}");
					configuration.PrioritySegments.Add(new[]
					{
						TrafficPriority.PrioritySegments[segmentId].Node1, segmentId,
						(int) TrafficPriority.PrioritySegments[segmentId].Instance1.Type
					});
				}

				if (TrafficPriority.PrioritySegments[segmentId].Node2 == 0)
					return;
				Log.Message(
					$"Saving Priority Segment of type: {TrafficPriority.PrioritySegments[segmentId].Instance2.Type}");
				configuration.PrioritySegments.Add(new[]
				{
					TrafficPriority.PrioritySegments[segmentId].Node2, segmentId,
					(int) TrafficPriority.PrioritySegments[segmentId].Instance2.Type
				});
			} catch (Exception e) {
				Log.Error($"Error adding Priority Segments to Save {e.Message}");
			}
		}
	}
}
