namespace TrafficManager.Manager.Impl {
    using System;
    using API.Manager;
    using API.Traffic.Enums;
    using CSUtil.Commons;
    using State;
    using UI.Helpers;

    public class OptionsManager
        : AbstractCustomManager,
          IOptionsManager
    {
        // TODO I contain ugly code
        public static OptionsManager Instance = new OptionsManager();

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log.NotImpl("InternalPrintDebugInfo for OptionsManager");
        }

        public bool MayPublishSegmentChanges() {
            return Options.instantEffects && !SerializableDataExtension.StateLoading;
        }

        public bool LoadData(byte[] data) {
            if (data.Length >= 1) {
                // Options.setSimAccuracy(data[0]);
            }

            if (data.Length >= 2) {
                // Options.setLaneChangingRandomization(options[1]);
            }

            if (data.Length >= 3) {
                OptionsGameplayTab.SetRecklessDrivers(data[2]);
            }

            if (data.Length >= 4) {
                OptionsVehicleRestrictionsTab.SetRelaxedBusses(data[3] == 1);
            }

            if (data.Length >= 5) {
                OptionsOverlaysTab.SetNodesOverlay(data[4] == 1);
            }

            if (data.Length >= 6) {
                OptionsVehicleRestrictionsTab.SetMayEnterBlockedJunctions(data[5] == 1);
            }

            if (data.Length >= 7) {
                OptionsGameplayTab.SetAdvancedAi(data[6] == 1);
            }

            if (data.Length >= 8) {
                OptionsVehicleRestrictionsTab.SetHighwayRules(data[7] == 1);
            }

            if (data.Length >= 9) {
                OptionsOverlaysTab.SetPrioritySignsOverlay(data[8] == 1);
            }

            if (data.Length >= 10) {
                OptionsOverlaysTab.SetTimedLightsOverlay(data[9] == 1);
            }

            if (data.Length >= 11) {
                OptionsOverlaysTab.SetSpeedLimitsOverlay(data[10] == 1);
            }

            if (data.Length >= 12) {
                OptionsOverlaysTab.SetVehicleRestrictionsOverlay(data[11] == 1);
            }

            if (data.Length >= 13) {
                OptionsGameplayTab.SetStrongerRoadConditionEffects(data[12] == 1);
            }

            if (data.Length >= 14) {
                OptionsVehicleRestrictionsTab.SetAllowUTurns(data[13] == 1);
            }

            if (data.Length >= 15) {
                OptionsVehicleRestrictionsTab.SetAllowLaneChangesWhileGoingStraight(data[14] == 1);
            }

            if (data.Length >= 16) {
                OptionsGameplayTab.SetDisableDespawning(data[15] != 1);
            }

            if (data.Length >= 17) {
                // Options.setDynamicPathRecalculation(data[16] == (byte)1);
            }

            if (data.Length >= 18) {
                OptionsOverlaysTab.SetConnectedLanesOverlay(data[17] == 1);
            }

            if (data.Length >= 19) {
                OptionsVehicleRestrictionsTab.SetPrioritySignsEnabled(data[18] == 1);
            }

            if (data.Length >= 20) {
                OptionsVehicleRestrictionsTab.SetTimedLightsEnabled(data[19] == 1);
            }

            if (data.Length >= 21) {
                OptionsMaintenanceTab.SetCustomSpeedLimitsEnabled(data[20] == 1);
            }

            if (data.Length >= 22) {
                OptionsMaintenanceTab.SetVehicleRestrictionsEnabled(data[21] == 1);
            }

            if (data.Length >= 23) {
                OptionsMaintenanceTab.SetLaneConnectorEnabled(data[22] == 1);
            }

            if (data.Length >= 24) {
                OptionsOverlaysTab.SetJunctionRestrictionsOverlay(data[23] == 1);
            }

            if (data.Length >= 25) {
                OptionsMaintenanceTab.SetJunctionRestrictionsEnabled(data[24] == 1);
            }

            if (data.Length >= 26) {
                OptionsGameplayTab.SetProhibitPocketCars(data[25] == 1);
            }

            if (data.Length >= 27) {
                OptionsVehicleRestrictionsTab.SetPreferOuterLane(data[26] == 1);
            }

            if (data.Length >= 28) {
                OptionsGameplayTab.SetIndividualDrivingStyle(data[27] == 1);
            }

            if (data.Length >= 29) {
                OptionsVehicleRestrictionsTab.SetEvacBussesMayIgnoreRules(data[28] == 1);
            }

            if (data.Length >= 30) {
                OptionsGeneralTab.SetInstantEffects(data[29] == 1);
            }

            if (data.Length >= 31) {
                OptionsMaintenanceTab.SetParkingRestrictionsEnabled(data[30] == 1);
            }

            if (data.Length >= 32) {
                OptionsOverlaysTab.SetParkingRestrictionsOverlay(data[31] == 1);
            }

            if (data.Length >= 33) {
                OptionsVehicleRestrictionsTab.SetBanRegularTrafficOnBusLanes(data[32] == 1);
            }

            if (data.Length >= 34) {
                OptionsMaintenanceTab.SetShowPathFindStats(data[33] == 1);
            }

            if (data.Length >= 35) {
                OptionsGameplayTab.SetDLSPercentage(data[34]);
            }

            if (data.Length >= 36) {
                try {
                    OptionsVehicleRestrictionsTab.SetVehicleRestrictionsAggression(
                        (VehicleRestrictionsAggression)data[35]);
                }
                catch (Exception e) {
                    Log.Warning(
                        $"Skipping invalid value {data[35]} for vehicle restrictions aggression");
                }
            }

            if (data.Length >= 37) {
                OptionsVehicleRestrictionsTab.SetTrafficLightPriorityRules(data[36] == 1);
            }

            if (data.Length >= 38) {
                OptionsGameplayTab.SetRealisticPublicTransport(data[37] == 1);
            }

            if (data.Length >= 39) {
                OptionsMaintenanceTab.SetTurnOnRedEnabled(data[38] == 1);
            }

            if (data.Length >= 40) {
                OptionsVehicleRestrictionsTab.SetAllowNearTurnOnRed(data[39] == 1);
            }

            if (data.Length >= 41) {
                OptionsVehicleRestrictionsTab.SetAllowFarTurnOnRed(data[40] == 1);
            }

            if (data.Length >= 42) {
                OptionsVehicleRestrictionsTab.SetAddTrafficLightsIfApplicable(data[41] == 1);
            }

            Func<int, ISerializableOptionBase, int> loadBool = (idx, opt) => {
                if (data.Length > idx) {
                    opt.Load(data[idx]);
                }
                return idx + 1;
            };

            int index = 42;
            index = loadBool(index, OptionsMassEditTab.rabout_StayInLaneMainR);
            index = loadBool(index, OptionsMassEditTab.rabout_StayInLaneNearRabout);
            index = loadBool(index, OptionsMassEditTab.rabout_DedicatedExitLanes);
            index = loadBool(index, OptionsMassEditTab.rabout_NoCrossMainR);
            index = loadBool(index, OptionsMassEditTab.rabout_NoCrossYeildR);
            index = loadBool(index, OptionsMassEditTab.rabout_PrioritySigns);
            index = loadBool(index, OptionsMassEditTab.avn_NoCrossMainR);
            return true;
        }

