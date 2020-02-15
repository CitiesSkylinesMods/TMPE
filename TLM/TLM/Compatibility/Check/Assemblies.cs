namespace TrafficManager.Compatibility.Check {
    using ColossalFramework;
    using ColossalFramework.Plugins;
    using CSUtil.Commons;
    using ICities;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using static ColossalFramework.Plugins.PluginManager;

    public class Assemblies {

        /// <summary>
        /// Used to retrieve <c>Version</c> property.
        /// </summary>
        internal const BindingFlags PUBLIC_STATIC = BindingFlags.Public | BindingFlags.Static;

        /// <summary>
        /// Default build string if we can't determine LABS, STABLE, or DEBUG.
        /// </summary>
        internal const string OBSOLETE = "OBSOLETE";

        internal static readonly Version VersionedByAssembly;

        /// <summary>
        /// Initializes static members of the <see cref="Assemblies"/> class.
        /// </summary>
        static Assemblies() {
            VersionedByAssembly = new Version(11, 1, 0);
        }

        public static bool Verify(/*out Dictionary<Assembly,Guid> results*/) {

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly asm in assemblies) {
                AssemblyName details = asm.GetName();
                if (details.Name.Contains("TrafficManager")) {
                    Log.Info("--------------------------- extracting ver info ---------------");

                    ExtractVersionDetails(asm, out Version ver, out string build);

                    Log.InfoFormat(
                        "-- {0} v{1} {2}",
                        details.Name,
                        ver.ToString(3),
                        build);
                }
            }
            return true; // to do
        }

        internal static void ExtractVersionDetails(Assembly asm, out Version ver, out string build) {

            Type mod = asm.GetType("TrafficManager.TrafficManagerMod");

            ver = asm.GetName().Version;

            if (ver < VersionedByAssembly) {
                try {
                    // get dirty version string, which may include stuff like ` hotfix`, `-alpha1`, etc.

                    string dirty = string.Empty;

                    MemberInfo[] results = mod
                        .GetMember("Version", PUBLIC_STATIC);

                    if (results.Length > 0) {
                        MemberInfo result = results[0];

                        if (result.MemberType == MemberTypes.Property) {
                            dirty = mod
                                .GetProperty("Version", PUBLIC_STATIC)
                                .GetValue(mod, null)
                                .ToString();
                        } else if (result.MemberType == MemberTypes.Field) {
                            dirty = mod
                                .GetField("Version", PUBLIC_STATIC)
                                .GetValue(mod)
                                .ToString();
                        } else if (result.MemberType == MemberTypes.Method) {
                            dirty = mod
                                .GetMethod("Version", PUBLIC_STATIC)
                                .Invoke(null, null)
                                .ToString();
                        }
                    }

                    if (!string.IsNullOrEmpty(dirty)) {
                        Log.Info("Raw string: " + dirty);
                        // clean the raw string in to something that resembles a verison number
                        string clean = Regex.Match(dirty, @"[0-9]+(?:\.[0-9]+)+").Value;
                        Log.Info("clean string: " + clean);
                        // parse in to Version instance
                        ver = new Version(clean);
                    }
                }
                catch (Exception e) {
                    Log.Error(e.ToString());
                    // use the assembly version we already have
                }
            }

            build = OBSOLETE;

            try {
                build = mod
                    .GetField("BRANCH", PUBLIC_STATIC)
                    .GetValue(mod)
                    .ToString();
            } catch {
                // treat as obsolete
            }

        }

    }
}
