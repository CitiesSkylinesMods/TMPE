namespace TrafficManager.API.Util {
    using System;

    public interface IObservable<T> {
        IDisposable Subscribe(IObserver<T> observer);
    }
}