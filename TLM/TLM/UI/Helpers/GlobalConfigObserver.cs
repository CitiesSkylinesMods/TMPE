namespace TrafficManager.UI.Helpers {
    using System;
    using API.Util;
    using CSUtil.Commons;
    using State;
    using UnityEngine;

    /// <summary>
    /// GlobalConfig observer implementation, can be attached to gameObject,
    /// it will destroy itself before GameObject is going to be destroyed
    /// </summary>
    public class GlobalConfigObserver : MonoBehaviour, IObserver<GlobalConfig> {
        private IDisposable _configSubscription;

        public event Action<GlobalConfig> OnUpdateObservers = delegate {};

        public void Awake() {
            _configSubscription = GlobalConfig.Instance.Subscribe(this);
        }

        private void OnDestroy() {
            Log._Debug("Destroying GlobalConfigObserver");
            _configSubscription.Dispose();
            _configSubscription = null;
            OnUpdateObservers = null;
        }

        public void OnUpdate(GlobalConfig newConfig) {
            Log._Debug("GlobalConfigObserver.OnUpdate() called!");
            OnUpdateObservers(newConfig);
        }
    }
}