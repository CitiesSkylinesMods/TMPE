using System;
using System.Collections.Generic;
using System.Text;

namespace TrafficManager.Util {
	public interface IObserver<T> {
		void OnUpdate(IObservable<T> observable);
	}
}
