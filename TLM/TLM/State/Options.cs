﻿using System;
using System.Collections.Generic;
using UnityEngine;
using ColossalFramework.UI;
using ICities;
using TrafficManager.UI;
using TrafficManager.Manager;
using CSUtil.Commons;
using System.Reflection;
using TrafficManager.Manager.Impl;
using TrafficManager.Traffic.Enums;
using TrafficManager.State.Keybinds;
using TrafficManager.Traffic.Data;

namespace TrafficManager.State {
    using API.Traffic.Enums;

    public class Options : MonoBehaviour {
        private static UIDropDown languageDropdown = null;
        //private static UIDropDown laneChangingRandomizationDropdown = null;
        private static UICheckBox instantEffectsToggle = null;
        private static UICheckBox lockButtonToggle = null;
        private static UICheckBox lockMenuToggle = null;
        private static UISlider guiTransparencySlider = null;
        private static UISlider overlayTransparencySlider = null;
        private static UICheckBox tinyMenuToggle = null;
        private static UICheckBox enableTutorialToggle = null;
        private static UICheckBox showCompatibilityCheckErrorToggle = null;
        private static UICheckBox scanForKnownIncompatibleModsToggle = null;
        private static UICheckBox ignoreDisabledModsToggle = null;
        private static UICheckBox displayMphToggle = null;
        private static UIDropDown roadSignsMphThemeDropdown = null;
        private static UICheckBox individualDrivingStyleToggle = null;
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
        private static UICheckBox allowNearTurnOnRedToggle = null;
        private static UICheckBox allowFarTurnOnRedToggle = null;
        private static UICheckBox allowLaneChangesWhileGoingStraightToggle = null;
        private static UICheckBox trafficLightPriorityRulesToggle = null;
        private static UIDropDown vehicleRestrictionsAggressionDropdown = null;
        private static UICheckBox banRegularTrafficOnBusLanesToggle = null;
        private static UICheckBox disableDespawningToggle = null;

        private static UICheckBox strongerRoadConditionEffectsToggle = null;
        private static UICheckBox prohibitPocketCarsToggle = null;
        private static UICheckBox advancedAIToggle = null;
        private static UICheckBox realisticPublicTransportToggle = null;
        private static UISlider altLaneSelectionRatioSlider = null;
        private static UICheckBox highwayRulesToggle = null;
        private static UICheckBox preferOuterLaneToggle = null;
        private static UICheckBox showLanesToggle = null;
#if QUEUEDSTATS
        private static UICheckBox showPathFindStatsToggle = null;
#endif
        private static UIButton resetStuckEntitiesBtn = null;

        private static UICheckBox enablePrioritySignsToggle = null;
        private static UICheckBox enableTimedLightsToggle = null;
        private static UICheckBox enableCustomSpeedLimitsToggle = null;
        private static UICheckBox enableVehicleRestrictionsToggle = null;
        private static UICheckBox enableParkingRestrictionsToggle = null;
        private static UICheckBox enableJunctionRestrictionsToggle = null;
        private static UICheckBox turnOnRedEnabledToggle = null;
        private static UICheckBox enableLaneConnectorToggle = null;

        private static UIButton removeParkedVehiclesBtn = null;
#if DEBUG
        private static UIButton resetSpeedLimitsBtn = null;
        private static List<UICheckBox> debugSwitchFields = new List<UICheckBox>();
        private static List<UITextField> debugValueFields = new List<UITextField>();
        private static UITextField pathCostMultiplicatorField = null;
        private static UITextField pathCostMultiplicator2Field = null;
#endif
        private static UIButton reloadGlobalConfBtn = null;
        private static UIButton resetGlobalConfBtn = null;

        public static int roadSignMphStyleInt;
        public static bool instantEffects = true;
        //public static int laneChangingRandomization = 2;
        public static bool individualDrivingStyle = true;
        public static int recklessDrivers = 3;

        /// <summary>
        /// Option: buses may ignore lane arrows
        /// </summary>
        public static bool relaxedBusses = false;

        /// <summary>
        /// debug option: all vehicles may ignore lane arrows
        /// </summary>
        public static bool allRelaxed = false;
        public static bool evacBussesMayIgnoreRules = false;
        public static bool prioritySignsOverlay = false;
        public static bool timedLightsOverlay = false;
        public static bool speedLimitsOverlay = false;
        public static bool vehicleRestrictionsOverlay = false;
        public static bool parkingRestrictionsOverlay = false;
        public static bool junctionRestrictionsOverlay = false;
        public static bool connectedLanesOverlay = false;
#if QUEUEDSTATS
        public static bool showPathFindStats =
#if DEBUG
            true;
#else
            false;
#endif

#endif
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
        public static bool allowNearTurnOnRed = false;
        public static bool allowFarTurnOnRed = false;
        public static bool allowLaneChangesWhileGoingStraight = false;
        public static bool trafficLightPriorityRules = false;
        public static bool banRegularTrafficOnBusLanes = false;
        public static bool advancedAI = false;
        public static bool realisticPublicTransport = false;
        public static byte altLaneSelectionRatio = 0;
        public static bool highwayRules = false;
#if DEBUG
        public static bool showLanes = true;
#else
        public static bool showLanes = false;
#endif
        public static bool strongerRoadConditionEffects = false;
        public static bool parkingAI = false;
        public static bool disableDespawning = false;
        public static bool preferOuterLane = false;
        //public static byte publicTransportUsage = 1;

        public static bool prioritySignsEnabled = true;
        public static bool timedLightsEnabled = true;
        public static bool customSpeedLimitsEnabled = true;
        public static bool vehicleRestrictionsEnabled = true;
        public static bool parkingRestrictionsEnabled = true;
        public static bool junctionRestrictionsEnabled = true;
        public static bool turnOnRedEnabled = true;
        public static bool laneConnectorEnabled = true;
        public static bool scanForKnownIncompatibleModsEnabled = true;
        public static bool ignoreDisabledModsEnabled = false;

        public static VehicleRestrictionsAggression vehicleRestrictionsAggression = VehicleRestrictionsAggression.Medium;

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

