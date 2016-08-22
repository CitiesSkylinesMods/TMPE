using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace TrafficManager.Util {
	public class GenericUnsubscriber<T> : IDisposable {
		private List<IObserver<T>> observers;
		private IObserver<T> observer;
		public object lck;

		public GenericUnsubscriber(List<IObserver<T>> observers, IObserver<T> observer, object lck) {
			this.observers = observers;
			this.observer = observer;
			this.lck = lck;
		}

		public void Dispose() {
			if (observer != null) {
				try {
					Monitor.Enter(lck);
					observers.Remove(observer);
				} finally {
					Monitor.Exit(lck);
				}
			}
		}
	}
}
