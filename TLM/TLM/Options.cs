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

namespace TrafficManager {

	public class Options : MonoBehaviour {
		private static UIDropDown simAccuracyDropdown = null;
		private static UIDropDown laneChangingRandomizationDropdown = null;
		private static UIDropDown recklessDriversDropdown = null;
		private static UICheckBox relaxedBussesToggle = null;
		private static UICheckBox allRelaxedToggle = null;
		private static UICheckBox nodesOverlayToggle = null;
		private static UICheckBox mayEnterBlockedJunctionsToggle = null;
		private static UICheckBox advancedAIToggle = null;
		private static UICheckBox highwayRulesToggle = null;
		private static UICheckBox showLanesToggle = null;
#if DEBUG
		private static UICheckBox disableSomething3Toggle = null;
		private static UICheckBox disableSomething2Toggle = null;
		private static UICheckBox disableSomething1Toggle = null;
		private static UITextField pathCostMultiplicatorField = null;
		private static UITextField someValueField = null;
#endif

		public static int simAccuracy = 1;
		public static int laneChangingRandomization = 2;
		public static int recklessDrivers = 3;
		public static bool relaxedBusses = false;
		public static bool allRelaxed = false;
		public static bool nodesOverlay = false;
		public static bool mayEnterBlockedJunctions = false;
		public static bool advancedAI = false;
		public static bool highwayRules = false;
		public static bool showLanes = false;
		public static float pathCostMultiplicator = 2f; // debug value
		public static bool disableSomething1 = false; // debug switch
		public static bool disableSomething2 = false; // debug switch
		public static bool disableSomething3 = false; // debug switch
		public static float someValue = 3f; // debug value

		public static void makeSettings(UIHelperBase helper) {
			UIHelperBase group = helper.AddGroup(Translation.GetString("TMPE_Title"));
			simAccuracyDropdown = group.AddDropdown(Translation.GetString("Simulation_accuracy") + ":", new string[] { Translation.GetString("Very_high"), Translation.GetString("High"), Translation.GetString("Medium"), Translation.GetString("Low"), Translation.GetString("Very_Low") }, simAccuracy, onSimAccuracyChanged) as UIDropDown;
			recklessDriversDropdown = group.AddDropdown(Translation.GetString("Reckless_driving") + ":", new string[] { Translation.GetString("Path_Of_Evil_(10_%)"), Translation.GetString("Rush_Hour_(5_%)"), Translation.GetString("Minor_Complaints_(2_%)"), Translation.GetString("Holy_City_(0_%)") }, recklessDrivers, onRecklessDriversChanged) as UIDropDown;
			relaxedBussesToggle = group.AddCheckbox(Translation.GetString("Busses_may_ignore_lane_arrows"), relaxedBusses, onRelaxedBussesChanged) as UICheckBox;
#if DEBUG
			allRelaxedToggle = group.AddCheckbox(Translation.GetString("All_vehicles_may_ignore_lane_arrows"), allRelaxed, onAllRelaxedChanged) as UICheckBox;
#endif
			mayEnterBlockedJunctionsToggle = group.AddCheckbox(Translation.GetString("Vehicles_may_enter_blocked_junctions"), mayEnterBlockedJunctions, onMayEnterBlockedJunctionsChanged) as UICheckBox;
			UIHelperBase groupAI = helper.AddGroup("Advanced Vehicle AI");
			advancedAIToggle = groupAI.AddCheckbox(Translation.GetString("Enable_Advanced_Vehicle_AI"), advancedAI, onAdvancedAIChanged) as UICheckBox;
			highwayRulesToggle = groupAI.AddCheckbox(Translation.GetString("Enable_highway_specific_lane_merging/splitting_rules")+" (BETA feature)", highwayRules, onHighwayRulesChanged) as UICheckBox;
			laneChangingRandomizationDropdown = groupAI.AddDropdown(Translation.GetString("Drivers_want_to_change_lanes_(only_applied_if_Advanced_AI_is_enabled):"), new string[] { Translation.GetString("Very_often_(50_%)"), Translation.GetString("Often_(25_%)"), Translation.GetString("Sometimes_(10_%)"), Translation.GetString("Rarely_(5_%)"), Translation.GetString("Very_rarely_(2.5_%)"), Translation.GetString("Only_if_necessary") }, laneChangingRandomization, onLaneChangingRandomizationChanged) as UIDropDown;
			UIHelperBase group2 = helper.AddGroup(Translation.GetString("Maintenance"));
			group2.AddButton(Translation.GetString("Forget_toggled_traffic_lights"), onClickForgetToggledLights);
			nodesOverlayToggle = group2.AddCheckbox(Translation.GetString("Show_nodes_and_segments"), nodesOverlay, onNodesOverlayChanged) as UICheckBox;
            showLanesToggle = group2.AddCheckbox(Translation.GetString("Show_lanes"), showLanes, onShowLanesChanged) as UICheckBox;
#if DEBUG
			disableSomething1Toggle = group2.AddCheckbox("Enable path-finding debugging", disableSomething1, onDisableSomething1Changed) as UICheckBox;
			disableSomething2Toggle = group2.AddCheckbox("Disable something #2", disableSomething2, onDisableSomething2Changed) as UICheckBox;
			disableSomething3Toggle = group2.AddCheckbox("Disable something #3", disableSomething3, onDisableSomething3Changed) as UICheckBox;
			pathCostMultiplicatorField = group2.AddTextfield("Pathcost multiplicator", String.Format("{0:0.##}", pathCostMultiplicator), onPathCostMultiplicatorChanged) as UITextField;
			someValueField = group2.AddTextfield("Some value", String.Format("{0:0.##}", someValue), onSomeValueChanged) as UITextField;
#endif
		}

