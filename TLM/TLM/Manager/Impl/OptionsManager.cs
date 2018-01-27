using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;

namespace TrafficManager.Manager.Impl {
	public class OptionsManager : AbstractCustomManager, IOptionsManager {
		// TODO I contain ugly code
		public static OptionsManager Instance = new OptionsManager();

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"- Not implemented -");
			// TODO implement
		}

		public bool MayPublishSegmentChanges() {
			return Options.instantEffects && !SerializableDataExtension.StateLoading;
		}

		public bool LoadData(byte[] data) {
			if (data.Length >= 1) {
				Options.setSimAccuracy(data[0]);
			}

			if (data.Length >= 2) {
				//Options.setLaneChangingRandomization(options[1]);
			}

			if (data.Length >= 3) {
				Options.setRecklessDrivers(data[2]);
			}

			if (data.Length >= 4) {
				Options.setRelaxedBusses(data[3] == (byte)1);
			}

			if (data.Length >= 5) {
				Options.setNodesOverlay(data[4] == (byte)1);
			}

			if (data.Length >= 6) {
				Options.setMayEnterBlockedJunctions(data[5] == (byte)1);
			}

			if (data.Length >= 7) {
				Options.setAdvancedAI(data[6] == (byte)1);
			}

			if (data.Length >= 8) {
				Options.setHighwayRules(data[7] == (byte)1);
			}

			if (data.Length >= 9) {
				Options.setPrioritySignsOverlay(data[8] == (byte)1);
			}

			if (data.Length >= 10) {
				Options.setTimedLightsOverlay(data[9] == (byte)1);
			}

			if (data.Length >= 11) {
				Options.setSpeedLimitsOverlay(data[10] == (byte)1);
			}

			if (data.Length >= 12) {
				Options.setVehicleRestrictionsOverlay(data[11] == (byte)1);
			}

			if (data.Length >= 13) {
				Options.setStrongerRoadConditionEffects(data[12] == (byte)1);
			}

			if (data.Length >= 14) {
				Options.setAllowUTurns(data[13] == (byte)1);
			}

			if (data.Length >= 15) {
				Options.setAllowLaneChangesWhileGoingStraight(data[14] == (byte)1);
			}

			if (data.Length >= 16) {
				Options.setDisableDespawning(data[15] != (byte)1);
			}

			if (data.Length >= 17) {
				//Options.setDynamicPathRecalculation(data[16] == (byte)1);
			}

			if (data.Length >= 18) {
				Options.setConnectedLanesOverlay(data[17] == (byte)1);
			}

			if (data.Length >= 19) {
				Options.setPrioritySignsEnabled(data[18] == (byte)1);
			}

			if (data.Length >= 20) {
				Options.setTimedLightsEnabled(data[19] == (byte)1);
			}

			if (data.Length >= 21) {
				Options.setCustomSpeedLimitsEnabled(data[20] == (byte)1);
			}

			if (data.Length >= 22) {
				Options.setVehicleRestrictionsEnabled(data[21] == (byte)1);
			}

			if (data.Length >= 23) {
				Options.setLaneConnectorEnabled(data[22] == (byte)1);
			}

			if (data.Length >= 24) {
				Options.setJunctionRestrictionsOverlay(data[23] == (byte)1);
			}

			if (data.Length >= 25) {
				Options.setJunctionRestrictionsEnabled(data[24] == (byte)1);
			}

			if (data.Length >= 26) {
				Options.setProhibitPocketCars(data[25] == (byte)1);
			}

			if (data.Length >= 27) {
				Options.setPreferOuterLane(data[26] == (byte)1);
			}

			if (data.Length >= 28) {
				Options.setRealisticSpeeds(data[27] == (byte)1);
			}

			if (data.Length >= 29) {
				Options.setEvacBussesMayIgnoreRules(data[28] == (byte)1);
			}

			if (data.Length >= 30) {
				Options.setInstantEffects(data[29] == (byte)1);
			}

			if (data.Length >= 31) {
				Options.setParkingRestrictionsEnabled(data[30] == (byte)1);
			}

			if (data.Length >= 32) {
				Options.setParkingRestrictionsOverlay(data[31] == (byte)1);
			}

			if (data.Length >= 33) {
				Options.setBanRegularTrafficOnBusLanes(data[32] == (byte)1);
			}

			if (data.Length >= 34) {
				Options.setShowPathFindStats(data[33] == (byte)1);
			}

			if (data.Length >= 35) {
				Options.setAltLaneSelectionRatio(data[34]);
			}

			if (data.Length >= 36) {
				try {
					Options.setVehicleRestrictionsAggression((VehicleRestrictionsAggression)data[35]);
				} catch (Exception e) {
					Log.Warning($"Skipping invalid value {data[35]} for vehicle restrictions aggression");
				}
			}

			if (data.Length >= 37) {
				Options.setTrafficLightPriorityRules(data[36] == (byte)1);
			}

			if (data.Length >= 38) {
				Options.setRealisticPublicTransport(data[37] == (byte)1);
			}

			return true;
		}

		public byte[] SaveData(ref bool success) {
			return new byte[] {
						(byte)Options.simAccuracy,
						(byte)0,//Options.laneChangingRandomization,
						(byte)Options.recklessDrivers,
						(byte)(Options.relaxedBusses ? 1 : 0),
						(byte) (Options.nodesOverlay ? 1 : 0),
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
						(byte)0,//Options.IsDynamicPathRecalculationActive()
						(byte)(Options.connectedLanesOverlay ? 1 : 0),
						(byte)(Options.prioritySignsEnabled ? 1 : 0),
						(byte)(Options.timedLightsEnabled ? 1 : 0),
						(byte)(Options.customSpeedLimitsEnabled ? 1 : 0),
						(byte)(Options.vehicleRestrictionsEnabled ? 1 : 0),
						(byte)(Options.laneConnectorEnabled ? 1 : 0),
						(byte)(Options.junctionRestrictionsOverlay ? 1 : 0),
						(byte)(Options.junctionRestrictionsEnabled ? 1 : 0),
						(byte)(Options.prohibitPocketCars ? 1 : 0),
						(byte)(Options.preferOuterLane ? 1 : 0),
						(byte)(Options.realisticSpeeds ? 1 : 0),
						(byte)(Options.evacBussesMayIgnoreRules ? 1 : 0),
						(byte)(Options.instantEffects ? 1 : 0),
						(byte)(Options.parkingRestrictionsEnabled ? 1 : 0),
						(byte)(Options.parkingRestrictionsOverlay ? 1 : 0),
						(byte)(Options.banRegularTrafficOnBusLanes ? 1 : 0),
						(byte)(Options.showPathFindStats ? 1 : 0),
						(byte)Options.altLaneSelectionRatio,
						(byte)Options.vehicleRestrictionsAggression,
						(byte)(Options.trafficLightPriorityRules ? 1 : 0),
						(byte)(Options.realisticPublicTransport ? 1 : 0),
				};
		}
	}
}
