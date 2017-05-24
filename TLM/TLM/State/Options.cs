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
using CSUtil.Commons;
using System.Reflection;

namespace TrafficManager.State {

	public class Options : MonoBehaviour {
		private static UIDropDown languageDropdown = null;
		private static UIDropDown simAccuracyDropdown = null;
		//private static UIDropDown laneChangingRandomizationDropdown = null;
		private static UICheckBox instantEffectsToggle = null;
		private static UICheckBox lockButtonToggle = null;
		private static UICheckBox lockMenuToggle = null;
		private static UICheckBox realisticSpeedsToggle = null;
		private static UIDropDown recklessDriversDropdown = null;
		private static UICheckBox relaxedBussesToggle = null;
		private static UICheckBox allRelaxedToggle = null;
		private static UICheckBox evacBussesMayIgnoreRulesToggle = null;
		private static UICheckBox prioritySignsOverlayToggle = null;
		private static UICheckBox timedLightsOverlayToggle = null;
		private static UICheckBox speedLimitsOverlayToggle = null;
		private static UICheckBox vehicleRestrictionsOverlayToggle = null;
		private static UICheckBox parkingRestrictionsOverlayToggle = null;
		private static UICheckBox junctionRestrictionsOverlayToggle = null;
		private static UICheckBox connectedLanesOverlayToggle = null;
		private static UICheckBox nodesOverlayToggle = null;
		private static UICheckBox vehicleOverlayToggle = null;
#if DEBUG
		private static UICheckBox citizenOverlayToggle = null;
		private static UICheckBox buildingOverlayToggle = null;
#endif
		private static UICheckBox allowEnterBlockedJunctionsToggle = null;
		private static UICheckBox allowUTurnsToggle = null;
		private static UICheckBox allowLaneChangesWhileGoingStraightToggle = null;
		private static UICheckBox banRegularTrafficOnBusLanesToggle = null;
		private static UICheckBox enableDespawningToggle = null;

		private static UICheckBox strongerRoadConditionEffectsToggle = null;
		private static UICheckBox prohibitPocketCarsToggle = null;
		private static UICheckBox advancedAIToggle = null;
		private static UICheckBox highwayRulesToggle = null;
		private static UICheckBox preferOuterLaneToggle = null;
		private static UICheckBox showLanesToggle = null;
		private static UIButton resetStuckEntitiesBtn = null;

		private static UICheckBox enablePrioritySignsToggle = null;
		private static UICheckBox enableTimedLightsToggle = null;
		private static UICheckBox enableCustomSpeedLimitsToggle = null;
		private static UICheckBox enableVehicleRestrictionsToggle = null;
		private static UICheckBox enableParkingRestrictionsToggle = null;
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

		public static bool instantEffects = true;
		public static int simAccuracy = 0;
		//public static int laneChangingRandomization = 2;
		public static bool realisticSpeeds = true;
		public static int recklessDrivers = 3;
		public static bool relaxedBusses = true;
		public static bool allRelaxed = false;
		public static bool evacBussesMayIgnoreRules = false;
		public static bool prioritySignsOverlay = false;
		public static bool timedLightsOverlay = false;
		public static bool speedLimitsOverlay = false;
		public static bool vehicleRestrictionsOverlay = false;
		public static bool parkingRestrictionsOverlay = false;
		public static bool junctionRestrictionsOverlay = false;
		public static bool connectedLanesOverlay = false;
#if DEBUG
		public static bool nodesOverlay = false;
		public static bool vehicleOverlay = false;
		public static bool citizenOverlay = false;
		public static bool buildingOverlay = false;
#else
		public static bool nodesOverlay = false;
		public static bool vehicleOverlay = false;
		public static bool citizenOverlay = false;
		public static bool buildingOverlay = false;
#endif
		public static bool allowEnterBlockedJunctions = false;
		public static bool allowUTurns = false;
		public static bool allowLaneChangesWhileGoingStraight = false;
		public static bool banRegularTrafficOnBusLanes = false;
		public static bool advancedAI = false;
		public static bool highwayRules = false;
#if DEBUG
		public static bool showLanes = true;
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
		public static bool parkingRestrictionsEnabled = true;
		public static bool junctionRestrictionsEnabled = true;
		public static bool laneConnectorEnabled = true;