		private static void onDisableSomething1Changed(bool newDisableSomething) {
			Log._Debug($"disableSomething1 changed to {newDisableSomething}");
			disableSomething1 = newDisableSomething;
		}

		private static void onDisableSomething2Changed(bool newDisableSomething) {
			Log._Debug($"disableSomething2 changed to {newDisableSomething}");
			disableSomething2 = newDisableSomething;
		}

		private static void onDisableSomething3Changed(bool newDisableSomething) {
			Log._Debug($"disableSomething3 changed to {newDisableSomething}");
			disableSomething3 = newDisableSomething;
		}

		private static void onSimAccuracyChanged(int newAccuracy) {
			Log._Debug($"Simulation accuracy changed to {newAccuracy}");
			simAccuracy = newAccuracy;
		}

		private static void onLaneChangingRandomizationChanged(int newLaneChangingRandomization) {
			Log._Debug($"Lane changing frequency changed to {newLaneChangingRandomization}");
			laneChangingRandomization = newLaneChangingRandomization;
		}

		private static void onRecklessDriversChanged(int newRecklessDrivers) {
			Log._Debug($"Reckless driver amount changed to {newRecklessDrivers}");
			recklessDrivers = newRecklessDrivers;
		}

		private static void onRelaxedBussesChanged(bool newRelaxedBusses) {
			Log._Debug($"Relaxed busses changed to {newRelaxedBusses}");
			relaxedBusses = newRelaxedBusses;
		}

		private static void onAllRelaxedChanged(bool newAllRelaxed) {
			Log._Debug($"All relaxed changed to {newAllRelaxed}");
			allRelaxed = newAllRelaxed;
		}

		private static void onHighwayRulesChanged(bool newHighwayRules) {
			Log._Debug($"Highway rules changed to {newHighwayRules}");
			highwayRules = newHighwayRules;
			Flags.clearHighwayLaneArrows();
			Flags.applyAllFlags();
		}

