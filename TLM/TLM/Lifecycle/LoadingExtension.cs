namespace TrafficManager.Lifecycle {
    using ColossalFramework;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using System.Reflection;

    [UsedImplicitly]
    public class LoadingExtension : LoadingExtensionBase {
        public override void OnLevelLoaded(LoadMode mode) => TMPELifecycle.Instance.Load();
        public override void OnLevelUnloading() => TMPELifecycle.Instance.Unload();
    }
}