        public static void MakeSettings(UIHelperBase helper) {
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
            UIPanel currentPanel;
            UIHelper panelHelper;
            MakeSettings_General(tabStrip, tabIndex);

            // GAMEPLAY
            ++tabIndex;
            MakeSettings_Gameplay(tabStrip, tabIndex);

            // VEHICLE RESTRICTIONS
            ++tabIndex;
            MakeSettings_VehicleRestrictions(tabStrip, tabIndex);

            // OVERLAYS
            ++tabIndex;
            MakeSettings_Overlays(tabStrip, tabIndex);

            // MAINTENANCE
            ++tabIndex;
            MakeSettings_Maintenance(tabStrip, tabIndex);

            // KEYBOARD
            ++tabIndex;
            MakeSettings_Keybinds(tabStrip, tabIndex);

#if DEBUG
            // GLOBAL CONFIG
            /*
            AddOptionTab(tabStrip, Translation.GetString("Global_configuration"));// tabStrip.AddTab(Translation.GetString("General"), tabTemplate, true);
            tabStrip.selectedIndex = tabIndex;

            currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
            currentPanel.autoLayout = true;
            currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
            currentPanel.autoLayoutPadding.top = 5;
            currentPanel.autoLayoutPadding.left = 10;
            currentPanel.autoLayoutPadding.right = 10;

            panelHelper = new UIHelper(currentPanel);

            GlobalConfig globalConf = GlobalConfig.Instance;

            var aiTrafficMeasurementConfGroup = panelHelper.AddGroup(Translation.GetString("Advanced_Vehicle_AI") + ": " + Translation.GetString("General"));

            aiTrafficMeasurementConfGroup.AddSlider(Translation.GetString("Live_traffic_buffer_size"), 0f, 10000f, 100f, globalConf.MaxTrafficBuffer, onMaxTrafficBufferChanged);
            aiTrafficMeasurementConfGroup.AddSlider(Translation.GetString("Path-find_traffic_buffer_size"), 0f, 10000f, 100f, globalConf.MaxPathFindTrafficBuffer, onMaxPathFindTrafficBufferChanged);
            aiTrafficMeasurementConfGroup.AddSlider(Translation.GetString("Max._congestion_measurements"), 0f, 1000f, 10f, globalConf.MaxNumCongestionMeasurements, onMaxNumCongestionMeasurementsChanged);

            var aiLaneSelParamConfGroup = panelHelper.AddGroup(Translation.GetString("Advanced_Vehicle_AI") + ": " + Translation.GetString("Lane_selection_parameters"));

            aiLaneSelParamConfGroup.AddSlider(Translation.GetString("Lane_density_randomization"), 0f, 100f, 1f, globalConf.LaneDensityRandInterval, onLaneDensityRandIntervalChanged);
            aiLaneSelParamConfGroup.AddSlider(Translation.GetString("Lane_density_discretization"), 0f, 100f, 1f, globalConf.LaneDensityDiscretization, onLaneTrafficDiscretizationChanged);
            aiLaneSelParamConfGroup.AddSlider(Translation.GetString("Lane_spread_randomization"), 0f, 100f, 1f, globalConf.LaneUsageRandInterval, onLaneUsageRandIntervalChanged);
            aiLaneSelParamConfGroup.AddSlider(Translation.GetString("Lane_spread_discretization"), 0f, 100f, 1f, globalConf.LaneUsageDiscretization, onLaneUsageDiscretizationChanged);
            aiLaneSelParamConfGroup.AddSlider(Translation.GetString("Congestion_rel._velocity_threshold"), 0f, 100f, 1f, globalConf.CongestionSqrSpeedThreshold, onCongestionSqrSpeedThresholdChanged);
            aiLaneSelParamConfGroup.AddSlider(Translation.GetString("Max._walking_distance"), 0f, 10000f, 100f, globalConf.MaxWalkingDistance, onMaxWalkingDistanceChanged);

            var aiLaneSelFactorConfGroup = panelHelper.AddGroup(Translation.GetString("Advanced_Vehicle_AI") + ": " + Translation.GetString("Lane_selection_factors"));

            aiLaneSelFactorConfGroup.AddSlider(Translation.GetString("Spread_randomization_factor"), 1f, 5f, 0.05f, globalConf.UsageCostFactor, onUsageCostFactorChanged);
            aiLaneSelFactorConfGroup.AddSlider(Translation.GetString("Traffic_avoidance_factor"), 1f, 5f, 0.05f, globalConf.TrafficCostFactor, onTrafficCostFactorChanged);
            aiLaneSelFactorConfGroup.AddSlider(Translation.GetString("Public_transport_lane_penalty"), 1f, 50f, 0.5f, globalConf.PublicTransportLanePenalty, onPublicTransportLanePenaltyChanged);
            aiLaneSelFactorConfGroup.AddSlider(Translation.GetString("Public_transport_lane_reward"), 0f, 1f, 0.05f, globalConf.PublicTransportLaneReward, onPublicTransportLaneRewardChanged);
            aiLaneSelFactorConfGroup.AddSlider(Translation.GetString("Heavy_vehicle_max._inner_lane_penalty"), 0f, 5f, 0.05f, globalConf.HeavyVehicleMaxInnerLanePenalty, onHeavyVehicleMaxInnerLanePenaltyChanged);
            aiLaneSelFactorConfGroup.AddSlider(Translation.GetString("Vehicle_restrictions_penalty"), 0f, 1000f, 25f, globalConf.VehicleRestrictionsPenalty, onVehicleRestrictionsPenaltyChanged);

            var aiLaneChangeParamConfGroup = panelHelper.AddGroup(Translation.GetString("Advanced_Vehicle_AI") + ": " + Translation.GetString("Lane_changing_parameters"));

            aiLaneChangeParamConfGroup.AddSlider(Translation.GetString("U-turn_lane_distance"), 1f, 5f, 1f, globalConf.UturnLaneDistance, onUturnLaneDistanceChanged);
            aiLaneChangeParamConfGroup.AddSlider(Translation.GetString("Incompatible_lane_distance"), 1f, 5f, 1f, globalConf.IncompatibleLaneDistance, onIncompatibleLaneDistanceChanged);
            aiLaneChangeParamConfGroup.AddSlider(Translation.GetString("Lane_changing_randomization_modulo"), 1f, 100f, 1f, globalConf.RandomizedLaneChangingModulo, onRandomizedLaneChangingModuloChanged);
            aiLaneChangeParamConfGroup.AddSlider(Translation.GetString("Min._controlled_segments_in_front_of_highway_interchanges"), 1f, 10f, 1f, globalConf.MinHighwayInterchangeSegments, onMinHighwayInterchangeSegmentsChanged);
            aiLaneChangeParamConfGroup.AddSlider(Translation.GetString("Max._controlled_segments_in_front_of_highway_interchanges"), 1f, 30f, 1f, globalConf.MaxHighwayInterchangeSegments, onMaxHighwayInterchangeSegmentsChanged);

            var aiLaneChangeFactorConfGroup = panelHelper.AddGroup(Translation.GetString("Advanced_Vehicle_AI") + ": " + Translation.GetString("Lane_changing_cost_factors"));

            aiLaneChangeFactorConfGroup.AddSlider(Translation.GetString("On_city_roads"), 1f, 5f, 0.05f, globalConf.CityRoadLaneChangingBaseCost, onCityRoadLaneChangingBaseCostChanged);
            aiLaneChangeFactorConfGroup.AddSlider(Translation.GetString("On_highways"), 1f, 5f, 0.05f, globalConf.HighwayLaneChangingBaseCost, onHighwayLaneChangingBaseCostChanged);
            aiLaneChangeFactorConfGroup.AddSlider(Translation.GetString("In_front_of_highway_interchanges"), 1f, 5f, 0.05f, globalConf.HighwayInterchangeLaneChangingBaseCost, onHighwayInterchangeLaneChangingBaseCostChanged);
            aiLaneChangeFactorConfGroup.AddSlider(Translation.GetString("For_heavy_vehicles"), 1f, 5f, 0.05f, globalConf.HeavyVehicleLaneChangingCostFactor, onHeavyVehicleLaneChangingCostFactorChanged);
            aiLaneChangeFactorConfGroup.AddSlider(Translation.GetString("On_congested_roads"), 1f, 5f, 0.05f, globalConf.CongestionLaneChangingCostFactor, onCongestionLaneChangingCostFactorChanged);
            aiLaneChangeFactorConfGroup.AddSlider(Translation.GetString("When_changing_multiple_lanes_at_once"), 1f, 5f, 0.05f, globalConf.MoreThanOneLaneChangingCostFactor, onMoreThanOneLaneChangingCostFactorChanged);

            var aiParkingLaneChangeFactorConfGroup = panelHelper.AddGroup(Translation.GetString("Parking_AI") + ": " + Translation.GetString("General"));
            */

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
            for (int i = 0; i < Debug.Switches.Length; ++i) {
                int index = i;
                string varName = $"Debug switch #{i}";
                debugSwitchFields.Add(panelHelper.AddCheckbox(varName, Debug.Switches[i], delegate (bool newVal) { onBoolValueChanged(varName, newVal, ref Debug.Switches[index]); }) as UICheckBox);
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

        private static void MakeSettings_Keybinds(UITabstrip tabStrip, int tabIndex) {
            UIPanel currentPanel;
            UIHelper panelHelper;
            AddOptionTab(tabStrip, Translation.GetString("Keybinds"));
            tabStrip.selectedIndex = tabIndex;

            currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
            currentPanel.autoLayout = true;
            currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
            currentPanel.autoLayoutPadding.top = 5;
            currentPanel.autoLayoutPadding.left = 10;
            currentPanel.autoLayoutPadding.right = 10;

            panelHelper = new UIHelper(currentPanel);

            var keyboardGroup = panelHelper.AddGroup(Translation.GetString("Keybinds"));
            ((UIPanel) ((UIHelper) keyboardGroup).self).gameObject.AddComponent<KeybindSettingsPage>();
        }

        private static void MakeSettings_Maintenance(UITabstrip tabStrip, int tabIndex) {
            UIPanel currentPanel;
            UIHelper panelHelper;
            AddOptionTab(tabStrip, Translation.GetString("Maintenance"));
            tabStrip.selectedIndex = tabIndex;

            currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
            currentPanel.autoLayout = true;
            currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
            currentPanel.autoLayoutPadding.top = 5;
            currentPanel.autoLayoutPadding.left = 10;
            currentPanel.autoLayoutPadding.right = 10;

            panelHelper = new UIHelper(currentPanel);

            var maintenanceGroup = panelHelper.AddGroup(Translation.GetString("Maintenance"));

            resetStuckEntitiesBtn = maintenanceGroup.AddButton(Translation.GetString("Reset_stuck_cims_and_vehicles"), onClickResetStuckEntities) as UIButton;
            removeParkedVehiclesBtn = maintenanceGroup.AddButton(Translation.GetString("Remove_parked_vehicles"), onClickRemoveParkedVehicles) as UIButton;
#if DEBUG
            resetSpeedLimitsBtn = maintenanceGroup.AddButton(Translation.GetString("Reset_custom_speed_limits"), onClickResetSpeedLimits) as UIButton;
#endif
            reloadGlobalConfBtn = maintenanceGroup.AddButton(Translation.GetString("Reload_global_configuration"), onClickReloadGlobalConf) as UIButton;
            resetGlobalConfBtn = maintenanceGroup.AddButton(Translation.GetString("Reset_global_configuration"), onClickResetGlobalConf) as UIButton;

#if QUEUEDSTATS
            showPathFindStatsToggle = maintenanceGroup.AddCheckbox(Translation.GetString("Show_path-find_stats"), showPathFindStats, onShowPathFindStatsChanged) as UICheckBox;
#endif

            var featureGroup = panelHelper.AddGroup(Translation.GetString("Activated_features")) as UIHelper;
            enablePrioritySignsToggle = featureGroup.AddCheckbox(Translation.GetString("Priority_signs"), prioritySignsEnabled, onPrioritySignsEnabledChanged) as UICheckBox;
            enableTimedLightsToggle = featureGroup.AddCheckbox(Translation.GetString("Timed_traffic_lights"), timedLightsEnabled, onTimedLightsEnabledChanged) as UICheckBox;
            enableCustomSpeedLimitsToggle = featureGroup.AddCheckbox(Translation.GetString("Speed_limits"), customSpeedLimitsEnabled, onCustomSpeedLimitsEnabledChanged) as UICheckBox;
            enableVehicleRestrictionsToggle = featureGroup.AddCheckbox(Translation.GetString("Vehicle_restrictions"), vehicleRestrictionsEnabled, onVehicleRestrictionsEnabledChanged) as UICheckBox;
            enableParkingRestrictionsToggle = featureGroup.AddCheckbox(Translation.GetString("Parking_restrictions"), parkingRestrictionsEnabled, onParkingRestrictionsEnabledChanged) as UICheckBox;
            enableJunctionRestrictionsToggle = featureGroup.AddCheckbox(Translation.GetString("Junction_restrictions"), junctionRestrictionsEnabled, onJunctionRestrictionsEnabledChanged) as UICheckBox;
            turnOnRedEnabledToggle = featureGroup.AddCheckbox(Translation.GetString("Turn_on_red"), turnOnRedEnabled, onTurnOnRedEnabledChanged) as UICheckBox;
            enableLaneConnectorToggle = featureGroup.AddCheckbox(Translation.GetString("Lane_connector"), laneConnectorEnabled, onLaneConnectorEnabledChanged) as UICheckBox;

            Indent(turnOnRedEnabledToggle);
        }

        private static void MakeSettings_Overlays(UITabstrip tabStrip, int tabIndex) {
            UIPanel currentPanel;
            UIHelper panelHelper;
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
        }

        private static void MakeSettings_VehicleRestrictions(UITabstrip tabStrip, int tabIndex) {
            UIPanel currentPanel;
            UIHelper panelHelper;
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
            allowNearTurnOnRedToggle = atJunctionsGroup.AddCheckbox(Translation.GetString("Vehicles_may_turn_on_red"), allowNearTurnOnRed, onAllowNearTurnOnRedChanged) as UICheckBox;
            allowFarTurnOnRedToggle = atJunctionsGroup.AddCheckbox(Translation.GetString("Also_apply_to_left/right_turns_between_one-way_streets"), allowFarTurnOnRed, onAllowFarTurnOnRedChanged) as UICheckBox;
            allowLaneChangesWhileGoingStraightToggle = atJunctionsGroup.AddCheckbox(Translation.GetString("Vehicles_going_straight_may_change_lanes_at_junctions"), allowLaneChangesWhileGoingStraight, onAllowLaneChangesWhileGoingStraightChanged) as UICheckBox;
            trafficLightPriorityRulesToggle = atJunctionsGroup.AddCheckbox(Translation.GetString("Vehicles_follow_priority_rules_at_junctions_with_timed_traffic_lights"), trafficLightPriorityRules, onTrafficLightPriorityRulesChanged) as UICheckBox;

            Indent(allowFarTurnOnRedToggle);

            var onRoadsGroup = panelHelper.AddGroup(Translation.GetString("On_roads"));
            vehicleRestrictionsAggressionDropdown = onRoadsGroup.AddDropdown(Translation.GetString("Vehicle_restrictions_aggression") + ":", new string[] { Translation.GetString("Low"), Translation.GetString("Medium"), Translation.GetString("High"), Translation.GetString("Strict") }, (int)vehicleRestrictionsAggression, onVehicleRestrictionsAggressionChanged) as UIDropDown;
            banRegularTrafficOnBusLanesToggle = onRoadsGroup.AddCheckbox(Translation.GetString("Ban_private_cars_and_trucks_on_bus_lanes"), banRegularTrafficOnBusLanes, onBanRegularTrafficOnBusLanesChanged) as UICheckBox;
            highwayRulesToggle = onRoadsGroup.AddCheckbox(Translation.GetString("Enable_highway_specific_lane_merging/splitting_rules"), highwayRules, onHighwayRulesChanged) as UICheckBox;
            preferOuterLaneToggle = onRoadsGroup.AddCheckbox(Translation.GetString("Heavy_trucks_prefer_outer_lanes_on_highways"), preferOuterLane, onPreferOuterLaneChanged) as UICheckBox;

            if (SteamHelper.IsDLCOwned(SteamHelper.DLC.NaturalDisastersDLC)) {
                var inCaseOfEmergencyGroup = panelHelper.AddGroup(Translation.GetString("In_case_of_emergency"));
                evacBussesMayIgnoreRulesToggle = inCaseOfEmergencyGroup.AddCheckbox(Translation.GetString("Evacuation_busses_may_ignore_traffic_rules"), evacBussesMayIgnoreRules, onEvacBussesMayIgnoreRulesChanged) as UICheckBox;
            }
        }

        private static void MakeSettings_Gameplay(UITabstrip tabStrip, int tabIndex) {
            UIPanel currentPanel;
            UIHelper panelHelper;
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
            recklessDriversDropdown.width = 300;
            individualDrivingStyleToggle = vehBehaviorGroup.AddCheckbox(Translation.GetString("Individual_driving_styles"), individualDrivingStyle, onIndividualDrivingStyleChanged) as UICheckBox;
            if (SteamHelper.IsDLCOwned(SteamHelper.DLC.SnowFallDLC)) {
                strongerRoadConditionEffectsToggle = vehBehaviorGroup.AddCheckbox(Translation.GetString("Road_condition_has_a_bigger_impact_on_vehicle_speed"), strongerRoadConditionEffects, onStrongerRoadConditionEffectsChanged) as UICheckBox;
            }
            disableDespawningToggle = vehBehaviorGroup.AddCheckbox(Translation.GetString("Disable_despawning"), disableDespawning, onDisableDespawningChanged) as UICheckBox;
            var vehAiGroup = panelHelper.AddGroup(Translation.GetString("Advanced_Vehicle_AI"));
            advancedAIToggle = vehAiGroup.AddCheckbox(Translation.GetString("Enable_Advanced_Vehicle_AI"), advancedAI, onAdvancedAIChanged) as UICheckBox;
            altLaneSelectionRatioSlider = vehAiGroup.AddSlider(Translation.GetString("Dynamic_lane_section") + ":", 0, 100, 5, altLaneSelectionRatio, onAltLaneSelectionRatioChanged) as UISlider;
            altLaneSelectionRatioSlider.parent.Find<UILabel>("Label").width = 450;
            var parkAiGroup = panelHelper.AddGroup(Translation.GetString("Parking_AI"));
            prohibitPocketCarsToggle = parkAiGroup.AddCheckbox(Translation.GetString("Enable_more_realistic_parking"), parkingAI, onProhibitPocketCarsChanged) as UICheckBox;
            var ptGroup = panelHelper.AddGroup(Translation.GetString("Public_transport"));
            realisticPublicTransportToggle = ptGroup.AddCheckbox(Translation.GetString("Prevent_excessive_transfers_at_public_transport_stations"), realisticPublicTransport, onRealisticPublicTransportChanged) as UICheckBox;
        }
        private static void MakeSettings_General(UITabstrip tabStrip, int tabIndex) {
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
            lockButtonToggle = generalGroup.AddCheckbox(Translation.GetString("Lock_main_menu_button_position"), GlobalConfig.Instance.Main.MainMenuButtonPosLocked, onLockButtonChanged) as UICheckBox;
            lockMenuToggle = generalGroup.AddCheckbox(Translation.GetString("Lock_main_menu_position"), GlobalConfig.Instance.Main.MainMenuPosLocked, onLockMenuChanged) as UICheckBox;
            tinyMenuToggle = generalGroup.AddCheckbox(Translation.GetString("Compact_main_menu"), GlobalConfig.Instance.Main.TinyMainMenu, onTinyMenuChanged) as UICheckBox;
            guiTransparencySlider = generalGroup.AddSlider(Translation.GetString("Window_transparency") + ":", 0, 90, 5, GlobalConfig.Instance.Main.GuiTransparency, onGuiTransparencyChanged) as UISlider;
            guiTransparencySlider.parent.Find<UILabel>("Label").width = 500;
            overlayTransparencySlider = generalGroup.AddSlider(Translation.GetString("Overlay_transparency") + ":", 0, 90, 5, GlobalConfig.Instance.Main.OverlayTransparency, onOverlayTransparencyChanged) as UISlider;
            overlayTransparencySlider.parent.Find<UILabel>("Label").width = 500;
            enableTutorialToggle = generalGroup.AddCheckbox(Translation.GetString("Enable_tutorial_messages"), GlobalConfig.Instance.Main.EnableTutorial, onEnableTutorialsChanged) as UICheckBox;
            showCompatibilityCheckErrorToggle = generalGroup.AddCheckbox(Translation.GetString("Notify_me_if_there_is_an_unexpected_mod_conflict"), GlobalConfig.Instance.Main.ShowCompatibilityCheckErrorMessage, onShowCompatibilityCheckErrorChanged) as UICheckBox;
            scanForKnownIncompatibleModsToggle = generalGroup.AddCheckbox(Translation.GetString("Scan_for_known_incompatible_mods_on_startup"), GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup, onScanForKnownIncompatibleModsChanged) as UICheckBox;
            ignoreDisabledModsToggle = generalGroup.AddCheckbox(Translation.GetString("Ignore_disabled_mods"), GlobalConfig.Instance.Main.IgnoreDisabledMods, onIgnoreDisabledModsChanged) as UICheckBox;
            Indent(ignoreDisabledModsToggle);

            // General: Speed Limits
            setupSpeedLimitsPanel(panelHelper, generalGroup);

            // General: Simulation
            var simGroup = panelHelper.AddGroup(Translation.GetString("Simulation"));
            instantEffectsToggle = simGroup.AddCheckbox(Translation.GetString("Customizations_come_into_effect_instantaneously"), instantEffects, onInstantEffectsChanged) as UICheckBox;
        }

        private static void setupSpeedLimitsPanel(UIHelper panelHelper, UIHelperBase generalGroup) {
            displayMphToggle = generalGroup.AddCheckbox(
                                   Translation.GetString("Display_speed_limits_mph"),
                                   GlobalConfig.Instance.Main.DisplaySpeedLimitsMph,
                                   onDisplayMphChanged) as UICheckBox;
            var mphThemeOptions = new[] {
                                            Translation.GetString("theme_Square_US"),
                                            Translation.GetString("theme_Round_UK"),
                                            Translation.GetString("theme_Round_German"),
                                        };
            roadSignMphStyleInt = (int)GlobalConfig.Instance.Main.MphRoadSignStyle;
            roadSignsMphThemeDropdown = generalGroup.AddDropdown(
                                            Translation.GetString("Road_signs_theme_mph") + ":",
                                            mphThemeOptions, roadSignMphStyleInt,
                                            onRoadSignsMphThemeChanged) as UIDropDown;
            roadSignsMphThemeDropdown.width = 400;
        }

        private static void Indent<T>(T component) where T : UIComponent {
            UILabel label = component.Find<UILabel>("Label");
            if (label != null) {
                label.padding = new RectOffset(22, 0, 0, 0);
            }
            UISprite check = component.Find<UISprite>("Unchecked");
            if (check != null) {
                check.relativePosition += new Vector3(22.0f, 0);
            }
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

        private static bool IsGameLoaded(bool warn = true) {
            if (!SerializableDataExtension.StateLoading && !LoadingExtension.IsGameLoaded) {
                if (warn) {
                    UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Nope!", Translation.GetString("Settings_are_defined_for_each_savegame_separately") + ". https://www.viathinksoft.de/tmpe/#options", false);
                }
                return false;
            }
            return true;
        }

        private static void onGuiTransparencyChanged(float newVal) {
            if (!IsGameLoaded())
                return;

            setGuiTransparency((byte) Mathf.RoundToInt(newVal));
            guiTransparencySlider.tooltip = Translation.GetString("Window_transparency") + ": " + GlobalConfig.Instance.Main.GuiTransparency + " %";

            GlobalConfig.WriteConfig();

            Log._Debug($"GuiTransparency changed to {GlobalConfig.Instance.Main.GuiTransparency}");
        }

        private static void onOverlayTransparencyChanged(float newVal) {
            if (!IsGameLoaded())
                return;

            setOverlayTransparency((byte) Mathf.RoundToInt(newVal));
            overlayTransparencySlider.tooltip = Translation.GetString("Overlay_transparency") + ": " + GlobalConfig.Instance.Main.OverlayTransparency + " %";

            GlobalConfig.WriteConfig();

            Log._Debug($"OverlayTransparency changed to {GlobalConfig.Instance.Main.OverlayTransparency}");
        }

        private static void onAltLaneSelectionRatioChanged(float newVal) {
            if (!IsGameLoaded())
                return;

            setAltLaneSelectionRatio((byte) Mathf.RoundToInt(newVal));
            altLaneSelectionRatioSlider.tooltip = Translation.GetString("Percentage_of_vehicles_performing_dynamic_lane_section") + ": " + altLaneSelectionRatio + " %";

            Log._Debug($"altLaneSelectionRatio changed to {altLaneSelectionRatio}");
        }

        private static void onPrioritySignsOverlayChanged(bool newPrioritySignsOverlay) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"prioritySignsOverlay changed to {newPrioritySignsOverlay}");
            prioritySignsOverlay = newPrioritySignsOverlay;

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void onTimedLightsOverlayChanged(bool newTimedLightsOverlay) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"timedLightsOverlay changed to {newTimedLightsOverlay}");
            timedLightsOverlay = newTimedLightsOverlay;

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void onSpeedLimitsOverlayChanged(bool newSpeedLimitsOverlay) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"speedLimitsOverlay changed to {newSpeedLimitsOverlay}");
            speedLimitsOverlay = newSpeedLimitsOverlay;

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void onVehicleRestrictionsOverlayChanged(bool newVehicleRestrictionsOverlay) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"vehicleRestrictionsOverlay changed to {newVehicleRestrictionsOverlay}");
            vehicleRestrictionsOverlay = newVehicleRestrictionsOverlay;

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void onParkingRestrictionsOverlayChanged(bool newParkingRestrictionsOverlay) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"parkingRestrictionsOverlay changed to {newParkingRestrictionsOverlay}");
            parkingRestrictionsOverlay = newParkingRestrictionsOverlay;

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void onJunctionRestrictionsOverlayChanged(bool newValue) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"junctionRestrictionsOverlay changed to {newValue}");
            junctionRestrictionsOverlay = newValue;

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void onConnectedLanesOverlayChanged(bool newValue) {
            if (!IsGameLoaded())
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
            if (IsGameLoaded(false)) {
                LoadingExtension.BaseUI.MainMenuButton.SetPosLock(newValue);
            }
            GlobalConfig.Instance.Main.MainMenuButtonPosLocked = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void onLockMenuChanged(bool newValue) {
            Log._Debug($"Menu lock changed to {newValue}");
            if (IsGameLoaded(false)) {
                LoadingExtension.BaseUI.MainMenu.SetPosLock(newValue);
            }
            GlobalConfig.Instance.Main.MainMenuPosLocked = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void onTinyMenuChanged(bool newValue) {
            Log._Debug($"Menu tiny changed to {newValue}");
            GlobalConfig.Instance.Main.TinyMainMenu = newValue;
            GlobalConfig.Instance.NotifyObservers(GlobalConfig.Instance);
            GlobalConfig.WriteConfig();
        }

        private static void onEnableTutorialsChanged(bool newValue) {
            Log._Debug($"Enable tutorial messages changed to {newValue}");
            GlobalConfig.Instance.Main.EnableTutorial = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void onShowCompatibilityCheckErrorChanged(bool newValue) {
            Log._Debug($"Show mod compatibility error changed to {newValue}");
            GlobalConfig.Instance.Main.ShowCompatibilityCheckErrorMessage = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void onScanForKnownIncompatibleModsChanged(bool newValue) {
            Log._Debug($"Show incompatible mod checker warnings changed to {newValue}");
            GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup = newValue;
            if (newValue) {
                GlobalConfig.WriteConfig();
            } else {
                setIgnoreDisabledMods(false);
                onIgnoreDisabledModsChanged(false);
            }
        }

        private static void onIgnoreDisabledModsChanged(bool newValue) {
            Log._Debug($"Ignore disabled mods changed to {newValue}");
            GlobalConfig.Instance.Main.IgnoreDisabledMods = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void onDisplayMphChanged(bool newValue) {
            Log._Debug($"Display MPH changed to {newValue}");
            GlobalConfig.Instance.Main.DisplaySpeedLimitsMph = newValue;
            GlobalConfig.WriteConfig();
        }

        public static void setDisplayInMPH(bool value) {
            if (displayMphToggle != null) {
                displayMphToggle.isChecked = value;
            }
        }

        private static void onRoadSignsMphThemeChanged(int newRoadSignStyle) {
            if (!IsGameLoaded()) {
                return;
            }

            // The UI order is: US, UK, German
            var newStyle = MphSignStyle.RoundGerman;
            switch (newRoadSignStyle) {
                case 1:
                    newStyle = MphSignStyle.RoundUK;
                    break;
                case 0:
                    newStyle = MphSignStyle.SquareUS;
                    break;
            }

            Log._Debug($"Road Sign theme changed to {newStyle}");
            GlobalConfig.Instance.Main.MphRoadSignStyle = newStyle;
        }

        private static void onInstantEffectsChanged(bool newValue) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"Instant effects changed to {newValue}");
            instantEffects = newValue;
        }

        private static void onVehicleRestrictionsAggressionChanged(int newValue) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"vehicleRestrictionsAggression changed to {newValue}");
            setVehicleRestrictionsAggression((VehicleRestrictionsAggression)newValue);
        }

        /*private static void onLaneChangingRandomizationChanged(int newLaneChangingRandomization) {
                if (!checkGameLoaded())
                        return;

                Log._Debug($"Lane changing frequency changed to {newLaneChangingRandomization}");
                laneChangingRandomization = newLaneChangingRandomization;
        }*/

        private static void onRecklessDriversChanged(int newRecklessDrivers) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"Reckless driver amount changed to {newRecklessDrivers}");
            recklessDrivers = newRecklessDrivers;
        }

        private static void onRelaxedBussesChanged(bool newRelaxedBusses) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"Relaxed busses changed to {newRelaxedBusses}");
            relaxedBusses = newRelaxedBusses;
        }

        private static void onAllRelaxedChanged(bool newAllRelaxed) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"All relaxed changed to {newAllRelaxed}");
            allRelaxed = newAllRelaxed;
        }

        private static void onAdvancedAIChanged(bool newAdvancedAI) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"advancedAI changed to {newAdvancedAI}");
            setAdvancedAI(newAdvancedAI);
        }

        private static void onHighwayRulesChanged(bool newHighwayRules) {
            if (!IsGameLoaded())
                return;

            bool changed = newHighwayRules != highwayRules;
            if (!changed) {
                return;
            }

            Log._Debug($"Highway rules changed to {newHighwayRules}");
            highwayRules = newHighwayRules;
            Flags.clearHighwayLaneArrows();
            Flags.applyAllFlags();
            RoutingManager.Instance.RequestFullRecalculation();
        }

        private static void onPreferOuterLaneChanged(bool val) {
            if (!IsGameLoaded())
                return;

            preferOuterLane = val;
        }

        private static void onPrioritySignsEnabledChanged(bool val) {
            if (!IsGameLoaded())
                return;

            MenuRebuildRequired = true;
            prioritySignsEnabled = val;
            if (!val) {
                setPrioritySignsOverlay(false);
                setTrafficLightPriorityRules(false);
            }
        }

        private static void onTimedLightsEnabledChanged(bool val) {
            if (!IsGameLoaded())
                return;

            MenuRebuildRequired = true;
            timedLightsEnabled = val;
            if (!val) {
                setTimedLightsOverlay(false);
                setTrafficLightPriorityRules(false);
            }
        }

        private static void onCustomSpeedLimitsEnabledChanged(bool val) {
            if (!IsGameLoaded())
                return;

            MenuRebuildRequired = true;
            customSpeedLimitsEnabled = val;
            if (!val)
                setSpeedLimitsOverlay(false);
        }

        private static void onVehicleRestrictionsEnabledChanged(bool val) {
            if (!IsGameLoaded())
                return;

            MenuRebuildRequired = true;
            vehicleRestrictionsEnabled = val;
            if (!val)
                setVehicleRestrictionsOverlay(false);
        }

        private static void onParkingRestrictionsEnabledChanged(bool val) {
            if (!IsGameLoaded())
                return;

            MenuRebuildRequired = true;
            parkingRestrictionsEnabled = val;
            if (!val)
                setParkingRestrictionsOverlay(false);
        }

        private static void onJunctionRestrictionsEnabledChanged(bool val) {
            if (!IsGameLoaded())
                return;

            MenuRebuildRequired = true;
            junctionRestrictionsEnabled = val;
            if (!val) {
                setAllowUTurns(false);
                setAllowEnterBlockedJunctions(false);
                setAllowLaneChangesWhileGoingStraight(false);
                setTurnOnRedEnabled(false);
                setJunctionRestrictionsOverlay(false);
            }
        }

        private static void onTurnOnRedEnabledChanged(bool val) {
            if (!IsGameLoaded())
                return;

            setTurnOnRedEnabled(val);
        }

        private static void onLaneConnectorEnabledChanged(bool val) {
            if (!IsGameLoaded())
                return;

            bool changed = val != laneConnectorEnabled;
            if (!changed) {
                return;
            }

            MenuRebuildRequired = true;
            laneConnectorEnabled = val;
            RoutingManager.Instance.RequestFullRecalculation();
            if (!val)
                setConnectedLanesOverlay(false);
        }

        private static void onEvacBussesMayIgnoreRulesChanged(bool value) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"evacBussesMayIgnoreRules changed to {value}");
            evacBussesMayIgnoreRules = value;
        }

        private static void onAllowEnterBlockedJunctionsChanged(bool newValue) {
            if (!IsGameLoaded())
                return;
            if (newValue && !junctionRestrictionsEnabled) {
                setAllowEnterBlockedJunctions(false);
                return;
            }

            Log._Debug($"allowEnterBlockedJunctions changed to {newValue}");
            setAllowEnterBlockedJunctions(newValue);
        }

        private static void onAllowUTurnsChanged(bool newValue) {
            if (!IsGameLoaded())
                return;
            if (newValue && !junctionRestrictionsEnabled) {
                setAllowUTurns(false);
                return;
            }

            Log._Debug($"allowUTurns changed to {newValue}");
            setAllowUTurns(newValue);
        }

        private static void onAllowNearTurnOnRedChanged(bool newValue) {
            if (!IsGameLoaded())
                return;
            if (newValue && !turnOnRedEnabled) {
                setAllowNearTurnOnRed(false);
                setAllowFarTurnOnRed(false);
                return;
            }

            Log._Debug($"allowNearTurnOnRed changed to {newValue}");
            setAllowNearTurnOnRed(newValue);

            if (!newValue) {
                setAllowFarTurnOnRed(false);
            }
        }

        private static void onAllowFarTurnOnRedChanged(bool newValue) {
            if (!IsGameLoaded())
                return;
            if (newValue && (!turnOnRedEnabled || !allowNearTurnOnRed)) {
                setAllowFarTurnOnRed(false);
                return;
            }

            Log._Debug($"allowFarTurnOnRed changed to {newValue}");
            setAllowFarTurnOnRed(newValue);
        }

        private static void onAllowLaneChangesWhileGoingStraightChanged(bool newValue) {
            if (!IsGameLoaded())
                return;
            if (newValue && !junctionRestrictionsEnabled) {
                setAllowLaneChangesWhileGoingStraight(false);
                return;
            }

            Log._Debug($"allowLaneChangesWhileGoingStraight changed to {newValue}");
            setAllowLaneChangesWhileGoingStraight(newValue);
        }

        private static void onTrafficLightPriorityRulesChanged(bool newValue) {
            if (!IsGameLoaded())
                return;
            if (newValue && !prioritySignsEnabled) {
                setTrafficLightPriorityRules(false);
                return;
            }

            Log._Debug($"trafficLightPriorityRules changed to {newValue}");
            trafficLightPriorityRules = newValue;
            if (newValue) {
                setPrioritySignsEnabled(true);
                setTimedLightsEnabled(true);
            }
        }

        private static void onBanRegularTrafficOnBusLanesChanged(bool newValue) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"banRegularTrafficOnBusLanes changed to {newValue}");
            banRegularTrafficOnBusLanes = newValue;
            VehicleRestrictionsManager.Instance.ClearCache();
            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void onStrongerRoadConditionEffectsChanged(bool newStrongerRoadConditionEffects) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"strongerRoadConditionEffects changed to {newStrongerRoadConditionEffects}");
            strongerRoadConditionEffects = newStrongerRoadConditionEffects;
        }

        private static void onProhibitPocketCarsChanged(bool newValue) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"prohibitPocketCars changed to {newValue}");

            parkingAI = newValue;
            if (parkingAI) {
                AdvancedParkingManager.Instance.OnEnableFeature();
            } else {
                AdvancedParkingManager.Instance.OnDisableFeature();
            }
        }

		private static void onRealisticPublicTransportChanged(bool newValue) {
			if (!IsGameLoaded())
				return;

            Log._Debug($"realisticPublicTransport changed to {newValue}");
            realisticPublicTransport = newValue;
        }

        private static void onIndividualDrivingStyleChanged(bool value) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"individualDrivingStyle changed to {value}");
            setIndividualDrivingStyle(value);
        }

        private static void onDisableDespawningChanged(bool value) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"disableDespawning changed to {value}");
            disableDespawning = value;
        }

        private static void onNodesOverlayChanged(bool newNodesOverlay) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"Nodes overlay changed to {newNodesOverlay}");
            nodesOverlay = newNodesOverlay;
        }

        private static void onShowLanesChanged(bool newShowLanes) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"Show lanes changed to {newShowLanes}");
            showLanes = newShowLanes;
        }

        private static void onVehicleOverlayChanged(bool newVal) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"Vehicle overlay changed to {newVal}");
            vehicleOverlay = newVal;
        }

        private static void onCitizenOverlayChanged(bool newVal) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"Citizen overlay changed to {newVal}");
            citizenOverlay = newVal;
        }

        private static void onBuildingOverlayChanged(bool newVal) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"Building overlay changed to {newVal}");
            buildingOverlay = newVal;
        }

