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
    using ICities;

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
            if (!Options.Available) {
                value = default;
                return false;
            }

            var field = typeof(Options).GetField(optionName, BindingFlags.Static | BindingFlags.Public);

            if (field == null || field.FieldType is not TVal) {
                value = default;
                return false;
            }

            value = (TVal)field.GetValue(null);
            return true;
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

        /// <summary>
        /// Converts value to SimulationAccuracy
        /// </summary>
        /// <param name="value">Old value</param>
        /// <returns>SimulationAccuracy value</returns>
        private static SimulationAccuracy ConvertToSimulationAccuracy(byte value) {
            return SimulationAccuracy.MaxValue - value;
        }

        /// <summary>
        /// Converts SimulationAccuracy to SimulationAccuracy
        /// </summary>
        /// <param name="value">SimulationAccuracy value</param>
        /// <returns>byte representation of value (backward compatible)</returns>
        private static byte ConvertFromSimulationAccuracy(SimulationAccuracy value) {
            return (byte)(SimulationAccuracy.MaxValue - value);
        }

        private static bool LoadBool([NotNull] byte[] data, uint idx, bool defaultVal = false)
            => (data.Length > idx) ? data[idx] == 1 : defaultVal;

        private static byte LoadByte([NotNull] byte[] data, uint idx, byte defaultVal = 0)
            => (data.Length > idx) ? data[idx] : defaultVal;

        /// <summary>Load LegacySerializableOption bool.</summary>
        private static void ToCheckbox(byte[] data, uint idx, ILegacySerializableOption opt, bool defaultVal = false)
            => opt.Load((byte)(LoadBool(data, idx, defaultVal) ? 1 : 0));

        /// <summary>Load LegacySerializableOption byte.</summary>
        private static void ToSlider(byte[] data, uint idx, ILegacySerializableOption opt, byte defaultVal = 0)
            => opt.Load(LoadByte(data, idx, defaultVal));

        /// <summary>
        /// Restores the mod options based on supplied <paramref name="data"/>.
        /// </summary>
        /// <param name="data">Byte array obtained from the savegame.</param>
        /// <returns>Returns <c>true</c> if successful, otherwise <c>false</c>.</returns>
        /// <remarks>Applies default values if the data for an option does not exist.</remarks>
        public bool LoadData(byte[] data) {
            try {
                Options.Available = false;

                Log.Info($"OptionsManager.LoadData: {data.Length} bytes");

                GeneralTab_SimulationGroup.SetSimulationAccuracy(ConvertToSimulationAccuracy(LoadByte(data, idx: 0)));
                // skip Options.setLaneChangingRandomization(options[1]);
                GameplayTab_VehicleBehaviourGroup.SetRecklessDrivers(LoadByte(data, idx: 2));
                ToCheckbox(data, idx: 3, PoliciesTab_AtJunctionsGroup.RelaxedBusses, true);
                ToCheckbox(data, idx: 4, OverlaysTab_OverlaysGroup.NodesOverlay, false);
                ToCheckbox(data, idx: 5, PoliciesTab_AtJunctionsGroup.AllowEnterBlockedJunctions, false);
                ToCheckbox(data, idx: 6, GameplayTab_AIGroups.AdvancedAI, false);
                PoliciesTab.SetHighwayRules(LoadBool(data, idx: 7));
                ToCheckbox(data, idx: 8, OverlaysTab_OverlaysGroup.PrioritySignsOverlay, false);
                ToCheckbox(data, idx: 9, OverlaysTab_OverlaysGroup.TimedLightsOverlay, false);
                ToCheckbox(data, idx: 10, OverlaysTab_OverlaysGroup.SpeedLimitsOverlay, false);
                ToCheckbox(data, idx: 11, OverlaysTab_OverlaysGroup.VehicleRestrictionsOverlay, false);
                ToCheckbox(data, idx: 12, GameplayTab_VehicleBehaviourGroup.StrongerRoadConditionEffects, false);
                ToCheckbox(data, idx: 13, PoliciesTab_AtJunctionsGroup.AllowUTurns, false);
                ToCheckbox(data, idx: 14, PoliciesTab_AtJunctionsGroup.AllowLaneChangesWhileGoingStraight, true);
                GameplayTab_VehicleBehaviourGroup.DisableDespawning.Value = !LoadBool(data, idx: 15, true); // inverted
                // skip Options.setDynamicPathRecalculation(data[16] == (byte)1);
                ToCheckbox(data, idx: 17, OverlaysTab_OverlaysGroup.ConnectedLanesOverlay, false);
                ToCheckbox(data, idx: 18, MaintenanceTab_FeaturesGroup.PrioritySignsEnabled, true);
                ToCheckbox(data, idx: 19, MaintenanceTab_FeaturesGroup.TimedLightsEnabled, true);
                ToCheckbox(data, idx: 20, MaintenanceTab_FeaturesGroup.CustomSpeedLimitsEnabled, true);
                ToCheckbox(data, idx: 21, MaintenanceTab_FeaturesGroup.VehicleRestrictionsEnabled, true);
                ToCheckbox(data, idx: 22, MaintenanceTab_FeaturesGroup.LaneConnectorEnabled, true);
                ToCheckbox(data, idx: 23, OverlaysTab_OverlaysGroup.JunctionRestrictionsOverlay, false);
                ToCheckbox(data, idx: 24, MaintenanceTab_FeaturesGroup.JunctionRestrictionsEnabled, true);
                ToCheckbox(data, idx: 25, GameplayTab_AIGroups.ParkingAI, false);
                PoliciesTab.SetPreferOuterLane(LoadBool(data, idx: 26));
                ToCheckbox(data, idx: 27, GameplayTab_VehicleBehaviourGroup.IndividualDrivingStyle, false);
                PoliciesTab.SetEvacBussesMayIgnoreRules(LoadBool(data, idx: 28));
                // skip ToCheckbox(data, idx: 29, GeneralTab_SimulationGroup.InstantEffects, true);
                ToCheckbox(data, idx: 30, MaintenanceTab_FeaturesGroup.ParkingRestrictionsEnabled, true);
                ToCheckbox(data, idx: 31, OverlaysTab_OverlaysGroup.ParkingRestrictionsOverlay, false);
                PoliciesTab.SetBanRegularTrafficOnBusLanes(LoadBool(data, idx: 32));
                ToCheckbox(data, idx: 33, OverlaysTab_OverlaysGroup.ShowPathFindStats, VersionUtil.IS_DEBUG);
                ToSlider(data, idx: 34, GameplayTab_AIGroups.AltLaneSelectionRatio, 0);

                if (data.Length > 35) {
                    try {
                        PoliciesTab.SetVehicleRestrictionsAggression(
                            (VehicleRestrictionsAggression)data[35]);
                    }
                    catch (Exception e) {
                        Log.Warning(
                            $"Skipping invalid value {data[35]} for vehicle restrictions aggression");
                    }
                }

                ToCheckbox(data, idx: 36, PoliciesTab_AtJunctionsGroup.TrafficLightPriorityRules, false);
                ToCheckbox(data, idx: 37, GameplayTab_AIGroups.RealisticPublicTransport, false);
                ToCheckbox(data, idx: 38, MaintenanceTab_FeaturesGroup.TurnOnRedEnabled, true);
                ToCheckbox(data, idx: 39, PoliciesTab_AtJunctionsGroup.AllowNearTurnOnRed, false);
                ToCheckbox(data, idx: 40, PoliciesTab_AtJunctionsGroup.AllowFarTurnOnRed, false);
                ToCheckbox(data, idx: 41, PoliciesTab_AtJunctionsGroup.AutomaticallyAddTrafficLightsIfApplicable, true);

                ToCheckbox(data, idx: 42, OptionsMassEditTab.RoundAboutQuickFix_StayInLaneMainR, true);
                ToCheckbox(data, idx: 43, OptionsMassEditTab.RoundAboutQuickFix_StayInLaneNearRabout, true);
                ToCheckbox(data, idx: 44, OptionsMassEditTab.RoundAboutQuickFix_DedicatedExitLanes, true);
                ToCheckbox(data, idx: 45, OptionsMassEditTab.RoundAboutQuickFix_NoCrossMainR, true);
                ToCheckbox(data, idx: 46, OptionsMassEditTab.RoundAboutQuickFix_NoCrossYieldR);
                ToCheckbox(data, idx: 47, OptionsMassEditTab.RoundAboutQuickFix_PrioritySigns, true);

                ToCheckbox(data, idx: 48, OptionsMassEditTab.PriorityRoad_CrossMainR);
                ToCheckbox(data, idx: 49, OptionsMassEditTab.PriorityRoad_AllowLeftTurns);
                ToCheckbox(data, idx: 50, OptionsMassEditTab.PriorityRoad_EnterBlockedYeild);
                ToCheckbox(data, idx: 51, OptionsMassEditTab.PriorityRoad_StopAtEntry);

                ToCheckbox(data, idx: 52, OptionsMassEditTab.RoundAboutQuickFix_KeepClearYieldR, true);
                ToCheckbox(data, idx: 53, OptionsMassEditTab.RoundAboutQuickFix_RealisticSpeedLimits, false);
                ToCheckbox(data, idx: 54, OptionsMassEditTab.RoundAboutQuickFix_ParkingBanMainR, true);
                ToCheckbox(data, idx: 55, OptionsMassEditTab.RoundAboutQuickFix_ParkingBanYieldR);

                ToCheckbox(data, idx: 56, PoliciesTab.NoDoubleCrossings);
                ToCheckbox(data, idx: 57, PoliciesTab_AtJunctionsGroup.DedicatedTurningLanes);

                Options.SavegamePathfinderEdition = LoadByte(data, idx: 58, defaultVal: 0);

                ToCheckbox(data, idx: 59, OverlaysTab_OverlaysGroup.ShowDefaultSpeedSubIcon, false);

                Options.Available = true;
                return true;
            }
            catch (Exception ex) {
                ex.LogException();

                Options.Available = true;
                return false;
            }
        }

        /// <summary>
        /// Compiles mod options in to a byte array for storage in savegame.
        /// </summary>
        /// <param name="success">Current success state of SaveData operation.</param>
        /// <returns>Returns <c>true</c> if successful, otherwise <c>false</c>.</returns>
        public byte[] SaveData(ref bool success) {

            // Remember to update this when adding new options (lastIdx + 1)
            var save = new byte[60];

            try {
                save[0] = ConvertFromSimulationAccuracy(Options.simulationAccuracy);
                save[1] = 0; // Options.laneChangingRandomization
                save[2] = (byte)Options.recklessDrivers;
                save[3] = (byte)(Options.relaxedBusses ? 1 : 0);
                save[4] = (byte)(Options.nodesOverlay ? 1 : 0);
                save[5] = (byte)(Options.allowEnterBlockedJunctions ? 1 : 0);
                save[6] = (byte)(Options.advancedAI ? 1 : 0);
                save[7] = (byte)(Options.highwayRules ? 1 : 0);
                save[8] = (byte)(Options.prioritySignsOverlay ? 1 : 0);
                save[9] = (byte)(Options.timedLightsOverlay ? 1 : 0);
                save[10] = (byte)(Options.speedLimitsOverlay ? 1 : 0);
                save[11] = (byte)(Options.vehicleRestrictionsOverlay ? 1 : 0);
                save[12] = GameplayTab_VehicleBehaviourGroup.StrongerRoadConditionEffects.Save();
                save[13] = (byte)(Options.allowUTurns ? 1 : 0);
                save[14] = (byte)(Options.allowLaneChangesWhileGoingStraight ? 1 : 0);
                save[15] = (byte)(Options.disableDespawning ? 0 : 1); // inverted
                save[16] = 0; // Options.IsDynamicPathRecalculationActive
                save[17] = (byte)(Options.connectedLanesOverlay ? 1 : 0);
                save[18] = (byte)(Options.prioritySignsEnabled ? 1 : 0);
                save[19] = (byte)(Options.timedLightsEnabled ? 1 : 0);
                save[20] = (byte)(Options.customSpeedLimitsEnabled ? 1 : 0);
                save[21] = (byte)(Options.vehicleRestrictionsEnabled ? 1 : 0);
                save[22] = (byte)(Options.laneConnectorEnabled ? 1 : 0);
                save[23] = (byte)(Options.junctionRestrictionsOverlay ? 1 : 0);
                save[24] = (byte)(Options.junctionRestrictionsEnabled ? 1 : 0);
                save[25] = (byte)(Options.parkingAI ? 1 : 0);
                save[26] = (byte)(Options.preferOuterLane ? 1 : 0);
                save[27] = GameplayTab_VehicleBehaviourGroup.IndividualDrivingStyle.Save();
                save[28] = (byte)(Options.evacBussesMayIgnoreRules ? 1 : 0);
                save[29] = 0; // (byte)(Options.instantEffects ? 1 : 0);
                save[30] = (byte)(Options.parkingRestrictionsEnabled ? 1 : 0);
                save[31] = (byte)(Options.parkingRestrictionsOverlay ? 1 : 0);
                save[32] = (byte)(Options.banRegularTrafficOnBusLanes ? 1 : 0);
                save[33] = (byte)(Options.showPathFindStats ? 1 : 0);
                save[34] = Options.altLaneSelectionRatio;
                save[35] = (byte)Options.vehicleRestrictionsAggression;
                save[36] = (byte)(Options.trafficLightPriorityRules ? 1 : 0);
                save[37] = (byte)(Options.realisticPublicTransport ? 1 : 0);
                save[38] = (byte)(Options.turnOnRedEnabled ? 1 : 0);
                save[39] = (byte)(Options.allowNearTurnOnRed ? 1 : 0);
                save[40] = (byte)(Options.allowFarTurnOnRed ? 1 : 0);
                save[41] = (byte)(Options.automaticallyAddTrafficLightsIfApplicable ? 1 : 0);

                save[42] = OptionsMassEditTab.RoundAboutQuickFix_StayInLaneMainR.Save();
                save[43] = OptionsMassEditTab.RoundAboutQuickFix_StayInLaneNearRabout.Save();
                save[44] = OptionsMassEditTab.RoundAboutQuickFix_DedicatedExitLanes.Save();
                save[45] = OptionsMassEditTab.RoundAboutQuickFix_NoCrossMainR.Save();
                save[46] = OptionsMassEditTab.RoundAboutQuickFix_NoCrossYieldR.Save();
                save[47] = OptionsMassEditTab.RoundAboutQuickFix_PrioritySigns.Save();

                save[48] = OptionsMassEditTab.PriorityRoad_CrossMainR.Save();
                save[49] = OptionsMassEditTab.PriorityRoad_AllowLeftTurns.Save();
                save[50] = OptionsMassEditTab.PriorityRoad_EnterBlockedYeild.Save();
                save[51] = OptionsMassEditTab.PriorityRoad_StopAtEntry.Save();

                save[52] = OptionsMassEditTab.RoundAboutQuickFix_KeepClearYieldR.Save();
                save[53] = OptionsMassEditTab.RoundAboutQuickFix_RealisticSpeedLimits.Save();
                save[54] = OptionsMassEditTab.RoundAboutQuickFix_ParkingBanMainR.Save();
                save[55] = OptionsMassEditTab.RoundAboutQuickFix_ParkingBanYieldR.Save();

                save[56] = PoliciesTab.NoDoubleCrossings.Save();
                save[57] = PoliciesTab_AtJunctionsGroup.DedicatedTurningLanes.Save();

                save[58] = Options.SavegamePathfinderEdition;

                save[59] = OverlaysTab_OverlaysGroup.ShowDefaultSpeedSubIcon.Save();

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
        internal static void RebuildMenu() {
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

            RoadUI.Instance.ReloadTexturesWithTranslation();
            TrafficLightTextures.Instance.ReloadTexturesWithTranslation();
            TMPELifecycle.Instance.TranslationDatabase.ReloadTutorialTranslations();
            TMPELifecycle.Instance.TranslationDatabase.ReloadGuideTranslations();
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
                Log._Debug("Options.UpdateJunctionRestrictionsManager() - Waiting for deserialisation");
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
                Log._Debug("Options.UpdateDedicatedTurningLanes() - Waiting for deserialisation");
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
