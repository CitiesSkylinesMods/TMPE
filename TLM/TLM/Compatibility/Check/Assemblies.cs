namespace TrafficManager.Compatibility.Check {
    using ColossalFramework;
    using ColossalFramework.Plugins;
    using CSUtil.Commons;
    using ICities;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using static ColossalFramework.Plugins.PluginManager;
    using TrafficManager.Util;

    public class Assemblies {

        /// <summary>
        /// Default build string if we can't determine LABS, STABLE, or DEBUG.
        /// </summary>
        internal const string OBSOLETE = "OBSOLETE";

        internal const string STABLE = "STABLE";

        internal const string BROKEN = "BROKEN";

        internal const string TMMOD = "TrafficManager.TrafficManagerMod";

        internal static readonly Version VersionedByAssembly;

        internal static readonly Version LinuxFanVersion;

        /// <summary>
        /// Initializes static members of the <see cref="Assemblies"/> class.
        /// </summary>
        static Assemblies() {
            VersionedByAssembly = new Version(11, 1, 0);
            LinuxFanVersion = new Version(10, 20);
        }

        public static bool Verify(/*out Dictionary<Assembly,Guid> results*/) {

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly asm in assemblies) {
                AssemblyName details = asm.GetName();
                if (details.Name.Contains("TrafficManager")) {

                    try {
                        //Log.Info("--------------------------- extracting ver info ---------------");

                        if (ExtractVersionDetails(asm, out Version ver, out string build)) {
                            Log.InfoFormat(
                                "Assembly: {0} v{1} {2}",
                                details.Name,
                                ver.Build == -1 ? ver.ToString(2) : ver.ToString(3),
                                build);
                        }

                    } catch (Exception e) {
                        Log.Info("loop failed -----------");
                        Log.Error(e.ToString());
                    }
                }
            }
            return true; // to do
        }

        internal static bool ExtractVersionDetails(Assembly asm, out Version ver, out string branch) {

            ver = asm.GetName().Version;
            branch = OBSOLETE;

            Type type = asm.GetType(TMMOD);
            object instance = Activator.CreateInstance(type);

            if (ver < VersionedByAssembly) {
                try {
                    if (MemberValue.TryGetMemberValue<string>(type, instance, "Version", out string dirty)) {
                        //Log.Info("Raw string: " + dirty);

                        // clean the raw string in to something that resembles a verison number
                        string clean = Regex.Match(dirty, @"[0-9]+(?:\.[0-9]+)+").Value;
                        //Log.Info("clean string: " + clean);

                        // parse in to Version instance
                        ver = new Version(clean);
                    }
                }
                catch {
                    Log.Warning("Unable to retrieve or parse 'Version' member");
                }
            }

            try {
                if (MemberValue.TryGetMemberValue<string>(type, instance, "BRANCH", out string val)) {
                    branch = val;
                } else if (ver == LinuxFanVersion) { // temporary
                    branch = STABLE;
                }
            }
            catch {
                Log.Warning("Unable to retrieve or parse 'BRANCH' member");
            }

            (instance as IDisposable)?.Dispose();

            return true;
        }

    }
}
