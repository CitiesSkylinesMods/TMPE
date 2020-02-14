namespace TrafficManager.Compatibility {
    using ColossalFramework;
    using ColossalFramework.Plugins;
    using CSUtil.Commons;
    using ICities;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;
    using static ColossalFramework.Plugins.PluginManager;

    public class AssemblyScanner {

        public static bool Scan(/*out Dictionary<Assembly,Guid> results*/) {
            Version v11 = new Version(11, 1, 0);
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly asm in assemblies) {
                AssemblyName details = asm.GetName();
                if (details.Name.Contains("TrafficManager")) {
                    Version version = details.Version;

                    // asm.ManifestModule.ModuleVersionId
                    //mod.userModInstance.GetType().Assembly.GetName().

                    try {
                        PluginInfo mod = Singleton<PluginManager>.instance.FindPluginInfo(asm);

                        Log.InfoFormat(
                            "Assembly: {0} v{1} in {2}",
                            details.Name,
                            version.ToString(3),
                            mod.modPath);
                    } catch {
                        Log.InfoFormat(
                            "Assembly: {0} v{1} in {2}",
                            details.Name,
                            version.ToString(3),
                            "MOD ERROR");
                        
                    }
                }
            }
            return false;
        }

        internal static string GetModVersion(PluginInfo mod) {
            string ver = "meh";
            try {
                //ver = ((IUserMod)mod.userModInstance).Version;
            }
            catch {
                Log.ErrorFormat(
                    "Unable to get userModInstrance.Version for ",
                    mod.modPath);
                ver = "Unknown";
            }
            return ver;
        }
    }
}
