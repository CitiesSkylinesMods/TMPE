using ICities;
using TrafficManager.State;
using UnityEngine;

namespace TrafficManager {
	public class TrafficManagerMod : IUserMod {

		public static readonly string Version = "1.8.16";

		public static readonly uint GameVersion = 159899920u;
		public static readonly uint GameVersionA = 1u;
		public static readonly uint GameVersionB = 6u;
		public static readonly uint GameVersionC = 3u;
		public static readonly uint GameVersionBuild = 1u;

		public string Name => "Traffic Manager: President Edition";

		public string Description => "Manage your city's traffic";

		public void OnEnabled() {
			Log._Debug("TrafficManagerMod Enabled");
		}

		public void OnDisabled() {
			Log._Debug("TrafficManagerMod Disabled");
		}

		public void OnSettingsUI(UIHelperBase helper) {
			Options.makeSettings(helper);
		}
	}
}
