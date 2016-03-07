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
using TrafficManager.Custom.AI;

namespace TrafficManager.State {
	public class SerializableDataExtension : SerializableDataExtensionBase {
		private const string DataId = "TrafficManager_v1.0";

		private static ISerializableData _serializableData;
		private static Configuration _configuration;
		public static bool StateLoading = false;

		public override void OnCreated(ISerializableData serializableData) {
			_serializableData = serializableData;
		}

		public override void OnReleased() {
		}
		
		public override void OnLoadData() {
			Log.Info("Loading Traffic Manager: PE Data");
			StateLoading = true;
			try {
				Log.Info("Initializing flags");
				Flags.OnBeforeLoadData();
				Log.Info("Initializing segment geometries");
				CustomRoadAI.OnBeforeLoadData();
				Log.Info("Initialization done. Loading mod data now.");
				byte[] data = _serializableData.LoadData(DataId);
				DeserializeData(data);

				// load options
				byte[] options = _serializableData.LoadData("TMPE_Options");
				if (options != null) {
					if (options.Length >= 1) {
						Options.setSimAccuracy(options[0]);
					}

					if (options.Length >= 2) {
						Options.setLaneChangingRandomization(options[1]);
					}

					if (options.Length >= 3) {
						Options.setRecklessDrivers(options[2]);
					}

					if (options.Length >= 4) {
						Options.setRelaxedBusses(options[3] == (byte)1);
					}

					if (options.Length >= 5) {
						Options.setNodesOverlay(options[4] == (byte)1);
					}

					if (options.Length >= 6) {
						Options.setMayEnterBlockedJunctions(options[5] == (byte)1);
					}

					if (options.Length >= 7) {
#if !TAM
						if (!LoadingExtension.IsPathManagerCompatible) {
							Options.setAdvancedAI(false);
						} else {
#endif
							Options.setAdvancedAI(options[6] == (byte)1);
#if !TAM
						}
#endif
					}

					if (options.Length >= 8) {
						Options.setHighwayRules(options[7] == (byte)1);
					}

					if (options.Length >= 9) {
						Options.setPrioritySignsOverlay(options[8] == (byte)1);
					}

					if (options.Length >= 10) {
						Options.setTimedLightsOverlay(options[9] == (byte)1);
					}

					if (options.Length >= 11) {
						Options.setSpeedLimitsOverlay(options[10] == (byte)1);
					}

					if (options.Length >= 12) {
						Options.setVehicleRestrictionsOverlay(options[11] == (byte)1);
					}

					if (options.Length >= 13) {
						Options.setStrongerRoadConditionEffects(options[12] == (byte)1);
					}

					if (options.Length >= 14) {
						Options.setAllowUTurns(options[13] == (byte)1);
					}

					if (options.Length >= 15) {
						Options.setAllowLaneChangesWhileGoingStraight(options[14] == (byte)1);
					}

					if (options.Length >= 16) {
						Options.setEnableDespawning(options[15] == (byte)1);
					}

					if (options.Length >= 17) {
						Options.setDynamicPathRecalculation(options[16] == (byte)1);
					}
				}
			} catch (Exception e) {
				Log.Error($"OnLoadData: {e.ToString()}");
            } finally {
				StateLoading = false;
			}

			Log.Info("OnLoadData completed.");

			// load toggled traffic lights
			//byte[] trafficLight = _serializableData.LoadData("TMPE_Options");
		}

		private static void DeserializeData(byte[] data) {
			try {
				if (data != null && data.Length != 0) {
					Log.Info("Loading Data from New Load Routine!");
					var memoryStream = new MemoryStream();
					memoryStream.Write(data, 0, data.Length);
					memoryStream.Position = 0;

					var binaryFormatter = new BinaryFormatter();
					_configuration = (Configuration)binaryFormatter.Deserialize(memoryStream);
				} else {
					Log.Warning("No data to deserialize!");
				}
			} catch (Exception e) {
				Log.Error($"Error deserializing data: {e.Message}");
			}
			
			LoadDataState();
			Flags.clearHighwayLaneArrows();
			Flags.applyAllFlags();
			TrafficPriority.HandleAllVehicles();
		}