		public static bool MenuRebuildRequired {
			get { return false; }
			internal set {
				if (value) {
					if (LoadingExtension.BaseUI != null) {
						LoadingExtension.BaseUI.RebuildMenu();
					}
				}
			}
		}

		public static void makeSettings(UIHelperBase helper) {
			// tabbing code is borrowed from RushHour mod
			// https://github.com/PropaneDragon/RushHour/blob/release/RushHour/Options/OptionHandler.cs

			UIHelper actualHelper = helper as UIHelper;
			UIComponent container = actualHelper.self as UIComponent;

			UITabstrip tabStrip = container.AddUIComponent<UITabstrip>();
			tabStrip.relativePosition = new Vector3(0, 0);
			tabStrip.size = new Vector2(container.width - 20, 40);

			UITabContainer tabContainer = container.AddUIComponent<UITabContainer>();
			tabContainer.relativePosition = new Vector3(0, 40);
			tabContainer.size = new Vector2(container.width - 20, container.height - tabStrip.height - 20);
			tabStrip.tabPages = tabContainer;

			int tabIndex = 0;
			// GENERAL

			AddOptionTab(tabStrip, Translation.GetString("General"));// tabStrip.AddTab(Translation.GetString("General"), tabTemplate, true);
			tabStrip.selectedIndex = tabIndex;

			UIPanel currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
			currentPanel.autoLayout = true;
			currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
			currentPanel.autoLayoutPadding.top = 5;
			currentPanel.autoLayoutPadding.left = 10;
			currentPanel.autoLayoutPadding.right = 10;

			UIHelper panelHelper = new UIHelper(currentPanel);

			var generalGroup = panelHelper.AddGroup(Translation.GetString("General"));

			string[] languageLabels = new string[Translation.AVAILABLE_LANGUAGE_CODES.Count + 1];
			languageLabels[0] = Translation.GetString("Game_language");
			for (int i = 0; i < Translation.AVAILABLE_LANGUAGE_CODES.Count; ++i) {
				languageLabels[i + 1] = Translation.LANGUAGE_LABELS[Translation.AVAILABLE_LANGUAGE_CODES[i]];
			}
			int languageIndex = 0;
			string curLangCode = GlobalConfig.Instance.LanguageCode;
			if (curLangCode != null) {
				languageIndex = Translation.AVAILABLE_LANGUAGE_CODES.IndexOf(curLangCode);
				if (languageIndex < 0) {
					languageIndex = 0;
				} else {
					++languageIndex;
				}
			}

			languageDropdown = generalGroup.AddDropdown(Translation.GetString("Language") + ":", languageLabels, languageIndex, onLanguageChanged) as UIDropDown;
			lockButtonToggle = generalGroup.AddCheckbox(Translation.GetString("Lock_main_menu_button_position"), GlobalConfig.Instance.MainMenuButtonPosLocked, onLockButtonChanged) as UICheckBox;
			lockMenuToggle = generalGroup.AddCheckbox(Translation.GetString("Lock_main_menu_position"), GlobalConfig.Instance.MainMenuPosLocked, onLockMenuChanged) as UICheckBox;

			var simGroup = panelHelper.AddGroup(Translation.GetString("Simulation"));
			simAccuracyDropdown = simGroup.AddDropdown(Translation.GetString("Simulation_accuracy") + ":", new string[] { Translation.GetString("Very_high"), Translation.GetString("High"), Translation.GetString("Medium"), Translation.GetString("Low"), Translation.GetString("Very_Low") }, simAccuracy, onSimAccuracyChanged) as UIDropDown;
			instantEffectsToggle = simGroup.AddCheckbox(Translation.GetString("Customizations_come_into_effect_instantaneously"), instantEffects, onInstantEffectsChanged) as UICheckBox;

			var featureGroup = panelHelper.AddGroup(Translation.GetString("Activated_features"));
			enablePrioritySignsToggle = featureGroup.AddCheckbox(Translation.GetString("Priority_signs"), prioritySignsEnabled, onPrioritySignsEnabledChanged) as UICheckBox;
			enableTimedLightsToggle = featureGroup.AddCheckbox(Translation.GetString("Timed_traffic_lights"), timedLightsEnabled, onTimedLightsEnabledChanged) as UICheckBox;
			enableCustomSpeedLimitsToggle = featureGroup.AddCheckbox(Translation.GetString("Speed_limits"), customSpeedLimitsEnabled, onCustomSpeedLimitsEnabledChanged) as UICheckBox;
			enableVehicleRestrictionsToggle = featureGroup.AddCheckbox(Translation.GetString("Vehicle_restrictions"), vehicleRestrictionsEnabled, onVehicleRestrictionsEnabledChanged) as UICheckBox;
			enableParkingRestrictionsToggle = featureGroup.AddCheckbox(Translation.GetString("Parking_restrictions") + " (BETA feature)", parkingRestrictionsEnabled, onParkingRestrictionsEnabledChanged) as UICheckBox;
			enableJunctionRestrictionsToggle = featureGroup.AddCheckbox(Translation.GetString("Junction_restrictions"), junctionRestrictionsEnabled, onJunctionRestrictionsEnabledChanged) as UICheckBox;
			enableLaneConnectorToggle = featureGroup.AddCheckbox(Translation.GetString("Lane_connector"), laneConnectorEnabled, onLaneConnectorEnabledChanged) as UICheckBox;

			// GAMEPLAY
			++tabIndex;

			AddOptionTab(tabStrip, Translation.GetString("Gameplay"));
			tabStrip.selectedIndex = tabIndex;

			currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
			currentPanel.autoLayout = true;
			currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
			currentPanel.autoLayoutPadding.top = 5;
			currentPanel.autoLayoutPadding.left = 10;
			currentPanel.autoLayoutPadding.right = 10;

			panelHelper = new UIHelper(currentPanel);

			var vehBehaviorGroup = panelHelper.AddGroup(Translation.GetString("Vehicle_behavior"));

			recklessDriversDropdown = vehBehaviorGroup.AddDropdown(Translation.GetString("Reckless_driving") + ":", new string[] { Translation.GetString("Path_Of_Evil_(10_%)"), Translation.GetString("Rush_Hour_(5_%)"), Translation.GetString("Minor_Complaints_(2_%)"), Translation.GetString("Holy_City_(0_%)") }, recklessDrivers, onRecklessDriversChanged) as UIDropDown;
			realisticSpeedsToggle = vehBehaviorGroup.AddCheckbox(Translation.GetString("Realistic_speeds"), realisticSpeeds, onRealisticSpeedsChanged) as UICheckBox;
			strongerRoadConditionEffectsToggle = vehBehaviorGroup.AddCheckbox(Translation.GetString("Road_condition_has_a_bigger_impact_on_vehicle_speed"), strongerRoadConditionEffects, onStrongerRoadConditionEffectsChanged) as UICheckBox;
			enableDespawningToggle = vehBehaviorGroup.AddCheckbox(Translation.GetString("Enable_despawning"), enableDespawning, onEnableDespawningChanged) as UICheckBox;

			var vehAiGroup = panelHelper.AddGroup(Translation.GetString("Advanced_Vehicle_AI"));
			advancedAIToggle = vehAiGroup.AddCheckbox(Translation.GetString("Enable_Advanced_Vehicle_AI"), advancedAI, onAdvancedAIChanged) as UICheckBox;
			highwayRulesToggle = vehAiGroup.AddCheckbox(Translation.GetString("Enable_highway_specific_lane_merging/splitting_rules"), highwayRules, onHighwayRulesChanged) as UICheckBox;
			preferOuterLaneToggle = vehAiGroup.AddCheckbox(Translation.GetString("Heavy_trucks_prefer_outer_lanes_on_highways"), preferOuterLane, onPreferOuterLaneChanged) as UICheckBox;

			var parkAiGroup = panelHelper.AddGroup(Translation.GetString("Parking_AI"));
			prohibitPocketCarsToggle = parkAiGroup.AddCheckbox(Translation.GetString("Enable_more_realistic_parking") + " (BETA feature)", prohibitPocketCars, onProhibitPocketCarsChanged) as UICheckBox;

			// VEHICLE RESTRICTIONS
			++tabIndex;

			AddOptionTab(tabStrip, Translation.GetString("Policies_&_Restrictions"));
			tabStrip.selectedIndex = tabIndex;

			currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
			currentPanel.autoLayout = true;
			currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
			currentPanel.autoLayoutPadding.top = 5;
			currentPanel.autoLayoutPadding.left = 10;
			currentPanel.autoLayoutPadding.right = 10;

			panelHelper = new UIHelper(currentPanel);

			var atJunctionsGroup = panelHelper.AddGroup(Translation.GetString("At_junctions"));
#if DEBUG
			allRelaxedToggle = atJunctionsGroup.AddCheckbox(Translation.GetString("All_vehicles_may_ignore_lane_arrows"), allRelaxed, onAllRelaxedChanged) as UICheckBox;
#endif
			relaxedBussesToggle = atJunctionsGroup.AddCheckbox(Translation.GetString("Busses_may_ignore_lane_arrows"), relaxedBusses, onRelaxedBussesChanged) as UICheckBox;
			allowEnterBlockedJunctionsToggle = atJunctionsGroup.AddCheckbox(Translation.GetString("Vehicles_may_enter_blocked_junctions"), allowEnterBlockedJunctions, onAllowEnterBlockedJunctionsChanged) as UICheckBox;
			allowUTurnsToggle = atJunctionsGroup.AddCheckbox(Translation.GetString("Vehicles_may_do_u-turns_at_junctions"), allowUTurns, onAllowUTurnsChanged) as UICheckBox;
			allowLaneChangesWhileGoingStraightToggle = atJunctionsGroup.AddCheckbox(Translation.GetString("Vehicles_going_straight_may_change_lanes_at_junctions"), allowLaneChangesWhileGoingStraight, onAllowLaneChangesWhileGoingStraightChanged) as UICheckBox;

			var onRoadsGroup = panelHelper.AddGroup(Translation.GetString("On_roads"));
			banRegularTrafficOnBusLanesToggle = onRoadsGroup.AddCheckbox(Translation.GetString("Ban_private_cars_and_trucks_on_bus_lanes"), banRegularTrafficOnBusLanes, onBanRegularTrafficOnBusLanesChanged) as UICheckBox;

			if (SteamHelper.IsDLCOwned(SteamHelper.DLC.NaturalDisastersDLC)) {
				var inCaseOfEmergencyGroup = panelHelper.AddGroup(Translation.GetString("In_case_of_emergency"));
				evacBussesMayIgnoreRulesToggle = inCaseOfEmergencyGroup.AddCheckbox(Translation.GetString("Evacuation_busses_may_ignore_traffic_rules"), evacBussesMayIgnoreRules, onEvacBussesMayIgnoreRulesChanged) as UICheckBox;
			}

			// OVERLAYS
			++tabIndex;

			AddOptionTab(tabStrip, Translation.GetString("Overlays"));
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
			parkingRestrictionsOverlayToggle = panelHelper.AddCheckbox(Translation.GetString("Parking_restrictions"), parkingRestrictionsOverlay, onParkingRestrictionsOverlayChanged) as UICheckBox;
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

			AddOptionTab(tabStrip, Translation.GetString("Maintenance"));
			tabStrip.selectedIndex = tabIndex;

			currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
			currentPanel.autoLayout = true;
			currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
			currentPanel.autoLayoutPadding.top = 5;
			currentPanel.autoLayoutPadding.left = 10;
			currentPanel.autoLayoutPadding.right = 10;

			panelHelper = new UIHelper(currentPanel);

			resetStuckEntitiesBtn = panelHelper.AddButton(Translation.GetString("Reset_stuck_cims_and_vehicles"), onClickResetStuckEntities) as UIButton;
#if DEBUG
			resetSpeedLimitsBtn = panelHelper.AddButton(Translation.GetString("Reset_custom_speed_limits"), onClickResetSpeedLimits) as UIButton;
#endif
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

		private static UIButton AddOptionTab(UITabstrip tabStrip, string caption) {
			UIButton tabButton = tabStrip.AddTab(caption);

			tabButton.normalBgSprite = "SubBarButtonBase";
			tabButton.disabledBgSprite = "SubBarButtonBaseDisabled";
			tabButton.focusedBgSprite = "SubBarButtonBaseFocused";
			tabButton.hoveredBgSprite = "SubBarButtonBaseHovered";
			tabButton.pressedBgSprite = "SubBarButtonBasePressed";

			tabButton.textPadding = new RectOffset(10, 10, 10, 10);
			tabButton.autoSize = true;
			tabButton.tooltip = caption;

			return tabButton;
		}

		private static bool checkGameLoaded() {
			if (!SerializableDataExtension.StateLoading && !LoadingExtension.IsGameLoaded) {
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

			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
		}

		private static void onTimedLightsOverlayChanged(bool newTimedLightsOverlay) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"timedLightsOverlay changed to {newTimedLightsOverlay}");
			timedLightsOverlay = newTimedLightsOverlay;

			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
		}

