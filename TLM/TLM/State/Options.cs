using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ColossalFramework;
using ColossalFramework.UI;
using ICities;
using TrafficManager.Geometry;
using TrafficManager.State;
using TrafficManager.UI;
using ColossalFramework.Plugins;
using ColossalFramework.Globalization;
using TrafficManager.Manager;

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
		private static UICheckBox junctionRestrictionsOverlayToggle = null;
		private static UICheckBox connectedLanesOverlayToggle = null;
		private static UICheckBox nodesOverlayToggle = null;
		private static UICheckBox vehicleOverlayToggle = null;
		private static UICheckBox citizenOverlayToggle = null;
		private static UICheckBox buildingOverlayToggle = null;
		private static UICheckBox allowEnterBlockedJunctionsToggle = null;
		private static UICheckBox allowUTurnsToggle = null;
		private static UICheckBox allowLaneChangesWhileGoingStraightToggle = null;
		private static UICheckBox enableDespawningToggle = null;

		private static UICheckBox strongerRoadConditionEffectsToggle = null;
		private static UICheckBox prohibitPocketCarsToggle = null;
		private static UICheckBox advancedAIToggle = null;
		private static UICheckBox dynamicPathRecalculationToggle = null;
		private static UICheckBox highwayRulesToggle = null;
#if DEBUG
		private static UICheckBox preferOuterLaneToggle = null;
#endif
		private static UICheckBox showLanesToggle = null;
		private static UIButton forgetTrafficLightsBtn = null;
		private static UIButton resetStuckEntitiesBtn = null;

		private static UICheckBox enablePrioritySignsToggle = null;
		private static UICheckBox enableTimedLightsToggle = null;
		private static UICheckBox enableCustomSpeedLimitsToggle = null;
		private static UICheckBox enableVehicleRestrictionsToggle = null;
		private static UICheckBox enableJunctionRestrictionsToggle = null;
		private static UICheckBox enableLaneConnectorToggle = null;

#if DEBUG
		private static UIButton resetSpeedLimitsBtn = null;
		private static List<UICheckBox> debugSwitchFields = new List<UICheckBox>();
		private static List<UITextField> debugValueFields = new List<UITextField>();
		private static UITextField pathCostMultiplicatorField = null;
		private static UITextField pathCostMultiplicator2Field = null;
#endif
		private static UIButton reloadGlobalConfBtn = null;
		private static UIButton resetGlobalConfBtn = null;

		private static UIHelperBase mainGroup = null;
		private static UIHelperBase vehAiGroup = null;
		private static UIHelperBase parkAiGroup = null;
		private static UIHelperBase overlayGroup = null;
		private static UIHelperBase maintenanceGroup = null;
		private static UIHelperBase featureGroup = null;

		public static int simAccuracy = 0;
		//public static int laneChangingRandomization = 2;
		public static int recklessDrivers = 3;
		public static bool relaxedBusses = true;
		public static bool allRelaxed = false;
		public static bool prioritySignsOverlay = false;
		public static bool timedLightsOverlay = false;
		public static bool speedLimitsOverlay = false;
		public static bool vehicleRestrictionsOverlay = false;
		public static bool junctionRestrictionsOverlay = false;
		public static bool connectedLanesOverlay = false;
#if DEBUG
		public static bool nodesOverlay = true;
		public static bool vehicleOverlay = false;
		public static bool citizenOverlay = false;
		public static bool buildingOverlay = true;
#else
		public static bool nodesOverlay = false;
		public static bool vehicleOverlay = false;
		public static bool citizenOverlay = false;
		public static bool buildingOverlay = false;
#endif
		public static bool allowEnterBlockedJunctions = false;
		public static bool allowUTurns = false;
		public static bool allowLaneChangesWhileGoingStraight = false;
		public static bool advancedAI = false;
		private static bool dynamicPathRecalculation = false;
		public static bool highwayRules = false;
#if DEBUG
		public static bool showLanes = false;
#else
		public static bool showLanes = false;
