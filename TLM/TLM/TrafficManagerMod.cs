using CSUtil.Commons;
using ICities;
using System.Reflection;
using ColossalFramework.UI;
using TrafficManager.State;
using TrafficManager.Util;

namespace TrafficManager {
    public class TrafficManagerMod : IUserMod {
        public static readonly uint GameVersion = 184673552u;
        public static readonly uint GameVersionA = 1u;
        public static readonly uint GameVersionB = 12u;
        public static readonly uint GameVersionC = 0u;
        public static readonly uint GameVersionBuild = 5u;

        // Note: `Version` is also used in UI/MainMenu/VersionLabel.cs
        public static readonly string Version = "10.20";

#if LABS
        public string Branch => "LABS";
#elif DEBUG
        public string Branch => "DEBUG";
#else
        public string Branch => "STABLE";
#endif

        public string Name => "TM:PE " + Version + " " + Branch;

        public string Description => "Manage your city's traffic";

        public void OnEnabled() {
			      Log.Info($"TM:PE enabled. Version {Version}, Build {Assembly.GetExecutingAssembly().GetName().Version} {Branch} for game version {GameVersionA}.{GameVersionB}.{GameVersionC}-f{GameVersionBuild}");

            // check for incompatible mods
            if (UIView.GetAView() != null) { // when TM:PE is enabled in content manager
                CheckForIncompatibleMods();
			      } else { // or when game first loads if TM:PE was already enabled
				        LoadingManager.instance.m_introLoaded += CheckForIncompatibleMods;
			      }
        }

        public void OnDisabled() {
			      Log.Info("TM:PE disabled.");
			      LoadingManager.instance.m_introLoaded -= CheckForIncompatibleMods;
		    }

        public void OnSettingsUI(UIHelperBase helper) {
            Options.MakeSettings(helper);
        }

		    private static void CheckForIncompatibleMods() {
            if (GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup) {
                ModsCompatibilityChecker mcc = new ModsCompatibilityChecker();
                mcc.PerformModCheck();
            }
        }
    }
}