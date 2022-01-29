namespace TrafficManager.Manager.Impl {
    using CSUtil.Commons;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.State;
    using TrafficManager.UI.Helpers;
    using TrafficManager.Lifecycle;
    using JetBrains.Annotations;

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
        private static void ToCheckbox([NotNull] byte[] data, uint idx, ILegacySerializableOption opt) {
            if (data.Length > idx) {
                opt.Load(data[idx]);
            }
        }

        public bool LoadData(byte[] data) {
            OptionsGeneralTab.SetSimulationAccuracy(ConvertToSimulationAccuracy(LoadByte(data, idx: 0)));
            // skip Options.setLaneChangingRandomization(options[1]);
            OptionsGameplayTab.SetRecklessDrivers(LoadByte(data, idx: 2));
            OptionsVehicleRestrictionsTab.SetRelaxedBusses(LoadBool(data, idx: 3));
            OptionsOverlaysTab.SetNodesOverlay(LoadBool(data, idx: 4));
            OptionsVehicleRestrictionsTab.SetMayEnterBlockedJunctions(LoadBool(data, idx: 5));
            OptionsGameplayTab.SetAdvancedAi(LoadBool(data, idx: 6));
            OptionsVehicleRestrictionsTab.SetHighwayRules(LoadBool(data, idx: 7));
            OptionsOverlaysTab.SetPrioritySignsOverlay(LoadBool(data, idx: 8));
            OptionsOverlaysTab.SetTimedLightsOverlay(LoadBool(data, idx: 9));
            OptionsOverlaysTab.SetSpeedLimitsOverlay(LoadBool(data, idx: 10));
            OptionsOverlaysTab.SetVehicleRestrictionsOverlay(LoadBool(data, idx: 11));
            OptionsGameplayTab.SetStrongerRoadConditionEffects(LoadBool(data, idx: 12));
            OptionsVehicleRestrictionsTab.SetAllowUTurns(LoadBool(data, idx: 13));
            OptionsVehicleRestrictionsTab.SetAllowLaneChangesWhileGoingStraight(LoadBool(data, idx: 14));
            OptionsGameplayTab.SetDisableDespawning(!LoadBool(data, idx: 15)); // inverted
            // skip Options.setDynamicPathRecalculation(data[16] == (byte)1);
            OptionsOverlaysTab.SetConnectedLanesOverlay(LoadBool(data, idx: 17));
            OptionsVehicleRestrictionsTab.SetPrioritySignsEnabled(LoadBool(data, idx: 18));
            OptionsVehicleRestrictionsTab.SetTimedLightsEnabled(LoadBool(data, idx: 19));
            OptionsMaintenanceTab.SetCustomSpeedLimitsEnabled(LoadBool(data, idx: 20));
            OptionsMaintenanceTab.SetVehicleRestrictionsEnabled(LoadBool(data, idx: 21));
            OptionsMaintenanceTab.SetLaneConnectorEnabled(LoadBool(data, idx: 22));
            OptionsOverlaysTab.SetJunctionRestrictionsOverlay(LoadBool(data, idx: 23));
            OptionsMaintenanceTab.SetJunctionRestrictionsEnabled(LoadBool(data, idx: 24));
            OptionsGameplayTab.SetProhibitPocketCars(LoadBool(data, idx: 25));
            OptionsVehicleRestrictionsTab.SetPreferOuterLane(LoadBool(data, idx: 26));
            OptionsGameplayTab.SetIndividualDrivingStyle(LoadBool(data, idx: 27));
            OptionsVehicleRestrictionsTab.SetEvacBussesMayIgnoreRules(LoadBool(data, idx: 28));
            OptionsGeneralTab.SetInstantEffects(LoadBool(data, idx: 29));
            OptionsMaintenanceTab.SetParkingRestrictionsEnabled(LoadBool(data, idx: 30));
            OptionsOverlaysTab.SetParkingRestrictionsOverlay(LoadBool(data, idx: 31));
            OptionsVehicleRestrictionsTab.SetBanRegularTrafficOnBusLanes(LoadBool(data, idx: 32));
            OptionsMaintenanceTab.SetShowPathFindStats(LoadBool(data, idx: 33));
            OptionsGameplayTab.SetDLSPercentage(LoadByte(data, idx: 34));

            if (data.Length > 35) {
                try {
                    OptionsVehicleRestrictionsTab.SetVehicleRestrictionsAggression(
                        (VehicleRestrictionsAggression)data[35]);
                }
                catch (Exception e) {
                    Log.Warning(
                        $"Skipping invalid value {data[35]} for vehicle restrictions aggression");
                }
            }

            OptionsVehicleRestrictionsTab.SetTrafficLightPriorityRules(LoadBool(data, idx: 36));
            OptionsGameplayTab.SetRealisticPublicTransport(LoadBool(data, idx: 37));
            OptionsMaintenanceTab.SetTurnOnRedEnabled(LoadBool(data, idx: 38));
            OptionsVehicleRestrictionsTab.SetAllowNearTurnOnRed(LoadBool(data, idx: 39));
            OptionsVehicleRestrictionsTab.SetAllowFarTurnOnRed(LoadBool(data, idx: 40));
            OptionsVehicleRestrictionsTab.SetAddTrafficLightsIfApplicable(LoadBool(data, idx: 41));

            ToCheckbox(data, idx: 42, OptionsMassEditTab.RoundAboutQuickFix_StayInLaneMainR);
            ToCheckbox(data, idx: 43, OptionsMassEditTab.RoundAboutQuickFix_StayInLaneNearRabout);
            ToCheckbox(data, idx: 44, OptionsMassEditTab.RoundAboutQuickFix_DedicatedExitLanes);
            ToCheckbox(data, idx: 45, OptionsMassEditTab.RoundAboutQuickFix_NoCrossMainR);
            ToCheckbox(data, idx: 46, OptionsMassEditTab.RoundAboutQuickFix_NoCrossYieldR);
            ToCheckbox(data, idx: 47, OptionsMassEditTab.RoundAboutQuickFix_PrioritySigns);

            ToCheckbox(data, idx: 48, OptionsMassEditTab.PriorityRoad_CrossMainR);
            ToCheckbox(data, idx: 49, OptionsMassEditTab.PriorityRoad_AllowLeftTurns);
            ToCheckbox(data, idx: 50, OptionsMassEditTab.PriorityRoad_EnterBlockedYeild);
            ToCheckbox(data, idx: 51, OptionsMassEditTab.PriorityRoad_StopAtEntry);

            ToCheckbox(data, idx: 52, OptionsMassEditTab.RoundAboutQuickFix_KeepClearYieldR);
            ToCheckbox(data, idx: 53, OptionsMassEditTab.RoundAboutQuickFix_RealisticSpeedLimits);
            ToCheckbox(data, idx: 54, OptionsMassEditTab.RoundAboutQuickFix_ParkingBanMainR);
            ToCheckbox(data, idx: 55, OptionsMassEditTab.RoundAboutQuickFix_ParkingBanYieldR);

            ToCheckbox(data, idx: 56, OptionsVehicleRestrictionsTab.NoDoubleCrossings);
            ToCheckbox(data, idx: 57, OptionsVehicleRestrictionsTab.DedicatedTurningLanes);
            return true;
        }

        public byte[] SaveData(ref bool success) {
            var save = new byte[57];

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

            save[56] = OptionsVehicleRestrictionsTab.NoDoubleCrossings.Save();
            save[57] = OptionsVehicleRestrictionsTab.DedicatedTurningLanes.Save();

            return save;
        }
    }
}