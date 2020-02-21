namespace TrafficManager.Compatibility.Util {
    using static ColossalFramework.Plugins.PluginManager;
    using System;
    using System.Reflection;
    using TrafficManager.Compatibility.Struct;
    using ICities;
    using CSUtil.Commons;
    using TrafficManager.Compatibility.Enum;

    public class ModInspector {

        /// <summary>
        /// Game always uses ulong.MaxValue to depict local mods.
        /// </summary>
        internal const ulong LOCAL_MOD_ID = ulong.MaxValue;

        /// <summary>
        /// Obtain the <see cref="Assembly"/> for a <see cref="PluginInfo"/>.
        /// </summary>
        /// 
        /// <param name="mod">The <see cref="PluginInfo"/> to inspect.</param>
        /// 
        /// <returns>Returns the <see cref="Assembly"/> if successful, otherwise <c>null</c>.</returns>
        internal static Assembly GetModAssembly(PluginInfo mod) {
            return mod?.userModInstance?.GetType().Assembly;
        }

        /// <summary>
        /// Returns the name of an <see cref="Assembly"/>.
        /// </summary>
        /// 
        /// <param name="asm">The <see cref="Assembly"/> to inspect.</param>
        /// 
        /// <returns>Returns the name if successful, otherwise <c>null</c>.</returns>
        internal static string GetAssemblyName(Assembly asm) {
            return asm?.GetName().Name;
        }

        /// <summary>
        /// Obtain the assembly name for a <see cref="PluginInfo"/>.
        /// </summary>
        /// 
        /// <param name="mod">The <see cref="PluginInfo"/> to inspect.</param>
        /// 
        /// <returns>Returns the name if successful, otherwise <c>null</c>.</returns>
        internal static string GetAssemblyName(PluginInfo mod) {
            return GetAssemblyName(GetModAssembly(mod));
        }

        /// <summary>
        /// Obtain the <see cref="Guid"/> for an <see cref="Assembly"/>.
        /// </summary>
        /// 
        /// <param name="asm">The <see cref="Assembly"/> to inspect.</param>
        /// 
        /// <returns>Returns the <see cref="Guid"/> if successful, otherwise <c>default</c>.</returns>
        internal static Guid GetAssemblyGuid(Assembly asm) {
            return asm?.ManifestModule.ModuleVersionId ?? default;
        }

        /// <summary>
        /// Obtain the assembly <see cref="Guid"/> for a <see cref="PluginInfo"/>.
        /// </summary>
        /// 
        /// <param name="mod">The <see cref="PluginInfo"/> to inspect.</param>
        /// 
        /// <returns>Returns the <see cref="Guid"/> if successful, otherwise <c>default</c>.</returns>
        internal static Guid GetAssemblyGuid(PluginInfo mod) {
            return GetAssemblyGuid(GetModAssembly(mod));
        }

        internal static Version GetAssemblyVersion(Assembly asm) {
            return asm?.GetName().Version;
        }

        internal static Version GetAssemblyVersion(PluginInfo mod) {
            return GetAssemblyVersion(GetModAssembly(mod));
        }



        internal static string GetModName(PluginInfo mod) {
            return mod?.userModInstance != null ? ((IUserMod)mod.userModInstance).Name : string.Empty;
        }

        /// <summary>
        /// ONLY USE FOR MODS WE KNOW DON'T HAVE STATIC OR INSTANCE CONSTRUCTORS ON THEIR IUSERMOD CLASS.
        /// </summary>
        /// 
        /// <param name="mod">The <see cref="PluginInfo"/> to inspect.</param>
        /// <returns></returns>
        internal static Version GetModVersion(PluginInfo mod) {
            if (TryGetModVersion(mod, out Version ver)) {
                return ver;
            } else {
                return default;
            }
        }

        internal static bool TryGetModVersion(PluginInfo mod, out Version ver) {
            ver = default;

            if (mod == null || mod.userModInstance == null) {
                return false;
            }

            return true;
        }

    }
}
