namespace TrafficManager.Compatibility.Checks {
    using ColossalFramework.PlatformServices;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Scans for transit-affecting DLCs.
    /// </summary>
    public class CheckDLCs {

        /// <summary>
        /// Scan for DLCs and log whether they are installed or not.
        /// </summary>
        public static void Scan() {

            try {
                Dictionary<uint, string> DLCs = new Dictionary<uint, string>() {
                    { SteamHelper.kAfterDLCAppID, "After Dark" },
                    { SteamHelper.kMotionDLCAppID, "Mass Transit" },
                    { SteamHelper.kWinterDLCAppID, "Snowfall" },
                    { SteamHelper.kNaturalDisastersDLCAppID, "Natural Disasters" },
                    { SteamHelper.kParksDLCAppID, "Park Life" },
                    { SteamHelper.kIndustryDLCAppID, "Industries" },
                    { SteamHelper.kGreenDLCAppID, "Green Cities" },
                    { SteamHelper.kFootballAppID, "Match Day" },
                };

                StringBuilder sb = new StringBuilder(500);

                sb.Append("Transit-affecting DLCs [*] = Installed:\n\n");

                string formatStr = " {0} {1}\n";
                string strAsterisk = "*";
                string strSpace = " ";
                
                foreach (KeyValuePair<uint, string> dlc in DLCs) {
                    sb.AppendFormat(
                        formatStr,
                        PlatformService.IsDlcInstalled(dlc.Key) ? strAsterisk : strSpace,
                        dlc.Value);
                }

                Log.Info(sb.ToString());
            } catch { }
        }
    }
}
