using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using ColossalFramework;
using ColossalFramework.UI;
using ICities;
using TrafficManager.Traffic;
using TrafficManager.State;
using TrafficManager.UI;
using ColossalFramework.Plugins;
using ColossalFramework.Globalization;

namespace TrafficManager.State {

	public class Options : MonoBehaviour {
		public static readonly int DYNAMIC_RECALC_MIN_PROCESSOR_COUNT = 8;

		private static UIDropDown simAccuracyDropdown = null;
		//private static UIDropDown laneChangingRandomizationDropdown = null;
		private static UIDropDown recklessDriversDropdown = null;
		private static UICheckBox relaxedBussesToggle = null;
		private static UICheckBox allRelaxedToggle = null;
		private static UICheckBox prioritySignsOverlayToggle = null;
		private static UICheckBox timedLightsOverlayToggle = null;
		private static UICheckBox speedLimitsOverlayToggle = null;
		private static UICheckBox vehicleRestrictionsOverlayToggle = null;
		private static UICheckBox connectedLanesOverlayToggle = null;
		private static UICheckBox nodesOverlayToggle = null;
		private static UICheckBox vehicleOverlayToggle = null;
		private static UICheckBox allowEnterBlockedJunctionsToggle = null;
		private static UICheckBox allowUTurnsToggle = null;
		private static UICheckBox allowLaneChangesWhileGoingStraightToggle = null;
		private static UICheckBox enableDespawningToggle = null;

		private static UICheckBox strongerRoadConditionEffectsToggle = null;
		private static UICheckBox advancedAIToggle = null;
		private static UICheckBox dynamicPathRecalculationToggle = null;
		private static UICheckBox highwayRulesToggle = null;
#if DEBUG
		private static UICheckBox preferOuterLaneToggle = null;
#endif
		private static UICheckBox showLanesToggle = null;
		private static UIButton forgetTrafficLightsBtn = null;

		private static UICheckBox enablePrioritySignsToggle = null;
		private static UICheckBox enableTimedLightsToggle = null;
		private static UICheckBox enableCustomSpeedLimitsToggle = null;
		private static UICheckBox enableVehicleRestrictionsToggle = null;
		private static UICheckBox enableLaneConnectorToggle = null;

#if DEBUG
		private static UIButton resetSpeedLimitsBtn = null;
		private static UICheckBox disableSomething5Toggle = null;
		private static UICheckBox disableSomething4Toggle = null;
		private static UICheckBox disableSomething3Toggle = null;
		private static UICheckBox disableSomething2Toggle = null;
		private static UICheckBox disableSomething1Toggle = null;
		private static UITextField pathCostMultiplicatorField = null;
		private static UITextField pathCostMultiplicator2Field = null;
		private static UITextField someValueField = null;
		private static UITextField someValue2Field = null;
		private static UITextField someValue3Field = null;
		private static UITextField someValue4Field = null;
		private static UITextField someValue5Field = null;
		private static UITextField someValue6Field = null;
		private static UITextField someValue7Field = null;
		private static UITextField someValue8Field = null;
		private static UITextField someValue9Field = null;
#endif

		private static UIHelperBase mainGroup = null;
		private static UIHelperBase aiGroup = null;
		private static UIHelperBase overlayGroup = null;
		private static UIHelperBase maintenanceGroup = null;
		private static UIHelperBase featureGroup = null;

		public static int simAccuracy = 1;
		//public static int laneChangingRandomization = 2;
		public static int recklessDrivers = 3;
		public static bool relaxedBusses = true;
		public static bool allRelaxed = false;
		public static bool prioritySignsOverlay = false;
		public static bool timedLightsOverlay = false;
		public static bool speedLimitsOverlay = false;
		public static bool vehicleRestrictionsOverlay = false;
		public static bool connectedLanesOverlay = false;
		public static bool nodesOverlay = false;
		public static bool vehicleOverlay = false;
		public static bool allowEnterBlockedJunctions = false;
		public static bool allowUTurns = false;
		public static bool allowLaneChangesWhileGoingStraight = false;
		public static bool advancedAI = false;
		private static bool dynamicPathRecalculation = false;
		public static bool highwayRules = false;
		public static bool showLanes = false;
		public static bool strongerRoadConditionEffects = false;
		public static bool enableDespawning = true;
		public static bool preferOuterLane = false;
		//public static byte publicTransportUsage = 1;
		public static float pathCostMultiplicator = 0.75f; // debug value
		public static float pathCostMultiplicator2 = 1f; // debug value
		public static bool disableSomething1 = false; // debug switch
		public static bool disableSomething2 = false; // debug switch
		public static bool disableSomething3 = false; // debug switch
		public static bool disableSomething4 = false; // debug switch
		public static bool disableSomething5 = false; // debug switch
		public static float someValue = 3f; // debug value
		public static float someValue2 = 1.25f; // debug value
		public static float someValue3 = 2f; // debug value
		public static float someValue4 = 5f; // debug value
		public static float someValue5 = 2f; // debug value
		public static float someValue6 = 1.5f; // debug value
		public static float someValue7 = 0.75f; // debug value
		public static float someValue8 = 3f; // debug value
		public static float someValue9 = 0.8f; // debug value