#if QUEUEDSTATS
        private static void onShowPathFindStatsChanged(bool newVal) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"Show path-find stats changed to {newVal}");
            showPathFindStats = newVal;
        }
#endif

        private static void onFloatValueChanged(string varName, string newValueStr, ref float var) {
            if (!IsGameLoaded())
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
            if (!IsGameLoaded())
                return;

            var = newVal;
            Log._Debug($"{varName} changed to {newVal}");
        }

        private static void onClickResetStuckEntities() {
            if (!IsGameLoaded())
                return;

            Constants.ServiceFactory.SimulationService.AddAction(() => {
                UtilityManager.Instance.ResetStuckEntities();
            });
        }

        private static void onClickRemoveParkedVehicles() {
            if (!IsGameLoaded())
                return;

            Constants.ServiceFactory.SimulationService.AddAction(() => {
                UtilityManager.Instance.RemoveParkedVehicles();
            });
        }

        private static void onClickResetSpeedLimits() {
            if (!IsGameLoaded())
                return;

            Flags.resetSpeedLimits();
        }

        private static void onClickReloadGlobalConf() {
            GlobalConfig.Reload();
        }

        private static void onClickResetGlobalConf() {
            GlobalConfig.Reset(null, true);
        }

        public static void setVehicleRestrictionsAggression(VehicleRestrictionsAggression val) {
            bool changed = vehicleRestrictionsAggression != val;
            vehicleRestrictionsAggression = val;
            if (changed && vehicleRestrictionsAggressionDropdown != null) {
                vehicleRestrictionsAggressionDropdown.selectedIndex = (int)val;
            }
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
            bool changed = newAdvancedAI != advancedAI;
            advancedAI = newAdvancedAI;

            if (changed && advancedAIToggle != null) {
                advancedAIToggle.isChecked = newAdvancedAI;
            }

            if (changed && !newAdvancedAI) {
                setAltLaneSelectionRatio(0);
            }
        }

        public static void setGuiTransparency(byte val) {
            bool changed = val != GlobalConfig.Instance.Main.GuiTransparency;
            GlobalConfig.Instance.Main.GuiTransparency = val;

            if (changed && guiTransparencySlider != null) {
                guiTransparencySlider.value = val;
            }
        }

        public static void setOverlayTransparency(byte val) {
            bool changed = val != GlobalConfig.Instance.Main.OverlayTransparency;
            GlobalConfig.Instance.Main.OverlayTransparency = val;

            if (changed && overlayTransparencySlider != null) {
                overlayTransparencySlider.value = val;
            }
        }

        public static void setAltLaneSelectionRatio(byte val) {
            bool changed = val != altLaneSelectionRatio;
            altLaneSelectionRatio = val;

            if (changed && altLaneSelectionRatioSlider != null) {
                altLaneSelectionRatioSlider.value = val;
            }

            if (changed && altLaneSelectionRatio > 0) {
                setAdvancedAI(true);
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
            if (!SteamHelper.IsDLCOwned(SteamHelper.DLC.SnowFallDLC)) {
                newStrongerRoadConditionEffects = false;
            }

            strongerRoadConditionEffects = newStrongerRoadConditionEffects;
            if (strongerRoadConditionEffectsToggle != null)
                strongerRoadConditionEffectsToggle.isChecked = newStrongerRoadConditionEffects;
        }

		public static void setProhibitPocketCars(bool newValue) {
			bool valueChanged = newValue != parkingAI;
			parkingAI = newValue;
			if (prohibitPocketCarsToggle != null)
				prohibitPocketCarsToggle.isChecked = newValue;
		}

        public static void setRealisticPublicTransport(bool newValue) {
            bool valueChanged = newValue != realisticPublicTransport;
            realisticPublicTransport = newValue;
            if (realisticPublicTransportToggle != null)
                realisticPublicTransportToggle.isChecked = newValue;
        }

        public static void setIndividualDrivingStyle(bool newValue) {
            individualDrivingStyle = newValue;

            if (individualDrivingStyleToggle != null) {
                individualDrivingStyleToggle.isChecked = newValue;
            }
        }

        public static void setDisableDespawning(bool value) {
            disableDespawning = value;

            if (disableDespawningToggle != null)
                disableDespawningToggle.isChecked = value;
        }

        public static void setAllowUTurns(bool value) {
            allowUTurns = value;
            if (allowUTurnsToggle != null)
                allowUTurnsToggle.isChecked = value;
            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void setAllowNearTurnOnRed(bool newValue) {
            allowNearTurnOnRed = newValue;
            if (allowNearTurnOnRedToggle != null)
                allowNearTurnOnRedToggle.isChecked = newValue;
            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void setAllowFarTurnOnRed(bool newValue) {
            allowFarTurnOnRed = newValue;
            if (allowFarTurnOnRedToggle != null)
                allowFarTurnOnRedToggle.isChecked = newValue;
            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void setAllowLaneChangesWhileGoingStraight(bool value) {
            allowLaneChangesWhileGoingStraight = value;
            if (allowLaneChangesWhileGoingStraightToggle != null)
                allowLaneChangesWhileGoingStraightToggle.isChecked = value;
            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void setAllowEnterBlockedJunctions(bool value) {
            allowEnterBlockedJunctions = value;
            if (allowEnterBlockedJunctionsToggle != null)
                allowEnterBlockedJunctionsToggle.isChecked = value;
            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void setTrafficLightPriorityRules(bool value) {
            trafficLightPriorityRules = value;
            if (trafficLightPriorityRulesToggle != null)
                trafficLightPriorityRulesToggle.isChecked = value;
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

        public static void setTurnOnRedEnabled(bool newValue) {
            turnOnRedEnabled = newValue;
            if (turnOnRedEnabledToggle != null)
                turnOnRedEnabledToggle.isChecked = newValue;
            if (!newValue) {
                setAllowNearTurnOnRed(false);
                setAllowFarTurnOnRed(false);
            }
        }

        public static void setLaneConnectorEnabled(bool newValue) {
            MenuRebuildRequired = true;
            laneConnectorEnabled = newValue;
            if (enableLaneConnectorToggle != null)
                enableLaneConnectorToggle.isChecked = newValue;
            if (!newValue)
                setConnectedLanesOverlay(false);
        }

#if QUEUEDSTATS
        public static void setShowPathFindStats(bool value) {
            showPathFindStats = value;
            if (showPathFindStatsToggle != null)
                showPathFindStatsToggle.isChecked = value;
        }
#endif

        public static void setScanForKnownIncompatibleMods(bool value) {
            scanForKnownIncompatibleModsEnabled = value;
            if (scanForKnownIncompatibleModsToggle != null) {
                scanForKnownIncompatibleModsToggle.isChecked = value;
            }
            if (!value) {
                setIgnoreDisabledMods(false);
            }
        }

        public static void setIgnoreDisabledMods(bool value) {
            ignoreDisabledModsEnabled = value;
            if (ignoreDisabledModsToggle != null) {
                ignoreDisabledModsToggle.isChecked = value;
            }
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

        /// <summary>
        /// Determines whether Dynamic Lane Selection (DLS) is enabled.
        /// </summary>
        /// <returns></returns>
        public static bool IsDynamicLaneSelectionActive() {
            return advancedAI && altLaneSelectionRatio > 0;
        }
    }
}
