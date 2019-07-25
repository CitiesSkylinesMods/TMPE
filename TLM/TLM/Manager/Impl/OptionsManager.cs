﻿namespace TrafficManager.Manager.Impl {
    using System;
    using API.Manager;
    using API.Traffic.Enums;
    using CSUtil.Commons;
    using State;

    public class OptionsManager
        : AbstractCustomManager,
          IOptionsManager
    {
        // TODO I contain ugly code
        public static OptionsManager Instance = new OptionsManager();

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._DebugOnlyError($"- Not implemented -");
            // TODO implement
        }

        public bool MayPublishSegmentChanges() {
            return Options.instantEffects && !SerializableDataExtension.StateLoading;
        }

        public bool LoadData(byte[] data) {
            if (data.Length >= 1) {
                //Options.setSimAccuracy(data[0]);
            }

            if (data.Length >= 2) {
                //Options.setLaneChangingRandomization(options[1]);
            }

            if (data.Length >= 3) {
                Options.setRecklessDrivers(data[2]);
            }

            if (data.Length >= 4) {
                Options.setRelaxedBusses(data[3] == 1);
            }

            if (data.Length >= 5) {
                Options.setNodesOverlay(data[4] == 1);
            }

            if (data.Length >= 6) {
                Options.setMayEnterBlockedJunctions(data[5] == 1);
            }

            if (data.Length >= 7) {
                Options.setAdvancedAI(data[6] == 1);
            }

            if (data.Length >= 8) {
                Options.setHighwayRules(data[7] == 1);
            }

            if (data.Length >= 9) {
                Options.setPrioritySignsOverlay(data[8] == 1);
            }

            if (data.Length >= 10) {
                Options.setTimedLightsOverlay(data[9] == 1);
            }

            if (data.Length >= 11) {
                Options.setSpeedLimitsOverlay(data[10] == 1);
            }

            if (data.Length >= 12) {
                Options.setVehicleRestrictionsOverlay(data[11] == 1);
            }

            if (data.Length >= 13) {
                Options.setStrongerRoadConditionEffects(data[12] == 1);
            }

            if (data.Length >= 14) {
                Options.setAllowUTurns(data[13] == 1);
            }

            if (data.Length >= 15) {
                Options.setAllowLaneChangesWhileGoingStraight(data[14] == 1);
            }

            if (data.Length >= 16) {
                Options.setDisableDespawning(data[15] != 1);
            }

            if (data.Length >= 17) {
                //Options.setDynamicPathRecalculation(data[16] == (byte)1);
            }

            if (data.Length >= 18) {
                Options.setConnectedLanesOverlay(data[17] == 1);
            }

            if (data.Length >= 19) {
                Options.setPrioritySignsEnabled(data[18] == 1);
            }

            if (data.Length >= 20) {
                Options.setTimedLightsEnabled(data[19] == 1);
            }

            if (data.Length >= 21) {
                Options.setCustomSpeedLimitsEnabled(data[20] == 1);
            }

            if (data.Length >= 22) {
                Options.setVehicleRestrictionsEnabled(data[21] == 1);
            }

            if (data.Length >= 23) {
                Options.setLaneConnectorEnabled(data[22] == 1);
            }

            if (data.Length >= 24) {
                Options.setJunctionRestrictionsOverlay(data[23] == 1);
            }

            if (data.Length >= 25) {
                Options.setJunctionRestrictionsEnabled(data[24] == 1);
            }

            if (data.Length >= 26) {
                Options.setProhibitPocketCars(data[25] == 1);
            }

            if (data.Length >= 27) {
                Options.setPreferOuterLane(data[26] == 1);
            }

            if (data.Length >= 28) {
                Options.setIndividualDrivingStyle(data[27] == 1);
            }

            if (data.Length >= 29) {
                Options.setEvacBussesMayIgnoreRules(data[28] == 1);
            }

            if (data.Length >= 30) {
                Options.setInstantEffects(data[29] == 1);
            }

            if (data.Length >= 31) {
                Options.setParkingRestrictionsEnabled(data[30] == 1);
            }

            if (data.Length >= 32) {
                Options.setParkingRestrictionsOverlay(data[31] == 1);
            }

            if (data.Length >= 33) {
                Options.setBanRegularTrafficOnBusLanes(data[32] == 1);
            }

            if (data.Length >= 34) {
                Options.setShowPathFindStats(data[33] == 1);
            }

            if (data.Length >= 35) {
                Options.setAltLaneSelectionRatio(data[34]);
            }

            if (data.Length >= 36) {
                try {
                    Options.setVehicleRestrictionsAggression(
                        (VehicleRestrictionsAggression)data[35]);
                }
                catch (Exception e) {
                    Log.Warning(
                        $"Skipping invalid value {data[35]} for vehicle restrictions aggression");
                }
            }

            if (data.Length >= 37) {
                Options.setTrafficLightPriorityRules(data[36] == 1);
            }

            if (data.Length >= 38) {
                Options.setRealisticPublicTransport(data[37] == 1);
            }

            if (data.Length >= 39) {
                Options.setTurnOnRedEnabled(data[38] == 1);
            }

            if (data.Length >= 40) {
                Options.setAllowNearTurnOnRed(data[39] == 1);
            }

            if (data.Length >= 41) {
                Options.setAllowFarTurnOnRed(data[40] == 1);
            }

            return true;
        }

        public byte[] SaveData(ref bool success) {
            return new byte[] {
                0, //Options.simAccuracy,
                0, //Options.laneChangingRandomization,
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
                0, //Options.IsDynamicPathRecalculationActive()
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
                (byte)(Options.allowFarTurnOnRed ? 1 : 0)
            };
        }
    }
}