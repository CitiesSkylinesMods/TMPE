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
		private static UITextField pathCostMultiplicatorField = null;
		private static UISlider carCityTrafficSensitivitySlider = null;
		private static UISlider truckCityTrafficSensitivitySlider = null;
		private static UISlider carHighwayTrafficSensitivitySlider = null;
		private static UISlider truckHighwayTrafficSensitivitySlider = null;

		public static int simAccuracy = 1;
		public static int laneChangingRandomization = 2;
		public static int recklessDrivers = 3;
		public static bool relaxedBusses = false;
		public static bool allRelaxed = false;
		public static bool nodesOverlay = false;
		public static bool mayEnterBlockedJunctions = false;
		public static bool advancedAI = false;
		public static bool highwayRules = true;
		public static bool showLanes = false;
		public static float pathCostMultiplicator = 20f;
		public static float carCityTrafficSensitivity = 0.35f;
		public static float carHighwayTrafficSensitivity = 0.1f;
		public static float truckCityTrafficSensitivity = 0.25f;
		public static float truckHighwayTrafficSensitivity = 0.05f;

		public static void makeSettings(UIHelperBase helper) {
			UIHelperBase group = helper.AddGroup("Traffic Manager: President Edition (Settings are defined for each savegame separately)");
			simAccuracyDropdown = group.AddDropdown("Simulation accuracy (higher accuracy reduces performance):", new string[] { "Very high", "High", "Medium", "Low", "Very Low" }, simAccuracy, onSimAccuracyChanged) as UIDropDown;
			recklessDriversDropdown = group.AddDropdown("Reckless driving (BETA feature):", new string[] { "Path Of Evil (10 %)", "Rush Hour (5 %)", "Minor Complaints (2 %)", "Holy City (0 %)" }, recklessDrivers, onRecklessDriversChanged) as UIDropDown;
			relaxedBussesToggle = group.AddCheckbox("Busses may ignore lane arrows", relaxedBusses, onRelaxedBussesChanged) as UICheckBox;
#if DEBUG
			allRelaxedToggle = group.AddCheckbox("All vehicles may ignore lane arrows", allRelaxed, onAllRelaxedChanged) as UICheckBox;
#endif
			mayEnterBlockedJunctionsToggle = group.AddCheckbox("Vehicles may enter blocked junctions", mayEnterBlockedJunctions, onMayEnterBlockedJunctionsChanged) as UICheckBox;
			UIHelperBase groupAI = helper.AddGroup("Advanced Vehicle AI");
			advancedAIToggle = groupAI.AddCheckbox("Enable Advanced Vehicle AI", advancedAI, onAdvancedAIChanged) as UICheckBox;
			highwayRulesToggle = groupAI.AddCheckbox("Enable highway specific lane merging/splitting rules", highwayRules, onHighwayRulesChanged) as UICheckBox;
			laneChangingRandomizationDropdown = groupAI.AddDropdown("Drivers want to change lanes (only applied if Advanced AI is enabled):", new string[] { "Very often (50 %)", "Often (25 %)", "Sometimes (10 %)", "Rarely (5 %)", "Very rarely (2.5 %)", "Only if necessary" }, laneChangingRandomization, onLaneChangingRandomizationChanged) as UIDropDown;
//#if DEBUG
			UIHelperBase senseAI = helper.AddGroup("Avoidance of lanes with high traffic density (low - high)");
			carCityTrafficSensitivitySlider = senseAI.AddSlider("Cars, city:", 0f, 1f, 0.05f, carCityTrafficSensitivity, onCarCityTrafficSensitivityChange) as UISlider;
			carHighwayTrafficSensitivitySlider = senseAI.AddSlider("Cars, highway:", 0f, 1f, 0.05f, carHighwayTrafficSensitivity, onCarHighwayTrafficSensitivityChange) as UISlider;
			truckCityTrafficSensitivitySlider = senseAI.AddSlider("Trucks, city:", 0f, 1f, 0.05f, truckCityTrafficSensitivity, onTruckCityTrafficSensitivityChange) as UISlider;
			truckHighwayTrafficSensitivitySlider = senseAI.AddSlider("Trucks, highway:", 0f, 1f, 0.05f, truckHighwayTrafficSensitivity, onTruckHighwayTrafficSensitivityChange) as UISlider;
//#endif
			UIHelperBase group2 = helper.AddGroup("Maintenance");
			group2.AddButton("Forget toggled traffic lights", onClickForgetToggledLights);
			nodesOverlayToggle = group2.AddCheckbox("Show nodes and segments", nodesOverlay, onNodesOverlayChanged) as UICheckBox;
			showLanesToggle = group2.AddCheckbox("Show lanes", showLanes, onShowLanesChanged) as UICheckBox;
#if DEBUG
			pathCostMultiplicatorField = group2.AddTextfield("Pathcost multiplicator", String.Format("{0:0.##}", pathCostMultiplicator), onPathCostMultiplicatorChanged) as UITextField;
#endif
		}

		private static void onTruckHighwayTrafficSensitivityChange(float val) {
			Log.Message($"Truck highway sensitivity changed to {val}");
			truckHighwayTrafficSensitivity = val;
		}

		private static void onTruckCityTrafficSensitivityChange(float val) {
			Log.Message($"Truck city sensitivity changed to {val}");
			truckCityTrafficSensitivity = val;
		}

		private static void onCarHighwayTrafficSensitivityChange(float val) {
			Log.Message($"Car highway sensitivity changed to {val}");
			carHighwayTrafficSensitivity = val;
		}

		private static void onCarCityTrafficSensitivityChange(float val) {
			Log.Message($"Car city sensitivity changed to {val}");
			carCityTrafficSensitivity = val;
		}

		private static void onSimAccuracyChanged(int newAccuracy) {
			Log.Message($"Simulation accuracy changed to {newAccuracy}");
			simAccuracy = newAccuracy;
		}

		private static void onLaneChangingRandomizationChanged(int newLaneChangingRandomization) {
			Log.Message($"Lane changing frequency changed to {newLaneChangingRandomization}");
			laneChangingRandomization = newLaneChangingRandomization;
		}

		private static void onRecklessDriversChanged(int newRecklessDrivers) {
			Log.Message($"Reckless driver amount changed to {newRecklessDrivers}");
			recklessDrivers = newRecklessDrivers;
		}

		private static void onRelaxedBussesChanged(bool newRelaxedBusses) {
			Log.Message($"Relaxed busses changed to {newRelaxedBusses}");
			relaxedBusses = newRelaxedBusses;
		}

		private static void onAllRelaxedChanged(bool newAllRelaxed) {
			Log.Message($"All relaxed changed to {newAllRelaxed}");
			allRelaxed = newAllRelaxed;
		}

		private static void onHighwayRulesChanged(bool newHighwayRules) {
			Log.Message($"Highway rules changed to {newHighwayRules}");
			highwayRules = newHighwayRules;
		}

		private static void onAdvancedAIChanged(bool newAdvancedAI) {
			if (LoadingExtension.IsPathManagerCompatible) {
				Log.Message($"advancedAI busses changed to {newAdvancedAI}");
				advancedAI = newAdvancedAI;
			} else if (newAdvancedAI) {
				setAdvancedAI(false);
				UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Advanced AI cannot be activated", "The Advanced Vehicle AI cannot be activated because you are already using another mod that modifies vehicle behavior (e.g. Improved AI or Traffic++).", false);
			}
		}

		private static void onMayEnterBlockedJunctionsChanged(bool newMayEnterBlockedJunctions) {
			Log.Message($"MayEnterBlockedJunctions changed to {newMayEnterBlockedJunctions}");
			mayEnterBlockedJunctions = newMayEnterBlockedJunctions;
		}

		private static void onNodesOverlayChanged(bool newNodesOverlay) {
			Log.Message($"Nodes overlay changed to {newNodesOverlay}");
			nodesOverlay = newNodesOverlay;
		}

		private static void onShowLanesChanged(bool newShowLanes) {
			Log.Message($"Show lanes changed to {newShowLanes}");
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

		private static void onClickForgetToggledLights() {
			Flags.resetTrafficLights(false);
		}

		public static void setTruckHighwayTrafficSensitivity(float val) {
			truckHighwayTrafficSensitivity = val;
			if (truckHighwayTrafficSensitivitySlider != null)
				truckHighwayTrafficSensitivitySlider.value = val;
		}

		public static void setTruckCityTrafficSensitivity(float val) {
			truckCityTrafficSensitivity = val;
			if (truckCityTrafficSensitivitySlider != null)
				truckCityTrafficSensitivitySlider.value = val;
		}

		public static void setCarHighwayTrafficSensitivity(float val) {
			carHighwayTrafficSensitivity = val;
			if (carHighwayTrafficSensitivitySlider != null)
				carHighwayTrafficSensitivitySlider.value = val;
		}

		public static void setCarCityTrafficSensitivity(float val) {
			carCityTrafficSensitivity = val;
			if (carCityTrafficSensitivitySlider != null)
				carCityTrafficSensitivitySlider.value = val;
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

		public static void setPathCostMultiplicator(float newPathCostMultiplicator) {
			pathCostMultiplicator = newPathCostMultiplicator;
			if (pathCostMultiplicatorField != null)
				pathCostMultiplicatorField.text = newPathCostMultiplicator.ToString();
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
			switch (laneChangingRandomization) {
				case 0:
					return 2;
				case 1:
					return 4;
				case 2:
					return 10;
				case 3:
					return 20;
				case 4:
					return 40;
			}
			return 100;
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
