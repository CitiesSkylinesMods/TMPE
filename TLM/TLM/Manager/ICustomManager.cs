using System;
using System.Collections.Generic;
using System.Text;

namespace TrafficManager.Manager {
	public interface ICustomManager {
		void OnBeforeLoadData();
		void OnAfterLoadData();
		void OnBeforeSaveData();
		void OnAfterSaveData();
		void OnLevelLoading();
		void OnLevelUnloading();
	}
}
