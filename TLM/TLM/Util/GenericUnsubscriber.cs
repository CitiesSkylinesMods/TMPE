namespace TrafficManager.Util {
    using System.Collections.Generic;
    using System.Threading;
    using System;
    using TrafficManager.API.Util;

    public class GenericUnsubscriber<T> : IDisposable {
        private readonly List<IObserver<T>> observers_;
        private readonly IObserver<T> observer_;
        private readonly object lck_;

        public GenericUnsubscriber(List<IObserver<T>> observers,
                                   IObserver<T> observer,
                                   object lck) {
            observers_ = observers;
            observer_ = observer;
            lck_ = lck;
        }

        public void Dispose() {
            if (observer_ == null) {
                return;
            }

            try {
                Monitor.Enter(lck_);
                observers_.Remove(observer_);
            }
            finally {
                Monitor.Exit(lck_);
            }
        }
    }
}