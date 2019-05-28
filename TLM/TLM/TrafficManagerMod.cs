using CSUtil.Commons;
using ICities;
using System.Reflection;
using System.Runtime.CompilerServices;
using ColossalFramework;
using ColossalFramework.UI;
using TrafficManager.State;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager {
	public class TrafficManagerMod : IUserMod {

		public static readonly string Version = "10.20";

		public static readonly uint GameVersion = 184673552u;
		public static readonly uint GameVersionA = 1u;
		public static readonly uint GameVersionB = 12u;
		public static readonly uint GameVersionC = 0u;
		public static readonly uint GameVersionBuild = 5u;

		public string Name => "TM:PE " + Version;

		public string Description => "Manage your city's traffic";

		public void OnEnabled() {
			Log.Info($"TM:PE enabled. Version {Version}, Build {Assembly.GetExecutingAssembly().GetName().Version} for game version {GameVersionA}.{GameVersionB}.{GameVersionC}-f{GameVersionBuild}");
			if (UIView.GetAView() != null) {
				OnGameIntroLoaded();
			} else {
				LoadingManager.instance.m_introLoaded += OnGameIntroLoaded;
			}
		}

		public void OnDisabled() {
			Log.Info("TM:PE disabled.");
			LoadingManager.instance.m_introLoaded -= OnGameIntroLoaded;
		}

		public void OnSettingsUI(UIHelperBase helper) {
			Options.makeSettings(helper);
		}

		private static void OnGameIntroLoaded() {
            if (GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup) {
                ModsCompatibilityChecker mcc = new ModsCompatibilityChecker();
                mcc.PerformModCheck();
            }
		}
	}
}