        public byte[] SaveData(ref bool success) {
            return new byte[] {
                0, // Options.simAccuracy,
                0, // Options.laneChangingRandomization,
                (byte)Options.recklessDrivers,
                (byte)(Options.relaxedBusses ? 1 : 0),
                (byte)(Options.nodesOverlay ? 1 : 0),
                (byte)(Options.allowEnterBlockedJunctions ? 1 : 0),
                (byte)(Options.advancedAI ? 1 : 0),
                (byte)(Options.highwayRules ? 1 : 0),
                (byte)(Options.prioritySignsOverlay ? 1 : 0),
                (byte)(Options.timedLightsOverlay ? 1 : 0),
                (byte)(Options.speedLimitsOverlay ? 1 : 0),
                (byte)(Options.vehicleRestrictionsOverlay ? 1 : 0),
                (byte)(Options.strongerRoadConditionEffects ? 1 : 0),
                (byte)(Options.allowUTurns ? 1 : 0),
                (byte)(Options.allowLaneChangesWhileGoingStraight ? 1 : 0),
                (byte)(Options.disableDespawning ? 0 : 1),
                0, // Options.IsDynamicPathRecalculationActive()
                (byte)(Options.connectedLanesOverlay ? 1 : 0),
                (byte)(Options.prioritySignsEnabled ? 1 : 0),
                (byte)(Options.timedLightsEnabled ? 1 : 0),
                (byte)(Options.customSpeedLimitsEnabled ? 1 : 0),
                (byte)(Options.vehicleRestrictionsEnabled ? 1 : 0),
                (byte)(Options.laneConnectorEnabled ? 1 : 0),
                (byte)(Options.junctionRestrictionsOverlay ? 1 : 0),
                (byte)(Options.junctionRestrictionsEnabled ? 1 : 0),
                (byte)(Options.parkingAI ? 1 : 0),
                (byte)(Options.preferOuterLane ? 1 : 0),
                (byte)(Options.individualDrivingStyle ? 1 : 0),
                (byte)(Options.evacBussesMayIgnoreRules ? 1 : 0),
                (byte)(Options.instantEffects ? 1 : 0),
                (byte)(Options.parkingRestrictionsEnabled ? 1 : 0),
                (byte)(Options.parkingRestrictionsOverlay ? 1 : 0),
                (byte)(Options.banRegularTrafficOnBusLanes ? 1 : 0),
                (byte)(Options.showPathFindStats ? 1 : 0),
                Options.altLaneSelectionRatio,
                (byte)Options.vehicleRestrictionsAggression,
                (byte)(Options.trafficLightPriorityRules ? 1 : 0),
                (byte)(Options.realisticPublicTransport ? 1 : 0),
                (byte)(Options.turnOnRedEnabled ? 1 : 0),
                (byte)(Options.allowNearTurnOnRed ? 1 : 0),
                (byte)(Options.allowFarTurnOnRed ? 1 : 0),
                (byte)(Options.automaticallyAddTrafficLightsIfApplicable ? 1 : 0),

                (byte)(OptionsMassEditTab.rabout_StayInLaneMainR.Save()),
                (byte)(OptionsMassEditTab.rabout_StayInLaneNearRabout.Save()),
                (byte)(OptionsMassEditTab.rabout_DedicatedExitLanes.Save()),
                (byte)(OptionsMassEditTab.rabout_NoCrossMainR.Save()),
                (byte)(OptionsMassEditTab.rabout_NoCrossYeildR.Save()),
                (byte)(OptionsMassEditTab.rabout_PrioritySigns.Save()),
                (byte)(OptionsMassEditTab.avn_NoCrossMainR.Save()),
            };
        }
    }
}