using System;
using System.Collections.Generic;
using System.Text;

namespace TrafficManager.Util {
	public interface IObservable<T> {
		IDisposable Subscribe(IObserver<T> observer); 
	}
}
