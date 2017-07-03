using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager {
	public interface IVehicleBehaviorManager {
		// TODO define me!
		// TODO documentation

		/// <summary>
		/// Checks if space reservation at <paramref name="targetPos"/> is allowed. When a custom traffic light is active at the transit node
		/// space reservation is only allowed if the light is not red.
		/// </summary>
		/// <param name="transitNodeId">transition node id</param>
		/// <param name="sourcePos">source path position</param>
		/// <param name="targetPos">target path position</param>
		/// <returns></returns>
		bool IsSpaceReservationAllowed(ushort transitNodeId, PathUnit.Position sourcePos, PathUnit.Position targetPos);
	}
}
