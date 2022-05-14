#define DEBUGLOAD
#define DEBUGSAVE

namespace TrafficManager.Lifecycle {
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Manager.Impl.LaneConnection;
    using TrafficManager.State;
    using Util;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using TrafficManager.Persistence;
    using System.Xml;

    [UsedImplicitly]
    public class SerializableDataExtension
        : SerializableDataExtensionBase
    {
        public static int Version => _configuration?.Version ?? Configuration.CURRENT_VERSION;

        private const string DATA_ID = "TrafficManager_v1.0";
        private const string VERSION_INFO_DATA_ID = "TrafficManager_VersionInfo_v1.0";
        private const string DOM_ID = "TrafficManager_Document_v1.0";

        private static ISerializableData SerializableData => SimulationManager.instance.m_SerializableDataWrapper;
        private static XDocument _dom;
        private static Type[] _persistenceMigration;
        private static Configuration _configuration;
        private static VersionInfoConfiguration _versionInfoConfiguration;

        public override void OnLoadData() => Load();
        public override void OnSaveData() => Save();

        public static void Load() {
            Log.Info("Loading Traffic Manager: PE Data");
            TMPELifecycle.Instance.Deserializing = true;
            bool loadingSucceeded = true;

            try {
                Log.Info("Initializing flags");
                Flags.OnBeforeLoadData();
            }
            catch (Exception e) {
                Log.Error($"OnLoadData: Error while initializing Flags: {e}");
                loadingSucceeded = false;
            }

            foreach (ICustomManager manager in TMPELifecycle.Instance.RegisteredManagers) {
                try {
                    Log.Info($"OnBeforeLoadData: {manager.GetType().Name}");
                    manager.OnBeforeLoadData();
                }
                catch (Exception e) {
                    Log.Error($"OnLoadData: Error while initializing {manager.GetType().Name}: {e}");
                    loadingSucceeded = false;
                }
            }

            Log.Info("Initialization done. Loading mod data now.");

            try {
                byte[] data = SerializableData.LoadData(VERSION_INFO_DATA_ID);
                DeserializeVersionData(data);
            }
            catch (Exception e) {
                Log.Error($"OnLoadData: Error while deserializing version data: {e}");
                loadingSucceeded = false;
            }

            try {
                byte[] data = SerializableData.LoadData(DOM_ID);
                LoadDom(data);
            }
            catch (Exception e) {
                Log.Error($"OnLoadData: Error while deserializing container collection (old savegame?): {e}");
            }

            bool loadedData = false;
            try {
                byte[] data = SerializableData.LoadData(DATA_ID);
                DeserializeData(data);
                loadedData = true;
            }
            catch (Exception e) {
                Log.Error($"OnLoadData: Error while deserializing data: {e}");
            }

            bool loadedContainers = false;
            try {
                LoadDomElements();
                loadedContainers = true;
            }
            catch (Exception e) {
                Log.Error($"OnLoadData: Error while deserializing containers: {e}");
                loadingSucceeded = false;
            }

            loadingSucceeded &= loadedData || loadedContainers;

            // load options (empty byte array causes default options to be applied)
            try {
                if (TMPELifecycle.InGameOrEditor()) {
                    // Always force default options on new game
                    // See: https://github.com/CitiesSkylinesMods/TMPE/pull/1425
                    byte[] options = TMPELifecycle.IsNewGame
                        ? null
                        : SerializableData.LoadData("TMPE_Options");

                    if (!OptionsManager.Instance.LoadData(options ?? new byte[0])) {
                        loadingSucceeded = false;
                    }
                }
            }
            catch (Exception e) {
                Log.Error($"OnLoadData: Error while loading options: {e}");
                loadingSucceeded = false;
            }

            if (loadingSucceeded) {
                Log.Info("OnLoadData completed successfully.");
            } else {
                Log.Info("An error occurred while loading.");

                // UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel")
                // .SetMessage("An error occurred while loading",
                // "Traffic Manager: President Edition detected an error while loading. Please do
                // NOT save this game under the old filename, otherwise your timed traffic lights,
                // custom lane arrows, etc. are in danger. Instead, please navigate to
                // http://steamcommunity.com/sharedfiles/filedetails/?id=583429740 and follow the
                // steps under 'In case problems arise'.", true);
            }

            TMPELifecycle.Instance.Deserializing = false;

            foreach (ICustomManager manager in TMPELifecycle.Instance.RegisteredManagers) {
                try {
                    Log.Info($"OnAfterLoadData: {manager.GetType().Name}");
                    manager.OnAfterLoadData();
                }
                catch (Exception e) {
                    Log.Error($"OnLoadData: Error while initializing {manager.GetType().Name}: {e}");
                    loadingSucceeded = false;
                }
            }
        }

        private static void DeserializeVersionData(byte[] data) {
            bool error = false;
            try {
                if (data != null && data.Length != 0) {
                    Log.Info($"Loading VersionInfo Data! Length={data.Length}");
                    var memoryStream = new MemoryStream();
                    memoryStream.Write(data, 0, data.Length);
                    memoryStream.Position = 0;

                    var binaryFormatter = new BinaryFormatter();
                    binaryFormatter.AssemblyFormat = System
                                                     .Runtime.Serialization.Formatters
                                                     .FormatterAssemblyStyle.Simple;
                    _versionInfoConfiguration = (VersionInfoConfiguration)binaryFormatter.Deserialize(memoryStream);
                } else {
                    Log.Info("No VersionInfo data to deserialize!");
                }
            }
            catch (Exception e) {
                Log.Error($"Error deserializing data: {e}");
                Log.Info(e.StackTrace);
                error = true;
            }

            if (!error) {
                ReportVersionInfo(out error);
            }

            if (error) {
                throw new ApplicationException("An error occurred while loading version information");
            }
        }

        private static void LoadDom(byte[] data) {
            try {
                if (data?.Length > 0) {
                    using (var memoryStream = new MemoryStream(data)) {
                        using (var streamReader = new StreamReader(memoryStream, Encoding.UTF8)) {

                            XmlReaderSettings xmlReaderSettings = new XmlReaderSettings {
                                ProhibitDtd = false,
                                XmlResolver = null,
                            };
                            using (var xmlReader = XmlReader.Create(streamReader, xmlReaderSettings))
                                _dom = XDocument.Load(xmlReader);

#if DEBUGLOAD
                            Log._Debug("Loaded DOM:\r" + _dom.ToString());
#endif

                            _persistenceMigration = GlobalPersistence.PersistentObjects
                                                    .Where(o => _dom.Root.Elements(o.ElementName)?.Any(e => o.CanLoad(e)) == true)
                                                    .Select(o => o.DependencyTarget)
                                                    .Distinct()
                                                    .ToArray();
                        }
                    }
                } else {
                    Log.Info("No DOM to load!");
                }
            }
            catch (Exception ex) {
                Log.Error($"Error loading DOM: {ex}");
                Log.Info(ex.StackTrace);
                throw new ApplicationException("An error occurred while loading");
            }
        }

        private static void LoadDomElements() {
            try {
                if (_dom?.Root.HasElements == true && GlobalPersistence.PersistentObjects.Count > 0) {
                    foreach (var o in GlobalPersistence.PersistentObjects.OrderBy(o => o)) {
                        var elements = _dom.Root.Elements(o.ElementName)?.Where(c => o.CanLoad(c));
                        if (elements?.Any() == true) {
                            if (elements.Count() > 1) {
                                Log.Error($"More than one compatible element {o.ElementName} was found. Using the first one.");
                            }
                            try {
                                var result = o.LoadData(elements.First(), new PersistenceContext { Version = Version });
                                result.LogMessage($"LoadData for DOM element {o.ElementName} reported {result}.");
                            }
                            catch (Exception ex) {
                                Log.Error($"Error loading DOM element {o.ElementName}: {ex}");
                                Log.Info(ex.StackTrace);
                            }
                        }
                    }
                } else {
                    Log.Info("No DOM elements to load!");
                }
            }
            catch (Exception ex) {
                Log.Error($"Error loading DOM elements: {ex}");
                Log.Info(ex.StackTrace);
                throw new ApplicationException("An error occurred while loading");
            }
        }

        private static void DeserializeData(byte[] data) {
            bool error = false;
            try {
                if (data != null && data.Length != 0) {
                    Log.Info($"Loading Data from Old 'New' Load Routine! Length={data.Length}");
                    var memoryStream = new MemoryStream();
                    memoryStream.Write(data, 0, data.Length);
                    memoryStream.Position = 0;

                    var binaryFormatter = new BinaryFormatter();
                    binaryFormatter.AssemblyFormat = System
                                                     .Runtime.Serialization.Formatters
                                                     .FormatterAssemblyStyle.Simple;
                    _configuration = (Configuration)binaryFormatter.Deserialize(memoryStream);
                } else {
                    Log.Info("No data to deserialize!");
                }
            }
            catch (Exception e) {
                Log.Error($"Error deserializing data: {e}");
                Log.Info(e.StackTrace);
                error = true;
            }

            if (!error) {
                LoadDataState(out error);
            }

            if (error) {
                throw new ApplicationException("An error occurred while loading");
            }
        }

        private static void ReportVersionInfo(out bool error) {
            error = false;
            Log.Info("Reading VersionInfo from config");
            if (_versionInfoConfiguration == null) {
                Log.Warning("Version configuration NULL, Couldn't load data. Possibly a new game?");
                return;
            }

            if (_versionInfoConfiguration.VersionInfo != null) {
                VersionInfo versionInfo = _versionInfoConfiguration.VersionInfo;
                Log.Info($"Save game was created with TM:PE {versionInfo.assemblyVersion} - {versionInfo.releaseType}");
            } else {
                Log.Info("Version info undefined!");
            }
        }

        private static void LoadDataState<T>(ICustomDataManager<T> manager, T data, string description, ref bool error) {
            if (_persistenceMigration?.Contains(manager.GetType()) == true) {
                if (data != null) {
                    Log.Info($"{manager.GetType().FullName} is in migration to DOM. {description} data structure ignored.");
                }
            }
            else if (data != null) {
                if (GlobalPersistence.PersistentObjects.Any(o => o.DependencyTarget == manager.GetType())) {
                    Log.Info($"Reading legacy {description} data structure (no DOM element was found).");
                }
                if (!manager.LoadData(data)) {
                    error = true;
                }
            } else if (description != null) {
                Log.Info($"{description} data structure undefined!");
            }
        }

        private static void LoadDataState(out bool error) {
            error = false;

            Log.Info("Loading State from Config");
            if (_configuration == null) {
                Log.Warning("Configuration NULL, Couldn't load save data. Possibly a new game?");
                return;
            }

            LoadDataState(ExtCitizenManager.Instance, _configuration.ExtCitizens, "Ext. citizen", ref error);
            LoadDataState(ExtCitizenInstanceManager.Instance, _configuration.ExtCitizenInstances, "Ext. citizen instance", ref error);
            LoadDataState(TrafficPriorityManager.Instance, _configuration.PrioritySegments, "Priority segments (old)", ref error);
            LoadDataState(TrafficPriorityManager.Instance, _configuration.CustomPrioritySegments, "Priority segments (new)", ref error);
            LoadDataState(ParkingRestrictionsManager.Instance, _configuration.ParkingRestrictions, "Parking restrctions", ref error);
            LoadDataState(VehicleRestrictionsManager.Instance, _configuration.LaneAllowedVehicleTypes, "Vehicle restrctions", ref error);
            LoadDataState(TrafficLightSimulationManager.Instance, _configuration.TimedLights, "Timed traffic lights", ref error);
            LoadDataState(TrafficLightManager.Instance, _configuration.NodeTrafficLights, "Junction traffic lights (old)", ref error);
            LoadDataState(TrafficLightManager.Instance, _configuration.ToggledTrafficLights, "Junction traffic lights (new)", ref error);
            LoadDataState(LaneArrowManager.Instance, _configuration.LaneFlags, "Lane arrow (old)", ref error);
            LoadDataState(LaneArrowManager.Instance, _configuration.LaneArrows, "Lane arrow (new)", ref error);
            LoadDataState(LaneConnectionManager.Instance, _configuration.LaneConnections, "Lane connection", ref error);
            LoadDataState(SpeedLimitManager.Instance, _configuration.CustomDefaultSpeedLimits, "Default speed limit", ref error);
            LoadDataState(SpeedLimitManager.Instance, _configuration.LaneSpeedLimits, "Lane speed limit", ref error);
            LoadDataState(JunctionRestrictionsManager.Instance, _configuration.SegmentNodeConfs, "Segment-at-node", ref error);
        }

        public static void Save() {
            bool success = true;

            // try {
            //    Log.Info("Recalculating segment geometries");
            //    SegmentGeometry.OnBeforeSaveData();
            // }
            // catch (Exception e) {
            //    Log.Error(
            //        $"OnSaveData: Exception occurred while calling SegmentGeometry.OnBeforeSaveData: {e.ToString()}");
            //    error = true;
            // }

            foreach (ICustomManager manager in TMPELifecycle.Instance.RegisteredManagers) {
                try {
                    Log.Info($"OnBeforeSaveData: {manager.GetType().Name}");
                    manager.OnBeforeSaveData();
                }
                catch (Exception e) {
                    Log.Error($"OnSaveData: Error while notifying {manager.GetType().Name}.OnBeforeSaveData: {e}");
                    success = false;
                }
            }

            try {
                Log.Info("Saving Mod Data.");
                var configuration = new Configuration() { Version = Configuration.CURRENT_VERSION };

                //------------------
                // Citizens
                //------------------
                configuration.ExtCitizens = ExtCitizenManager.Instance.SaveData(ref success);
                configuration.ExtCitizenInstances = ExtCitizenInstanceManager.Instance.SaveData(ref success);

                //------------------
                // Traffic Priorities
                //------------------
                configuration.PrioritySegments = TrafficPriorityManager.AsPrioritySegmentsDM()
                                                                       .SaveData(ref success);
                configuration.CustomPrioritySegments = TrafficPriorityManager.AsCustomPrioritySegmentsDM()
                                                                             .SaveData(ref success);

                //------------------
                // Junction Restrictions
                //------------------
                configuration.SegmentNodeConfs = JunctionRestrictionsManager.Instance.SaveData(ref success);

                //------------------
                // Traffic Lights
                //------------------
                configuration.TimedLights = TrafficLightSimulationManager.Instance.SaveData(ref success);

                // configuration.NodeTrafficLights = ((ICustomDataManager<string>)TrafficLightManager.Instance)
                // .SaveData(ref success);
                // configuration.ToggledTrafficLights =
                // ((ICustomDataManager<List<Configuration.NodeTrafficLight>>)TrafficLightManager.Instance)
                // .SaveData(ref success);

                //------------------
                // Lane Arrows and Connections
                //------------------
                configuration.LaneFlags = LaneArrowManager.AsLaneFlagsDM().SaveData(ref success);
                configuration.LaneArrows = LaneArrowManager.AsLaneArrowsDM().SaveData(ref success);

                configuration.LaneConnections = LaneConnectionManager.Instance.SaveData(ref success);

                //------------------
                // Speed Limits
                //------------------
                configuration.LaneSpeedLimits = SpeedLimitManager.AsLaneSpeedLimitsDM().SaveData(ref success);
                configuration.CustomDefaultSpeedLimits = SpeedLimitManager.AsCustomDefaultSpeedLimitsDM()
                                                                          .SaveData(ref success);

                //------------------
                // Vehicle and Parking Restrictions
                //------------------
                configuration.LaneAllowedVehicleTypes = VehicleRestrictionsManager.Instance.SaveData(ref success);
                configuration.ParkingRestrictions = ParkingRestrictionsManager.Instance.SaveData(ref success);

                //------------------
                // Version
                //------------------
                VersionInfoConfiguration versionConfig = new VersionInfoConfiguration();
                versionConfig.VersionInfo = new VersionInfo(VersionUtil.ModVersion);

                var binaryFormatterVersion = new BinaryFormatter();
                var memoryStreamVersion = new MemoryStream();

                try {
                    binaryFormatterVersion.Serialize(memoryStreamVersion, versionConfig);
                    memoryStreamVersion.Position = 0;
                    Log.Info($"Version data byte length {memoryStreamVersion.Length}");
                    SerializableData.SaveData(VERSION_INFO_DATA_ID, memoryStreamVersion.ToArray());
                }
                catch (Exception ex) {
                    Log.Error("Unexpected error while saving version data: " + ex);
                    success = false;
                }
                finally {
                    memoryStreamVersion.Close();
                }

                try {
                    if (TMPELifecycle.PlayMode) {
                        SerializableData.SaveData("TMPE_Options", OptionsManager.Instance.SaveData(ref success));
                    }
                }
                catch (Exception ex) {
                    Log.Error("Unexpected error while saving options: " + ex.Message);
                    success = false;
                }

                var binaryFormatter = new BinaryFormatter();
                var memoryStream = new MemoryStream();

                try {
                    binaryFormatter.Serialize(memoryStream, configuration);
                    memoryStream.Position = 0;
                    Log.Info($"Save data byte length {memoryStream.Length}");
                    SerializableData.SaveData(DATA_ID, memoryStream.ToArray());
                }
                catch (Exception ex) {
                    Log.Error("Unexpected error while saving data: " + ex);
                    success = false;
                }
                finally {
                    memoryStream.Close();
                }

                SaveDom(ref success);

                var reverseManagers = new List<ICustomManager>(TMPELifecycle.Instance.RegisteredManagers);
                reverseManagers.Reverse();
                foreach (ICustomManager manager in reverseManagers) {
                    try {
                        Log.Info($"OnAfterSaveData: {manager.GetType().Name}");
                        manager.OnAfterSaveData();
                    }
                    catch (Exception e) {
                        Log.Error(
                            $"OnSaveData: Error while notifying {manager.GetType().Name}.OnAfterSaveData: {e}");
                        success = false;
                    }
                }
            }
            catch (Exception e) {
                success = false;
                Log.Error($"Error occurred while saving data: {e}");

                // UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel")
                // .SetMessage("An error occurred while saving", "Traffic Manager: President Edition
                // detected an error while saving. To help preventing future errors, please navigate
                // to http://steamcommunity.com/sharedfiles/filedetails/?id=583429740 and follow
                // the steps under 'In case problems arise'.", true);
            }
        }

        private static void SaveDom(ref bool success) {

            try {
                _dom = new XDocument();
                _dom.Add(new XElement("TmpSaveData"));
                foreach (var o in GlobalPersistence.PersistentObjects.OrderBy(o => o)) {
                    var result = o.SaveData(_dom.Root, new PersistenceContext { Version = Version });
                    result.LogMessage($"SaveData for DOM element {o.ElementName} reported {result}.");
                }

                using (var memoryStream = new MemoryStream()) {
                    using (var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8)) {

#if DEBUGSAVE
                        Log._Debug("Saving DOM:\r" + _dom.ToString());
#endif

                        _dom.Save(streamWriter);

                        memoryStream.Position = 0;
                        Log.Info($"Save DOM byte length {memoryStream.Length}");

                        SerializableData.SaveData(DOM_ID, memoryStream.ToArray());
                    }
                }
            }
            catch (Exception ex) {
                Log.Error("Unexpected error while saving DOM: " + ex);
                success = false;
            }
        }
    }
}