		private static void onSpeedLimitsOverlayChanged(bool newSpeedLimitsOverlay) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"speedLimitsOverlay changed to {newSpeedLimitsOverlay}");
			speedLimitsOverlay = newSpeedLimitsOverlay;

			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
		}

		private static void onVehicleRestrictionsOverlayChanged(bool newVehicleRestrictionsOverlay) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"vehicleRestrictionsOverlay changed to {newVehicleRestrictionsOverlay}");
			vehicleRestrictionsOverlay = newVehicleRestrictionsOverlay;

			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
		}

		private static void onParkingRestrictionsOverlayChanged(bool newParkingRestrictionsOverlay) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"parkingRestrictionsOverlay changed to {newParkingRestrictionsOverlay}");
			parkingRestrictionsOverlay = newParkingRestrictionsOverlay;

			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
		}

		private static void onJunctionRestrictionsOverlayChanged(bool newValue) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"junctionRestrictionsOverlay changed to {newValue}");
			junctionRestrictionsOverlay = newValue;

			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
		}

		private static void onConnectedLanesOverlayChanged(bool newValue) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"connectedLanesOverlay changed to {newValue}");
			connectedLanesOverlay = newValue;

			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
		}

		private static void onLanguageChanged(int newLanguageIndex) {
			bool localeChanged = false;

			if (newLanguageIndex <= 0) {
				GlobalConfig.Instance.LanguageCode = null;
				GlobalConfig.WriteConfig();
				MenuRebuildRequired = true;
				localeChanged = true;
			} else if (newLanguageIndex - 1 < Translation.AVAILABLE_LANGUAGE_CODES.Count) {
				GlobalConfig.Instance.LanguageCode = Translation.AVAILABLE_LANGUAGE_CODES[newLanguageIndex - 1];
				GlobalConfig.WriteConfig();
				MenuRebuildRequired = true;
				localeChanged = true;
			} else {
				Log.Warning($"Options.onLanguageChanged: Invalid language index: {newLanguageIndex}");
			}

			if (localeChanged) {
				MethodInfo onChangedHandler = typeof(OptionsMainPanel).GetMethod("OnLocaleChanged", BindingFlags.Instance | BindingFlags.NonPublic);
				if (onChangedHandler != null) {
					onChangedHandler.Invoke(UIView.library.Get<OptionsMainPanel>("OptionsPanel"), new object[0] { });
				}
			}
		}

		private static void onLockButtonChanged(bool newValue) {
			Log._Debug($"Button lock changed to {newValue}");
			LoadingExtension.BaseUI.MainMenuButton.SetPosLock(newValue);
			GlobalConfig.Instance.MainMenuButtonPosLocked = newValue;
			GlobalConfig.WriteConfig();
		}

		private static void onLockMenuChanged(bool newValue) {
			Log._Debug($"Menu lock changed to {newValue}");
			LoadingExtension.BaseUI.MainMenu.SetPosLock(newValue);
			GlobalConfig.Instance.MainMenuPosLocked = newValue;
			GlobalConfig.WriteConfig();
		}

		private static void onInstantEffectsChanged(bool newValue) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"Instant effects changed to {newValue}");
			instantEffects = newValue;
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

			Log._Debug($"advancedAI changed to {newAdvancedAI}");
			setAdvancedAI(newAdvancedAI);
		}

		private static void onHighwayRulesChanged(bool newHighwayRules) {
			if (!checkGameLoaded())
				return;

			bool changed = newHighwayRules != highwayRules;
			if (!changed) {
				return;
			}

			Log._Debug($"Highway rules changed to {newHighwayRules}");
			highwayRules = newHighwayRules;
			Flags.clearHighwayLaneArrows();
			Flags.applyAllFlags();
			RoutingManager.Instance.RequestFullRecalculation(true);
			if (newHighwayRules)
				setAdvancedAI(true);
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
				VehicleStateManager.Instance.InitAllVehicles();
			else
				setPrioritySignsOverlay(false);
		}

		private static void onTimedLightsEnabledChanged(bool val) {
			if (!checkGameLoaded())
				return;

			MenuRebuildRequired = true;
			timedLightsEnabled = val;
			if (val)
				VehicleStateManager.Instance.InitAllVehicles();
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

		private static void onParkingRestrictionsEnabledChanged(bool val) {
			if (!checkGameLoaded())
				return;

			MenuRebuildRequired = true;
			parkingRestrictionsEnabled = val;
			if (!val)
				setParkingRestrictionsOverlay(false);
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

			bool changed = val != laneConnectorEnabled;
			if (!changed) {
				return;
			}

			MenuRebuildRequired = true;
			laneConnectorEnabled = val;
			RoutingManager.Instance.RequestFullRecalculation(true);
			if (!val)
				setConnectedLanesOverlay(false);
		}

		private static void onEvacBussesMayIgnoreRulesChanged(bool value) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"evacBussesMayIgnoreRules changed to {value}");
			evacBussesMayIgnoreRules = value;
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

		private static void onBanRegularTrafficOnBusLanesChanged(bool newValue) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"banRegularTrafficOnBusLanes changed to {newValue}");
			banRegularTrafficOnBusLanes = newValue;
			VehicleRestrictionsManager.Instance.ClearCache();
			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
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
			ExtCitizenInstanceManager.Instance.Reset();
		}

		private static void onRealisticSpeedsChanged(bool value) {
			if (!checkGameLoaded())
				return;

			Log._Debug($"realisticSpeeds changed to {value}");
			realisticSpeeds = value;
		}

		private static void onEnableDespawningChanged(bool value) {
			if (!checkGameLoaded())
				return;

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

		private static void onClickResetStuckEntities() {
			if (!checkGameLoaded())
				return;

			UtilityManager.Instance.RequestResetStuckEntities();
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

			GlobalConfig.Reset(null);
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
			highwayRules = newHighwayRules;

			if (highwayRulesToggle != null)
				highwayRulesToggle.isChecked = highwayRules;
		}

		public static void setPreferOuterLane(bool val) {
			preferOuterLane = val;

			if (preferOuterLaneToggle != null)
				preferOuterLaneToggle.isChecked = preferOuterLane;
		}

		public static void setShowLanes(bool newShowLanes) {
			showLanes = newShowLanes;
			if (showLanesToggle != null)
				showLanesToggle.isChecked = newShowLanes;
		}

		public static void setAdvancedAI(bool newAdvancedAI) {
			advancedAI = newAdvancedAI;

			if (advancedAIToggle != null)
				advancedAIToggle.isChecked = newAdvancedAI;

			if (!newAdvancedAI) {
				setHighwayRules(false);
				setPreferOuterLane(false);
			}
		}

		public static void setEvacBussesMayIgnoreRules(bool value) {
			if (! SteamHelper.IsDLCOwned(SteamHelper.DLC.NaturalDisastersDLC))
				value = false;

			evacBussesMayIgnoreRules = value;
			if (evacBussesMayIgnoreRulesToggle != null)
				evacBussesMayIgnoreRulesToggle.isChecked = value;
		}

		public static void setInstantEffects(bool value) {
			instantEffects = value;
			if (instantEffectsToggle != null)
				instantEffectsToggle.isChecked = value;
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
			ExtCitizenInstanceManager.Instance.Reset();
		}

		public static void setRealisticSpeeds(bool newValue) {
			realisticSpeeds = newValue;
			if (realisticSpeedsToggle != null)
				realisticSpeedsToggle.isChecked = newValue;
		}

		public static void setEnableDespawning(bool value) {
			enableDespawning = value;

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

		public static void setBanRegularTrafficOnBusLanes(bool value) {
			banRegularTrafficOnBusLanes = value;
			if (banRegularTrafficOnBusLanesToggle != null)
				banRegularTrafficOnBusLanesToggle.isChecked = value;

			VehicleRestrictionsManager.Instance.ClearCache();
			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
		}

		public static void setPrioritySignsOverlay(bool newPrioritySignsOverlay) {
			prioritySignsOverlay = newPrioritySignsOverlay;
			if (prioritySignsOverlayToggle != null)
				prioritySignsOverlayToggle.isChecked = newPrioritySignsOverlay;

			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
		}

		public static void setTimedLightsOverlay(bool newTimedLightsOverlay) {
			timedLightsOverlay = newTimedLightsOverlay;
			if (timedLightsOverlayToggle != null)
				timedLightsOverlayToggle.isChecked = newTimedLightsOverlay;

			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
		}

		public static void setSpeedLimitsOverlay(bool newSpeedLimitsOverlay) {
			speedLimitsOverlay = newSpeedLimitsOverlay;
			if (speedLimitsOverlayToggle != null)
				speedLimitsOverlayToggle.isChecked = newSpeedLimitsOverlay;

			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
		}

		public static void setVehicleRestrictionsOverlay(bool newVehicleRestrictionsOverlay) {
			vehicleRestrictionsOverlay = newVehicleRestrictionsOverlay;
			if (vehicleRestrictionsOverlayToggle != null)
				vehicleRestrictionsOverlayToggle.isChecked = newVehicleRestrictionsOverlay;

			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
		}

		public static void setParkingRestrictionsOverlay(bool newParkingRestrictionsOverlay) {
			parkingRestrictionsOverlay = newParkingRestrictionsOverlay;
			if (parkingRestrictionsOverlayToggle != null)
				parkingRestrictionsOverlayToggle.isChecked = newParkingRestrictionsOverlay;

			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
		}

		public static void setJunctionRestrictionsOverlay(bool newValue) {
			junctionRestrictionsOverlay = newValue;
			if (junctionRestrictionsOverlayToggle != null)
				junctionRestrictionsOverlayToggle.isChecked = newValue;

			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
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

			UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
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

		public static void setParkingRestrictionsEnabled(bool newValue) {
			MenuRebuildRequired = true;
			parkingRestrictionsEnabled = newValue;
			if (enableParkingRestrictionsToggle != null)
				enableParkingRestrictionsToggle.isChecked = newValue;
			if (!newValue)
				setParkingRestrictionsOverlay(false);
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