		private static void LoadDataState() {
			Log.Info("Loading State from Config");
			if (_configuration == null) {
				Log.Warning("Configuration NULL, Couldn't load save data. Possibly a new game?");
				return;
			}

			// load priority segments
			if (_configuration.PrioritySegments != null) {
				Log.Info($"Loading {_configuration.PrioritySegments.Count()} priority segments");
				foreach (var segment in _configuration.PrioritySegments) {
					if (segment.Length < 3)
						continue;
#if DEBUG
					bool debug = segment[0] == 13630;
#endif

					if ((SegmentEnd.PriorityType)segment[2] == SegmentEnd.PriorityType.None) {
#if DEBUG
						if (debug)
							Log._Debug($"Loading priority segment: Not adding 'None' priority segment: {segment[1]} @ node {segment[0]}");
#endif
						continue;
					}

					if ((Singleton<NetManager>.instance.m_nodes.m_buffer[segment[0]].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
#if DEBUG
						if (debug)
							Log._Debug($"Loading priority segment: node {segment[0]} is invalid");
#endif
						continue;
					}
					if ((Singleton<NetManager>.instance.m_segments.m_buffer[segment[1]].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
#if DEBUG
						if (debug)
							Log._Debug($"Loading priority segment: segment {segment[1]} @ node {segment[0]} is invalid");
#endif
						continue;
					}
					if (TrafficPriority.IsPrioritySegment((ushort)segment[0], (ushort)segment[1])) {
#if DEBUG
						if (debug)
							Log._Debug($"Loading priority segment: segment {segment[1]} @ node {segment[0]} is already a priority segment");
#endif
						TrafficPriority.GetPrioritySegment((ushort)segment[0], (ushort)segment[1]).Type = (SegmentEnd.PriorityType)segment[2];
						continue;
					}
#if DEBUG
					Log._Debug($"Adding Priority Segment of type: {segment[2].ToString()} to segment {segment[1]} @ node {segment[0]}");
#endif
					TrafficPriority.AddPrioritySegment((ushort)segment[0], (ushort)segment[1], (SegmentEnd.PriorityType)segment[2]);
				}
			} else {
				Log.Warning("Priority segments data structure undefined!");
			}

			// load vehicle restrictions (warning: has to be done before loading timed lights!)
			if (_configuration.LaneAllowedVehicleTypes != null) {
				Log.Info($"Loading lane vehicle restriction data. {_configuration.LaneAllowedVehicleTypes.Count} elements");
				foreach (Configuration.LaneVehicleTypes laneVehicleTypes in _configuration.LaneAllowedVehicleTypes) {
					Log._Debug($"Loading lane vehicle restriction: lane {laneVehicleTypes.laneId} = {laneVehicleTypes.vehicleTypes}");
					Flags.setLaneAllowedVehicleTypes(laneVehicleTypes.laneId, laneVehicleTypes.vehicleTypes);
				}
			} else {
				Log.Warning("Lane speed limit structure undefined!");
			}

			var timedStepCount = 0;
			var timedStepSegmentCount = 0;

			NetManager netManager = Singleton<NetManager>.instance;

			if (_configuration.TimedLights != null) {
				Log.Info($"Loading {_configuration.TimedLights.Count()} timed traffic lights (new method)");

				foreach (Configuration.TimedTrafficLights cnfTimedLights in _configuration.TimedLights) {
					if ((Singleton<NetManager>.instance.m_nodes.m_buffer[cnfTimedLights.nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
						continue;
					Flags.setNodeTrafficLight(cnfTimedLights.nodeId, true);

					Log._Debug($"Adding Timed Node at node {cnfTimedLights.nodeId}");

					TrafficLightSimulation sim = TrafficLightSimulation.AddNodeToSimulation(cnfTimedLights.nodeId);
					sim.SetupTimedTrafficLight(cnfTimedLights.nodeGroup);
					var timedNode = sim.TimedLight;

					int j = 0;
					foreach (Configuration.TimedTrafficLightsStep cnfTimedStep in cnfTimedLights.timedSteps) {
						Log._Debug($"Loading timed step {j} at node {cnfTimedLights.nodeId}");
						TimedTrafficLightsStep step = timedNode.AddStep(cnfTimedStep.minTime, cnfTimedStep.maxTime, cnfTimedStep.waitFlowBalance);

						foreach (KeyValuePair<ushort, Configuration.CustomSegmentLights> e in cnfTimedStep.segmentLights) {
							Log._Debug($"Loading timed step {j}, segment {e.Key} at node {cnfTimedLights.nodeId}");
							CustomSegmentLights lights = null;
							if (!step.segmentLights.TryGetValue(e.Key, out lights)) {
								Log._Debug($"No segment lights found at timed step {j} for segment {e.Key}, node {cnfTimedLights.nodeId}");
								continue;
							}
							Configuration.CustomSegmentLights cnfLights = e.Value;

							Log._Debug($"Loading pedestrian light @ seg. {e.Key}, step {j}: {cnfLights.pedestrianLightState} {cnfLights.manualPedestrianMode}");

							lights.ManualPedestrianMode = cnfLights.manualPedestrianMode;
							lights.PedestrianLightState = cnfLights.pedestrianLightState;

							foreach (KeyValuePair<ExtVehicleType, Configuration.CustomSegmentLight> e2 in cnfLights.customLights) {
								Log._Debug($"Loading timed step {j}, segment {e.Key}, vehicleType {e2.Key} at node {cnfTimedLights.nodeId}");
								CustomSegmentLight light = null;
								if (!lights.CustomLights.TryGetValue(e2.Key, out light)) {
									Log._Debug($"No segment light found for timed step {j}, segment {e.Key}, vehicleType {e2.Key} at node {cnfTimedLights.nodeId}");
									continue;
								}
								Configuration.CustomSegmentLight cnfLight = e2.Value;

								light.CurrentMode = (CustomSegmentLight.Mode)cnfLight.currentMode;
								light.LightLeft = cnfLight.leftLight;
								light.LightMain = cnfLight.mainLight;
								light.LightRight = cnfLight.rightLight;
							}
						}
						++j;
					}

					if (cnfTimedLights.started)
						timedNode.Start();
				}
			} else if (_configuration.TimedNodes != null && _configuration.TimedNodeGroups != null) {
				Log.Info($"Loading {_configuration.TimedNodes.Count()} timed traffic lights (old method)");
				for (var i = 0; i < _configuration.TimedNodes.Count; i++) {
					try {
						var nodeid = (ushort)_configuration.TimedNodes[i][0];
						if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeid].m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
							continue;
						Flags.setNodeTrafficLight(nodeid, true);

						Log._Debug($"Adding Timed Node {i} at node {nodeid}");

						var nodeGroup = new List<ushort>();
						for (var j = 0; j < _configuration.TimedNodeGroups[i].Length; j++) {
							nodeGroup.Add(_configuration.TimedNodeGroups[i][j]);
						}

						TrafficLightSimulation sim = TrafficLightSimulation.AddNodeToSimulation(nodeid);
						sim.SetupTimedTrafficLight(nodeGroup);
						var timedNode = sim.TimedLight;

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
							//int numSegments = 0;
							float waitFlowBalance = 1f;

							if (cfgstep.Length == 2) {
								minTime = cfgstep[0];
								maxTime = cfgstep[0];
								//numSegments = cfgstep[1];
							} else if (cfgstep.Length >= 3) {
								minTime = cfgstep[0];
								maxTime = cfgstep[1];
								//numSegments = cfgstep[2];
								if (cfgstep.Length == 4) {
									waitFlowBalance = Convert.ToSingle(cfgstep[3]) / 10f;
								}
								if (cfgstep.Length == 5) {
									waitFlowBalance = Convert.ToSingle(cfgstep[4]) / 1000f;
								}
							}

							Log._Debug($"Adding timed step to node {nodeid}: min/max: {minTime}/{maxTime}, waitFlowBalance: {waitFlowBalance}");

							timedNode.AddStep(minTime, maxTime, waitFlowBalance);
							var step = timedNode.Steps[j];

							for (var s = 0; s < 8; s++) {
								var segmentId = netManager.m_nodes.m_buffer[nodeid].GetSegment(s);
								if (segmentId <= 0)
									continue;

								bool tooFewSegments = (timedStepSegmentCount >= _configuration.TimedNodeStepSegments.Count);

								var leftLightState = tooFewSegments ? RoadBaseAI.TrafficLightState.Red : (RoadBaseAI.TrafficLightState)_configuration.TimedNodeStepSegments[timedStepSegmentCount][0];
								var mainLightState = tooFewSegments ? RoadBaseAI.TrafficLightState.Red : (RoadBaseAI.TrafficLightState)_configuration.TimedNodeStepSegments[timedStepSegmentCount][1];
								var rightLightState = tooFewSegments ? RoadBaseAI.TrafficLightState.Red : (RoadBaseAI.TrafficLightState)_configuration.TimedNodeStepSegments[timedStepSegmentCount][2];
								var pedLightState = tooFewSegments ? RoadBaseAI.TrafficLightState.Red : (RoadBaseAI.TrafficLightState)_configuration.TimedNodeStepSegments[timedStepSegmentCount][3];
								CustomSegmentLight.Mode? mode = null;
								if (_configuration.TimedNodeStepSegments[timedStepSegmentCount].Length >= 5) {
									mode = (CustomSegmentLight.Mode)_configuration.TimedNodeStepSegments[timedStepSegmentCount][4];
								}

								foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in step.segmentLights[segmentId].CustomLights) {
									//ManualSegmentLight segmentLight = new ManualSegmentLight(step.NodeId, step.segmentIds[k], mainLightState, leftLightState, rightLightState, pedLightState);
									e.Value.LightLeft = leftLightState;
									e.Value.LightMain = mainLightState;
									e.Value.LightRight = rightLightState;
									if (mode != null)
										e.Value.CurrentMode = (CustomSegmentLight.Mode)mode;
								}

								if (step.segmentLights[segmentId].PedestrianLightState != null)
									step.segmentLights[segmentId].PedestrianLightState = pedLightState;

								timedStepSegmentCount++;
							}
							timedStepCount++;
						}

						if (Convert.ToBoolean(_configuration.TimedNodes[i][3])) {
							timedNode.Start();
						}
					} catch (Exception e) {
						// ignore, as it's probably corrupt save data. it'll be culled on next save
						Log.Warning("Error loading data from the TimedNodes: " + e.ToString());
					}
				}
			} else {
				Log.Warning("Timed traffic lights data structure undefined!");
			}

			var trafficLightDefs = _configuration.NodeTrafficLights.Split(',');

			Log.Info($"Loading junction traffic light data");
			if (trafficLightDefs.Length <= 1) {
				// old method
				Log.Info($"Using old method to load traffic light data");

				var saveDataIndex = 0;
				for (var i = 0; i < Singleton<NetManager>.instance.m_nodes.m_buffer.Length; i++) {
					//Log.Message($"Adding NodeTrafficLights iteration: {i1}");
					try {
						if ((Singleton<NetManager>.instance.m_nodes.m_buffer[i].Info.m_class.m_service != ItemClass.Service.Road &&
							Singleton<NetManager>.instance.m_nodes.m_buffer[i].Info.m_class.m_service != ItemClass.Service.PublicTransport) ||
							(Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
							continue;

						// prevent overflow
						if (_configuration.NodeTrafficLights.Length > saveDataIndex) {
							var trafficLight = _configuration.NodeTrafficLights[saveDataIndex];
#if DEBUG
							Log._Debug("Setting traffic light flag for node " + i + ": " + (trafficLight == '1'));
#endif
							Flags.setNodeTrafficLight((ushort)i, trafficLight == '1');
						}
						++saveDataIndex;
					} catch (Exception e) {
						// ignore as it's probably bad save data.
						Log.Warning("Error setting the NodeTrafficLights (old): " + e.Message);
					}
				}
			} else {
				// new method
				foreach (var split in trafficLightDefs.Select(def => def.Split(':')).Where(split => split.Length > 1)) {
					try {
						Log.Info($"Traffic light split data: {split[0]} , {split[1]}");
						var nodeId = Convert.ToUInt16(split[0]);
						uint flag = Convert.ToUInt16(split[1]);

						Flags.setNodeTrafficLight(nodeId, flag > 0);
					} catch (Exception e) {
						// ignore as it's probably bad save data.
						Log.Warning("Error setting the NodeTrafficLights (new): " + e.Message);
					}
				}
			}

			if (_configuration.LaneFlags != null) {
				Log.Info($"Loading lane arrow data");
#if DEBUG
				Log._Debug($"LaneFlags: {_configuration.LaneFlags}");
#endif
				var lanes = _configuration.LaneFlags.Split(',');

				if (lanes.Length > 1) {
					foreach (var split in lanes.Select(lane => lane.Split(':')).Where(split => split.Length > 1)) {
						try {
							Log.Info($"Split Data: {split[0]} , {split[1]}");
							var laneId = Convert.ToUInt32(split[0]);
							uint flags = Convert.ToUInt32(split[1]);

							//make sure we don't cause any overflows because of bad save data.
							if (Singleton<NetManager>.instance.m_lanes.m_buffer.Length <= laneId)
								continue;

							if (flags > ushort.MaxValue)
								continue;

							if ((Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags & (ushort)NetLane.Flags.Created) == 0 || Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment == 0)
								continue;

							Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags = fixLaneFlags(Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags);

							uint laneArrowFlags = flags & Flags.lfr;
							uint origFlags = (Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags & Flags.lfr);
#if DEBUG
							Log._Debug("Setting flags for lane " + laneId + " to " + flags + " (" + ((Flags.LaneArrows)(laneArrowFlags)).ToString() + ")");
							if ((origFlags | laneArrowFlags) == origFlags) { // only load if setting differs from default
								Log._Debug("Flags for lane " + laneId + " are original (" + ((NetLane.Flags)(origFlags)).ToString() + ")");
							}
#endif
							Flags.setLaneArrowFlags(laneId, (Flags.LaneArrows)(laneArrowFlags));
						} catch (Exception e) {
							Log.Error($"Error loading Lane Split data. Length: {split.Length} value: {split}\nError: {e.Message}");
						}
					}
				}
			} else {
				Log.Warning("Lane arrow data structure undefined!");
			}

			// load speed limits
			if (_configuration.LaneSpeedLimits != null) {
				Log.Info($"Loading lane speed limit data. {_configuration.LaneSpeedLimits.Count} elements");
				foreach (Configuration.LaneSpeedLimit laneSpeedLimit in _configuration.LaneSpeedLimits) {
					Log._Debug($"Loading lane speed limit: lane {laneSpeedLimit.laneId} = {laneSpeedLimit.speedLimit}");
                    Flags.setLaneSpeedLimit(laneSpeedLimit.laneId, laneSpeedLimit.speedLimit);
				}
			} else {
				Log.Warning("Lane speed limit structure undefined!");
			}

			// Load segment-at-node flags
			if (_configuration.SegmentNodeConfs != null) {
				Log.Info($"Loading segment-at-node data. {_configuration.SegmentNodeConfs.Count} elements");
				foreach (Configuration.SegmentNodeConf segNodeConf in _configuration.SegmentNodeConfs) {
					if ((Singleton<NetManager>.instance.m_segments.m_buffer[segNodeConf.segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
						continue;
					Flags.setSegmentNodeFlags(segNodeConf.segmentId, true, segNodeConf.startNodeFlags);
					Flags.setSegmentNodeFlags(segNodeConf.segmentId, false, segNodeConf.endNodeFlags);
				}
			} else {
				Log.Warning("Segment-at-node structure undefined!");
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
			Log.Info("Saving Mod Data.");
			var configuration = new Configuration();

			if (TrafficPriority.PrioritySegments != null) {
				for (ushort i = 0; i < Singleton<NetManager>.instance.m_segments.m_size; i++) {
					SavePrioritySegment(i, configuration);
					SaveSegmentNodeFlags(i, configuration);
				}
			}

			for (ushort i = 0; i < Singleton<NetManager>.instance.m_nodes.m_size; i++) {
				/*if (TrafficLightSimulation.LightSimulationByNodeId != null) {
					SaveTrafficLightSimulation(i, configuration);
				}*/

				/*if (TrafficLightsManual.ManualSegments != null) {
					SaveManualTrafficLight(i, configuration);
				}*/

				TrafficLightSimulation sim = TrafficLightSimulation.GetNodeSimulation(i);
				if (sim != null && sim.IsTimedLight()) {
					SaveTimedTrafficLight(i, configuration);
					// TODO save new traffic lights
				}

				SaveNodeLights(i, configuration);
			}

#if !TAM
			if (LoadingExtension.IsPathManagerCompatible) {
#endif
				for (uint i = 0; i < Singleton<NetManager>.instance.m_lanes.m_buffer.Length; i++) {
					SaveLaneData(i, configuration);
				}
#if !TAM
			}
#endif

			foreach (KeyValuePair<uint, ushort> e in Flags.getAllLaneSpeedLimits()) {
				SaveLaneSpeedLimit(new Configuration.LaneSpeedLimit(e.Key, e.Value), configuration);
			}

			foreach (KeyValuePair<uint, ExtVehicleType> e in Flags.getAllLaneAllowedVehicleTypes()) {
				SaveLaneAllowedVehicleTypes(new Configuration.LaneVehicleTypes(e.Key, e.Value), configuration);
			}

			var binaryFormatter = new BinaryFormatter();
			var memoryStream = new MemoryStream();

			try {
				binaryFormatter.Serialize(memoryStream, configuration);
				memoryStream.Position = 0;
				Log.Info($"Save data byte length {memoryStream.Length}");
				_serializableData.SaveData(DataId, memoryStream.ToArray());

				// save options
				_serializableData.SaveData("TMPE_Options", new byte[] {
					(byte)Options.simAccuracy,
					(byte)Options.laneChangingRandomization,
					(byte)Options.recklessDrivers,
					(byte)(Options.relaxedBusses ? 1 : 0),
					(byte) (Options.nodesOverlay ? 1 : 0),
					(byte)(Options.allowEnterBlockedJunctions ? 1 : 0),
					(byte)(Options.advancedAI ? 1 : 0),
					(byte)(Options.highwayRules ? 1 : 0),
					(byte)(Options.prioritySignsOverlay ? 1 : 0),
					(byte)(Options.timedLightsOverlay ? 1 : 0),
					(byte)(Options.speedLimitsOverlay ? 1 : 0),
					(byte)(Options.vehicleRestrictionsOverlay ? 1 : 0),
					(byte)(Options.strongerRoadConditionEffects ? 1 : 0),
					(byte)(Options.allowUTurns ? 1 : 0),
					(byte)(Options.allowLaneChangesWhileGoingStraight ? 1 : 0),
					(byte)(Options.enableDespawning ? 1 : 0),
					(byte)(Options.dynamicPathRecalculation ? 1 : 0)
				});
			} catch (Exception ex) {
				Log.Error("Unexpected error saving data: " + ex.Message);
			} finally {
				memoryStream.Close();
			}
		}

		private static void SaveLaneData(uint i, Configuration configuration) {
			try {
				//NetLane.Flags flags = (NetLane.Flags)lane.m_flags;
				/*if ((flags & NetLane.Flags.LeftForwardRight) == NetLane.Flags.None) // only save lanes with explicit lane arrows
					return;*/
				var laneSegmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[i].m_segment;
				if (laneSegmentId <= 0)
					return;
				if ((Singleton<NetManager>.instance.m_lanes.m_buffer[i].m_flags & (ushort)NetLane.Flags.Created) == 0 || laneSegmentId == 0)
					return;
				if ((Singleton<NetManager>.instance.m_segments.m_buffer[laneSegmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
					return;

				//if (TrafficPriority.PrioritySegments.ContainsKey(laneSegmentId)) {
				Flags.LaneArrows? laneArrows = Flags.getLaneArrowFlags(i);
				if (laneArrows != null) {
					uint laneArrowInt = (uint)laneArrows;
					Log._Debug($"Saving lane data for lane {i}, segment {laneSegmentId}, setting to {laneArrows.ToString()} ({laneArrowInt})");
                    configuration.LaneFlags += $"{i}:{laneArrowInt},";
				}
				//}
			} catch (Exception e) {
				Log.Error($"Error saving NodeLaneData {e.Message}");
			}
		}

		private static void SaveNodeLights(int i, Configuration configuration) {
			try {
				if (!Flags.mayHaveTrafficLight((ushort)i))
					return;

				bool? hasTrafficLight = Flags.isNodeTrafficLight((ushort)i);
				if (hasTrafficLight == null)
					return;

				if ((bool)hasTrafficLight) {
					Log.Info($"Saving that node {i} has a traffic light");
				} else {
					Log.Info($"Saving that node {i} does not have a traffic light");
				}
				configuration.NodeTrafficLights += $"{i}:{Convert.ToUInt16((bool)hasTrafficLight)},";
				return;
			} catch (Exception e) {
				Log.Error($"Error Adding Node Lights and Crosswalks {e.Message}");
				return;
			}
		}

		private static void SaveTimedTrafficLight(ushort i, Configuration configuration) {
			try {
				TrafficLightSimulation sim = TrafficLightSimulation.GetNodeSimulation(i);
				if (sim == null || !sim.IsTimedLight())
					return;

				Log._Debug($"Going to save timed light at node {i}.");

				var timedNode = sim.TimedLight;
				timedNode.handleNewSegments();

				Configuration.TimedTrafficLights cnfTimedLights = new Configuration.TimedTrafficLights();
				configuration.TimedLights.Add(cnfTimedLights);

				cnfTimedLights.nodeId = timedNode.NodeId;
				cnfTimedLights.nodeGroup = timedNode.NodeGroup;
				cnfTimedLights.started = timedNode.IsStarted();
				cnfTimedLights.timedSteps = new List<Configuration.TimedTrafficLightsStep>();

				for (var j = 0; j < timedNode.NumSteps(); j++) {
					Log._Debug($"Saving timed light step {j} at node {i}.");
					TimedTrafficLightsStep timedStep = timedNode.Steps[j];
					Configuration.TimedTrafficLightsStep cnfTimedStep = new Configuration.TimedTrafficLightsStep();
					cnfTimedLights.timedSteps.Add(cnfTimedStep);

					cnfTimedStep.minTime = timedStep.minTime;
					cnfTimedStep.maxTime = timedStep.maxTime;
					cnfTimedStep.waitFlowBalance = timedStep.waitFlowBalance;
					cnfTimedStep.segmentLights = new Dictionary<ushort, Configuration.CustomSegmentLights>();
					foreach (KeyValuePair<ushort, CustomSegmentLights> e in timedStep.segmentLights) {
						Log._Debug($"Saving timed light step {j}, segment {e.Key} at node {i}.");

						CustomSegmentLights segLights = e.Value;
						Configuration.CustomSegmentLights cnfSegLights = new Configuration.CustomSegmentLights();
						cnfTimedStep.segmentLights.Add(e.Key, cnfSegLights);

						cnfSegLights.nodeId = segLights.NodeId;
						cnfSegLights.segmentId = segLights.SegmentId;
						cnfSegLights.customLights = new Dictionary<ExtVehicleType, Configuration.CustomSegmentLight>();
						cnfSegLights.pedestrianLightState = segLights.PedestrianLightState;
						cnfSegLights.manualPedestrianMode = segLights.ManualPedestrianMode;

						Log._Debug($"Saving pedestrian light @ seg. {e.Key}, step {j}: {cnfSegLights.pedestrianLightState} {cnfSegLights.manualPedestrianMode}");

						foreach (KeyValuePair<Traffic.ExtVehicleType, CustomSegmentLight> e2 in segLights.CustomLights) {
							Log._Debug($"Saving timed light step {j}, segment {e.Key}, vehicleType {e2.Key} at node {i}.");

							CustomSegmentLight segLight = e2.Value;
							Configuration.CustomSegmentLight cnfSegLight = new Configuration.CustomSegmentLight();
							cnfSegLights.customLights.Add(e2.Key, cnfSegLight);

							cnfSegLight.nodeId = segLight.NodeId;
							cnfSegLight.segmentId = segLight.SegmentId;
							cnfSegLight.currentMode = (int)segLight.CurrentMode;
							cnfSegLight.leftLight = segLight.LightLeft;
							cnfSegLight.mainLight = segLight.LightMain;
							cnfSegLight.rightLight = segLight.LightRight;
						}
					}
				}
			} catch (Exception e) {
				Log.Error($"Error adding TimedTrafficLights to save {e.Message}");
			}
		}

		private static void SaveLaneSpeedLimit(Configuration.LaneSpeedLimit laneSpeedLimit, Configuration configuration) {
			Log._Debug($"Saving speed limit of lane {laneSpeedLimit.laneId}: {laneSpeedLimit.speedLimit}");
			configuration.LaneSpeedLimits.Add(laneSpeedLimit);
		}

		private void SaveLaneAllowedVehicleTypes(Configuration.LaneVehicleTypes laneVehicleTypes, Configuration configuration) {
			Log._Debug($"Saving vehicle restrictions of lane {laneVehicleTypes.laneId}: {laneVehicleTypes.vehicleTypes}");
			configuration.LaneAllowedVehicleTypes.Add(laneVehicleTypes);
		}

		private static void SavePrioritySegment(ushort segmentId, Configuration configuration) {
			try {
				if (TrafficPriority.PrioritySegments[segmentId] == null) {
					return;
				}

				if (TrafficPriority.PrioritySegments[segmentId].Node1 != 0 && TrafficPriority.PrioritySegments[segmentId].Instance1.Type != SegmentEnd.PriorityType.None) {
					Log.Info($"Saving Priority Segment of type: {TrafficPriority.PrioritySegments[segmentId].Instance1.Type} @ node {TrafficPriority.PrioritySegments[segmentId].Node1}, seg. {segmentId}");
                    configuration.PrioritySegments.Add(new[]
					{
						TrafficPriority.PrioritySegments[segmentId].Node1, segmentId,
						(int) TrafficPriority.PrioritySegments[segmentId].Instance1.Type
					});
				}

				if (TrafficPriority.PrioritySegments[segmentId].Node2 == 0 || TrafficPriority.PrioritySegments[segmentId].Instance2.Type == SegmentEnd.PriorityType.None)
					return;

				Log.Info($"Saving Priority Segment of type: {TrafficPriority.PrioritySegments[segmentId].Instance2.Type} @ node {TrafficPriority.PrioritySegments[segmentId].Node2}, seg. {segmentId}");
				configuration.PrioritySegments.Add(new[] {
					TrafficPriority.PrioritySegments[segmentId].Node2, segmentId,
					(int) TrafficPriority.PrioritySegments[segmentId].Instance2.Type
				});
			} catch (Exception e) {
				Log.Error($"Error adding Priority Segments to Save: {e.ToString()}");
			}
		}

		private static void SaveSegmentNodeFlags(ushort segmentId, Configuration configuration) {
			try {
				if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
					return;

				Configuration.SegmentNodeFlags startNodeFlags = Flags.getSegmentNodeFlags(segmentId, true);
				Configuration.SegmentNodeFlags endNodeFlags = Flags.getSegmentNodeFlags(segmentId, false);

				if (startNodeFlags == null && endNodeFlags == null)
					return;

				Configuration.SegmentNodeConf conf = new Configuration.SegmentNodeConf(segmentId);

				conf.startNodeFlags = startNodeFlags;
				conf.endNodeFlags = endNodeFlags;

				Log.Info($"Saving segment-at-node flags for seg. {segmentId}");
				configuration.SegmentNodeConfs.Add(conf);
			} catch (Exception e) {
				Log.Error($"Error adding Priority Segments to Save: {e.ToString()}");
			}
		}
	}
}