		private static void onAdvancedAIChanged(bool newAdvancedAI) {
			if (LoadingExtension.IsPathManagerCompatible) {
				Log._Debug($"advancedAI busses changed to {newAdvancedAI}");
				advancedAI = newAdvancedAI;
			} else if (newAdvancedAI) {
				setAdvancedAI(false);
				UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Advanced AI cannot be activated", "The Advanced Vehicle AI cannot be activated because you are already using another mod that modifies vehicle behavior (e.g. Improved AI or Traffic++).", false);
			}
		}

		private static void onMayEnterBlockedJunctionsChanged(bool newMayEnterBlockedJunctions) {
			Log._Debug($"MayEnterBlockedJunctions changed to {newMayEnterBlockedJunctions}");
			mayEnterBlockedJunctions = newMayEnterBlockedJunctions;
		}

		private static void onNodesOverlayChanged(bool newNodesOverlay) {
			Log._Debug($"Nodes overlay changed to {newNodesOverlay}");
			nodesOverlay = newNodesOverlay;
		}

		private static void onShowLanesChanged(bool newShowLanes) {
			Log._Debug($"Show lanes changed to {newShowLanes}");
			showLanes = newShowLanes;
		}

		private static void onPathCostMultiplicatorChanged(string newPathCostMultiplicatorStr) {
			try {
				float newPathCostMultiplicator = Single.Parse(newPathCostMultiplicatorStr);
				pathCostMultiplicator = newPathCostMultiplicator;
			} catch (Exception e) {
				Log.Warning($"An invalid value was inserted: '{newPathCostMultiplicatorStr}'. Error: {e.ToString()}");
                //UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Invalid value", "An invalid value was inserted.", false);
			}
		}

		private static void onSomeValueChanged(string newSomeValueStr) {
			try {
				float newSomeValue = Single.Parse(newSomeValueStr);
				someValue = newSomeValue;
			} catch (Exception e) {
				Log.Warning($"An invalid value was inserted: '{newSomeValueStr}'. Error: {e.ToString()}");
				//UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Invalid value", "An invalid value was inserted.", false);
			}
		}

		private static void onClickForgetToggledLights() {
			Flags.resetTrafficLights(false);
		}

		public static void setSimAccuracy(int newAccuracy) {
			simAccuracy = newAccuracy;
			if (simAccuracyDropdown != null)
				simAccuracyDropdown.selectedIndex = newAccuracy;
		}

		public static void setLaneChangingRandomization(int newLaneChangingRandomization) {
			laneChangingRandomization = newLaneChangingRandomization;
			if (laneChangingRandomizationDropdown != null)
				laneChangingRandomizationDropdown.selectedIndex = newLaneChangingRandomization;
		}

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
			highwayRules = newHighwayRules;
			if (highwayRulesToggle != null)
				highwayRulesToggle.isChecked = newHighwayRules;
		}

		public static void setShowLanes(bool newShowLanes) {
			showLanes = newShowLanes;
			if (showLanesToggle != null)
				showLanesToggle.isChecked = newShowLanes;
		}

		public static void setAdvancedAI(bool newAdvancedAI) {
			if (!LoadingExtension.IsPathManagerCompatible) {
				advancedAI = false;
			} else {
				advancedAI = newAdvancedAI;
			}
			if (advancedAIToggle != null)
				advancedAIToggle.isChecked = newAdvancedAI;
		}

		public static void setMayEnterBlockedJunctions(bool newMayEnterBlockedJunctions) {
			mayEnterBlockedJunctions = newMayEnterBlockedJunctions;
			if (mayEnterBlockedJunctionsToggle != null)
				mayEnterBlockedJunctionsToggle.isChecked = newMayEnterBlockedJunctions;
		}

		public static void setNodesOverlay(bool newNodesOverlay) {
			nodesOverlay = newNodesOverlay;
			if (nodesOverlayToggle != null)
				nodesOverlayToggle.isChecked = newNodesOverlay;
		}

		internal static int getLaneChangingRandomizationTargetValue() {
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
		}

		internal static float getLaneChangingProbability() {
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
		}

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

		internal static float getPathCostMultiplicator() {
			return pathCostMultiplicator;
		}
	}
}
