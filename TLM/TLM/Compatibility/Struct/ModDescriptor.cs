namespace TrafficManager.Compatibility.Struct {
    using static ColossalFramework.Plugins.PluginManager;
    using System;
    using System.IO;
    using System.Reflection;
    using TrafficManager.Compatibility.Enum;
    using TrafficManager.Compatibility.Util;

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
            return From(mod);
        }

        private static ModDescriptor From(PluginInfo mod) {
            Assembly asm = ModInspector.GetModAssembly(mod);

            string asmName = ModInspector.GetAssemblyName(asm);

            Guid asmGuid = ModInspector.GetAssemblyGuid(asm);

            ulong modId = mod?.publishedFileID.AsUInt64 ?? 0;

            // If workshop id is ulong.MaxValue, it's a locally installed mod
            bool modLocal = modId == ulong.MaxValue;

            string modName = modLocal
                ? $"{ModInspector.GetModName(mod)} /{Path.GetFileName(mod.modPath)}"
                : ModInspector.GetModName(mod);

            Severity severity;

            if (IncompatibleMods.Instance.List.TryGetValue(modId, out Severity s)) {
                severity = s;
            } else if (asmName == "TrafficManager") {
                // Detect currently unknown or local builds of TM:PE.
                // Assume anything newer than v11 LABS (aubergine18) is safe,
                // anything older is rogue or obsolete.
                // Local builds are treated as newer due to ulong.MaxValue id.
                severity = modId > 1806963141u
                    ? Severity.TMPE
                    : Severity.Critical;
            } else {
                severity = Severity.None;
            }

            // Show Guid for potentially valid TM:PE mods.
            if (severity == Severity.TMPE) {
                modName += $" - {asmGuid}";
            }

            return new ModDescriptor(
                asmName,
                ModInspector.GetAssemblyVersion(asm),
                asmGuid,

                modName,
                modId,
                modLocal,

                severity);
        }
    }
}
