﻿namespace TrafficManager.Util {
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using API.Util;
    using CSUtil.Commons;

    public abstract class GenericObservable<T> : IObservable<T> {
        /// <summary>
        /// Holds a list of observers which are being notified as soon as the managed node's
        /// geometry is updated (but not neccessarily modified)
        /// </summary>
        protected List<IObserver<T>> Observers = new List<IObserver<T>>();

        /// <summary>
        /// Lock object. Acquire this before accessing the HashSets.
        /// </summary>
        protected object ObserverLock = new object();

        /// <summary>
        /// Registers an observer.
        /// </summary>
        /// <param name="observer"></param>
        /// <returns>An unsubscriber</returns>
        public IDisposable Subscribe(IObserver<T> observer) {
            // Log._Debug($"GenericObserable.Subscribe: Subscribing observer {observer} to observable {this}");
            try {
                Monitor.Enter(ObserverLock);
                Observers.Add(observer);
            }
            finally {
                Monitor.Exit(ObserverLock);
            }

            return new GenericUnsubscriber<T>(Observers, observer, ObserverLock);
        }

        /// <summary>
        /// Notifies all observers that the observable object' state has changed
        /// </summary>
        public virtual void NotifyObservers(T subject) {
            // Log._Debug($"GenericObserable.NotifyObservers: Notifying observers of observable {this}");
            // in case somebody unsubscribes while iterating over subscribers
            var myObservers = new List<IObserver<T>>(Observers);

            foreach (IObserver<T> observer in myObservers) {
                try {
                    // Log._Debug($"GenericObserable.NotifyObservers: Notifying observer {observer} of observable {this}");
                    observer.OnUpdate(subject);
                }
                catch (Exception e) {
                    Log.Error("GenericObserable.NotifyObservers: An exception occured while " +
                              $"notifying an observer of observable {this}: {e}");
                }
            }
        }
    } // end class
}