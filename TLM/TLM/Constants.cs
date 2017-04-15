using GenericGameBridge.Factory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager {
	public static class Constants {
		public static IServiceFactory ServiceFactory {
			get {
#if UNITTEST
				return TestGameBridge.Factory.ServiceFactory.Instance;
#else
				return CitiesGameBridge.Factory.ServiceFactory.Instance;
#endif
			}
		}
	}
}
