using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using ColossalFramework;
using ColossalFramework.UI;
using ICities;

namespace TrafficManager {

	public class Options : MonoBehaviour {
		private static UIDropDown simAccuracyDropdown = null;

		public static int simAccuracy = 1;
		
		public static void makeSettings(UIHelperBase helper) {
			UIHelperBase group = helper.AddGroup("Traffic Manager: President Edition");
			simAccuracyDropdown = group.AddDropdown("Simulation accuracy (affects performance)", new string[] { "Very high", "High", "Medium", "Low", "Very Low" }, simAccuracy, onSimAccuracyChanged) as UIDropDown;
		}

		private static void onSimAccuracyChanged(int newAccuracy) {
			Log.Message($"Simulation accuracy changed to {newAccuracy}");
			simAccuracy = newAccuracy;
		}

		public static void setSimAccuracy(int newAccuracy) {
			simAccuracy = newAccuracy;
			simAccuracyDropdown.selectedIndex = newAccuracy;
		}
	}
}
