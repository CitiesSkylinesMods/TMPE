namespace TrafficManager.API.Notifier {
    using System;
    public interface INotifier {
        event Action EventLevelLoaded;

        event Action<OnModifiedEventArgs> EventModified;
    }
}
