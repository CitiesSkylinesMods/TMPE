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
using TrafficManager.State;

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

			// load options
			byte[] options = _serializableData.LoadData("TMPE_Options");
			if (options != null) {
				if (options.Length >= 1) {
					Options.setSimAccuracy(options[0]);
				}
			}
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
				Log.Message($"Adding Priority Segment of type: {segment[2].ToString()} to segment {segment[1]} @ node {segment[0]}");
				TrafficPriority.AddPrioritySegment((ushort)segment[0], (ushort)segment[1], (PrioritySegment.PriorityType)segment[2]);
			}

			foreach (var node in _configuration.NodeDictionary) {
				if (node.Length < 4)
					continue;
				if (TrafficLightSimulation.GetNodeSimulation((ushort)node[0]) != null)
					continue;

				Log.Message($"Adding node simulation {node[0]}");
				try {
					TrafficLightSimulation.AddNodeToSimulation((ushort)node[0]);
					var nodeDict = TrafficLightSimulation.GetNodeSimulation((ushort)node[0]);

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
						if (Singleton<NetManager>.instance.m_nodes.m_buffer[i].Info.m_class.m_service != ItemClass.Service.Road ||
							Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags == 0)
							continue;

						// prevent overflow
						if (_configuration.NodeTrafficLights.Length > saveDataIndex) {

							var trafficLight = _configuration.NodeTrafficLights[saveDataIndex];
							Flags.setNodeTrafficLight((ushort)i, trafficLight == '1');
						}

						if (_configuration.NodeCrosswalk.Length > saveDataIndex) {
							var crossWalk = _configuration.NodeCrosswalk[saveDataIndex];
							Flags.setNodeCrossingFlag((ushort)i, crossWalk == '1');
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
					var laneIndex = Convert.ToUInt32(split[0]);
					uint flags = Convert.ToUInt32(split[1]);

					//make sure we don't cause any overflows because of bad save data.
					if (Singleton<NetManager>.instance.m_lanes.m_buffer.Length <= laneIndex)
						continue;

					if (flags > ushort.MaxValue)
						continue;

					Singleton<NetManager>.instance.m_lanes.m_buffer[laneIndex].m_flags = fixLaneFlags(Singleton<NetManager>.instance.m_lanes.m_buffer[laneIndex].m_flags);

					uint laneArrowFlags = flags & Flags.lfr;
					uint origFlags = (Singleton<NetManager>.instance.m_lanes.m_buffer[laneIndex].m_flags & Flags.lfr);
					Log.Message("Setting flags for lane " + laneIndex + " to " + flags + " (" + ((Flags.LaneArrows)(laneArrowFlags)).ToString() + ")");
					if ((origFlags | laneArrowFlags) == origFlags) { // only load if setting differs from default
						Log.Message("Flags for lane " + laneIndex + " are original (" + ((NetLane.Flags)(origFlags)).ToString() + ")");
					}
					Flags.setLaneArrowFlags(laneIndex, (Flags.LaneArrows)(laneArrowFlags));
				} catch (Exception e) {
					Log.Error(
						$"Error loading Lane Split data. Length: {split.Length} value: {split}\nError: {e.Message}");
				}
			}
		}

		private static ushort fixLaneFlags(ushort flags) {
			ushort ret = 0;
			if ((flags & (ushort)NetLane.Flags.Created) != 0)
				ret |= (ushort)NetLane.Flags.Created;
			if ((flags & (ushort)NetLane.Flags.Deleted) != 0)
				ret |= (ushort)NetLane.Flags.Deleted;
			if ((flags & (ushort)NetLane.Flags.Inverted) != 0)
				ret |= (ushort)NetLane.Flags.Inverted;
			if ((flags & (ushort)NetLane.Flags.JoinedJunction) != 0)
				ret |= (ushort)NetLane.Flags.JoinedJunction;
			if ((flags & (ushort)NetLane.Flags.Forward) != 0)
				ret |= (ushort)NetLane.Flags.Forward;
			if ((flags & (ushort)NetLane.Flags.Left) != 0)
				ret |= (ushort)NetLane.Flags.Left;
			if ((flags & (ushort)NetLane.Flags.Right) != 0)
				ret |= (ushort)NetLane.Flags.Right;
			if ((flags & (ushort)NetLane.Flags.Stop) != 0)
				ret |= (ushort)NetLane.Flags.Stop;
			if ((flags & (ushort)NetLane.Flags.StartOneWayLeft) != 0)
				ret |= (ushort)NetLane.Flags.StartOneWayLeft;
			if ((flags & (ushort)NetLane.Flags.StartOneWayRight) != 0)
				ret |= (ushort)NetLane.Flags.StartOneWayRight;
			if ((flags & (ushort)NetLane.Flags.EndOneWayLeft) != 0)
				ret |= (ushort)NetLane.Flags.EndOneWayLeft;
			if ((flags & (ushort)NetLane.Flags.EndOneWayRight) != 0)
				ret |= (ushort)NetLane.Flags.EndOneWayRight;
			return ret;
		}

		public override void OnSaveData() {
			Log.Warning("Saving Mod Data.");
			var configuration = new Configuration();

			for (ushort i = 0; i < 36864; i++) {
				if (TrafficPriority.PrioritySegments != null) {
					SavePrioritySegment(i, configuration);
				}

				if (TrafficLightSimulation.LightSimulationByNodeId != null) {
					SaveTrafficLightSimulation(i, configuration);
				}

				if (TrafficLightsManual.ManualSegments != null) {
					SaveManualTrafficLight(i, configuration);
				}

				if (TrafficLightsTimed.TimedScripts != null) {
					SaveTimedTrafficLight(i, configuration);
				}
			}

			if (Singleton<NetManager>.instance?.m_nodes?.m_buffer != null) {
				for (var i = 0; i < Singleton<NetManager>.instance.m_nodes.m_buffer.Length; i++) {
					SaveNodeLightsAndCrosswalks(i, configuration);
				}
			}

			if (LoadingExtension.IsPathManagerCompatible && Singleton<NetManager>.instance?.m_lanes?.m_buffer != null) {
				for (uint i = 0; i < Singleton<NetManager>.instance.m_lanes.m_buffer.Length; i++) {
					SaveLaneData(i, configuration);
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

				// save options
				_serializableData.SaveData("TMPE_Options", new byte[] { (byte)Options.simAccuracy });
			} catch (Exception ex) {
				Log.Error("Unexpected error saving data: " + ex.Message);
			} finally {
				memoryStream.Close();
			}
		}

		private static void SaveLaneData(uint i, Configuration configuration) {
			try {
				NetLane lane = Singleton<NetManager>.instance.m_lanes.m_buffer[i];
				//NetLane.Flags flags = (NetLane.Flags)lane.m_flags;
				/*if ((flags & NetLane.Flags.LeftForwardRight) == NetLane.Flags.None) // only save lanes with explicit lane arrows
					return;*/
				var laneSegmentId = lane.m_segment;
				if (laneSegmentId <= 0)
					return;
				NetSegment segment = Singleton<NetManager>.instance.m_segments.m_buffer[laneSegmentId];
				if (segment.m_flags == NetSegment.Flags.None)
					return;

				//if (TrafficPriority.PrioritySegments.ContainsKey(laneSegmentId)) {
				Flags.LaneArrows? laneArrows = Flags.getLaneArrowFlags(i);
				if (laneArrows != null) {
					uint laneArrowInt = (uint)laneArrows;
					Log.Message($"Saving lane data for lane {i}, segment {laneSegmentId}, setting to {laneArrows.ToString()} ({laneArrowInt})");
                    configuration.LaneFlags += $"{i}:{laneArrowInt},";
				}
				//}
			} catch (Exception e) {
				Log.Error($"Error saving NodeLaneData {e.Message}");
			}
		}

		private static bool SaveNodeLightsAndCrosswalks(int i, Configuration configuration) {
			try {
				var nodeFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags;

				if (nodeFlags == 0)
					return true;
				if (Singleton<NetManager>.instance.m_nodes.m_buffer[i].Info.m_class.m_service != ItemClass.Service.Road)
					return true;
				bool hasTrafficLight = (nodeFlags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
				if (hasTrafficLight) {
					Log.Message($"Saving that node {i} has a traffic light");
				} else {
					Log.Message($"Saving that node {i} does not have a traffic light");
				}
				configuration.NodeTrafficLights += Convert.ToInt16(hasTrafficLight);
				configuration.NodeCrosswalk += Convert.ToInt16((nodeFlags & NetNode.Flags.Junction) != NetNode.Flags.None);
				return false;
			} catch (Exception e) {
				Log.Error($"Error Adding Node Lights and Crosswalks {e.Message}");
				return true;
			}
		}

		private static void SaveTimedTrafficLight(int i, Configuration configuration) {
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

		private static void SaveManualTrafficLight(ushort segmentId, Configuration configuration) {
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

		private static void SaveTrafficLightSimulation(int i, Configuration configuration) {
			try {
				if (!TrafficLightSimulation.LightSimulationByNodeId.ContainsKey((ushort)i))
					return;
				var nodeSim = TrafficLightSimulation.LightSimulationByNodeId[(ushort)i];

				/*if (nodeSim == null)
					return;*/

				Log.Message($"Saving traffic light simulation at node {i}, timed: {nodeSim.TimedTrafficLights}, active: {nodeSim.TimedTrafficLightsActive}");

				configuration.NodeDictionary.Add(new[]
				{
					nodeSim.nodeId, Convert.ToInt32(nodeSim.ManualTrafficLights),
					Convert.ToInt32(nodeSim.TimedTrafficLights),
					Convert.ToInt32(nodeSim.TimedTrafficLightsActive)
				});
			} catch (Exception e) {
				Log.Error($"Error adding Nodes to Dictionary {e.Message}");
			}
		}

		private static void SavePrioritySegment(ushort segmentId, Configuration configuration) {
			try {
				if (!TrafficPriority.PrioritySegments.ContainsKey(segmentId))
					return;

				if (TrafficPriority.PrioritySegments[segmentId].Node1 != 0) {
					Log.Message($"Saving Priority Segment of type: {TrafficPriority.PrioritySegments[segmentId].Instance1.Type} @ node {TrafficPriority.PrioritySegments[segmentId].Node1}, seg. {segmentId}");
                    configuration.PrioritySegments.Add(new[]
					{
						TrafficPriority.PrioritySegments[segmentId].Node1, segmentId,
						(int) TrafficPriority.PrioritySegments[segmentId].Instance1.Type
					});
				}

				if (TrafficPriority.PrioritySegments[segmentId].Node2 == 0)
					return;

				Log.Message($"Saving Priority Segment of type: {TrafficPriority.PrioritySegments[segmentId].Instance2.Type} @ node {TrafficPriority.PrioritySegments[segmentId].Node2}, seg. {segmentId}");
				configuration.PrioritySegments.Add(new[] {
					TrafficPriority.PrioritySegments[segmentId].Node2, segmentId,
					(int) TrafficPriority.PrioritySegments[segmentId].Instance2.Type
				});
			} catch (Exception e) {
				Log.Error($"Error adding Priority Segments to Save {e.Message}");
			}
		}
	}
}