		public static bool prioritySignsEnabled = true;
		public static bool timedLightsEnabled = true;
		public static bool customSpeedLimitsEnabled = true;
		public static bool vehicleRestrictionsEnabled = true;
		public static bool laneConnectorEnabled = true;

		public static bool MenuRebuildRequired {
			get { return menuRebuildRequired; }
			private set {
				menuRebuildRequired = value;
				if (LoadingExtension.Instance.UI != null)
					LoadingExtension.Instance.UI.Close();
			}
		}

		private static bool menuRebuildRequired = false;

		public static void makeSettings(UIHelperBase helper) {
			mainGroup = helper.AddGroup(Translation.GetString("TMPE_Title"));
			simAccuracyDropdown = mainGroup.AddDropdown(Translation.GetString("Simulation_accuracy") + ":", new string[] { Translation.GetString("Very_high"), Translation.GetString("High"), Translation.GetString("Medium"), Translation.GetString("Low"), Translation.GetString("Very_Low") }, simAccuracy, onSimAccuracyChanged) as UIDropDown;
			recklessDriversDropdown = mainGroup.AddDropdown(Translation.GetString("Reckless_driving") + ":", new string[] { Translation.GetString("Path_Of_Evil_(10_%)"), Translation.GetString("Rush_Hour_(5_%)"), Translation.GetString("Minor_Complaints_(2_%)"), Translation.GetString("Holy_City_(0_%)") }, recklessDrivers, onRecklessDriversChanged) as UIDropDown;
			//publicTransportUsageDropdown = mainGroup.AddDropdown(Translation.GetString("Citizens_use_public_transportation") + ":", new string[] { Translation.GetString("Very_often"), Translation.GetString("Often"), Translation.GetString("Sometimes"), Translation.GetString("Rarely"), Translation.GetString("Very_rarely") }, recklessDrivers, onRecklessDriversChanged) as UIDropDown;
			relaxedBussesToggle = mainGroup.AddCheckbox(Translation.GetString("Busses_may_ignore_lane_arrows"), relaxedBusses, onRelaxedBussesChanged) as UICheckBox;
#if DEBUG
			allRelaxedToggle = mainGroup.AddCheckbox(Translation.GetString("All_vehicles_may_ignore_lane_arrows"), allRelaxed, onAllRelaxedChanged) as UICheckBox;
#endif
			allowEnterBlockedJunctionsToggle = mainGroup.AddCheckbox(Translation.GetString("Vehicles_may_enter_blocked_junctions"), allowEnterBlockedJunctions, onAllowEnterBlockedJunctionsChanged) as UICheckBox;
			allowUTurnsToggle = mainGroup.AddCheckbox(Translation.GetString("Vehicles_may_do_u-turns_at_junctions") + " (BETA feature)", allowUTurns, onAllowUTurnsChanged) as UICheckBox;
			allowLaneChangesWhileGoingStraightToggle = mainGroup.AddCheckbox(Translation.GetString("Vehicles_going_straight_may_change_lanes_at_junctions"), allowLaneChangesWhileGoingStraight, onAllowLaneChangesWhileGoingStraightChanged) as UICheckBox;
			strongerRoadConditionEffectsToggle = mainGroup.AddCheckbox(Translation.GetString("Road_condition_has_a_bigger_impact_on_vehicle_speed"), strongerRoadConditionEffects, onStrongerRoadConditionEffectsChanged) as UICheckBox;
			enableDespawningToggle = mainGroup.AddCheckbox(Translation.GetString("Enable_despawning"), enableDespawning, onEnableDespawningChanged) as UICheckBox;
			aiGroup = helper.AddGroup("Advanced Vehicle AI");
			advancedAIToggle = aiGroup.AddCheckbox(Translation.GetString("Enable_Advanced_Vehicle_AI"), advancedAI, onAdvancedAIChanged) as UICheckBox;
#if DEBUG
			//if (SystemInfo.processorCount >= DYNAMIC_RECALC_MIN_PROCESSOR_COUNT)
				dynamicPathRecalculationToggle = aiGroup.AddCheckbox(Translation.GetString("Enable_dynamic_path_calculation"), dynamicPathRecalculation, onDynamicPathRecalculationChanged) as UICheckBox;
#endif
			highwayRulesToggle = aiGroup.AddCheckbox(Translation.GetString("Enable_highway_specific_lane_merging/splitting_rules"), highwayRules, onHighwayRulesChanged) as UICheckBox;
#if DEBUG
			preferOuterLaneToggle = aiGroup.AddCheckbox(Translation.GetString("Prefer_outer_lane") + " (BETA feature)", preferOuterLane, onPreferOuterLaneChanged) as UICheckBox;
#endif
			featureGroup = helper.AddGroup(Translation.GetString("Activated_features"));
			enablePrioritySignsToggle = featureGroup.AddCheckbox(Translation.GetString("Priority_signs"), prioritySignsEnabled, onPrioritySignsEnabledChanged) as UICheckBox;
			enableTimedLightsToggle = featureGroup.AddCheckbox(Translation.GetString("Timed_traffic_lights"), timedLightsEnabled, onTimedLightsEnabledChanged) as UICheckBox;
			enableCustomSpeedLimitsToggle = featureGroup.AddCheckbox(Translation.GetString("Speed_limits"), customSpeedLimitsEnabled, onCustomSpeedLimitsEnabledChanged) as UICheckBox;
			enableVehicleRestrictionsToggle = featureGroup.AddCheckbox(Translation.GetString("Vehicle_restrictions"), vehicleRestrictionsEnabled, onVehicleRestrictionsEnabledChanged) as UICheckBox;
			enableLaneConnectorToggle = featureGroup.AddCheckbox(Translation.GetString("Lane_connector"), laneConnectorEnabled, onLaneConnectorEnabledChanged) as UICheckBox;

			//laneChangingRandomizationDropdown = aiGroup.AddDropdown(Translation.GetString("Drivers_want_to_change_lanes_(only_applied_if_Advanced_AI_is_enabled):"), new string[] { Translation.GetString("Very_often") + " (50 %)", Translation.GetString("Often") + " (25 %)", Translation.GetString("Sometimes") + " (10 %)", Translation.GetString("Rarely") + " (5 %)", Translation.GetString("Very_rarely") + " (2.5 %)", Translation.GetString("Only_if_necessary") }, laneChangingRandomization, onLaneChangingRandomizationChanged) as UIDropDown;
			overlayGroup = helper.AddGroup(Translation.GetString("Persistently_visible_overlays"));
			prioritySignsOverlayToggle = overlayGroup.AddCheckbox(Translation.GetString("Priority_signs"), prioritySignsOverlay, onPrioritySignsOverlayChanged) as UICheckBox;
			timedLightsOverlayToggle = overlayGroup.AddCheckbox(Translation.GetString("Timed_traffic_lights"), timedLightsOverlay, onTimedLightsOverlayChanged) as UICheckBox;
			speedLimitsOverlayToggle = overlayGroup.AddCheckbox(Translation.GetString("Speed_limits"), speedLimitsOverlay, onSpeedLimitsOverlayChanged) as UICheckBox;
			vehicleRestrictionsOverlayToggle = overlayGroup.AddCheckbox(Translation.GetString("Vehicle_restrictions"), vehicleRestrictionsOverlay, onVehicleRestrictionsOverlayChanged) as UICheckBox;
			connectedLanesOverlayToggle = overlayGroup.AddCheckbox(Translation.GetString("Connected_lanes"), connectedLanesOverlay, onConnectedLanesOverlayChanged) as UICheckBox;
			nodesOverlayToggle = overlayGroup.AddCheckbox(Translation.GetString("Nodes_and_segments"), nodesOverlay, onNodesOverlayChanged) as UICheckBox;
			showLanesToggle = overlayGroup.AddCheckbox(Translation.GetString("Lanes"), showLanes, onShowLanesChanged) as UICheckBox;
#if DEBUG
			vehicleOverlayToggle = overlayGroup.AddCheckbox(Translation.GetString("Vehicles"), vehicleOverlay, onVehicleOverlayChanged) as UICheckBox;
#endif
			maintenanceGroup = helper.AddGroup(Translation.GetString("Maintenance"));
			forgetTrafficLightsBtn = maintenanceGroup.AddButton(Translation.GetString("Forget_toggled_traffic_lights"), onClickForgetToggledLights) as UIButton;
#if DEBUG
			resetSpeedLimitsBtn = maintenanceGroup.AddButton(Translation.GetString("Reset_custom_speed_limits"), onClickResetSpeedLimits) as UIButton;
			disableSomething1Toggle = maintenanceGroup.AddCheckbox("Enable path-finding debugging", disableSomething1, onDisableSomething1Changed) as UICheckBox;
			disableSomething2Toggle = maintenanceGroup.AddCheckbox("Disable something #2", disableSomething2, onDisableSomething2Changed) as UICheckBox;
			disableSomething3Toggle = maintenanceGroup.AddCheckbox("Disable something #3", disableSomething3, onDisableSomething3Changed) as UICheckBox;
			disableSomething4Toggle = maintenanceGroup.AddCheckbox("Disable something #4", disableSomething4, onDisableSomething4Changed) as UICheckBox;
			disableSomething5Toggle = maintenanceGroup.AddCheckbox("Disable something #5", disableSomething5, onDisableSomething5Changed) as UICheckBox;
			pathCostMultiplicatorField = maintenanceGroup.AddTextfield("Pathcost multiplicator (mult)", String.Format("{0:0.##}", pathCostMultiplicator), onPathCostMultiplicatorChanged) as UITextField;
			pathCostMultiplicator2Field = maintenanceGroup.AddTextfield("Pathcost multiplicator (div)", String.Format("{0:0.##}", pathCostMultiplicator2), onPathCostMultiplicator2Changed) as UITextField;
			someValueField = maintenanceGroup.AddTextfield("Some value #1", String.Format("{0:0.##}", someValue), onSomeValueChanged) as UITextField;
			someValue2Field = maintenanceGroup.AddTextfield("Some value #2", String.Format("{0:0.##}", someValue2), onSomeValue2Changed) as UITextField;
			someValue3Field = maintenanceGroup.AddTextfield("Some value #3", String.Format("{0:0.##}", someValue3), onSomeValue3Changed) as UITextField;
			someValue4Field = maintenanceGroup.AddTextfield("Some value #4", String.Format("{0:0.##}", someValue4), onSomeValue4Changed) as UITextField;
			someValue5Field = maintenanceGroup.AddTextfield("Some value #5", String.Format("{0:0.##}", someValue5), onSomeValue5Changed) as UITextField;
			someValue6Field = maintenanceGroup.AddTextfield("Some value #6", String.Format("{0:0.##}", someValue6), onSomeValue6Changed) as UITextField;
			someValue7Field = maintenanceGroup.AddTextfield("Some value #7", String.Format("{0:0.##}", someValue7), onSomeValue7Changed) as UITextField;
			someValue8Field = maintenanceGroup.AddTextfield("Some value #8", String.Format("{0:0.##}", someValue8), onSomeValue8Changed) as UITextField;
			someValue9Field = maintenanceGroup.AddTextfield("Some value #9", String.Format("{0:0.##}", someValue9), onSomeValue9Changed) as UITextField;
#endif
		}

