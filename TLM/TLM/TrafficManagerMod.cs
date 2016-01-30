using ICities;
using TrafficManager.State;
using UnityEngine;

namespace TrafficManager {
	public class TrafficManagerMod : IUserMod {
		public string Name => "Traffic Manager: President Edition";

		public string Description => "Traffic Manager";

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
