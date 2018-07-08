using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager {
	public interface IExtCitizenInstanceManager {
		// TODO define me!
		void ResetInstance(ushort instanceId);
		/// <summary>
		/// Determines if the given citizen instance is located at an outside connection.
		/// </summary>
		/// <param name="instanceId">citizen instance id</param>
		/// <param name="instanceData">citizen instance data</param>
		/// <param name="citizenData">citizen data</param>
		/// <returns><code>true</code> if the citizen instance is located at an outside connection, <code>false</code> otherwise</returns>
		bool IsAtOutsideConnection(ushort instanceId, ref CitizenInstance instanceData, ref Citizen citizenData);
	}
}
