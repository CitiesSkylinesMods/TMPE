using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager {
	public interface IExtCitizenInstanceManager {
		// TODO define me!
		void ResetInstance(ushort instanceId);

		/// <summary>
		/// Handles a released citizen instance.
		/// </summary>
		/// <param name="instanceId">citizen instance id</param>
		void OnReleaseInstance(ushort instanceId);
	}
}
