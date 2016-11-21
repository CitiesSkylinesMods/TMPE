using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using ColossalFramework;
using ICities;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using UnityEngine;
using Random = UnityEngine.Random;
using Timer = System.Timers.Timer;
using TrafficManager.State;
using TrafficManager.Custom.AI;
using TrafficManager.UI;
using TrafficManager.Manager;
using TrafficManager.Traffic;
using ColossalFramework.UI;
using TrafficManager.Util;
using System.Linq;

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
			bool loadingSucceeded = true;
			try {
				Log.Info("Initializing flags");
				Flags.OnBeforeLoadData();
			} catch (Exception e) {
				Log.Error($"OnLoadData: Error while initializing Flags: {e.ToString()}");
				loadingSucceeded = false;
			}

			try {
				Log.Info("Initializing node geometries");
				NodeGeometry.OnBeforeLoadData();
			} catch (Exception e) {
				Log.Error($"OnLoadData: Error while initializing NodeGeometry: {e.ToString()}");
				loadingSucceeded = false;
			}
			
			try {
				Log.Info("Initializing segment geometries");
				SegmentGeometry.OnBeforeLoadData();
			} catch (Exception e) {
				Log.Error($"OnLoadData: Error while initializing SegmentGeometry: {e.ToString()}");
				loadingSucceeded = false;
			}

			try {
				Log.Info("Initializing lane connection manager");
				LaneConnectionManager.Instance().OnBeforeLoadData(); // requires segment geometries
			} catch (Exception e) {
				Log.Error($"OnLoadData: Error while initializing LaneConnectionManager: {e.ToString()}");
				loadingSucceeded = false;
			}

			try {
				Log.Info("Initializing CitizenAI");
				CustomCitizenAI.OnBeforeLoadData();
			} catch (Exception e) {
				Log.Error($"OnLoadData: Error while initializing CitizenAI: {e.ToString()}");
				loadingSucceeded = false;
			}

			try {
				Log.Info("Initializing CustomRoadAI");
				CustomRoadAI.OnBeforeLoadData();
			} catch (Exception e) {
				Log.Error($"OnLoadData: Error while initializing CustomRoadAI: {e.ToString()}");
				loadingSucceeded = false;
			}

			try {
				Log.Info("Initializing SpeedLimitManager");
				SpeedLimitManager.Instance().OnBeforeLoadData();
			} catch (Exception e) {
				Log.Error($"OnLoadData: Error while initializing SpeedLimitManager: {e.ToString()}");
				loadingSucceeded = false;
			}

			Log.Info("Initialization done. Loading mod data now.");

			try {
				byte[] data = _serializableData.LoadData(DataId);
				DeserializeData(data);
			} catch (Exception e) {
				Log.Error($"OnLoadData: Error while deserializing data: {e.ToString()}");
				loadingSucceeded = false;
			}

			// load options
			try {
				byte[] options = _serializableData.LoadData("TMPE_Options");
				if (options != null) {
					if (options.Length >= 1) {
						Options.setSimAccuracy(options[0]);
					}

					if (options.Length >= 2) {
						//Options.setLaneChangingRandomization(options[1]);
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

					if (options.Length >= 18) {
						Options.setConnectedLanesOverlay(options[17] == (byte)1);
					}

					if (options.Length >= 19) {
						Options.setPrioritySignsEnabled(options[18] == (byte)1);
					}

					if (options.Length >= 20) {
						Options.setTimedLightsEnabled(options[19] == (byte)1);
					}

					if (options.Length >= 21) {
						Options.setCustomSpeedLimitsEnabled(options[20] == (byte)1);
					}

					if (options.Length >= 22) {
						Options.setVehicleRestrictionsEnabled(options[21] == (byte)1);
					}

					if (options.Length >= 23) {
						Options.setLaneConnectorEnabled(options[22] == (byte)1);
					}

					if (options.Length >= 24) {
						Options.setJunctionRestrictionsOverlay(options[23] == (byte)1);
					}

					if (options.Length >= 25) {
						Options.setJunctionRestrictionsEnabled(options[24] == (byte)1);
					}

					if (options.Length >= 26) {
						Options.setProhibitPocketCars(options[25] == (byte)1);
					}

					if (options.Length >= 27) {
						Options.setPreferOuterLane(options[26] == (byte)1);
					}
				}
			} catch (Exception e) {
				Log.Error($"OnLoadData: Error while loading options: {e.ToString()}");
				loadingSucceeded = false;
			}

			if (loadingSucceeded)
				Log.Info("OnLoadData completed successfully.");
			else {
				Log.Info("An error occurred while loading.");
				//UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("An error occurred while loading", "Traffic Manager: President Edition detected an error while loading. Please do NOT save this game under the old filename, otherwise your timed traffic lights, custom lane arrows, etc. are in danger. Instead, please navigate to http://steamcommunity.com/sharedfiles/filedetails/?id=583429740 and follow the steps under 'In case problems arise'.", true);
			}
			StateLoading = false;
		}

		private static void DeserializeData(byte[] data) {
			bool error = false;
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
				Log.Error($"Error deserializing data: {e.ToString()}");
				error = true;
			}

			if (!error) {
				LoadDataState(out error);
			}

			try {
				Flags.clearHighwayLaneArrows();
			} catch (Exception e) {
				Log.Error($"Error while clearing highway lane arrows: {e.ToString()}");
			}

			try {
				Flags.applyAllFlags();
			} catch (Exception e) {
				Log.Error($"Error while applying all flags: {e.ToString()}");
			}

			try {
				VehicleStateManager.Instance().InitAllVehicles();
			} catch (Exception e) {
				Log.Error($"Error while initializing all vehicles: {e.ToString()}");
			}

			if (error) {
				throw new ApplicationException("An error occurred while loading");
			}
		}

		private static void LoadDataState(out bool error) {
			error = false;

			Log.Info("Loading State from Config");
			if (_configuration == null) {
				Log.Warning("Configuration NULL, Couldn't load save data. Possibly a new game?");
				return;
			}

			TrafficPriorityManager prioMan = TrafficPriorityManager.Instance();

			// load priority segments
			if (_configuration.PrioritySegments != null) {
				Log.Info($"Loading {_configuration.PrioritySegments.Count} priority segments");
				foreach (var segment in _configuration.PrioritySegments) {
					try {
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

						if (! NetUtil.IsNodeValid((ushort)segment[0])) {
#if DEBUG
							if (debug)
								Log._Debug($"Loading priority segment: node {segment[0]} is invalid");
#endif
							continue;
						}
						if (! NetUtil.IsSegmentValid((ushort)segment[1])) {
#if DEBUG
							if (debug)
								Log._Debug($"Loading priority segment: segment {segment[1]} @ node {segment[0]} is invalid");
#endif
							continue;
						}
						if (prioMan.IsPrioritySegment((ushort)segment[0], (ushort)segment[1])) {
#if DEBUG
							if (debug)
								Log._Debug($"Loading priority segment: segment {segment[1]} @ node {segment[0]} is already a priority segment");
#endif
							prioMan.GetPrioritySegment((ushort)segment[0], (ushort)segment[1]).Type = (SegmentEnd.PriorityType)segment[2];
							continue;
						}
#if DEBUG
						Log._Debug($"Adding Priority Segment of type: {segment[2].ToString()} to segment {segment[1]} @ node {segment[0]}");
#endif
						prioMan.AddPrioritySegment((ushort)segment[0], (ushort)segment[1], (SegmentEnd.PriorityType)segment[2]);
					} catch (Exception e) {
						// ignore, as it's probably corrupt save data. it'll be culled on next save
						Log.Warning("Error loading data from Priority segments: " + e.ToString());
						error = true;
					}
				}
			} else {
				Log.Warning("Priority segments data structure undefined!");
			}

			// load vehicle restrictions (warning: has to be done before loading timed lights!)
			if (_configuration.LaneAllowedVehicleTypes != null) {
				Log.Info($"Loading lane vehicle restriction data. {_configuration.LaneAllowedVehicleTypes.Count} elements");
				foreach (Configuration.LaneVehicleTypes laneVehicleTypes in _configuration.LaneAllowedVehicleTypes) {
					try {
						if (!NetUtil.IsLaneValid(laneVehicleTypes.laneId))
							continue;

						ExtVehicleType baseMask = VehicleRestrictionsManager.Instance().GetBaseMask(laneVehicleTypes.laneId);
						ExtVehicleType maskedType = laneVehicleTypes.vehicleTypes & baseMask;
						Log._Debug($"Loading lane vehicle restriction: lane {laneVehicleTypes.laneId} = {laneVehicleTypes.vehicleTypes}, masked = {maskedType}");
						if (maskedType != baseMask) {
							Flags.setLaneAllowedVehicleTypes(laneVehicleTypes.laneId, maskedType);
						} else {
							Log._Debug($"Masked type does not differ from base type. Ignoring.");
						}
					} catch (Exception e) {
						// ignore, as it's probably corrupt save data. it'll be culled on next save
						Log.Warning("Error loading data from vehicle restrictions: " + e.ToString());
						error = true;
					}
				}
			} else {
				Log.Warning("Vehicle restrctions structure undefined!");
			}

			NetManager netManager = Singleton<NetManager>.instance;
			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance();

			if (_configuration.TimedLights != null) {
				Log.Info($"Loading {_configuration.TimedLights.Count} timed traffic lights (new method)");

				foreach (Configuration.TimedTrafficLights cnfTimedLights in _configuration.TimedLights) {
					try {
						if (! NetUtil.IsNodeValid(cnfTimedLights.nodeId))
							continue;
						Flags.setNodeTrafficLight(cnfTimedLights.nodeId, true);

						Log._Debug($"Adding Timed Node at node {cnfTimedLights.nodeId}");

						TrafficLightSimulation sim = tlsMan.AddNodeToSimulation(cnfTimedLights.nodeId);
						sim.SetupTimedTrafficLight(cnfTimedLights.nodeGroup);
						var timedNode = sim.TimedLight;

						int j = 0;
						foreach (Configuration.TimedTrafficLightsStep cnfTimedStep in cnfTimedLights.timedSteps) {
							Log._Debug($"Loading timed step {j} at node {cnfTimedLights.nodeId}");
							TimedTrafficLightsStep step = timedNode.AddStep(cnfTimedStep.minTime, cnfTimedStep.maxTime, cnfTimedStep.waitFlowBalance);

							foreach (KeyValuePair<ushort, Configuration.CustomSegmentLights> e in cnfTimedStep.segmentLights) {
								if (!NetUtil.IsSegmentValid(e.Key))
									continue;

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
					} catch (Exception e) {
						// ignore, as it's probably corrupt save data. it'll be culled on next save
						Log.Warning("Error loading data from TimedNode (new method): " + e.ToString());
						error = true;
					}
				}
			} else {
				Log.Warning("Timed traffic lights data structure undefined!");
			}

			if (_configuration.NodeTrafficLights != null) {
				var trafficLightDefs = _configuration.NodeTrafficLights.Split(',');

				Log.Info($"Loading junction traffic light data");
	
				// new method
				foreach (var split in trafficLightDefs.Select(def => def.Split(':')).Where(split => split.Length > 1)) {
					try {
						Log._Debug($"Traffic light split data: {split[0]} , {split[1]}");
						var nodeId = Convert.ToUInt16(split[0]);
						uint flag = Convert.ToUInt16(split[1]);

						if (!NetUtil.IsNodeValid(nodeId))
							continue;

						Flags.setNodeTrafficLight(nodeId, flag > 0);
					} catch (Exception e) {
						// ignore as it's probably bad save data.
						Log.Error($"Error setting the NodeTrafficLights: " + e.ToString());
						error = true;
					}
				}
			} else {
				Log.Warning("Junction traffic lights data structure undefined!");
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
							Log._Debug($"Split Data: {split[0]} , {split[1]}");
							var laneId = Convert.ToUInt32(split[0]);
							uint flags = Convert.ToUInt32(split[1]);

							if (!NetUtil.IsLaneValid(laneId))
								continue;

							//make sure we don't cause any overflows because of bad save data.
							if (Singleton<NetManager>.instance.m_lanes.m_buffer.Length <= laneId)
								continue;

							if (flags > ushort.MaxValue)
								continue;

							//Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags = fixLaneFlags(Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags);

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
							Log.Error($"Error loading Lane Split data. Length: {split.Length} value: {split}\nError: {e.ToString()}");
							error = true;
						}
					}
				}
			} else {
				Log.Warning("Lane arrow data structure undefined!");
			}

			// load lane connections
			if (_configuration.LaneConnections != null) {
				Log.Info($"Loading {_configuration.LaneConnections.Count} lane connections");
				foreach (Configuration.LaneConnection conn in _configuration.LaneConnections) {
					try {
						if (!NetUtil.IsLaneValid(conn.lowerLaneId))
							continue;
						if (!NetUtil.IsLaneValid(conn.higherLaneId))
							continue;
						Log._Debug($"Loading lane connection: lane {conn.lowerLaneId} -> {conn.higherLaneId}");
						LaneConnectionManager.Instance().AddLaneConnection(conn.lowerLaneId, conn.higherLaneId, conn.lowerStartNode);
					} catch (Exception e) {
						// ignore, as it's probably corrupt save data. it'll be culled on next save
						Log.Error("Error loading data from lane connection: " + e.ToString());
						error = true;
					}
				}
			} else {
				Log.Warning("Lane connection data structure undefined!");
			}

			// Load custom default speed limits
			SpeedLimitManager speedLimitManager = SpeedLimitManager.Instance();
			if (_configuration.CustomDefaultSpeedLimits != null) {
				Log.Info($"Loading custom default speed limit data. {_configuration.CustomDefaultSpeedLimits.Count} elements");
				foreach (KeyValuePair<string, float> e in _configuration.CustomDefaultSpeedLimits) {
					if (!speedLimitManager.NetInfoByName.ContainsKey(e.Key))
						continue;

					ushort customSpeedLimit = speedLimitManager.LaneToCustomSpeedLimit(e.Value, true);
					int customSpeedLimitIndex = speedLimitManager.AvailableSpeedLimits.IndexOf(customSpeedLimit);
					if (customSpeedLimitIndex >= 0) {
						NetInfo info = speedLimitManager.NetInfoByName[e.Key];
						speedLimitManager.SetCustomNetInfoSpeedLimitIndex(info, customSpeedLimitIndex);
					}
				}
			}

			// load speed limits
			if (_configuration.LaneSpeedLimits != null) {
				Log.Info($"Loading lane speed limit data. {_configuration.LaneSpeedLimits.Count} elements");
				foreach (Configuration.LaneSpeedLimit laneSpeedLimit in _configuration.LaneSpeedLimits) {
					try {
						if (!NetUtil.IsLaneValid(laneSpeedLimit.laneId))
							continue;
						NetInfo info = Singleton<NetManager>.instance.m_segments.m_buffer[Singleton<NetManager>.instance.m_lanes.m_buffer[laneSpeedLimit.laneId].m_segment].Info;
						int customSpeedLimitIndex = speedLimitManager.GetCustomNetInfoSpeedLimitIndex(info);
						if (customSpeedLimitIndex < 0 || speedLimitManager.AvailableSpeedLimits[customSpeedLimitIndex] != laneSpeedLimit.speedLimit) {
							// lane speed limit differs from default speed limit
							Log._Debug($"Loading lane speed limit: lane {laneSpeedLimit.laneId} = {laneSpeedLimit.speedLimit}");
							Flags.setLaneSpeedLimit(laneSpeedLimit.laneId, laneSpeedLimit.speedLimit);
						}
					} catch (Exception e) {
						// ignore, as it's probably corrupt save data. it'll be culled on next save
						Log.Warning("Error loading speed limits: " + e.ToString());
						error = true;
					}
				}
			} else {
				Log.Warning("Lane speed limit structure undefined!");
			}

			// Load segment-at-node flags
			if (_configuration.SegmentNodeConfs != null) {
				Log.Info($"Loading segment-at-node data. {_configuration.SegmentNodeConfs.Count} elements");
				foreach (Configuration.SegmentNodeConf segNodeConf in _configuration.SegmentNodeConfs) {
					try {
						if (!NetUtil.IsSegmentValid(segNodeConf.segmentId))
							continue;
						Flags.setSegmentNodeFlags(segNodeConf.segmentId, true, segNodeConf.startNodeFlags);
						Flags.setSegmentNodeFlags(segNodeConf.segmentId, false, segNodeConf.endNodeFlags);
					} catch (Exception e) {
						// ignore, as it's probably corrupt save data. it'll be culled on next save
						Log.Warning("Error loading segment-at-node config: " + e.ToString());
						error = true;
					}
				}
			} else {
				Log.Warning("Segment-at-node structure undefined!");
			}
		}

		public override void OnSaveData() {
			bool error = false;

			/*try {
				Log.Info("Recalculating segment geometries");
				SegmentGeometry.OnBeforeSaveData();
			} catch (Exception e) {
				Log.Error($"OnSaveData: Exception occurred while calling SegmentGeometry.OnBeforeSaveData: {e.ToString()}");
				error = true;
			}*/

			try {
				Log.Info("Applying all flags");
				Flags.applyAllFlags();
			} catch (Exception e) {
				Log.Error($"OnSaveData: Exception occurred while applying all flags: {e.ToString()}");
				error = true;
			}

			try {
				Log.Info("Saving Mod Data.");
				var configuration = new Configuration();

				TrafficPriorityManager prioMan = TrafficPriorityManager.Instance();

				if (prioMan.TrafficSegments != null) {
					for (ushort i = 0; i < Singleton<NetManager>.instance.m_segments.m_size; i++) {
						try {
							SavePrioritySegment(i, configuration);
						} catch (Exception e) {
							Log.Error($"Exception occurred while saving priority segment @ {i}: {e.ToString()}");
							error = true;
						}

						try {
							SaveSegmentNodeFlags(i, configuration);
						} catch (Exception e) {
							Log.Error($"Exception occurred while saving segment node flags @ {i}: {e.ToString()}");
							error = true;
						}
					}
				}

				TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance();

				for (ushort i = 0; i < Singleton<NetManager>.instance.m_nodes.m_size; i++) {
					/*if (TrafficLightSimulation.LightSimulationByNodeId != null) {
						SaveTrafficLightSimulation(i, configuration);
					}*/

					/*if (TrafficLightsManual.ManualSegments != null) {
						SaveManualTrafficLight(i, configuration);
					}*/

					TrafficLightSimulation sim = tlsMan.GetNodeSimulation(i);
					if (sim != null && sim.IsTimedLight()) {
						try {
							SaveTimedTrafficLight(i, configuration);
						} catch (Exception e) {
							Log.Error($"Exception occurred while saving timed traffic light @ {i}: {e.ToString()}");
							error = true;
						}
						// TODO save new traffic lights
					}

					try {
						SaveNodeLights(i, configuration);
					} catch (Exception e) {
						Log.Error($"Exception occurred while saving node traffic light @ {i}: {e.ToString()}");
						error = true;
					}
				}

#if !TAM
				if (LoadingExtension.IsPathManagerCompatible) {
#endif
					for (uint i = 0; i < Singleton<NetManager>.instance.m_lanes.m_buffer.Length; i++) {
						try {
							SaveLaneData(i, configuration);
						} catch (Exception e) {
							Log.Error($"Exception occurred while saving lane data @ {i}: {e.ToString()}");
							error = true;
						}
					}
#if !TAM
				}
#endif

				foreach (KeyValuePair<uint, ushort> e in Flags.getAllLaneSpeedLimits()) {
					try {
						SaveLaneSpeedLimit(new Configuration.LaneSpeedLimit(e.Key, e.Value), configuration);
					} catch (Exception ex) {
						Log.Error($"Exception occurred while saving lane speed limit @ {e.Key}: {ex.ToString()}");
						error = true;
					}
				}

				foreach (KeyValuePair<uint, ExtVehicleType> e in Flags.getAllLaneAllowedVehicleTypes()) {
					try {
						SaveLaneAllowedVehicleTypes(new Configuration.LaneVehicleTypes(e.Key, e.Value), configuration);
					} catch (Exception ex) {
						Log.Error($"Exception occurred while saving lane vehicle restrictions @ {e.Key}: {ex.ToString()}");
						error = true;
					}
				}

				foreach (KeyValuePair<string, int> e in SpeedLimitManager.Instance().CustomLaneSpeedLimitIndexByNetInfoName) {
					try {
						SaveCustomDefaultSpeedLimit(e.Key, e.Value, configuration);
					} catch (Exception ex) {
						Log.Error($"Exception occurred while saving custom default speed limits @ {e.Key}: {ex.ToString()}");
						error = true;
					}
				}

				var binaryFormatter = new BinaryFormatter();
				var memoryStream = new MemoryStream();

				try {
					binaryFormatter.Serialize(memoryStream, configuration);
					memoryStream.Position = 0;
					Log.Info($"Save data byte length {memoryStream.Length}");
					_serializableData.SaveData(DataId, memoryStream.ToArray());
				} catch (Exception ex) {
					Log.Error("Unexpected error while saving data: " + ex.ToString());
					error = true;
				} finally {
					memoryStream.Close();
				}

				try {
					// save options
					_serializableData.SaveData("TMPE_Options", new byte[] {
						(byte)Options.simAccuracy,
						(byte)0,//Options.laneChangingRandomization,
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
						(byte)(Options.IsDynamicPathRecalculationActive() ? 1 : 0),
						(byte)(Options.connectedLanesOverlay ? 1 : 0),
						(byte)(Options.prioritySignsEnabled ? 1 : 0),
						(byte)(Options.timedLightsEnabled ? 1 : 0),
						(byte)(Options.customSpeedLimitsEnabled ? 1 : 0),
						(byte)(Options.vehicleRestrictionsEnabled ? 1 : 0),
						(byte)(Options.laneConnectorEnabled ? 1 : 0),
						(byte)(Options.junctionRestrictionsOverlay ? 1 : 0),
						(byte)(Options.junctionRestrictionsEnabled ? 1 : 0),
						(byte)(Options.prohibitPocketCars ? 1 : 0),
						(byte)(Options.preferOuterLane ? 1 : 0)
				});
				} catch (Exception ex) {
					Log.Error("Unexpected error while saving options: " + ex.Message);
					error = true;
				}
			} catch (Exception e) {
				error = true;
				Log.Error($"Error occurred while saving data: {e.ToString()}");
				//UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("An error occurred while saving", "Traffic Manager: President Edition detected an error while saving. To help preventing future errors, please navigate to http://steamcommunity.com/sharedfiles/filedetails/?id=583429740 and follow the steps under 'In case problems arise'.", true);
			}
		}

		private void SaveCustomDefaultSpeedLimit(string infoName, int customSpeedLimitIndex, Configuration configuration) {
			try {
				SpeedLimitManager speedLimitManager = SpeedLimitManager.Instance();
				ushort customSpeedLimit = speedLimitManager.AvailableSpeedLimits[customSpeedLimitIndex];
				float gameSpeedLimit = speedLimitManager.ToGameSpeedLimit(customSpeedLimit);

				configuration.CustomDefaultSpeedLimits.Add(infoName, gameSpeedLimit);
			} catch (Exception e) {
				Log.Error($"Error occurred while saving custom default speed limit: {e.ToString()}");
			}
		}

		private static void SaveLaneData(uint i, Configuration configuration) {
			try {
				//NetLane.Flags flags = (NetLane.Flags)lane.m_flags;
				/*if ((flags & NetLane.Flags.LeftForwardRight) == NetLane.Flags.None) // only save lanes with explicit lane arrows
					return;*/
				if (! NetUtil.IsLaneValid(i))
					return;

				if (Flags.laneConnections[i] != null) {
					for (int nodeArrayIndex = 0; nodeArrayIndex <= 1; ++nodeArrayIndex) {
						uint[] connectedLaneIds = Flags.laneConnections[i][nodeArrayIndex];
						bool startNode = nodeArrayIndex == 0;
						if (connectedLaneIds != null) {
							foreach (uint otherHigherLaneId in connectedLaneIds) {
								if (otherHigherLaneId <= i)
									continue;
								if (!NetUtil.IsLaneValid(otherHigherLaneId))
									continue;

								Log._Debug($"Saving lane connection: lane {i} -> {otherHigherLaneId}");
								configuration.LaneConnections.Add(new Configuration.LaneConnection(i, (uint)otherHigherLaneId, startNode));
							}
						}
					}
				}

				//if (TrafficPriority.PrioritySegments.ContainsKey(laneSegmentId)) {
				Flags.LaneArrows? laneArrows = Flags.getLaneArrowFlags(i);
				if (laneArrows != null) {
					uint laneArrowInt = (uint)laneArrows;
					Log._Debug($"Saving lane data for lane {i}, setting to {laneArrows.ToString()} ({laneArrowInt})");
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

#if DEBUG
				if ((bool)hasTrafficLight) {
					Log._Debug($"Saving that node {i} has a traffic light");
				} else {
					Log._Debug($"Saving that node {i} does not have a traffic light");
				}
#endif
				configuration.NodeTrafficLights += $"{i}:{Convert.ToUInt16((bool)hasTrafficLight)},";
				return;
			} catch (Exception e) {
				Log.Error($"Error Adding Node Lights and Crosswalks {e.Message}");
				return;
			}
		}

		private static void SaveTimedTrafficLight(ushort i, Configuration configuration) {
			try {
				TrafficLightSimulation sim = TrafficLightSimulationManager.Instance().GetNodeSimulation(i);
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

						foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e2 in segLights.CustomLights) {
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
				TrafficPriorityManager prioMan = TrafficPriorityManager.Instance();

				if (prioMan.TrafficSegments[segmentId] == null) {
					return;
				}

				if (prioMan.TrafficSegments[segmentId].Node1 != 0 && prioMan.TrafficSegments[segmentId].Instance1.Type != SegmentEnd.PriorityType.None) {
					Log._Debug($"Saving Priority Segment of type: {prioMan.TrafficSegments[segmentId].Instance1.Type} @ node {prioMan.TrafficSegments[segmentId].Node1}, seg. {segmentId}");
                    configuration.PrioritySegments.Add(new[]
					{
						prioMan.TrafficSegments[segmentId].Node1, segmentId,
						(int) prioMan.TrafficSegments[segmentId].Instance1.Type
					});
				}

				if (prioMan.TrafficSegments[segmentId].Node2 == 0 || prioMan.TrafficSegments[segmentId].Instance2.Type == SegmentEnd.PriorityType.None)
					return;

				Log._Debug($"Saving Priority Segment of type: {prioMan.TrafficSegments[segmentId].Instance2.Type} @ node {prioMan.TrafficSegments[segmentId].Node2}, seg. {segmentId}");
				configuration.PrioritySegments.Add(new[] {
					prioMan.TrafficSegments[segmentId].Node2, segmentId,
					(int) prioMan.TrafficSegments[segmentId].Instance2.Type
				});
			} catch (Exception e) {
				Log.Error($"Error adding Priority Segments to Save: {e.ToString()}");
			}
		}

		private static void SaveSegmentNodeFlags(ushort segmentId, Configuration configuration) {
			try {
				NetManager netManager = Singleton<NetManager>.instance;

				if (! NetUtil.IsSegmentValid(segmentId))
					return;

				ushort startNodeId = netManager.m_segments.m_buffer[segmentId].m_startNode;
				ushort endNodeId = netManager.m_segments.m_buffer[segmentId].m_endNode;

				Configuration.SegmentNodeFlags startNodeFlags = NetUtil.IsNodeValid(startNodeId) ? Flags.getSegmentNodeFlags(segmentId, true) : null;
				Configuration.SegmentNodeFlags endNodeFlags = NetUtil.IsNodeValid(endNodeId) ? Flags.getSegmentNodeFlags(segmentId, false) : null;

				if (startNodeFlags == null && endNodeFlags == null)
					return;

				bool isDefaultConfiguration = true;
				if (startNodeFlags != null) {
					if (!startNodeFlags.IsDefault())
						isDefaultConfiguration = false;
				}

				if (endNodeFlags != null) {
					if (!endNodeFlags.IsDefault())
						isDefaultConfiguration = false;
				}

				if (isDefaultConfiguration)
					return;

				Configuration.SegmentNodeConf conf = new Configuration.SegmentNodeConf(segmentId);

				conf.startNodeFlags = startNodeFlags;
				conf.endNodeFlags = endNodeFlags;

				Log._Debug($"Saving segment-at-node flags for seg. {segmentId}");
				configuration.SegmentNodeConfs.Add(conf);
			} catch (Exception e) {
				Log.Error($"Error adding Priority Segments to Save: {e.ToString()}");
			}
		}
	}
}
