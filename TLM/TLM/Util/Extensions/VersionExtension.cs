namespace TrafficManager.Util.Extensions {
    using System;
    using System.Reflection;

    public static class VersionExtension {
        internal static Version VersionOf(this Assembly asm) => asm.GetName().Version;

        internal static Version VersionOf(this Type t) => t.Assembly.GetName().Version;

        internal static Version VersionOf(this object obj) => VersionOf(obj.GetType());
    }
}
