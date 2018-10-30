using GenericGameBridge.Factory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Manager;

namespace TrafficManager {
	public static class Constants {
		public static readonly bool[] ALL_BOOL = new bool[] { false, true };
		public const float BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR = 0.003921569f;

		public static IServiceFactory ServiceFactory {
			get {
#if UNITTEST
				return TestGameBridge.Factory.ServiceFactory.Instance;
#else
				return CitiesGameBridge.Factory.ServiceFactory.Instance;
#endif
			}
		}

		public static IManagerFactory ManagerFactory {
			get {
				return Manager.Impl.ManagerFactory.Instance;
			}
		}
	}
}