		private static bool checkGameLoaded() {
			if (!SerializableDataExtension.StateLoading && !LoadingExtension.IsGameLoaded()) {
				UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Nope!", Translation.GetString("Settings_are_defined_for_each_savegame_separately") + ". https://www.viathinksoft.de/tmpe/#options", false);
				return false;
			}
			return true;
		}

		private static void onPrioritySignsOverlayChanged(bool newPrioritySignsOverlay) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"prioritySignsOverlay changed to {newPrioritySignsOverlay}");
			prioritySignsOverlay = newPrioritySignsOverlay;
		}

		private static void onTimedLightsOverlayChanged(bool newTimedLightsOverlay) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"timedLightsOverlay changed to {newTimedLightsOverlay}");
			timedLightsOverlay = newTimedLightsOverlay;
		}

		private static void onSpeedLimitsOverlayChanged(bool newSpeedLimitsOverlay) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"speedLimitsOverlay changed to {newSpeedLimitsOverlay}");
			speedLimitsOverlay = newSpeedLimitsOverlay;
		}

		private static void onVehicleRestrictionsOverlayChanged(bool newVehicleRestrictionsOverlay) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"vehicleRestrictionsOverlay changed to {newVehicleRestrictionsOverlay}");
			vehicleRestrictionsOverlay = newVehicleRestrictionsOverlay;
		}

		private static void onConnectedLanesOverlayChanged(bool newValue) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"connectedLanesOverlay changed to {newValue}");
			connectedLanesOverlay = newValue;
		}

		private static void onDisableSomething1Changed(bool newDisableSomething) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"disableSomething1 changed to {newDisableSomething}");
			disableSomething1 = newDisableSomething;
		}

		private static void onDisableSomething2Changed(bool newDisableSomething) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"disableSomething2 changed to {newDisableSomething}");
			disableSomething2 = newDisableSomething;
		}

		private static void onDisableSomething3Changed(bool newDisableSomething) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"disableSomething3 changed to {newDisableSomething}");
			disableSomething3 = newDisableSomething;
		}

		private static void onDisableSomething4Changed(bool newDisableSomething) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"disableSomething4 changed to {newDisableSomething}");
			disableSomething4 = newDisableSomething;
		}

		private static void onDisableSomething5Changed(bool newDisableSomething) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"disableSomething5 changed to {newDisableSomething}");
			disableSomething5 = newDisableSomething;
		}

		private static void onSimAccuracyChanged(int newAccuracy) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"Simulation accuracy changed to {newAccuracy}");
			simAccuracy = newAccuracy;
		}

		/*private static void onLaneChangingRandomizationChanged(int newLaneChangingRandomization) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"Lane changing frequency changed to {newLaneChangingRandomization}");
			laneChangingRandomization = newLaneChangingRandomization;
		}*/

		private static void onRecklessDriversChanged(int newRecklessDrivers) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"Reckless driver amount changed to {newRecklessDrivers}");
			recklessDrivers = newRecklessDrivers;
		}

		private static void onRelaxedBussesChanged(bool newRelaxedBusses) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"Relaxed busses changed to {newRelaxedBusses}");
			relaxedBusses = newRelaxedBusses;
		}

		private static void onAllRelaxedChanged(bool newAllRelaxed) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"All relaxed changed to {newAllRelaxed}");
			allRelaxed = newAllRelaxed;
		}

		private static void onAdvancedAIChanged(bool newAdvancedAI) {
			if (!checkGameLoaded())
				return;

#if !TAM
			if (!LoadingExtension.IsPathManagerCompatible) {
				if (newAdvancedAI) {
					setAdvancedAI(false);
					UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage(Translation.GetString("Advanced_AI_cannot_be_activated"), Translation.GetString("The_Advanced_Vehicle_AI_cannot_be_activated"), false);
				}
			} else {
#endif
				Log._Debug($"advancedAI changed to {newAdvancedAI}");
				setAdvancedAI(newAdvancedAI);
#if !TAM
			}
#endif
		}

		private static void onHighwayRulesChanged(bool newHighwayRules) {
			if (!checkGameLoaded())
				return;

#if !TAM
			if (!LoadingExtension.IsPathManagerCompatible) {
				if (newHighwayRules) {
					setAdvancedAI(false);
					setDynamicPathRecalculation(false);
					setHighwayRules(false);
					UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage(Translation.GetString("Advanced_AI_cannot_be_activated"), Translation.GetString("The_Advanced_Vehicle_AI_cannot_be_activated"), false);
				}
			} else {
#endif
				Log._Debug($"Highway rules changed to {newHighwayRules}");
				highwayRules = newHighwayRules;
				Flags.clearHighwayLaneArrows();
				Flags.applyAllFlags();
				if (newHighwayRules)
					setAdvancedAI(true);
#if !TAM
			}
#endif
		}

		private static void onPreferOuterLaneChanged(bool val) {
			if (!checkGameLoaded())
				return;

			preferOuterLane = val;
		}

		private static void onPrioritySignsEnabledChanged(bool val) {
			if (!checkGameLoaded())
				return;

			MenuRebuildRequired = true;
			prioritySignsEnabled = val;
			if (val)
				VehicleStateManager.Instance().InitAllVehicles();
			else
				setPrioritySignsOverlay(false);
		}

		private static void onTimedLightsEnabledChanged(bool val) {
			if (!checkGameLoaded())
				return;

			MenuRebuildRequired = true;
			timedLightsEnabled = val;
			if (val)
				VehicleStateManager.Instance().InitAllVehicles();
			else
				setTimedLightsOverlay(false);
		}

		private static void onCustomSpeedLimitsEnabledChanged(bool val) {
			if (!checkGameLoaded())
				return;

			MenuRebuildRequired = true;
			customSpeedLimitsEnabled = val;
			if (!val)
				setSpeedLimitsOverlay(false);
		}

		private static void onVehicleRestrictionsEnabledChanged(bool val) {
			if (!checkGameLoaded())
				return;

			MenuRebuildRequired = true;
			vehicleRestrictionsEnabled = val;
			if (!val)
				setVehicleRestrictionsOverlay(false);
		}

		private static void onLaneConnectorEnabledChanged(bool val) {
			if (!checkGameLoaded())
				return;

			MenuRebuildRequired = true;
			laneConnectorEnabled = val;
			if (!val)
				setConnectedLanesOverlay(false);
		}

		private static void onDynamicPathRecalculationChanged(bool value) {
			if (!checkGameLoaded())
				return;

#if !TAM
			if (!LoadingExtension.IsPathManagerCompatible) {
				if (value) {
					setAdvancedAI(false);
					setDynamicPathRecalculation(false);
					setHighwayRules(false);
					UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage(Translation.GetString("Advanced_AI_cannot_be_activated"), Translation.GetString("The_Advanced_Vehicle_AI_cannot_be_activated"), false);
				}
			} else {
#endif
				Log._Debug($"dynamicPathRecalculation changed to {value}");
				dynamicPathRecalculation = value;
				if (value)
					setAdvancedAI(true);
#if !TAM
			}
#endif
		}

		private static void onAllowEnterBlockedJunctionsChanged(bool newMayEnterBlockedJunctions) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"allowEnterBlockedJunctions changed to {newMayEnterBlockedJunctions}");
			allowEnterBlockedJunctions = newMayEnterBlockedJunctions;
		}

		private static void onAllowUTurnsChanged(bool newValue) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"allowUTurns changed to {newValue}");
			allowUTurns = newValue;
		}

		private static void onAllowLaneChangesWhileGoingStraightChanged(bool newValue) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"allowLaneChangesWhileGoingStraight changed to {newValue}");
			allowLaneChangesWhileGoingStraight = newValue;
		}

		private static void onStrongerRoadConditionEffectsChanged(bool newStrongerRoadConditionEffects) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"strongerRoadConditionEffects changed to {newStrongerRoadConditionEffects}");
			strongerRoadConditionEffects = newStrongerRoadConditionEffects;
		}

		private static void onEnableDespawningChanged(bool value) {
			if (!checkGameLoaded())
				return;

#if !TAM
			if (!LoadingExtension.IsPathManagerCompatible) {
				setEnableDespawning(true);
				UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Despawning cannot be modified", "The despawning option cannot be changed because you are using another mod that modifies vehicle behavior (e.g. Improved AI or Traffic++).", false);
			} else
#endif
			Log._Debug($"enableDespawning changed to {value}");
			enableDespawning = value;
		}

		private static void onNodesOverlayChanged(bool newNodesOverlay) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"Nodes overlay changed to {newNodesOverlay}");
			nodesOverlay = newNodesOverlay;
		}

		private static void onShowLanesChanged(bool newShowLanes) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"Show lanes changed to {newShowLanes}");
			showLanes = newShowLanes;
		}

		private static void onVehicleOverlayChanged(bool newVal) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"Vehicle overlay changed to {newVal}");
			vehicleOverlay = newVal;
		}

		private static void onPathCostMultiplicatorChanged(string newPathCostMultiplicatorStr) {
			if (!checkGameLoaded())
				return;

			try {
				float newPathCostMultiplicator = Single.Parse(newPathCostMultiplicatorStr);
				pathCostMultiplicator = newPathCostMultiplicator;
			} catch (Exception e) {
				Log.Warning($"An invalid value was inserted: '{newPathCostMultiplicatorStr}'. Error: {e.ToString()}");
                //UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Invalid value", "An invalid value was inserted.", false);
			}
		}

		private static void onPathCostMultiplicator2Changed(string newPathCostMultiplicatorStr) {
			if (!checkGameLoaded())
				return;

			try {
				float newPathCostMultiplicator = Single.Parse(newPathCostMultiplicatorStr);
				pathCostMultiplicator2 = newPathCostMultiplicator;
			} catch (Exception e) {
				Log.Warning($"An invalid value was inserted: '{newPathCostMultiplicatorStr}'. Error: {e.ToString()}");
				//UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Invalid value", "An invalid value was inserted.", false);
			}
		}

		private static void onSomeValueChanged(string newSomeValueStr) {
			if (!checkGameLoaded())
				return;

			try {
				float newSomeValue = Single.Parse(newSomeValueStr);
				someValue = newSomeValue;
			} catch (Exception e) {
				Log.Warning($"An invalid value was inserted: '{newSomeValueStr}'. Error: {e.ToString()}");
				//UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Invalid value", "An invalid value was inserted.", false);
			}
		}

		private static void onSomeValue2Changed(string newSomeValueStr) {
			if (!checkGameLoaded())
				return;

			try {
				float newSomeValue = Single.Parse(newSomeValueStr);
				someValue2 = newSomeValue;
			} catch (Exception e) {
				Log.Warning($"An invalid value was inserted: '{newSomeValueStr}'. Error: {e.ToString()}");
				//UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Invalid value", "An invalid value was inserted.", false);
			}
		}

		private static void onSomeValue3Changed(string newSomeValueStr) {
			if (!checkGameLoaded())
				return;

			try {
				float newSomeValue = Single.Parse(newSomeValueStr);
				someValue3 = newSomeValue;
			} catch (Exception e) {
				Log.Warning($"An invalid value was inserted: '{newSomeValueStr}'. Error: {e.ToString()}");
				//UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Invalid value", "An invalid value was inserted.", false);
			}
		}

		private static void onSomeValue4Changed(string newSomeValueStr) {
			if (!checkGameLoaded())
				return;

			try {
				float newSomeValue = Single.Parse(newSomeValueStr);
				someValue4 = newSomeValue;
			} catch (Exception e) {
				Log.Warning($"An invalid value was inserted: '{newSomeValueStr}'. Error: {e.ToString()}");
				//UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Invalid value", "An invalid value was inserted.", false);
			}
		}

		private static void onSomeValue5Changed(string newSomeValueStr) {
			if (!checkGameLoaded())
				return;

			try {
				float newSomeValue = Single.Parse(newSomeValueStr);
				someValue5 = newSomeValue;
			} catch (Exception e) {
				Log.Warning($"An invalid value was inserted: '{newSomeValueStr}'. Error: {e.ToString()}");
				//UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Invalid value", "An invalid value was inserted.", false);
			}
		}

		private static void onSomeValue6Changed(string newSomeValueStr) {
			if (!checkGameLoaded())
				return;

			try {
				float newSomeValue = Single.Parse(newSomeValueStr);
				someValue6 = newSomeValue;
			} catch (Exception e) {
				Log.Warning($"An invalid value was inserted: '{newSomeValueStr}'. Error: {e.ToString()}");
				//UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Invalid value", "An invalid value was inserted.", false);
			}
		}

		private static void onSomeValue7Changed(string newSomeValueStr) {
			if (!checkGameLoaded())
				return;

			try {
				float newSomeValue = Single.Parse(newSomeValueStr);
				someValue7 = newSomeValue;
			} catch (Exception e) {
				Log.Warning($"An invalid value was inserted: '{newSomeValueStr}'. Error: {e.ToString()}");
				//UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Invalid value", "An invalid value was inserted.", false);
			}
		}

		private static void onSomeValue8Changed(string newSomeValueStr) {
			if (!checkGameLoaded())
				return;

			try {
				float newSomeValue = Single.Parse(newSomeValueStr);
				someValue8 = newSomeValue;
			} catch (Exception e) {
				Log.Warning($"An invalid value was inserted: '{newSomeValueStr}'. Error: {e.ToString()}");
				//UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Invalid value", "An invalid value was inserted.", false);
			}
		}

		private static void onSomeValue9Changed(string newSomeValueStr) {
			if (!checkGameLoaded())
				return;

			try {
				float newSomeValue = Single.Parse(newSomeValueStr);
				someValue9 = newSomeValue;
			} catch (Exception e) {
				Log.Warning($"An invalid value was inserted: '{newSomeValueStr}'. Error: {e.ToString()}");
				//UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Invalid value", "An invalid value was inserted.", false);
			}
		}

		private static void onClickForgetToggledLights() {
			if (!checkGameLoaded())
				return;

			Flags.resetTrafficLights(false);
		}

		private static void onClickResetSpeedLimits() {
			if (!checkGameLoaded())
				return;

			Flags.resetSpeedLimits();
		}

		public static void setSimAccuracy(int newAccuracy) {
			simAccuracy = newAccuracy;
			if (simAccuracyDropdown != null)
				simAccuracyDropdown.selectedIndex = newAccuracy;
		}

		/*public static void setLaneChangingRandomization(int newLaneChangingRandomization) {
			laneChangingRandomization = newLaneChangingRandomization;
			if (laneChangingRandomizationDropdown != null)
				laneChangingRandomizationDropdown.selectedIndex = newLaneChangingRandomization;
		}*/

		public static void setRecklessDrivers(int newRecklessDrivers) {
			recklessDrivers = newRecklessDrivers;
			if (recklessDriversDropdown != null)
				recklessDriversDropdown.selectedIndex = newRecklessDrivers;
		}