#endif
		public static bool strongerRoadConditionEffects = false;
		public static bool prohibitPocketCars = false;
		public static bool enableDespawning = true;
		public static bool preferOuterLane = false;
		//public static byte publicTransportUsage = 1;

		public static bool prioritySignsEnabled = true;
		public static bool timedLightsEnabled = true;
		public static bool customSpeedLimitsEnabled = true;
		public static bool vehicleRestrictionsEnabled = true;
		public static bool junctionRestrictionsEnabled = true;
		public static bool laneConnectorEnabled = true;

		public static bool MenuRebuildRequired {
			get { return menuRebuildRequired; }
			private set {
				menuRebuildRequired = value;
				if (LoadingExtension.Instance != null && LoadingExtension.Instance.BaseUI != null)
					LoadingExtension.Instance.BaseUI.Close();
			}
		}

		private static bool menuRebuildRequired = false;

		public static void makeSettings(UIHelperBase helper) {
			// tabbing code is borrowed from RushHour mod
			// https://github.com/PropaneDragon/RushHour/blob/release/RushHour/Options/OptionHandler.cs

			UIHelper actualHelper = helper as UIHelper;
			UIComponent container = actualHelper.self as UIComponent;

			//Find the tab button in the KeyMappingPanel, so we can copy it
			UIButton tabTemplate = GameObject.Find("KeyMappingTabStrip").GetComponentInChildren<UIButton>();

			UITabstrip tabStrip = container.AddUIComponent<UITabstrip>();
			tabStrip.relativePosition = new Vector3(0, 0);
			tabStrip.size = new Vector2(container.width - 20, 40);

			UITabContainer tabContainer = container.AddUIComponent<UITabContainer>();
			tabContainer.relativePosition = new Vector3(0, 40);
			tabContainer.size = new Vector2(container.width - 20, container.height - tabStrip.height - 20);
			tabStrip.tabPages = tabContainer;

			int tabIndex = 0;
			// GENERAL

			UIButton settingsButton = tabStrip.AddTab(Translation.GetString("General"), tabTemplate, true);
			settingsButton.textPadding = new RectOffset(10, 10, 10, 10);
			settingsButton.autoSize = true;
			settingsButton.tooltip = Translation.GetString("General");
			tabStrip.selectedIndex = tabIndex;

			UIPanel currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
			currentPanel.autoLayout = true;
			currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
			currentPanel.autoLayoutPadding.top = 5;
			currentPanel.autoLayoutPadding.left = 10;
			currentPanel.autoLayoutPadding.right = 10;

			UIHelper panelHelper = new UIHelper(currentPanel);

			simAccuracyDropdown = panelHelper.AddDropdown(Translation.GetString("Simulation_accuracy") + ":", new string[] { Translation.GetString("Very_high"), Translation.GetString("High"), Translation.GetString("Medium"), Translation.GetString("Low"), Translation.GetString("Very_Low") }, simAccuracy, onSimAccuracyChanged) as UIDropDown;

			featureGroup = panelHelper.AddGroup(Translation.GetString("Activated_features"));
			enablePrioritySignsToggle = featureGroup.AddCheckbox(Translation.GetString("Priority_signs"), prioritySignsEnabled, onPrioritySignsEnabledChanged) as UICheckBox;
			enableTimedLightsToggle = featureGroup.AddCheckbox(Translation.GetString("Timed_traffic_lights"), timedLightsEnabled, onTimedLightsEnabledChanged) as UICheckBox;
			enableCustomSpeedLimitsToggle = featureGroup.AddCheckbox(Translation.GetString("Speed_limits"), customSpeedLimitsEnabled, onCustomSpeedLimitsEnabledChanged) as UICheckBox;
			enableVehicleRestrictionsToggle = featureGroup.AddCheckbox(Translation.GetString("Vehicle_restrictions"), vehicleRestrictionsEnabled, onVehicleRestrictionsEnabledChanged) as UICheckBox;
			enableJunctionRestrictionsToggle = featureGroup.AddCheckbox(Translation.GetString("Junction_restrictions"), junctionRestrictionsEnabled, onJunctionRestrictionsEnabledChanged) as UICheckBox;
			enableLaneConnectorToggle = featureGroup.AddCheckbox(Translation.GetString("Lane_connector"), laneConnectorEnabled, onLaneConnectorEnabledChanged) as UICheckBox;

			// GAMEPLAY
			++tabIndex;

			settingsButton = tabStrip.AddTab(Translation.GetString("Gameplay"), tabTemplate, true);
			settingsButton.textPadding = new RectOffset(10, 10, 10, 10);
			settingsButton.autoSize = true;
			settingsButton.tooltip = Translation.GetString("Gameplay");
			tabStrip.selectedIndex = tabIndex;

			currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
			currentPanel.autoLayout = true;
			currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
			currentPanel.autoLayoutPadding.top = 5;
			currentPanel.autoLayoutPadding.left = 10;
			currentPanel.autoLayoutPadding.right = 10;

			panelHelper = new UIHelper(currentPanel);

			recklessDriversDropdown = panelHelper.AddDropdown(Translation.GetString("Reckless_driving") + ":", new string[] { Translation.GetString("Path_Of_Evil_(10_%)"), Translation.GetString("Rush_Hour_(5_%)"), Translation.GetString("Minor_Complaints_(2_%)"), Translation.GetString("Holy_City_(0_%)") }, recklessDrivers, onRecklessDriversChanged) as UIDropDown;
			strongerRoadConditionEffectsToggle = panelHelper.AddCheckbox(Translation.GetString("Road_condition_has_a_bigger_impact_on_vehicle_speed"), strongerRoadConditionEffects, onStrongerRoadConditionEffectsChanged) as UICheckBox;
			enableDespawningToggle = panelHelper.AddCheckbox(Translation.GetString("Enable_despawning"), enableDespawning, onEnableDespawningChanged) as UICheckBox;

			vehAiGroup = panelHelper.AddGroup(Translation.GetString("Advanced_Vehicle_AI"));
			advancedAIToggle = vehAiGroup.AddCheckbox(Translation.GetString("Enable_Advanced_Vehicle_AI"), advancedAI, onAdvancedAIChanged) as UICheckBox;
#if DEBUG
			//if (SystemInfo.processorCount >= DYNAMIC_RECALC_MIN_PROCESSOR_COUNT)
			//dynamicPathRecalculationToggle = vehAiGroup.AddCheckbox(Translation.GetString("Enable_dynamic_path_calculation"), dynamicPathRecalculation, onDynamicPathRecalculationChanged) as UICheckBox;
#endif
			highwayRulesToggle = vehAiGroup.AddCheckbox(Translation.GetString("Enable_highway_specific_lane_merging/splitting_rules"), highwayRules, onHighwayRulesChanged) as UICheckBox;
			preferOuterLaneToggle = vehAiGroup.AddCheckbox(Translation.GetString("Heavy_vehicles_prefer_outer_lanes_on_highways"), preferOuterLane, onPreferOuterLaneChanged) as UICheckBox;

			parkAiGroup = panelHelper.AddGroup("Parking AI");
			prohibitPocketCarsToggle = parkAiGroup.AddCheckbox(Translation.GetString("Enable_more_realistic_parking") + " (BETA feature)", prohibitPocketCars, onProhibitPocketCarsChanged) as UICheckBox;

			// VEHICLE RESTRICTIONS
			++tabIndex;

			settingsButton = tabStrip.AddTab(Translation.GetString("Vehicle_restrictions"), tabTemplate, true);
			settingsButton.textPadding = new RectOffset(10, 10, 10, 10);
			settingsButton.autoSize = true;
			settingsButton.tooltip = Translation.GetString("Vehicle_restrictions");
			tabStrip.selectedIndex = tabIndex;

			currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
			currentPanel.autoLayout = true;
			currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
			currentPanel.autoLayoutPadding.top = 5;
			currentPanel.autoLayoutPadding.left = 10;
			currentPanel.autoLayoutPadding.right = 10;

			panelHelper = new UIHelper(currentPanel);

			relaxedBussesToggle = panelHelper.AddCheckbox(Translation.GetString("Busses_may_ignore_lane_arrows"), relaxedBusses, onRelaxedBussesChanged) as UICheckBox;
#if DEBUG
			allRelaxedToggle = panelHelper.AddCheckbox(Translation.GetString("All_vehicles_may_ignore_lane_arrows"), allRelaxed, onAllRelaxedChanged) as UICheckBox;
#endif
			allowEnterBlockedJunctionsToggle = panelHelper.AddCheckbox(Translation.GetString("Vehicles_may_enter_blocked_junctions"), allowEnterBlockedJunctions, onAllowEnterBlockedJunctionsChanged) as UICheckBox;
			allowUTurnsToggle = panelHelper.AddCheckbox(Translation.GetString("Vehicles_may_do_u-turns_at_junctions"), allowUTurns, onAllowUTurnsChanged) as UICheckBox;
			allowLaneChangesWhileGoingStraightToggle = panelHelper.AddCheckbox(Translation.GetString("Vehicles_going_straight_may_change_lanes_at_junctions"), allowLaneChangesWhileGoingStraight, onAllowLaneChangesWhileGoingStraightChanged) as UICheckBox;

			// OVERLAYS
			++tabIndex;

			settingsButton = tabStrip.AddTab(Translation.GetString("Overlays"), tabTemplate, true);
			settingsButton.textPadding = new RectOffset(10, 10, 10, 10);
			settingsButton.autoSize = true;
			settingsButton.tooltip = Translation.GetString("Overlays");
			tabStrip.selectedIndex = tabIndex;

			currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
			currentPanel.autoLayout = true;
			currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
			currentPanel.autoLayoutPadding.top = 5;
			currentPanel.autoLayoutPadding.left = 10;
			currentPanel.autoLayoutPadding.right = 10;

			panelHelper = new UIHelper(currentPanel);

			prioritySignsOverlayToggle = panelHelper.AddCheckbox(Translation.GetString("Priority_signs"), prioritySignsOverlay, onPrioritySignsOverlayChanged) as UICheckBox;
			timedLightsOverlayToggle = panelHelper.AddCheckbox(Translation.GetString("Timed_traffic_lights"), timedLightsOverlay, onTimedLightsOverlayChanged) as UICheckBox;
			speedLimitsOverlayToggle = panelHelper.AddCheckbox(Translation.GetString("Speed_limits"), speedLimitsOverlay, onSpeedLimitsOverlayChanged) as UICheckBox;
			vehicleRestrictionsOverlayToggle = panelHelper.AddCheckbox(Translation.GetString("Vehicle_restrictions"), vehicleRestrictionsOverlay, onVehicleRestrictionsOverlayChanged) as UICheckBox;
			junctionRestrictionsOverlayToggle = panelHelper.AddCheckbox(Translation.GetString("Junction_restrictions"), junctionRestrictionsOverlay, onJunctionRestrictionsOverlayChanged) as UICheckBox;
			connectedLanesOverlayToggle = panelHelper.AddCheckbox(Translation.GetString("Connected_lanes"), connectedLanesOverlay, onConnectedLanesOverlayChanged) as UICheckBox;
			nodesOverlayToggle = panelHelper.AddCheckbox(Translation.GetString("Nodes_and_segments"), nodesOverlay, onNodesOverlayChanged) as UICheckBox;
			showLanesToggle = panelHelper.AddCheckbox(Translation.GetString("Lanes"), showLanes, onShowLanesChanged) as UICheckBox;
#if DEBUG
			vehicleOverlayToggle = panelHelper.AddCheckbox(Translation.GetString("Vehicles"), vehicleOverlay, onVehicleOverlayChanged) as UICheckBox;
			citizenOverlayToggle = panelHelper.AddCheckbox(Translation.GetString("Citizens"), citizenOverlay, onCitizenOverlayChanged) as UICheckBox;
			buildingOverlayToggle = panelHelper.AddCheckbox(Translation.GetString("Buildings"), buildingOverlay, onBuildingOverlayChanged) as UICheckBox;
#endif

			// MAINTENANCE
			++tabIndex;

			settingsButton = tabStrip.AddTab(Translation.GetString("Maintenance"), tabTemplate, true);
			settingsButton.textPadding = new RectOffset(10, 10, 10, 10);
			settingsButton.autoSize = true;
			settingsButton.tooltip = Translation.GetString("Maintenance");
			tabStrip.selectedIndex = tabIndex;

			currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
			currentPanel.autoLayout = true;
			currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
			currentPanel.autoLayoutPadding.top = 5;
			currentPanel.autoLayoutPadding.left = 10;
			currentPanel.autoLayoutPadding.right = 10;

			panelHelper = new UIHelper(currentPanel);

			forgetTrafficLightsBtn = panelHelper.AddButton(Translation.GetString("Forget_toggled_traffic_lights"), onClickForgetToggledLights) as UIButton;
			resetStuckEntitiesBtn = panelHelper.AddButton(Translation.GetString("Reset_stuck_cims_and_vehicles"), onClickResetStuckEntities) as UIButton;
			resetSpeedLimitsBtn = panelHelper.AddButton(Translation.GetString("Reset_custom_speed_limits"), onClickResetSpeedLimits) as UIButton;
			reloadGlobalConfBtn = panelHelper.AddButton(Translation.GetString("Reload_global_configuration"), onClickReloadGlobalConf) as UIButton;
			resetGlobalConfBtn = panelHelper.AddButton(Translation.GetString("Reset_global_configuration"), onClickResetGlobalConf) as UIButton;
#if DEBUG
			// DEBUG
			/*++tabIndex;

			settingsButton = tabStrip.AddTab("Debug", tabTemplate, true);
			settingsButton.textPadding = new RectOffset(10, 10, 10, 10);
			settingsButton.autoSize = true;
			settingsButton.tooltip = "Debug";

			currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
			currentPanel.autoLayout = true;
			currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
			currentPanel.autoLayoutPadding.top = 5;
			currentPanel.autoLayoutPadding.left = 10;
			currentPanel.autoLayoutPadding.right = 10;

			panelHelper = new UIHelper(currentPanel);
			
			debugSwitchFields.Clear();
			for (int i = 0; i < debugSwitches.Length; ++i) {
				int index = i;
				string varName = $"Debug switch #{i}";
				debugSwitchFields.Add(panelHelper.AddCheckbox(varName, debugSwitches[i], delegate (bool newVal) { onBoolValueChanged(varName, newVal, ref debugSwitches[index]); }) as UICheckBox);
			}

			debugValueFields.Clear();
			for (int i = 0; i < debugValues.Length; ++i) {
				int index = i;
				string varName = $"Debug value #{i}";
				debugValueFields.Add(panelHelper.AddTextfield(varName, String.Format("{0:0.##}", debugValues[i]), delegate(string newValStr) { onFloatValueChanged(varName, newValStr, ref debugValues[index]); }, null) as UITextField);
			}*/
#endif

			tabStrip.selectedIndex = 0;
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

		private static void onJunctionRestrictionsOverlayChanged(bool newValue) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"junctionRestrictionsOverlay changed to {newValue}");
			junctionRestrictionsOverlay = newValue;
		}

		private static void onConnectedLanesOverlayChanged(bool newValue) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"connectedLanesOverlay changed to {newValue}");
			connectedLanesOverlay = newValue;
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

		private static void onJunctionRestrictionsEnabledChanged(bool val) {
			if (!checkGameLoaded())
				return;

			MenuRebuildRequired = true;
			junctionRestrictionsEnabled = val;
			if (!val)
				setJunctionRestrictionsOverlay(false);
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

		private static void onProhibitPocketCarsChanged(bool newValue) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"prohibitPocketCars changed to {newValue}");
			prohibitPocketCars = newValue;
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

		private static void onCitizenOverlayChanged(bool newVal) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"Citizen overlay changed to {newVal}");
			citizenOverlay = newVal;
		}

		private static void onBuildingOverlayChanged(bool newVal) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"Building overlay changed to {newVal}");
			buildingOverlay = newVal;
		}

		private static void onFloatValueChanged(string varName, string newValueStr, ref float var) {
			if (!checkGameLoaded())
				return;

			try {
				float newValue = Single.Parse(newValueStr);
				var = newValue;
				Log._Debug($"{varName} changed to {newValue}");
			} catch (Exception e) {
				Log.Warning($"An invalid value was inserted: '{newValueStr}'. Error: {e.ToString()}");
				//UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Invalid value", "An invalid value was inserted.", false);
			}
		}

		private static void onBoolValueChanged(string varName, bool newVal, ref bool var) {
			if (!checkGameLoaded())
				return;

			var = newVal;
			Log._Debug($"{varName} changed to {newVal}");
		}


		private static void onClickForgetToggledLights() {
			if (!checkGameLoaded())
				return;

			Flags.resetTrafficLights(false);
		}

		private static void onClickResetStuckEntities() {
			if (!checkGameLoaded())
				return;

			UtilityManager.Instance().RequestResetStuckEntities();
		}

		private static void onClickResetSpeedLimits() {
			if (!checkGameLoaded())
				return;

			Flags.resetSpeedLimits();
		}

		private static void onClickReloadGlobalConf() {
			if (!checkGameLoaded())
				return;

			GlobalConfig.Reload();
		}

		private static void onClickResetGlobalConf() {
			if (!checkGameLoaded())
				return;

			GlobalConfig.Reset();
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

		public static void setProhibitPocketCars(bool newValue) {
			prohibitPocketCars = newValue;
			if (prohibitPocketCarsToggle != null)
				prohibitPocketCarsToggle.isChecked = newValue;
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

		public static void setJunctionRestrictionsOverlay(bool newValue) {
			junctionRestrictionsOverlay = newValue;
			if (junctionRestrictionsOverlayToggle != null)
				junctionRestrictionsOverlayToggle.isChecked = newValue;
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

		public static void setJunctionRestrictionsEnabled(bool newValue) {
			MenuRebuildRequired = true;
			junctionRestrictionsEnabled = newValue;
			if (enableJunctionRestrictionsToggle != null)
				enableJunctionRestrictionsToggle.isChecked = newValue;
			if (!newValue)
				setJunctionRestrictionsOverlay(false);
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
