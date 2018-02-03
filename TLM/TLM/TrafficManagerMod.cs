using CSUtil.Commons;
using ICities;
using System.Reflection;
using TrafficManager.State;
using UnityEngine;

namespace TrafficManager {
	public class TrafficManagerMod : IUserMod {

		public static readonly string Version = "1.10.6-alpha2";

		public static readonly uint GameVersion = 172221200u;
		public static readonly uint GameVersionA = 1u;
		public static readonly uint GameVersionB = 9u;
		public static readonly uint GameVersionC = 1u;
		public static readonly uint GameVersionBuild = 3u;

		public string Name => "Traffic Manager: President Edition [" + Version + "]";

		public string Description => "Manage your city's traffic";

		public void OnEnabled() {
			Log.Info($"Traffic Manager: President Edition enabled. Version {Version}, Build {Assembly.GetExecutingAssembly().GetName().Version} for game version {GameVersionA}.{GameVersionB}.{GameVersionC}-f{GameVersionBuild}");
		}

		public void OnDisabled() {
			Log.Info("Traffic Manager: President Edition disabled.");
		}

		public void OnSettingsUI(UIHelperBase helper) {
			Options.makeSettings(helper);
		}
	}
}