#if DEBUG
		public static void setPathCostMultiplicator(float newPathCostMultiplicator) {
			pathCostMultiplicator = newPathCostMultiplicator;
			if (pathCostMultiplicatorField != null)
				pathCostMultiplicatorField.text = newPathCostMultiplicator.ToString();
		}
#endif

		internal static bool isStockLaneChangerUsed() {
			return !advancedAI;
		}

		public static void setRelaxedBusses(bool newRelaxedBusses) {
			relaxedBusses = newRelaxedBusses;
			if (relaxedBussesToggle != null)
				relaxedBussesToggle.isChecked = newRelaxedBusses;
		}

		public static void setAllRelaxed(bool newAllRelaxed) {
			allRelaxed = newAllRelaxed;
			if (allRelaxedToggle != null)
				allRelaxedToggle.isChecked = newAllRelaxed;
		}

		public static void setHighwayRules(bool newHighwayRules) {
#if !TAM
			if (!LoadingExtension.IsPathManagerCompatible) {
				newHighwayRules = false;
				highwayRules = false;
			} else {
#endif
				highwayRules = newHighwayRules;
#if !TAM
			}
#endif
			if (highwayRulesToggle != null)
				highwayRulesToggle.isChecked = newHighwayRules;
		}

#if DEBUG
		public static void setPreferOuterLane(bool val) {
#if !TAM
			if (!LoadingExtension.IsPathManagerCompatible) {
				preferOuterLane = false;
			} else {
#endif
				preferOuterLane = val;
#if !TAM
			}
#endif
			if (preferOuterLaneToggle != null)
				preferOuterLaneToggle.isChecked = val;
		}
