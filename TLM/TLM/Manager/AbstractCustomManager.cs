using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager {
	public abstract class AbstractCustomManager : ICustomManager {
		public virtual void OnAfterLoadData() {
			
		}

		public virtual void OnAfterSaveData() {
			
		}

		public virtual void OnBeforeLoadData() {
			
		}

		public virtual void OnBeforeSaveData() {
			
		}

		public virtual void OnLevelLoading() {
			
		}

		public virtual void OnLevelUnloading() {
			
		}
	}
}
