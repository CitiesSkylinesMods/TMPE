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

                    try {
                        //Log.Info("--------------------------- extracting ver info ---------------");

                        if (ExtractVersionDetails(asm, out Version ver, out string build)) {
                            Log.InfoFormat(
                                "-- {0} v{1} {2}",
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

        internal static MemberTypes GetMemberType(Type mod, string memberName) {
            MemberInfo[] results = mod
                .GetMember(memberName, PUBLIC_STATIC);

            if (results.Length == 0) {
                return MemberTypes.Custom; // couldn't think of anything better to use
            } else {
                return results[0].MemberType;
            }
        }

        internal static bool ExtractVersionDetails(Assembly asm, out Version ver, out string branch) {

            Type mod = asm.GetType("TrafficManager.TrafficManagerMod");

            if (mod == null) {
                ver = new Version(0, 0, 0);
                branch = "BROKEN";
                return false;
            } else {
                ver = asm.GetName().Version;
                branch = OBSOLETE;
            }

            MemberTypes memberType;

            if (ver < VersionedByAssembly) {
                try {
                    // get dirty version string, which may include stuff like ` hotfix`, `-alpha1`, etc.

                    string dirty = string.Empty;

                    memberType = GetMemberType(mod, "Version");

                    if (memberType == MemberTypes.Property) {
                        //Log.Info("It's a property");
                        dirty = mod
                            .GetProperty("Version", PUBLIC_STATIC)
                            .GetValue(mod, null)
                            .ToString();
                    } else if (memberType == MemberTypes.Field) {
                        //Log.Info("It's a field");
                        dirty = mod
                            .GetField("Version", PUBLIC_STATIC)
                            .GetValue(mod)
                            .ToString();
                    } else if (memberType == MemberTypes.Method) {
                        //Log.Info("It's a method");
                        dirty = mod
                            .GetMethod("Version", PUBLIC_STATIC)
                            .Invoke(null, null)
                            .ToString();
                    } else {
                        //Log.Info("Version: Unsupported member type or not found");
                    }

                    if (!string.IsNullOrEmpty(dirty)) {
                        //Log.Info("Raw string: " + dirty);
                        // clean the raw string in to something that resembles a verison number
                        string clean = Regex.Match(dirty, @"[0-9]+(?:\.[0-9]+)+").Value;
                        //Log.Info("clean string: " + clean);
                        // parse in to Version instance
                        ver = new Version(clean);
                    }
                } catch (Exception e) {
                    Log.Info("version failed ----------");
                    Log.Error(e.ToString());
                    // use the assembly version we already have
                }
            }

            try {
                memberType = GetMemberType(mod, "BRANCH");

                if (memberType == MemberTypes.Property) {
                    //Log.Info("It's a property");
                    branch = mod
                        .GetProperty("BRANCH", PUBLIC_STATIC)
                        .GetValue(mod, null)
                        .ToString();
                } else if (memberType == MemberTypes.Field) {
                    //Log.Info("It's a field");
                    branch = mod
                        .GetField("BRANCH", PUBLIC_STATIC)
                        .GetValue(mod)
                        .ToString();
                } else if (memberType == MemberTypes.Method) {
                    //Log.Info("It's a method");
                    branch = mod
                        .GetMethod("BRANCH", PUBLIC_STATIC)
                        .Invoke(null, null)
                        .ToString();
                } else {
                    //Log.Info("BRANCH: Unsupported member type or not found");
                }

            } catch (Exception e) {
                Log.Info("branch failed ----------");
                Log.Error(e.ToString());
                // treat as obsolete
            }

            return true;
        }

    }
}