#endif

		public static void setShowLanes(bool newShowLanes) {
			showLanes = newShowLanes;
			if (showLanesToggle != null)
				showLanesToggle.isChecked = newShowLanes;
		}

		public static void setAdvancedAI(bool newAdvancedAI) {
#if !TAM
			if (!LoadingExtension.IsPathManagerCompatible) {
				newAdvancedAI = false;
				advancedAI = false;
			} else {
#endif
				advancedAI = newAdvancedAI;
#if !TAM
			}
#endif
			if (advancedAIToggle != null)
				advancedAIToggle.isChecked = newAdvancedAI;

			if (!newAdvancedAI) {
				setDynamicPathRecalculation(false);
				setHighwayRules(false);
#if DEBUG
				setPreferOuterLane(false);
#endif
			}
		}

		public static void setDynamicPathRecalculation(bool value) {
#if DEBUG
			/*if (SystemInfo.processorCount < DYNAMIC_RECALC_MIN_PROCESSOR_COUNT)
				value = false;*/
#endif

#if !TAM
			if (!LoadingExtension.IsPathManagerCompatible) {
				value = false;
				dynamicPathRecalculation = false;
			} else {
#endif
				dynamicPathRecalculation = value;
#if !TAM
			}
#endif
			if (dynamicPathRecalculationToggle != null)
				dynamicPathRecalculationToggle.isChecked = value;
		}

		public static bool IsDynamicPathRecalculationActive() {
			return Options.dynamicPathRecalculation;
		}

		public static void setMayEnterBlockedJunctions(bool newMayEnterBlockedJunctions) {
			allowEnterBlockedJunctions = newMayEnterBlockedJunctions;
			if (allowEnterBlockedJunctionsToggle != null)
				allowEnterBlockedJunctionsToggle.isChecked = newMayEnterBlockedJunctions;
		}

		public static void setStrongerRoadConditionEffects(bool newStrongerRoadConditionEffects) {
			strongerRoadConditionEffects = newStrongerRoadConditionEffects;
			if (strongerRoadConditionEffectsToggle != null)
				strongerRoadConditionEffectsToggle.isChecked = newStrongerRoadConditionEffects;
		}

		public static void setEnableDespawning(bool value) {
#if !TAM
			if (!LoadingExtension.IsPathManagerCompatible) {
				value = true;
				enableDespawning = true;
			} else {
#endif
				enableDespawning = value;
#if !TAM
			}
#endif

			if (enableDespawningToggle != null)
				enableDespawningToggle.isChecked = value;
		}

		public static void setAllowUTurns(bool value) {
			allowUTurns = value;
			if (allowUTurnsToggle != null)
				allowUTurnsToggle.isChecked = value;
		}

		public static void setAllowLaneChangesWhileGoingStraight(bool value) {
			allowLaneChangesWhileGoingStraight = value;
			if (allowLaneChangesWhileGoingStraightToggle != null)
				allowLaneChangesWhileGoingStraightToggle.isChecked = value;
		}

		public static void setPrioritySignsOverlay(bool newPrioritySignsOverlay) {
			prioritySignsOverlay = newPrioritySignsOverlay;
			if (prioritySignsOverlayToggle != null)
				prioritySignsOverlayToggle.isChecked = newPrioritySignsOverlay;
		}

		public static void setTimedLightsOverlay(bool newTimedLightsOverlay) {
			timedLightsOverlay = newTimedLightsOverlay;
			if (timedLightsOverlayToggle != null)
				timedLightsOverlayToggle.isChecked = newTimedLightsOverlay;
		}

		public static void setSpeedLimitsOverlay(bool newSpeedLimitsOverlay) {
			speedLimitsOverlay = newSpeedLimitsOverlay;
			if (speedLimitsOverlayToggle != null)
				speedLimitsOverlayToggle.isChecked = newSpeedLimitsOverlay;
		}

		public static void setVehicleRestrictionsOverlay(bool newVehicleRestrictionsOverlay) {
			vehicleRestrictionsOverlay = newVehicleRestrictionsOverlay;
			if (vehicleRestrictionsOverlayToggle != null)
				vehicleRestrictionsOverlayToggle.isChecked = newVehicleRestrictionsOverlay;
		}

		public static void setConnectedLanesOverlay(bool newValue) {
			connectedLanesOverlay = newValue;
			if (connectedLanesOverlayToggle != null)
				connectedLanesOverlayToggle.isChecked = newValue;
		}

		public static void setNodesOverlay(bool newNodesOverlay) {
			nodesOverlay = newNodesOverlay;
			if (nodesOverlayToggle != null)
				nodesOverlayToggle.isChecked = newNodesOverlay;
		}

		public static void setVehicleOverlay(bool newVal) {
			vehicleOverlay = newVal;
			if (vehicleOverlayToggle != null)
				vehicleOverlayToggle.isChecked = newVal;
		}

		public static void setPrioritySignsEnabled(bool newValue) {
			MenuRebuildRequired = true;
			prioritySignsEnabled = newValue;
			if (enablePrioritySignsToggle != null)
				enablePrioritySignsToggle.isChecked = newValue;
			if (!newValue)
				setPrioritySignsOverlay(false);
		}

		public static void setTimedLightsEnabled(bool newValue) {
			MenuRebuildRequired = true;
			timedLightsEnabled = newValue;
			if (enableTimedLightsToggle != null)
				enableTimedLightsToggle.isChecked = newValue;
			if (!newValue)
				setTimedLightsOverlay(false);
		}

		public static void setCustomSpeedLimitsEnabled(bool newValue) {
			MenuRebuildRequired = true;
			customSpeedLimitsEnabled = newValue;
			if (enableCustomSpeedLimitsToggle != null)
				enableCustomSpeedLimitsToggle.isChecked = newValue;
			if (!newValue)
				setSpeedLimitsOverlay(false);
		}

		public static void setVehicleRestrictionsEnabled(bool newValue) {
			MenuRebuildRequired = true;
			vehicleRestrictionsEnabled = newValue;
			if (enableVehicleRestrictionsToggle != null)
				enableVehicleRestrictionsToggle.isChecked = newValue;
			if (!newValue)
				setVehicleRestrictionsOverlay(false);
		}

		public static void setLaneConnectorEnabled(bool newValue) {
			MenuRebuildRequired = true;
			laneConnectorEnabled = newValue;
			if (enableLaneConnectorToggle != null)
				enableLaneConnectorToggle.isChecked = newValue;
			if (!newValue)
				setConnectedLanesOverlay(false);
		}

		/*internal static int getLaneChangingRandomizationTargetValue() {
			int ret = 100;
			switch (laneChangingRandomization) {
				case 0:
					ret = 2;
					break;
				case 1:
					ret = 4;
					break;
				case 2:
					ret = 10;
					break;
				case 3:
					ret = 20;
					break;
				case 4:
					ret = 50;
					break;
			}
			return ret;
		}*/

		/*internal static float getLaneChangingProbability() {
			switch (laneChangingRandomization) {
				case 0:
					return 0.5f;
				case 1:
					return 0.25f;
				case 2:
					return 0.1f;
				case 3:
					return 0.05f;
				case 4:
					return 0.01f;
			}
			return 0.01f;
		}*/

		internal static int getRecklessDriverModulo() {
			switch (recklessDrivers) {
				case 0:
					return 10;
				case 1:
					return 20;
				case 2:
					return 50;
				case 3:
					return 10000;
			}
			return 10000;
		}
	}
}
