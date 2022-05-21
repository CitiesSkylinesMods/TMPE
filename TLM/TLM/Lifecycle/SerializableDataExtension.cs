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
    using System.IO.Compression;

    [UsedImplicitly]
    public class SerializableDataExtension
        : SerializableDataExtensionBase
    {
        public static int Version => _configuration?.Version ?? Configuration.CURRENT_VERSION;

        private const string DATA_ID = "TrafficManager_v1.0";
        private const string VERSION_INFO_DATA_ID = "TrafficManager_VersionInfo_v1.0";

        private static ISerializableData SerializableData => SimulationManager.instance.m_SerializableDataWrapper;
        private static Dictionary<IPersistentObject, XDocument> _domCollection = new Dictionary<IPersistentObject, XDocument>(ReferenceEqualityComparer<IPersistentObject>.Instance);
        private static HashSet<Type> _persistenceMigration = new HashSet<Type>(ReferenceEqualityComparer<Type>.Instance);
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
                LoadDomCollection();
            }
            catch (Exception e) {
                Log.Error($"OnLoadData: Error while loading DOM collection: {e}");
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

        private static void LoadDomCollection() {

            _domCollection.Clear();
            _persistenceMigration.Clear();

            foreach (var po in GlobalPersistence.PersistentObjects) {

                byte[] data;
                try {
                    data = SerializableData.LoadData(po.DependencyTarget.FullName);
                }
                catch {
                    Log.Info($"No DOM found for {po.DependencyTarget.Name}");
                    data = null;
                }

                try {
                    if (data?.Length > 0) {

                        Log.Info($"Attempting to load DOM for {po.DependencyTarget.Name}");

                        try {
                            using (var memoryStream = new MemoryStream(data)) {
                                using (var compressionStream = new GZipStream(memoryStream, CompressionMode.Decompress)) {
                                    using (var streamReader = new StreamReader(compressionStream, Encoding.UTF8)) {

                                        XmlReaderSettings xmlReaderSettings = new XmlReaderSettings {
                                            ProhibitDtd = false,
                                            XmlResolver = null,
                                        };
                                        using (var xmlReader = XmlReader.Create(streamReader, xmlReaderSettings))
                                            _domCollection[po] = XDocument.Load(xmlReader);
                                    }
                                }
                            }
                        }
                        catch (Exception ex) {

                            Log.Error("Load DOM failed, attempting without compression. " + ex);

                            using (var memoryStream = new MemoryStream(data)) {
                                using (var streamReader = new StreamReader(memoryStream, Encoding.UTF8)) {

                                    XmlReaderSettings xmlReaderSettings = new XmlReaderSettings {
                                        ProhibitDtd = false,
                                        XmlResolver = null,
                                    };
                                    using (var xmlReader = XmlReader.Create(streamReader, xmlReaderSettings))
                                        _domCollection[po] = XDocument.Load(xmlReader);
                                }
                            }

                            Log.Info("Load DOM without compression succeeded.");

                        }

                        if (_domCollection[po]?.Root?.Elements(po.ElementName)?.Any(e => po.CanLoad(e)) == true)
                            _persistenceMigration.Add(po.DependencyTarget);
#if DEBUGLOAD
                        Log._Debug($"Loaded DOM for {po.DependencyTarget.Name}: {_domCollection[po]}\r");
#endif
                    }
                }
                catch (Exception ex) {
                    Log.Error($"Error loading DOM for {po.DependencyTarget.Name}: {ex}");
                    Log.Info(ex.StackTrace);
                }
            }
        }

        private static void LoadDomElements() {

            try {
                foreach (var e in _domCollection.OrderBy(e => e.Key)) {
                    var po = e.Key;
                    var dom = e.Value;

                    var elements = dom.Root.Elements(po.ElementName)?.Where(c => po.CanLoad(c));
                    if (elements?.Any() == true) {
                        if (elements.Count() > 1) {
                            Log.Error($"More than one compatible element {po.ElementName} was found for {po.DependencyTarget.Name}. Using the first one.");
                        }
                        try {
                            var result = po.LoadData(elements.First(), new PersistenceContext { Version = Version });
                            result.LogMessage($"LoadData of DOM element {po.ElementName} for {po.DependencyTarget.Name} reported {result}.");
                        }
                        catch (Exception ex) {
                            Log.Error($"Error loading DOM element {po.ElementName} for {po.DependencyTarget.Name}: {ex}");
                            Log.Info(ex.StackTrace);
                        }
                    }
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

            foreach (var po in GlobalPersistence.PersistentObjects.OrderBy(o => o)) {

                try {
                    var dom = new XDocument();
                    dom.Add(new XElement(po.DependencyTarget.Name));

                    var result = po.SaveData(dom.Root, new PersistenceContext { Version = Version });

                    result.LogMessage($"SaveData of DOM element {po.ElementName} for {po.DependencyTarget.Name} reported {result}.");

#if DEBUGSAVE
                    Log._Debug($"Saving DOM for {po.DependencyTarget.Name}: {dom}");
#endif

                    try {
                        using (var memoryStream = new MemoryStream()) {

                            using (var compressionStream = new GZipStream(memoryStream, CompressionMode.Compress, true)) {

                                using (var streamWriter = new StreamWriter(compressionStream, Encoding.UTF8)) {

                                    dom.Save(streamWriter);
                                }
                            }

                            memoryStream.Position = 0;
                            Log.Info($"Save DOM for {po.DependencyTarget.Name} byte length {memoryStream.Length}");

                            SerializableData.SaveData(po.DependencyTarget.FullName, memoryStream.ToArray());
                        }
                    }
                    catch (Exception ex) {

                        Log.Error("Save DOM failed, attempting without compression. " + ex);

                        try {
                            SerializableData.EraseData(po.DependencyTarget.FullName);
                        }
                        catch {
                        }

                        using (var memoryStream = new MemoryStream()) {
                            using (var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8)) {

                                dom.Save(streamWriter);

                                memoryStream.Position = 0;
                                Log.Info($"Save DOM for {po.DependencyTarget.Name} byte length {memoryStream.Length}");

                                SerializableData.SaveData(po.DependencyTarget.FullName, memoryStream.ToArray());
                            }
                        }

                        Log.Info("Save DOM without compression succeeded.");

                    }
                }
                catch (Exception ex) {

                    Log.Error($"Unexpected error while saving DOM for {po.DependencyTarget.Name}: {ex}");
                    success = false;
                }
            }
        }
    }
}
