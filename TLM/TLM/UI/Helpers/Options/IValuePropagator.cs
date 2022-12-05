namespace TrafficManager.UI.Helpers {
    /// <summary>
    /// we propagate <c>true</c> when depender* has been enabled.
    /// we propagate <c>false</c> when dependee* has been disabled.
    /// (depender depends on dependee)
    /// </summary>
    public interface IValuePropagator {
        void Propagate(bool value);
        void AddPropagate(IValuePropagator item, bool value);
    }
}
