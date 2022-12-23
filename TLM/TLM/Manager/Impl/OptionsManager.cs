namespace TrafficManager.Manager.Impl {
    using CSUtil.Commons;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.State;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.Textures;
    using TrafficManager.Lifecycle;
    using JetBrains.Annotations;
    using TrafficManager.Util;
    using System.Reflection;


    public class OptionsManager
        : AbstractCustomManager,
          IOptionsManager {

        public static OptionsManager Instance = new OptionsManager();

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log.NotImpl("InternalPrintDebugInfo for OptionsManager");
        }

        // See: OnAfterLoadData() and related methods
        private static bool _needUpdateDedicatedTurningLanes = false;
        private static bool _needUpdateJunctionRestrictionsManager = false;

        /// <summary>
        /// API method for external mods to get option values by name.
        /// </summary>
        /// <typeparam name="TVal">Option type, eg. <c>bool</c>.</typeparam>
        /// <param name="optionName">Name of the option in <see cref="Options"/>.</param>
        /// <param name="value">The option value, if found, otherwise <c>default</c> for <typeparamref name="TVal"/>.</param>
        /// <returns>Returns <c>true</c> if successful, or <c>false</c> if there was a problem (eg. option not found, wrong TVal, etc).</returns>
        /// <remarks>Check <see cref="OptionsAreSafeToQuery"/> first before trying to get an option value.</remarks>
        public bool TryGetOptionByName<TVal>(string optionName, out TVal value) {
            if (!SavedGameOptions.Available) {
                value = default;
                return false;
            }

            var field = typeof(SavedGameOptions).GetField(optionName);

            if (field == null || field.FieldType is not TVal) {
                value = default;
                return false;
            }

            value = (TVal)field.GetValue(SavedGameOptions.Instance);
            return true;
        }

        public override void OnBeforeLoadData() {
            SavedGameOptions.Ensure();
        }

        /// <summary>
        /// Loading options may trigger multiple calls to certain methods. To de-spam
        /// invocations, the methods are skipped during loading and a bool is set
        /// so that we can invoke those methods just once after loading has completed.
        /// </summary>
        public override void OnAfterLoadData() {
            base.OnAfterLoadData();
            Log.Info("OptionsManger.OnAfterLoadData() checking for queued method calls");

            if (_needUpdateDedicatedTurningLanes) {
                _needUpdateDedicatedTurningLanes = false;
                UpdateDedicatedTurningLanes();
            }

            if (_needUpdateJunctionRestrictionsManager) {
                _needUpdateJunctionRestrictionsManager = false;
                UpdateJunctionRestrictionsManager();
            }
        }

        [Obsolete("Use TMPELifecycle method of same name instead")]
        public bool MayPublishSegmentChanges()
            => TMPELifecycle.Instance.MayPublishSegmentChanges();

        private static void LoadByte([NotNull] byte[] data, uint idx, ref byte value) {
            if(idx < data.Length) {
                value = data[idx];
            }
        }

        /// <summary>Load LegacySerializableOption</summary>
        private static void ToOption(byte[] data, uint idx, ILegacySerializableOption opt) {
            if(idx < data.Length) {
                opt.Load(data[idx]);
            }
        }

        /// <summary>
        /// Restores the mod options based on supplied <paramref name="data"/>.
        /// </summary>
        /// <param name="data">Byte array obtained from the savegame.</param>
        /// <returns>Returns <c>true</c> if successful, otherwise <c>false</c>.</returns>
        /// <remarks>Applies default values if the data for an option does not exist.</remarks>
        public bool LoadData(byte[] data) {
            try {
                SavedGameOptions.Available = false;
                int dataVersion = SerializableDataExtension.Version;

                Log.Info($"OptionsManager.LoadData: {data.Length} bytes");

                if (dataVersion >= 3 || data.Length == 0) {
                    // new version or default.
                    ToOption(data, idx: 0, GeneralTab_SimulationGroup.SimulationAccuracy);
                } else {
                    // legacy
                    GeneralTab_SimulationGroup.SimulationAccuracy.Load((byte)(SimulationAccuracy.MaxValue - data[0]));
                }

                // skip SavedGameOptions.Instance.setLaneChangingRandomization(options[1]);
                ToOption(data, idx: 2, GameplayTab_VehicleBehaviourGroup.RecklessDrivers);
                ToOption(data, idx: 3, PoliciesTab_AtJunctionsGroup.RelaxedBusses);
                ToOption(data, idx: 4, OverlaysTab_OverlaysGroup.NodesOverlay);
                ToOption(data, idx: 5, PoliciesTab_AtJunctionsGroup.AllowEnterBlockedJunctions);
                ToOption(data, idx: 6, GameplayTab_AIGroups.AdvancedAI);
                ToOption(data, idx: 7, PoliciesTab_OnHighwaysGroup.HighwayRules);
                ToOption(data, idx: 8, OverlaysTab_OverlaysGroup.PrioritySignsOverlay);
                ToOption(data, idx: 9, OverlaysTab_OverlaysGroup.TimedLightsOverlay);
                ToOption(data, idx: 10, OverlaysTab_OverlaysGroup.SpeedLimitsOverlay);
                ToOption(data, idx: 11, OverlaysTab_OverlaysGroup.VehicleRestrictionsOverlay);
                ToOption(data, idx: 12, GameplayTab_VehicleBehaviourGroup.StrongerRoadConditionEffects);
                ToOption(data, idx: 13, PoliciesTab_AtJunctionsGroup.AllowUTurns);
                ToOption(data, idx: 14, PoliciesTab_AtJunctionsGroup.AllowLaneChangesWhileGoingStraight);

                ToOption(data, idx: 15, GameplayTab_VehicleBehaviourGroup.DisableDespawning);
                if (dataVersion < 1) {
                    //legacy:
                    GameplayTab_VehicleBehaviourGroup.DisableDespawning.Value = !GameplayTab_VehicleBehaviourGroup.DisableDespawning.Value; 
                }

                // skip SavedGameOptions.Instance.setDynamicPathRecalculation(data[16] == (byte)1);
                ToOption(data, idx: 17, OverlaysTab_OverlaysGroup.ConnectedLanesOverlay);
                ToOption(data, idx: 18, MaintenanceTab_FeaturesGroup.PrioritySignsEnabled);
                ToOption(data, idx: 19, MaintenanceTab_FeaturesGroup.TimedLightsEnabled);
                ToOption(data, idx: 20, MaintenanceTab_FeaturesGroup.CustomSpeedLimitsEnabled);
                ToOption(data, idx: 21, MaintenanceTab_FeaturesGroup.VehicleRestrictionsEnabled);
                ToOption(data, idx: 22, MaintenanceTab_FeaturesGroup.LaneConnectorEnabled);
                ToOption(data, idx: 23, OverlaysTab_OverlaysGroup.JunctionRestrictionsOverlay);
                ToOption(data, idx: 24, MaintenanceTab_FeaturesGroup.JunctionRestrictionsEnabled);
                ToOption(data, idx: 25, GameplayTab_AIGroups.ParkingAI);
                ToOption(data, idx: 26, PoliciesTab_OnHighwaysGroup.PreferOuterLane);
                ToOption(data, idx: 27, GameplayTab_VehicleBehaviourGroup.IndividualDrivingStyle);
                ToOption(data, idx: 28, PoliciesTab_InEmergenciesGroup.EvacBussesMayIgnoreRules);
                // skip ToOption(data, idx: 29, GeneralTab_SimulationGroup.InstantEffects, true);
                ToOption(data, idx: 30, MaintenanceTab_FeaturesGroup.ParkingRestrictionsEnabled);
                ToOption(data, idx: 31, OverlaysTab_OverlaysGroup.ParkingRestrictionsOverlay);
                ToOption(data, idx: 32, PoliciesTab_OnRoadsGroup.BanRegularTrafficOnBusLanes);
                ToOption(data, idx: 33, OverlaysTab_OverlaysGroup.ShowPathFindStats);
                ToOption(data, idx: 34, GameplayTab_AIGroups.AltLaneSelectionRatio);

                ToOption(data, idx: 35, PoliciesTab_OnRoadsGroup.VehicleRestrictionsAggression);

                ToOption(data, idx: 36, PoliciesTab_AtJunctionsGroup.TrafficLightPriorityRules);
                ToOption(data, idx: 37, GameplayTab_AIGroups.RealisticPublicTransport);
                ToOption(data, idx: 38, MaintenanceTab_FeaturesGroup.TurnOnRedEnabled);
                ToOption(data, idx: 39, PoliciesTab_AtJunctionsGroup.AllowNearTurnOnRed);
                ToOption(data, idx: 40, PoliciesTab_AtJunctionsGroup.AllowFarTurnOnRed);
                ToOption(data, idx: 41, PoliciesTab_AtJunctionsGroup.AutomaticallyAddTrafficLightsIfApplicable);

                ToOption(data, idx: 42, PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_StayInLaneMainR);
                ToOption(data, idx: 43, PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_StayInLaneNearRabout);
                ToOption(data, idx: 44, PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_DedicatedExitLanes);
                ToOption(data, idx: 45, PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_NoCrossMainR);
                ToOption(data, idx: 46, PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_NoCrossYieldR);
                ToOption(data, idx: 47, PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_PrioritySigns);

                ToOption(data, idx: 48, PoliciesTab_PriorityRoadsGroup.PriorityRoad_CrossMainR);
                ToOption(data, idx: 49, PoliciesTab_PriorityRoadsGroup.PriorityRoad_AllowLeftTurns);
                ToOption(data, idx: 50, PoliciesTab_PriorityRoadsGroup.PriorityRoad_EnterBlockedYeild);
                ToOption(data, idx: 51, PoliciesTab_PriorityRoadsGroup.PriorityRoad_StopAtEntry);

                ToOption(data, idx: 52, PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_KeepClearYieldR);
                ToOption(data, idx: 53, PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_RealisticSpeedLimits);
                ToOption(data, idx: 54, PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_ParkingBanMainR);
                ToOption(data, idx: 55, PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_ParkingBanYieldR);

                ToOption(data, idx: 56, PoliciesTab_OnRoadsGroup.NoDoubleCrossings);
                ToOption(data, idx: 57, PoliciesTab_AtJunctionsGroup.DedicatedTurningLanes);

                LoadByte(data, idx: 58, ref SavedGameOptions.Instance.SavegamePathfinderEdition);

                ToOption(data, idx: 59, OverlaysTab_OverlaysGroup.ShowDefaultSpeedSubIcon);

                ToOption(data, idx: 60, PoliciesTab_OnHighwaysGroup.HighwayMergingRules);
                return true;
            }
            catch (Exception ex) {
                ex.LogException();
                return false;
            } finally {
                SavedGameOptions.Available = true;
            }
        }

        /// <summary>
        /// Compiles mod options in to a byte array for storage in savegame.
        /// </summary>
        /// <param name="success">Current success state of SaveData operation.</param>
        /// <returns>Returns <c>true</c> if successful, otherwise <c>false</c>.</returns>
        public byte[] SaveData(ref bool success) {

            // Remember to update this when adding new options (lastIdx + 1)
            var save = new byte[61];

            try {
                save[0] = GeneralTab_SimulationGroup.SimulationAccuracy.Save();
                save[1] = 0; // SavedGameOptions.Instance.laneChangingRandomization
                save[2] = GameplayTab_VehicleBehaviourGroup.RecklessDrivers.Save();
                save[3] = (byte)(SavedGameOptions.Instance.relaxedBusses ? 1 : 0);
                save[4] = (byte)(SavedGameOptions.Instance.nodesOverlay ? 1 : 0);
                save[5] = (byte)(SavedGameOptions.Instance.allowEnterBlockedJunctions ? 1 : 0);
                save[6] = (byte)(SavedGameOptions.Instance.advancedAI ? 1 : 0);
                save[7] = (byte)(SavedGameOptions.Instance.highwayRules ? 1 : 0);
                save[8] = (byte)(SavedGameOptions.Instance.prioritySignsOverlay ? 1 : 0);
                save[9] = (byte)(SavedGameOptions.Instance.timedLightsOverlay ? 1 : 0);
                save[10] = (byte)(SavedGameOptions.Instance.speedLimitsOverlay ? 1 : 0);
                save[11] = (byte)(SavedGameOptions.Instance.vehicleRestrictionsOverlay ? 1 : 0);
                save[12] = GameplayTab_VehicleBehaviourGroup.StrongerRoadConditionEffects.Save();
                save[13] = (byte)(SavedGameOptions.Instance.allowUTurns ? 1 : 0);
                save[14] = (byte)(SavedGameOptions.Instance.allowLaneChangesWhileGoingStraight ? 1 : 0);
                save[15] = (byte)(SavedGameOptions.Instance.disableDespawning ? 1 : 0);
                save[16] = 0; // SavedGameOptions.Instance.IsDynamicPathRecalculationActive
                save[17] = (byte)(SavedGameOptions.Instance.connectedLanesOverlay ? 1 : 0);
                save[18] = (byte)(SavedGameOptions.Instance.prioritySignsEnabled ? 1 : 0);
                save[19] = (byte)(SavedGameOptions.Instance.timedLightsEnabled ? 1 : 0);
                save[20] = (byte)(SavedGameOptions.Instance.customSpeedLimitsEnabled ? 1 : 0);
                save[21] = (byte)(SavedGameOptions.Instance.vehicleRestrictionsEnabled ? 1 : 0);
                save[22] = (byte)(SavedGameOptions.Instance.laneConnectorEnabled ? 1 : 0);
                save[23] = (byte)(SavedGameOptions.Instance.junctionRestrictionsOverlay ? 1 : 0);
                save[24] = (byte)(SavedGameOptions.Instance.junctionRestrictionsEnabled ? 1 : 0);
                save[25] = (byte)(SavedGameOptions.Instance.parkingAI ? 1 : 0);
                save[26] = (byte)(SavedGameOptions.Instance.preferOuterLane ? 1 : 0);
                save[27] = GameplayTab_VehicleBehaviourGroup.IndividualDrivingStyle.Save();
                save[28] = (byte)(SavedGameOptions.Instance.evacBussesMayIgnoreRules ? 1 : 0);
                save[29] = 0; // (byte)(SavedGameOptions.Instance.instantEffects ? 1 : 0);
                save[30] = (byte)(SavedGameOptions.Instance.parkingRestrictionsEnabled ? 1 : 0);
                save[31] = (byte)(SavedGameOptions.Instance.parkingRestrictionsOverlay ? 1 : 0);
                save[32] = (byte)(SavedGameOptions.Instance.banRegularTrafficOnBusLanes ? 1 : 0);
                save[33] = (byte)(SavedGameOptions.Instance.showPathFindStats ? 1 : 0);
                save[34] = SavedGameOptions.Instance.altLaneSelectionRatio;
                save[35] = PoliciesTab_OnRoadsGroup.VehicleRestrictionsAggression.Save();
                save[36] = (byte)(SavedGameOptions.Instance.trafficLightPriorityRules ? 1 : 0);
                save[37] = (byte)(SavedGameOptions.Instance.realisticPublicTransport ? 1 : 0);
                save[38] = (byte)(SavedGameOptions.Instance.turnOnRedEnabled ? 1 : 0);
                save[39] = (byte)(SavedGameOptions.Instance.allowNearTurnOnRed ? 1 : 0);
                save[40] = (byte)(SavedGameOptions.Instance.allowFarTurnOnRed ? 1 : 0);
                save[41] = (byte)(SavedGameOptions.Instance.automaticallyAddTrafficLightsIfApplicable ? 1 : 0);

                save[42] = PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_StayInLaneMainR.Save();
                save[43] = PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_StayInLaneNearRabout.Save();
                save[44] = PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_DedicatedExitLanes.Save();
                save[45] = PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_NoCrossMainR.Save();
                save[46] = PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_NoCrossYieldR.Save();
                save[47] = PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_PrioritySigns.Save();

                save[48] = PoliciesTab_PriorityRoadsGroup.PriorityRoad_CrossMainR.Save();
                save[49] = PoliciesTab_PriorityRoadsGroup.PriorityRoad_AllowLeftTurns.Save();
                save[50] = PoliciesTab_PriorityRoadsGroup.PriorityRoad_EnterBlockedYeild.Save();
                save[51] = PoliciesTab_PriorityRoadsGroup.PriorityRoad_StopAtEntry.Save();

                save[52] = PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_KeepClearYieldR.Save();
                save[53] = PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_RealisticSpeedLimits.Save();
                save[54] = PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_ParkingBanMainR.Save();
                save[55] = PoliciesTab_RoundaboutsGroup.RoundAboutQuickFix_ParkingBanYieldR.Save();

                save[56] = PoliciesTab_OnRoadsGroup.NoDoubleCrossings.Save();
                save[57] = PoliciesTab_AtJunctionsGroup.DedicatedTurningLanes.Save();

                save[58] = SavedGameOptions.Instance.SavegamePathfinderEdition;

                save[59] = OverlaysTab_OverlaysGroup.ShowDefaultSpeedSubIcon.Save();

                save[60] = PoliciesTab_OnHighwaysGroup.HighwayMergingRules.Save();

                return save;
            }
            catch (Exception ex) {
                ex.LogException();
                return save; // try and salvage some of the settings
            }
        }

        /// <summary>
        /// Triggers a rebuild of the main menu (toolbar), adding/removing buttons
        /// where applicable, and refreshing all translated text. Very slow.
        /// </summary>
        internal static void RebuildMenu(bool languageChanged = false) {
            if (TMPELifecycle.Instance.Deserializing || ModUI.Instance == null) {
                Log._Debug("OptionsManager.RebuildMenu() - Ignoring; Deserialising or ModUI.Instance is null");
                return;
            }

            Log.Info("OptionsManager.RebuildMenu()");
            ModUI.Instance.RebuildMenu();

            // TM:PE main button also needs to be updated
            if (ModUI.Instance.MainMenuButton != null) {
                ModUI.Instance.MainMenuButton.UpdateButtonSkinAndTooltip();
            }

            if (languageChanged) {
                TrafficLightTextures.Instance.ReloadTexturesWithTranslation();
                TMPELifecycle.Instance.TranslationDatabase.ReloadTutorialTranslations();
                TMPELifecycle.Instance.TranslationDatabase.ReloadGuideTranslations();
            }
        }

        /// <summary>
        /// When options which affect subtools or overlays are toggled,
        /// all subtools are reinitialised to ensure they reflect the change.
        /// </summary>
        internal static void ReinitialiseSubTools() {
            if (TMPELifecycle.Instance.Deserializing || ModUI.Instance == null) {
                Log._Debug("OptionsManager.ReinitialiseSubTools() - Ignoring; Deserialising or ModUI is null");
                return;
            }

            Log.Info("OptionsManager.ReinitialiseSubTools()");
            ModUI.GetTrafficManagerTool()?.InitializeSubTools();
        }

        /// <summary>
        /// When junction restriction policies are toggled, all junctions
        /// need to reflect the new default settings.
        /// </summary>
        internal static void UpdateJunctionRestrictionsManager() {
            if (TMPELifecycle.Instance.Deserializing) {
                Log._Debug("SavedGameOptions.Instance.UpdateJunctionRestrictionsManager() - Waiting for deserialisation");
                _needUpdateJunctionRestrictionsManager = true;
                return;
            }

            if (TMPELifecycle.InGameOrEditor()) {
                Log.Info("OptionsManager.UpdateJunctionRestrictionsManager()");
                JunctionRestrictionsManager.Instance.UpdateAllDefaults();
            }
        }

        /// <summary>
        /// When dedicated turning lane policy is toggled, all junctions
        /// need to be checked and updated if necessary.
        /// </summary>
        internal static void UpdateDedicatedTurningLanes() {
            if (TMPELifecycle.Instance.Deserializing) {
                Log._Debug("SavedGameOptions.Instance.UpdateDedicatedTurningLanes() - Waiting for deserialisation");
                _needUpdateDedicatedTurningLanes = true;
                return;
            }

            if (TMPELifecycle.InGameOrEditor()) {
                Log.Info("OptionsManager.UpdateDedicatedTurningLanes()");
                LaneArrowManager.Instance.UpdateDedicatedTurningLanePolicy(true);
            }
        }

        /// <summary>
        /// When lane routing feature activation is toggled, all junctions
        /// need to be updated to reflect the change.
        /// </summary>
        internal static void UpdateRoutingManager() {
            if (TMPELifecycle.Instance.Deserializing) {
                Log._Debug("OptionsManager.UpdateRoutingManager() - Ignoring; Deserialising");
                return;
            }

            if (TMPELifecycle.InGameOrEditor()) {
                Log.Info("OptionsManager.UpdateRoutingManager()");
                RoutingManager.Instance.RequestFullRecalculation();
            }
        }
    }
}
