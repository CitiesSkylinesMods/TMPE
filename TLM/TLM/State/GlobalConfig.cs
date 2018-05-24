#define RUSHHOUR

using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using TrafficManager.Manager;
using TrafficManager.State.ConfigData;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using TrafficManager.Util;

namespace TrafficManager.State {
	[XmlRootAttribute("GlobalConfig", Namespace = "http://www.viathinksoft.de/tmpe", IsNullable = false)]
	public class GlobalConfig : GenericObservable<GlobalConfig> {
		public const string FILENAME = "TMPE_GlobalConfig.xml";
		public const string BACKUP_FILENAME = FILENAME + ".bak";
		private static int LATEST_VERSION = 13;
#if DEBUG
		private static uint lastModificationCheckFrame = 0;
#endif

		public static int? RushHourParkingSearchRadius { get; private set; } = null;

#if RUSHHOUR
		private static DateTime? rushHourConfigModifiedTime = null;
		private const string RUSHHOUR_CONFIG_FILENAME = "RushHourOptions.xml";
#endif

		public static GlobalConfig Instance {
			get {
				return instance;
			}
			private set {
				if (value != null && instance != null) {
					value.Observers = instance.Observers;
					value.ObserverLock = instance.ObserverLock;
				}
				instance = value;
				if (instance != null) {
					instance.NotifyObservers();
				}
			}
		}

		private static GlobalConfig instance = null;

		//private object ObserverLock = new object();

		/// <summary>
		/// Holds a list of observers which are being notified as soon as the configuration is updated
		/// </summary>
		//private List<IObserver<GlobalConfig>> observers = new List<IObserver<GlobalConfig>>();

		static GlobalConfig() {
			Reload();
		}

		internal static void OnLevelUnloading() {
#if RUSHHOUR
			rushHourConfigModifiedTime = null;
			RushHourParkingSearchRadius = null;
#endif
		}

		private static DateTime ModifiedTime = DateTime.MinValue;

		/// <summary>
		/// Configuration version
		/// </summary>
		public int Version = LATEST_VERSION;

		/// <summary>
		/// Language to use (if null then the game's language is being used)
		/// </summary>
		public string LanguageCode = null;

#if DEBUG
		public Debug Debug = new Debug();
#endif

		public AdvancedVehicleAI AdvancedVehicleAI = new AdvancedVehicleAI();

		public DynamicLaneSelection DynamicLaneSelection = new DynamicLaneSelection();

		public Main Main = new Main();

		public ParkingAI ParkingAI = new ParkingAI();

		public PathFinding PathFinding = new PathFinding();

		public PriorityRules PriorityRules = new PriorityRules();

		public TimedTrafficLights TimedTrafficLights = new TimedTrafficLights();

		internal static void WriteConfig() {
			ModifiedTime = WriteConfig(Instance);
		}

		private static GlobalConfig WriteDefaultConfig(GlobalConfig oldConfig, bool resetAll, out DateTime modifiedTime) {
			Log._Debug($"Writing default config...");
			GlobalConfig conf = new GlobalConfig();
			if (!resetAll && oldConfig != null) {
				conf.Main.MainMenuButtonX = oldConfig.Main.MainMenuButtonX;
				conf.Main.MainMenuButtonY = oldConfig.Main.MainMenuButtonY;

				conf.Main.MainMenuX = oldConfig.Main.MainMenuX;
				conf.Main.MainMenuY = oldConfig.Main.MainMenuY;

				conf.Main.MainMenuButtonPosLocked = oldConfig.Main.MainMenuButtonPosLocked;
				conf.Main.MainMenuPosLocked = oldConfig.Main.MainMenuPosLocked;
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
					if (LoadingExtension.IsGameLoaded
#if DEBUG
						&& !conf.Debug.Switches[10]
#endif
						) {
						Constants.ManagerFactory.RoutingManager.RequestFullRecalculation(true);
					}

#if DEBUG
					if (conf.Debug == null) {
						conf.Debug = new Debug();
					}
#endif

					if (conf.AdvancedVehicleAI == null) {
						conf.AdvancedVehicleAI = new AdvancedVehicleAI();
					}

					if (conf.DynamicLaneSelection == null) {
						conf.DynamicLaneSelection = new DynamicLaneSelection();
					}

					if (conf.ParkingAI == null) {
						conf.ParkingAI = new ParkingAI();
					}

					if (conf.PathFinding == null) {
						conf.PathFinding = new PathFinding();
					}

					if (conf.PriorityRules == null) {
						conf.PriorityRules = new PriorityRules();
					}

					if (conf.TimedTrafficLights == null) {
						conf.TimedTrafficLights = new TimedTrafficLights();
					}

					return conf;
				}
			} catch (Exception e) {
				Log.Warning($"Could not load global config: {e} Generating default config.");
				return WriteDefaultConfig(null, false, out modifiedTime);
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

		public static void Reset(GlobalConfig oldConfig, bool resetAll=false) {
			Log.Info($"Resetting global config.");
			DateTime modifiedTime;
			Instance = WriteDefaultConfig(oldConfig, resetAll, out modifiedTime);
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
				ReloadRushHourConfigIfNewer();
			}
#endif
		}

		/*public IDisposable Subscribe(IObserver<GlobalConfig> observer) {
			try {
				Monitor.Enter(ObserverLock);
				Log.Info($"Adding {observer} as observer of global config");
				observers.Add(observer);
			} finally {
				Monitor.Exit(ObserverLock);
			}
			return new GenericUnsubscriber<GlobalConfig>(observers, observer, ObserverLock);
		}*/

		/*protected void NotifyObservers() {
			//Log.Warning($"NodeGeometry.NotifyObservers(): CurrentSegmentReplacement={CurrentSegmentReplacement}");

			List<IObserver<GlobalConfig>> myObservers = new List<IObserver<GlobalConfig>>(observers); // in case somebody unsubscribes while iterating over subscribers
			foreach (IObserver<GlobalConfig> observer in myObservers) {
				try {
					Log.Info($"Notifying global config observer {observer}");
					observer.OnUpdate(this);
				} catch (Exception e) {
					Log.Error($"GlobalConfig.NotifyObservers: An exception occured while notifying an observer of global config: {e}");
				}
			}
		}*/
	}
}
