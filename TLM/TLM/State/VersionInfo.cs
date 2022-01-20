namespace TrafficManager.State {
    using System;
    using JetBrains.Annotations;
    using Util;

    [Serializable]
    public class VersionInfo {
        public Version assemblyVersion;
        public ReleaseType releaseType;

        public VersionInfo(Version assemblyVersion) {
            this.assemblyVersion = assemblyVersion;
            releaseType = VersionUtil.BRANCH;
        }

    }

    [Serializable]
    public class VersionInfoConfiguration {
        public VersionInfo VersionInfo = new VersionInfo(VersionUtil.ModVersion);
    }
}