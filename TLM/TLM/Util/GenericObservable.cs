namespace TrafficManager.Util {
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using TrafficManager.API.Util;

    public abstract class GenericObservable<T> : IObservable<T> {
        /// <summary>
        /// Holds a list of observers which are being notified as soon as the managed node's
        /// geometry is updated (but not necessarily modified)
        /// </summary>
        protected List<IObserver<T>> _observers = new List<IObserver<T>>();

        /// <summary>
        /// Lock object. Acquire this before accessing the HashSets.
        /// </summary>
        protected object _lock = new object();

        /// <summary>
        /// Registers an observer.
        /// </summary>
        /// <param name="observer"></param>
        /// <returns>An unsubscriber</returns>
        public IDisposable Subscribe(IObserver<T> observer) {
            lock (_lock) {
                _observers.Add(observer);
            }

            return new GenericUnsubscriber<T>(_observers, observer, _lock);
        }

        /// <summary>
        /// Notifies all observers that the observable object' state has changed
        /// </summary>
        public virtual void NotifyObservers(T subject) {
            lock (_lock) {
                foreach (IObserver<T> observer in _observers) {
                    try {
                        observer.OnUpdate(subject);
                    }
                    catch (Exception e) {
                        Log.Error("GenericObserable.NotifyObservers: An exception occurred while " +
                                  $"notifying an observer of observable {this}: {e}");
                    }
                }
            }
        }
    } // end class
}