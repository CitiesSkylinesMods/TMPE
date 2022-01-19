namespace TrafficManager.State {
    using System;
    using JetBrains.Annotations;
    using Util;

    [Serializable]
    public class VersionInfoConfiguration {
        public VersionInfo VersionInfo = new VersionInfo(VersionUtil.ModVersion);
    }

    [Serializable]
    public class VersionInfo {
        public Version assemblyVersion;
        public ReleaseType releaseType;

        public VersionInfo(Version assemblyVersion) {
            this.assemblyVersion = assemblyVersion;
#if DEBUG
            releaseType = ReleaseType.Debug;
#elif TEST
            releaseType = ReleaseType.Test;
#else
            releaseType = ReleaseType.Stable;
#endif
        }

    }

    public enum ReleaseType {
        Test,
        Debug,
        Stable,
    }
}