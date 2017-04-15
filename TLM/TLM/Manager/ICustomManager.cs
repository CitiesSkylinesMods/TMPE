using GenericGameBridge.Factory;
using System;
using System.Collections.Generic;
using System.Text;

namespace TrafficManager.Manager {
	public interface ICustomManager {
		IServiceFactory Services { get; }
		void OnBeforeLoadData();
		void OnAfterLoadData();
		void OnBeforeSaveData();
		void OnAfterSaveData();
		void OnLevelLoading();
		void OnLevelUnloading();
		void PrintDebugInfo();
	}
}
