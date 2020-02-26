namespace TrafficManager.Compatibility.Check {
    using ColossalFramework.PlatformServices;
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Scans for transit-affecting DLCs.
    /// </summary>
    public class DLCs {

        // Strings for log entries
        internal const string LOG_ENTRY_FORMAT = " {0} {1}\n";
        internal const string MARKER_ENABLED = "*";
        internal const string MARKER_BLANK = " ";

        /// <summary>
        /// Scan for DLCs and log whether they are installed or not.
        /// </summary>
        public static void Verify() {

            try {
                Dictionary<uint, string> DLCs = new Dictionary<uint, string>() {
                    { (uint)SteamHelper.DLC.AfterDarkDLC, "After Dark" },
                    { (uint)SteamHelper.DLC.InMotionDLC, "Mass Transit" },
                    { (uint)SteamHelper.DLC.SnowFallDLC, "Snowfall" },
                    { (uint)SteamHelper.DLC.NaturalDisastersDLC, "Natural Disasters" },
                    { (uint)SteamHelper.DLC.ParksDLC, "Park Life" },
                    { (uint)SteamHelper.DLC.IndustryDLC, "Industries" },
                    { (uint)SteamHelper.DLC.GreenCitiesDLC, "Green Cities" },
                    { (uint)SteamHelper.DLC.Football, "Match Day" },
                };

                StringBuilder sb = new StringBuilder(500);

                sb.Append("Transit-affecting DLCs [*] = Installed:\n\n");

                foreach (KeyValuePair<uint, string> dlc in DLCs) {
                    sb.AppendFormat(
                        LOG_ENTRY_FORMAT,
                        PlatformService.IsDlcInstalled(dlc.Key) ? MARKER_ENABLED : MARKER_BLANK,
                        dlc.Value);
                }

                Log.Info(sb.ToString());
            }
            catch (Exception e) {
                Log.ErrorFormat("Error logging DLC\n{0}", e.ToString());
            }
        }
    }
}
