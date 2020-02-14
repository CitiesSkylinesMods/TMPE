namespace TrafficManager.Compatibility {
    using ColossalFramework.PlatformServices;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Scans for transit-affecting DLCs.
    /// </summary>
    public class DlcScanner {

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
                };

                StringBuilder sb = new StringBuilder(500);

                sb.Append("Transit-affecting DLCs [*] = Installed:\n\n");

                string formatStr = " {0} {1}\n";
                string strAsterisk = "*";
                string strSpace = " ";

                foreach (KeyValuePair<uint, string> item in DLCs) {
                    sb.AppendFormat(
                        formatStr,
                        PlatformService.IsDlcInstalled(item.Key) ? strAsterisk : strSpace,
                        item.Value);
                }

                Log.Info(sb.ToString());
            } catch { }
        }
    }
}
