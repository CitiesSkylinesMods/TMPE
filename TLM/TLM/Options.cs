using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using ColossalFramework;
using ColossalFramework.UI;
using ICities;
using TrafficManager.State;

namespace TrafficManager {

	public class Options : MonoBehaviour {
		private static UIDropDown simAccuracyDropdown = null;
		private static UIDropDown laneChangingRandomizationDropdown = null;
		private static UIDropDown recklessDriversDropdown = null;
		private static UICheckBox relaxedBussesToggle = null;
		private static UICheckBox nodesOverlayToggle = null;
		private static UICheckBox mayEnterBlockedJunctionsToggle = null;

		public static int simAccuracy = 1;
		public static int laneChangingRandomization = 4;
		public static int recklessDrivers = 3;
		public static bool relaxedBusses = false;
		public static bool nodesOverlay = false;
		public static bool mayEnterBlockedJunctions = false;

		public static void makeSettings(UIHelperBase helper) {
			UIHelperBase group = helper.AddGroup("Traffic Manager: President Edition (Settings are defined for each savegame separately)");
			simAccuracyDropdown = group.AddDropdown("Simulation accuracy (higher accuracy reduces performance):", new string[] { "Very high", "High", "Medium", "Low", "Very Low" }, simAccuracy, onSimAccuracyChanged) as UIDropDown;
			laneChangingRandomizationDropdown = group.AddDropdown("Vehicles may change lanes (BETA feature):", new string[] { "Very often (10 %)", "Often (5 %)", "Sometimes (2.5 %)", "Rarely (1 %)", "Only if neccessary" }, laneChangingRandomization, onLaneChangingRandomizationChanged) as UIDropDown;
			recklessDriversDropdown = group.AddDropdown("Reckless driving (BETA feature):", new string[] { "Path Of Evil (10 %)", "Rush Hour (5 %)", "Minor Complaints (2 %)", "The Holy City (0 %)" }, recklessDrivers, onRecklessDriversChanged) as UIDropDown;
			relaxedBussesToggle = group.AddCheckbox("Busses may ignore lane arrows", relaxedBusses, onRelaxedBussesChanged) as UICheckBox;
			mayEnterBlockedJunctionsToggle = group.AddCheckbox("Vehicles may enter blocked junctions", mayEnterBlockedJunctions, onMayEnterBlockedJunctionsChanged) as UICheckBox;
			UIHelperBase group2 = helper.AddGroup("Maintenance");
			group2.AddButton("Forget toggled traffic lights", onClickForgetToggledLights);
			nodesOverlayToggle = group2.AddCheckbox("Show nodes and segments", nodesOverlay, onNodesOverlayChanged) as UICheckBox;
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

		private static void onMayEnterBlockedJunctionsChanged(bool newMayEnterBlockedJunctions) {
			Log.Message($"MayEnterBlockedJunctions changed to {newMayEnterBlockedJunctions}");
			mayEnterBlockedJunctions = newMayEnterBlockedJunctions;
		}

		private static void onNodesOverlayChanged(bool newNodesOverlay) {
			Log.Message($"Nodes overlay changed to {newNodesOverlay}");
			nodesOverlay = newNodesOverlay;
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

		public static void setRelaxedBusses(bool newRelaxedBusses) {
			relaxedBusses = newRelaxedBusses;
			if (relaxedBussesToggle != null)
				relaxedBussesToggle.isChecked = newRelaxedBusses;
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
					return 10;
				case 1:
					return 20;
				case 2:
					return 40;
				case 3:
					return 100;
				case 4:
					return 0;
			}
			return 0;
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
	}
}
