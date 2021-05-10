namespace TrafficManager.Lifecycle {
    using ICities;
    using JetBrains.Annotations;

    [UsedImplicitly]
    public class LoadingExtension : LoadingExtensionBase {
        public override void OnLevelLoaded(LoadMode mode) => TMPELifecycle.Instance.Load();
        public override void OnLevelUnloading() => TMPELifecycle.Instance.Unload();
    }
}