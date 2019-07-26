using CSUtil.Commons;
using ICities;
using System.Reflection;
using ColossalFramework.UI;
using TrafficManager.State;
using TrafficManager.Util;

namespace TrafficManager
{
    public class TrafficManagerMod : IUserMod
    {
#if LABS
        public const string Branch = "LABS";
#elif DEBUG
        public const string Branch = "DEBUG";
#else
        public const string Branch = "STABLE";
#endif

        public static readonly uint GameVersion = 184803856u;
        public static readonly uint GameVersionA = 1u;
        public static readonly uint GameVersionB = 12u;
        public static readonly uint GameVersionC = 1u;
        public static readonly uint GameVersionBuild = 2u;

        public static readonly string Version = "11.0-alpha4";

        public static readonly string ModName = "TM:PE " + Branch + " " + Version;

        public string Name => ModName;

        public string Description => "Manage your city's traffic";

        public void OnEnabled()
        {
            Log.Info($"TM:PE enabled. Version {Version}, Build {Assembly.GetExecutingAssembly().GetName().Version} {Branch} for game version {GameVersionA}.{GameVersionB}.{GameVersionC}-f{GameVersionBuild}");
            Log.Info($"Enabled TM:PE has GUID {Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId}");

            // check for incompatible mods
            if (UIView.GetAView() != null)
            { // when TM:PE is enabled in content manager
                CheckForIncompatibleMods();
            }
            else
            { // or when game first loads if TM:PE was already enabled
                LoadingManager.instance.m_introLoaded += CheckForIncompatibleMods;
            }
        }

        public void OnDisabled()
        {
            Log.Info("TM:PE disabled.");
            LoadingManager.instance.m_introLoaded -= CheckForIncompatibleMods;
        }

        public void OnSettingsUI(UIHelperBase helper)
        {
            Options.MakeSettings(helper);
        }

        private static void CheckForIncompatibleMods()
        {   
            ModsCompatibilityChecker mcc = new ModsCompatibilityChecker();
            mcc.PerformModCheck();
        }
    }
}
