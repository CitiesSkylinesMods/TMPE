namespace TrafficManager.Compatibility.Struct {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using TrafficManager.Compatibility.Enum;
    using TrafficManager.Compatibility.Util;
    using static ColossalFramework.Plugins.PluginManager;

    /// <summary>
    /// Descriptor for subscribed/local mods.
    /// </summary>
    public struct ModDescriptor {
        /// <summary>
        /// Assembly name (of the IUserMod assembly).
        /// </summary>
        public readonly string AssemblyName;

        /// <summary>
        /// Assembly version (of the IUserMod assembly).
        /// </summary>
        public readonly Version AssemblyVersion;

        /// <summary>
        /// Assembly Guid (of the IUserMod assembly).
        /// </summary>
        public readonly Guid AssemblyGuid;

        /// <summary>
        /// Value of <c>Name</c> property of the IUserMod.
        /// </summary>
        public readonly string ModName;

        /// <summary>
        /// Workshop ID of the IUserMod.
        /// </summary>
        public readonly ulong ModWorkshopId;

        /// <summary>
        /// If <c>true</c>, the mod is locally installed (not a workshop subscription).
        /// </summary>
        public readonly bool ModIsLocal;

        /// <summary>
        /// Incompatibility severity of the mod.
        /// </summary>
        public readonly Severity Incompatibility;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModDescriptor"/> struct.
        /// </summary>
        /// 
        /// <param name="asmName">Assembly name.</param>
        /// <param name="asmVer">Assembly version.</param>
        /// <param name="asmGuid">Assembly guid.</param>
        /// <param name="modName">Mod name.</param>
        /// <param name="modId">Mod workshop ID.</param>
        /// <param name="modLocal">Is mod local?</param>
        /// <param name="sev">Compatibility severity.</param>
        public ModDescriptor(
            string asmName,
            Version asmVer,
            Guid asmGuid,
            string modName,
            ulong modId,
            bool modLocal,
            Severity sev) {
            AssemblyName = asmName;
            AssemblyVersion = asmVer;
            AssemblyGuid = asmGuid;
            ModName = modName;
            ModWorkshopId = modId;
            ModIsLocal = modLocal;
            Incompatibility = sev;
        }

        /// <summary>
        /// Generates descriptor from a <see cref="PluginInfo"/>.
        /// </summary>
        /// 
        /// <param name="mod">The <see cref="PluginInfo"/> to inspect.</param>
        public static implicit operator ModDescriptor(PluginInfo mod) {
            return GetModDescriptorFrom(mod);
        }

        private static ModDescriptor GetModDescriptorFrom(PluginInfo mod) {
            Assembly asm = ModInspector.GetModAssembly(mod);

            // Assembly values

            string asmName = ModInspector.GetAssemblyName(asm);

            Guid asmGuid = ModInspector.GetAssemblyGuid(asm);

            Version asmVer = ModInspector.GetAssemblyVersion(asm);

            // Mod values

            string modName = ModInspector.GetModName(mod);

            ulong modId = mod?.publishedFileID.AsUInt64 ?? 0;

            // If workshop id is ulong.MaxValue, it's a locally installed mod
            bool modLocal = modId == ulong.MaxValue;

            Severity severity;

            if (IncompatibleMods.Instance.List.TryGetValue(modId, out Severity s)) {
                severity = s;
            } else if (asmName == "TrafficManager") {
                severity = Severity.Candidate;
            } else {
                severity = Severity.None;
            }

            return new ModDescriptor(
                asmName,
                asmVer,
                asmGuid,

                modName,
                modId,
                modLocal,

                severity);
        }
    }
}
