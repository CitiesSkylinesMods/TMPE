using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic.Data;
using UnityEngine;

namespace TrafficManager.Manager {
	public interface IExtCitizenInstanceManager {
		// TODO define me!
		void ResetInstance(ushort instanceId);
		/// <summary>
		/// Determines whether the given citizen instance is located at an outside connection based on the given start position.
		/// </summary>
		/// <param name="instanceId">citizen instance id</param>
		/// <param name="instanceData">citizen instance data</param>
		/// <param name="extInstance">extended citizen instance data</param>
		/// <param name="startPos">start position</param>
		/// <returns><code>true</code> if the citizen instance is located at an outside connection, <code>false</code> otherwise</returns>
		bool IsAtOutsideConnection(ushort instanceId, ref CitizenInstance instanceData, ref ExtCitizenInstance extInstance, Vector3 startPos);
	}
}
