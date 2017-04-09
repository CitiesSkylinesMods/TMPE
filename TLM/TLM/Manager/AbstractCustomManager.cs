using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager {
	/// <summary>
	/// Abstract manager class, supports events before/after loading/saving.
	/// 
	/// Event sequence:
	/// OnBeforeLoadData -> (loading game data) -> OnAfterLoadData -> (setting up detours) -> OnLevelLoading
	/// OnBeforeSaveData -> (saving game data) -> OnAfterSaveData -> (releasing detours) -> OnLevelUnloading
	/// </summary>
	public abstract class AbstractCustomManager : ICustomManager {
		/// <summary>
		/// Performs actions after game data has been loaded
		/// </summary>
		public virtual void OnAfterLoadData() {
			
		}

		/// <summary>
		/// Performs actions after game data has been saved
		/// </summary>
		public virtual void OnAfterSaveData() {
			
		}

		/// <summary>
		/// Performs actions before game data is going to be loaded
		/// </summary>
		public virtual void OnBeforeLoadData() {
			
		}

		/// <summary>
		/// Performs actions before game data is going to be saved
		/// </summary>
		public virtual void OnBeforeSaveData() {
			
		}

		/// <summary>
		/// Performs actions after a game has been loaded
		/// </summary>
		public virtual void OnLevelLoading() {
			
		}

		/// <summary>
		/// Performs actions after a game has been unloaded
		/// </summary>
		public virtual void OnLevelUnloading() {
			
		}
	}
}
