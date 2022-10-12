namespace TrafficManager.Lifecycle {
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using System;
    using ColossalFramework.UI;
    using TrafficManager.API.Manager;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Manager.Impl.LaneConnection;
    using TrafficManager.State;
    using Util;

    [UsedImplicitly]
    public class SerializableDataExtension
        : SerializableDataExtensionBase
    {
        public static int Version => _configuration?.Version ?? Configuration.CURRENT_VERSION;

        private const string DATA_ID = "TrafficManager_v1.0";
        private const string VERSION_INFO_DATA_ID = "TrafficManager_VersionInfo_v1.0";

        private static ISerializableData SerializableData => SimulationManager.instance.m_SerializableDataWrapper;
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
                    Log.Error($"OnLoadData_OnBeforeLoadData: Error while initializing {manager.GetType().Name}: {e}");
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
                byte[] data = SerializableData.LoadData(DATA_ID);
                DeserializeData(data);
            }
            catch (Exception e) {
                Log.Error($"OnLoadData: Error while deserializing data: {e}");
                loadingSucceeded = false;
            }

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
                Log.Warning("An error occurred while loading.");
                UIView.ForwardException(new Exception(
                                            "Traffic Manager mod detected an error while loading saved data.\n" +
                                            "Please do NOT save this game under the old filename, \n" +
                                            "otherwise your timed traffic lights, custom lane arrows, etc. will get unrecoverable.\n" +
                                            "Contact TM:PE team via Steam Workshop page or Discord text chat, prepare log files."));
            }

            TMPELifecycle.Instance.Deserializing = false;

            foreach (ICustomManager manager in TMPELifecycle.Instance.RegisteredManagers) {
                try {
                    Log.Info($"OnAfterLoadData: {manager.GetType().Name}");
                    manager.OnAfterLoadData();
                }
                catch (Exception e) {
                    Log.Error($"OnLoadData_OnAfterLoadData: Error while initializing {manager.GetType().Name}: {e}");

                    UIView.ForwardException(new Exception(
                                                "Traffic Manager mod detected an initialization error.\n" +
                                                "Some features of the mod may not work correctly and mod setting may not be fully initialized.\n" +
                                                "Contact TM:PE team via Steam Workshop page or Discord text chat, prepare log files.",
                                                new Exception($"OnAfterLoadData: Error while initializing {manager.GetType().Name}:\n{e}")));
                }
            }

            Patcher.Install();
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

        private static void DeserializeData(byte[] data) {
            bool error = false;
            try {
                if (data != null && data.Length != 0) {
                    Log.Info($"Loading Data from New Load Routine! Length={data.Length}");
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

        private static void LoadDataState(out bool error) {
            error = false;

            Log.Info("Loading State from Config");
            if (_configuration == null) {
                Log.Warning("Configuration NULL, Couldn't load save data. Possibly a new game?");
                return;
            }

            // load ext. citizens
            if (_configuration.ExtCitizens != null) {
                if (!ExtCitizenManager.Instance.LoadData(_configuration.ExtCitizens)) {
                    error = true;
                }
            } else {
                Log.Info("Ext. citizen data structure undefined!");
            }

            // load ext. citizen instances
            if (_configuration.ExtCitizenInstances != null) {
                if (!ExtCitizenInstanceManager.Instance.LoadData(_configuration.ExtCitizenInstances)) {
                    error = true;
                }
            } else {
                Log.Info("Ext. citizen instance data structure undefined!");
            }

            // load priority segments
            if (_configuration.PrioritySegments != null) {
                if (!TrafficPriorityManager.Instance.LoadData(_configuration.PrioritySegments)) {
                    error = true;
                }
            } else {
                Log.Info("Priority segments data structure (old) undefined!");
            }

            if (_configuration.CustomPrioritySegments != null) {
                if (!TrafficPriorityManager.Instance.LoadData(_configuration.CustomPrioritySegments)) {
                    error = true;
                }
            } else {
                Log.Info("Priority segments data structure (new) undefined!");
            }

            // load parking restrictions
            if (_configuration.ParkingRestrictions != null) {
                if (!ParkingRestrictionsManager.Instance.LoadData(_configuration.ParkingRestrictions)) {
                    error = true;
                }
            } else {
                Log.Info("Parking restrictions structure undefined!");
            }

            // load vehicle restrictions (warning: has to be done before loading timed lights!)
            if (_configuration.LaneAllowedVehicleTypes != null) {
                if (!VehicleRestrictionsManager.Instance.LoadData(_configuration.LaneAllowedVehicleTypes)) {
                    error = true;
                }
            } else {
                Log.Info("Vehicle restrictions structure undefined!");
            }

            if (_configuration.TimedLights != null) {
                if (!TrafficLightSimulationManager.Instance.LoadData(_configuration.TimedLights)) {
                    error = true;
                }
            } else {
                Log.Info("Timed traffic lights data structure undefined!");
            }

            // load toggled traffic lights (old method)
            if (_configuration.NodeTrafficLights != null) {
                if (!TrafficLightManager.Instance.LoadData(_configuration.NodeTrafficLights)) {
                    error = true;
                }
            } else {
                Log.Info("Junction traffic lights data structure (old) undefined!");
            }

            // load toggled traffic lights (new method)
            if (_configuration.ToggledTrafficLights != null) {
                if (!TrafficLightManager.Instance.LoadData(_configuration.ToggledTrafficLights)) {
                    error = true;
                }
            } else {
                Log.Info("Junction traffic lights data structure (new) undefined!");
            }

            // load lane arrows (old method)
            if (_configuration.LaneFlags != null) {
                if (!LaneArrowManager.Instance.LoadData(_configuration.LaneFlags)) {
                    error = true;
                }
            } else {
                Log.Info("Lane arrow data structure (old) undefined!");
            }

            // load lane arrows (new method)
            if (_configuration.LaneArrows != null) {
                if (!LaneArrowManager.Instance.LoadData(_configuration.LaneArrows)) {
                    error = true;
                }
            } else {
                Log.Info("Lane arrow data structure (new) undefined!");
            }

            // load lane connections
            if (_configuration.LaneConnections != null) {
                if (!LaneConnectionManager.Instance.LoadData(_configuration.LaneConnections)) {
                    error = true;
                }
            } else {
                Log.Info("Lane connection data structure undefined!");
            }

            // Load custom default speed limits
            if (_configuration.CustomDefaultSpeedLimits != null) {
                if (!SpeedLimitManager.Instance.LoadData(_configuration.CustomDefaultSpeedLimits)) {
                    error = true;
                }
            }

            // load speed limits
            if (_configuration.LaneSpeedLimits != null) {
                if (!SpeedLimitManager.Instance.LoadData(_configuration.LaneSpeedLimits)) {
                    error = true;
                }
            } else {
                Log.Info("Lane speed limit structure undefined!");
            }

            // Load segment-at-node flags
            if (_configuration.SegmentNodeConfs != null) {
                if (!JunctionRestrictionsManager.Instance.LoadData(_configuration.SegmentNodeConfs)) {
                    error = true;
                }
            } else {
                Log.Info("Segment-at-node structure undefined!");
            }
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
                } catch (Exception ex) {
                    Log.Error("Unexpected error while saving version data: " + ex);
                    success = false;
                } finally {
                    memoryStreamVersion.Close();
                }

                try {
                    if (TMPELifecycle.PlayMode) {
                        SerializableData.SaveData("TMPE_Options", OptionsManager.Instance.SaveData(ref success));
                    }
                } catch (Exception ex) {
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
                } catch (Exception ex) {
                    Log.Error("Unexpected error while saving data: " + ex);
                    success = false;
                } finally {
                    memoryStream.Close();
                }

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
            } catch (Exception e) {
                success = false;
                Log.Error($"Error occurred while saving data: {e}");

                // UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel")
                // .SetMessage("An error occurred while saving", "Traffic Manager: President Edition
                // detected an error while saving. To help preventing future errors, please navigate
                // to http://steamcommunity.com/sharedfiles/filedetails/?id=583429740 and follow
                // the steps under 'In case problems arise'.", true);
            }
        }
    }
}