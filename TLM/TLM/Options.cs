using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using ColossalFramework;
using ColossalFramework.UI;
using ICities;
using TrafficManager.Traffic;

namespace TrafficManager {

	public class Options : MonoBehaviour {
		private static UIDropDown simAccuracyDropdown = null;
		private static UIDropDown laneChangingRandomizationDropdown = null;
		private static UIDropDown recklessDriversDropdown = null;

		public static int simAccuracy = 1;
		public static int laneChangingRandomization = 5;
		public static int recklessDrivers = 3;

		public static void makeSettings(UIHelperBase helper) {
			UIHelperBase group = helper.AddGroup("Traffic Manager: President Edition (Settings are defined for each savegame separately)");
			simAccuracyDropdown = group.AddDropdown("Simulation accuracy (higher accuracy reduces performance):", new string[] { "Very high", "High", "Medium", "Low", "Very Low" }, simAccuracy, onSimAccuracyChanged) as UIDropDown;
			laneChangingRandomizationDropdown = group.AddDropdown("Vehicles may change lanes (BETA feature):", new string[] { "Very often (25 %)", "Often (12.5 %)", "Sometimes (10 %)", "Rarely (5 %)", "Very rarely (2.5 %)", "Use game default" }, laneChangingRandomization, onLaneChangingRandomizationChanged) as UIDropDown;
			recklessDriversDropdown = group.AddDropdown("Reckless driving (BETA feature):", new string[] { "Path Of Evil (10 %)", "Rush Hour (5 %)", "Minor Complaints (2 %)", "The Holy City (0 %)" }, recklessDrivers, onRecklessDriversChanged) as UIDropDown;
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

		internal static bool isStockLaneChangerUsed() {
			return laneChangingRandomization == 5;
		}

		internal static int getLaneChangingRandomizationTargetValue() {
			switch (laneChangingRandomization) {
				case 0:
					return 4;
				case 1:
					return 8;
				case 2:
					return 10;
				case 3:
					return 20;
				case 4:
					return 40;
			}
			return 1000;
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
					return 100000;
			}
			return 100000;
		}
	}
}
