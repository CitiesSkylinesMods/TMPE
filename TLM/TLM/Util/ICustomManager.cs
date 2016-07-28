using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Util {
	public interface ICustomManager {
		void OnLevelLoaded();
		void OnLevelUnloaded();
	}
}
