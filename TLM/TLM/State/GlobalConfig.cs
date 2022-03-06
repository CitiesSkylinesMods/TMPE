namespace TrafficManager.State {
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.IO;
    using System.Xml.Serialization;
    using System;
    using TrafficManager.State.ConfigData;
    using TrafficManager.Util;
    using TrafficManager.Lifecycle;

    [XmlRootAttribute(
        "GlobalConfig",
        IsNullable = false)]
    public class GlobalConfig : GenericObservable<GlobalConfig> {
        public const string FILENAME = "TMPE_GlobalConfig.xml";
        public const string BACKUP_FILENAME = FILENAME + ".bak";
        private static int LATEST_VERSION = 20;

        public static GlobalConfig Instance {
            get => instance;
            private set {
                if (value != null && instance != null) {
                    value._observers = instance._observers;
                    value._lock = instance._lock;
                }

                instance = value;
                if (instance != null) {
                    instance.NotifyObservers(instance);
                }
            }
        }

        private static GlobalConfig instance = null;

        //private object ObserverLock = new object();

        /// <summary>
        /// Initializes static members of the <see cref="GlobalConfig"/> class.
        /// Holds a list of observers which are being notified as soon as the configuration is updated.
        /// </summary>
        //private List<IObserver<GlobalConfig>> observers = new List<IObserver<GlobalConfig>>();
        static GlobalConfig() {
            Reload();
        }

        internal static void OnLevelUnloading() { }

        private static DateTime ModifiedTime = DateTime.MinValue;

        /// <summary>
        /// Configuration version
        /// </summary>
        public int Version = LATEST_VERSION;

        /// <summary>
        /// Language to use (if null then the game's language is being used)
        /// </summary>
        [CanBeNull]
        public string LanguageCode = null;

#if DEBUG
        public ConfigData.DebugSettings Debug = new DebugSettings();
#endif

        public ConfigData.AdvancedVehicleAI AdvancedVehicleAI = new();

        public ConfigData.DynamicLaneSelection DynamicLaneSelection = new();

        public ConfigData.Gameplay Gameplay = new();

        public ConfigData.Main Main = new();

        public ConfigData.ParkingAI ParkingAI = new();

        public ConfigData.PathFinding PathFinding = new();

        public ConfigData.PriorityRules PriorityRules = new();

        public ConfigData.TimedTrafficLights TimedTrafficLights = new();

        internal static void WriteConfig() {
            ModifiedTime = WriteConfig(Instance);
        }

        private static GlobalConfig WriteDefaultConfig(GlobalConfig oldConfig,
                                                       bool resetAll,
                                                       out DateTime modifiedTime) {
            Log._Debug($"Writing default config...");
            GlobalConfig conf = new GlobalConfig();

            if (!resetAll && oldConfig != null) {
                conf.Main.MainMenuButtonX = oldConfig.Main.MainMenuButtonX;
                conf.Main.MainMenuButtonY = oldConfig.Main.MainMenuButtonY;

                conf.Main.MainMenuX = oldConfig.Main.MainMenuX;
                conf.Main.MainMenuY = oldConfig.Main.MainMenuY;

                conf.Main.MainMenuButtonPosLocked = oldConfig.Main.MainMenuButtonPosLocked;
                conf.Main.MainMenuPosLocked = oldConfig.Main.MainMenuPosLocked;

                conf.Main.GuiOpacity = oldConfig.Main.GuiOpacity;
                conf.Main.OverlayOpacity = oldConfig.Main.OverlayOpacity;

                conf.Main.EnableTutorial = oldConfig.Main.EnableTutorial;
                conf.Main.DisplayedTutorialMessages = oldConfig.Main.DisplayedTutorialMessages;

                conf.Main.OpenUrlsInSteamOverlay = oldConfig.Main.OpenUrlsInSteamOverlay;
            }

            modifiedTime = WriteConfig(conf);
            return conf;
        }

        private static DateTime WriteConfig(GlobalConfig config, string filename = FILENAME) {
            try {
                Log.Info($"Writing global config to file '{filename}'...");
                XmlSerializer serializer = new XmlSerializer(typeof(GlobalConfig));
                using (TextWriter writer = new StreamWriter(filename)) {
                    serializer.Serialize(writer, config);
                }
            }
            catch (Exception e) {
                Log.Error($"Could not write global config: {e.ToString()}");
            }

            try {
                return File.GetLastWriteTime(FILENAME);
            }
            catch (Exception e) {
                Log.Warning(
                    $"Could not determine modification date of global config: {e.ToString()}");
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
                    if (TMPELifecycle.Instance.IsGameLoaded
#if DEBUG
                        && !DebugSwitch.NoRoutingRecalculationOnConfigReload.Get()
#endif
                        ) {
                        Constants.ManagerFactory.RoutingManager.RequestFullRecalculation();
                    }

#if DEBUG
                    if (conf.Debug == null) {
                        conf.Debug = new DebugSettings();
                    }
#endif

                    if (conf.AdvancedVehicleAI == null) {
                        conf.AdvancedVehicleAI = new AdvancedVehicleAI();
                    }

                    if (conf.DynamicLaneSelection == null) {
                        conf.DynamicLaneSelection = new DynamicLaneSelection();
                    }

                    if (conf.Gameplay == null) {
                        conf.Gameplay = new Gameplay();
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
            }
            catch (Exception e) {
                Log.Warning($"Could not load global config: {e} Generating default config.");
                return WriteDefaultConfig(null, false, out modifiedTime);
            }
        }

        public static void Reload(bool checkVersion = true) {
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
                }
                catch (Exception e) {
                    Log.Warning(
                        $"Error occurred while saving backup config to '{filename}': {e.ToString()}");
                }

                Reset(conf);
            } else {
                Instance = conf;
                ModifiedTime = WriteConfig(Instance);
            }
        }

        public static void Reset(GlobalConfig oldConfig, bool resetAll = false) {
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
            }
            catch (Exception) {
                Log.Warning("Could not determine modification date of global config.");
            }
        }
    }
}