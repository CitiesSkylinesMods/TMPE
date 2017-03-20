#define RUSHHOUR

using ColossalFramework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace TrafficManager.State {
	[XmlRootAttribute("GlobalConfig", Namespace = "http://www.viathinksoft.de/tmpe", IsNullable = false)]
	public class GlobalConfig {
		public const string FILENAME = "TMPE_GlobalConfig.xml";
		public const string BACKUP_FILENAME = FILENAME + ".bak";
		private static int LATEST_VERSION = 5;
#if DEBUG
		private static uint lastModificationCheckFrame = 0;
#endif

		public static int? RushHourParkingSearchRadius { get; private set; } = null;

#if RUSHHOUR
		private static DateTime? rushHourConfigModifiedTime = null;
		private const string RUSHHOUR_CONFIG_FILENAME = "RushHourOptions.xml";
		private static uint lastRushHourConfigCheck = 0;
#endif

		public static GlobalConfig Instance { get; private set; } = null;

		static GlobalConfig() {
			Reload();
		}

		internal static void OnLevelUnloading() {
#if RUSHHOUR
			rushHourConfigModifiedTime = null;
			lastRushHourConfigCheck = 0;
			RushHourParkingSearchRadius = null;
#endif
		}

		//public static GlobalConfig Instance() {
		//#if DEBUG
		//			uint curDebugFrame = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 10;
		//			if (lastModificationCheckFrame == 0) {
		//				lastModificationCheckFrame = curDebugFrame;
		//			} else if (lastModificationCheckFrame < curDebugFrame) {
		//				lastModificationCheckFrame = curDebugFrame;
		//				ReloadIfNewer();
		//			}
		//#endif
		//return Instance;
		//}

		private static DateTime ModifiedTime = DateTime.MinValue;

		/// <summary>
		/// Configuration version
		/// </summary>
		public int Version = LATEST_VERSION;

		public bool[] DebugSwitches = {
			false, // path-find debug log
			false, 
			false, // parking ai debug log (basic)
			false, // emergency vehicles may not ignore traffic rules
			false, // parking ai debug log (extended)
			false, // geometry debug log
			false, // debug pause
			false, // debug TTL
			false
		};

#if DEBUG
		public ushort PathFindDebugNodeId = 0;
		public ushort TTLDebugNodeId = 0;
#endif

		/// <summary>
		/// base lane changing cost factor on highways
		/// </summary>
		public float HighwayLaneChangingBaseCost = 1.25f;

		/// <summary>
		/// base lane changing cost factor on city streets
		/// </summary>
		public float CityRoadLaneChangingBaseCost = 1.1f;

		/// <summary>
		/// congestion lane changing base cost
		/// </summary>
		public float CongestionLaneChangingBaseCost = 1f;

		/// <summary>
		/// heavy vehicle lane changing cost factor
		/// </summary>
		public float HeavyVehicleLaneChangingCostFactor = 1.5f;

		/// <summary>
		/// > 1 lane changing cost factor
		/// </summary>
		public float MoreThanOneLaneChangingCostFactor = 2.5f;

		/// <summary>
		/// speed-to-density balance factor, 1 = only speed is considered, 0 = both speed and density are considered
		/// </summary>
		public float SpeedToDensityBalance = 0.75f;

		/// <summary>
		/// lane changing cost reduction modulo
		/// </summary>
		public int RandomizedLaneChangingModulo = 100;

		/// <summary>
		/// artifical lane distance for u-turns
		/// </summary>
		public int UturnLaneDistance = 2;

		/// <summary>
		/// artifical lane distance for vehicles that change to lanes which have an incompatible lane arrow configuration
		/// </summary>
		public byte IncompatibleLaneDistance = 1;

		/// <summary>
		/// lane density random interval
		/// </summary>
		public float LaneDensityRandInterval = 10f;

		/// <summary>
		/// lane speed random interval
		/// </summary>
		public float LaneSpeedRandInterval = 20f;


		/// <summary>
		/// penalty for busses not driving on bus lanes
		/// </summary>
		public float PublicTransportLanePenalty = 1.5f;

		/// <summary>
		/// reward for public transport staying on transport lane
		/// </summary>
		public float PublicTransportLaneReward = 0.75f;

		/// <summary>
		/// maximum penalty for heavy vehicles driving on an inner lane (in %)
		/// </summary>
		public float HeavyVehicleMaxInnerLanePenalty = 40f;


		/// <summary>
		/// parking space search radius; used if pocket car spawning is disabled
		/// </summary>
		public float VicinityParkingSpaceSearchRadius = 256f;

		/// <summary>
		/// parking space search in vicinity is randomized. Cims do not always select the nearest parking space possible.
		/// A value of 1u always selects the nearest parking space.
		/// A value of 2u selects the nearest parking space with 50% chance, the next one with 25%, then 12.5% and so on.
		/// A value of 4u selects the nearest parking space with 75% chance, the next one with 18.75%, then 4.6875% and so on.
		/// A value of N selects the nearest parking space with (N-1)/N chance, the next one with (1-(N-1)/N)*(N-1)/N, then (1-(N-1)/N)^2*(N-1)/N and so on.
		/// </summary>
		public uint VicinityParkingSpaceSelectionRand = 4u;

		/// <summary>
		/// maximum number of parking attempts for passenger cars
		/// </summary>
		public int MaxParkingAttempts = 10;

		/// <summary>
		/// minimum required distance between target building and parked car for using a car
		/// </summary>
		public float MinParkedCarToTargetBuildingDistance = 256f;

		/// <summary>
		/// maximum required squared distance between citizen instance and parked vehicle before the parked car is turned into a vehicle
		/// </summary>
		public float MaxParkedCarInstanceSwitchSqrDistance = 6f;

		/// <summary>
		/// maximum distance between building and pedestrian lane
		/// </summary>
		public float MaxBuildingToPedestrianLaneDistance = 64f;

		/// <summary>
		/// Maximum allowed distance between home and parked car when travelling home without forced to use the car
		/// </summary>
		public float MaxParkedCarDistanceToHome = 512f;

		/// <summary>
		/// maximum incoming vehicle square distance to junction for priority signs
		/// </summary>
		public float MaxPriorityCheckSqrDist = 225f;

		/// <summary>
		/// maximum junction approach time for priority signs
		/// </summary>
		public float MaxPriorityApproachTime = 10f;


		/// <summary>
		/// Minimum speed update factor
		/// </summary>
		public float MinSpeedUpdateFactor = 0.025f;

		/// <summary>
		/// Maximum speed update factor
		/// </summary>
		public float MaxSpeedUpdateFactor = 0.25f;

		/// <summary>
		/// %/100 of time a segment must be flagged as congested to count as permanently congested
		/// </summary>
		public float CongestionSpeedThreshold = 0.6f;

		/// <summary>
		/// lower congestion threshold (per ten-thousands)
		/// </summary>
		public int LowerSpeedCongestionThreshold = 6000;

		/// <summary>
		/// upper congestion threshold (per ten-thousands)
		/// </summary>
		public int UpperSpeedCongestionThreshold = 7000;


		/// <summary>
		/// public transport demand increment on path-find failure
		/// </summary>
		public uint PublicTransportDemandIncrement = 10u;

		/// <summary>
		/// public transport demand increment if waiting time was exceeded
		/// </summary>
		public uint PublicTransportDemandWaitingIncrement = 3u;

		/// <summary>
		/// public transport demand decrement on simulation step
		/// </summary>
		public uint PublicTransportDemandDecrement = 1u;

		/// <summary>
		/// public transport demand decrement on path-find success
		/// </summary>
		public uint PublicTransportDemandUsageDecrement = 7u;

		/// <summary>
		/// parking space demand decrement on simulation step
		/// </summary>
		public uint ParkingSpaceDemandDecrement = 1u;

		/// <summary>
		/// minimum parking space demand delta when a passenger car could be spawned
		/// </summary>
		public int MinSpawnedCarParkingSpaceDemandDelta = -5;

		/// <summary>
		/// maximum parking space demand delta when a passenger car could be spawned
		/// </summary>
		public int MaxSpawnedCarParkingSpaceDemandDelta = 3;

		/// <summary>
		/// minimum parking space demand delta when a parking spot could be found
		/// </summary>
		public int MinFoundParkPosParkingSpaceDemandDelta = -5;

		/// <summary>
		/// maximum parking space demand delta when a parking spot could be found
		/// </summary>
		public int MaxFoundParkPosParkingSpaceDemandDelta = 3;

		/// <summary>
		/// parking space demand increment when no parking spot could be found while trying to park
		/// </summary>
		public uint FailedParkingSpaceDemandIncrement = 5u;

		/// <summary>
		/// parking space demand increment when no parking spot could be found while trying to spawn a parked vehicle
		/// </summary>
		public uint FailedSpawnParkingSpaceDemandIncrement = 10u;

		/// <summary>
		/// Maximum allowed reported speed difference among all lanes of one segment (in 10000ths)
		/// </summary>
		public uint MaxSpeedDifference = 950u;

		/// <summary>
		/// Main menu button position
		/// </summary>
		public int MainMenuButtonX = 464;
		public int MainMenuButtonY = 10;

		internal static void WriteConfig() {
			ModifiedTime = WriteConfig(Instance);
		}

		private static GlobalConfig WriteDefaultConfig(GlobalConfig oldConfig, out DateTime modifiedTime) {
			Log._Debug($"Writing default config...");
			GlobalConfig conf = new GlobalConfig();
			if (oldConfig != null) {
				conf.MainMenuButtonX = oldConfig.MainMenuButtonX;
				conf.MainMenuButtonY = oldConfig.MainMenuButtonY;
			}
			modifiedTime = WriteConfig(conf);
			return conf;
		}

		private static DateTime WriteConfig(GlobalConfig config, string filename=FILENAME) {
			try {
				Log.Info($"Writing global config to file '{filename}'...");
				XmlSerializer serializer = new XmlSerializer(typeof(GlobalConfig));
				using (TextWriter writer = new StreamWriter(filename)) {
					serializer.Serialize(writer, config);
				}
			} catch (Exception e) {
				Log.Error($"Could not write global config: {e.ToString()}");
			}

			try {
				return File.GetLastWriteTime(FILENAME);
			} catch (Exception e) {
				Log.Warning($"Could not determine modification date of global config: {e.ToString()}");
				return DateTime.Now;
			}
		}

		public static GlobalConfig Load(out DateTime modifiedTime) {
			try {
				modifiedTime = File.GetLastWriteTime(FILENAME);

				Log.Info($"Loading global config from file '{FILENAME}'...");
				using (FileStream fs = new FileStream(FILENAME, FileMode.Open)) {
					XmlSerializer serializer = new XmlSerializer(typeof(GlobalConfig));
					Log.Info($"Global config loaded.");
					GlobalConfig conf = (GlobalConfig)serializer.Deserialize(fs);
					return conf;
				}
			} catch (Exception) {
				Log.Warning("Could not load global config. Generating default config.");
				return WriteDefaultConfig(null, out modifiedTime);
			}
		}

		public static void Reload(bool checkVersion=true) {
			DateTime modifiedTime;
			GlobalConfig conf = Load(out modifiedTime);
			if (checkVersion && conf.Version != -1 && conf.Version < LATEST_VERSION) {
				// backup old config and reset
				string filename = BACKUP_FILENAME;
				try {
					int backupIndex = 0;
					while (File.Exists(filename)) {
						filename = BACKUP_FILENAME + "." + backupIndex;
						++backupIndex;
					}
					WriteConfig(conf, filename);
				} catch (Exception e) {
					Log.Warning($"Error occurred while saving backup config to '{filename}': {e.ToString()}");
				}
				Reset(conf);
			} else {
				Instance = conf;
				ModifiedTime = WriteConfig(Instance);
			}
		}

		public static void Reset(GlobalConfig oldConfig) {
			Log.Info($"Resetting global config.");
			DateTime modifiedTime;
			Instance = WriteDefaultConfig(oldConfig, out modifiedTime);
			ModifiedTime = modifiedTime;
		}

		private static void ReloadIfNewer() {
			try {
				DateTime modifiedTime = File.GetLastWriteTime(FILENAME);
				if (modifiedTime > ModifiedTime) {
					Log.Info($"Detected modification of global config.");
					Reload(false);
				}
			} catch (Exception) {
				Log.Warning("Could not determine modification date of global config.");
			}
		}

#if RUSHHOUR
		private static void ReloadRushHourConfigIfNewer() { // TODO refactor
			try {
				DateTime newModifiedTime = File.GetLastWriteTime(RUSHHOUR_CONFIG_FILENAME);
				if (rushHourConfigModifiedTime != null) {
					if (newModifiedTime <= rushHourConfigModifiedTime)
						return;
				}

				rushHourConfigModifiedTime = newModifiedTime;

				XmlDocument doc = new XmlDocument();
				doc.Load(RUSHHOUR_CONFIG_FILENAME);
				XmlNode root = doc.DocumentElement;

				XmlNode betterParkingNode = root.SelectSingleNode("OptionPanel/data/BetterParking");
				XmlNode parkingSpaceRadiusNode = root.SelectSingleNode("OptionPanel/data/ParkingSearchRadius");

				if ("True".Equals(betterParkingNode.InnerText)) {
					RushHourParkingSearchRadius = int.Parse(parkingSpaceRadiusNode.InnerText);
				}

				Log._Debug($"RushHour config has changed. Setting searchRadius={RushHourParkingSearchRadius}");
			} catch (Exception ex) {
				Log.Error("GlobalConfig.ReloadRushHourConfigIfNewer: " + ex.ToString());
			}
		}
#endif

		public void SimulationStep() {
#if RUSHHOUR
			if (LoadingExtension.IsRushHourLoaded) {
				uint curFrame = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 12;
				if (lastRushHourConfigCheck < curFrame) {
					lastRushHourConfigCheck = curFrame;
					ReloadRushHourConfigIfNewer();
				}
			}
#endif
		}
	}
}
