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

            opt.Load(defaultVal ? (byte)1 : (byte)0);
        }

        public bool LoadData(byte[] data) {
            try {
                Log.Info($"OptionsManager.LoadData: {data.Length} bytes");

                OptionsGeneralTab.SetSimulationAccuracy(ConvertToSimulationAccuracy(LoadByte(data, idx: 0)));
                // skip Options.setLaneChangingRandomization(options[1]);
                GameplayTab.SetRecklessDrivers(LoadByte(data, idx: 2));
                PoliciesTab.SetRelaxedBusses(LoadBool(data, idx: 3));
                OptionsOverlaysTab.SetNodesOverlay(LoadBool(data, idx: 4));
                PoliciesTab.SetMayEnterBlockedJunctions(LoadBool(data, idx: 5));
                GameplayTab.SetAdvancedAi(LoadBool(data, idx: 6));
                PoliciesTab.SetHighwayRules(LoadBool(data, idx: 7));
                OptionsOverlaysTab.SetPrioritySignsOverlay(LoadBool(data, idx: 8));
                OptionsOverlaysTab.SetTimedLightsOverlay(LoadBool(data, idx: 9));
                OptionsOverlaysTab.SetSpeedLimitsOverlay(LoadBool(data, idx: 10));
                OptionsOverlaysTab.SetVehicleRestrictionsOverlay(LoadBool(data, idx: 11));
                GameplayTab.SetStrongerRoadConditionEffects(LoadBool(data, idx: 12));
                PoliciesTab.SetAllowUTurns(LoadBool(data, idx: 13));
                PoliciesTab.SetAllowLaneChangesWhileGoingStraight(LoadBool(data, idx: 14));
                GameplayTab.SetDisableDespawning(!LoadBool(data, idx: 15)); // inverted
                // skip Options.setDynamicPathRecalculation(data[16] == (byte)1);
                OptionsOverlaysTab.SetConnectedLanesOverlay(LoadBool(data, idx: 17));
                PoliciesTab.SetPrioritySignsEnabled(LoadBool(data, idx: 18));
                PoliciesTab.SetTimedLightsEnabled(LoadBool(data, idx: 19));
                OptionsMaintenanceTab.SetCustomSpeedLimitsEnabled(LoadBool(data, idx: 20));
                OptionsMaintenanceTab.SetVehicleRestrictionsEnabled(LoadBool(data, idx: 21));
                OptionsMaintenanceTab.SetLaneConnectorEnabled(LoadBool(data, idx: 22));
                OptionsOverlaysTab.SetJunctionRestrictionsOverlay(LoadBool(data, idx: 23));
                OptionsMaintenanceTab.SetJunctionRestrictionsEnabled(LoadBool(data, idx: 24));
                GameplayTab.SetProhibitPocketCars(LoadBool(data, idx: 25));
                PoliciesTab.SetPreferOuterLane(LoadBool(data, idx: 26));
                GameplayTab.SetIndividualDrivingStyle(LoadBool(data, idx: 27));
                PoliciesTab.SetEvacBussesMayIgnoreRules(LoadBool(data, idx: 28));
                OptionsGeneralTab.SetInstantEffects(LoadBool(data, idx: 29));
                OptionsMaintenanceTab.SetParkingRestrictionsEnabled(LoadBool(data, idx: 30));
                OptionsOverlaysTab.SetParkingRestrictionsOverlay(LoadBool(data, idx: 31));
                PoliciesTab.SetBanRegularTrafficOnBusLanes(LoadBool(data, idx: 32));
                OptionsMaintenanceTab.SetShowPathFindStats(LoadBool(data, idx: 33));
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
                OptionsMaintenanceTab.SetTurnOnRedEnabled(LoadBool(data, idx: 38));
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
                return true;
            }
            catch (Exception ex) {
                ex.LogException();
                return false;
            }
        }

        public byte[] SaveData(ref bool success) {

            // Remember to update this when adding new options (lastIdx + 1)
            var save = new byte[59];

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
                save[12] = (byte)(Options.strongerRoadConditionEffects ? 1 : 0);
                save[13] = (byte)(Options.allowUTurns ? 1 : 0);
                save[14] = (byte)(Options.allowLaneChangesWhileGoingStraight ? 1 : 0);
                save[15] = (byte)(Options.disableDespawning ? 0 : 1);
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
                save[27] = (byte)(Options.individualDrivingStyle ? 1 : 0);
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

                save[58] = (byte)Options.SavegamePathfinderEdition;

                return save;
            }
            catch (Exception ex) {
                ex.LogException();
                return save; // try and salvage some of the settings
            }
        }
    }
}