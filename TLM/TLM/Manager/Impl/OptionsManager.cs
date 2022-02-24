namespace TrafficManager.Manager.Impl {
    using CSUtil.Commons;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.State;
    using TrafficManager.UI.Helpers;
    using TrafficManager.Lifecycle;
    using JetBrains.Annotations;
    using TrafficManager.Util;
    using System.Reflection;

    public class OptionsManager
        : AbstractCustomManager,
          IOptionsManager {
        // TODO I contain ugly code
        public static OptionsManager Instance = new OptionsManager();

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log.NotImpl("InternalPrintDebugInfo for OptionsManager");
        }

        /// <summary>
        /// Converts value to SimulationAccuracy
        /// </summary>
        /// <param name="value">Old value</param>
        /// <returns>SimulationAccuracy value</returns>
        private static SimulationAccuracy ConvertToSimulationAccuracy(byte value) {
            return SimulationAccuracy.MaxValue - value;
        }

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
        /// Converts SimulationAccuracy to SimulationAccuracy
        /// </summary>
        /// <param name="value">SimulationAccuracy value</param>
        /// <returns>byte representation of value (backward compatible)</returns>
        private static byte ConvertFromSimulationAccuracy(SimulationAccuracy value) {
            return (byte)(SimulationAccuracy.MaxValue - value);
        }

        public bool MayPublishSegmentChanges() {
            return Options.instantEffects && TMPELifecycle.InGameOrEditor() &&
                !TMPELifecycle.Instance.Deserializing;
        }

        // Takes a bool from data and sets it in `out result`
        private static bool LoadBool([NotNull] byte[] data, uint idx, bool defaultVal = false) {
            if (data.Length > idx) {
                var result = data[idx] == 1;
                return result;
            }

            return defaultVal;
        }

        private static byte LoadByte([NotNull] byte[] data, uint idx, byte defaultVal = 0) {
            if (data.Length > idx) {
                var result = data[idx];
                return result;
            }

            return defaultVal;
        }

        /// <summary>Load LegacySerializableOption bool.</summary>
        private static void ToCheckbox([NotNull] byte[] data, uint idx, ILegacySerializableOption opt, bool defaultVal = false) {
            if (data.Length > idx) {
                opt.Load(data[idx]);
                return;
            }

        /// <summary>Load LegacySerializableOption byte.</summary>
        private static void ToSlider(byte[] data, uint idx, ILegacySerializableOption opt, byte defaultVal = 0)
            => opt.Load(LoadByte(data, idx, defaultVal));

        public bool LoadData(byte[] data) {
            try {
                Options.Available = false;

                Log.Info($"OptionsManager.LoadData: {data.Length} bytes");

                GeneralTab_SimulationGroup.SetSimulationAccuracy(ConvertToSimulationAccuracy(LoadByte(data, idx: 0)));
                // skip Options.setLaneChangingRandomization(options[1]);
                GameplayTab_VehicleBehaviourGroup.SetRecklessDrivers(LoadByte(data, idx: 2));
                PoliciesTab.SetRelaxedBusses(LoadBool(data, idx: 3));
                OverlaysTab.SetNodesOverlay(LoadBool(data, idx: 4));
                PoliciesTab.SetMayEnterBlockedJunctions(LoadBool(data, idx: 5));
                GameplayTab.SetAdvancedAi(LoadBool(data, idx: 6));
                PoliciesTab.SetHighwayRules(LoadBool(data, idx: 7));
                OverlaysTab.SetPrioritySignsOverlay(LoadBool(data, idx: 8));
                OverlaysTab.SetTimedLightsOverlay(LoadBool(data, idx: 9));
                OverlaysTab.SetSpeedLimitsOverlay(LoadBool(data, idx: 10));
                OverlaysTab.SetVehicleRestrictionsOverlay(LoadBool(data, idx: 11));
                ToCheckbox(data, idx: 12, GameplayTab_VehicleBehaviourGroup.StrongerRoadConditionEffects, false);
                PoliciesTab.SetAllowUTurns(LoadBool(data, idx: 13));
                PoliciesTab.SetAllowLaneChangesWhileGoingStraight(LoadBool(data, idx: 14));
                GameplayTab_VehicleBehaviourGroup.DisableDespawning.Value = !LoadBool(data, idx: 15, true); // inverted
                // skip Options.setDynamicPathRecalculation(data[16] == (byte)1);
                OverlaysTab.SetConnectedLanesOverlay(LoadBool(data, idx: 17));
                PoliciesTab.SetPrioritySignsEnabled(LoadBool(data, idx: 18));
                PoliciesTab.SetTimedLightsEnabled(LoadBool(data, idx: 19));
                MaintenanceTab.SetCustomSpeedLimitsEnabled(LoadBool(data, idx: 20));
                MaintenanceTab.SetVehicleRestrictionsEnabled(LoadBool(data, idx: 21));
                MaintenanceTab.SetLaneConnectorEnabled(LoadBool(data, idx: 22));
                OverlaysTab.SetJunctionRestrictionsOverlay(LoadBool(data, idx: 23));
                MaintenanceTab.SetJunctionRestrictionsEnabled(LoadBool(data, idx: 24));
                GameplayTab.SetProhibitPocketCars(LoadBool(data, idx: 25));
                PoliciesTab.SetPreferOuterLane(LoadBool(data, idx: 26));
                ToCheckbox(data, idx: 27, GameplayTab_VehicleBehaviourGroup.IndividualDrivingStyle, false);
                PoliciesTab.SetEvacBussesMayIgnoreRules(LoadBool(data, idx: 28));
                ToCheckbox(data, idx: 29, GeneralTab_SimulationGroup.InstantEffects, true);
                MaintenanceTab.SetParkingRestrictionsEnabled(LoadBool(data, idx: 30));
                OverlaysTab.SetParkingRestrictionsOverlay(LoadBool(data, idx: 31));
                PoliciesTab.SetBanRegularTrafficOnBusLanes(LoadBool(data, idx: 32));
                MaintenanceTab.SetShowPathFindStats(LoadBool(data, idx: 33));
                GameplayTab.SetDLSPercentage(LoadByte(data, idx: 34));

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

                PoliciesTab.SetTrafficLightPriorityRules(LoadBool(data, idx: 36));
                GameplayTab.SetRealisticPublicTransport(LoadBool(data, idx: 37));
                MaintenanceTab.SetTurnOnRedEnabled(LoadBool(data, idx: 38));
                PoliciesTab.SetAllowNearTurnOnRed(LoadBool(data, idx: 39));
                PoliciesTab.SetAllowFarTurnOnRed(LoadBool(data, idx: 40));
                PoliciesTab.SetAddTrafficLightsIfApplicable(LoadBool(data, idx: 41));

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
                ToCheckbox(data, idx: 57, PoliciesTab.DedicatedTurningLanes);

                Options.SavegamePathfinderEdition = LoadByte(data, idx: 58, defaultVal: 0);

                ToCheckbox(data, idx: 59, OverlaysTab.ShowDefaultSpeedSubIcon, false);

                Options.Available = true;

                return true;
            }
            catch (Exception ex) {
                ex.LogException();

                // even though there was error, the options are now available for querying
                Options.Available = true;

                return false;
            }
        }

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
                save[29] = (byte)(Options.instantEffects ? 1 : 0);
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
                save[57] = PoliciesTab.DedicatedTurningLanes.Save();

                save[58] = Options.SavegamePathfinderEdition;

                save[59] = OverlaysTab.ShowDefaultSpeedSubIcon.Save();

                return save;
            }
            catch (Exception ex) {
                ex.LogException();
                return save; // try and salvage some of the settings
            }
        }
    }
}
